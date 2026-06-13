using Microsoft.AspNetCore.Http;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;
using System.Security.Claims;

namespace OssmmasoftVerticalSlice.Features.Support;

public class GetSupportNotificationsByUserHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<List<SupportNotificationResponse>>> HandleAsync(
        SupportNotificationGetByUserQuery value,
        ClaimsPrincipal user,
        HttpRequest request)
    {
        var userValidation = await SupportSecurity.ValidateRequestUserAsync(connectionDB, config, user, request, value.UsuarioId);
        if (!userValidation.IsValid)
        {
            return new ResultDto<List<SupportNotificationResponse>>(null!) { IsValid = false, Message = userValidation.Message };
        }

        if (!SupportDb.TryGetEmpresa(config, out int empresa, out string errorMessage))
        {
            return new ResultDto<List<SupportNotificationResponse>>(null!) { IsValid = false, Message = errorMessage };
        }

        using var cn = connectionDB.GetSisConnection();
        await cn.OpenAsync();
        using var cmd = new OracleCommand("SIS.SP_SOP_NOTIF_GET_USR", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
        cmd.Parameters.Add("p_USUARIO_ID", OracleDbType.Int32).Value = value.UsuarioId;
        cmd.Parameters.Add("p_PageSize", OracleDbType.Int32).Value = value.PageSize <= 0 ? 10 : value.PageSize;
        cmd.Parameters.Add("p_PageNumber", OracleDbType.Int32).Value = value.PageNumber <= 0 ? 1 : value.PageNumber;
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
        var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
        var list = new List<SupportNotificationResponse>();

        using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                list.Add(new SupportNotificationResponse(
                    reader.SafeGetInt32("NOTIF_ID"),
                    reader.SafeGetInt32("TICKET_ID"),
                    reader.SafeGetInt32("USUARIO_DESTINO_ID"),
                    reader.SafeGetString("EVENTO"),
                    reader.SafeGetString("TITULO"),
                    reader.SafeGetString("MENSAJE"),
                    reader.SafeGetString("CANAL"),
                    SupportDb.SafeGetFlag(reader, "LEIDA"),
                    Convert.ToDateTime(reader.GetValue(reader.GetOrdinal("FECHA_CREACION")))
                ));
            }
        }

        var message = SupportDb.GetMessage(pMessage);
        var isSuccess = SupportDb.IsSuccessMessage(message);
        return new ResultDto<List<SupportNotificationResponse>>(list) { Data = isSuccess ? list : null, IsValid = isSuccess, Message = message };
    }
}
