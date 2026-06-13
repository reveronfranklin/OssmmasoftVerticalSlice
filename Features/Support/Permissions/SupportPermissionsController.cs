using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OssmmasoftVerticalSlice.Features.Support;

[ApiController]
[Authorize]
[Route("api/SupportPermissions")]
public class SupportPermissionsController(GetSupportPermissionsHandler getByUserHandler) : ControllerBase
{
    [HttpPost("getByUser")]
    public async Task<IActionResult> GetByUser(SupportPermissionsQuery value) =>
        Ok(await getByUserHandler.HandleAsync(value, User, Request));
}
