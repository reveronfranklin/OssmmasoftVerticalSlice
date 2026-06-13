using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

[ApiController]
[Authorize]
[Route("api/CntRubros")]
public class CntRubrosController(
    GetCntRubrosHandler getAllHandler,
    SaveCntRubroHandler saveHandler,
    DeleteCntRubroHandler deleteHandler) : ControllerBase
{
    [HttpPost("GetAll")]
    public async Task<IActionResult> GetAll(CntRubroGetAllQuery value) =>
        Ok(await getAllHandler.HandleAsync(value, User, Request));

    [HttpPost("save")]
    public async Task<IActionResult> Save(CntRubroSaveCommand value) =>
        Ok(await saveHandler.HandleAsync(value, User, Request));

    [HttpPost("delete")]
    public async Task<IActionResult> Delete(CntRubroDeleteCommand value) =>
        Ok(await deleteHandler.HandleAsync(value, User, Request));
}
