using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.ReporteRetencionIslr;

public record ReporteRetencionIslrGetByCodigoQuery(int CodigoOrdenPago);

public record ReporteRetencionIslrResponse(
    ReporteRetencionIslrHeaderResponse? Header,
    List<ReporteRetencionIslrDocumentoResponse> Documentos
);

public record ReporteRetencionIslrHeaderResponse(
    int CodigoOrdenPago,
    string NumeroOrdenPago,
    DateTime? Fecha,
    string NombreAgenteRetencion,
    string TelefonoAgenteRetencion,
    string RifAgenteRetencion,
    string DireccionAgenteRetencion,
    string NombreSujetoRetenido,
    string RifSujetoRetenido,
    string Status
);

public record ReporteRetencionIslrDocumentoResponse(
    string NumeroFactura,
    DateTime? FechaFactura,
    string ConceptoPago,
    decimal ImpuestoExento,
    decimal BaseImponible,
    string Alicuota,
    decimal IslrRetenido,
    decimal Sustraendo
);

public class ReporteRetencionIslrGetByCodigoHandler(ConnectionDB _connectionDB)
{
    public async Task<ResultDto<ReporteRetencionIslrResponse>> HandleAsync(ReporteRetencionIslrGetByCodigoQuery query)
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

        var headerResult = await ExecuteListAsync(cn, "ADM.SP_REP_RET_ISLR_GET", query.CodigoOrdenPago, ReporteRetencionIslrDb.MapHeader);
        if (!headerResult.IsValid)
        {
            return BuildInvalidResult(headerResult.Message);
        }

        var header = headerResult.Data?.FirstOrDefault();
        if (header is null)
        {
            return BuildInvalidResult("No se encontro la orden de pago solicitada.");
        }

        var documentosResult = await ExecuteListAsync(cn, "ADM.SP_REP_RET_ISLR_DOC_GET", query.CodigoOrdenPago, ReporteRetencionIslrDb.MapDocumento);
        if (!documentosResult.IsValid)
        {
            return BuildInvalidResult(documentosResult.Message);
        }

        var response = new ReporteRetencionIslrResponse(
            header,
            documentosResult.Data ?? new List<ReporteRetencionIslrDocumentoResponse>()
        );

        return new ResultDto<ReporteRetencionIslrResponse>(response)
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

            var dbMessage = ReporteRetencionIslrDb.GetMessage(pMessage);
            var isSuccess = ReporteRetencionIslrDb.IsSuccessMessage(dbMessage);

            return new ResultDto<List<T>>(list)
            {
                Data = isSuccess ? list : null,
                CantidadRegistros = ReporteRetencionIslrDb.GetIntOutput(pTotalRecords),
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

    private static ResultDto<ReporteRetencionIslrResponse> BuildInvalidResult(string message)
    {
        return new ResultDto<ReporteRetencionIslrResponse>(new ReporteRetencionIslrResponse(null, new()))
        {
            Data = null,
            IsValid = false,
            Message = message
        };
    }
}

internal static class ReporteRetencionIslrDb
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

    public static ReporteRetencionIslrHeaderResponse MapHeader(IDataReader reader)
    {
        return new ReporteRetencionIslrHeaderResponse(
            reader.SafeGetInt32("CODIGO_ORDEN_PAGO"),
            reader.SafeGetString("NUMERO_ORDEN_PAGO"),
            SafeGetNullableDateTime(reader, "FECHA"),
            reader.SafeGetString("NOMBRE_AGENTE_RETENCION"),
            reader.SafeGetString("TELEFONO_AGENTE_RETENCION"),
            reader.SafeGetString("RIF_AGENTE_RETENCION"),
            reader.SafeGetString("DIRECCION_AGENTE_RETENCION"),
            reader.SafeGetString("NOMBRE_SUJETO_RETENIDO"),
            reader.SafeGetString("RIF_SUJETO_RETENIDO"),
            reader.SafeGetString("STATUS")
        );
    }

    public static ReporteRetencionIslrDocumentoResponse MapDocumento(IDataReader reader)
    {
        return new ReporteRetencionIslrDocumentoResponse(
            reader.SafeGetString("NUMERO_FACTURA"),
            SafeGetNullableDateTime(reader, "FECHA_FACTURA"),
            reader.SafeGetString("CONCEPTO_PAGO"),
            reader.SafeGetDecimal("IMPUESTO_EXENTO"),
            reader.SafeGetDecimal("BASE_IMPONIBLE"),
            reader.SafeGetString("ALICUOTA"),
            reader.SafeGetDecimal("ISLR_RETENIDO"),
            reader.SafeGetDecimal("SUSTRAENDO")
        );
    }

    private static DateTime? SafeGetNullableDateTime(IDataReader reader, string columnName)
    {
        int ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : Convert.ToDateTime(reader.GetValue(ordinal));
    }
}

[ApiController]
[Route("api/ReporteRetencionIslr")]
public class ReporteRetencionIslrGetByCodigoController(ConnectionDB _connectionDB) : ControllerBase
{
    [HttpPost]
    [Route("GetByCodigo")]
    public async Task<IActionResult> GetByCodigo(ReporteRetencionIslrGetByCodigoQuery value)
    {
        var handler = new ReporteRetencionIslrGetByCodigoHandler(_connectionDB);
        var result = await handler.HandleAsync(value);
        return Ok(result);
    }
}
