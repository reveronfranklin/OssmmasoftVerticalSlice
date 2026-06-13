using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OssmmasoftVerticalSlice.Features.Support;

[ApiController]
[Authorize]
[Route("api/SupportDashboard")]
public class SupportDashboardController(SupportDashboardSummaryHandler summaryHandler) : ControllerBase
{
    [HttpPost("summary")]
    public async Task<IActionResult> Summary(SupportDashboardSummaryQuery value) =>
        Ok(await summaryHandler.HandleAsync(value, User, Request));
}
