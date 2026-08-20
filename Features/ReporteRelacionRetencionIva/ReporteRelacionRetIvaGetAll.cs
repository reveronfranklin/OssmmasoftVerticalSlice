using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;
using System.Globalization;

namespace OssmmasoftVerticalSlice.Features.ReporteRelacionRetencionIva;

// =============================================================================
// Relacion de Retenciones de IVA por periodos de Orden de Pago.
// Requerimiento 22 - migracion de ADM_RELACION_RETENCION_IVA_OP2.rdf.
//
// El reporte lista, para un rango de fechas, los comprobantes de retencion de
// IVA emitidos, y por cada comprobante los documentos (facturas y notas) que lo
// componen, con un unico total de cierre. El agrupamiento por comprobante se
// hace aqui sobre el resultado plano del SP, igual que ReporteBm1Esp: el SP
// devuelve filas y el C# decide como se quiebran.
// =============================================================================

/// <summary>
/// Filtros del reporte. Las dos fechas son obligatorias -es un reporte por
/// periodo y sin rango imprimiria el historico completo-; el estatus es
/// opcional y vacio significa "todos", igual que el
/// <c>nvl(:P_ESTATUS, aop.status)</c> del reporte legado.
/// </summary>
public record ReporteRelacionRetIvaQuery(
    DateTime? FechaDesde = null,
    DateTime? FechaHasta = null,
    string? Estatus = null,
    string? Usuario = null
);

/// <summary>Una fila de detalle: un documento dentro de un comprobante.</summary>
public record ReporteRelacionRetIvaItem(
    int NumeroOperacion,
    string NumeroComprobante,
    DateTime? FechaComprobante,
    string NumeroOrdenPago,
    string Status,
    string EstatusDescripcion,
    string NombreProveedor,
    string RifProveedor,
    DateTime? FechaDocumento,
    string NumeroDocumento,
    string NumeroFactura,
    decimal MontoDocumento,
    decimal MontoImpuestoExento,
    decimal BaseImponible,
    string Alicuota,
    decimal MontoImpuesto,
    decimal MontoRetenido,
    decimal MontoRetenidoNeto
);

/// <summary>
/// Un comprobante con sus documentos. La cabecera se toma de la primera fila del
/// grupo: comprobante, fecha, orden de pago, estatus y proveedor son constantes
/// dentro del comprobante en las dos ramas del query.
/// </summary>
public record ReporteRelacionRetIvaComprobante(
    string NumeroComprobante,
    DateTime? Fecha,
    string NumeroOrdenPago,
    string EstatusDescripcion,
    string NombreProveedor,
    string RifProveedor,
    List<ReporteRelacionRetIvaItem> Documentos,
    decimal TotalRetenido
);

public class ReporteRelacionRetIvaHandler(ConnectionDB _connectionDB, IConfiguration _config)
{
    public async Task<ResultDto<List<ReporteRelacionRetIvaComprobante>>> HandleAsync(
        ReporteRelacionRetIvaQuery query)
    {
        if (!ReporteRelacionRetIvaDb.TryGetEmpresa(_config, out int empresa, out string errorEmpresa))
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

        // El dominio de STATUS en ADM_ORDEN_PAGO es cerrado. Se valida aqui y no
        // solo en el formulario porque el endpoint tambien se puede llamar
        // directo, y un valor fuera del dominio devolveria vacio en silencio -que
        // se lee como "no hay retenciones en el periodo"- en vez de un error.
        var estatus = NormalizarEstatus(query.Estatus);

        if (estatus is null && !string.IsNullOrWhiteSpace(query.Estatus))
        {
            return Invalid("El estatus debe ser AP (aprobado), PE (pendiente) o AN (anulado).");
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

        using var cmd = new OracleCommand("ADM.SP_REP_RET_IVA_PER_GET", cn)
        {
            CommandType = CommandType.StoredProcedure,
            BindByName = true
        };

        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_FechaDesde", OracleDbType.Date).Value = query.FechaDesde.Value.Date;
        cmd.Parameters.Add("p_FechaHasta", OracleDbType.Date).Value = query.FechaHasta.Value.Date;
        cmd.Parameters.Add("p_Estatus", OracleDbType.Varchar2).Value =
            estatus is null ? DBNull.Value : estatus;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
        var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
        cmd.Parameters.Add("p_TotalRecords", OracleDbType.Int32, ParameterDirection.Output);

        var planos = new List<ReporteRelacionRetIvaItem>();

        try
        {
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                planos.Add(ReporteRelacionRetIvaDb.MapItem(reader));
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

        if (!ReporteRelacionRetIvaDb.IsSuccessMessage(message))
        {
            return Invalid(message);
        }

        // GroupBy conserva el orden de aparicion, y el SP ya devuelve ordenado
        // por comprobante: reagrupar aqui no reordena nada.
        var comprobantes = planos
            .GroupBy(i => i.NumeroComprobante)
            .Select(g =>
            {
                var primero = g.First();

                return new ReporteRelacionRetIvaComprobante(
                    g.Key,
                    primero.FechaComprobante,
                    primero.NumeroOrdenPago,
                    primero.EstatusDescripcion,
                    primero.NombreProveedor,
                    primero.RifProveedor,
                    g.ToList(),
                    g.Sum(i => i.MontoRetenidoNeto));
            })
            .ToList();

        return new ResultDto<List<ReporteRelacionRetIvaComprobante>>(comprobantes)
        {
            Data = comprobantes,
            IsValid = true,
            Message = string.Empty,
            CantidadRegistros = planos.Count,
            Total1 = comprobantes.Sum(c => c.TotalRetenido)
        };
    }

    /// <summary>
    /// Vacio, nulo o solo espacios significa "todos los estatus". Devuelve
    /// <c>null</c> tambien cuando el valor no pertenece al dominio; el llamador
    /// distingue los dos casos mirando si la entrada venia vacia.
    /// </summary>
    private static string? NormalizarEstatus(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return null;
        }

        var normalizado = valor.Trim().ToUpperInvariant();

        return normalizado is "AP" or "PE" or "AN" ? normalizado : null;
    }

    private static ResultDto<List<ReporteRelacionRetIvaComprobante>> Invalid(string mensaje) =>
        new(null!) { Data = null, IsValid = false, Message = mensaje };
}

internal static class ReporteRelacionRetIvaDb
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

