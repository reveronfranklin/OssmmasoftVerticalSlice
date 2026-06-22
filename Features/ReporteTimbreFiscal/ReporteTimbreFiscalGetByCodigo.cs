using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.ReporteTimbreFiscal;

public record ReporteTimbreFiscalGetByCodigoQuery(int CodigoOrdenPago);

public record ReporteTimbreFiscalResponse(
    ReporteTimbreFiscalHeaderResponse? Header,
    List<ReporteTimbreFiscalDocumentoResponse> Documentos
);

public record ReporteTimbreFiscalHeaderResponse(
    int CodigoOrdenPago,
    string NumeroOrdenPago,
    string NombreAgenteRetencion,
    string RifAgenteRetencion,
    string NombreContribuyente,
    string RifContribuyente,
    string Motivo,
    string Status,
    decimal BaseImponible,
    decimal MontoRetencion
);

public record ReporteTimbreFiscalDocumentoResponse(
    string NumeroControlFactura,
    string NumeroFactura,
    decimal MontoDocumento,
    decimal MontoExento,
    decimal MontoIva
);

public class ReporteTimbreFiscalGetByCodigoHandler(ConnectionDB _connectionDB)
{
    public async Task<ResultDto<ReporteTimbreFiscalResponse>> HandleAsync(ReporteTimbreFiscalGetByCodigoQuery query)
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
            "ADM.SP_REP_TIM_FIS_GET",
            query.CodigoOrdenPago,
            ReporteTimbreFiscalDb.MapHeader);

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
            "ADM.SP_REP_TIM_FIS_DOC_GET",
            query.CodigoOrdenPago,
            ReporteTimbreFiscalDb.MapDocumento);

        if (!documentosResult.IsValid)
        {
            return BuildInvalidResult(documentosResult.Message);
        }

        var response = new ReporteTimbreFiscalResponse(
            header,
            documentosResult.Data ?? new List<ReporteTimbreFiscalDocumentoResponse>()
        );

        return new ResultDto<ReporteTimbreFiscalResponse>(response)
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

            var dbMessage = ReporteTimbreFiscalDb.GetMessage(pMessage);
            var isSuccess = ReporteTimbreFiscalDb.IsSuccessMessage(dbMessage);

            return new ResultDto<List<T>>(list)
            {
                Data = isSuccess ? list : null,
                CantidadRegistros = ReporteTimbreFiscalDb.GetIntOutput(pTotalRecords),
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

    private static ResultDto<ReporteTimbreFiscalResponse> BuildInvalidResult(string message)
    {
        return new ResultDto<ReporteTimbreFiscalResponse>(new ReporteTimbreFiscalResponse(null, new()))
        {
            Data = null,
            IsValid = false,
            Message = message
        };
    }
}

internal static class ReporteTimbreFiscalDb
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

    public static ReporteTimbreFiscalHeaderResponse MapHeader(IDataReader reader)
    {
        return new ReporteTimbreFiscalHeaderResponse(
            reader.SafeGetInt32("CODIGO_ORDEN_PAGO"),
            reader.SafeGetString("NUMERO_ORDEN_PAGO"),
            reader.SafeGetString("NOMBRE_AGENTE_RETENCION"),
            reader.SafeGetString("RIF_AGENTE_RETENCION"),
            reader.SafeGetString("NOMBRE_CONTRIBUYENTE"),
            reader.SafeGetString("RIF_CONTRIBUYENTE"),
            reader.SafeGetString("MOTIVO"),
            reader.SafeGetString("STATUS"),
            reader.SafeGetDecimal("BASE_IMPONIBLE"),
            reader.SafeGetDecimal("MONTO_RETENCION")
        );
    }

    public static ReporteTimbreFiscalDocumentoResponse MapDocumento(IDataReader reader)
    {
        return new ReporteTimbreFiscalDocumentoResponse(
            reader.SafeGetString("NUMERO_CONTROL_FACTURA"),
            reader.SafeGetString("NUMERO_FACTURA"),
            reader.SafeGetDecimal("MONTO_DOCUMENTO"),
            reader.SafeGetDecimal("MONTO_EXENTO"),
            reader.SafeGetDecimal("MONTO_IVA")
        );
    }
}

[ApiController]
[Route("api/ReporteTimbreFiscal")]
public class ReporteTimbreFiscalGetByCodigoController(ConnectionDB _connectionDB) : ControllerBase
{
    [HttpPost]
    [Route("GetByCodigo")]
    public async Task<IActionResult> GetByCodigo(ReporteTimbreFiscalGetByCodigoQuery value)
    {
        var handler = new ReporteTimbreFiscalGetByCodigoHandler(_connectionDB);
        var result = await handler.HandleAsync(value);
        return Ok(result);
    }
}
