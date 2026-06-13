using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

[ApiController]
[Authorize]
[Route("api/CntEstadosCuenta")]
public class CntEstadosCuentaController(
    GetCntEstadosCuentaHandler getAllHandler,
    GetCntEstadoCuentaDetallesHandler getDetailsHandler) : ControllerBase
{
    [HttpPost("GetAll")]
    public async Task<IActionResult> GetAll(CntEstadosCuentaGetQuery value) =>
        Ok(await getAllHandler.HandleAsync(value, User, Request));

    [HttpPost("Details")]
    public async Task<IActionResult> Details(CntEstadoCuentaDetalleGetQuery value) =>
        Ok(await getDetailsHandler.HandleAsync(value, User, Request));
}
