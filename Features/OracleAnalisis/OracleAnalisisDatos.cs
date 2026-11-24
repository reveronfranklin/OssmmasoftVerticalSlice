using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using OssmmasoftVerticalSlice.Features.Support;

namespace OssmmasoftVerticalSlice.Features.OracleAnalisis;

public record OracleAnalisisFiltro(string Columna, string Operador, JsonElement Valor = default);
public record OracleAnalisisOrden(string Columna, string Direccion = "asc");
public record OracleAnalisisDatosQuery(string Conexion, string Objeto, List<string> Columnas,
    List<OracleAnalisisFiltro>? Filtros = null, List<OracleAnalisisOrden>? Orden = null,
    int PageSize = 50, string? Cursor = null, string? Esquema = null);
public sealed class OracleAnalisisDataPolicy
{
    public List<string> Columns { get; set; } = [];
    public string? EmpresaColumn { get; set; }
    public bool Global { get; set; }
    // Explicit heap-table strategy: ROWID is used only as a unique tie breaker.
    public bool UseRowId { get; set; }
    public List<OracleAnalisisOrden> DefaultOrder { get; set; } = [];
}
public record OracleAnalisisColumn(string Name, string Type, int? Length = null);
public record OracleAnalisisBind(string Name, OracleDbType Type, object? Value);
public record OracleAnalisisPlan(string Sql, List<OracleAnalisisBind> Binds,
    List<OracleAnalisisOrden> Order, List<OracleAnalisisColumn> Columns, string Fingerprint, int Limit);
public record OracleAnalisisCursor(string Fingerprint, DateTimeOffset Expires, List<string?> Values);
public record OracleAnalisisDataResult(int? Empresa, string Objeto, List<OracleAnalisisColumn> Columnas,
    OracleAnalisisRows Datos, string? NextCursor, string Consistencia);

