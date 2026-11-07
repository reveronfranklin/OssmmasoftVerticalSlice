using Microsoft.AspNetCore.Mvc;

namespace OssmmasoftVerticalSlice.Features.Email;

public record EmailQueueCreateCommand(string ModuloOrigen, int? ReferenciaId, string ToEmail, string? ToName, string Subject, string BodyHtml, string? BodyText, DateTime? FechaProgramada = null);
public record EmailQueueGetPendingQuery(int PageSize = 25);
public record EmailQueueUpdateStatusCommand(int EmailId, string? MessageId = null, string? ErrorEnvio = null);
public record EmailQueueResponse(int EmailId, string ModuloOrigen, int? ReferenciaId, string ToEmail, string ToName, string Subject, string BodyHtml, string BodyText, string Estado, int Intentos, int MaxIntentos, DateTime? FechaProgramada, DateTime? FechaEnvio, string ErrorEnvio);

// Retired public queue mutations: only the authenticated manual flow can enqueue.
[Microsoft.AspNetCore.Authorization.Authorize]
[ApiController]
[Route("api/EmailQueue")]
public class EmailQueueController : ControllerBase
{
    [HttpPost("create")]
    [HttpPost("getPending")]
    [HttpPost("markSent")]
    [HttpPost("markError")]
    public IActionResult Retired() => StatusCode(410, EmailManualRules.Fail<string>("Operacion retirada. Utilice api/Email y su historial autorizado."));
}
