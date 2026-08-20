using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;
using System.Globalization;

namespace OssmmasoftVerticalSlice.Features.ReporteChequesPeriodoMotivo;

// =============================================================================
// Relacion de Cheques Emitidos Por Periodos (con Motivo).
// Requerimiento 23 - migracion de ADM_PERIODOS_CHEQUES_MOTIVO1.rdf.
//
// Lista los cheques emitidos en un rango de fechas, agrupados por banco/cuenta,
// con el motivo del cheque, la orden de pago que lo origina y las partidas
// presupuestarias imputadas. Cada grupo cierra con la cantidad y el monto de
// cheques validos y anulados, y el reporte con el total general.
//
// El agrupamiento y los subtotales se calculan aqui sobre el resultado plano del
// SP, no en SQL: es el patron que ya usan ReporteBm1Esp y ReporteOrdenPago, y
// evita meter logica de quiebre de grupo en un stored procedure.
// =============================================================================

/// <summary>
/// Filtros del reporte. El rango de fechas es obligatorio -a nivel de SQL el
/// legado lo hacia opcional con <c>NVL(:P_FECHA_INI, A.FECHA_CHEQUE)</c>, pero el
/// titulo del reporte siempre imprime un periodo y pedirlo sin rango recorreria
/// el historico completo-. Los cuatro filtros restantes son opcionales.
/// </summary>
public record ReporteChequesMotivoQuery(
    DateTime? FechaDesde = null,
    DateTime? FechaHasta = null,
    string? NombreBanco = null,
    string? NumeroCuenta = null,
    string? Status = null,
    int? CodigoProveedor = null,
    string? Usuario = null
);

