using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

[ApiController]
[Authorize]
[Route("api/CntConciliacionDetalle")]
public class CntConciliacionDetalleController(
    GetCntConciliacionBancoMovimientosHandler getBancoHandler,
    GetCntConciliacionLibroMovimientosHandler getLibroHandler,
    GetCntConciliacionTemporalesHandler getTemporalesHandler) : ControllerBase
{
    [HttpPost("banco")]
    public async Task<IActionResult> Banco(CntConciliacionDetalleGetQuery value) =>
        Ok(await getBancoHandler.HandleAsync(value, User, Request));

    [HttpPost("libro")]
    public async Task<IActionResult> Libro(CntConciliacionDetalleGetQuery value) =>
        Ok(await getLibroHandler.HandleAsync(value, User, Request));

    [HttpPost("temporales")]
    public async Task<IActionResult> Temporales(CntConciliacionTemporalGetQuery value) =>
        Ok(await getTemporalesHandler.HandleAsync(value, User, Request));
}
