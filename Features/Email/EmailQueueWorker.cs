using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Features.Support;
using System.Data;
using System.Net;
using System.Net.Mail;

namespace OssmmasoftVerticalSlice.Features.Email;

public record EmailSmtpConfig(
    string SmtpHost,
    int SmtpPort,
    string SmtpUser,
    string SmtpPass,
    bool SmtpSsl,
    string FromEmail,
    string FromName
);

public class EmailQueueWorker(IServiceScopeFactory scopeFactory, IConfiguration config, ILogger<EmailQueueWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var enabled = config.GetValue("settings:EmailWorkerEnabled", true);
        var intervalSeconds = Math.Max(10, config.GetValue("settings:EmailWorkerIntervalSeconds", 60));

        if (!enabled)
        {
            logger.LogInformation("EmailQueueWorker desactivado por configuración.");
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingEmailsAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error general procesando la cola de email.");
            }

            await Task.Delay(TimeSpan.FromSeconds(intervalSeconds), stoppingToken);
        }
    }

    private async Task ProcessPendingEmailsAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var connectionDB = scope.ServiceProvider.GetRequiredService<ConnectionDB>();

        if (!SupportDb.TryGetEmpresa(config, out int empresa, out string errorMessage))
        {
            logger.LogWarning("{Message}", errorMessage);
            return;
        }

        using var cn = connectionDB.GetSisConnection();
        await cn.OpenAsync(cancellationToken);

        var smtpConfig = await GetSmtpConfigAsync(cn, empresa, cancellationToken);
        if (smtpConfig is null)
        {
            logger.LogWarning("No existe configuración SMTP activa para la empresa {Empresa}.", empresa);
            return;
        }

        var pendingEmails = await GetPendingEmailsAsync(cn, empresa, cancellationToken);
        foreach (var email in pendingEmails)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await SendEmailAsync(smtpConfig, email, cancellationToken);
                await MarkSentAsync(cn, empresa, email.EmailId, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error enviando email {EmailId}.", email.EmailId);
                await MarkErrorAsync(cn, empresa, email.EmailId, ex.Message, cancellationToken);
            }
        }
    }

    private static async Task<EmailSmtpConfig?> GetSmtpConfigAsync(OracleConnection cn, int empresa, CancellationToken cancellationToken)
    {
        using var cmd = new OracleCommand("SIS.SP_EMAIL_CFG_GET", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
        var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken) || !SupportDb.IsSuccessMessage(SupportDb.GetMessage(pMessage)))
        {
            return null;
        }

        return new EmailSmtpConfig(
            reader.SafeGetString("SMTP_HOST"),
            reader.SafeGetInt32("SMTP_PORT"),
            reader.SafeGetString("SMTP_USER"),
            reader.SafeGetString("SMTP_PASS"),
            SupportDb.SafeGetFlag(reader, "SMTP_SSL"),
            reader.SafeGetString("FROM_EMAIL"),
            reader.SafeGetString("FROM_NAME")
        );
    }

    private static async Task<List<EmailQueueResponse>> GetPendingEmailsAsync(OracleConnection cn, int empresa, CancellationToken cancellationToken)
    {
        using var cmd = new OracleCommand("SIS.SP_EMAIL_Q_GET_PEND", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
        cmd.Parameters.Add("p_PageSize", OracleDbType.Int32).Value = 25;
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
        var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

        var list = new List<EmailQueueResponse>();
        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            list.Add(new EmailQueueResponse(
                reader.SafeGetInt32("EMAIL_ID"),
                reader.SafeGetString("MODULO_ORIGEN"),
                SupportDb.SafeGetNullableInt(reader, "REFERENCIA_ID"),
                reader.SafeGetString("TO_EMAIL"),
                reader.SafeGetString("TO_NAME"),
                reader.SafeGetString("SUBJECT"),
                reader.SafeGetString("BODY_HTML"),
                reader.SafeGetString("BODY_TEXT"),
                reader.SafeGetString("ESTADO"),
                reader.SafeGetInt32("INTENTOS"),
                reader.SafeGetInt32("MAX_INTENTOS"),
                SupportDb.SafeGetNullableDate(reader, "FECHA_PROGRAMADA"),
                SupportDb.SafeGetNullableDate(reader, "FECHA_ENVIO"),
                reader.SafeGetString("ERROR_ENVIO")
            ));
        }

        return SupportDb.IsSuccessMessage(SupportDb.GetMessage(pMessage)) ? list : [];
    }

    private static async Task SendEmailAsync(EmailSmtpConfig config, EmailQueueResponse email, CancellationToken cancellationToken)
    {
        using var message = new MailMessage
        {
            From = new MailAddress(config.FromEmail, config.FromName),
            Subject = email.Subject,
            Body = string.IsNullOrWhiteSpace(email.BodyHtml) ? email.BodyText : email.BodyHtml,
            IsBodyHtml = !string.IsNullOrWhiteSpace(email.BodyHtml)
        };
        message.To.Add(new MailAddress(email.ToEmail, email.ToName));

        using var client = new SmtpClient(config.SmtpHost, config.SmtpPort)
        {
            EnableSsl = config.SmtpSsl
        };

        if (!string.IsNullOrWhiteSpace(config.SmtpUser))
        {
            client.Credentials = new NetworkCredential(config.SmtpUser, config.SmtpPass);
        }

        await client.SendMailAsync(message, cancellationToken);
    }

    private static Task MarkSentAsync(OracleConnection cn, int empresa, int emailId, CancellationToken cancellationToken)
    {
        return ExecuteEmailStatusAsync(cn, "SIS.SP_EMAIL_Q_SENT", empresa, emailId, cmd =>
        {
            cmd.Parameters.Add("p_MESSAGE_ID", OracleDbType.Varchar2).Value = Guid.NewGuid().ToString("N");
        }, cancellationToken);
    }

    private static Task MarkErrorAsync(OracleConnection cn, int empresa, int emailId, string error, CancellationToken cancellationToken)
    {
        return ExecuteEmailStatusAsync(cn, "SIS.SP_EMAIL_Q_ERROR", empresa, emailId, cmd =>
        {
            cmd.Parameters.Add("p_ERROR_ENVIO", OracleDbType.Varchar2).Value = error.Length > 900 ? error[..900] : error;
        }, cancellationToken);
    }

    private static async Task ExecuteEmailStatusAsync(OracleConnection cn, string procedure, int empresa, int emailId, Action<OracleCommand> bind, CancellationToken cancellationToken)
    {
        using var cmd = new OracleCommand(procedure, cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
        cmd.Parameters.Add("p_EMAIL_ID", OracleDbType.Int32).Value = emailId;
        bind(cmd);
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
        await cmd.ExecuteNonQueryAsync(cancellationToken);
    }
}
