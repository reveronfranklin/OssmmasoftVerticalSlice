using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;
using System.Globalization;

namespace OssmmasoftVerticalSlice.Features.ReporteRelacionCompromiso;

// =============================================================================
// Relacion de Compromisos.
// Requerimiento 25 - migracion de ADM_RELACION_COMPROMISO.rdf.
//
// Lista los compromisos de un presupuesto en un rango de fechas, uniendo las
// tres fuentes del sistema -compromisos administrativos, compromisos de
// presupuesto y contratos- en un unico listado plano ordenado por fecha y
// numero, con un total de cierre.
//
// A diferencia de los reportes de cheques (requerimientos 23 y 24), este **no
// tiene quiebre de grupo**: una sola seccion y un total. Por eso no hay
// agrupamiento en C#; el handler devuelve la lista tal como la ordena el SP.
// =============================================================================

/// <summary>
/// Filtros del reporte.
///
/// <c>CodigoPresupuesto</c> es obligatorio y **no se resuelve por
/// configuracion**. El reporte legado lo tomaba como variable de contexto de
/// Oracle Reports, y en el vertical slice no existe un equivalente a
/// <c>settings:EmpresaConfig</c> para el presupuesto: el patron vigente es que el
/// usuario elija uno de la lista de <c>Features/PrePresupuesto</c>. En el motor de
/// formularios llega desde el catalogo <c>PRE_PRESUPUESTO</c>.
///
/// Las dos fechas son opcionales e independientes, como en el .rdf: se puede
/// pedir solo desde, solo hasta, ambas o ninguna. Sin ninguna, el reporte lista
/// el presupuesto completo, que es lo que hacia el legado.
/// </summary>
public record ReporteRelacionCompromisoQuery(
    int CodigoPresupuesto = 0,
    DateTime? FechaDesde = null,
    DateTime? FechaHasta = null,
    int? CodigoProveedor = null,
    string? Usuario = null
);

/// <summary>Una fila: un compromiso o contrato, ya neto de anulaciones.</summary>
public record ReporteRelacionCompromisoItem(
    int CodigoCompromiso,
    string NumeroCompromiso,
    DateTime? FechaCompromiso,
    string NombreProveedor,
    decimal MontoCompromiso,

    /// <summary>
    /// De cual de las tres ramas del UNION salio la fila: <c>ADM</c>, <c>PRE</c> o
    /// <c>CONTRATO</c>. El reporte legado no lo mostraba y el PDF tampoco lo
    /// imprime; se expone porque es lo unico que permite, ante un total que no
    /// cuadra, saber que fuente lo aporto.
    /// </summary>
    string Origen
);