public static class OracleAnalisisDataRules
{
    public const string Permission = "oracle.analisis.datos";
    private static string Quote(string value) => '"' + OracleAnalisisRules.Identifier(value) + '"';
    public static bool Sensitive(string name) => Regex.IsMatch(name,
        "PASSWORD|PASSWD|CLAVE|TOKEN|SECRET|CREDENTIAL|CREDENCIAL|API_?KEY|PRIVATE_?KEY", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private static void Fail(string message) => throw new OracleAnalisisValidation("CONSULTA_NO_AUTORIZADA", message);
    public static OracleAnalisisPlan Build(OracleAnalisisDatosQuery request, OracleAnalisisDataPolicy policy,
        IReadOnlyList<OracleAnalisisColumn> metadata, OracleAnalisisOptions options, int empresa,
        DateTimeOffset now)
    {
        var schema = options.Targets[request.Conexion].Schema;
        Quote(schema); Quote(request.Objeto);
        if (!policy.UseRowId) Fail("La tabla no tiene una estrategia de paginacion aprobada (UseRowId).");
        if (policy.Global ? policy.EmpresaColumn is not null : string.IsNullOrEmpty(policy.EmpresaColumn) || empresa <= 0)
            Fail("La politica debe definir alcance por empresa o catalogo global, exclusivamente.");
        if (request.Columnas is null || request.Columnas.Count is < 1 or > 40 || request.Columnas.Distinct().Count() != request.Columnas.Count)
            Fail("Solicite entre 1 y 40 columnas distintas y autorizadas.");
        if (request.PageSize is < 1 or > 500 || request.Filtros?.Count > 20) Fail("Limite de filas o filtros invalido.");
        var all = metadata.ToDictionary(c => c.Name, StringComparer.Ordinal);
        OracleAnalisisColumn Column(string name, bool scope = false)
        {
            Quote(name);
            if (name.StartsWith("OA$", StringComparison.Ordinal) || Sensitive(name)
                || !all.TryGetValue(name, out var column) || (!scope && !policy.Columns.Contains(name, StringComparer.Ordinal)))
                throw new OracleAnalisisValidation("COLUMNA_NO_AUTORIZADA", "Columna no autorizada o no visible.");
            if (column.Type is not ("NUMBER" or "VARCHAR2" or "NVARCHAR2" or "CHAR" or "NCHAR" or "DATE"))
                throw new OracleAnalisisValidation("TIPO_NO_SOPORTADO", "Primera version de datos admite NUMBER, VARCHAR2, NVARCHAR2, CHAR, NCHAR y DATE.");
            return column;
        }
        var selected = request.Columnas.Select(n => Column(n)).ToList();
        var order = request.Orden is { Count: > 0 } ? request.Orden : policy.DefaultOrder;
        if (order.Count is < 1 or > 3 || order.Select(o => o.Columna).Distinct().Count() != order.Count) Fail("Defina entre 1 y 3 columnas distintas para ordenar.");
        foreach (var item in order)
        {
            var sorted = Column(item.Columna);
            if (sorted.Type is "VARCHAR2" or "NVARCHAR2" or "CHAR" or "NCHAR" && sorted.Length > 1000)
                Fail("Ordene por una columna de texto de hasta 1000 bytes o por numero/fecha.");
            if (item.Direccion is not ("asc" or "desc")) Fail("Direccion debe ser asc o desc.");
        }
        var limit = Math.Min(request.PageSize, Math.Clamp(options.MaxRows, 1, 500));
        var binds = new List<OracleAnalisisBind>();
        var where = new List<string>();
        string Bind(OracleAnalisisColumn column, JsonElement value)
        {
            var name = "oa_p" + binds.Count;
            var (type, parsed) = Parse(column.Type, value);
            binds.Add(new(name, type, parsed)); return ":" + name;
        }
        if (!policy.Global)
        {
            var company = Column(policy.EmpresaColumn!, true);
            if (company.Type != "NUMBER") Fail("La columna de empresa debe ser NUMBER.");
            binds.Add(new("oa_empresa", OracleDbType.Int32, empresa));
            where.Add("T." + Quote(company.Name) + "=:oa_empresa");
        }
        foreach (var f in request.Filtros ?? [])
        {
            var col = Column(f.Columna); var expression = "T." + Quote(col.Name);
            if (f.Operador is "isNull" or "isNotNull")
            {
                where.Add(expression + (f.Operador == "isNull" ? " IS NULL" : " IS NOT NULL")); continue;
            }
            if (f.Operador == "in")
            {
                if (f.Valor.ValueKind != JsonValueKind.Array || f.Valor.GetArrayLength() is < 1 or > 100) Fail("IN requiere entre 1 y 100 valores.");
                where.Add(expression + " IN (" + string.Join(",", f.Valor.EnumerateArray().Select(v => Bind(col, v))) + ")"); continue;
            }
            if (f.Operador == "contains")
            {
                if (col.Type is not ("VARCHAR2" or "NVARCHAR2" or "CHAR" or "NCHAR")) Fail("contains solo admite texto.");
                where.Add("INSTR(" + expression + "," + Bind(col, f.Valor) + ") > 0"); continue;
            }
            var op = f.Operador switch { "eq" => "=", "ne" => "<>", "gt" => ">", "gte" => ">=", "lt" => "<", "lte" => "<=", _ => throw new OracleAnalisisValidation("OPERADOR_INVALIDO", "Operador no admitido.") };
            where.Add(expression + op + Bind(col, f.Valor));
        }
        var fingerprint = Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            request.Conexion, schema, request.Objeto, request.Columnas, request.Filtros, order, limit,
            empresa, policy, metadata, options.ServiceAccess.ClientId,
            target = options.Targets[request.Conexion]
        })));
        var expressions = order.Select(o => "T." + Quote(o.Columna)).Append("ROWIDTOCHAR(T.ROWID)").ToList();
        if (request.Cursor is not null)
        {
            var cursor = Unprotect(request.Cursor, options.ServiceAccess.Token, fingerprint, now);
            if (cursor.Values.Count != expressions.Count) Fail("Cursor incompatible.");
            var alternatives = new List<string>(); var prefix = new List<string>();
            for (var i = 0; i < expressions.Count; i++)
            {
                var value = cursor.Values[i]; var expression = expressions[i];
                if (value is null) { prefix.Add(expression + " IS NULL"); continue; }
                var column = i < order.Count ? Column(order[i].Columna) : new OracleAnalisisColumn("ROWID", "VARCHAR2");
                var parameter = Bind(column, JsonSerializer.SerializeToElement(value));
                var op = i < order.Count && order[i].Direccion == "desc" ? "<" : ">";
                var condition = "(" + expression + op + parameter + " OR " + expression + " IS NULL)";
                alternatives.Add("(" + string.Join(" AND ", prefix.Append(condition)) + ")");
                prefix.Add(expression + "=" + parameter);
            }
            if (alternatives.Count == 0) Fail("Cursor sin posicion valida.");
            where.Add("(" + string.Join(" OR ", alternatives) + ")");
        }
        var projection = selected.Select(c => "T." + Quote(c.Name)).Concat(expressions.Select((e, i) => e + " AS \"OA$" + i + "\""));
        var sorting = order.Select(o => "T." + Quote(o.Columna) + " " + o.Direccion.ToUpperInvariant() + " NULLS LAST").Append("ROWIDTOCHAR(T.ROWID) ASC");
        var sql = "SELECT " + string.Join(",", projection) + " FROM " + Quote(schema) + "." + Quote(request.Objeto) + " T"
            + (where.Count == 0 ? "" : " WHERE " + string.Join(" AND ", where)) + " ORDER BY " + string.Join(",", sorting);
        return new(sql, binds, order.ToList(), selected, fingerprint, limit);
    }
    public static (OracleDbType Type, object Value) Parse(string type, JsonElement value)
    {
        if (value.ValueKind is not (JsonValueKind.String or JsonValueKind.Number))
            throw new OracleAnalisisValidation("VALOR_INVALIDO", "Use valor escalar tipado; para nulos use isNull/isNotNull.");
        var text = value.ValueKind == JsonValueKind.String ? value.GetString()! : value.GetRawText();
        if (text.Length is < 1 or > 1000) throw new OracleAnalisisValidation("VALOR_INVALIDO", "Valor vacio o mayor de 1000 caracteres.");
        try
        {
            if (type == "NUMBER")
            {
                return (OracleDbType.Decimal, Number(text));
            }
            if (type == "DATE")
            {
                if (value.ValueKind != JsonValueKind.String || !DateTime.TryParseExact(text,
                    ["yyyy-MM-dd", "yyyy-MM-dd'T'HH:mm:ss", "yyyy-MM-dd'T'HH:mm:ss.fffffff"],
                    CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)) throw new FormatException();
                if (date.Ticks % TimeSpan.TicksPerSecond != 0) throw new FormatException();
                return (OracleDbType.Date, DateTime.SpecifyKind(date, DateTimeKind.Unspecified));
            }
            if (value.ValueKind != JsonValueKind.String) throw new FormatException();
            return (type is "NCHAR" or "NVARCHAR2" ? OracleDbType.NVarchar2 : OracleDbType.Varchar2, text);
        }
        catch (Exception ex) when (ex is FormatException or OverflowException or OracleTypeException or ArgumentException)
        { throw new OracleAnalisisValidation("VALOR_INVALIDO", "Valor incompatible con el tipo Oracle o fuera de rango."); }
    }
    private static OracleDecimal Number(string text)
    {
        var match = Regex.Match(text, @"\A([+-]?)([0-9]{1,126})(?:\.([0-9]{1,130}))?(?:[eE]([+-]?[0-9]{1,3}))?\z");
        if (!match.Success) throw new FormatException();
        var digits = match.Groups[2].Value + match.Groups[3].Value;
        if (digits.TrimStart('0').TrimEnd('0').Length > 38) throw new FormatException();
        var exponent = match.Groups[4].Success ? int.Parse(match.Groups[4].Value, CultureInfo.InvariantCulture) : 0;
        if (Math.Abs(exponent) > 130) throw new FormatException();
        var point = match.Groups[2].Value.Length + exponent;
        var expanded = point <= 0 ? "0." + new string('0', -point) + digits
            : point >= digits.Length ? digits + new string('0', point-digits.Length)
            : digits[..point] + "." + digits[point..];
        // ODP.NET Core Parse/ToString use dot decimal, independently of CurrentCulture.
        var result = OracleDecimal.Parse(match.Groups[1].Value + expanded);
        if (result.IsZero && digits.Trim('0').Length > 0) throw new FormatException();
        return result;
    }
    public static string Protect(OracleAnalisisCursor cursor, string token)
    {
        var plain = JsonSerializer.SerializeToUtf8Bytes(cursor);
        var nonce = RandomNumberGenerator.GetBytes(12); var tag = new byte[16]; var cipher = new byte[plain.Length];
        using var aes = new AesGcm(Key(token), 16); aes.Encrypt(nonce, plain, cipher, tag);
        return Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlEncode(nonce.Concat(tag).Concat(cipher).ToArray());
    }
    public static OracleAnalisisCursor Unprotect(string text, string token, string fingerprint, DateTimeOffset now)
    {
        try
        {
            if (text.Length is < 40 or > 16384) throw new FormatException();
            var data = Microsoft.AspNetCore.WebUtilities.WebEncoders.Base64UrlDecode(text);
            var plain = new byte[data.Length - 28];
            using var aes = new AesGcm(Key(token), 16); aes.Decrypt(data.AsSpan(0,12), data.AsSpan(28), data.AsSpan(12,16), plain);
            var cursor = JsonSerializer.Deserialize<OracleAnalisisCursor>(plain)!;
            if (cursor.Fingerprint != fingerprint || cursor.Expires <= now || cursor.Values is null) throw new FormatException();
            return cursor;
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or JsonException or ArgumentException or NullReferenceException)
        { throw new OracleAnalisisValidation("CURSOR_INVALIDO", "Cursor alterado, vencido o incompatible con la consulta, configuracion o cliente."); }
    }
    private static byte[] Key(string token) => SHA256.HashData(Encoding.UTF8.GetBytes("OracleAnalisis.Cursor.v1\0" + token));
}

