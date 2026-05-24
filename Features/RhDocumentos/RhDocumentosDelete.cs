using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.RhDocumentos;

public record DeleteRhDocumentoCommand(int CodigoDocumento);

public class DeleteRhDocumentoHandler(ConnectionDB _connectionDB, IConfiguration _config)
{
    public async Task<ResultDto<string>> HandleAsync(DeleteRhDocumentoCommand command)
    {
        if (!RhDocumentosDb.TryGetEmpresa(_config, out int empresa, out string errorMessage))
        {
            return new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = errorMessage };
        }

        using var cn = _connectionDB.GetRhConnection();
        await cn.OpenAsync();

        using var cmd = new OracleCommand("RH.SP_RH_DOC_DEL", cn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.BindByName = true;

        cmd.Parameters.Add("p_CODIGO_DOCUMENTO", OracleDbType.Int32).Value = command.CodigoDocumento;
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
        var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

        try
        {
            await cmd.ExecuteNonQueryAsync();

            string dbMessage = RhDocumentosDb.GetMessage(pMessage);
            bool isSuccess = RhDocumentosDb.IsSuccessMessage(dbMessage);

            return new ResultDto<string>(isSuccess ? "Registro eliminado correctamente" : string.Empty)
            {
                Data = isSuccess ? "Registro eliminado correctamente" : null,
                IsValid = isSuccess,
                Message = dbMessage
            };
        }
        catch (Exception ex)
        {
            return new ResultDto<string>(string.Empty)
            {
                Data = null,
                IsValid = false,
                Message = $"Error técnico al eliminar: {ex.Message}"
            };
        }
    }
}

[ApiController]
[Route("api/RhDocumentos")]
public class RhDocumentosDeleteController(ConnectionDB _connectionDB, IConfiguration _config) : ControllerBase
{
    [HttpPost]
    [Route("delete")]
    public async Task<IActionResult> Delete(DeleteRhDocumentoCommand value)
    {
        var handler = new DeleteRhDocumentoHandler(_connectionDB, _config);
        var result = await handler.HandleAsync(value);
        return Ok(result);
    }
}
