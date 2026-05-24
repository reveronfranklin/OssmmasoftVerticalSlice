using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.RhDocumentos;

public record GetRhDocumentosByPersonaQuery(int CodigoPersona);

public class GetRhDocumentosByPersonaHandler(ConnectionDB _connectionDB, IConfiguration _config)
{
    public async Task<ResultDto<List<RhDocumentoResponse>>> HandleAsync(GetRhDocumentosByPersonaQuery query)
    {
        if (!RhDocumentosDb.TryGetEmpresa(_config, out int empresa, out string errorMessage))
        {
            return new ResultDto<List<RhDocumentoResponse>>(null!) { IsValid = false, Message = errorMessage };
        }

        using var cn = _connectionDB.GetRhConnection();
        await cn.OpenAsync();

        using var cmd = new OracleCommand("RH.SP_RH_DOC_GET_PER", cn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.BindByName = true;

        cmd.Parameters.Add("p_CODIGO_PERSONA", OracleDbType.Int32).Value = query.CodigoPersona;
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
        var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
        var pTotalRecords = cmd.Parameters.Add("p_TotalRecords", OracleDbType.Int32, ParameterDirection.Output);

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

            return new ResultDto<List<RhDocumentoResponse>>(list)
            {
                Data = isSuccess ? list : null,
                CantidadRegistros = totalRecords,
                Page = 1,
                TotalPage = 1,
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
public class RhDocumentosGetByPersonaController(ConnectionDB _connectionDB, IConfiguration _config) : ControllerBase
{
    [HttpPost]
    [Route("getByPersona")]
    public async Task<IActionResult> GetByPersona(GetRhDocumentosByPersonaQuery value)
    {
        var handler = new GetRhDocumentosByPersonaHandler(_connectionDB, _config);
        var result = await handler.HandleAsync(value);
        return Ok(result);
    }
}
