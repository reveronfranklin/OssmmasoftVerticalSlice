using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

[ApiController]
[Authorize]
[Route("api/CntConciliacionMatching")]
public class CntConciliacionMatchingController(
    MatchCntConciliacionHandler matchHandler,
    MatchMultiCntConciliacionHandler matchMultiHandler,
    GetCntConciliacionSuggestionsHandler suggestionsHandler,
    UnmatchCntConciliacionHandler unmatchHandler) : ControllerBase
{
    [HttpPost("match")]
    public async Task<IActionResult> Match(CntConciliacionMatchCommand value) =>
        Ok(await matchHandler.HandleAsync(value, User, Request));

    [HttpPost("match-multi")]
    public async Task<IActionResult> MatchMulti(CntConciliacionMatchMultiCommand value) =>
        Ok(await matchMultiHandler.HandleAsync(value, User, Request));

    [HttpPost("suggestions")]
    public async Task<IActionResult> Suggestions(CntConciliacionSuggestionGetQuery value) =>
        Ok(await suggestionsHandler.HandleAsync(value, User, Request));

    [HttpPost("unmatch")]
    public async Task<IActionResult> Unmatch(CntConciliacionUnmatchCommand value) =>
        Ok(await unmatchHandler.HandleAsync(value, User, Request));
}
