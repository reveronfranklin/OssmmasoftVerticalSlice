using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;
using System.Diagnostics;

namespace OssmmasoftVerticalSlice.Features.RhTmpMovNomina;

// Request
public record RhTmpMovNominaGetAllQuery(string p_where);

// Response (Ajustado para ser consistente con los tipos de Oracle)
public record GetRhTmpMovNominaGetAllResponse(
   int CodigoPeriodo, 
   int CodigoMovNomina,
   int CodigoTipoNomina,
   string TipoNomina,
   int CodigoPersona,
   int Cedula,string Persona,
   int CodigoConcepto,
   string Concepto,
   string Denominacion,
   string TipoConcepto,
   string ComplementoConcepto,
   string Tipo,
   int FrecuenciaId,
   string Frecuencia,
   decimal Monto,
   int CodigoIcp,
   string Icp

);

public class GetRhTmpMovNominaGetAllHandler(ConnectionDB _connectionDB)
{
    public async Task<ResultDto<List<GetRhTmpMovNominaGetAllResponse>>> HandleAsync(RhTmpMovNominaGetAllQuery value)
    {
        if (!WhereClauseHelper.TryBuildCleanWhere(value.p_where, out var cleanWhere, out var errorMessage))
        {
            return new ResultDto<List<GetRhTmpMovNominaGetAllResponse>>(null)
            {
                IsValid = false,
                Message = errorMessage ?? "Error de sintaxis en el filtro."
            };
        }

        using var cn = _connectionDB.GetRhConnection();
        await cn.OpenAsync();

        using var cmd = new OracleCommand("RH.SP_RH_TMP_MOV_NOMINA_GET_ALL", cn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.BindByName = true;

        // Parámetro de entrada
        cmd.Parameters.Add("p_where", OracleDbType.Varchar2).Value = 
            string.IsNullOrWhiteSpace(cleanWhere) ? (object)DBNull.Value : cleanWhere;

        // Parámetros de salida
        var pResultSet = cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
        var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
        var pTotalRecords = cmd.Parameters.Add("p_TotalRecords", OracleDbType.Int32, ParameterDirection.Output);

        var list = new List<GetRhTmpMovNominaGetAllResponse>();

        try 
        {
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var id = reader.SafeGetInt32("CODIGO_MOV_NOMINA");
                    try
                    {
                        var dbCodigoPeriodo = reader.SafeGetInt32("CODIGO_PERIODO");
                        var dbCodigoMovNomina = reader.SafeGetInt32("CODIGO_MOV_NOMINA");
                        var dbCodigoTipoNomina = reader.SafeGetInt32("CODIGO_TIPO_NOMINA");
                        var dbTipoNomina = reader.SafeGetString("TIPO_NOMINA");
                        var dbCodigoPersona = reader.SafeGetInt32("CODIGO_PERSONA");
                        var dbCedula = reader.SafeGetInt32("CEDULA");
                        var dbPersona = reader.SafeGetString("PERSONA");
                        var dbCodigoConcepto = reader.SafeGetInt32("CODIGO_CONCEPTO");
                        var dbConcepto = reader.SafeGetString("CONCEPTO");
                        var dbDenominacion = reader.SafeGetString("DENOMINACION");
                        var dbTipoConcepto = reader.SafeGetString("TIPO_CONCEPTO");
                        var dbComplementoConcepto = reader.SafeGetString("COMPLEMENTO_CONCEPTO");
                        var dbTipo = reader.SafeGetString("TIPO");
                        var dbFrecuenciaId = reader.SafeGetInt32("FRECUENCIA_ID");
                        var dbFrecuencia = reader.SafeGetString("DESCRIPCION");
                        var dbMonto = reader.SafeGetDecimal("MONTO");
                        var dbCodigoIcp = reader.SafeGetInt32("CODIGO_ICP");
                        var dbIcp = reader.SafeGetString("DENOMINACION_ICP");

                        list.Add(new GetRhTmpMovNominaGetAllResponse(
                            dbCodigoPeriodo,
                            dbCodigoMovNomina,
                            dbCodigoTipoNomina,
                            dbTipoNomina,
                            dbCodigoPersona,
                            dbCedula,
                            dbPersona,
                            dbCodigoConcepto,
                            dbConcepto,
                            dbDenominacion,
                            dbTipoConcepto,
                            dbComplementoConcepto,
                            dbTipo,
                            dbFrecuenciaId,
                            dbFrecuencia,
                            dbMonto,
                            dbCodigoIcp,
                            dbIcp
                        ));
                    }catch (Exception ex)
                    {
                        string errmsg = ex.Message + " " + id ;
                    }
                      
                }
            }

            string dbMessage = pMessage.Value != DBNull.Value ? pMessage.Value.ToString() : "Success";
            int dbTotalRecords = pTotalRecords.Value != DBNull.Value ? int.Parse(pTotalRecords.Value.ToString()) : 0;
            bool isSuccess = dbMessage.Equals("Success", StringComparison.OrdinalIgnoreCase);

            return new ResultDto<List<GetRhTmpMovNominaGetAllResponse>>(list)
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

            return new ResultDto<List<GetRhTmpMovNominaGetAllResponse>>(null)
            {
                IsValid = false,
                Message = $"Error Base de Datos ({ex.Number}): {ex.Message}. Filtro: {cleanWhere}"
            };
        }
    }
}
//Endpoint
[ApiController]
[Route("api/RhTmpMovNominaGetAll")]
public class RhTipoNominaGetAllController(ConnectionDB _connectionDB) : ControllerBase
{
    [HttpPost]
    [Route("GetAll")]
    public async Task<IActionResult> GetAll(RhTmpMovNominaGetAllQuery value)
    {
        try
        {
            var handler = new GetRhTmpMovNominaGetAllHandler(_connectionDB);
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
