using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;
using System.Diagnostics;

namespace OssmmasoftVerticalSlice.Features.RhTipoNomina;

// Request
public record RhTipoNominaGetAllQuery(string p_where);

// Response (Ajustado para ser consistente con los tipos de Oracle)
public record GetRhTipoNominaGetAllResponse(
    int CodigoTipoNomina, string Descripcion
);

public class GetRhTipoNominaGetAllHandler(ConnectionDB _connectionDB)
{
    public async Task<ResultDto<List<GetRhTipoNominaGetAllResponse>>> HandleAsync(RhTipoNominaGetAllQuery value)
    {
        // 1. Limpieza inicial
        string cleanWhere = value.p_where?.Trim() ?? "";
        
        // Quitar comillas externas accidentales si existen (ej: "'STATUS = 'A''")
        if (cleanWhere.StartsWith("'") && cleanWhere.EndsWith("'") && cleanWhere.Length > 2)
        {
            cleanWhere = cleanWhere.Substring(1, cleanWhere.Length - 2);
        }

        // 2. Validación de Balanceo de Comillas (Prevenir ORA-01756)
        int quoteCount = cleanWhere.Count(f => f == '\'');
        if (quoteCount % 2 != 0)
        {
            return new ResultDto<List<GetRhTipoNominaGetAllResponse>>(null)
            {
                IsValid = false,
                Message = $"Error de sintaxis: Hay una comilla simple sin cerrar en el filtro: [{cleanWhere}]"
            };
        }

        using var cn = _connectionDB.GetRhConnection();
        await cn.OpenAsync();

        using var cmd = new OracleCommand("RH.SP_RH_TIPOS_NOMINA_GET_ALL", cn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.BindByName = true;

        // Parámetro de entrada
        cmd.Parameters.Add("p_where", OracleDbType.Varchar2).Value = 
            string.IsNullOrWhiteSpace(cleanWhere) ? (object)DBNull.Value : cleanWhere;

        // Parámetros de salida
        var pResultSet = cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
        var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
        var pTotalRecords = cmd.Parameters.Add("p_TotalRecords", OracleDbType.Int32, ParameterDirection.Output);

        var list = new List<GetRhTipoNominaGetAllResponse>();

        try 
        {
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                        var codigoTipoNomina=reader.SafeGetInt32("CODIGO_TIPO_NOMINA");
                        var descripcion= reader.SafeGetString("DESCRIPCION");
                        list.Add(new GetRhTipoNominaGetAllResponse(
                            reader.SafeGetInt32("CODIGO_TIPO_NOMINA"),
                            reader.SafeGetString("DESCRIPCION")
                        ));
                }
            }

            string dbMessage = pMessage.Value != DBNull.Value ? pMessage.Value.ToString() : "Success";
            int dbTotalRecords = pTotalRecords.Value != DBNull.Value ? int.Parse(pTotalRecords.Value.ToString()) : 0;
            bool isSuccess = dbMessage.Equals("Success", StringComparison.OrdinalIgnoreCase);

            return new ResultDto<List<GetRhTipoNominaGetAllResponse>>(list)
            {
                Data = isSuccess ? list : null,
                CantidadRegistros = dbTotalRecords,
                IsValid = isSuccess,
                Message = dbMessage
            };
        }
        catch (OracleException ex)
        {
            // LOG EN CONSOLA DE DEBUG
            Debug.WriteLine(">>>> ORACLE ERROR: " + ex.Message);
            Debug.WriteLine(">>>> FILTRO ENVIADO: " + cleanWhere);

            return new ResultDto<List<GetRhTipoNominaGetAllResponse>>(null)
            {
                IsValid = false,
                Message = $"Error Base de Datos ({ex.Number}): {ex.Message}. Filtro: {cleanWhere}"
            };
        }
    }
}
//Endpoint
[ApiController]
[Route("api/RhTipoNominaGetAll")]
public class RhTipoNominaGetAllController(ConnectionDB _connectionDB) : ControllerBase
{
    [HttpPost]
    [Route("GetAll")]
    public async Task<IActionResult> GetAll(RhTipoNominaGetAllQuery value)
    {
        try
        {
            var handler = new GetRhTipoNominaGetAllHandler(_connectionDB);
            // Llamada asíncrona al handler
            var categories = await handler.HandleAsync(value);

            return Ok(categories);
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