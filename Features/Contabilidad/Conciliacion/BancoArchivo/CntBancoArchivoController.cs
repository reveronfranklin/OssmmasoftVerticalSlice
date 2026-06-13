using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

[ApiController]
[Authorize]
[Route("api/CntBancoArchivo")]
public class CntBancoArchivoController(
    GetCntBancoArchivosHandler getAllHandler,
    CreateCntBancoArchivoControlHandler createControlHandler,
    CreateCntBancoArchivoDetalleHandler createDetalleHandler,
    GetCntBancoArchivoDetallesHandler getDetallesHandler,
    GetCntBancoArchivoPreviewHandler getPreviewHandler,
    GetCntBancoArchivoTraceHandler getTraceHandler,
    ExtractCntBancoArchivoHandler extractHandler,
    CreateCntBancoArchivoBatchHandler createBatchHandler,
    ConfirmCntBancoArchivoHandler confirmHandler) : ControllerBase
{
    [HttpPost("GetAll")]
    public async Task<IActionResult> GetAll(CntBancoArchivoGetQuery value) =>
        Ok(await getAllHandler.HandleAsync(value, User, Request));

    [HttpPost("CreateControl")]
    public async Task<IActionResult> CreateControl(CntBancoArchivoControlCreateCommand value) =>
        Ok(await createControlHandler.HandleAsync(value, User, Request));

    [HttpPost("CreateDetail")]
    public async Task<IActionResult> CreateDetail(CntBancoArchivoDetalleCreateCommand value) =>
        Ok(await createDetalleHandler.HandleAsync(value, User, Request));

    [HttpPost("Details")]
    public async Task<IActionResult> Details(CntBancoArchivoDetalleGetQuery value) =>
        Ok(await getDetallesHandler.HandleAsync(value, User, Request));

    [HttpPost("Preview")]
    public async Task<IActionResult> Preview(CntBancoArchivoDetalleGetQuery value) =>
        Ok(await getPreviewHandler.HandleAsync(value, User, Request));

    [HttpPost("Trace")]
    public async Task<IActionResult> Trace(CntBancoArchivoTraceGetQuery value) =>
        Ok(await getTraceHandler.HandleAsync(value, User, Request));

    [HttpPost("Extract")]
    public async Task<IActionResult> Extract(CntBancoArchivoExtractCommand value) =>
        Ok(await extractHandler.HandleAsync(value, User, Request));

    [HttpPost("CreateBatch")]
    public async Task<IActionResult> CreateBatch(CntBancoArchivoBatchCreateCommand value) =>
        Ok(await createBatchHandler.HandleAsync(value, User, Request));

    [HttpPost("Confirm")]
    public async Task<IActionResult> Confirm(CntBancoArchivoConfirmCommand value) =>
        Ok(await confirmHandler.HandleAsync(value, User, Request));
}
