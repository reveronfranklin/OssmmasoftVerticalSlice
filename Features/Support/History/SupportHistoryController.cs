using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OssmmasoftVerticalSlice.Features.Support;

[ApiController]
[Authorize]
[Route("api/SupportHistory")]
public class SupportHistoryController(GetSupportHistoryByTicketHandler getByTicketHandler) : ControllerBase
{
    [HttpPost("getByTicket")]
    public async Task<IActionResult> GetByTicket(SupportTicketChildQuery value) =>
        Ok(await getByTicketHandler.HandleAsync(value, User, Request));
}
