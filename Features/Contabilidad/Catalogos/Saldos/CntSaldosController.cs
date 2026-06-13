using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

[ApiController]
[Authorize]
[Route("api/CntSaldos")]
public class CntSaldosController(
    GetCntSaldosHandler getAllHandler,
    SaveCntSaldoHandler saveHandler,
    DeleteCntSaldoHandler deleteHandler) : ControllerBase
{
    [HttpPost("GetAll")]
    public async Task<IActionResult> GetAll(CntSaldoGetAllQuery value) =>
        Ok(await getAllHandler.HandleAsync(value, User, Request));

    [HttpPost("save")]
    public async Task<IActionResult> Save(CntSaldoSaveCommand value) =>
        Ok(await saveHandler.HandleAsync(value, User, Request));

    [HttpPost("delete")]
    public async Task<IActionResult> Delete(CntSaldoDeleteCommand value) =>
        Ok(await deleteHandler.HandleAsync(value, User, Request));
}
