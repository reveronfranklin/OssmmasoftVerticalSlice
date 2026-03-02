using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;



namespace OssmmasoftVerticalSlice.Features.RhCalculoNomina;

//request
public record RhCalculoNominaPorPersonaQuery(int CodigoPersona,int CodigoTipoNomina,int CodigoRelacionCargo,decimal Sueldo,int CodigoUsuario,int CodigoEmpresa,int PageSize,int PageNumber,string SearchText);

/*



RD1.CODIGO AS CODIGO_FRECUENCIA,
RD1.DESCRIPCION AS DESCRIPCION_FRECUENCIA,
RC.CODIGO,
RC.DENOMINACION,
RC.TIPO_CONCEPTO,
RC.MODULO_ID,
RD2.CODIGO AS CODIGO_MODULO,
RD2.DESCRIPCION AS DESCRIPCION_MODULO
RMN.USUARIO_INS, RMN.FECHA_INS, RMN.USUARIO_UPD, RMN.FECHA_UPD, RMN.CODIGO_EMPRESA,
*/

//response
public record GetRhCalculoNominaPorPersonaResponse(
    int CodigoMovNomina,
    int CodigoTipoNomina,
    int CodigoPersona,
    int CodigoConcepto,
    string ComplementoConcepto,
    string Tipo,
    int FrecuenciaId,
    decimal Monto,
    decimal Asignacion,
    decimal Deduccion,
    decimal asignacionDeduccion,
    string Status,
    string CodigoFrecuencia,
    string DescripcionFrecuencia,
    string Codigo,
    string Denominacion,
    string TipoConcepto,
    int ModuloId,
    string CodigoModulo,
    string DescripcionModulo,

    string Extra1,
    string Extra2,
    string Extra3
   

   );




//handler
//handler asíncrono
public class RhCalculoNominaPorPersonaHandler(ConnectionDB _connectionDB)
{


public async Task<ResultDto<List<GetRhCalculoNominaPorPersonaResponse>>> HandleAsync(RhCalculoNominaPorPersonaQuery value)
{
    using var cn = _connectionDB.GetRhConnection();
    await cn.OpenAsync();

    using var cmd = new OracleCommand("RH.SP_CALCULO_NOMINA", cn); // Nombre corto < 30 char
    cmd.CommandType = CommandType.StoredProcedure;
    cmd.BindByName = true;

    // Parámetros de entrada
    cmd.Parameters.Add("p_codigo_tipo_nomina", OracleDbType.Int32).Value = value.CodigoTipoNomina;
    cmd.Parameters.Add("p_codigo_relacion_cargo", OracleDbType.Int32).Value = value.CodigoRelacionCargo;
    cmd.Parameters.Add("p_Codigo_Persona", OracleDbType.Int32).Value = value.CodigoPersona;
    cmd.Parameters.Add("p_sueldo", OracleDbType.Decimal).Value = value.CodigoPersona;
    cmd.Parameters.Add("p_codigo_usuario", OracleDbType.Int32).Value = value.CodigoUsuario;
    cmd.Parameters.Add("p_codigo_empresa", OracleDbType.Int32).Value = value.CodigoEmpresa;
    

    // Parámetros de salida
    cmd.Parameters.Add("p_statement", OracleDbType.Varchar2).Value = "INSERT";
    cmd.Parameters.Add("p_PageSize", OracleDbType.Int32).Value = value.PageSize ;
    cmd.Parameters.Add("p_PageNumber", OracleDbType.Int32).Value = value.PageNumber ;
    cmd.Parameters.Add("p_SearchText", OracleDbType.Varchar2).Value = value.PageNumber ;

    
    // Parámetros de salida con referencias

    var pResultSet = cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
    var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
    var pTotalRecords = cmd.Parameters.Add("p_TotalRecords", OracleDbType.Int32, ParameterDirection.Output);
    var pTotalPages = cmd.Parameters.Add("p_TotalPages", OracleDbType.Int32, ParameterDirection.Output);

    var list = new List<GetRhCalculoNominaPorPersonaResponse>();

    try 
    {
        using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                list.Add(new GetRhCalculoNominaPorPersonaResponse(  
                    reader.GetInt32(reader.GetOrdinal("CODIGO_MOV_NOMINA")),
                    reader.GetInt32(reader.GetOrdinal("CODIGO_TIPO_NOMINA")),
                    reader.GetInt32(reader.GetOrdinal("CODIGO_PERSONA")),
                    reader.IsDBNull(reader.GetOrdinal("CODIGO_CONCEPTO")) ? 0 : reader.GetInt32(reader.GetOrdinal("CODIGO_CONCEPTO")),
                    reader.IsDBNull(reader.GetOrdinal("COMPLEMENTO_CONCEPTO")) ? "" : reader.GetString(reader.GetOrdinal("COMPLEMENTO_CONCEPTO")),
                    reader.IsDBNull(reader.GetOrdinal("TIPO")) ? "" : reader.GetString(reader.GetOrdinal("TIPO")),
                    reader.GetInt32(reader.GetOrdinal("FRECUENCIA_ID")),
                    reader.GetDecimal(reader.GetOrdinal("MONTO")),
                    reader.GetDecimal(reader.GetOrdinal("ASIGNACION")),
                    reader.GetDecimal(reader.GetOrdinal("DEDUCCION")),
                    reader.GetDecimal(reader.GetOrdinal("ASIGNACION_DEDUCCION")),
                    reader.IsDBNull(reader.GetOrdinal("STATUS")) ? "" : reader.GetString(reader.GetOrdinal("STATUS")),
                    reader.GetString(reader.GetOrdinal("CODIGO_FRECUENCIA")),
                    reader.IsDBNull(reader.GetOrdinal("DESCRIPCION_FRECUENCIA")) ? "" : reader.GetString(reader.GetOrdinal("DESCRIPCION_FRECUENCIA")),
                    reader.IsDBNull(reader.GetOrdinal("CODIGO")) ? "" : reader.GetString(reader.GetOrdinal("CODIGO")),
                    reader.IsDBNull(reader.GetOrdinal("DENOMINACION")) ? "" : reader.GetString(reader.GetOrdinal("DENOMINACION")),
                    reader.IsDBNull(reader.GetOrdinal("TIPO_CONCEPTO")) ? "" : reader.GetString(reader.GetOrdinal("TIPO_CONCEPTO")),
                    reader.IsDBNull(reader.GetOrdinal("MODULO_ID")) ? 0 : reader.GetInt32(reader.GetOrdinal("MODULO_ID")),
                    reader.IsDBNull(reader.GetOrdinal("CODIGO_MODULO")) ? "" : reader.GetString(reader.GetOrdinal("CODIGO_MODULO")),
                    reader.IsDBNull(reader.GetOrdinal("DESCRIPCION_MODULO")) ? "" : reader.GetString(reader.GetOrdinal("DESCRIPCION_MODULO")),
                    reader.IsDBNull(reader.GetOrdinal("EXTRA1")) ? "" : reader.GetString(reader.GetOrdinal("EXTRA1")),
                    reader.IsDBNull(reader.GetOrdinal("EXTRA2")) ? "" : reader.GetString(reader.GetOrdinal("EXTRA2")),
                    reader.IsDBNull(reader.GetOrdinal("EXTRA3")) ? "" : reader.GetString(reader.GetOrdinal("EXTRA3"))
                
                   

                ));
            }
        }

