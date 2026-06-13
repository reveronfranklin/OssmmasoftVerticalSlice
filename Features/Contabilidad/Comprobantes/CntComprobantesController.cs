using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

[ApiController]
[Authorize]
[Route("api/CntComprobantes")]
public class CntComprobantesController(
    GetCntComprobantesHandler getAllHandler,
    GetCntComprobanteByIdHandler getByIdHandler,
    GetCntComprobantePrintHandler printHandler,
    GenerateCntComprobanteNumberHandler numberHandler,
    GetCntDetallesByComprobanteHandler getDetailsHandler,
    CreateCntComprobanteHandler createHandler,
    UpdateCntComprobanteHandler updateHandler,
    DeleteCntComprobanteHandler deleteHandler,
    ReorderCntComprobantesHandler reorderHandler,
    AddCntDetalleHandler addDetailHandler,
    UpdateCntDetalleHandler updateDetailHandler,
    DeleteCntDetalleHandler deleteDetailHandler) : ControllerBase
{
    [HttpPost("GetAll")]
    public async Task<IActionResult> GetAll(CntComprobanteGetAllQuery value) =>
        Ok(await getAllHandler.HandleAsync(value, User, Request));

    [HttpPost("getById")]
    public async Task<IActionResult> GetById(CntComprobanteGetByIdQuery value) =>
        Ok(await getByIdHandler.HandleAsync(value, User, Request));

    [HttpPost("print")]
    public async Task<IActionResult> Print(CntComprobantePrintQuery value) =>
        Ok(await printHandler.HandleAsync(value, User, Request));

    [HttpPost("generateNumber")]
    public async Task<IActionResult> GenerateNumber(CntComprobanteNumberQuery value) =>
        Ok(await numberHandler.HandleAsync(value, User, Request));

    [HttpPost("getDetails")]
    public async Task<IActionResult> GetDetails(CntDetalleGetByComprobanteQuery value) =>
        Ok(await getDetailsHandler.HandleAsync(value, User, Request));

    [HttpPost("create")]
    public async Task<IActionResult> Create(CntComprobanteCreateCommand value) =>
        Ok(await createHandler.HandleAsync(value, User, Request));

    [HttpPost("update")]
    public async Task<IActionResult> Update(CntComprobanteUpdateCommand value) =>
        Ok(await updateHandler.HandleAsync(value, User, Request));

    [HttpPost("delete")]
    public async Task<IActionResult> Delete(CntComprobanteDeleteCommand value) =>
        Ok(await deleteHandler.HandleAsync(value, User, Request));

    [HttpPost("reorderNumbers")]
    public async Task<IActionResult> ReorderNumbers(CntComprobanteReorderCommand value) =>
        Ok(await reorderHandler.HandleAsync(value, User, Request));

    [HttpPost("addDetail")]
    public async Task<IActionResult> AddDetail(CntDetalleAddCommand value) =>
        Ok(await addDetailHandler.HandleAsync(value, User, Request));

    [HttpPost("updateDetail")]
    public async Task<IActionResult> UpdateDetail(CntDetalleUpdateCommand value) =>
        Ok(await updateDetailHandler.HandleAsync(value, User, Request));

    [HttpPost("deleteDetail")]
    public async Task<IActionResult> DeleteDetail(CntDetalleDeleteCommand value) =>
        Ok(await deleteDetailHandler.HandleAsync(value, User, Request));
}
