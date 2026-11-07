using System.Data;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.Features.Support;

namespace OssmmasoftVerticalSlice.Features.Email;

public record EmailManualPage(List<EmailManualStatus> Items, int Total, int Page, int PageSize);
internal static class EmailManualDb
{
    private static object Optional(string? value) => string.IsNullOrEmpty(value) ? DBNull.Value : value;
    public static OracleCommand Command(OracleConnection cn, string operation, int empresa)
    {
        var cmd = new OracleCommand("SIS.PKG_EMAIL_MANUAL." + operation, cn) { BindByName = true, CommandType = CommandType.StoredProcedure };
        cmd.Parameters.Add("p_EMPRESA", OracleDbType.Int32).Value = empresa;
        return cmd;
    }
    public static async Task Create(OracleConnection cn, int empresa, int actor, EmailManualNormalized value, EmailSettings settings, CancellationToken ct)
    {
        using var tx = cn.BeginTransaction();
        using var cmd = Command(cn, "CREAR", empresa);
        cmd.Transaction = tx;
        cmd.Parameters.Add("p_ACTOR", OracleDbType.Int32).Value = actor;
        foreach (var (key, text) in new[] { ("p_SOL", value.Solicitud), ("p_HASH",value.Huella), ("p_PARA",EmailManualRules.Join(value.Para)),
            ("p_CC",EmailManualRules.Join(value.Cc)), ("p_CCO",EmailManualRules.Join(value.Cco)), ("p_ASUNTO",value.Asunto),
            ("p_FROM",settings.FromEmail), ("p_NOMBRE",settings.FromName) })
            cmd.Parameters.Add(key, OracleDbType.Varchar2).Value = Optional(text);
        cmd.Parameters.Add("p_TEXTO", OracleDbType.Clob).Value = value.Mensaje;
        cmd.Parameters.Add("p_ID", OracleDbType.Int32, ParameterDirection.Output);
        await cmd.ExecuteNonQueryAsync(ct);
        tx.Commit();
    }
    public static async Task<EmailManualPage> List(OracleConnection cn, int empresa, int actor, string? solicitud, int page, int size, CancellationToken ct)
    {
        using var cmd = Command(cn, "LISTAR", empresa);
        cmd.Parameters.Add("p_ACTOR", OracleDbType.Int32).Value = actor;
        cmd.Parameters.Add("p_SOL", OracleDbType.Varchar2).Value = Optional(solicitud);
        cmd.Parameters.Add("p_PAGE", OracleDbType.Int32).Value = page;
        cmd.Parameters.Add("p_SIZE", OracleDbType.Int32).Value = size;
        cmd.Parameters.Add("p_ROWS", OracleDbType.RefCursor, ParameterDirection.Output);
        var total = cmd.Parameters.Add("p_TOTAL", OracleDbType.Int32, ParameterDirection.Output);
        var items = new List<EmailManualStatus>();
        using (var reader = await cmd.ExecuteReaderAsync(ct))
            while (await reader.ReadAsync(ct))
                items.Add(new(reader.SafeGetInt32("EMAIL_ID"), reader.SafeGetString("SOLICITUD"), reader.SafeGetString("SUBJECT"),
                    reader.SafeGetString("ESTADO"), reader.SafeGetInt32("INTENTOS"), reader.GetDateTime(reader.GetOrdinal("CREATED_AT")),
                    SupportDb.SafeGetNullableDate(reader,"FECHA_ENVIO"), reader.SafeGetString("ERROR_ENVIO"),
                    EmailManualRules.Addresses(reader.SafeGetString("PARA_LIST")), EmailManualRules.Addresses(reader.SafeGetString("CC_LIST")),
                    EmailManualRules.Addresses(reader.SafeGetString("CCO_LIST"))));
        return new(items, SupportDb.GetIntOutput(total), page, size);
    }
    public static async Task<EmailManualDelivery?> Claim(OracleConnection cn, int empresa, CancellationToken ct)
    {
        var token = Guid.NewGuid().ToString("D");
        using var tx = cn.BeginTransaction();
        using var cmd = Command(cn, "TOMAR", empresa); cmd.Transaction = tx;
        cmd.Parameters.Add("p_TOKEN", OracleDbType.Varchar2).Value = token;
        cmd.Parameters.Add("p_ROWS", OracleDbType.RefCursor, ParameterDirection.Output);
        EmailManualDelivery? result = null;
        using (var reader = await cmd.ExecuteReaderAsync(ct))
            if (await reader.ReadAsync(ct)) result = new(reader.SafeGetInt32("EMAIL_ID"), empresa, token,
                EmailManualRules.Addresses(reader.SafeGetString("PARA_LIST")), EmailManualRules.Addresses(reader.SafeGetString("CC_LIST")),
                EmailManualRules.Addresses(reader.SafeGetString("CCO_LIST")), reader.SafeGetString("SUBJECT"), reader.SafeGetString("BODY_TEXT"),
                reader.SafeGetString("REMITENTE"),reader.SafeGetString("REMITENTE_NOMBRE"),reader.SafeGetString("MESSAGE_ID"));
        tx.Commit();
        return result;
    }
    public static async Task Finish(OracleConnection cn, EmailManualDelivery email, EmailSendOutcome outcome, CancellationToken ct)
    {
        using var tx = cn.BeginTransaction();
        using var cmd = Command(cn,"FINALIZAR",email.Empresa); cmd.Transaction = tx;
        cmd.Parameters.Add("p_ID",OracleDbType.Int32).Value = email.EmailId;
        cmd.Parameters.Add("p_TOKEN",OracleDbType.Varchar2).Value = email.Token;
        cmd.Parameters.Add("p_ESTADO",OracleDbType.Varchar2).Value = outcome.Estado;
        cmd.Parameters.Add("p_ERROR",OracleDbType.Varchar2).Value = Optional(outcome.Error);
        await cmd.ExecuteNonQueryAsync(ct); tx.Commit();
    }
}
