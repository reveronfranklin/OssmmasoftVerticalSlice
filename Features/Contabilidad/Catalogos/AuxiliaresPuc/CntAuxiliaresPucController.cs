using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

[ApiController]
[Authorize]
[Route("api/CntAuxiliaresPuc")]
public class CntAuxiliaresPucController(
    GetCntAuxiliaresPucHandler getAllHandler,
    SaveCntAuxiliarPucHandler saveHandler,
    DeleteCntAuxiliarPucHandler deleteHandler) : ControllerBase
{
    [HttpPost("GetAll")]
    public async Task<IActionResult> GetAll(CntAuxiliarPucGetAllQuery value) =>
        Ok(await getAllHandler.HandleAsync(value, User, Request));

    [HttpPost("save")]
    public async Task<IActionResult> Save(CntAuxiliarPucSaveCommand value) =>
        Ok(await saveHandler.HandleAsync(value, User, Request));

    [HttpPost("delete")]
    public async Task<IActionResult> Delete(CntAuxiliarPucDeleteCommand value) =>
        Ok(await deleteHandler.HandleAsync(value, User, Request));
}
