using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.RhDocumentos;

public record CreateRhDocumentoCommand(
    int CodigoPersona,
    int TipoDocumentoId,
    string? NumeroDocumento,
    DateTime? FechaVencimiento,
    int? TipoGradoId,
    int? GradoId,
    int UsuarioIns,
    string? Extra1 = null,
    string? Extra2 = null,
    string? Extra3 = null
);

public class CreateRhDocumentoHandler(ConnectionDB _connectionDB, IConfiguration _config)
{
    public async Task<ResultDto<int>> HandleAsync(CreateRhDocumentoCommand command)
    {
        if (!RhDocumentosDb.TryGetEmpresa(_config, out int empresa, out string errorMessage))
        {
            return new ResultDto<int>(0) { IsValid = false, Message = errorMessage };
        }

        using var cn = _connectionDB.GetRhConnection();
        try
        {
            await cn.OpenAsync();
        }
        catch (Exception ex)
        {
            return new ResultDto<int>(0)
            {
                IsValid = false,
                Message = $"Error técnico al abrir conexión RH: {ex.Message}"
            };
        }

        using var cmd = new OracleCommand("RH.SP_RH_DOC_INS", cn);
        cmd.CommandType = CommandType.StoredProcedure;
        cmd.BindByName = true;

        cmd.Parameters.Add("p_CODIGO_PERSONA", OracleDbType.Int32).Value = command.CodigoPersona;
        cmd.Parameters.Add("p_TIPO_DOCUMENTO_ID", OracleDbType.Int32).Value = command.TipoDocumentoId;
        cmd.Parameters.Add("p_NUMERO_DOCUMENTO", OracleDbType.Varchar2).Value = RhDocumentosDb.DbValue(command.NumeroDocumento);
        cmd.Parameters.Add("p_FECHA_VENCIMIENTO", OracleDbType.Date).Value = RhDocumentosDb.DbValue(command.FechaVencimiento);
        cmd.Parameters.Add("p_TIPO_GRADO_ID", OracleDbType.Int32).Value = RhDocumentosDb.PositiveDbValue(command.TipoGradoId);
        cmd.Parameters.Add("p_GRADO_ID", OracleDbType.Int32).Value = RhDocumentosDb.PositiveDbValue(command.GradoId);
        cmd.Parameters.Add("p_EXTRA1", OracleDbType.Varchar2).Value = RhDocumentosDb.DbValue(command.Extra1);
        cmd.Parameters.Add("p_EXTRA2", OracleDbType.Varchar2).Value = RhDocumentosDb.DbValue(command.Extra2);
        cmd.Parameters.Add("p_EXTRA3", OracleDbType.Varchar2).Value = RhDocumentosDb.DbValue(command.Extra3);
        cmd.Parameters.Add("p_USUARIO_INS", OracleDbType.Int32).Value = command.UsuarioIns;
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;

        var pCodigoDocumento = cmd.Parameters.Add("p_CODIGO_DOCUMENTO_OUT", OracleDbType.Int32, ParameterDirection.Output);
        var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

        try
        {
            await cmd.ExecuteNonQueryAsync();

            string dbMessage = RhDocumentosDb.GetMessage(pMessage);
            bool isSuccess = RhDocumentosDb.IsSuccessMessage(dbMessage);
            int codigoDocumento = isSuccess ? RhDocumentosDb.GetIntOutput(pCodigoDocumento) : 0;

            return new ResultDto<int>(codigoDocumento)
            {
                IsValid = isSuccess,
                Message = dbMessage
            };
        }
        catch (Exception ex)
        {
            return new ResultDto<int>(0)
            {
                IsValid = false,
                Message = $"Error técnico: {ex.Message}"
            };
        }
    }

}

[ApiController]
[Route("api/RhDocumentos")]
public class RhDocumentosCreateController(ConnectionDB _connectionDB, IConfiguration _config) : ControllerBase
{
    [HttpPost]
    [Route("create")]
    public async Task<IActionResult> Create(CreateRhDocumentoCommand value)
    {
        var handler = new CreateRhDocumentoHandler(_connectionDB, _config);
        var result = await handler.HandleAsync(value);
        return Ok(result);
    }
}
