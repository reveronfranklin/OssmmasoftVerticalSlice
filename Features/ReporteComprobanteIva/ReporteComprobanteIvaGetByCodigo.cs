using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.ReporteComprobanteIva;

public record ReporteComprobanteIvaGetByCodigoQuery(int CodigoOrdenPago);

public record ReporteComprobanteIvaResponse(
    ReporteComprobanteIvaHeaderResponse? Header,
    List<ReporteComprobanteIvaDocumentoResponse> Documentos
);

public record ReporteComprobanteIvaHeaderResponse(
    int CodigoOrdenPago,
    string NumeroOrdenPago,
    string NumeroComprobante,
    DateTime? Fecha,
    string NombreAgenteRetencion,
    string RifAgenteRetencion,
    string DireccionAgenteRetencion,
    string NombreSujetoRetenido,
    string RifSujetoRetenido,
    string Status
);

public record ReporteComprobanteIvaDocumentoResponse(
    int NumeroOperacion,
    DateTime? FechaFactura,
    string NumeroFactura,
    string NumeroControlFactura,
    string NumeroNotaDebito,
    string NumeroNotaCredito,
    string TipoTransaccion,
    string NumeroFacturaAfectada,
    decimal TotalComprasIncluyendoIva,
    decimal ComprasSinDerechoCredito,
    decimal BaseImponible,
    string Alicuota,
    decimal ImpuestoIva,
    decimal IvaRetenido
);

public class ReporteComprobanteIvaGetByCodigoHandler(ConnectionDB _connectionDB)
{
    public async Task<ResultDto<ReporteComprobanteIvaResponse>> HandleAsync(ReporteComprobanteIvaGetByCodigoQuery query)
    {
        if (query.CodigoOrdenPago <= 0)
        {
            return BuildInvalidResult("El parametro CodigoOrdenPago debe ser mayor que cero.");
        }

        using var cn = _connectionDB.GetAdmConnection();
        try
        {
            await cn.OpenAsync();
        }
        catch (Exception ex)
        {
            return BuildInvalidResult($"Error tecnico al abrir conexion ADM: {ex.Message}");
        }

        var headerResult = await ExecuteListAsync(
            cn,
            "ADM.SP_REP_COMP_IVA_GET",
            query.CodigoOrdenPago,
            ReporteComprobanteIvaDb.MapHeader);

        if (!headerResult.IsValid)
        {
            return BuildInvalidResult(headerResult.Message);
        }

        var header = headerResult.Data?.FirstOrDefault();
        if (header is null)
        {
            return BuildInvalidResult("No se encontro la orden de pago solicitada.");
        }

        var documentosResult = await ExecuteListAsync(
            cn,
            "ADM.SP_REP_COMP_IVA_DOC_GET",
            query.CodigoOrdenPago,
            ReporteComprobanteIvaDb.MapDocumento);

        if (!documentosResult.IsValid)
        {
            return BuildInvalidResult(documentosResult.Message);
        }

        var response = new ReporteComprobanteIvaResponse(
            header,
            documentosResult.Data ?? new List<ReporteComprobanteIvaDocumentoResponse>()
        );

        return new ResultDto<ReporteComprobanteIvaResponse>(response)
        {
            Data = response,
            CantidadRegistros = headerResult.CantidadRegistros + documentosResult.CantidadRegistros,
            IsValid = true,
            Message = "Success"
        };
    }

    private static async Task<ResultDto<List<T>>> ExecuteListAsync<T>(
        OracleConnection cn,
        string procedure,
        int codigoOrdenPago,
        Func<IDataReader, T> map)
    {
        using var cmd = new OracleCommand(procedure, cn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.BindByName = true;

        cmd.Parameters.Add("p_CodigoOrdenPago", OracleDbType.Int32).Value = codigoOrdenPago;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
        var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
        var pTotalRecords = cmd.Parameters.Add("p_TotalRecords", OracleDbType.Int32, ParameterDirection.Output);

        var list = new List<T>();

        try
        {
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    list.Add(map(reader));
                }
            }

            var dbMessage = ReporteComprobanteIvaDb.GetMessage(pMessage);
            var isSuccess = ReporteComprobanteIvaDb.IsSuccessMessage(dbMessage);

            return new ResultDto<List<T>>(list)
            {
                Data = isSuccess ? list : null,
                CantidadRegistros = ReporteComprobanteIvaDb.GetIntOutput(pTotalRecords),
                IsValid = isSuccess,
                Message = dbMessage
            };
        }
        catch (Exception ex)
        {
            return new ResultDto<List<T>>(new List<T>())
            {
                Data = null,
                IsValid = false,
                Message = $"Error tecnico: {ex.Message}"
            };
        }
    }

    private static ResultDto<ReporteComprobanteIvaResponse> BuildInvalidResult(string message)
    {
        return new ResultDto<ReporteComprobanteIvaResponse>(new ReporteComprobanteIvaResponse(null, new()))
        {
            Data = null,
            IsValid = false,
            Message = message
        };
    }
}

