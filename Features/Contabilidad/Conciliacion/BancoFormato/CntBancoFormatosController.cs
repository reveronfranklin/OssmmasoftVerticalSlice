using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

[ApiController]
[Authorize]
[Route("api/CntBancoFormatos")]
public class CntBancoFormatosController(
    GetCntBancoFormatosHandler getAllHandler,
    SaveCntBancoFormatoHandler saveHandler,
    DeleteCntBancoFormatoHandler deleteHandler) : ControllerBase
{
    [HttpPost("GetAll")]
    public async Task<IActionResult> GetAll(CntBancoFormatoGetAllQuery value) =>
        Ok(await getAllHandler.HandleAsync(value, User, Request));

    [HttpPost("Save")]
    public async Task<IActionResult> Save(CntBancoFormatoSaveCommand value) =>
        Ok(await saveHandler.HandleAsync(value, User, Request));

    [HttpPost("Delete")]
    public async Task<IActionResult> Delete(CntBancoFormatoDeleteCommand value) =>
        Ok(await deleteHandler.HandleAsync(value, User, Request));
}

