using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

[ApiController]
[Authorize]
[Route("api/CntCatalogos")]
public class CntCatalogosController(
    GetCntCatalogosHandler getAllHandler,
    GetCntPeriodosHandler periodosHandler,
    SearchCntMayoresHandler mayoresHandler,
    SearchCntAuxiliaresHandler auxiliaresHandler) : ControllerBase
{
    [HttpPost("GetAll")]
    public async Task<IActionResult> GetAll(CntCatalogGetAllQuery value) =>
        Ok(await getAllHandler.HandleAsync(value));

    [HttpPost("periodos")]
    public async Task<IActionResult> Periodos(CntPeriodoGetAllQuery value) =>
        Ok(await periodosHandler.HandleAsync(value));

    [HttpPost("mayores")]
    public async Task<IActionResult> Mayores(CntMayorSearchQuery value) =>
        Ok(await mayoresHandler.HandleAsync(value));

    [HttpPost("auxiliares")]
    public async Task<IActionResult> Auxiliares(CntAuxiliarSearchQuery value) =>
        Ok(await auxiliaresHandler.HandleAsync(value));
}
