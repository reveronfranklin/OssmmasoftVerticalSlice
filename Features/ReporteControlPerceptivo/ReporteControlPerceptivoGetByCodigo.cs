using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.ReporteControlPerceptivo;

public record ReporteControlPerceptivoGetByCodigoQuery(int CodigoCompromiso);

public record ReporteControlPerceptivoResponse(
    ReporteControlPerceptivoHeaderResponse? Header,
    List<ReporteControlPerceptivoDetalleResponse> Detalle,
    decimal SubTotal,
    decimal MontoImpuesto,
    decimal MontoTotal,
    string MontoLetras
);

public record ReporteControlPerceptivoHeaderResponse(
    int CodigoCompromiso,
    string NumeroCompromiso,
    DateTime? FechaCompromiso,
    string Proveedor,
    string Solicitante,
    string DireccionEmpresa,
    string NombreEmpresa,
    string FechaEmisionTexto
);

public record ReporteControlPerceptivoDetalleResponse(
    decimal Cantidad,
    string Udm,
    string DescripcionArticulo,
    decimal PrecioUnitario,
    decimal Precio,
    decimal PorImpuesto,
    decimal MontoImpuesto
);

public class ReporteControlPerceptivoGetByCodigoHandler(ConnectionDB _connectionDB)
{
    public async Task<ResultDto<ReporteControlPerceptivoResponse>> HandleAsync(ReporteControlPerceptivoGetByCodigoQuery query)
    {
        if (query.CodigoCompromiso <= 0)
        {
            return BuildInvalidResult("El parametro CodigoCompromiso debe ser mayor que cero.");
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
            "ADM.SP_ADM_CTRL_PERCEP_HDR_GET",
            query.CodigoCompromiso,
            ReporteControlPerceptivoDb.MapHeader);

        if (!headerResult.IsValid)
        {
            return BuildInvalidResult(headerResult.Message);
        }

        var header = headerResult.Data?.FirstOrDefault();
        if (header is null)
        {
            return BuildInvalidResult("No se encontro el compromiso o contrato solicitado.");
        }

        var detalleResult = await ExecuteListAsync(
            cn,
            "ADM.SP_ADM_CTRL_PERCEP_DET_GET",
            query.CodigoCompromiso,
            ReporteControlPerceptivoDb.MapDetalle);

        if (!detalleResult.IsValid)
        {
            return BuildInvalidResult(detalleResult.Message);
        }

        var detalle = detalleResult.Data ?? new List<ReporteControlPerceptivoDetalleResponse>();
        var subTotal = detalle.Sum(item => item.Precio);
        var montoImpuesto = detalle.Sum(item => item.MontoImpuesto);
        var montoTotal = subTotal + montoImpuesto;
        var montoLetras = await GetMontoLetrasAsync(cn, montoTotal);

        var response = new ReporteControlPerceptivoResponse(
            header,
            detalle,
            subTotal,
            montoImpuesto,
            montoTotal,
            montoLetras
        );

        return new ResultDto<ReporteControlPerceptivoResponse>(response)
        {
            Data = response,
            CantidadRegistros = headerResult.CantidadRegistros + detalleResult.CantidadRegistros,
            IsValid = true,
            Message = "Success"
        };
    }

    private static async Task<string> GetMontoLetrasAsync(OracleConnection cn, decimal monto)
    {
        using var cmd = new OracleCommand("ADM.SP_ADM_CTRL_PERCEP_LETRAS_GET", cn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.BindByName = true;

        cmd.Parameters.Add("p_Monto", OracleDbType.Decimal).Value = monto;
        var pTexto = cmd.Parameters.Add("p_Texto", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
        var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

        try
        {
            await cmd.ExecuteNonQueryAsync();

            var dbMessage = ReporteControlPerceptivoDb.GetMessage(pMessage);
            if (!ReporteControlPerceptivoDb.IsSuccessMessage(dbMessage))
            {
                return string.Empty;
            }

            return pTexto.Value == DBNull.Value ? string.Empty : pTexto.Value?.ToString() ?? string.Empty;
        }
        catch (Exception)
        {
            return string.Empty;
        }
    }

    private static async Task<ResultDto<List<T>>> ExecuteListAsync<T>(
        OracleConnection cn,
        string procedure,
        int codigoCompromiso,
        Func<IDataReader, T> map)
    {
        using var cmd = new OracleCommand(procedure, cn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.BindByName = true;

        cmd.Parameters.Add("p_CodigoCompromiso", OracleDbType.Int32).Value = codigoCompromiso;
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

            var dbMessage = ReporteControlPerceptivoDb.GetMessage(pMessage);
            var isSuccess = ReporteControlPerceptivoDb.IsSuccessMessage(dbMessage);

            return new ResultDto<List<T>>(list)
            {
                Data = isSuccess ? list : null,
                CantidadRegistros = ReporteControlPerceptivoDb.GetIntOutput(pTotalRecords),
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

    private static ResultDto<ReporteControlPerceptivoResponse> BuildInvalidResult(string message)
    {
        return new ResultDto<ReporteControlPerceptivoResponse>(new ReporteControlPerceptivoResponse(null, new(), 0, 0, 0, string.Empty))
        {
            Data = null,
            IsValid = false,
            Message = message
        };
    }
}

internal static class ReporteControlPerceptivoDb
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

    public static ReporteControlPerceptivoHeaderResponse MapHeader(IDataReader reader)
    {
        return new ReporteControlPerceptivoHeaderResponse(
            reader.SafeGetInt32("CODIGO_COMPROMISO"),
            reader.SafeGetString("NUMERO_COMPROMISO"),
            SafeGetNullableDateTime(reader, "FECHA_COMPROMISO"),
            reader.SafeGetString("PROVEEDOR"),
            reader.SafeGetString("SOLICITANTE"),
            reader.SafeGetString("DIRECCION_EMPRESA"),
            reader.SafeGetString("NOMBRE_EMPRESA"),
            reader.SafeGetString("FECHA_EMISION_TEXTO")
        );
    }

    public static ReporteControlPerceptivoDetalleResponse MapDetalle(IDataReader reader)
    {
        return new ReporteControlPerceptivoDetalleResponse(
            reader.SafeGetDecimal("CANTIDAD"),
            reader.SafeGetString("UDM"),
            reader.SafeGetString("DESCRIPCION_ARTICULO"),
            reader.SafeGetDecimal("PRECIO_UNITARIO"),
            reader.SafeGetDecimal("PRECIO"),
            reader.SafeGetDecimal("POR_IMPUESTO"),
            reader.SafeGetDecimal("MONTO_IMPUESTO")
        );
    }

    private static DateTime? SafeGetNullableDateTime(IDataReader reader, string columnName)
    {
        int ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : Convert.ToDateTime(reader.GetValue(ordinal));
    }
}

[ApiController]
[Route("api/ReporteControlPerceptivo")]
public class ReporteControlPerceptivoGetByCodigoController(ConnectionDB _connectionDB) : ControllerBase
{
    [HttpPost]
    [Route("GetByCodigo")]
    public async Task<IActionResult> GetByCodigo(ReporteControlPerceptivoGetByCodigoQuery value)
    {
        var handler = new ReporteControlPerceptivoGetByCodigoHandler(_connectionDB);
        var result = await handler.HandleAsync(value);
        return Ok(result);
    }
}
