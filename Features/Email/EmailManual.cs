using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Features.Support;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.Email;

[Authorize]
[ApiController]
[Route("api/Email")]
public class EmailManualController(ConnectionDB db, IConfiguration config, ILogger<EmailManualController> logger) : ControllerBase
{
    // Every operation resolves company and actor server-side, including history/configuration.
    private async Task<IActionResult> Execute<T>(Func<OracleConnection,int,int,Task<ResultDto<T>>> action)
    {
        if (!SupportDb.TryGetEmpresa(config,out var empresa,out var error)) return Ok(EmailManualRules.Fail<T>(error));
        using var cn = db.GetSisConnection();
        try { await cn.OpenAsync(HttpContext.RequestAborted); }
        catch { return Ok(EmailManualRules.Fail<T>("No se pudo conectar con el servicio de correo.")); }
        try
        {
            var actor = await SupportSecurity.GetAuthenticatedUserIdAsync(cn,User,empresa,null);
            if (actor <= 0) return Unauthorized();
            var permissions = await SupportSecurity.GetPermissionsAsync(cn,actor,empresa);
            if (!SupportSecurity.HasAny(permissions,EmailManualRules.Permission)) return StatusCode(403,EmailManualRules.Fail<T>("No tiene permiso para enviar correos."));
            return Ok(await action(cn,empresa,actor));
        }
        catch (OracleException ex) when (ex.Number is 20002 or 20003)
        {
            return Ok(EmailManualRules.Fail<T>(ex.Number == 20002 ? "La solicitud ya existe con otro contenido. Consulte su estado." : "Limite de 20 solicitudes por hora alcanzado."));
        }
        catch (Exception ex)
        {
            logger.LogWarning("Operacion de correo no completada ({Type}).",ex.GetType().Name);
            return Ok(EmailManualRules.Fail<T>("No se pudo completar la operacion. Consulte el estado antes de repetir el envio."));
        }
    }
    [HttpGet("configuracion")]
    public Task<IActionResult> Configuration() => Execute<EmailManualAvailability>((cn,empresa,actor) => {
        var settings = EmailSettings.Read(config); var error = settings.Validate();
        var availability = new EmailManualAvailability(settings.Enabled,error is null,settings.FromEmail,settings.FromName,
            EmailManualRules.MaxRecipients,EmailManualRules.MaxSubjectBytes,EmailManualRules.MaxBody,
            !settings.Enabled ? "Envio deshabilitado en este ambiente." : error ?? "Envio disponible.");
        return Task.FromResult(new ResultDto<EmailManualAvailability>(availability) { IsValid=true, Message="OK" });
    });
    [HttpPost("enviar")]
    public Task<IActionResult> Send(EmailManualCommand value)
    {
        var validation = EmailManualRules.Normalize(value);
        if (!validation.IsValid || validation.Data is null)
            return Task.FromResult<IActionResult>(UnprocessableEntity(EmailManualRules.Fail<EmailManualStatus>(validation.Message)));
        return Execute<EmailManualStatus>(async(cn,empresa,actor) => {
            var settings = EmailSettings.Read(config);
            if (!settings.Enabled || settings.Validate() is not null) return EmailManualRules.Fail<EmailManualStatus>("El envio esta deshabilitado o pendiente de configurar.");
            await EmailManualDb.Create(cn,empresa,actor,validation.Data,settings,HttpContext.RequestAborted);
            var page = await EmailManualDb.List(cn,empresa,actor,validation.Data.Solicitud,1,1,HttpContext.RequestAborted);
            return new(page.Items.Single()) { IsValid=true, Message="Solicitud registrada. Consulte su estado; no significa entrega al destinatario." };
        });
    }
    [HttpPost("estado")]
    public Task<IActionResult> Status(EmailManualQuery value) => Execute<EmailManualStatus>(async(cn,empresa,actor) => {
        if (!EmailManualRules.ValidKey(value.Solicitud)) return EmailManualRules.Fail<EmailManualStatus>("Solicitud invalida.");
        var page = await EmailManualDb.List(cn,empresa,actor,value.Solicitud.ToLowerInvariant(),1,1,HttpContext.RequestAborted);
        return page.Items.Count==0 ? EmailManualRules.Fail<EmailManualStatus>("Solicitud no encontrada para este usuario.") : new(page.Items[0]) { IsValid=true,Message="OK" };
    });
    [HttpPost("historial")]
    public Task<IActionResult> History(EmailManualHistoryQuery value) => Execute<EmailManualPage>(async(cn,empresa,actor) => {
        if (value.Page<1 || value.Page>100000 || value.PageSize<1 || value.PageSize>50) return EmailManualRules.Fail<EmailManualPage>("Paginacion invalida (1 a 50 filas). ");
        return new(await EmailManualDb.List(cn,empresa,actor,null,value.Page,value.PageSize,HttpContext.RequestAborted)) { IsValid=true,Message="OK" };
    });
}