        // Recuperar valores después del reader
        string dbMessage = pMessage.Value != DBNull.Value ? pMessage.Value.ToString() : "Error desconocido";
        int dbTotalRecords = pTotalRecords.Value != DBNull.Value ? int.Parse(pTotalRecords.Value.ToString()) : 0;
        int dbTotalPages = pTotalPages.Value != DBNull.Value ? int.Parse(pTotalPages.Value.ToString()) : 0;
        // Determinamos si la operación fue exitosa según el mensaje del SP
        bool isSuccess = dbMessage.Equals("Success", StringComparison.OrdinalIgnoreCase);

        return new ResultDto<List<GetRhCalculoNominaPorPersonaResponse>>(list)
        {
            Data = isSuccess ? list : null, // Si falló, limpiamos la data
            CantidadRegistros = dbTotalRecords,
            Page = value.PageNumber,
            TotalPage = dbTotalPages,
            IsValid = isSuccess,
            Message = dbMessage
        };
    }
    catch (Exception ex)
    {
        // Error de conexión o de red
        return new ResultDto<List<GetRhCalculoNominaPorPersonaResponse>>(null)
        {
            IsValid = false,
            Message = $"Error técnico: {ex.Message}"
        };
    }
}



}

//Endpoint
[ApiController]
[Route("api/RhCalculoNomina")]
public class GetAllCategoryController(ConnectionDB _connectionDB) : ControllerBase
{
    [HttpPost]
    [Route("CalculoPorPersona")]
    public async Task<IActionResult> GetAll(RhCalculoNominaPorPersonaQuery value)
    {
        try
        {
            var handler = new RhCalculoNominaPorPersonaHandler(_connectionDB);
            // Llamada asíncrona al handler
            var categories = await handler.HandleAsync(value);

            return Ok(categories);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new 
            { 
                IsValid = false,
                Message = $"Error técnico: {ex.Message}"
            });
        }
    }
}