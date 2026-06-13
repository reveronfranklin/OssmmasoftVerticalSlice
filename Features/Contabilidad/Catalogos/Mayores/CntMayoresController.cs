using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

[ApiController]
[Authorize]
[Route("api/CntMayores")]
public class CntMayoresController(
    GetCntMayoresHandler getAllHandler,
    SaveCntMayorHandler saveHandler,
    DeleteCntMayorHandler deleteHandler,
    GetCntMayorUsedByHandler usedByHandler) : ControllerBase
{
    [HttpPost("GetAll")]
    public async Task<IActionResult> GetAll(CntMayorGetAllQuery value) =>
        Ok(await getAllHandler.HandleAsync(value, User, Request));

    [HttpPost("save")]
    public async Task<IActionResult> Save(CntMayorSaveCommand value) =>
        Ok(await saveHandler.HandleAsync(value, User, Request));

    [HttpPost("delete")]
    public async Task<IActionResult> Delete(CntMayorDeleteCommand value) =>
        Ok(await deleteHandler.HandleAsync(value, User, Request));

    [HttpPost("usedBy")]
    public async Task<IActionResult> UsedBy(CntMayorUsedByQuery value) =>
        Ok(await usedByHandler.HandleAsync(value, User, Request));
}
