using Microsoft.AspNetCore.Http;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;
using System.Security.Claims;

namespace OssmmasoftVerticalSlice.Features.Support;

public class CreateSupportCommentHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<int>> HandleAsync(SupportCommentCreateCommand value, ClaimsPrincipal user, HttpRequest request)
    {
        var userValidation = await SupportSecurity.ValidateRequestUserAsync(connectionDB, config, user, request, value.UsuarioId);
        if (!userValidation.IsValid)
        {
            return new ResultDto<int>(0) { IsValid = false, Message = userValidation.Message };
        }

        if (!SupportDb.TryGetEmpresa(config, out int empresa, out string errorMessage))
        {
            return new ResultDto<int>(0) { IsValid = false, Message = errorMessage };
        }

        using var cn = connectionDB.GetSisConnection();
        await cn.OpenAsync();
        var permissions = await SupportSecurity.GetPermissionsAsync(cn, value.UsuarioId, empresa);
        var requiredPermission = value.EsInterno ? SupportSecurity.CommentInternal : SupportSecurity.CommentCreate;

        if (!SupportSecurity.HasAny(permissions, requiredPermission))
        {
            return SupportSecurity.Forbidden<int>(requiredPermission);
        }

        using var cmd = new OracleCommand("SIS.SP_SOP_COMM_INS", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
        cmd.Parameters.Add("p_TICKET_ID", OracleDbType.Int32).Value = value.TicketId;
        cmd.Parameters.Add("p_USUARIO_ID", OracleDbType.Int32).Value = value.UsuarioId;
        cmd.Parameters.Add("p_COMENTARIO", OracleDbType.Varchar2).Value = SupportDb.StringDbValue(value.Comentario);
        cmd.Parameters.Add("p_ES_INTERNO", OracleDbType.Int32).Value = value.EsInterno ? 1 : 0;
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
        var pComentarioId = cmd.Parameters.Add("p_COMENTARIO_ID_OUT", OracleDbType.Int32, ParameterDirection.Output);
        var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
        await cmd.ExecuteNonQueryAsync();
        var message = SupportDb.GetMessage(pMessage);
        var isSuccess = SupportDb.IsSuccessMessage(message);

        return new ResultDto<int>(isSuccess ? SupportDb.GetIntOutput(pComentarioId) : 0) { IsValid = isSuccess, Message = message };
    }
}
