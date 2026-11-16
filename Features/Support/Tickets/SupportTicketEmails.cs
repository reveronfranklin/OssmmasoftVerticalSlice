using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.Support;

public record SupportTicketEmailResponse(int EmailId, string Para, string Cc, string Asunto,
    string Estado, int Intentos, DateTime FechaSolicitud, DateTime? FechaEnvio, string Error);

[ApiController]
[Authorize]
[Route("api/SupportTicketEmails")]
public class SupportTicketEmailsController(ConnectionDB db, IConfiguration config, ILogger<SupportTicketEmailsController> logger) : ControllerBase
{
    [HttpPost("getByTicket")]
    public async Task<IActionResult> GetByTicket(SupportTicketChildQuery value)
    {
        ResultDto<List<SupportTicketEmailResponse>> Fail(string message) => new(null!) { IsValid = false, Message = message };
        try
        {
            var validation = await SupportSecurity.ValidateRequestUserAsync(db, config, User, Request, value.UsuarioId);
            if (!validation.IsValid) return Ok(Fail(validation.Message));
            var access = await SupportSecurity.CanViewTicketAsync(db, config, value.UsuarioId, value.TicketId);
            if (!access.IsValid) return Ok(Fail(access.Message));
            var reader = new SupportChildRowsReader(db, config);
            return Ok(await reader.ReadAsync("SIS.SP_SOP_EMAIL_GET", value.TicketId, r => new SupportTicketEmailResponse(
                r.SafeGetInt32("EMAIL_ID"), r.SafeGetString("PARA_LIST"), r.SafeGetString("CC_LIST"),
                r.SafeGetString("SUBJECT"), r.SafeGetString("ESTADO"), r.SafeGetInt32("INTENTOS"),
                Convert.ToDateTime(r.GetValue(r.GetOrdinal("CREATED_AT"))),
                SupportDb.SafeGetNullableDate(r, "FECHA_ENVIO"), r.SafeGetString("ERROR_ENVIO"))));
        }
        catch (OracleException ex)
        {
            logger.LogWarning("Consulta de correo de ticket {Ticket}: ORA-{Code}", value.TicketId, ex.Number);
            var message = ex.Number is 6550 or 4043 or 4063 or 6508
                ? "La consulta de correos no esta instalada o no es valida en SIS. Ejecute SP_SOP_EMAIL_GET.sql y revise sus permisos."
                : $"No se pudo consultar el correo del ticket (ORA-{ex.Number:D5}). Contacte al administrador.";
            return Ok(Fail(message));
        }
        catch (Exception ex)
        {
            logger.LogWarning("Consulta de correo de ticket {Ticket}: {Type}; pila {Stack}", value.TicketId, ex.GetType().Name, ex.StackTrace);
            return Ok(Fail("No se pudo consultar el estado del correo del ticket. Intente nuevamente."));
        }
    }
}
