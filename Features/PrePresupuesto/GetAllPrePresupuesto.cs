using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using System.Data;



namespace OssmmasoftVerticalSlice.Features.AppCategories;

//request
public record GetAllPrePresupuestoQuery(int PageSize = 10, int PageNumber = 1, string SearchText = "");



//response
public record GetAllPrePresupuestoResponse(int CodigoPresupuesto,string Descripcion,bool PresupuestoEnEjecucion,int Ano,List<PreFinanciadoDto> 
PreFinanciadoDto);
public record PreFinanciadoDto(int FinanciadoId,string DescriptivaFinanciado);

//handler
//handler asíncrono
public class GetAllPrePresupuestoHandler(ConnectionDB _connectionDB)
{

    public async Task<List<PreFinanciadoDto>> GetFinanciadosAsync(int codigoPresupuesto)
    {
        
        var list = new List<PreFinanciadoDto>();
    using (var cn = _connectionDB.GetPresupuestoConnection())
    {
        await cn.OpenAsync();
        using (var cmd = new OracleCommand("SP_PRE_SALDOS_GET_FINANCIADO", cn))
        {
            cmd.CommandType = CommandType.StoredProcedure;
            cmd.BindByName = true;

         
            // Parámetros de entrada
            cmd.Parameters.Add("p_CodigoPresupuesto", OracleDbType.Int32).Value = codigoPresupuesto;
            cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor).Direction = ParameterDirection.Output;
            cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000).Direction = ParameterDirection.Output;

            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {

                    list.Add(new PreFinanciadoDto (
                        reader.GetInt32(reader.GetOrdinal("FINANCIADO_ID")),
                        reader.GetString(reader.GetOrdinal("DESCRIPTIVA_FINANCIADO"))
                    ));
                }
            }
        }
    }
    return list;
    }

    
   public async Task<List<GetAllPrePresupuestoResponse>> HandleAsync(GetAllPrePresupuestoQuery value)
    {
        using var cn = _connectionDB.GetPresupuestoConnection();
        await cn.OpenAsync();

        using var cmd = new OracleCommand("PRE.SP_PRE_PRESUPUESTO_GETALL", cn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.BindByName = true; // CRÍTICO para Oracle

        // Parámetros de entrada
        cmd.Parameters.Add("p_PageSize", OracleDbType.Int32).Value = value.PageSize;
        cmd.Parameters.Add("p_PageNumber", OracleDbType.Int32).Value = value.PageNumber; 
        cmd.Parameters.Add("p_SearchText", OracleDbType.Varchar2).Value = value.SearchText;

        // Parámetros de salida
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor).Direction = ParameterDirection.Output;
        cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000).Direction = ParameterDirection.Output;
        cmd.Parameters.Add("p_TotalPages", OracleDbType.Int32).Direction = ParameterDirection.Output;
        cmd.Parameters.Add("p_TotalRecords", OracleDbType.Int32).Direction = ParameterDirection.Output;

        using var reader = await cmd.ExecuteReaderAsync();
        var list = new List<GetAllPrePresupuestoResponse>();
        while (await reader.ReadAsync())
        {
           var presupuestoEnEjecucion = Convert.ToBoolean(reader.GetInt32(reader.GetOrdinal("PRESUPUESTO_EN_EJECUCION")));
           var financiados=await GetFinanciadosAsync(reader.GetInt32(reader.GetOrdinal("CODIGO_PRESUPUESTO")));
             list.Add(new GetAllPrePresupuestoResponse(
                                reader.GetInt32(reader.GetOrdinal("CODIGO_PRESUPUESTO")),
                                reader.GetString(reader.GetOrdinal("DENOMINACION")),
                                presupuestoEnEjecucion,
                                reader.GetInt32(reader.GetOrdinal("ANO")),
                                financiados
                            ));
            
        }
        return list;
    }

}

//Endpoint
[ApiController]
[Route("api/PrePresupuesto")]
public class GetAllCategoryController(ConnectionDB _connectionDB) : ControllerBase
{
    [HttpPost]
    [Route("GetListPresupuesto")]
    public async Task<IActionResult> GetAll(GetAllPrePresupuestoQuery value)
    {
        try
        {
            var handler = new GetAllPrePresupuestoHandler(_connectionDB);
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