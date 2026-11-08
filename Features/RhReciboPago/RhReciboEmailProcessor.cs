using System.Data;
using System.Text.Json;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Features.Email;
using OssmmasoftVerticalSlice.Features.Support;
using static OssmmasoftVerticalSlice.Features.RhReciboPago.RrmDb;

namespace OssmmasoftVerticalSlice.Features.RhReciboPago;

internal static class RhReciboEmailProcessor
{
    internal static async Task Tick(ConnectionDB db,IConfiguration config,IWebHostEnvironment env,int company,EmailSettings settings,CancellationToken ct)
    {
        using var rh=db.GetRhConnection();using var sis=db.GetSisConnection();await rh.OpenAsync(ct);await sis.OpenAsync(ct);
        foreach(var result in await EmailReciboDb.Results(sis,company,ct))
        {
            int applied;
            using(var tx=rh.BeginTransaction())
            {
                using var cmd=Cmd(rh,"RESULTADO",tx,N("p_EMPRESA",company),N("p_ID",result.Item),S("p_SOL",result.Solicitud),S("p_ESTADO",result.Estado),S("p_ERROR",result.Error),N("p_PAUSA",result.Pausa?1:0));
                var output=cmd.Parameters.Add("p_APLICADO",OracleDbType.Int32,ParameterDirection.Output);await cmd.ExecuteNonQueryAsync(ct);applied=SupportDb.GetIntOutput(output);tx.Commit();
            }
            if(applied==1) await EmailReciboDb.Ack(sis,company,result,ct);
        }
        if(await EmailReciboDb.Paused(sis,company,ct))
        {
            using var pausedTx=rh.BeginTransaction();await Run(rh,"PAUSAR",pausedTx,ct,N("p_EMPRESA",company));pausedTx.Commit();return;
        }
        var token=Guid.NewGuid().ToString("D");RrmItem? item=null;int nomina=0,periodo=0;string lotId="";
        using(var tx=rh.BeginTransaction())
        {
            using var cmd=Cmd(rh,"TOMAR",tx,N("p_EMPRESA",company),S("p_TOKEN",token));cmd.Parameters.Add("p_ROWS",OracleDbType.RefCursor,ParameterDirection.Output);
            using(var reader=await cmd.ExecuteReaderAsync(ct)) if(await reader.ReadAsync(ct)) {item=Item(reader);nomina=reader.SafeGetInt32("NOMINA");periodo=reader.SafeGetInt32("PERIODO");lotId=reader.SafeGetString("LOTE_ID");}
            if(item is not null)
            {
                OracleCommand Part() => Cmd(rh,"LOB_PARTE",tx,N("p_EMPRESA",company),N("p_ID",item.Id),S("p_TOKEN",token));
                item=item with { Fuente=await EmailLobParts.Text(Part,ct),Pdf=await EmailLobParts.Pdf(Part,ct) };
            }
            tx.Commit();
        }
        if(item is null) return;
        ReciboEmailFuente? source;
        try { source=JsonSerializer.Deserialize<ReciboEmailFuente>(item.Fuente); }
        catch { await Error("La version congelada del recibo no es valida.");return; }
        if(source is null || RhReciboEmailRules.Hash(item.Fuente)!=item.Huella || !RhReciboEmailRules.ValidSource(source,company,item.Persona,nomina,periodo))
        {await Error("La identidad o version del recibo no es valida.");return;}
        var pdf=item.Pdf;var hash=item.PdfHash;
        if(pdf is null)
        {
            try
            {
                var logo=RhReciboPagoPdfHandler.ResolveLogo(config,env);
                if(logo is null) {await Error("No se encontro el logo del recibo.");return;}
                var logoBytes=await File.ReadAllBytesAsync(logo,ct);
                if(RhReciboEmailRules.Hash(logoBytes)!=item.LogoHash) {await Error("El logo cambio desde la revision. Restaure la version antes de reintentar.");return;}
                pdf=RhReciboPagoPdfGenerator.Generate(source.Datos.Lineas,logoBytes,source.Datos.TipoNomina,false);
                if(!RhReciboEmailRules.ValidPdf(pdf)) {await Error("PDF vacio, invalido o mayor a 5 MiB.");return;}
                hash=RhReciboEmailRules.Hash(pdf);
                using var tx=rh.BeginTransaction();await Run(rh,"PDF_LISTO",tx,ct,N("p_ID",item.Id),S("p_TOKEN",token),B("p_PDF",pdf),S("p_HASH",hash));tx.Commit();
            }
            catch(OperationCanceledException) when(ct.IsCancellationRequested) {throw;}
            catch {await Error("No se pudo generar el PDF individual.");return;}
        }
        if(!RhReciboEmailRules.ValidPdf(pdf) || RhReciboEmailRules.Hash(pdf)!=hash || !EmailManualRules.IsAddress(item.Correo))
        {await Error("PDF o destinatario no valido. No se envio el recibo.");return;}
        // SIS insertion is idempotent by SOL_SMTP; a crash before RH acknowledgement safely repeats only this insertion.
        var actor=await Actor();
        var subject=RhReciboEmailRules.Subject(source);
        var body=$"Estimado/a {item.Nombre},\n\nSe adjunta su recibo de pago con fecha de nomina {source.Datos.Lineas[0].FechaNomina:dd/MM/yyyy}.\n\nOssmmasoft";
        await EmailReciboDb.Enqueue(sis,company,actor,item.Id,item.SolSmtp,item.Correo,subject,body,RhReciboEmailRules.FileName(item.Persona,nomina,periodo),pdf,hash,settings,ct);
        using(var tx=rh.BeginTransaction()) {await Run(rh,"ENCOLADO",tx,ct,N("p_ID",item.Id),S("p_TOKEN",token));tx.Commit();}
        async Task<int> Actor()
        {
            // Actor is obtained via a worker-only stored procedure, never from a request.
            using var cmd=Cmd(rh,"ACTOR_LOTE",null,S("p_ID",lotId),N("p_EMPRESA",company));var output=cmd.Parameters.Add("p_ACTOR",OracleDbType.Int32,ParameterDirection.Output);await cmd.ExecuteNonQueryAsync(ct);return SupportDb.GetIntOutput(output);
        }
        async Task Error(string text) {using var tx=rh.BeginTransaction();await Run(rh,"ERROR_PDF",tx,ct,N("p_ID",item.Id),S("p_TOKEN",token),S("p_ERROR",text));tx.Commit();}
    }
}
