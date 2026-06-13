using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OssmmasoftVerticalSlice.Features.Support;

[ApiController]
[Authorize]
[Route("api/SupportCatalogs")]
public class SupportCatalogsController(GetSupportCatalogsHandler getAllHandler) : ControllerBase
{
    [HttpPost("GetAll")]
    public async Task<IActionResult> GetAll(SupportCatalogGetAllQuery value) =>
        Ok(await getAllHandler.HandleAsync(value));
}
