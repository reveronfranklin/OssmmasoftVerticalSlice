using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.RhFamiliares;

public class RhFamiliaresCreateHandler(ConnectionDB db, IConfiguration config)
{
    public Task<ResultDto<RhFamiliarResponse>> HandleAsync(RhFamiliarRequest request, ClaimsPrincipal user)
        => RhFamiliaresDb.Save(db, config, request, true, user);
}

[Authorize]
[ApiController]
[Route("api/RhFamiliares")]
public class RhFamiliaresCreateController(ConnectionDB db, IConfiguration config) : ControllerBase
{
    [HttpPost("Create")]
    public async Task<IActionResult> Create(RhFamiliarRequest request)
        => Ok(await new RhFamiliaresCreateHandler(db, config).HandleAsync(request, User));
}
