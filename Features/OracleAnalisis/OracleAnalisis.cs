using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.OracleAnalisis;

public record OracleAnalisisQuery(string Conexion, string? Esquema = null, string? Nombre = null,
    string? Tipo = null, string? Buscar = null, int Page = 1, int PageSize = 50,
    int DesdeLinea = 1, int CantidadLineas = 200, string Direccion = "salientes");
public record OracleAnalisisEnvelope(string RequestId, string Ambiente, string Conexion,
    DateTimeOffset ConsultadoUtc, long DuracionMs, object Resultado);

// Scoped exclusively to this feature; normal application JWT authentication is unchanged.
[ApiController]
[Route("api/OracleAnalisis")]
[Authorize(AuthenticationSchemes = OracleAnalisisRules.Scheme)]
public sealed partial class OracleAnalisisController(ConnectionDB db, IConfiguration config,
    IWebHostEnvironment environment, ILogger<OracleAnalisisController> logger) : ControllerBase
{
    private static readonly SemaphoreSlim Gate = new(2);
    private static readonly Queue<DateTimeOffset> Requests = new();
    private static readonly HashSet<string> Types = ["TABLE", "VIEW", "MATERIALIZED VIEW", "INDEX", "SEQUENCE", "PROCEDURE", "FUNCTION", "PACKAGE", "PACKAGE BODY", "TRIGGER", "SYNONYM"];
    private static readonly HashSet<string> SourceTypes = ["PROCEDURE", "FUNCTION", "PACKAGE", "PACKAGE BODY", "TRIGGER", "TYPE", "TYPE BODY"];
    private ResultDto<object> Failure(string code, string message) => new(new { requestId = HttpContext.TraceIdentifier, code }) { IsValid = false, Message = message };
    private bool HasPermission(string permission) => User.HasClaim("permission", permission);

    [HttpPost("conexiones")]
    public IActionResult Connections()
    {
        if (!HasPermission(OracleAnalisisRules.Metadata)) return Forbid(OracleAnalisisRules.Scheme);
        var options = OracleAnalisisOptions.Read(config);
        var aliases = options.Targets.Where(t => OracleAnalisisRules.AliasAllowed(t.Key, options))
            .Select(t => new { conexion = t.Key, esquema = t.Value.Schema, configurada = !string.IsNullOrWhiteSpace(config.GetConnectionString(Key(t.Key))) }).ToList();
        logger.LogInformation("OracleAnalisis {RequestId} {Actor} conexiones {Count}", HttpContext.TraceIdentifier, User.Identity?.Name, aliases.Count);
        return Ok(new ResultDto<object>(new { ambiente = environment.EnvironmentName, version = typeof(OracleAnalisisController).Assembly.GetName().Version?.ToString(), conexiones = aliases, permisos = options.ServiceAccess.Permissions }) { IsValid = true, Message = "OK" });
    }
    private static string Key(string alias) => "DefaultConnection" + alias;

    private async Task<IActionResult> Run(OracleAnalisisQuery query, string permission,
        Func<OracleAnalisisDb, CancellationToken, Task<object>> operation, CancellationToken ct,
        [System.Runtime.CompilerServices.CallerMemberName] string action = "")
    {
        if (!HasPermission(permission)) return Forbid(OracleAnalisisRules.Scheme);
        var watch = Stopwatch.StartNew();
        var entered = false;
        var outcome = "ERROR";
        var responseBytes = 0;
        try
        {
            var options = OracleAnalisisOptions.Read(config);
            if (!OracleAnalisisRules.AliasAllowed(query.Conexion, options))
                throw new OracleAnalisisValidation("CONEXION_NO_AUTORIZADA", "Conexion no autorizada.");
            var target = options.Targets[query.Conexion];
            if (target.Schema is "BMC" or "RHC" || (query.Esquema is not null && query.Esquema != target.Schema))
                throw new OracleAnalisisValidation("ESQUEMA_NO_AUTORIZADO", "Esquema no autorizado para la conexion.");
            if (string.IsNullOrWhiteSpace(config.GetConnectionString(Key(query.Conexion))))
                throw new OracleAnalisisValidation("CONEXION_SIN_CONFIGURAR", "La conexion no esta configurada.");
            lock (Requests)
            {
                var now = DateTimeOffset.UtcNow;
                while (Requests.TryPeek(out var time) && time < now.AddMinutes(-1)) Requests.Dequeue();
                if (Requests.Count >= 30) { outcome = "RATE_LIMIT"; return StatusCode(429, Failure(outcome, "Limite de solicitudes alcanzado.")); }
                Requests.Enqueue(now);
            }
            entered = await Gate.WaitAsync(0, ct);
            if (!entered) { outcome = "BUSY"; return StatusCode(429, Failure(outcome, "Hay consultas en curso. Reintente luego.")); }
            using var scope = new OracleAnalisisDb(OracleAnalisisDb.Connection(db, query.Conexion), options, target);
            await scope.Open(ct);
            var value = await operation(scope, ct);
            var result = new ResultDto<object>(new OracleAnalisisEnvelope(HttpContext.TraceIdentifier,
                environment.EnvironmentName, query.Conexion, DateTimeOffset.UtcNow, watch.ElapsedMilliseconds, value)) { IsValid = true, Message = "OK" };
            responseBytes = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(result).Length;
            if (responseBytes > Math.Clamp(options.MaxResponseBytes, 32768, 2 * 1024 * 1024) - 1024)
                throw new OracleAnalisisValidation("RESPUESTA_DEMASIADO_GRANDE", "La respuesta compuesta supera el limite. Consulte menos objetos o reduzca el tamano de pagina.");
            outcome = "OK";
            return Ok(result);
        }
        catch (OracleAnalisisValidation ex) { outcome = ex.Code; return Ok(Failure(ex.Code, ex.Message)); }
        catch (OperationCanceledException) { outcome = "CANCELADO"; return Ok(Failure(outcome, "Consulta cancelada.")); }
        catch (OracleException ex)
        {
            // Oracle messages can contain SQL literals/credentials. Do not return or log them.
            outcome = "ORACLE_" + ex.Number;
            logger.LogWarning("OracleAnalisis {RequestId} OracleCode={Code}", HttpContext.TraceIdentifier, ex.Number);
            return Ok(Failure(outcome, "No se pudo completar la consulta Oracle. Revise permisos, disponibilidad y limites con la referencia de solicitud."));
        }
        catch (Exception)
        {
            outcome = "ERROR_TECNICO";
            return Ok(Failure(outcome, "No se pudo completar el analisis. Revise el log mediante la referencia de solicitud."));
        }
        finally
        {
            if (entered) Gate.Release();
            logger.LogInformation("OracleAnalisis {RequestId} Actor={Actor} Alias={Alias} Accion={Action} Resultado={Outcome} DuracionMs={Elapsed} Bytes={Bytes}",
                HttpContext.TraceIdentifier, User.Identity?.Name, query.Conexion, action, outcome, watch.ElapsedMilliseconds, responseBytes);
        }
    }
    private static string Name(OracleAnalisisQuery query) => OracleAnalisisRules.Identifier(query.Nombre);
    private static string Type(OracleAnalisisQuery query, HashSet<string> allowed)
    {
        if (query.Tipo is null || !allowed.Contains(query.Tipo))
            throw new OracleAnalisisValidation("TIPO_INVALIDO", "Tipo de objeto no admitido.");
        return query.Tipo;
    }
    private static void Source(OracleAnalisisDb scope, OracleAnalisisQuery query)
    {
        var name = Name(query);
        if (!scope.Target.SourceObjects.Contains(name, StringComparer.Ordinal))
            throw new OracleAnalisisValidation("FUENTE_NO_AUTORIZADA", "La fuente no esta autorizada en SourceObjects.");
    }

    [HttpPost("verificarConexion")]
    public Task<IActionResult> Verify(OracleAnalisisQuery query, CancellationToken ct) =>
        Run(query, OracleAnalisisRules.Metadata, (scope, _) => Task.FromResult<object>(scope.Identity), ct);

    [HttpPost("objetos")]
    public Task<IActionResult> Objects(OracleAnalisisQuery query, CancellationToken ct) => Run(query, OracleAnalisisRules.Metadata, async (scope, token) =>
    {
        if (query.Page is < 1 or > 10000 || query.PageSize is < 1 or > 500 || query.Buscar?.Length > 100)
            throw new OracleAnalisisValidation("PAGINACION_INVALIDA", "Page 1..10000, PageSize 1..500 y busqueda hasta 100 caracteres.");
        if (query.Tipo is not null) Type(query, Types);
        var size = Math.Clamp(query.PageSize, 1, Math.Clamp(OracleAnalisisOptions.Read(config).MaxRows, 1, 1000));
        var from = (query.Page - 1) * size;
        return await scope.Read("""
            SELECT * FROM (
              SELECT OWNER, OBJECT_NAME, OBJECT_TYPE, STATUS, LAST_DDL_TIME,
                     ROW_NUMBER() OVER (ORDER BY OBJECT_NAME, OBJECT_TYPE) OA_ROW
              FROM ALL_OBJECTS WHERE OWNER=:owner AND SUBOBJECT_NAME IS NULL
                AND OBJECT_TYPE IN ('TABLE','VIEW','MATERIALIZED VIEW','INDEX','SEQUENCE','PROCEDURE','FUNCTION','PACKAGE','PACKAGE BODY','TRIGGER','SYNONYM')
                AND (:kind IS NULL OR OBJECT_TYPE=:kind)
                AND (:search IS NULL OR INSTR(UPPER(OBJECT_NAME), UPPER(:search)) > 0)
            ) WHERE OA_ROW > :from_row ORDER BY OA_ROW
            """, size, token, ("owner", scope.Target.Schema), ("kind", query.Tipo), ("search", query.Buscar), ("from_row", from));
    }, ct);

    [HttpPost("describirObjeto")]
    public Task<IActionResult> Describe(OracleAnalisisQuery query, CancellationToken ct) => Run(query, OracleAnalisisRules.Metadata, async (scope, token) =>
    {
        var name = Name(query);
        var binds = new (string, object?)[] { ("owner", scope.Target.Schema), ("name", name) };
        var columns = await scope.Read("""
            SELECT C.COLUMN_ID, C.COLUMN_NAME, C.DATA_TYPE, C.DATA_LENGTH, C.CHAR_LENGTH,
                   C.CHAR_USED, C.DATA_PRECISION, C.DATA_SCALE, C.NULLABLE
              FROM ALL_TAB_COLUMNS C WHERE C.OWNER=:owner AND C.TABLE_NAME=:name ORDER BY C.COLUMN_ID
            """, 500, token, binds);
        var constraints = await scope.Read("""
            SELECT C.CONSTRAINT_NAME, C.CONSTRAINT_TYPE, C.STATUS, C.R_OWNER, C.R_CONSTRAINT_NAME,
                   C.DELETE_RULE, CC.COLUMN_NAME, CC.POSITION
              FROM ALL_CONSTRAINTS C LEFT JOIN ALL_CONS_COLUMNS CC
                ON CC.OWNER=C.OWNER AND CC.CONSTRAINT_NAME=C.CONSTRAINT_NAME AND CC.TABLE_NAME=C.TABLE_NAME
             WHERE C.OWNER=:owner AND C.TABLE_NAME=:name ORDER BY C.CONSTRAINT_NAME, CC.POSITION
            """, 500, token, binds);
        var indexes = await scope.Read("""
            SELECT I.INDEX_NAME, I.UNIQUENESS, I.STATUS, C.COLUMN_NAME, C.COLUMN_POSITION, C.DESCEND
              FROM ALL_INDEXES I LEFT JOIN ALL_IND_COLUMNS C ON C.INDEX_OWNER=I.OWNER AND C.INDEX_NAME=I.INDEX_NAME
             WHERE I.TABLE_OWNER=:owner AND I.TABLE_NAME=:name ORDER BY I.INDEX_NAME, C.COLUMN_POSITION
            """, 500, token, binds);
        return new { nombre = name, esquema = scope.Target.Schema, columnas = columns, restricciones = constraints, indices = indexes,
            advertencia = "Solo metadatos visibles para la cuenta configurada; cero filas no acredita inexistencia. Defaults, comentarios y texto CHECK pendientes." };
    }, ct);

    [HttpPost("fuente")]
    public Task<IActionResult> SourceCode(OracleAnalisisQuery query, CancellationToken ct) => Run(query, OracleAnalisisRules.Sources, async (scope, token) =>
    {
        Source(scope, query);
        Type(query, SourceTypes);
        if (query.DesdeLinea < 1 || query.CantidadLineas is < 1 or > 1000)
            throw new OracleAnalisisValidation("RANGO_INVALIDO", "DesdeLinea >= 1, CantidadLineas entre 1 y 1000.");
        var rows = await scope.Read("""
            SELECT TYPE, LINE, TEXT FROM ALL_SOURCE
             WHERE OWNER=:owner AND NAME=:name AND TYPE=:kind AND LINE>=:start_line ORDER BY LINE
            """, query.CantidadLineas, token, ("owner", scope.Target.Schema), ("name", query.Nombre), ("kind", query.Tipo), ("start_line", query.DesdeLinea));
        var text = string.Concat(rows.Filas.Select(row => row.GetValueOrDefault("TEXT")?.ToString()));
        return new { lineas = rows, hashSegmento = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))),
            advertencia = "Hash del segmento, no de la fuente completa. Sin snapshot entre solicitudes. Codigo wrapped se entrega como esta almacenado." };
    }, ct);

    [HttpPost("erroresCompilacion")]
    public Task<IActionResult> Errors(OracleAnalisisQuery query, CancellationToken ct) => Run(query, OracleAnalisisRules.Sources, async (scope, token) =>
    {
        Source(scope, query); Type(query, SourceTypes);
        return await scope.Read("""
            SELECT TYPE, SEQUENCE, LINE, POSITION, TEXT, ATTRIBUTE, MESSAGE_NUMBER FROM ALL_ERRORS
             WHERE OWNER=:owner AND NAME=:name AND TYPE=:kind ORDER BY SEQUENCE
            """, 500, token, ("owner", scope.Target.Schema), ("name", query.Nombre), ("kind", query.Tipo));
    }, ct);

    [HttpPost("argumentos")]
    public Task<IActionResult> Arguments(OracleAnalisisQuery query, CancellationToken ct) => Run(query, OracleAnalisisRules.Metadata, async (scope, token) =>
    {
        var name = Name(query);
        return await scope.Read("""
            SELECT PACKAGE_NAME, OBJECT_NAME, OVERLOAD, ARGUMENT_NAME, POSITION, SEQUENCE,
                   DATA_LEVEL, DATA_TYPE, IN_OUT, DEFAULTED, TYPE_OWNER, TYPE_NAME, TYPE_SUBNAME
              FROM ALL_ARGUMENTS WHERE OWNER=:owner AND (OBJECT_NAME=:name OR PACKAGE_NAME=:name)
             ORDER BY PACKAGE_NAME, OBJECT_NAME, OVERLOAD, SEQUENCE
            """, 500, token, ("owner", scope.Target.Schema), ("name", name));
    }, ct);

    [HttpPost("dependencias")]
    public Task<IActionResult> Dependencies(OracleAnalisisQuery query, CancellationToken ct) => Run(query, OracleAnalisisRules.Metadata, async (scope, token) =>
    {
        var name = Name(query); Type(query, Types);
        var incoming = query.Direccion == "entrantes";
        if (!incoming && query.Direccion != "salientes")
            throw new OracleAnalisisValidation("DIRECCION_INVALIDA", "Use entrantes o salientes.");
        // Cross-schema references are reported, never traversed. Incoming lookup stays inside authorized owner.
        var where = incoming ? "OWNER=:owner AND REFERENCED_OWNER=:owner AND REFERENCED_NAME=:name AND REFERENCED_TYPE=:kind"
            : "OWNER=:owner AND NAME=:name AND TYPE=:kind";
        return await scope.Read($"SELECT OWNER, NAME, TYPE, REFERENCED_OWNER, REFERENCED_NAME, REFERENCED_TYPE, REFERENCED_LINK_NAME FROM ALL_DEPENDENCIES WHERE {where} ORDER BY NAME, REFERENCED_OWNER, REFERENCED_NAME",
            500, token, ("owner", scope.Target.Schema), ("name", name), ("kind", query.Tipo));
    }, ct);
}
