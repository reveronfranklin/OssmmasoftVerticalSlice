using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Features.Support;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.Email;

public record EmailQueueCreateCommand(string ModuloOrigen, int? ReferenciaId, string ToEmail, string? ToName, string Subject, string BodyHtml, string? BodyText, DateTime? FechaProgramada = null);
public record EmailQueueGetPendingQuery(int PageSize = 25);
public record EmailQueueUpdateStatusCommand(int EmailId, string? MessageId = null, string? ErrorEnvio = null);
public record EmailQueueResponse(int EmailId, string ModuloOrigen, int? ReferenciaId, string ToEmail, string ToName, string Subject, string BodyHtml, string BodyText, string Estado, int Intentos, int MaxIntentos, DateTime? FechaProgramada, DateTime? FechaEnvio, string ErrorEnvio);

[ApiController]
[Route("api/EmailQueue")]
public class EmailQueueController(ConnectionDB connectionDB, IConfiguration config) : ControllerBase
{
    [HttpPost("create")]
    public async Task<IActionResult> Create(EmailQueueCreateCommand value)
    {
        var result = await ExecuteScalarAsync("SIS.SP_EMAIL_Q_INS", cmd =>
        {
            cmd.Parameters.Add("p_MODULO_ORIGEN", OracleDbType.Varchar2).Value = SupportDb.StringDbValue(value.ModuloOrigen);
            cmd.Parameters.Add("p_REFERENCIA_ID", OracleDbType.Int32).Value = SupportDb.DbValue(value.ReferenciaId);
            cmd.Parameters.Add("p_TO_EMAIL", OracleDbType.Varchar2).Value = SupportDb.StringDbValue(value.ToEmail);
            cmd.Parameters.Add("p_TO_NAME", OracleDbType.Varchar2).Value = SupportDb.StringDbValue(value.ToName);
            cmd.Parameters.Add("p_SUBJECT", OracleDbType.Varchar2).Value = SupportDb.StringDbValue(value.Subject);
            cmd.Parameters.Add("p_BODY_HTML", OracleDbType.Clob).Value = SupportDb.StringDbValue(value.BodyHtml);
            cmd.Parameters.Add("p_BODY_TEXT", OracleDbType.Clob).Value = SupportDb.StringDbValue(value.BodyText);
            cmd.Parameters.Add("p_FECHA_PROGRAMADA", OracleDbType.Date).Value = SupportDb.DbValue(value.FechaProgramada);
        }, "p_EMAIL_ID_OUT");
        return Ok(result);
    }

    [HttpPost("getPending")]
    public async Task<IActionResult> GetPending(EmailQueueGetPendingQuery value)
    {
        if (!SupportDb.TryGetEmpresa(config, out int empresa, out string errorMessage))
        {
            return Ok(new ResultDto<List<EmailQueueResponse>>(null!) { IsValid = false, Message = errorMessage });
        }

        using var cn = connectionDB.GetSisConnection();
        await cn.OpenAsync();
        using var cmd = new OracleCommand("SIS.SP_EMAIL_Q_GET_PEND", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
        cmd.Parameters.Add("p_PageSize", OracleDbType.Int32).Value = value.PageSize <= 0 ? 25 : value.PageSize;
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
        var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
        var list = new List<EmailQueueResponse>();
        using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                list.Add(new EmailQueueResponse(
                    reader.SafeGetInt32("EMAIL_ID"),
                    reader.SafeGetString("MODULO_ORIGEN"),
                    SupportDb.SafeGetNullableInt(reader, "REFERENCIA_ID"),
                    reader.SafeGetString("TO_EMAIL"),
                    reader.SafeGetString("TO_NAME"),
                    reader.SafeGetString("SUBJECT"),
                    reader.SafeGetString("BODY_HTML"),
                    reader.SafeGetString("BODY_TEXT"),
                    reader.SafeGetString("ESTADO"),
                    reader.SafeGetInt32("INTENTOS"),
                    reader.SafeGetInt32("MAX_INTENTOS"),
                    SupportDb.SafeGetNullableDate(reader, "FECHA_PROGRAMADA"),
                    SupportDb.SafeGetNullableDate(reader, "FECHA_ENVIO"),
                    reader.SafeGetString("ERROR_ENVIO")
                ));
            }
        }
        var message = SupportDb.GetMessage(pMessage);
        var isSuccess = SupportDb.IsSuccessMessage(message);
        return Ok(new ResultDto<List<EmailQueueResponse>>(list) { Data = isSuccess ? list : null, IsValid = isSuccess, Message = message });
    }

    [HttpPost("markSent")]
    public async Task<IActionResult> MarkSent(EmailQueueUpdateStatusCommand value)
    {
        return Ok(await ExecuteMessageAsync("SIS.SP_EMAIL_Q_SENT", cmd =>
        {
            cmd.Parameters.Add("p_EMAIL_ID", OracleDbType.Int32).Value = value.EmailId;
            cmd.Parameters.Add("p_MESSAGE_ID", OracleDbType.Varchar2).Value = SupportDb.StringDbValue(value.MessageId);
        }));
    }

    [HttpPost("markError")]
    public async Task<IActionResult> MarkError(EmailQueueUpdateStatusCommand value)
    {
        return Ok(await ExecuteMessageAsync("SIS.SP_EMAIL_Q_ERROR", cmd =>
        {
            cmd.Parameters.Add("p_EMAIL_ID", OracleDbType.Int32).Value = value.EmailId;
            cmd.Parameters.Add("p_ERROR_ENVIO", OracleDbType.Varchar2).Value = SupportDb.StringDbValue(value.ErrorEnvio);
        }));
    }

    private async Task<ResultDto<int>> ExecuteScalarAsync(string procedure, Action<OracleCommand> bind, string outputName)
    {
        if (!SupportDb.TryGetEmpresa(config, out int empresa, out string errorMessage))
        {
            return new ResultDto<int>(0) { IsValid = false, Message = errorMessage };
        }

        using var cn = connectionDB.GetSisConnection();
        await cn.OpenAsync();
        using var cmd = new OracleCommand(procedure, cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
        bind(cmd);
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
        var pOutput = cmd.Parameters.Add(outputName, OracleDbType.Int32, ParameterDirection.Output);
        var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
        await cmd.ExecuteNonQueryAsync();
        var message = SupportDb.GetMessage(pMessage);
        var isSuccess = SupportDb.IsSuccessMessage(message);
        return new ResultDto<int>(isSuccess ? SupportDb.GetIntOutput(pOutput) : 0) { IsValid = isSuccess, Message = message };
    }

    private async Task<ResultDto<string>> ExecuteMessageAsync(string procedure, Action<OracleCommand> bind)
    {
        if (!SupportDb.TryGetEmpresa(config, out int empresa, out string errorMessage))
        {
            return new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = errorMessage };
        }

        using var cn = connectionDB.GetSisConnection();
        await cn.OpenAsync();
        using var cmd = new OracleCommand(procedure, cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
        bind(cmd);
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
        var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
        await cmd.ExecuteNonQueryAsync();
        var message = SupportDb.GetMessage(pMessage);
        var isSuccess = SupportDb.IsSuccessMessage(message);
        return new ResultDto<string>(isSuccess ? "OK" : string.Empty) { Data = isSuccess ? "OK" : null, IsValid = isSuccess, Message = message };
    }
}
