using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace OssmmasoftVerticalSlice.Features.Email;

public record EmailManualDelivery(int EmailId, int Empresa, string Token, List<string> Para, List<string> Cc,
    List<string> Cco, string Subject, string Body, string FromEmail, string FromName, string MessageId);
public record EmailSendOutcome(string Estado, string Error);
public interface IEmailManualSender
{
    Task<EmailSendOutcome> SendAsync(EmailSettings settings, EmailManualDelivery email, CancellationToken ct);
}
public sealed class EmailSmtpSender : IEmailManualSender
{
    public static FormatOptions MessageFormat()
    {
        var options = FormatOptions.Default.Clone();
        options.HiddenHeaders.Add(HeaderId.Bcc);
        return options;
    }
    public static MimeMessage BuildMessage(EmailManualDelivery email)
    {
        var message = new MimeMessage { Subject = email.Subject, MessageId = email.MessageId, Date = DateTimeOffset.UtcNow };
        message.From.Add(new MailboxAddress(email.FromName, email.FromEmail));
        foreach (var to in email.Para) message.To.Add(new MailboxAddress("", to));
        foreach (var cc in email.Cc) message.Cc.Add(new MailboxAddress("", cc));
        foreach (var bcc in email.Cco) message.Bcc.Add(new MailboxAddress("", bcc));
        message.Body = new TextPart("plain") { Text = email.Body };
        return message;
    }
    public async Task<EmailSendOutcome> SendAsync(EmailSettings settings, EmailManualDelivery email, CancellationToken ct)
    {
        if (!settings.Enabled) return new("ERROR", "Envio deshabilitado. No se contacto SMTP.");
        if (settings.Validate() is not null) return new("ERROR", "Configuracion SMTP no valida. No se contacto SMTP.");
        if (settings.FromEmail != email.FromEmail || settings.FromName != email.FromName)
            return new("ERROR", "El remitente configurado cambio desde que se registro la solicitud. Revise antes de crear otra.");
        var attempted = false;
        using var message = BuildMessage(email);
        using var client = new SmtpClient { Timeout = 60000 };
        try
        {
            var mode = settings.Security == "SslOnConnect" ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTls;
            // Default certificate validation stays enabled. No insecure fallback.
            await client.ConnectAsync(settings.Host, settings.Port, mode, ct);
            await client.AuthenticateAsync(settings.Username, settings.Password, ct);
            attempted = true;
            await client.SendAsync(MessageFormat(), message, ct);
            return new("ACEPTADO_SMTP", "");
        }
        catch (Exception)
        {
            // Once SendAsync starts, a lost response can hide successful acceptance.
            // Never automatically repeat an ambiguous submission.
            return attempted ? new("INCIERTO", "No se pudo confirmar la aceptacion SMTP. No reenviar sin revisar con el proveedor.")
                : new("ERROR", "No se pudo conectar o autenticar con SMTP. No se envio el mensaje.");
        }
        finally
        {
            if (client.IsConnected)
                try { await client.DisconnectAsync(true, CancellationToken.None); } catch { /* Acceptance is already recorded in the outcome. */ }
        }
    }
}
