using Microsoft.AspNetCore.Http;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Security.Claims;

namespace OssmmasoftVerticalSlice.Features.Support;

public class CreateSupportAttachmentHandler(ConnectionDB connectionDB, IConfiguration config, SupportAttachmentHandlerSupport support)
{
    public async Task<ResultDto<int>> HandleAsync(SupportAttachmentCreateCommand value, ClaimsPrincipal user, HttpRequest request)
    {
        var userValidation = await SupportSecurity.ValidateRequestUserAsync(connectionDB, config, user, request, value.UsuarioCargaId);
        if (!userValidation.IsValid)
        {
            return new ResultDto<int>(0) { IsValid = false, Message = userValidation.Message };
        }

        var permission = await support.CheckAttachPermissionAsync(value.UsuarioCargaId);
        if (!permission.IsValid)
        {
            return new ResultDto<int>(0) { IsValid = false, Message = permission.Message };
        }

        if (!SupportDb.TryGetEmpresa(config, out int empresa, out string errorMessage))
        {
            return new ResultDto<int>(0) { IsValid = false, Message = errorMessage };
        }

        var validationMessage = SupportAttachmentHandlerSupport.ValidateAttachment(value);
        return !string.IsNullOrEmpty(validationMessage)
            ? new ResultDto<int>(0) { IsValid = false, Message = validationMessage }
            : await support.InsertAttachmentAsync(value, empresa);
    }
}

public class UploadSupportAttachmentHandler(ConnectionDB connectionDB, IConfiguration config, SupportAttachmentHandlerSupport support)
{
    public async Task<ResultDto<int>> HandleAsync(int ticketId, int usuarioCargaId, IFormFile file, ClaimsPrincipal user, HttpRequest request)
    {
        var userValidation = await SupportSecurity.ValidateRequestUserAsync(connectionDB, config, user, request, usuarioCargaId);
        if (!userValidation.IsValid)
        {
            return new ResultDto<int>(0) { IsValid = false, Message = userValidation.Message };
        }

        var permission = await support.CheckAttachPermissionAsync(usuarioCargaId);
        if (!permission.IsValid)
        {
            return new ResultDto<int>(0) { IsValid = false, Message = permission.Message };
        }

        if (!SupportDb.TryGetEmpresa(config, out int empresa, out string errorMessage))
        {
            return new ResultDto<int>(0) { IsValid = false, Message = errorMessage };
        }

        if (file is null || file.Length == 0)
        {
            return new ResultDto<int>(0) { IsValid = false, Message = "Debe seleccionar un archivo." };
        }

        var originalName = Path.GetFileName(file.FileName);
        var validationMessage = SupportAttachmentHandlerSupport.ValidateAttachment(new SupportAttachmentCreateCommand(
            ticketId,
            originalName,
            null,
            null,
            file.ContentType,
            file.Length,
            usuarioCargaId
        ));

        if (!string.IsNullOrEmpty(validationMessage))
        {
            return new ResultDto<int>(0) { IsValid = false, Message = validationMessage };
        }

        var extension = Path.GetExtension(originalName).ToLowerInvariant();
        var storedName = $"{Guid.NewGuid():N}{extension}";
        var relativePath = Path.Combine(empresa.ToString(), ticketId.ToString(), storedName).Replace(Path.DirectorySeparatorChar, '/');
        var storageRoot = support.GetAttachmentStorageRoot();
        var ticketFolder = Path.Combine(storageRoot, empresa.ToString(), ticketId.ToString());
        Directory.CreateDirectory(ticketFolder);
        var fullPath = Path.Combine(ticketFolder, storedName);

        await using (var stream = File.Create(fullPath))
        {
            await file.CopyToAsync(stream);
        }

        var result = await support.InsertAttachmentAsync(new SupportAttachmentCreateCommand(
            ticketId,
            originalName,
            relativePath,
            relativePath,
            file.ContentType,
            file.Length,
            usuarioCargaId
        ), empresa);

        if (!result.IsValid && File.Exists(fullPath))
        {
            File.Delete(fullPath);
        }

        return result;
    }
}

public class GetSupportAttachmentsByTicketHandler(ConnectionDB connectionDB, IConfiguration config, SupportChildRowsReader childRowsReader)
{
    public async Task<ResultDto<List<SupportAttachmentResponse>>> HandleAsync(
        SupportTicketChildQuery value,
        ClaimsPrincipal user,
        HttpRequest request)
    {
        var userValidation = await SupportSecurity.ValidateRequestUserAsync(connectionDB, config, user, request, value.UsuarioId);
        if (!userValidation.IsValid)
        {
            return new ResultDto<List<SupportAttachmentResponse>>(null!) { IsValid = false, Message = userValidation.Message };
        }

        var ticketAccess = await SupportSecurity.CanViewTicketAsync(connectionDB, config, value.UsuarioId, value.TicketId);
        if (!ticketAccess.IsValid)
        {
            return new ResultDto<List<SupportAttachmentResponse>>(null!) { IsValid = false, Message = ticketAccess.Message };
        }

        return await childRowsReader.ReadAsync("SIS.SP_SOP_ATT_GET_TKT", value.TicketId, reader => new SupportAttachmentResponse(
            reader.SafeGetInt32("ADJUNTO_ID"),
            reader.SafeGetInt32("TICKET_ID"),
            reader.SafeGetString("NOMBRE_ORIGINAL"),
            reader.SafeGetString("IDENTIFICADOR_ARCHIVO"),
            reader.SafeGetString("RUTA_ARCHIVO"),
            reader.SafeGetString("MIME_TYPE"),
            reader.SafeGetInt64("TAMANO_BYTES"),
            reader.SafeGetInt32("USUARIO_CARGA_ID"),
            Convert.ToDateTime(reader.GetValue(reader.GetOrdinal("FECHA_CARGA"))),
            SupportDb.SafeGetFlag(reader, "ACTIVO")
        ));
    }
}
