using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OssmmasoftVerticalSlice.Features.Support;

[ApiController]
[Authorize]
[Route("api/SupportTickets")]
public class SupportTicketsController(
    CreateSupportTicketHandler createHandler,
    GetSupportTicketsHandler getAllHandler,
    GetSupportTicketByIdHandler getByIdHandler,
    AssignSupportTicketHandler assignHandler,
    ChangeSupportTicketStatusHandler changeStatusHandler,
    CloseSupportTicketHandler closeHandler) : ControllerBase
{
    [HttpPost("create")]
    public async Task<IActionResult> Create(SupportTicketCreateCommand value) =>
        Ok(await createHandler.HandleAsync(value, User, Request));

    [HttpPost("GetAll")]
    public async Task<IActionResult> GetAll(SupportTicketGetAllQuery value) =>
        Ok(await getAllHandler.HandleAsync(value, User, Request));

    [HttpPost("getById")]
    public async Task<IActionResult> GetById(SupportTicketGetByIdQuery value) =>
        Ok(await getByIdHandler.HandleAsync(value, User, Request));

    [HttpPost("assign")]
    public async Task<IActionResult> Assign(SupportTicketAssignCommand value) =>
        Ok(await assignHandler.HandleAsync(value, User, Request));

    [HttpPost("changeStatus")]
    public async Task<IActionResult> ChangeStatus(SupportTicketStatusCommand value) =>
        Ok(await changeStatusHandler.HandleAsync(value, User, Request));

    [HttpPost("close")]
    public async Task<IActionResult> Close(SupportTicketCloseCommand value) =>
        Ok(await closeHandler.HandleAsync(value, User, Request));
}