public sealed partial class OracleAnalisisController
{
    [HttpPost("consultarDatos")]
    public Task<IActionResult> Data(OracleAnalisisDatosQuery request, CancellationToken ct) =>
        Run(new(request.Conexion, request.Esquema), OracleAnalisisDataRules.Permission, async (scope, token) =>
        {
            var name = OracleAnalisisRules.Identifier(request.Objeto);
            if (!scope.Target.DataObjects.TryGetValue(name, out var policy))
                throw new OracleAnalisisValidation("OBJETO_NO_AUTORIZADO", "La tabla no tiene una politica DataObjects autorizada.");
            // Only local heap tables. Never views, synonyms, database links or IOTs.
            var tables = await scope.Read("SELECT TABLE_NAME FROM ALL_TABLES WHERE OWNER=:owner AND TABLE_NAME=:name AND IOT_TYPE IS NULL", 1, token,
                ("owner", scope.Target.Schema), ("name", name));
            if (tables.Filas.Count != 1) throw new OracleAnalisisValidation("TABLA_NO_DISPONIBLE", "Se requiere una tabla heap local visible para la cuenta.");
            var metadataRows = await scope.Read("SELECT COLUMN_NAME, DATA_TYPE, DATA_LENGTH FROM ALL_TAB_COLUMNS WHERE OWNER=:owner AND TABLE_NAME=:name ORDER BY COLUMN_ID", 1000, token,
                ("owner", scope.Target.Schema), ("name", name));
            if (metadataRows.HasMore || metadataRows.Truncado) throw new OracleAnalisisValidation("METADATOS_INCOMPLETOS", "No se pudo validar la estructura completa.");
            var metadata = metadataRows.Filas.Select(r => new OracleAnalisisColumn((string)r["COLUMN_NAME"]!, (string)r["DATA_TYPE"]!, int.Parse(r["DATA_LENGTH"]!.ToString()!, CultureInfo.InvariantCulture))).ToList();
            var options = scope.Options;
            if (!SupportDb.TryGetEmpresa(config, out var empresa, out var error) && !policy.Global)
                throw new OracleAnalisisValidation("EMPRESA_INVALIDA", error);
            var plan = OracleAnalisisDataRules.Build(request, policy, metadata, options, empresa, DateTimeOffset.UtcNow);
            var rows = await scope.ReadData(plan, token);
            string? next = null;
            if (rows.HasMore && rows.Filas.Count > 0)
            {
                var last = rows.Filas[^1];
                var values = Enumerable.Range(0, plan.Order.Count+1).Select(i => last["OA$"+i]?.ToString()).ToList();
                next = OracleAnalisisDataRules.Protect(new(plan.Fingerprint, DateTimeOffset.UtcNow.AddMinutes(15), values), options.ServiceAccess.Token);
            }
            foreach (var row in rows.Filas) foreach (var key in row.Keys.Where(k => k.StartsWith("OA$", StringComparison.Ordinal)).ToArray()) row.Remove(key);
            logger.LogInformation("OracleAnalisisData {RequestId} Actor={Actor} Alias={Alias} Objeto={Object} Empresa={Company} Filas={Rows} QueryHash={Hash}",
                HttpContext.TraceIdentifier, User.Identity?.Name, request.Conexion, name, policy.Global ? null : (int?)empresa, rows.Filas.Count, plan.Fingerprint);
            return new OracleAnalisisDataResult(policy.Global ? null : empresa, name, plan.Columns, rows, next,
                "Sin snapshot entre paginas. ROWID como desempate en tabla heap; invalidar investigacion si se reorganiza la tabla.");
        }, ct);
}
