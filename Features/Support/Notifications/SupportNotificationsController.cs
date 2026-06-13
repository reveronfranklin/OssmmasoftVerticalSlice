using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OssmmasoftVerticalSlice.Features.Support;

[ApiController]
[Authorize]
[Route("api/SupportNotifications")]
public class SupportNotificationsController(
    GetSupportNotificationsByUserHandler getByUserHandler,
    MarkSupportNotificationReadHandler markReadHandler) : ControllerBase
{
    [HttpPost("getByUser")]
    public async Task<IActionResult> GetByUser(SupportNotificationGetByUserQuery value) =>
        Ok(await getByUserHandler.HandleAsync(value, User, Request));

    [HttpPost("markRead")]
    public async Task<IActionResult> MarkRead(SupportNotificationMarkReadCommand value) =>
        Ok(await markReadHandler.HandleAsync(value, User, Request));
}
