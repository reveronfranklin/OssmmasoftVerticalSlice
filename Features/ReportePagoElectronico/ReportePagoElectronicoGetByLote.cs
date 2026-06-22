using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.ReportePagoElectronico;

public record ReportePagoElectronicoGetByLoteQuery(int CodigoLotePago);

public record ReportePagoElectronicoItemResponse(
    int CodigoLotePago,
    int CodigoPago,
    string NumeroPago,
    DateTime? FechaPago,
    string Nombre,
    string NumeroCuenta,
    string PagarALaOrdenDe,
    string Motivo,
    decimal Monto,
    string DetalleOpIcpPuc,
    decimal MontoOpIcpPuc,
    string DetalleImpRet,
    decimal MontoImpRet,
    string TituloReporte
);

public class ReportePagoElectronicoGetByLoteHandler(ConnectionDB _connectionDB)
{
    public async Task<ResultDto<List<ReportePagoElectronicoItemResponse>>> HandleAsync(ReportePagoElectronicoGetByLoteQuery query)
    {
        if (query.CodigoLotePago <= 0)
        {
            return BuildInvalidResult("El parametro CodigoLotePago debe ser mayor que cero.");
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

        using var cmd = new OracleCommand("ADM.SP_REP_PAG_ELE_GET", cn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.BindByName = true;

        cmd.Parameters.Add("p_CodigoLotePago", OracleDbType.Int32).Value = query.CodigoLotePago;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
        var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
        var pTotalRecords = cmd.Parameters.Add("p_TotalRecords", OracleDbType.Int32, ParameterDirection.Output);

        var list = new List<ReportePagoElectronicoItemResponse>();

        try
        {
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    list.Add(ReportePagoElectronicoDb.MapItem(reader));
                }
            }

            var dbMessage = ReportePagoElectronicoDb.GetMessage(pMessage);
            var isSuccess = ReportePagoElectronicoDb.IsSuccessMessage(dbMessage);

            return new ResultDto<List<ReportePagoElectronicoItemResponse>>(list)
            {
                Data = isSuccess ? list : null,
                CantidadRegistros = ReportePagoElectronicoDb.GetIntOutput(pTotalRecords),
                IsValid = isSuccess,
                Message = dbMessage
            };
        }
        catch (Exception ex)
        {
            return BuildInvalidResult($"Error tecnico: {ex.Message}");
        }
    }

    private static ResultDto<List<ReportePagoElectronicoItemResponse>> BuildInvalidResult(string message)
    {
        return new ResultDto<List<ReportePagoElectronicoItemResponse>>(new List<ReportePagoElectronicoItemResponse>())
        {
            Data = null,
            IsValid = false,
            Message = message
        };
    }
}

internal static class ReportePagoElectronicoDb
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

    public static ReportePagoElectronicoItemResponse MapItem(IDataReader reader)
    {
        return new ReportePagoElectronicoItemResponse(
            reader.SafeGetInt32("CODIGO_LOTE_PAGO"),
            reader.SafeGetInt32("CODIGO_PAGO"),
            reader.SafeGetString("NUMERO_PAGO"),
            SafeGetNullableDateTime(reader, "FECHA_PAGO"),
            reader.SafeGetString("NOMBRE"),
            reader.SafeGetString("NO_CUENTA"),
            reader.SafeGetString("PAGAR_A_LA_ORDEN_DE"),
            reader.SafeGetString("MOTIVO"),
            reader.SafeGetDecimal("MONTO"),
            reader.SafeGetString("DETALLE_OP_ICP_PUC"),
            reader.SafeGetDecimal("MONTO_OP_ICP_PUC"),
            reader.SafeGetString("DETALLE_IMP_RET"),
            reader.SafeGetDecimal("MONTO_IMP_RET"),
            reader.SafeGetString("TITULO_REPORTE")
        );
    }

    private static DateTime? SafeGetNullableDateTime(IDataReader reader, string columnName)
    {
        int ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : Convert.ToDateTime(reader.GetValue(ordinal));
    }
}

[ApiController]
[Route("api/ReportePagoElectronico")]
public class ReportePagoElectronicoGetByLoteController(ConnectionDB _connectionDB) : ControllerBase
{
    [HttpPost]
    [Route("GetByLote")]
    public async Task<IActionResult> GetByLote(ReportePagoElectronicoGetByLoteQuery value)
    {
        var handler = new ReportePagoElectronicoGetByLoteHandler(_connectionDB);
        var result = await handler.HandleAsync(value);
        return Ok(result);
    }
}
