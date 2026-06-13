using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

[ApiController]
[Authorize]
[Route("api/CntAuxiliares")]
public class CntAuxiliaresController(
    GetCntAuxiliaresHandler getAllHandler,
    SaveCntAuxiliarHandler saveHandler,
    DeleteCntAuxiliarHandler deleteHandler,
    GetCntAuxiliarUsedByHandler usedByHandler) : ControllerBase
{
    [HttpPost("GetAll")]
    public async Task<IActionResult> GetAll(CntAuxiliarGetAllQuery value) =>
        Ok(await getAllHandler.HandleAsync(value, User, Request));

    [HttpPost("save")]
    public async Task<IActionResult> Save(CntAuxiliarSaveCommand value) =>
        Ok(await saveHandler.HandleAsync(value, User, Request));

    [HttpPost("delete")]
    public async Task<IActionResult> Delete(CntAuxiliarDeleteCommand value) =>
        Ok(await deleteHandler.HandleAsync(value, User, Request));

    [HttpPost("usedBy")]
    public async Task<IActionResult> UsedBy(CntAuxiliarUsedByQuery value) =>
        Ok(await usedByHandler.HandleAsync(value, User, Request));
}
