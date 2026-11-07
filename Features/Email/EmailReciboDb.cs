using System.Data;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.Features.Support;

namespace OssmmasoftVerticalSlice.Features.Email;

internal record EmailReciboResult(int QueueId, int Item, string Solicitud, string Estado, string Error, bool Pausa);
internal static class EmailReciboDb
{
    private static OracleCommand Cmd(OracleConnection cn,string operation,int company,OracleTransaction? tx=null)
    {
        var cmd=new OracleCommand("SIS.PKG_EMAIL_RECIBO."+operation,cn){BindByName=true,CommandType=CommandType.StoredProcedure,Transaction=tx};
        cmd.Parameters.Add("p_EMPRESA",OracleDbType.Int32).Value=company;return cmd;
    }
    internal static async Task Enqueue(OracleConnection cn,int company,int actor,int item,string sol,string email,string subject,string body,string file,byte[] pdf,string hash,EmailSettings settings,CancellationToken ct)
    {
        using var tx=cn.BeginTransaction();using var cmd=Cmd(cn,"ENCOLAR",company,tx);
        cmd.Parameters.Add("p_ACTOR",OracleDbType.Int32).Value=actor;cmd.Parameters.Add("p_ITEM",OracleDbType.Int32).Value=item;
        foreach(var (key,value) in new[]{("p_SOL",sol),("p_CORREO",email),("p_ASUNTO",subject),("p_FROM",settings.FromEmail),("p_NOMBRE",settings.FromName),("p_ARCHIVO",file),("p_HASH",hash)}) cmd.Parameters.Add(key,OracleDbType.Varchar2).Value=value;
        cmd.Parameters.Add("p_TEXTO",OracleDbType.Clob).Value=body;cmd.Parameters.Add("p_PDF",OracleDbType.Blob).Value=pdf;
        await cmd.ExecuteNonQueryAsync(ct);tx.Commit();
    }
    internal static async Task<EmailManualDelivery?> Claim(OracleConnection cn,int company,int maxDay,int interval,CancellationToken ct)
    {
        var token=Guid.NewGuid().ToString("D");using var tx=cn.BeginTransaction();using var cmd=Cmd(cn,"TOMAR",company,tx);
        cmd.Parameters.Add("p_TOKEN",OracleDbType.Varchar2).Value=token;cmd.Parameters.Add("p_MAX_DIA",OracleDbType.Int32).Value=maxDay;cmd.Parameters.Add("p_INTERVALO",OracleDbType.Int32).Value=interval;
        cmd.Parameters.Add("p_ROWS",OracleDbType.RefCursor,ParameterDirection.Output);
        EmailManualDelivery? result=null;
        using(var r=await cmd.ExecuteReaderAsync(ct)) if(await r.ReadAsync(ct))
        {
            EmailPdfAttachment? attachment=null;
            if(!r.IsDBNull(r.GetOrdinal("RH_PDF")))
            {
                using var blob=r.GetOracleBlob(r.GetOrdinal("RH_PDF"));
                if(blob.Length>5*1024*1024) throw new InvalidOperationException("Adjunto excede limite.");
                attachment=new(r.SafeGetString("RH_ARCHIVO"),blob.Value,r.SafeGetString("RH_PDF_HASH"));
            }
            result=new(r.SafeGetInt32("EMAIL_ID"),company,token,EmailManualRules.Addresses(r.SafeGetString("PARA_LIST")),EmailManualRules.Addresses(r.SafeGetString("CC_LIST")),EmailManualRules.Addresses(r.SafeGetString("CCO_LIST")),r.SafeGetString("SUBJECT"),r.SafeGetString("BODY_TEXT"),r.SafeGetString("REMITENTE"),r.SafeGetString("REMITENTE_NOMBRE"),r.SafeGetString("MESSAGE_ID"),attachment,r.SafeGetString("MODULO_ORIGEN")=="RH_RECIBO_R53");
        }
        tx.Commit();return result;
    }
    internal static async Task Finish(OracleConnection cn,EmailManualDelivery email,EmailSendOutcome outcome,CancellationToken ct)
    {
        using var tx=cn.BeginTransaction();using var cmd=Cmd(cn,"FINALIZAR",email.Empresa,tx);
        cmd.Parameters.Add("p_ID",OracleDbType.Int32).Value=email.EmailId;cmd.Parameters.Add("p_TOKEN",OracleDbType.Varchar2).Value=email.Token;
        cmd.Parameters.Add("p_ESTADO",OracleDbType.Varchar2).Value=outcome.Estado;cmd.Parameters.Add("p_ERROR",OracleDbType.Varchar2).Value=string.IsNullOrEmpty(outcome.Error)?DBNull.Value:outcome.Error;cmd.Parameters.Add("p_PAUSA",OracleDbType.Int32).Value=outcome.Pausar?1:0;
        await cmd.ExecuteNonQueryAsync(ct);tx.Commit();
    }
    internal static async Task<bool> Paused(OracleConnection cn,int company,CancellationToken ct)
    {
        using var cmd=Cmd(cn,"PAUSA",company);var output=cmd.Parameters.Add("p_VALOR",OracleDbType.Int32,ParameterDirection.Output);await cmd.ExecuteNonQueryAsync(ct);return SupportDb.GetIntOutput(output)==1;
    }
    internal static async Task Resume(OracleConnection cn,int company,CancellationToken ct)
    {
        using var tx=cn.BeginTransaction();using var cmd=Cmd(cn,"REANUDAR",company,tx);await cmd.ExecuteNonQueryAsync(ct);tx.Commit();
    }
    internal static async Task Ack(OracleConnection cn,int company,EmailReciboResult row,CancellationToken ct)
    {
        using var tx=cn.BeginTransaction();using var cmd=Cmd(cn,"ACK",company,tx);
        cmd.Parameters.Add("p_ID",OracleDbType.Int32).Value=row.QueueId;cmd.Parameters.Add("p_SOL",OracleDbType.Varchar2).Value=row.Solicitud;cmd.Parameters.Add("p_ESTADO",OracleDbType.Varchar2).Value=row.Estado;
        await cmd.ExecuteNonQueryAsync(ct);tx.Commit();
    }
    internal static async Task<List<EmailReciboResult>> Results(OracleConnection cn,int company,CancellationToken ct)
    {
        using var cmd=Cmd(cn,"RESULTADOS",company);cmd.Parameters.Add("p_ROWS",OracleDbType.RefCursor,ParameterDirection.Output);
        var result=new List<EmailReciboResult>();using var r=await cmd.ExecuteReaderAsync(ct);
        while(await r.ReadAsync(ct)) result.Add(new(r.SafeGetInt32("EMAIL_ID"),r.SafeGetInt32("REFERENCIA_ID"),r.SafeGetString("RH_SOL"),r.SafeGetString("ESTADO"),r.SafeGetString("ERROR_ENVIO"),r.SafeGetInt32("RH_PAUSA")==1));
        return result;
    }
}
