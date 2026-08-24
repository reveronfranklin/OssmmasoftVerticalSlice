using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.CatastroInmuebles;

public record CatastroInmueblesGetAllQuery(int PageSize = 10, int PageNumber = 1, string SearchText = "");

public class CatastroInmueblesGetAllHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<List<CatastroInmuebleResponse>>> HandleAsync(CatastroInmueblesGetAllQuery query)
    {
        if (!CatastroInmueblesDb.TryGetEmpresa(config, out var empresa, out var empresaError))
        {
            return Failure(empresaError);
        }

        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var pageNumber = Math.Max(query.PageNumber, 1);

        using var cn = connectionDB.GetCatConnection();
        try
        {
            await cn.OpenAsync();
        }
        catch (Exception ex)
        {
            return Failure($"Error técnico al abrir conexión CAT: {ex.Message}");
        }

        using var cmd = new OracleCommand("CAT.SP_CAT_INM_GET_ALL", cn)
        {
            CommandType = CommandType.StoredProcedure,
            BindByName = true
        };
        cmd.Parameters.Add("p_PageSize", OracleDbType.Int32).Value = pageSize;
        cmd.Parameters.Add("p_PageNumber", OracleDbType.Int32).Value = pageNumber;
        cmd.Parameters.Add("p_SearchText", OracleDbType.Varchar2).Value = CatastroInmueblesDb.DbValue(query.SearchText);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
        var message = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
        var totalRecords = cmd.Parameters.Add("p_TotalRecords", OracleDbType.Int32, ParameterDirection.Output);
        var totalPages = cmd.Parameters.Add("p_TotalPages", OracleDbType.Int32, ParameterDirection.Output);

        var rows = new List<CatastroInmuebleResponse>();
        try
        {
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                rows.Add(CatastroInmueblesDb.MapInmueble(reader));
            }

            var dbMessage = CatastroInmueblesDb.GetMessage(message);
            var isValid = CatastroInmueblesDb.IsSuccessMessage(dbMessage);
            return new ResultDto<List<CatastroInmuebleResponse>>(isValid ? rows : null!)
            {
                IsValid = isValid,
                Message = dbMessage,
                CantidadRegistros = CatastroInmueblesDb.GetIntOutput(totalRecords),
                Page = pageNumber,
                TotalPage = CatastroInmueblesDb.GetIntOutput(totalPages)
            };
        }
        catch (Exception ex)
        {
            return Failure($"Error técnico: {ex.Message}");
        }
    }

    private static ResultDto<List<CatastroInmuebleResponse>> Failure(string message) =>
        new(null!) { IsValid = false, Message = message };
}

[ApiController]
[Authorize]
[Route("api/CatastroInmuebles")]
public class CatastroInmueblesGetAllController(ConnectionDB connectionDB, IConfiguration config) : ControllerBase
{
    [HttpPost("GetAll")]
    public async Task<IActionResult> GetAll(CatastroInmueblesGetAllQuery query)
    {
        var result = await new CatastroInmueblesGetAllHandler(connectionDB, config).HandleAsync(query);
        return Ok(result);
    }
}
