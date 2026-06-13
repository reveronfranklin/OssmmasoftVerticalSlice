using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OssmmasoftVerticalSlice.Features.Support;

[ApiController]
[Authorize]
[Route("api/SupportAttachments")]
public class SupportAttachmentsController(
    CreateSupportAttachmentHandler createHandler,
    UploadSupportAttachmentHandler uploadHandler,
    GetSupportAttachmentsByTicketHandler getByTicketHandler) : ControllerBase
{
    [HttpPost("create")]
    public async Task<IActionResult> Create(SupportAttachmentCreateCommand value) =>
        Ok(await createHandler.HandleAsync(value, User, Request));

    [HttpPost("upload")]
    [RequestSizeLimit(SupportAttachmentHandlerSupport.MaxAttachmentBytes)]
    public async Task<IActionResult> Upload([FromForm] int ticketId, [FromForm] int usuarioCargaId, IFormFile file) =>
        Ok(await uploadHandler.HandleAsync(ticketId, usuarioCargaId, file, User, Request));

    [HttpPost("getByTicket")]
    public async Task<IActionResult> GetByTicket(SupportTicketChildQuery value) =>
        Ok(await getByTicketHandler.HandleAsync(value, User, Request));
}
