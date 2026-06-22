using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.ReporteNotaDebitoTerceros;

public record ReporteNotaDebitoTercerosGetByLotePagoQuery(int CodigoLotePago, int CodigoPago = 0);

public record ReporteNotaDebitoTercerosItemResponse(
    int CodigoLotePago,
    int CodigoCheque,
    string NumeroCheque,
    DateTime? FechaCheque,
    string Nombre,
    string NumeroCuenta,
    string PagarALaOrdenDe,
    string Motivo,
    decimal Monto,
    string DetalleOpIcpPuc,
    decimal MontoOpIcpPuc,
    string DetalleImpRet,
    decimal MontoImpRet
);

public class ReporteNotaDebitoTercerosGetByLotePagoHandler(ConnectionDB _connectionDB)
{
    public async Task<ResultDto<List<ReporteNotaDebitoTercerosItemResponse>>> HandleAsync(ReporteNotaDebitoTercerosGetByLotePagoQuery query)
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

        using var cmd = new OracleCommand("ADM.SP_REP_NOT_DEB_TER_GET", cn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.BindByName = true;

        cmd.Parameters.Add("p_CodigoLotePago", OracleDbType.Int32).Value = query.CodigoLotePago;
        cmd.Parameters.Add("p_CodigoPago", OracleDbType.Int32).Value = query.CodigoPago;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
        var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
        var pTotalRecords = cmd.Parameters.Add("p_TotalRecords", OracleDbType.Int32, ParameterDirection.Output);

        var list = new List<ReporteNotaDebitoTercerosItemResponse>();

        try
        {
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    list.Add(ReporteNotaDebitoTercerosDb.MapItem(reader));
                }
            }

            var dbMessage = ReporteNotaDebitoTercerosDb.GetMessage(pMessage);
            var isSuccess = ReporteNotaDebitoTercerosDb.IsSuccessMessage(dbMessage);

            return new ResultDto<List<ReporteNotaDebitoTercerosItemResponse>>(list)
            {
                Data = isSuccess ? list : null,
                CantidadRegistros = ReporteNotaDebitoTercerosDb.GetIntOutput(pTotalRecords),
                IsValid = isSuccess,
                Message = dbMessage
            };
        }
        catch (Exception ex)
        {
            return BuildInvalidResult($"Error tecnico: {ex.Message}");
        }
    }

    private static ResultDto<List<ReporteNotaDebitoTercerosItemResponse>> BuildInvalidResult(string message)
    {
        return new ResultDto<List<ReporteNotaDebitoTercerosItemResponse>>(new List<ReporteNotaDebitoTercerosItemResponse>())
        {
            Data = null,
            IsValid = false,
            Message = message
        };
    }
}

internal static class ReporteNotaDebitoTercerosDb
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

    public static ReporteNotaDebitoTercerosItemResponse MapItem(IDataReader reader)
    {
        return new ReporteNotaDebitoTercerosItemResponse(
            reader.SafeGetInt32("CODIGO_LOTE_PAGO"),
            reader.SafeGetInt32("CODIGO_CHEQUE"),
            reader.SafeGetString("NUMERO_CHEQUE"),
            SafeGetNullableDateTime(reader, "FECHA_CHEQUE"),
            reader.SafeGetString("NOMBRE"),
            reader.SafeGetString("NO_CUENTA"),
            reader.SafeGetString("PAGAR_A_LA_ORDEN_DE"),
            reader.SafeGetString("MOTIVO"),
            reader.SafeGetDecimal("MONTO"),
            reader.SafeGetString("DETALLE_OP_ICP_PUC"),
            reader.SafeGetDecimal("MONTO_OP_ICP_PUC"),
            reader.SafeGetString("DETALLE_IMP_RET"),
            reader.SafeGetDecimal("MONTO_IMP_RET")
        );
    }

    private static DateTime? SafeGetNullableDateTime(IDataReader reader, string columnName)
    {
        int ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : Convert.ToDateTime(reader.GetValue(ordinal));
    }
}

[ApiController]
[Route("api/ReporteNotaDebitoTerceros")]
public class ReporteNotaDebitoTercerosGetByLotePagoController(ConnectionDB _connectionDB) : ControllerBase
{
    [HttpPost]
    [Route("GetByLotePago")]
    public async Task<IActionResult> GetByLotePago(ReporteNotaDebitoTercerosGetByLotePagoQuery value)
    {
        var handler = new ReporteNotaDebitoTercerosGetByLotePagoHandler(_connectionDB);
        var result = await handler.HandleAsync(value);
        return Ok(result);
    }
}
