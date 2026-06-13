using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.Support;

public class SupportAttachmentHandlerSupport(ConnectionDB connectionDB, IConfiguration config)
{
    public const long MaxAttachmentBytes = 10L * 1024L * 1024L;

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase) { ".pdf", ".jpg", ".jpeg", ".png", ".doc", ".docx", ".xls", ".xlsx", ".txt" };
    private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "application/pdf",
        "image/jpeg",
        "image/png",
        "application/msword",
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        "application/vnd.ms-excel",
        "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        "text/plain"
    };

    public static string ValidateAttachment(SupportAttachmentCreateCommand value)
    {
        if (string.IsNullOrWhiteSpace(value.NombreOriginal))
        {
            return "El nombre original del adjunto es requerido.";
        }

        if (value.TamanoBytes <= 0)
        {
            return "El tamaño del adjunto debe ser mayor a cero.";
        }

        if (value.TamanoBytes > MaxAttachmentBytes)
        {
            return "El tamaño maximo permitido para adjuntos es 10 MB.";
        }

        var extension = Path.GetExtension(value.NombreOriginal);
        if (string.IsNullOrWhiteSpace(extension) || !AllowedExtensions.Contains(extension))
        {
            return "La extension del adjunto no esta permitida.";
        }

        if (!string.IsNullOrWhiteSpace(value.MimeType) && !AllowedMimeTypes.Contains(value.MimeType))
        {
            return "El MIME type del adjunto no esta permitido.";
        }

        return string.Empty;
    }

    public async Task<ResultDto<string>> CheckAttachPermissionAsync(int usuarioId)
    {
        if (!SupportDb.TryGetEmpresa(config, out int empresa, out string errorMessage))
        {
            return new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = errorMessage };
        }

        using var cn = connectionDB.GetSisConnection();
        await cn.OpenAsync();
        var permissions = await SupportSecurity.GetPermissionsAsync(cn, usuarioId, empresa);

        return SupportSecurity.HasAny(permissions, SupportSecurity.CommentCreate)
            ? new ResultDto<string>("OK") { IsValid = true, Message = "Success" }
            : SupportSecurity.Forbidden<string>(SupportSecurity.CommentCreate);
    }

    public string GetAttachmentStorageRoot()
    {
        var configuredPath = config["settings:SupportAttachmentsPath"];
        var basePath = string.IsNullOrWhiteSpace(configuredPath)
            ? Path.Combine(AppContext.BaseDirectory, "support-attachments")
            : configuredPath;

        return Path.IsPathRooted(basePath)
            ? basePath
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, basePath));
    }

    public async Task<ResultDto<int>> InsertAttachmentAsync(SupportAttachmentCreateCommand value, int empresa)
    {
        using var cn = connectionDB.GetSisConnection();
        await cn.OpenAsync();
        using var cmd = new OracleCommand("SIS.SP_SOP_ATT_INS", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
        cmd.Parameters.Add("p_TICKET_ID", OracleDbType.Int32).Value = value.TicketId;
        cmd.Parameters.Add("p_NOMBRE_ORIGINAL", OracleDbType.Varchar2).Value = SupportDb.StringDbValue(value.NombreOriginal);
        cmd.Parameters.Add("p_IDENTIFICADOR_ARCHIVO", OracleDbType.Varchar2).Value = SupportDb.StringDbValue(value.IdentificadorArchivo);
        cmd.Parameters.Add("p_RUTA_ARCHIVO", OracleDbType.Varchar2).Value = SupportDb.StringDbValue(value.RutaArchivo);
        cmd.Parameters.Add("p_MIME_TYPE", OracleDbType.Varchar2).Value = SupportDb.StringDbValue(value.MimeType);
        cmd.Parameters.Add("p_TAMANO_BYTES", OracleDbType.Int64).Value = value.TamanoBytes;
        cmd.Parameters.Add("p_USUARIO_CARGA_ID", OracleDbType.Int32).Value = value.UsuarioCargaId;
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
        var pAdjuntoId = cmd.Parameters.Add("p_ADJUNTO_ID_OUT", OracleDbType.Int32, ParameterDirection.Output);
        var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
        await cmd.ExecuteNonQueryAsync();
        var message = SupportDb.GetMessage(pMessage);
        var isSuccess = SupportDb.IsSuccessMessage(message);
        return new ResultDto<int>(isSuccess ? SupportDb.GetIntOutput(pAdjuntoId) : 0) { IsValid = isSuccess, Message = message };
    }
}
