using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

[ApiController]
[Authorize]
[Route("api/CntPermissions")]
public class CntPermissionsController(CheckCntPermissionHandler checkHandler) : ControllerBase
{
    [HttpPost("check")]
    public async Task<IActionResult> Check(CntPermissionQuery value) =>
        Ok(await checkHandler.HandleAsync(value, User, Request));
}
