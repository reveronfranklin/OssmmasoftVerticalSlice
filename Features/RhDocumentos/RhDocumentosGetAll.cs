using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.RhDocumentos;

public record RhDocumentosGetAllQuery(int PageSize = 10, int PageNumber = 1, string SearchText = "");

public class RhDocumentosGetAllHandler(ConnectionDB _connectionDB, IConfiguration _config)
{
    public async Task<ResultDto<List<RhDocumentoResponse>>> HandleAsync(RhDocumentosGetAllQuery query)
    {
        if (!RhDocumentosDb.TryGetEmpresa(_config, out int empresa, out string errorMessage))
        {
            return new ResultDto<List<RhDocumentoResponse>>(null!) { IsValid = false, Message = errorMessage };
        }

        int pageSize = query.PageSize <= 0 ? 10 : query.PageSize;
        int pageNumber = query.PageNumber <= 0 ? 1 : query.PageNumber;

        using var cn = _connectionDB.GetRhConnection();
        await cn.OpenAsync();

        using var cmd = new OracleCommand("RH.SP_RH_DOC_GET_ALL", cn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.BindByName = true;

        cmd.Parameters.Add("p_PageSize", OracleDbType.Int32).Value = pageSize;
        cmd.Parameters.Add("p_PageNumber", OracleDbType.Int32).Value = pageNumber;
        cmd.Parameters.Add("p_SearchText", OracleDbType.Varchar2).Value = RhDocumentosDb.DbValue(query.SearchText);
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
        var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
        var pTotalRecords = cmd.Parameters.Add("p_TotalRecords", OracleDbType.Int32, ParameterDirection.Output);
        var pTotalPages = cmd.Parameters.Add("p_TotalPages", OracleDbType.Int32, ParameterDirection.Output);

        var list = new List<RhDocumentoResponse>();

        try
        {
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    list.Add(RhDocumentosDb.MapDocumento(reader));
                }
            }

            string dbMessage = RhDocumentosDb.GetMessage(pMessage);
            bool isSuccess = RhDocumentosDb.IsSuccessMessage(dbMessage);
            int totalRecords = RhDocumentosDb.GetIntOutput(pTotalRecords);
            int totalPages = RhDocumentosDb.GetIntOutput(pTotalPages);

            return new ResultDto<List<RhDocumentoResponse>>(list)
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
            return new ResultDto<List<RhDocumentoResponse>>(null!)
            {
                IsValid = false,
                Message = $"Error técnico: {ex.Message}"
            };
        }
    }
}

[ApiController]
[Route("api/RhDocumentos")]
public class RhDocumentosGetAllController(ConnectionDB _connectionDB, IConfiguration _config) : ControllerBase
{
    [HttpPost]
    [Route("GetAll")]
    public async Task<IActionResult> GetAll(RhDocumentosGetAllQuery value)
    {
        var handler = new RhDocumentosGetAllHandler(_connectionDB, _config);
        var result = await handler.HandleAsync(value);
        return Ok(result);
    }
}
