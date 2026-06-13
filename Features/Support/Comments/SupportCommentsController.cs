using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OssmmasoftVerticalSlice.Features.Support;

[ApiController]
[Authorize]
[Route("api/SupportComments")]
public class SupportCommentsController(
    CreateSupportCommentHandler createHandler,
    GetSupportCommentsByTicketHandler getByTicketHandler) : ControllerBase
{
    [HttpPost("create")]
    public async Task<IActionResult> Create(SupportCommentCreateCommand value) =>
        Ok(await createHandler.HandleAsync(value, User, Request));

    [HttpPost("getByTicket")]
    public async Task<IActionResult> GetByTicket(SupportTicketChildQuery value) =>
        Ok(await getByTicketHandler.HandleAsync(value, User, Request));
}
