using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.OssUsuarioRol;

public record OssUsuarioRolGetAllQuery(int PageSize = 10, int PageNumber = 1, string SearchText = "");

public class OssUsuarioRolGetAllHandler(ConnectionDB _connectionDB)
{
    public async Task<ResultDto<List<OssUsuarioRolResponse>>> HandleAsync(OssUsuarioRolGetAllQuery query)
    {
        int pageSize = query.PageSize <= 0 ? 10 : query.PageSize;
        int pageNumber = query.PageNumber <= 0 ? 1 : query.PageNumber;

        using var cn = _connectionDB.GetSisConnection();
        try
        {
            await cn.OpenAsync();
        }
        catch (Exception ex)
        {
            return new ResultDto<List<OssUsuarioRolResponse>>(null!)
            {
                IsValid = false,
                Message = $"Error técnico al abrir conexión SIS: {ex.Message}"
            };
        }

        using var cmd = new OracleCommand("SIS.SP_OSS_USR_ROL_GET_ALL", cn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.BindByName = true;

        cmd.Parameters.Add("p_PageSize", OracleDbType.Int32).Value = pageSize;
        cmd.Parameters.Add("p_PageNumber", OracleDbType.Int32).Value = pageNumber;
        cmd.Parameters.Add("p_SearchText", OracleDbType.Varchar2).Value = OssUsuarioRolDb.DbValue(query.SearchText);
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
        var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
        var pTotalRecords = cmd.Parameters.Add("p_TotalRecords", OracleDbType.Int32, ParameterDirection.Output);
        var pTotalPages = cmd.Parameters.Add("p_TotalPages", OracleDbType.Int32, ParameterDirection.Output);

        var list = new List<OssUsuarioRolResponse>();

        try
        {
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    list.Add(OssUsuarioRolDb.MapUsuarioRol(reader));
                }
            }

            string dbMessage = OssUsuarioRolDb.GetMessage(pMessage);
            bool isSuccess = OssUsuarioRolDb.IsSuccessMessage(dbMessage);
            int totalRecords = OssUsuarioRolDb.GetIntOutput(pTotalRecords);
            int totalPages = OssUsuarioRolDb.GetIntOutput(pTotalPages);

            return new ResultDto<List<OssUsuarioRolResponse>>(list)
            {
                Data = isSuccess ? list : null,
                CantidadRegistros = totalRecords,
                Page = pageNumber,
                TotalPage = totalPages,
                IsValid = isSuccess,
                Message = dbMessage
            };
        }
        catch (Exception ex)
        {
            return new ResultDto<List<OssUsuarioRolResponse>>(null!)
            {
                IsValid = false,
                Message = $"Error técnico: {ex.Message}"
            };
        }
    }
}

[ApiController]
[Route("api/OssUsuarioRol")]
public class OssUsuarioRolGetAllController(ConnectionDB _connectionDB) : ControllerBase
{
    [HttpPost]
    [Route("GetAll")]
    public async Task<IActionResult> GetAll(OssUsuarioRolGetAllQuery value)
    {
        var handler = new OssUsuarioRolGetAllHandler(_connectionDB);
        var result = await handler.HandleAsync(value);
        return Ok(result);
    }
}
