using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MimeKit;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.Email;

public record EmailManualCommand(string Solicitud, List<string> Para, string Asunto, string Mensaje,
    List<string>? Cc = null, List<string>? Cco = null);
public record EmailManualNormalized(string Solicitud, List<string> Para, List<string> Cc, List<string> Cco,
    string Asunto, string Mensaje, string Huella);
public record EmailManualQuery(string Solicitud);
public record EmailManualHistoryQuery(int Page = 1, int PageSize = 10);
public record EmailManualStatus(int EmailId, string Solicitud, string Asunto, string Estado, int Intentos,
    DateTime FechaSolicitud, DateTime? FechaEnvio, string Error, List<string> Para, List<string> Cc, List<string> Cco);
public record EmailManualAvailability(bool Habilitado, bool Configurado, string Remitente, string Nombre,
    int MaxDestinatarios, int MaxAsuntoBytes, int MaxMensajeCaracteres, string Mensaje);

// Never serialize or log this class: public API uses EmailManualAvailability instead.
public sealed class EmailSettings
{
    public bool Enabled { get; init; }
    public string Host { get; init; } = "";
    public int Port { get; init; }
    public string Security { get; init; } = "";
    public string Username { get; init; } = "";
    public string Password { get; init; } = "";
    public string FromEmail { get; init; } = "";
    public string FromName { get; init; } = "";
    public override string ToString() => "EmailSettings (private)";
    public static EmailSettings Read(IConfiguration config) => new() {
        Enabled = config.GetValue("settings:Email:Enabled", false), Host = config["settings:Email:Host"] ?? "",
        Port = config.GetValue("settings:Email:Port", 0), Security = config["settings:Email:Security"] ?? "",
        Username = config["settings:Email:Username"] ?? "", Password = config["settings:Email:Password"] ?? "",
        FromEmail = config["settings:Email:FromEmail"] ?? "", FromName = config["settings:Email:FromName"] ?? ""
    };
    public string? Validate()
    {
        if (string.IsNullOrWhiteSpace(Host) || Host.Length > 253 || Host.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('.' or '-'))
            || Port is <1 or >65535) return "Servidor SMTP no configurado correctamente.";
        if (Security is not ("SslOnConnect" or "StartTls")) return "Configure un modo SMTP seguro: SslOnConnect o StartTls.";
        if ((Port == 465 && Security != "SslOnConnect") || (Port == 587 && Security != "StartTls")) return "El puerto y el modo TLS configurados no coinciden.";
        if (!EmailManualRules.IsAddress(Username) || !EmailManualRules.IsAddress(FromEmail) || string.IsNullOrEmpty(Password)) return "Faltan credenciales o remitente SMTP.";
        if (Encoding.UTF8.GetByteCount(FromName) > 150 || FromName.Any(char.IsControl)) return "Nombre de remitente no valido.";
        return null;
    }
}

public static class EmailManualRules
{
    public const string Permission = "sistema.correos.enviar";
    public const int MaxRecipients = 20;
    public const int MaxSubjectBytes = 250;
    public const int MaxBody = 10000;
    public static bool IsAddress(string? value) => !string.IsNullOrWhiteSpace(value) && value.Length <= 150 && Encoding.UTF8.GetByteCount(value) <= 150
        && !value.Any(c => char.IsWhiteSpace(c) || char.IsControl(c))
        && MailboxAddress.TryParse(value, out var mailbox) && string.Equals(value, mailbox.Address, StringComparison.OrdinalIgnoreCase)
        && mailbox.Address.Contains('@');
    public static ResultDto<T> Fail<T>(string message) => new(default!) { IsValid = false, Message = message };
    public static List<string> Addresses(string? serialized) => string.IsNullOrEmpty(serialized) ? [] : serialized.Split(';').ToList();
    public static string Join(IEnumerable<string> values) => string.Join(';', values);
    public static bool ValidKey(string? key) => Guid.TryParseExact(key, "D", out var id) && id != Guid.Empty;
    public static ResultDto<EmailManualNormalized> Normalize(EmailManualCommand value)
    {
        if (!ValidKey(value.Solicitud)) return Fail<EmailManualNormalized>("Identificador de solicitud invalido.");
        if (value.Para is null || value.Para.Count == 0) return Fail<EmailManualNormalized>("Indique al menos un destinatario en Para.");
        var all = value.Para.Concat(value.Cc ?? []).Concat(value.Cco ?? []).ToList();
        if (all.Count > MaxRecipients || all.Any(x => x is null || x.Any(char.IsControl) || !IsAddress(x.Trim())))
            return Fail<EmailManualNormalized>($"Revise las direcciones. Maximo {MaxRecipients} destinatarios y 150 caracteres por direccion.");
        if (string.IsNullOrWhiteSpace(value.Asunto) || value.Asunto.Any(char.IsControl) || Encoding.UTF8.GetByteCount(value.Asunto.Trim()) > MaxSubjectBytes)
            return Fail<EmailManualNormalized>("Asunto obligatorio, sin saltos de linea y de hasta 250 bytes UTF-8.");
        if (string.IsNullOrWhiteSpace(value.Mensaje) || value.Mensaje.Length > MaxBody || value.Mensaje.Contains('\0'))
            return Fail<EmailManualNormalized>("Mensaje obligatorio, de hasta 10000 caracteres.");
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        List<string> Clean(IEnumerable<string> list) => list.Select(x => x.Trim()).Where(seen.Add).ToList();
        var para = Clean(value.Para); var cc = Clean(value.Cc ?? []); var cco = Clean(value.Cco ?? []);
        if (new[] { Join(para), Join(cc), Join(cco) }.Any(x => Encoding.UTF8.GetByteCount(x) > 3000))
            return Fail<EmailManualNormalized>("La lista de destinatarios supera el limite permitido.");
        var subject = value.Asunto.Trim();
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { para, cc, cco, subject, value.Mensaje }))));
        return new(new(value.Solicitud.ToLowerInvariant(), para, cc, cco, subject, value.Mensaje, hash)) { IsValid = true, Message = "OK" };
    }
}
