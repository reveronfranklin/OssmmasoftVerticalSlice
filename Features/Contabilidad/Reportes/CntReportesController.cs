using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

[ApiController]
[Authorize]
[Route("api/CntReportes")]
public class CntReportesController(
    GetCntMayorAnaliticoHandler mayorAnaliticoHandler,
    GetCntMovimientoAuxiliarHandler movimientoAuxiliarHandler) : ControllerBase
{
    [HttpPost("mayorAnalitico")]
    public async Task<IActionResult> MayorAnalitico(CntMayorAnaliticoQuery value) =>
        Ok(await mayorAnaliticoHandler.HandleAsync(value, User, Request));

    [HttpPost("movimientoAuxiliar")]
    public async Task<IActionResult> MovimientoAuxiliar(CntMovimientoAuxiliarQuery value) =>
        Ok(await movimientoAuxiliarHandler.HandleAsync(value, User, Request));
}
