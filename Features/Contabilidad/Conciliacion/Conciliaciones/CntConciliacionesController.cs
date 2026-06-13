using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

[ApiController]
[Authorize]
[Route("api/CntConciliaciones")]
public class CntConciliacionesController(
    GetCntConciliacionesHandler getAllHandler,
    GetCntConciliacionByIdHandler getByIdHandler,
    CreateCntConciliacionHandler createHandler,
    PrecloseCntConciliacionHandler precloseHandler,
    CloseCntConciliacionHandler closeHandler,
    ReverseCntConciliacionHandler reverseHandler) : ControllerBase
{
    [HttpPost("GetAll")]
    public async Task<IActionResult> GetAll(CntConciliacionGetAllQuery value) =>
        Ok(await getAllHandler.HandleAsync(value, User, Request));

    [HttpPost("getById")]
    public async Task<IActionResult> GetById(CntConciliacionGetByIdQuery value) =>
        Ok(await getByIdHandler.HandleAsync(value, User, Request));

    [HttpPost("Create")]
    public async Task<IActionResult> Create(CntConciliacionCreateCommand value) =>
        Ok(await createHandler.HandleAsync(value, User, Request));

    [HttpPost("Preclose")]
    public async Task<IActionResult> Preclose(CntConciliacionPrecloseCommand value) =>
        Ok(await precloseHandler.HandleAsync(value, User, Request));

    [HttpPost("Close")]
    public async Task<IActionResult> Close(CntConciliacionCloseCommand value) =>
        Ok(await closeHandler.HandleAsync(value, User, Request));

    [HttpPost("Reverse")]
    public async Task<IActionResult> Reverse(CntConciliacionReverseCommand value) =>
        Ok(await reverseHandler.HandleAsync(value, User, Request));
}
