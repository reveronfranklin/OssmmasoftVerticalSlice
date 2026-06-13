using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

[ApiController]
[Authorize]
[Route("api/CntCatalogClone")]
public class CntCatalogCloneController(
    CloneCntDescriptivasHandler cloneDescriptivasHandler,
    CloneCntPlanCuentasHandler clonePlanCuentasHandler) : ControllerBase
{
    [HttpPost("descriptivas")]
    public async Task<IActionResult> CloneDescriptivas(CntCloneDescriptivasCommand value) =>
        Ok(await cloneDescriptivasHandler.HandleAsync(value, User, Request));

    [HttpPost("planCuentas")]
    public async Task<IActionResult> ClonePlanCuentas(CntClonePlanCuentasCommand value) =>
        Ok(await clonePlanCuentasHandler.HandleAsync(value, User, Request));
}
