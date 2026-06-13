using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

[ApiController]
[Authorize]
[Route("api/CntAutomaticos")]
public class CntAutomaticosController(
    PreviewCntAutomaticoHandler previewHandler,
    ConfirmCntAutomaticoHandler confirmHandler) : ControllerBase
{
    [HttpPost("preview")]
    public async Task<IActionResult> Preview(CntAutomaticPreviewCommand value) =>
        Ok(await previewHandler.HandleAsync(value, User, Request));

    [HttpPost("confirm")]
    public async Task<IActionResult> Confirm(CntAutomaticConfirmCommand value) =>
        Ok(await confirmHandler.HandleAsync(value, User, Request));
}
