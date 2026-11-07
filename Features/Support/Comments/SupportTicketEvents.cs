using System.Data;
using System.Text;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Features.Email;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.Support;

internal record SupportEventResult(int Id, int Encolados);

public class SupportTicketEvents(ConnectionDB db, IConfiguration config)
{
    internal async Task<ResultDto<SupportEventResult>> Execute(int ticket, int actor, string kind, string? text, int? state = null, bool isInternal = false)
    {
        if (!SupportDb.TryGetEmpresa(config, out var company, out var error)) return EmailManualRules.Fail<SupportEventResult>(error);
        try
        {
            using var cn = db.GetSisConnection();
            try { await cn.OpenAsync(); } catch { return EmailManualRules.Fail<SupportEventResult>("No se pudo conectar con SIS."); }
            var access = await SupportSecurity.CanViewTicketAsync(db, config, actor, ticket);
            if (!access.IsValid) return EmailManualRules.Fail<SupportEventResult>(access.Message);
            var rights = await SupportSecurity.GetPermissionsAsync(cn, actor, company);
            var required = kind switch { "COMENTARIO" => isInternal ? SupportSecurity.CommentInternal : SupportSecurity.CommentCreate, "ESTADO" => SupportSecurity.TicketStatus, "CIERRE" => SupportSecurity.TicketClose, _ => "" };
            if (required.Length == 0 || !SupportSecurity.HasAny(rights, required)) return SupportSecurity.Forbidden<SupportEventResult>(required);
            if ((kind is "COMENTARIO" or "CIERRE") && (string.IsNullOrWhiteSpace(text) || Encoding.UTF8.GetByteCount(text) > 4000))
                return EmailManualRules.Fail<SupportEventResult>("Escriba un texto de hasta 4000 bytes UTF-8.");
            int requester; int? responsible; string requesterMail; string responsibleMail;
            using (var query = SupportTicketAutomation.Command(cn, "SP_SOP_EVT_GET", company))
            {
                query.Parameters.Add("p_TICKET", OracleDbType.Int32).Value = ticket;
                query.Parameters.Add("p_ROWS", OracleDbType.RefCursor, ParameterDirection.Output);
                using var r = await query.ExecuteReaderAsync();
                if (!await r.ReadAsync()) return EmailManualRules.Fail<SupportEventResult>("Ticket inexistente en la empresa.");
                requester = r.SafeGetInt32("SOLICITANTE"); responsible = SupportDb.SafeGetNullableInt(r, "RESPONSABLE");
                requesterMail = r.SafeGetString("MAIL_SOL"); responsibleMail = r.SafeGetString("MAIL_RESP");
            }
            var responsibleAuthorized = false;
            if (isInternal && responsible.HasValue && responsible != actor && responsible != requester)
            {
                var receiver = await SupportSecurity.GetPermissionsAsync(cn, responsible.Value, company);
                responsibleAuthorized = SupportSecurity.HasAny(receiver, SupportSecurity.CommentInternal);
            }
            var recipients = SupportEventRules.Recipients(actor, requester, responsible, isInternal, responsibleAuthorized);
            foreach (var recipient in recipients)
                if (!EmailManualRules.IsAddress(recipient == requester ? requesterMail : responsibleMail))
                    return EmailManualRules.Fail<SupportEventResult>("Actualice el correo del destinatario en Sistema > Usuarios antes de registrar el cambio.");
            var settings = EmailSettings.Read(config);
            if (recipients.Count > 0 && (!settings.Enabled || settings.Validate() is not null))
                return EmailManualRules.Fail<SupportEventResult>("El envio institucional de correos no esta disponible. No se guardaron cambios.");
            using var tx = cn.BeginTransaction();
            using var cmd = SupportTicketAutomation.Command(cn, "SP_SOP_EVT_EXEC", company); cmd.Transaction = tx;
            foreach (var (name, value) in new (string, int?)[] { ("p_TICKET", ticket), ("p_ACTOR", actor), ("p_INTERNO", isInternal ? 1 : 0), ("p_ESTADO", state), ("p_SOLICITANTE", requester), ("p_RESPONSABLE", responsible), ("p_PRIV_RESP", responsibleAuthorized ? 1 : 0) })
                cmd.Parameters.Add(name, OracleDbType.Int32).Value = SupportDb.DbValue(value);
            foreach (var (name, value) in new[] { ("p_EVENTO", kind), ("p_TEXTO", text), ("p_MAIL_SOL", requesterMail), ("p_MAIL_RESP", responsibleMail), ("p_FROM", settings.FromEmail), ("p_NOMBRE", settings.FromName) })
                cmd.Parameters.Add(name, OracleDbType.Varchar2).Value = SupportDb.StringDbValue(value);
            var id = cmd.Parameters.Add("p_ID", OracleDbType.Int32, ParameterDirection.Output);
            var count = cmd.Parameters.Add("p_ENCOLADOS", OracleDbType.Int32, ParameterDirection.Output);
            await cmd.ExecuteNonQueryAsync(); tx.Commit();
            var result = new SupportEventResult(SupportDb.GetIntOutput(id), SupportDb.GetIntOutput(count));
            return new(result) { IsValid = true, Message = result.Encolados > 0 ? "Cambio registrado. Notificacion por correo pendiente de envio." : "Cambio registrado. No corresponde notificar a otro participante." };
        }
        catch (OracleException ex) when (ex.Number == 20061) { return EmailManualRules.Fail<SupportEventResult>("No se guardaron cambios. Verifique el estatus, los participantes y sus correos y vuelva a intentar."); }
        catch (Exception) { return EmailManualRules.Fail<SupportEventResult>("No se pudo registrar el cambio y su notificacion. No se guardaron cambios."); }
    }
}
