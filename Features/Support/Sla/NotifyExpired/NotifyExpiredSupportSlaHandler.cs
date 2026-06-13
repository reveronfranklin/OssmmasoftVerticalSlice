using Microsoft.AspNetCore.Http;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;
using System.Security.Claims;

namespace OssmmasoftVerticalSlice.Features.Support;

public class NotifyExpiredSupportSlaHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<int>> HandleAsync(
        SupportSlaNotifyCommand value,
        ClaimsPrincipal user,
        HttpRequest request)
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

        if (!SupportSecurity.HasAny(permissions, SupportSecurity.TicketViewAll, SupportSecurity.DashboardView))
        {
            return SupportSecurity.Forbidden<int>(SupportSecurity.DashboardView);
        }

        using var cmd = new OracleCommand("SIS.SP_SOP_SLA_NOTIF", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
        var pCount = cmd.Parameters.Add("p_NOTIF_COUNT_OUT", OracleDbType.Int32, ParameterDirection.Output);
        var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
        await cmd.ExecuteNonQueryAsync();
        var message = SupportDb.GetMessage(pMessage);
        var isSuccess = SupportDb.IsSuccessMessage(message);

        return new ResultDto<int>(isSuccess ? SupportDb.GetIntOutput(pCount) : 0) { IsValid = isSuccess, Message = message };
    }
}
