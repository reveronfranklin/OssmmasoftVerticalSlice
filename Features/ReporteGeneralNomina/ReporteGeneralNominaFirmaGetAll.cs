using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;
using System.Diagnostics;

namespace OssmmasoftVerticalSlice.Features.ReporteGeneralNomina;

// Request
public record ReporteGeneralNominaFirmaGetAllQuery();

// Response
public record GetReporteGeneralNominaFirmaGetAllResponse(
    string Oficina,
    string Orden,
    decimal CodigoPersona,
    string Nombre,
    string Apellido,
    string Cedula,
    string DescripcionCargo
);

public class GetReporteGeneralNominaFirmaGetAllHandler(ConnectionDB _connectionDB)
{
    public async Task<ResultDto<List<GetReporteGeneralNominaFirmaGetAllResponse>>> HandleAsync()
    {
        using var cn = _connectionDB.GetRhConnection();
        await cn.OpenAsync();

        using var cmd = new OracleCommand("RH.SP_REP_GRAL_NOM_FIR_GET_ALL", cn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.BindByName = true;

        var pResultSet = cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
        var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
        var pTotalRecords = cmd.Parameters.Add("p_TotalRecords", OracleDbType.Int32, ParameterDirection.Output);

        var list = new List<GetReporteGeneralNominaFirmaGetAllResponse>();

        try
        {
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    list.Add(new GetReporteGeneralNominaFirmaGetAllResponse(
                        GetValueAsString(reader, "OFICINA"),
                        GetValueAsString(reader, "ORDEN"),
                        reader.SafeGetDecimal("CODIGO_PERSONA"),
                        GetValueAsString(reader, "NOMBRE"),
                        GetValueAsString(reader, "APELLIDO"),
                        GetValueAsString(reader, "CEDULA"),
                        GetValueAsString(reader, "DESCRIPCION_CARGO")
                    ));
                }
            }

            string dbMessage = pMessage.Value != DBNull.Value ? pMessage.Value.ToString() ?? "Success" : "Success";
            int dbTotalRecords = pTotalRecords.Value != DBNull.Value ? int.Parse(pTotalRecords.Value.ToString() ?? "0") : 0;
            bool isSuccess = dbMessage.Equals("Success", StringComparison.OrdinalIgnoreCase);

            return new ResultDto<List<GetReporteGeneralNominaFirmaGetAllResponse>>(list)
            {
                Data = isSuccess ? list : null,
                CantidadRegistros = dbTotalRecords,
                IsValid = isSuccess,
                Message = dbMessage
            };
        }
        catch (OracleException ex)
        {
            Debug.WriteLine(">>>> ORACLE ERROR: " + ex.Message);

            return new ResultDto<List<GetReporteGeneralNominaFirmaGetAllResponse>>(new List<GetReporteGeneralNominaFirmaGetAllResponse>())
            {
                Data = null,
                IsValid = false,
                Message = $"Error Base de Datos ({ex.Number}): {ex.Message}"
            };
        }
    }

    private static ResultDto<List<GetReporteGeneralNominaFirmaGetAllResponse>> BuildInvalidResult(string message)
    {
        return new ResultDto<List<GetReporteGeneralNominaFirmaGetAllResponse>>(new List<GetReporteGeneralNominaFirmaGetAllResponse>())
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

}

// Endpoint
[ApiController]
[Route("api/ReporteGeneralNominaFirmaGetAll")]
public class ReporteGeneralNominaFirmaGetAllController(ConnectionDB _connectionDB) : ControllerBase
{
    [HttpPost]
    [Route("GetAll")]
    public async Task<IActionResult> GetAll(ReporteGeneralNominaFirmaGetAllQuery value)
    {
        try
        {
            var handler = new GetReporteGeneralNominaFirmaGetAllHandler(_connectionDB);
            var result = await handler.HandleAsync();

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