/// <summary>Una linea de detalle: una linea de beneficiario de un cheque.</summary>
public record ReporteChequesMotivoItem(
    string NombreBanco,
    string NumeroCuenta,
    DateTime? FechaCheque,
    string NumeroDocumento,
    string Status,
    string EstatusDescripcion,
    string Beneficiario,
    decimal Monto,
    string Motivo
)
{
    public bool EsAnulado => string.Equals(Status, "AN", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Monto que cuenta como cheque valido. Replica <c>CF_TOTAL_VALIDOS</c>.
    /// </summary>
    public decimal MontoValido => EsAnulado ? 0m : Monto;

    /// <summary>
    /// Monto que cuenta como cheque anulado, **en positivo**. Replica
    /// <c>CF_1</c>: el query devuelve el monto de un anulado ya negado, y la
    /// linea "CHEQUES ANULADOS" del pie lo muestra positivo.
    /// </summary>
    public decimal MontoAnulado => EsAnulado ? -Monto : 0m;
}

/// <summary>
/// Un banco/cuenta con sus cheques y sus subtotales. Es el grupo
/// <c>G_DTOS_BANCO</c> del reporte legado, que quiebra pagina.
/// </summary>
public record ReporteChequesMotivoGrupo(
    string NombreBanco,
    string NumeroCuenta,
    List<ReporteChequesMotivoItem> Items,
    int CantidadValidos,
    decimal MontoValidos,
    int CantidadAnulados,
    decimal MontoAnulados
);

public class ReporteChequesMotivoHandler(ConnectionDB _connectionDB, IConfiguration _config)
{
    public async Task<ResultDto<List<ReporteChequesMotivoGrupo>>> HandleAsync(
        ReporteChequesMotivoQuery query)
    {
        if (!ReporteChequesMotivoDb.TryGetEmpresa(_config, out int empresa, out string errorEmpresa))
        {
            return Invalid(errorEmpresa);
        }

        if (!query.FechaDesde.HasValue || !query.FechaHasta.HasValue)
        {
            return Invalid("Indique la fecha desde y la fecha hasta del periodo.");
        }

        if (query.FechaDesde.Value.Date > query.FechaHasta.Value.Date)
        {
            return Invalid("La fecha desde no puede ser posterior a la fecha hasta.");
        }

        // Dominio cerrado de ADM_CHEQUES.STATUS segun el DECODE del reporte
        // legado. Se valida aqui porque un valor fuera del dominio devolveria
        // cero filas, y cero filas se lee como "no hubo cheques en el periodo".
        var status = NormalizarStatus(query.Status);

        if (status is null && !string.IsNullOrWhiteSpace(query.Status))
        {
            return Invalid("El status debe ser AP (aprobado) o AN (anulado).");
        }

        using var cn = _connectionDB.GetAdmConnection();

        try
        {
            await cn.OpenAsync();
        }
        catch (Exception ex)
        {
            return Invalid($"Error tecnico al abrir conexion ADM: {ex.Message}");
        }

        using var cmd = new OracleCommand("ADM.SP_REP_CHEQ_MOTIVO_GET", cn)
        {
            CommandType = CommandType.StoredProcedure,
            BindByName = true
        };

        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_FechaDesde", OracleDbType.Date).Value = query.FechaDesde.Value.Date;
        cmd.Parameters.Add("p_FechaHasta", OracleDbType.Date).Value = query.FechaHasta.Value.Date;
        cmd.Parameters.Add("p_NombreBanco", OracleDbType.Varchar2).Value = DbValue(query.NombreBanco);
        cmd.Parameters.Add("p_NumeroCuenta", OracleDbType.Varchar2).Value = DbValue(query.NumeroCuenta);
        cmd.Parameters.Add("p_Status", OracleDbType.Varchar2).Value =
            status is null ? DBNull.Value : status;
        cmd.Parameters.Add("p_CodigoProveedor", OracleDbType.Int32).Value = DbValue(query.CodigoProveedor);
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
        var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
        cmd.Parameters.Add("p_TotalRecords", OracleDbType.Int32, ParameterDirection.Output);

        var planos = new List<ReporteChequesMotivoItem>();

        try
        {
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                planos.Add(ReporteChequesMotivoDb.MapItem(reader));
            }
        }
        catch (OracleException ex)
        {
            return Invalid($"Error de base de datos ({ex.Number}): {ex.Message}");
        }
        catch (Exception ex)
        {
            return Invalid($"Error tecnico: {ex.Message}");
        }

        var message = pMessage.Value == DBNull.Value ? string.Empty : pMessage.Value?.ToString() ?? string.Empty;

        if (!ReporteChequesMotivoDb.IsSuccessMessage(message))
        {
            return Invalid(message);
        }

        // GroupBy conserva el orden de aparicion, y el SP ordena por banco y
        // cuenta antes que nada: sin eso un mismo banco saldria partido en
        // varios bloques con subtotales incompletos.
        var grupos = planos
            .GroupBy(i => new { i.NombreBanco, i.NumeroCuenta })
            .Select(g => new ReporteChequesMotivoGrupo(
                g.Key.NombreBanco,
                g.Key.NumeroCuenta,
                g.ToList(),
                g.Count(i => !i.EsAnulado),
                g.Sum(i => i.MontoValido),
                g.Count(i => i.EsAnulado),
                g.Sum(i => i.MontoAnulado)))
            .ToList();

        return new ResultDto<List<ReporteChequesMotivoGrupo>>(grupos)
        {
            Data = grupos,
            IsValid = true,
            Message = string.Empty,
            CantidadRegistros = planos.Count,
            Total1 = grupos.Sum(g => g.MontoValidos),
            Total2 = grupos.Sum(g => g.MontoAnulados)
        };
    }

    /// <summary>
    /// Vacio, nulo o solo espacios significa "todos". Devuelve <c>null</c>
    /// tambien cuando el valor no pertenece al dominio; el llamador distingue los
    /// dos casos mirando si la entrada venia vacia.
    /// </summary>
    private static string? NormalizarStatus(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        var normalizado = valor.Trim().ToUpperInvariant();

        return normalizado is "AP" or "AN" ? normalizado : null;
    }

    private static object DbValue(string? value) =>
        string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();

    private static object DbValue(int? value) =>
        value.HasValue && value.Value > 0 ? value.Value : DBNull.Value;

    private static ResultDto<List<ReporteChequesMotivoGrupo>> Invalid(string mensaje) =>
        new(null!) { Data = null, IsValid = false, Message = mensaje };
}

