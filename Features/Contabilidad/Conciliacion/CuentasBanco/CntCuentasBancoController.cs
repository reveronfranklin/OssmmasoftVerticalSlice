using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

[ApiController]
[Authorize]
[Route("api/CntCuentasBanco")]
public class CntCuentasBancoController(GetCntCuentasBancoHandler getAllHandler) : ControllerBase
{
    [HttpPost("GetAll")]
    public async Task<IActionResult> GetAll(CntCuentaBancoGetAllQuery value) =>
        Ok(await getAllHandler.HandleAsync(value, User, Request));
}
