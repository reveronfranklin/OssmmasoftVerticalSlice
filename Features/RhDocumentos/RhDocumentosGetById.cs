using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.RhDocumentos;

public record GetRhDocumentoByIdQuery(int CodigoDocumento);

public class GetRhDocumentoByIdHandler(ConnectionDB _connectionDB, IConfiguration _config)
{
    public async Task<ResultDto<RhDocumentoResponse>> HandleAsync(GetRhDocumentoByIdQuery query)
    {
        if (!RhDocumentosDb.TryGetEmpresa(_config, out int empresa, out string errorMessage))
        {
            return new ResultDto<RhDocumentoResponse>(null!) { IsValid = false, Message = errorMessage };
        }

        using var cn = _connectionDB.GetRhConnection();
        await cn.OpenAsync();

        using var cmd = new OracleCommand("RH.SP_RH_DOC_GET_ID", cn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.BindByName = true;

        cmd.Parameters.Add("p_CODIGO_DOCUMENTO", OracleDbType.Int32).Value = query.CodigoDocumento;
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
        var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

        RhDocumentoResponse? resultData = null;

        try
        {
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    resultData = RhDocumentosDb.MapDocumento(reader);
                }
            }

            string dbMessage = RhDocumentosDb.GetMessage(pMessage);
            bool isSuccess = RhDocumentosDb.IsSuccessMessage(dbMessage) && resultData is not null;

            return new ResultDto<RhDocumentoResponse>(resultData!)
            {
                IsValid = isSuccess,
                Message = resultData is null ? "Registro no encontrado" : dbMessage
            };
        }
        catch (Exception ex)
        {
            return new ResultDto<RhDocumentoResponse>(null!)
            {
                IsValid = false,
                Message = $"Error técnico: {ex.Message}"
            };
        }
    }
}

[ApiController]
[Route("api/RhDocumentos")]
public class RhDocumentosGetByIdController(ConnectionDB _connectionDB, IConfiguration _config) : ControllerBase
{
    [HttpPost]
    [Route("getById")]
    public async Task<IActionResult> GetById(GetRhDocumentoByIdQuery value)
    {
        var handler = new GetRhDocumentoByIdHandler(_connectionDB, _config);
        var result = await handler.HandleAsync(value);
        return Ok(result);
    }
}
