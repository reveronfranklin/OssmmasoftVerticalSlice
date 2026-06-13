using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;
using System.Diagnostics;

namespace OssmmasoftVerticalSlice.Features.ReporteGeneralNomina;

// Request
public record ReporteGeneralNominaPeriodoGetByCodigoQuery(
    int p_codigo_periodo
);

// Response
public record GetReporteGeneralNominaPeriodoGetByCodigoResponse(
    decimal CodigoPeriodo,
    string Descripcion,
    decimal CodigoTipoNomina,
    string DescripcionTipoNomina,
    DateTime? FechaNomina,
    decimal Periodo,
    string DescripcionPeriodo,
    string TipoNomina,
    string TipoNominaDescripcion
);

public class GetReporteGeneralNominaPeriodoGetByCodigoHandler(ConnectionDB _connectionDB)
{
    public async Task<ResultDto<GetReporteGeneralNominaPeriodoGetByCodigoResponse?>> HandleAsync(ReporteGeneralNominaPeriodoGetByCodigoQuery value)
    {
        if (value.p_codigo_periodo <= 0)
        {
            return BuildInvalidResult("El parametro p_codigo_periodo debe ser mayor que cero.");
        }

        using var cn = _connectionDB.GetRhConnection();
        await cn.OpenAsync();

        using var cmd = new OracleCommand("RH.SP_REP_GRAL_NOM_PER_GET_ID", cn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.BindByName = true;

        cmd.Parameters.Add("p_codigo_periodo", OracleDbType.Int32).Value = value.p_codigo_periodo;

        var pResultSet = cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
        var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
        var pTotalRecords = cmd.Parameters.Add("p_TotalRecords", OracleDbType.Int32, ParameterDirection.Output);

        GetReporteGeneralNominaPeriodoGetByCodigoResponse? periodo = null;

        try
        {
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    periodo = new GetReporteGeneralNominaPeriodoGetByCodigoResponse(
                        reader.SafeGetDecimal("CODIGO_PERIODO"),
                        GetValueAsString(reader, "DESCRIPCION"),
                        reader.SafeGetDecimal("CODIGO_TIPO_NOMINA"),
                        GetValueAsString(reader, "DESCRIPCION_TIPO_NOMINA"),
                        SafeGetNullableDateTime(reader, "FECHA_NOMINA"),
                        reader.SafeGetDecimal("PERIODO"),
                        GetValueAsString(reader, "DESCRIPCION_PERIODO"),
                        GetValueAsString(reader, "TIPO_NOMINA"),
                        GetValueAsString(reader, "TIPO_NOMINA_DESCRIPCION")
                    );
                }
            }

            string dbMessage = pMessage.Value != DBNull.Value ? pMessage.Value.ToString() ?? "Success" : "Success";
            int dbTotalRecords = pTotalRecords.Value != DBNull.Value ? int.Parse(pTotalRecords.Value.ToString() ?? "0") : 0;
            bool isSuccess = dbMessage.Equals("Success", StringComparison.OrdinalIgnoreCase);

            if (isSuccess && periodo is null)
            {
                return BuildInvalidResult("No se encontro informacion para el periodo consultado.");
            }

            return new ResultDto<GetReporteGeneralNominaPeriodoGetByCodigoResponse?>(periodo)
            {
                Data = isSuccess ? periodo : null,
                CantidadRegistros = dbTotalRecords,
                IsValid = isSuccess,
                Message = dbMessage
            };
        }
        catch (OracleException ex)
        {
            Debug.WriteLine(">>>> ORACLE ERROR: " + ex.Message);
            Debug.WriteLine(">>>> CODIGO PERIODO: " + value.p_codigo_periodo);

            return new ResultDto<GetReporteGeneralNominaPeriodoGetByCodigoResponse?>(null)
            {
                Data = null,
                IsValid = false,
                Message = $"Error Base de Datos ({ex.Number}): {ex.Message}. Periodo: {value.p_codigo_periodo}"
            };
        }
    }

    private static ResultDto<GetReporteGeneralNominaPeriodoGetByCodigoResponse?> BuildInvalidResult(string message)
    {
        return new ResultDto<GetReporteGeneralNominaPeriodoGetByCodigoResponse?>(null)
        {
            Data = null,
            IsValid = false,
            Message = message
        };
    }

    private static string GetValueAsString(IDataReader reader, string columnName)
    {
        int ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? string.Empty : reader.GetValue(ordinal).ToString() ?? string.Empty;
    }

    private static DateTime? SafeGetNullableDateTime(IDataReader reader, string columnName)
    {
        int ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : Convert.ToDateTime(reader.GetValue(ordinal));
    }
}

// Endpoint
[ApiController]
[Route("api/ReporteGeneralNominaPeriodoGetByCodigo")]
public class ReporteGeneralNominaPeriodoGetByCodigoController(ConnectionDB _connectionDB) : ControllerBase
{
    [HttpPost]
    [Route("getById")]
    public async Task<IActionResult> GetById(ReporteGeneralNominaPeriodoGetByCodigoQuery value)
    {
        try
        {
            var handler = new GetReporteGeneralNominaPeriodoGetByCodigoHandler(_connectionDB);
            var result = await handler.HandleAsync(value);

            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                message = "Error interno en el servidor",
                detail = ex.Message
            });
        }
    }
}
