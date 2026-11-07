using System.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Features.Email;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.Support;

public record SupportDefaultUser(int CodigoUsuario, string Usuario, string Login, string Email);
public record SupportConfigurationResponse(int? ResponsableId, List<SupportDefaultUser> Responsables);
public record SupportConfigurationSave(int ResponsableId);

[Authorize, ApiController, Route("api/SupportConfiguration")]
public class SupportConfigurationController(SupportTicketAutomation handler) : ControllerBase
{
    [HttpPost("get")]
    public async Task<IActionResult> Get() => Ok(await handler.Configuration(User, Request, null));
    [HttpPost("save")]
    public async Task<IActionResult> Save(SupportConfigurationSave value) => Ok(await handler.Configuration(User, Request, value));
}

public class SupportTicketAutomation(ConnectionDB db, IConfiguration config)
{
    internal static OracleCommand Command(OracleConnection cn, string name, int company)
    {
        var cmd = new OracleCommand("SIS." + name, cn) { BindByName = true, CommandType = CommandType.StoredProcedure };
        cmd.Parameters.Add("p_EMPRESA", OracleDbType.Int32).Value = company;
        return cmd;
    }
    internal static async Task<SupportConfigurationResponse> Read(OracleConnection cn, int company)
    {
        using var cmd = Command(cn, "SP_SOP_CFG_GET", company);
        var id = cmd.Parameters.Add("p_RESPONSABLE", OracleDbType.Int32, ParameterDirection.Output);
        cmd.Parameters.Add("p_ROWS", OracleDbType.RefCursor, ParameterDirection.Output);
        var users = new List<SupportDefaultUser>();
        using (var r = await cmd.ExecuteReaderAsync())
            while (await r.ReadAsync()) users.Add(new(r.SafeGetInt32("CODIGO_USUARIO"), r.SafeGetString("USUARIO"), r.SafeGetString("LOGIN"), r.SafeGetString("EMAIL").Trim()));
        var number = SupportDb.GetIntOutput(id);
        return new(number > 0 ? number : null, users);
    }
    internal static async Task<bool> CanReceive(OracleConnection cn, int company, int id)
    {
        var rights = await SupportSecurity.GetPermissionsAsync(cn, id, company);
        return SupportSecurity.HasAny(rights, SupportSecurity.TicketViewAssigned, SupportSecurity.TicketViewAll);
    }
    public async Task<ResultDto<SupportConfigurationResponse>> Configuration(ClaimsPrincipal user, HttpRequest request, SupportConfigurationSave? value)
    {
        if (!SupportDb.TryGetEmpresa(config, out var company, out var error)) return EmailManualRules.Fail<SupportConfigurationResponse>(error);
        try
        {
            using var cn = db.GetSisConnection();
            try { await cn.OpenAsync(); } catch { return EmailManualRules.Fail<SupportConfigurationResponse>("No se pudo conectar con SIS."); }
            var actor = await SupportSecurity.ResolveAuthenticatedUserAsync(cn, company, user, request);
            if (!actor.IsValid) return EmailManualRules.Fail<SupportConfigurationResponse>(actor.Message);
            var rights = await SupportSecurity.GetPermissionsAsync(cn, actor.Data, company);
            if (!SupportSecurity.HasAny(rights, SupportSecurity.SupportUsersConfig)) return SupportSecurity.Forbidden<SupportConfigurationResponse>(SupportSecurity.SupportUsersConfig);
            var current = await Read(cn, company);
            if (value is not null)
            {
                var selected = current.Responsables.SingleOrDefault(x => x.CodigoUsuario == value.ResponsableId);
                if (selected is null || !EmailManualRules.IsAddress(selected.Email) || !await CanReceive(cn, company, selected.CodigoUsuario))
                    return EmailManualRules.Fail<SupportConfigurationResponse>("Seleccione un analista activo, con correo valido y permiso para consultar tickets asignados.");
                using var tx = cn.BeginTransaction();
                using var cmd = Command(cn, "SP_SOP_CFG_SAVE", company); cmd.Transaction = tx;
                cmd.Parameters.Add("p_RESPONSABLE", OracleDbType.Int32).Value = selected.CodigoUsuario;
                cmd.Parameters.Add("p_ACTOR", OracleDbType.Int32).Value = actor.Data;
                await cmd.ExecuteNonQueryAsync(); tx.Commit();
                current = await Read(cn, company);
            }
            return new(current) { IsValid = true, Message = "Success" };
        }
        catch (OracleException) { return EmailManualRules.Fail<SupportConfigurationResponse>("No se pudo leer o guardar la configuracion de Soporte. Verifique la instalacion SQL."); }
        catch (Exception) { return EmailManualRules.Fail<SupportConfigurationResponse>("No se pudo procesar la configuracion de Soporte."); }
    }
    public async Task<ResultDto<int>> Create(SupportTicketCreateCommand value)
    {
        if (!SupportDb.TryGetEmpresa(config, out var company, out var error)) return EmailManualRules.Fail<int>(error);
        try
        {
            using var cn = db.GetSisConnection();
            try { await cn.OpenAsync(); } catch { return EmailManualRules.Fail<int>("No se pudo conectar con SIS."); }
            var defaults = await Read(cn, company);
            var responsible = defaults.Responsables.SingleOrDefault(x => x.CodigoUsuario == defaults.ResponsableId);
            if (responsible is null || !EmailManualRules.IsAddress(responsible.Email) || !await CanReceive(cn, company, responsible.CodigoUsuario))
                return EmailManualRules.Fail<int>("Configure un responsable activo de Soporte con correo valido y acceso a tickets asignados.");
            var settings = EmailSettings.Read(config);
            if (!settings.Enabled || settings.Validate() is not null) return EmailManualRules.Fail<int>("El envio institucional de correos no esta disponible.");
            using var contact = Command(cn, "SP_SOP_USR_MAIL", company);
            contact.Parameters.Add("p_USUARIO", OracleDbType.Int32).Value = value.CreatedBy;
            var mail = contact.Parameters.Add("p_EMAIL", OracleDbType.Varchar2, 150, null, ParameterDirection.Output);
            await contact.ExecuteNonQueryAsync();
            var requester = SupportDb.GetMessage(mail).Trim();
            if (!EmailManualRules.IsAddress(requester)) return EmailManualRules.Fail<int>("Actualice el correo del usuario solicitante en Usuarios antes de crear el ticket.");
            using var tx = cn.BeginTransaction();
            using var cmd = Command(cn, "SP_SOP_TKT_AUTO", company); cmd.Transaction = tx;
            foreach (var (name, id) in new[] { ("p_SOLICITANTE", value.CreatedBy), ("p_TIPO", value.TipoSolicitudId), ("p_MODULO", value.ModuloId), ("p_PRIORIDAD", value.PrioridadId), ("p_RESPONSABLE", responsible.CodigoUsuario) })
                cmd.Parameters.Add(name, OracleDbType.Int32).Value = id;
            foreach (var (name, text) in new[] { ("p_ASUNTO", value.Asunto), ("p_DESCRIPCION", value.Descripcion), ("p_PARA", responsible.Email), ("p_CC", requester), ("p_FROM", settings.FromEmail), ("p_NOMBRE", settings.FromName) })
                cmd.Parameters.Add(name, OracleDbType.Varchar2).Value = SupportDb.StringDbValue(text);
            var output = cmd.Parameters.Add("p_ID", OracleDbType.Int32, ParameterDirection.Output);
            await cmd.ExecuteNonQueryAsync(); tx.Commit();
            return new(SupportDb.GetIntOutput(output)) { IsValid = true, Message = "Ticket creado, asignado al responsable y notificacion de correo pendiente de envio." };
        }
        catch (OracleException ex) when (ex.Number == 20060) { return EmailManualRules.Fail<int>("La configuracion o los contactos cambiaron. Revise el responsable y los correos y vuelva a intentar."); }
        catch (Exception) { return EmailManualRules.Fail<int>("No se pudo crear y asignar el ticket. No se guardaron cambios; verifique los catalogos y la configuracion de Soporte."); }
    }
}
