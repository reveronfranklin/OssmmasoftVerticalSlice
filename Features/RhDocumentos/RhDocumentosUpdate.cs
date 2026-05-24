using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.RhDocumentos;

public record UpdateRhDocumentoCommand(
    int CodigoDocumento,
    int CodigoPersona,
    int TipoDocumentoId,
    string? NumeroDocumento,
    DateTime? FechaVencimiento,
    int? TipoGradoId,
    int? GradoId,
    int UsuarioUpd,
    string? Extra1 = null,
    string? Extra2 = null,
    string? Extra3 = null
);

public class UpdateRhDocumentoHandler(ConnectionDB _connectionDB, IConfiguration _config)
{
    public async Task<ResultDto<string>> HandleAsync(UpdateRhDocumentoCommand command)
    {
        if (!RhDocumentosDb.TryGetEmpresa(_config, out int empresa, out string errorMessage))
        {
            return new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = errorMessage };
        }

        using var cn = _connectionDB.GetRhConnection();
        try
        {
            await cn.OpenAsync();
        }
        catch (Exception ex)
        {
            return new ResultDto<string>(string.Empty)
            {
                Data = null,
                IsValid = false,
                Message = $"Error técnico al abrir conexión RH: {ex.Message}"
            };
        }

        using var cmd = new OracleCommand("RH.SP_RH_DOC_UPD", cn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.BindByName = true;

        cmd.Parameters.Add("p_CODIGO_DOCUMENTO", OracleDbType.Int32).Value = command.CodigoDocumento;
        cmd.Parameters.Add("p_CODIGO_PERSONA", OracleDbType.Int32).Value = command.CodigoPersona;
        cmd.Parameters.Add("p_TIPO_DOCUMENTO_ID", OracleDbType.Int32).Value = command.TipoDocumentoId;
        cmd.Parameters.Add("p_NUMERO_DOCUMENTO", OracleDbType.Varchar2).Value = RhDocumentosDb.DbValue(command.NumeroDocumento);
        cmd.Parameters.Add("p_FECHA_VENCIMIENTO", OracleDbType.Date).Value = RhDocumentosDb.DbValue(command.FechaVencimiento);
        cmd.Parameters.Add("p_TIPO_GRADO_ID", OracleDbType.Int32).Value = RhDocumentosDb.PositiveDbValue(command.TipoGradoId);
        cmd.Parameters.Add("p_GRADO_ID", OracleDbType.Int32).Value = RhDocumentosDb.PositiveDbValue(command.GradoId);
        cmd.Parameters.Add("p_EXTRA1", OracleDbType.Varchar2).Value = RhDocumentosDb.DbValue(command.Extra1);
        cmd.Parameters.Add("p_EXTRA2", OracleDbType.Varchar2).Value = RhDocumentosDb.DbValue(command.Extra2);
        cmd.Parameters.Add("p_EXTRA3", OracleDbType.Varchar2).Value = RhDocumentosDb.DbValue(command.Extra3);
        cmd.Parameters.Add("p_USUARIO_UPD", OracleDbType.Int32).Value = command.UsuarioUpd;
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;

        var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

        try
        {
            await cmd.ExecuteNonQueryAsync();

            string dbMessage = RhDocumentosDb.GetMessage(pMessage);
            bool isSuccess = RhDocumentosDb.IsSuccessMessage(dbMessage);

            return new ResultDto<string>(isSuccess ? "Registro actualizado correctamente" : string.Empty)
            {
                Data = isSuccess ? "Registro actualizado correctamente" : null,
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
                Message = $"Error técnico en Update: {ex.Message}"
            };
        }
    }
}

[ApiController]
[Route("api/RhDocumentos")]
public class RhDocumentosUpdateController(ConnectionDB _connectionDB, IConfiguration _config) : ControllerBase
{
    [HttpPost]
    [Route("update")]
    public async Task<IActionResult> Update(UpdateRhDocumentoCommand value)
    {
        var handler = new UpdateRhDocumentoHandler(_connectionDB, _config);
        var result = await handler.HandleAsync(value);
        return Ok(result);
    }
}
