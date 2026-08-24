using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.CatastroContribuyentes;

public record CatastroContribuyentesGetAllQuery(int PageSize = 10, int PageNumber = 1, string SearchText = "");

public class CatastroContribuyentesGetAllHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<List<CatastroContribuyenteResponse>>> HandleAsync(CatastroContribuyentesGetAllQuery query)
    {
        if (!CatastroContribuyentesDb.TryGetEmpresa(config, out var empresa, out var error)) return Failure(error);
        using var cn = connectionDB.GetRmConnection();
        try { await cn.OpenAsync(); } catch (Exception ex) { return Failure($"Error técnico al abrir conexión RM: {ex.Message}"); }

        using var cmd = new OracleCommand("RM.SP_RM_CONTRI_GET_ALL", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
        cmd.Parameters.Add("p_PageSize", OracleDbType.Int32).Value = Math.Clamp(query.PageSize, 1, 100);
        cmd.Parameters.Add("p_PageNumber", OracleDbType.Int32).Value = Math.Max(query.PageNumber, 1);
        cmd.Parameters.Add("p_SearchText", OracleDbType.Varchar2).Value = CatastroContribuyentesDb.DbValue(query.SearchText);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
        var message = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
        var total = cmd.Parameters.Add("p_TotalRecords", OracleDbType.Int32, ParameterDirection.Output);
        var pages = cmd.Parameters.Add("p_TotalPages", OracleDbType.Int32, ParameterDirection.Output);
        try
        {
            var rows = new List<CatastroContribuyenteResponse>();
            using (var reader = await cmd.ExecuteReaderAsync()) while (await reader.ReadAsync()) rows.Add(CatastroContribuyentesDb.MapContribuyente(reader));
            var dbMessage = CatastroContribuyentesDb.Message(message);
            var valid = CatastroContribuyentesDb.Success(dbMessage);
            return new(valid ? rows : null!) { IsValid = valid, Message = dbMessage, Page = Math.Max(query.PageNumber, 1), CantidadRegistros = CatastroContribuyentesDb.OutputInt(total), TotalPage = CatastroContribuyentesDb.OutputInt(pages) };
        }
        catch (Exception ex) { return Failure($"Error técnico: {ex.Message}"); }
    }
    private static ResultDto<List<CatastroContribuyenteResponse>> Failure(string message) => new(null!) { IsValid = false, Message = message };
}

[ApiController]
[Authorize]
[Route("api/CatastroContribuyentes")]
public class CatastroContribuyentesGetAllController(ConnectionDB connectionDB, IConfiguration config) : ControllerBase
{
    [HttpPost("GetAll")]
    public async Task<IActionResult> GetAll(CatastroContribuyentesGetAllQuery query) => Ok(await new CatastroContribuyentesGetAllHandler(connectionDB, config).HandleAsync(query));
}
