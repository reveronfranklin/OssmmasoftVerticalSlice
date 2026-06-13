using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

[ApiController]
[Authorize]
[Route("api/CntBancos")]
public class CntBancosController(GetCntBancosHandler getAllHandler) : ControllerBase
{
    [HttpPost("GetAll")]
    public async Task<IActionResult> GetAll(CntBancoGetAllQuery value) =>
        Ok(await getAllHandler.HandleAsync(value, User, Request));
}
