using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

[ApiController]
[Authorize]
[Route("api/CntPeriodos")]
public class CntPeriodosController(
    GetCntPeriodosAdminHandler getAllHandler,
    SaveCntPeriodoHandler saveHandler,
    DeleteCntPeriodoHandler deleteHandler,
    GenerateCntPeriodoYearHandler generateYearHandler) : ControllerBase
{
    [HttpPost("GetAll")]
    public async Task<IActionResult> GetAll(CntPeriodoAdminGetAllQuery value) =>
        Ok(await getAllHandler.HandleAsync(value, User, Request));

    [HttpPost("save")]
    public async Task<IActionResult> Save(CntPeriodoSaveCommand value) =>
        Ok(await saveHandler.HandleAsync(value, User, Request));

    [HttpPost("delete")]
    public async Task<IActionResult> Delete(CntPeriodoDeleteCommand value) =>
        Ok(await deleteHandler.HandleAsync(value, User, Request));

    [HttpPost("generateYear")]
    public async Task<IActionResult> GenerateYear(CntPeriodoGenerateYearCommand value) =>
        Ok(await generateYearHandler.HandleAsync(value, User, Request));
}
