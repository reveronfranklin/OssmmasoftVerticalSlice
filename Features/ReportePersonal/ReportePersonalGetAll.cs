using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.ReportePersonal;

public record ReportePersonalGetAllQuery(
    int? CodigoTipoNomina = null,
    string? Status = null
);

public record ReportePersonalResponse(
    string Cedula,
    string Nombre,
    DateTime? FechaIngreso,
    string Departamento,
    string Codigo,
    string Cargo,
    decimal Sueldo,
    string DescripcionStatus,
    string TipoNomina
);

public class ReportePersonalGetAllHandler(ConnectionDB _connectionDB, IConfiguration _config)
{
    public async Task<ResultDto<List<ReportePersonalResponse>>> HandleAsync(ReportePersonalGetAllQuery query)
    {
        if (!ReportePersonalDb.TryGetEmpresa(_config, out int empresa, out string errorMessage))
        {
            return BuildInvalidResult(errorMessage);
        }

        using var cn = _connectionDB.GetRhConnection();
        try
        {
            await cn.OpenAsync();
        }
        catch (Exception ex)
        {
            return new ResultDto<List<ReportePersonalResponse>>(null!)
            {
                IsValid = false,
                Message = $"Error tecnico al abrir conexion RH: {ex.Message}"
            };
        }

        using var cmd = new OracleCommand("RH.SP_REPORTE_PERSONAL_GET_ALL", cn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.BindByName = true;

        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_CodigoTipoNomina", OracleDbType.Int32).Value = ReportePersonalDb.PositiveDbValue(query.CodigoTipoNomina);
        cmd.Parameters.Add("p_Status", OracleDbType.Varchar2).Value = ReportePersonalDb.DbValue(query.Status);
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
        var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
        var pTotalRecords = cmd.Parameters.Add("p_TotalRecords", OracleDbType.Int32, ParameterDirection.Output);

        var list = new List<ReportePersonalResponse>();

        try
        {
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    list.Add(ReportePersonalDb.MapReportePersonal(reader));
                }
            }

            string dbMessage = ReportePersonalDb.GetMessage(pMessage);
            bool isSuccess = ReportePersonalDb.IsSuccessMessage(dbMessage);

            return new ResultDto<List<ReportePersonalResponse>>(list)
            {
                Data = isSuccess ? list : null,
                CantidadRegistros = ReportePersonalDb.GetIntOutput(pTotalRecords),
                IsValid = isSuccess,
                Message = dbMessage
            };
        }
        catch (Exception ex)
        {
            return new ResultDto<List<ReportePersonalResponse>>(null!)
            {
                IsValid = false,
                Message = $"Error tecnico: {ex.Message}"
            };
        }
    }

    private static ResultDto<List<ReportePersonalResponse>> BuildInvalidResult(string message)
    {
        return new ResultDto<List<ReportePersonalResponse>>(new List<ReportePersonalResponse>())
        {
            Data = null,
            IsValid = false,
            Message = message
        };
    }
}

internal static class ReportePersonalDb
{
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

        if (!int.TryParse(empresaString, out empresa))
        {
            errorMessage = "EmpresaConfig debe ser un numero valido.";
            return false;
        }

        return true;
    }

    public static bool IsSuccessMessage(string? message)
    {
        return string.Equals(message, "success", StringComparison.OrdinalIgnoreCase)
            || string.Equals(message, "suscces", StringComparison.OrdinalIgnoreCase);
    }

    public static object DbValue(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();
    }

    public static object PositiveDbValue(int? value)
    {
        return value.HasValue && value.Value > 0 ? value.Value : DBNull.Value;
    }

    public static string GetMessage(OracleParameter parameter, string defaultMessage = "Sin respuesta de BD")
    {
        return parameter.Value == DBNull.Value ? defaultMessage : parameter.Value?.ToString() ?? defaultMessage;
    }

    public static int GetIntOutput(OracleParameter parameter)
    {
        return parameter.Value == DBNull.Value ? 0 : Convert.ToInt32(parameter.Value.ToString());
    }

    public static ReportePersonalResponse MapReportePersonal(IDataReader reader)
    {
        return new ReportePersonalResponse(
            reader.SafeGetString("CEDULA"),
            reader.SafeGetString("NOMBRE"),
            SafeGetNullableDateTime(reader, "FECHA_INGRESO"),
            reader.SafeGetString("DEPARTAMENTO"),
            GetValueAsString(reader, "CODIGO"),
            reader.SafeGetString("CARGO"),
            reader.SafeGetDecimal("SUELDO"),
            reader.SafeGetString("DESCRIPCION_STATUS"),
            reader.SafeGetString("TIPO_NOMINA")
        );
    }

    private static DateTime? SafeGetNullableDateTime(IDataReader reader, string columnName)
    {
        int ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : Convert.ToDateTime(reader.GetValue(ordinal));
    }

    private static string GetValueAsString(IDataReader reader, string columnName)
    {
        int ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? string.Empty : reader.GetValue(ordinal).ToString() ?? string.Empty;
    }
}

[ApiController]
[Route("api/ReportePersonal")]
public class ReportePersonalGetAllController(ConnectionDB _connectionDB, IConfiguration _config) : ControllerBase
{
    [HttpPost]
    [Route("GetAll")]
    public async Task<IActionResult> GetAll(ReportePersonalGetAllQuery value)
    {
        var handler = new ReportePersonalGetAllHandler(_connectionDB, _config);
        var result = await handler.HandleAsync(value);
        return Ok(result);
    }
}
