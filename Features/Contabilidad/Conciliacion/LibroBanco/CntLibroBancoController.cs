using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

[ApiController]
[Authorize]
[Route("api/CntLibroBanco")]
public class CntLibroBancoController(
    GetCntLibrosBancoHandler getAllHandler,
    GetCntLibroBancoDetallesHandler getDetailsHandler,
    GenerateCntLibroBancoHandler generateHandler) : ControllerBase
{
    [HttpPost("GetAll")]
    public async Task<IActionResult> GetAll(CntLibroBancoGetQuery value) =>
        Ok(await getAllHandler.HandleAsync(value, User, Request));

    [HttpPost("Details")]
    public async Task<IActionResult> Details(CntLibroBancoDetalleGetQuery value) =>
        Ok(await getDetailsHandler.HandleAsync(value, User, Request));

    [HttpPost("Generate")]
    public async Task<IActionResult> Generate(CntLibroBancoGenerateCommand value) =>
        Ok(await generateHandler.HandleAsync(value, User, Request));
}
