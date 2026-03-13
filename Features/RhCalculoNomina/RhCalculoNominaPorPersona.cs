using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;
using System.Reflection.Metadata;



namespace OssmmasoftVerticalSlice.Features.RhCalculoNomina;

//request
public record RhCalculoNominaPorPersonaQuery(int CodigoPersona,int CodigoTipoNomina,int CodigoUsuario,int CodigoEmpresa,int PageSize,int PageNumber,string SearchText);


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
    string Extra3,
    bool Automatico,
    string SearchText
   

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
    //cmd.Parameters.Add("p_codigo_relacion_cargo", OracleDbType.Int32).Value = value.CodigoRelacionCargo;
    cmd.Parameters.Add("p_Codigo_Persona", OracleDbType.Int32).Value = value.CodigoPersona;
    //cmd.Parameters.Add("p_sueldo", OracleDbType.Decimal).Value = value.CodigoPersona;
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
                    string searchText =  
                    reader.SafeGetString("CODIGO_FRECUENCIA") + "-" + 
                    reader.SafeGetString("DESCRIPCION_FRECUENCIA")+ "-"  +
                     reader.SafeGetString("CODIGO")+ "-" +  
                     reader.SafeGetString("DENOMINACION") + "-" + 
                     reader.SafeGetString("TIPO_CONCEPTO") + "-" + 
                      reader.SafeGetString("CODIGO_MODULO") + "-" + 
                       reader.SafeGetString("DESCRIPCION_MODULO") + "-" + reader.SafeGetString("TIPO_CONCEPTO"); ;
                    list.Add(new GetRhCalculoNominaPorPersonaResponse(
                        reader.SafeGetInt32("CODIGO_MOV_NOMINA"),
                        reader.SafeGetInt32("CODIGO_TIPO_NOMINA"),
                        reader.SafeGetInt32("CODIGO_PERSONA"),
                        reader.SafeGetInt32("CODIGO_CONCEPTO"),
                        reader.SafeGetString("COMPLEMENTO_CONCEPTO"),
                        reader.SafeGetString("TIPO"),
                        reader.SafeGetInt32("FRECUENCIA_ID"),
                        reader.SafeGetDecimal("MONTO"),
                        reader.SafeGetDecimal("ASIGNACION"),
                        reader.SafeGetDecimal("DEDUCCION"),
                        reader.SafeGetDecimal("ASIGNACION_DEDUCCION"),
                        reader.SafeGetString("STATUS"),
                        reader.SafeGetString("CODIGO_FRECUENCIA"),
                        reader.SafeGetString("DESCRIPCION_FRECUENCIA"),
                        reader.SafeGetString("CODIGO"),
                        reader.SafeGetString("DENOMINACION"),
                        reader.SafeGetString("TIPO_CONCEPTO"),
                        reader.SafeGetInt32("MODULO_ID"),
                        reader.SafeGetString("CODIGO_MODULO"),
                        reader.SafeGetString("DESCRIPCION_MODULO"),
                        reader.SafeGetString("EXTRA1"),
                        reader.SafeGetString("EXTRA2"),
                        reader.SafeGetString("EXTRA3"),
                        reader.SafeGetBoolean("AUTOMATICO"),
                        searchText
                    ));
            }
        }

        // Recuperar valores después del reader
        string dbMessage = pMessage.Value != DBNull.Value ? pMessage.Value.ToString() : "Error desconocido";
        int dbTotalRecords = pTotalRecords.Value != DBNull.Value ? int.Parse(pTotalRecords.Value.ToString()) : 0;
        int dbTotalPages = pTotalPages.Value != DBNull.Value ? int.Parse(pTotalPages.Value.ToString()) : 0;
        // Determinamos si la operación fue exitosa según el mensaje del SP
        bool isSuccess = dbMessage.Equals("Success", StringComparison.OrdinalIgnoreCase);

            var totales = list.Aggregate(new { Monto = 0m, Asig = 0m, Ded = 0m }, 
            (acc, x) => new { 
                Monto = acc.Monto + x.Monto, 
                Asig = acc.Asig + x.Asignacion, 
                Ded = acc.Ded + x.Deduccion 
            });



        return new ResultDto<List<GetRhCalculoNominaPorPersonaResponse>>(list)
        {
            Data = isSuccess ? list : null, // Si falló, limpiamos la data
            CantidadRegistros = dbTotalRecords,
            Page = value.PageNumber,
            TotalPage = dbTotalPages,
            IsValid = isSuccess,
            Message = dbMessage,
            Total1 = totales.Monto,
            Total2 = totales.Asig,
            Total3 = totales.Ded,
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