internal static class ReporteChequesMotivoDb
{
    public static bool IsSuccessMessage(string? message)
    {
        return string.Equals(message, "success", StringComparison.OrdinalIgnoreCase)
            || string.Equals(message, "suscces", StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryGetEmpresa(IConfiguration config, out int empresa, out string errorMessage)
    {
        empresa = 0;
        errorMessage = string.Empty;
        var empresaString = config["settings:EmpresaConfig"];

        if (string.IsNullOrEmpty(empresaString))
        {
            errorMessage = "Configuracion 'EmpresaConfig' no encontrada.";
            return false;
        }

        if (!int.TryParse(empresaString, NumberStyles.Integer, CultureInfo.InvariantCulture, out empresa))
        {
            errorMessage = "EmpresaConfig debe ser un numero valido.";
            return false;
        }

        return true;
    }

    public static ReporteChequesMotivoItem MapItem(IDataReader reader)
    {
        return new ReporteChequesMotivoItem(
            reader.SafeGetString("NOMBRE_BANCO").Trim(),
            reader.SafeGetString("NUMERO_CUENTA").Trim(),
            SafeGetNullableDateTime(reader, "FECHA_CHEQUE"),
            reader.SafeGetString("NUMERO_DOCUMENTO").Trim(),
            reader.SafeGetString("STATUS").Trim(),
            reader.SafeGetString("ESTATUS_DESC").Trim(),
            reader.SafeGetString("BENEFICIARIO").Trim(),
            reader.SafeGetDecimal("MONTO"),
            NormalizarMotivo(reader.SafeGetString("MOTIVO"))
        );
    }

    /// <summary>
    /// El motivo llega con separadores <c>CHR(13)</c> -retorno de carro suelto-,
    /// que es como los concatena el query legado y como los emite
    /// <c>ADM_F_GET_PARTIDAS_CHEQUE</c>. Un CR sin LF no rompe linea en QuestPDF,
    /// asi que se normaliza a salto de linea; los espacios repetidos que el
    /// legado usaba como separacion visual se colapsan porque en un parrafo con
    /// ajuste de linea no aportan nada.
    /// </summary>
    private static string NormalizarMotivo(string valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return string.Empty;
        }

        var texto = valor.Replace("\r\n", "\n").Replace('\r', '\n');
        var lineas = texto
            .Split('\n')
            .Select(linea => string.Join(' ', linea.Split(' ', StringSplitOptions.RemoveEmptyEntries)))
            .Where(linea => linea.Length > 0);

        return string.Join('\n', lineas);
    }

    private static DateTime? SafeGetNullableDateTime(IDataReader reader, string columnName)
    {
        int ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal) ? null : Convert.ToDateTime(reader.GetValue(ordinal));
    }
}

[ApiController]
[Route("api/ReporteChequesMotivo")]
public class ReporteChequesMotivoController(
    ConnectionDB _connectionDB,
    IConfiguration _config) : ControllerBase
{
    [HttpPost]
    [Route("GetAll")]
    public async Task<IActionResult> GetAll(ReporteChequesMotivoQuery value)
    {
        var handler = new ReporteChequesMotivoHandler(_connectionDB, _config);
        var result = await handler.HandleAsync(value);

        return Ok(result);
    }

    /// <summary>
    /// PDF del reporte. El usuario conectado es obligatorio: alimenta el pie de
    /// auditoria compartido (requerimiento 17), y una relacion de cheques sin
    /// constancia de quien la emitio es justo lo que ese requerimiento cerro.
    /// </summary>
    [HttpPost]
    [Route("pdf")]
    public async Task<IActionResult> Pdf(ReporteChequesMotivoQuery value)
    {
        if (string.IsNullOrWhiteSpace(value.Usuario))
        {
            return BadRequest(new ResultDto<object?>(null)
            {
                IsValid = false,
                Message = "El usuario conectado es requerido para generar la relacion de cheques."
            });
        }

        var handler = new ReporteChequesMotivoHandler(_connectionDB, _config);
        var result = await handler.HandleAsync(value);

        if (!result.IsValid || result.Data is null)
        {
            return Ok(result);
        }

        if (result.Data.Count == 0)
        {
            return Ok(new ResultDto<string>(string.Empty)
            {
                IsValid = false,
                Message = "No hay cheques emitidos en el periodo seleccionado."
            });
        }

        var printContext = ReportPrintContext.Create(value.Usuario);
        var bytes = ReporteChequesMotivoPdfGenerator.Generate(result.Data, value, printContext);

        Response.Headers.ContentDisposition = "inline; filename=\"relacion-cheques-motivo.pdf\"";

        return File(bytes, "application/pdf", enableRangeProcessing: true);
    }
}
