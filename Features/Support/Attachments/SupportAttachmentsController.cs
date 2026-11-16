using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OssmmasoftVerticalSlice.Features.Support;

[ApiController]
[Authorize]
[Route("api/SupportAttachments")]
public class SupportAttachmentsController(
    CreateSupportAttachmentHandler createHandler,
    UploadSupportAttachmentHandler uploadHandler,
    GetSupportAttachmentsByTicketHandler getByTicketHandler,
    SupportAttachmentHandlerSupport support, IConfiguration config) : ControllerBase
{
    [HttpPost("create")]
    public async Task<IActionResult> Create(SupportAttachmentCreateCommand value) =>
        Ok(await createHandler.HandleAsync(value, User, Request));

    [HttpPost("upload")]
    [RequestSizeLimit(SupportAttachmentHandlerSupport.MaxAttachmentBytes)]
    public async Task<IActionResult> Upload([FromForm] int ticketId, [FromForm] int usuarioCargaId, IFormFile file) =>
        Ok(await uploadHandler.HandleAsync(ticketId, usuarioCargaId, file, User, Request));

    [HttpPost("getByTicket")]
    public async Task<IActionResult> GetByTicket(SupportTicketChildQuery value) =>
        Ok(await getByTicketHandler.HandleAsync(value, User, Request));

    [HttpPost("file")]
    public async Task<IActionResult> GetFile(SupportAttachmentFileQuery value)
    {
        try
        {
            var result = await getByTicketHandler.HandleAsync(new SupportTicketChildQuery(value.TicketId, value.UsuarioId), User, Request);
            if (!result.IsValid) return Ok(result);
            var attachment = result.Data?.FirstOrDefault(x => x.AdjuntoId == value.AdjuntoId && x.Activo);
            if (attachment is null) return NotFound(new { message = "Adjunto no disponible." });
            if (!SupportDb.TryGetEmpresa(config, out var company, out var error)) return BadRequest(new { message = error });
            // Only files inside this company's ticket directory can be served.
            var root = Path.GetFullPath(support.GetAttachmentStorageRoot());
            var folder = Path.GetFullPath(Path.Combine(root, company.ToString(), value.TicketId.ToString())) + Path.DirectorySeparatorChar;
            var path = Path.GetFullPath(Path.Combine(root, attachment.RutaArchivo));
            if (!path.StartsWith(folder, StringComparison.Ordinal) || !System.IO.File.Exists(path))
                return NotFound(new { message = "El archivo del adjunto no esta disponible en el servidor." });
            for (var current = new FileInfo(path) as FileSystemInfo; current is not null; current = current is FileInfo file ? file.Directory : ((DirectoryInfo)current).Parent)
            {
                if (current.LinkTarget is not null) return BadRequest(new { message = "Ruta de adjunto no permitida." });
                if (current.FullName == root) break;
            }
            var mime = Path.GetExtension(path).ToLowerInvariant() switch
            {
                ".pdf" => "application/pdf", ".png" => "image/png", ".jpg" or ".jpeg" => "image/jpeg",
                ".txt" => "text/plain", ".doc" => "application/msword",
                ".docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
                ".xls" => "application/vnd.ms-excel",
                ".xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                _ => "application/octet-stream"
            };
            Response.Headers["X-Content-Type-Options"] = "nosniff";
            return File(await System.IO.File.ReadAllBytesAsync(path, HttpContext.RequestAborted), mime, Path.GetFileName(attachment.NombreOriginal));
        }
        catch (Exception)
        {
            return BadRequest(new { message = "No se pudo obtener el archivo adjunto. Intente nuevamente." });
        }
    }
}

public record SupportAttachmentFileQuery(int TicketId, int UsuarioId, int AdjuntoId);
