using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

[ApiController]
[Authorize]
[Route("api/CntRelacionDocumentos")]
public class CntRelacionDocumentosController(
    GetCntRelacionDocumentosHandler getAllHandler,
    SaveCntRelacionDocumentoHandler saveHandler,
    DeleteCntRelacionDocumentoHandler deleteHandler) : ControllerBase
{
    [HttpPost("GetAll")]
    public async Task<IActionResult> GetAll(CntRelacionDocumentoGetAllQuery value) =>
        Ok(await getAllHandler.HandleAsync(value, User, Request));

    [HttpPost("save")]
    public async Task<IActionResult> Save(CntRelacionDocumentoSaveCommand value) =>
        Ok(await saveHandler.HandleAsync(value, User, Request));

    [HttpPost("delete")]
    public async Task<IActionResult> Delete(CntRelacionDocumentoDeleteCommand value) =>
        Ok(await deleteHandler.HandleAsync(value, User, Request));
}
