using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

[ApiController]
[Authorize]
[Route("api/CntCierreContable")]
public class CntCierreContableController(
    GetCntCierrePeriodosHandler getPeriodosHandler,
    GetCntCierreModificacionesHandler modificacionesHandler,
    PrecierreCntContableHandler precierreHandler,
    CierreCntContableHandler cierreHandler,
    ReversoCntContableHandler reversoHandler) : ControllerBase
{
    [HttpPost("GetPeriodos")]
    public async Task<IActionResult> GetPeriodos(CntCierrePeriodoGetQuery value) =>
        Ok(await getPeriodosHandler.HandleAsync(value, User, Request));

    [HttpPost("Modificaciones")]
    public async Task<IActionResult> Modificaciones(CntCierreModificacionesQuery value) =>
        Ok(await modificacionesHandler.HandleAsync(value, User, Request));

    [HttpPost("Precierre")]
    public async Task<IActionResult> Precierre(CntCierreActionCommand value) =>
        Ok(await precierreHandler.HandleAsync(value, User, Request));

    [HttpPost("Cierre")]
    public async Task<IActionResult> Cierre(CntCierreActionCommand value) =>
        Ok(await cierreHandler.HandleAsync(value, User, Request));

    [HttpPost("Reverso")]
    public async Task<IActionResult> Reverso(CntCierreActionCommand value) =>
        Ok(await reversoHandler.HandleAsync(value, User, Request));
}
