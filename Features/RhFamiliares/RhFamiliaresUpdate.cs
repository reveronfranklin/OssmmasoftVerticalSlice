using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.RhFamiliares;

public class RhFamiliaresUpdateHandler(ConnectionDB db, IConfiguration config)
{
    public Task<ResultDto<RhFamiliarResponse>> HandleAsync(RhFamiliarRequest request, ClaimsPrincipal user)
        => RhFamiliaresDb.Save(db, config, request, false, user);
}

[Authorize]
[ApiController]
[Route("api/RhFamiliares")]
public class RhFamiliaresUpdateController(ConnectionDB db, IConfiguration config) : ControllerBase
{
    [HttpPost("Update")]
    public async Task<IActionResult> Update(RhFamiliarRequest request)
        => Ok(await new RhFamiliaresUpdateHandler(db, config).HandleAsync(request, User));
}