public class ReporteRelacionCompromisoHandler(ConnectionDB _connectionDB, IConfiguration _config)
{
    public async Task<ResultDto<List<ReporteRelacionCompromisoItem>>> HandleAsync(
        ReporteRelacionCompromisoQuery query)
    {
        if (!ReporteRelacionCompromisoDb.TryGetEmpresa(_config, out int empresa, out string errorEmpresa))
        {
            return Invalid(errorEmpresa);
        }

        if (query.CodigoPresupuesto <= 0)
        {
            return Invalid("Indique el presupuesto del reporte.");
        }

        if (query.FechaDesde.HasValue && query.FechaHasta.HasValue
            && query.FechaDesde.Value.Date > query.FechaHasta.Value.Date)
        {
            return Invalid("La fecha desde no puede ser posterior a la fecha hasta.");
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

        using var cmd = new OracleCommand("ADM.SP_REP_COMPROMISO_GET", cn)
        {
            CommandType = CommandType.StoredProcedure,
            BindByName = true
        };

        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_CodigoPresupuesto", OracleDbType.Int32).Value = query.CodigoPresupuesto;
        cmd.Parameters.Add("p_FechaDesde", OracleDbType.Date).Value = DbValue(query.FechaDesde);
        cmd.Parameters.Add("p_FechaHasta", OracleDbType.Date).Value = DbValue(query.FechaHasta);
        cmd.Parameters.Add("p_CodigoProveedor", OracleDbType.Int32).Value = DbValue(query.CodigoProveedor);
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
        var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
        cmd.Parameters.Add("p_TotalRecords", OracleDbType.Int32, ParameterDirection.Output);

        var items = new List<ReporteRelacionCompromisoItem>();

        try
        {
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                items.Add(ReporteRelacionCompromisoDb.MapItem(reader));
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

        if (!ReporteRelacionCompromisoDb.IsSuccessMessage(message))
        {
            return Invalid(message);
        }

        return new ResultDto<List<ReporteRelacionCompromisoItem>>(items)
        {
            Data = items,
            IsValid = true,
            Message = string.Empty,
            // El SP devuelve p_TotalRecords en 0 a proposito: contar exigiria
            // repetir las tres ramas del UNION. El conteo autoritativo es este.
            CantidadRegistros = items.Count,
            Total1 = items.Sum(i => i.MontoCompromiso)
        };
    }

    /// <summary>
    /// Nombre de la entidad para el encabezado, donde el reporte legado ponia el
    /// membrete de <c>SIS_MEMBRETE</c>.
    ///
    /// Se resuelve igual que en <c>ReporteBm1Esp.ObtenerEntidadAsync</c>: una
    /// consulta directa a <c>PRE_IDENTIFICACIONES</c>, sin procedimiento propio,
    /// porque es una sola fila sin logica de negocio. **A diferencia de aquel, la
    /// identificacion se busca por el presupuesto del reporte** y no por el mas
    /// reciente: un reporte de un presupuesto de 2024 tiene que llevar el
    /// membrete de 2024.
    ///
    /// Si no se puede resolver devuelve vacio: el encabezado es presentacion, y
    /// es mejor un reporte con el membrete en blanco que ningun reporte.
    /// </summary>
    public async Task<string> ObtenerEntidadAsync(int codigoPresupuesto)
    {
        if (!ReporteRelacionCompromisoDb.TryGetEmpresa(_config, out int empresa, out _))
        {
            return string.Empty;
        }

        try
        {
            using var cn = _connectionDB.GetAdmConnection();
            await cn.OpenAsync();

            using var cmd = new OracleCommand(
                @"SELECT A.DENOMINACION_ONP
                    FROM PRE.PRE_IDENTIFICACIONES A
                   WHERE A.CODIGO_EMPRESA = :p_CodigoEmpresa
                     AND A.CODIGO_PRESUPUESTO = :p_CodigoPresupuesto", cn)
            {
                BindByName = true
            };

            cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
            cmd.Parameters.Add("p_CodigoPresupuesto", OracleDbType.Int32).Value = codigoPresupuesto;

            using var reader = await cmd.ExecuteReaderAsync();

            return await reader.ReadAsync() ? reader.SafeGetString("DENOMINACION_ONP").Trim() : string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static object DbValue(DateTime? value) => value.HasValue ? value.Value.Date : DBNull.Value;

    private static object DbValue(int? value) =>
        value.HasValue && value.Value > 0 ? value.Value : DBNull.Value;

    private static ResultDto<List<ReporteRelacionCompromisoItem>> Invalid(string mensaje) =>
        new(null!) { Data = null, IsValid = false, Message = mensaje };
}

internal static class ReporteRelacionCompromisoDb
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

    public static ReporteRelacionCompromisoItem MapItem(IDataReader reader)
    {
        return new ReporteRelacionCompromisoItem(
            reader.SafeGetInt32("CODIGO_COMPROMISO"),
            reader.SafeGetString("NUMERO_COMPROMISO").Trim(),
            SafeGetNullableDateTime(reader, "FECHA_COMPROMISO"),
            reader.SafeGetString("NOMBRE_PROVEEDOR").Trim(),
            reader.SafeGetDecimal("MONTO_COMPROMISO"),
            reader.SafeGetString("ORIGEN").Trim()
        );
    }

    private static DateTime? SafeGetNullableDateTime(IDataReader reader, string columnName)
    {
        int ordinal = reader.GetOrdinal(columnName);

        return reader.IsDBNull(ordinal) ? null : Convert.ToDateTime(reader.GetValue(ordinal));
    }
}

[ApiController]
[Route("api/ReporteRelacionCompromiso")]
public class ReporteRelacionCompromisoController(
    ConnectionDB _connectionDB,
    IConfiguration _config) : ControllerBase
{
    [HttpPost]
    [Route("GetAll")]
    public async Task<IActionResult> GetAll(ReporteRelacionCompromisoQuery value)
    {
        var handler = new ReporteRelacionCompromisoHandler(_connectionDB, _config);
        var result = await handler.HandleAsync(value);

        return Ok(result);
    }

    /// <summary>
    /// PDF del reporte. El usuario conectado es obligatorio: el reporte legado
    /// imprimia el login en el pie de cada pagina (<c>user$currentdate</c>), y el
    /// pie de auditoria compartido del requerimiento 17 hace lo mismo.
    /// </summary>
    [HttpPost]
    [Route("pdf")]
    public async Task<IActionResult> Pdf(ReporteRelacionCompromisoQuery value)
    {
        if (string.IsNullOrWhiteSpace(value.Usuario))
        {
            return BadRequest(new ResultDto<object?>(null)
            {
                IsValid = false,
                Message = "El usuario conectado es requerido para generar la relacion de compromisos."
            });
        }

        var handler = new ReporteRelacionCompromisoHandler(_connectionDB, _config);
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
                Message = "No hay compromisos con los filtros seleccionados."
            });
        }

        var entidad = await handler.ObtenerEntidadAsync(value.CodigoPresupuesto);
        var printContext = ReportPrintContext.Create(value.Usuario);
        var bytes = ReporteRelacionCompromisoPdfGenerator.Generate(result.Data, value, entidad, printContext);

        Response.Headers.ContentDisposition = "inline; filename=\"relacion-compromisos.pdf\"";

        return File(bytes, "application/pdf", enableRangeProcessing: true);
    }
}