    public static ReporteRelacionRetIvaItem MapItem(IDataReader reader)
    {
        return new ReporteRelacionRetIvaItem(
            reader.SafeGetInt32("NUMERO_OPERACION"),
            reader.SafeGetString("NUMERO_COMPROBANTE"),
            SafeGetNullableDateTime(reader, "FECHA_COMPROBANTE"),
            reader.SafeGetString("NUMERO_ORDEN_PAGO"),
            reader.SafeGetString("STATUS").Trim(),
            reader.SafeGetString("ESTATUS_DESC").Trim(),
            reader.SafeGetString("NOMBRE_PROVEEDOR").Trim(),
            reader.SafeGetString("RIF_PROVEEDOR").Trim(),
            SafeGetNullableDateTime(reader, "FECHA_DOCUMENTO"),
            reader.SafeGetString("NUMERO_DOCUMENTO").Trim(),
            reader.SafeGetString("NUMERO_FACTURA").Trim(),
            reader.SafeGetDecimal("MONTO_DOCUMENTO"),
            reader.SafeGetDecimal("MONTO_IMPUESTO_EXENTO"),
            reader.SafeGetDecimal("BASE_IMPONIBLE"),
            reader.SafeGetString("ALICUOTA").Trim(),
            reader.SafeGetDecimal("MONTO_IMPUESTO"),
            reader.SafeGetDecimal("MONTO_RETENIDO"),
            reader.SafeGetDecimal("MONTO_RETENIDO_NETO")
        );
    }

    private static DateTime? SafeGetNullableDateTime(IDataReader reader, string columnName)
    {
        int ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal) ? null : Convert.ToDateTime(reader.GetValue(ordinal));
    }
}

[ApiController]
[Route("api/ReporteRelacionRetIva")]
public class ReporteRelacionRetIvaController(
    ConnectionDB _connectionDB,
    IConfiguration _config) : ControllerBase
{
    [HttpPost]
    [Route("GetAll")]
    public async Task<IActionResult> GetAll(ReporteRelacionRetIvaQuery value)
    {
        var handler = new ReporteRelacionRetIvaHandler(_connectionDB, _config);
        var result = await handler.HandleAsync(value);

        return Ok(result);
    }

    /// <summary>
    /// PDF del reporte. El usuario conectado es obligatorio: el pie de auditoria
    /// del requerimiento 17 lo imprime en todos los reportes de retenciones, y
    /// generarlo sin usuario dejaria un documento fiscal sin trazabilidad de
    /// quien lo emitio.
    /// </summary>
    [HttpPost]
    [Route("pdf")]
    public async Task<IActionResult> Pdf(ReporteRelacionRetIvaQuery value)
    {
        if (string.IsNullOrWhiteSpace(value.Usuario))
        {
            return BadRequest(new ResultDto<object?>(null)
            {
                IsValid = false,
                Message = "El usuario conectado es requerido para generar la relacion de retenciones de IVA."
            });
        }

        var handler = new ReporteRelacionRetIvaHandler(_connectionDB, _config);
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
                Message = "No hay retenciones de IVA en el periodo seleccionado."
            });
        }

        var printContext = ReportPrintContext.Create(value.Usuario);
        var bytes = ReporteRelacionRetIvaPdfGenerator.Generate(result.Data, value, printContext);

        Response.Headers.ContentDisposition = "inline; filename=\"relacion-retenciones-iva.pdf\"";

        return File(bytes, "application/pdf", enableRangeProcessing: true);
    }
}
