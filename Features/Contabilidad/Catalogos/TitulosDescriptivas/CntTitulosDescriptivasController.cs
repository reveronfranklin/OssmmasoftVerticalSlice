using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

[ApiController]
[Authorize]
[Route("api/CntCatalogos")]
public class CntTitulosDescriptivasController(
    GetCntTitulosHandler titulosHandler,
    SaveCntTituloHandler saveTituloHandler,
    DeleteCntTituloHandler deleteTituloHandler,
    GetCntDescriptivasHandler descriptivasHandler,
    SaveCntDescriptivaHandler saveDescriptivaHandler,
    DeleteCntDescriptivaHandler deleteDescriptivaHandler,
    GetCntDescriptivaUsedByHandler descriptivaUsedByHandler) : ControllerBase
{
    [HttpPost("titulos")]
    public async Task<IActionResult> Titulos(CntTituloGetAllQuery value) =>
        Ok(await titulosHandler.HandleAsync(value, User, Request));

    [HttpPost("titulos/save")]
    public async Task<IActionResult> SaveTitulo(CntTituloSaveCommand value) =>
        Ok(await saveTituloHandler.HandleAsync(value, User, Request));

    [HttpPost("titulos/delete")]
    public async Task<IActionResult> DeleteTitulo(CntTituloDeleteCommand value) =>
        Ok(await deleteTituloHandler.HandleAsync(value, User, Request));

    [HttpPost("descriptivas")]
    public async Task<IActionResult> Descriptivas(CntDescriptivaGetAllQuery value) =>
        Ok(await descriptivasHandler.HandleAsync(value, User, Request));

    [HttpPost("descriptivas/save")]
    public async Task<IActionResult> SaveDescriptiva(CntDescriptivaSaveCommand value) =>
        Ok(await saveDescriptivaHandler.HandleAsync(value, User, Request));

    [HttpPost("descriptivas/delete")]
    public async Task<IActionResult> DeleteDescriptiva(CntDescriptivaDeleteCommand value) =>
        Ok(await deleteDescriptivaHandler.HandleAsync(value, User, Request));

    [HttpPost("descriptivas/usedBy")]
    public async Task<IActionResult> DescriptivaUsedBy(CntDescriptivaUsedByQuery value) =>
        Ok(await descriptivaUsedByHandler.HandleAsync(value, User, Request));
}
