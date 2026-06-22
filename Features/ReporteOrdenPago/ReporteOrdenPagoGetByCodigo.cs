using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.ReporteOrdenPago;

public record ReporteOrdenPagoGetByCodigoQuery(int CodigoOrdenPago);

public record ReporteOrdenPagoResponse(
    ReporteOrdenPagoHeaderResponse? Header,
    List<ReporteOrdenPagoFondoResponse> Fondos,
    List<ReporteOrdenPagoRetencionResponse> Retenciones
);

public record ReporteOrdenPagoHeaderResponse(
    int CodigoOrdenPago,
    string TituloReporte,
    string TipoOrdenPago,
    string NumeroOrdenPago,
    DateTime? FechaOrdenPago,
    string NumeroCompromiso,
    DateTime? FechaCompromiso,
    string NombreProveedor,
    string CedulaProveedor,
    string RifProveedor,
    string NombreBeneficiario,
    string ApellidoBeneficiario,
    string CedulaBeneficiario,
    DateTime? FechaPlazoDesde,
    DateTime? FechaPlazoHasta,
    string MontoLetras,
    string FormaPago,
    decimal CantidadPago,
    string Motivo,
    string Status
);

public record ReporteOrdenPagoFondoResponse(
    int Ano,
    string DescripcionFinanciado,
    string CodigoIcpConcat,
    string CodigoPucConcat,
    string DenominacionPuc,
    decimal Monto
);

public record ReporteOrdenPagoRetencionResponse(
    string Descripcion,
    decimal PorRetencion,
    decimal MontoRetencion
);

public class ReporteOrdenPagoGetByCodigoHandler(ConnectionDB _connectionDB)
{
    public async Task<ResultDto<ReporteOrdenPagoResponse>> HandleAsync(ReporteOrdenPagoGetByCodigoQuery query)
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
            "ADM.SP_REP_ORD_PAGO_GET",
            query.CodigoOrdenPago,
            ReporteOrdenPagoDb.MapHeader);

        if (!headerResult.IsValid)
        {
            return BuildInvalidResult(headerResult.Message);
        }

        var header = headerResult.Data?.FirstOrDefault();
        if (header is null)
        {
            return BuildInvalidResult("No se encontro la orden de pago solicitada.");
        }

        var fondosResult = await ExecuteListAsync(
            cn,
            "ADM.SP_REP_ORD_PAGO_PUC_GET",
            query.CodigoOrdenPago,
            ReporteOrdenPagoDb.MapFondo);

        if (!fondosResult.IsValid)
        {
            return BuildInvalidResult(fondosResult.Message);
        }

        var retencionesResult = await ExecuteListAsync(
            cn,
            "ADM.SP_REP_ORD_PAGO_RET_GET",
            query.CodigoOrdenPago,
            ReporteOrdenPagoDb.MapRetencion);

        if (!retencionesResult.IsValid)
        {
            return BuildInvalidResult(retencionesResult.Message);
        }

        var response = new ReporteOrdenPagoResponse(
            header,
            fondosResult.Data ?? new List<ReporteOrdenPagoFondoResponse>(),
            retencionesResult.Data ?? new List<ReporteOrdenPagoRetencionResponse>()
        );

        return new ResultDto<ReporteOrdenPagoResponse>(response)
        {
            Data = response,
            CantidadRegistros = headerResult.CantidadRegistros + fondosResult.CantidadRegistros + retencionesResult.CantidadRegistros,
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

            var dbMessage = ReporteOrdenPagoDb.GetMessage(pMessage);
            var isSuccess = ReporteOrdenPagoDb.IsSuccessMessage(dbMessage);

            return new ResultDto<List<T>>(list)
            {
                Data = isSuccess ? list : null,
                CantidadRegistros = ReporteOrdenPagoDb.GetIntOutput(pTotalRecords),
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

    private static ResultDto<ReporteOrdenPagoResponse> BuildInvalidResult(string message)
    {
        return new ResultDto<ReporteOrdenPagoResponse>(new ReporteOrdenPagoResponse(null, new(), new()))
        {
            Data = null,
            IsValid = false,
            Message = message
        };
    }
}

internal static class ReporteOrdenPagoDb
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

    public static ReporteOrdenPagoHeaderResponse MapHeader(IDataReader reader)
    {
        return new ReporteOrdenPagoHeaderResponse(
            reader.SafeGetInt32("CODIGO_ORDEN_PAGO"),
            reader.SafeGetString("TITULO_REPORTE"),
            reader.SafeGetString("TIPO_ORDEN_PAGO"),
            reader.SafeGetString("NUMERO_ORDEN_PAGO"),
            SafeGetNullableDateTime(reader, "FECHA_ORDEN_PAGO"),
            reader.SafeGetString("NUMERO_COMPROMISO"),
            SafeGetNullableDateTime(reader, "FECHA_COMPROMISO"),
            reader.SafeGetString("NOMBRE_PROVEEDOR"),
            reader.SafeGetString("CEDULA_PROVEEDOR"),
            reader.SafeGetString("RIF_PROVEEDOR"),
            reader.SafeGetString("NOMBRE_BENEFICIARIO"),
            reader.SafeGetString("APELLIDO_BENEFICIARIO"),
            reader.SafeGetString("CEDULA_BENEFICIARIO"),
            SafeGetNullableDateTime(reader, "FECHA_PLAZO_DESDE"),
            SafeGetNullableDateTime(reader, "FECHA_PLAZO_HASTA"),
            reader.SafeGetString("MONTO_LETRAS"),
            reader.SafeGetString("FORMA_PAGO"),
            reader.SafeGetDecimal("CANTIDAD_PAGO"),
            reader.SafeGetString("MOTIVO"),
            reader.SafeGetString("STATUS")
        );
    }

    public static ReporteOrdenPagoFondoResponse MapFondo(IDataReader reader)
    {
        return new ReporteOrdenPagoFondoResponse(
            reader.SafeGetInt32("ANO"),
            reader.SafeGetString("DESCRIPCION_FINANCIADO"),
            reader.SafeGetString("CODIGO_ICP_CONCAT"),
            reader.SafeGetString("CODIGO_PUC_CONCAT"),
            reader.SafeGetString("DENOMINACION_PUC"),
            reader.SafeGetDecimal("MONTO")
        );
    }

    public static ReporteOrdenPagoRetencionResponse MapRetencion(IDataReader reader)
    {
        return new ReporteOrdenPagoRetencionResponse(
            reader.SafeGetString("DESCRIPCION"),
            reader.SafeGetDecimal("POR_RETENCION"),
            reader.SafeGetDecimal("MONTO_RETENCION")
        );
    }

    private static DateTime? SafeGetNullableDateTime(IDataReader reader, string columnName)
    {
        int ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : Convert.ToDateTime(reader.GetValue(ordinal));
    }
}

[ApiController]
[Route("api/ReporteOrdenPago")]
public class ReporteOrdenPagoGetByCodigoController(ConnectionDB _connectionDB) : ControllerBase
{
    [HttpPost]
    [Route("GetByCodigo")]
    public async Task<IActionResult> GetByCodigo(ReporteOrdenPagoGetByCodigoQuery value)
    {
        var handler = new ReporteOrdenPagoGetByCodigoHandler(_connectionDB);
        var result = await handler.HandleAsync(value);
        return Ok(result);
    }
}
