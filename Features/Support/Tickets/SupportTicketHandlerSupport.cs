using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.Support;

public class SupportTicketHandlerSupport(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<string>> ValidateSupportAnalystAsync(int usuarioResponsableId)
    {
        if (usuarioResponsableId <= 0)
        {
            return new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = "Debe seleccionar un analista responsable." };
        }

        if (!SupportDb.TryGetEmpresa(config, out int empresa, out string errorMessage))
        {
            return new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = errorMessage };
        }

        using var cn = connectionDB.GetSisConnection();
        await cn.OpenAsync();
        using var cmd = new OracleCommand(@"
            SELECT COUNT(1)
              FROM SIS.SIS_USUARIOS
             WHERE CODIGO_USUARIO = :usuarioId
               AND CODIGO_EMPRESA = :empresa
               AND NVL(STATUS, 'A') IN ('1', 'A')
               AND NVL(ES_ANALISTA_SOPORTE, 0) = 1", cn)
        {
            CommandType = CommandType.Text,
            BindByName = true
        };
        cmd.Parameters.Add("usuarioId", OracleDbType.Int32).Value = usuarioResponsableId;
        cmd.Parameters.Add("empresa", OracleDbType.Int32).Value = empresa;

        var count = SupportDb.ToInt32(await cmd.ExecuteScalarAsync());
        return count > 0
            ? new ResultDto<string>("OK") { IsValid = true, Message = "Success" }
            : new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = "El responsable seleccionado no esta marcado como analista de soporte." };
    }

    public async Task<ResultDto<string>> CheckPermissionAsync(int usuarioId, string permission)
    {
        if (!SupportDb.TryGetEmpresa(config, out int empresa, out string errorMessage))
        {
            return new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = errorMessage };
        }

        using var cn = connectionDB.GetSisConnection();
        await cn.OpenAsync();
        var permissions = await SupportSecurity.GetPermissionsAsync(cn, usuarioId, empresa);

        return SupportSecurity.HasAny(permissions, permission)
            ? new ResultDto<string>("OK") { IsValid = true, Message = "Success" }
            : SupportSecurity.Forbidden<string>(permission);
    }

    public async Task<ResultDto<string>> CanViewTicketAsync(int usuarioId, SupportTicketResponse ticket)
    {
        if (!SupportDb.TryGetEmpresa(config, out int empresa, out string errorMessage))
        {
            return new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = errorMessage };
        }

        using var cn = connectionDB.GetSisConnection();
        await cn.OpenAsync();
        var permissions = await SupportSecurity.GetPermissionsAsync(cn, usuarioId, empresa);

        if (SupportSecurity.HasAny(permissions, SupportSecurity.TicketViewAll)
            || (SupportSecurity.HasAny(permissions, SupportSecurity.TicketViewAssigned) && ticket.UsuarioResponsableId == usuarioId)
            || (SupportSecurity.HasAny(permissions, SupportSecurity.TicketViewOwn) && ticket.UsuarioSolicitanteId == usuarioId))
        {
            return new ResultDto<string>("OK") { IsValid = true, Message = "Success" };
        }

        return SupportSecurity.Forbidden<string>(SupportSecurity.TicketViewOwn);
    }

    public async Task<ResultDto<string>> ExecuteMessageAsync(string procedure, Action<OracleCommand> bind)
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

    public async Task<ResultDto<int>> ExecuteScalarAsync(string procedure, Action<OracleCommand> bind, string outputName)
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

    public async Task<ResultDto<T>> ExecuteReaderSingleAsync<T>(string procedure, Action<OracleCommand> bind, Func<IDataReader, T> map)
    {
        if (!SupportDb.TryGetEmpresa(config, out int empresa, out string errorMessage))
        {
            return new ResultDto<T>(default!) { Data = default, IsValid = false, Message = errorMessage };
        }

        using var cn = connectionDB.GetSisConnection();
        await cn.OpenAsync();
        using var cmd = new OracleCommand(procedure, cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
        bind(cmd);
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
        var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
        T? item = default;

        using (var reader = await cmd.ExecuteReaderAsync())
        {
            if (await reader.ReadAsync())
            {
                item = map(reader);
            }
        }

        var message = SupportDb.GetMessage(pMessage);
        var isSuccess = SupportDb.IsSuccessMessage(message) && item is not null;
        return new ResultDto<T>(item!) { Data = isSuccess ? item : default, IsValid = isSuccess, Message = isSuccess ? message : "No se encontró el registro indicado." };
    }
}
