using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OssmmasoftVerticalSlice.Features.Support;

[ApiController]
[Authorize]
[Route("api/SupportSla")]
public class SupportSlaController(NotifyExpiredSupportSlaHandler notifyExpiredHandler) : ControllerBase
{
    [HttpPost("notifyExpired")]
    public async Task<IActionResult> NotifyExpired(SupportSlaNotifyCommand value) =>
        Ok(await notifyExpiredHandler.HandleAsync(value, User, Request));
}