internal static class ReporteComprobanteIvaDb
{
    public static bool IsSuccessMessage(string? message)
    {
        return string.Equals(message, "success", StringComparison.OrdinalIgnoreCase)
            || string.Equals(message, "suscces", StringComparison.OrdinalIgnoreCase);
    }

    public static string GetMessage(OracleParameter parameter, string defaultMessage = "Sin respuesta de BD")
    {
        return parameter.Value == DBNull.Value ? defaultMessage : parameter.Value?.ToString() ?? defaultMessage;
    }

    public static int GetIntOutput(OracleParameter parameter)
    {
        return parameter.Value == DBNull.Value ? 0 : Convert.ToInt32(parameter.Value.ToString());
    }

    public static ReporteComprobanteIvaHeaderResponse MapHeader(IDataReader reader)
    {
        return new ReporteComprobanteIvaHeaderResponse(
            reader.SafeGetInt32("CODIGO_ORDEN_PAGO"),
            reader.SafeGetString("NUMERO_ORDEN_PAGO"),
            reader.SafeGetString("NUMERO_COMPROBANTE"),
            SafeGetNullableDateTime(reader, "FECHA"),
            reader.SafeGetString("NOMBRE_AGENTE_RETENCION"),
            reader.SafeGetString("RIF_AGENTE_RETENCION"),
            reader.SafeGetString("DIRECCION_AGENTE_RETENCION"),
            reader.SafeGetString("NOMBRE_SUJETO_RETENIDO"),
            reader.SafeGetString("RIF_SUJETO_RETENIDO"),
            reader.SafeGetString("STATUS")
        );
    }

    public static ReporteComprobanteIvaDocumentoResponse MapDocumento(IDataReader reader)
    {
        return new ReporteComprobanteIvaDocumentoResponse(
            reader.SafeGetInt32("NUMERO_OPERACION"),
            SafeGetNullableDateTime(reader, "FECHA_FACTURA"),
            reader.SafeGetString("NUMERO_FACTURA"),
            reader.SafeGetString("NUMERO_CONTROL_FACTURA"),
            reader.SafeGetString("NUMERO_NOTA_DEBITO"),
            reader.SafeGetString("NUMERO_NOTA_CREDITO"),
            reader.SafeGetString("TIPO_TRANSACCION"),
            reader.SafeGetString("NUMERO_FACTURA_AFECTADA"),
            reader.SafeGetDecimal("TOTAL_COMPRAS_IVA"),
            reader.SafeGetDecimal("COMPRAS_SIN_CREDITO"),
            reader.SafeGetDecimal("BASE_IMPONIBLE"),
            reader.SafeGetString("ALICUOTA"),
            reader.SafeGetDecimal("IMPUESTO_IVA"),
            reader.SafeGetDecimal("IVA_RETENIDO")
        );
    }

    private static DateTime? SafeGetNullableDateTime(IDataReader reader, string columnName)
    {
        int ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : Convert.ToDateTime(reader.GetValue(ordinal));
    }
}

[ApiController]
[Route("api/ReporteComprobanteIva")]
public class ReporteComprobanteIvaGetByCodigoController(ConnectionDB _connectionDB) : ControllerBase
{
    [HttpPost]
    [Route("GetByCodigo")]
    public async Task<IActionResult> GetByCodigo(ReporteComprobanteIvaGetByCodigoQuery value)
    {
        var handler = new ReporteComprobanteIvaGetByCodigoHandler(_connectionDB);
        var result = await handler.HandleAsync(value);
        return Ok(result);
    }
}
