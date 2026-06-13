using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

[ApiController]
[Authorize]
[Route("api/CntBalances")]
public class CntBalancesController(
    GetCntBalancesHandler getAllHandler,
    SaveCntBalanceHandler saveHandler,
    DeleteCntBalanceHandler deleteHandler) : ControllerBase
{
    [HttpPost("GetAll")]
    public async Task<IActionResult> GetAll(CntBalanceGetAllQuery value) =>
        Ok(await getAllHandler.HandleAsync(value, User, Request));

    [HttpPost("save")]
    public async Task<IActionResult> Save(CntBalanceSaveCommand value) =>
        Ok(await saveHandler.HandleAsync(value, User, Request));

    [HttpPost("delete")]
    public async Task<IActionResult> Delete(CntBalanceDeleteCommand value) =>
        Ok(await deleteHandler.HandleAsync(value, User, Request));
}
