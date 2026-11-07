using System.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Features.Email;
using OssmmasoftVerticalSlice.Features.Support;
using OssmmasoftVerticalSlice.Helpers;
using static OssmmasoftVerticalSlice.Features.RhReciboPago.RrmDb;

namespace OssmmasoftVerticalSlice.Features.RhReciboPago;

[Authorize]
[ApiController]
[Route("api/RhReciboPago")]
public class RhReciboEmailController(ConnectionDB db,IConfiguration config,IWebHostEnvironment env,ILogger<RhReciboEmailController> logger) : ControllerBase
{
    private bool Enabled => config.GetValue("settings:RhReciboEmail:Enabled",false) && EmailSettings.Read(config).Enabled && EmailSettings.Read(config).Validate() is null;
    private CancellationToken Ct => HttpContext.RequestAborted;
    private async Task<IActionResult> Execute<T>(Func<OracleConnection,int,int,Task<ResultDto<T>>> action,string permission=RhReciboEmailRules.Permission)
    {
        if(!SupportDb.TryGetEmpresa(config,out var company,out var error)) return Ok(EmailManualRules.Fail<T>(error));
        using var sis=db.GetSisConnection(); using var rh=db.GetRhConnection();
        try { await sis.OpenAsync(Ct);await rh.OpenAsync(Ct); }
        catch { return Ok(EmailManualRules.Fail<T>("No se pudo conectar para procesar recibos por correo.")); }
        try
        {
            var actor=await SupportSecurity.GetAuthenticatedUserIdAsync(sis,User,company,null);
            if(actor<=0) return Unauthorized();
            var permissions=await SupportSecurity.GetPermissionsAsync(sis,actor,company);
            if(!SupportSecurity.HasAny(permissions,permission)) return StatusCode(403,EmailManualRules.Fail<T>("No tiene permiso para esta operacion de recibos."));
            return Ok(await action(rh,company,actor));
        }
        catch(OracleException ex) when(ex.Number==20051) { return Ok(EmailManualRules.Fail<T>("Lote vencido, modificado o no disponible para esta accion. Actualice y vuelva a analizar.")); }
        catch(Exception ex) { logger.LogWarning("Operacion de recibos por correo no completada ({Type}).",ex.GetType().Name);return Ok(EmailManualRules.Fail<T>("No se pudo completar la operacion. Consulte el lote antes de repetir.")); }
    }
    private static ResultDto<T> Success<T>(T data) => new(data){IsValid=true,Message="success"};
    private async Task<(string Json,string Hash,string Error)> Source(int company,int person,int nomina,int periodo,int icp,string logoHash)
    {
        var data=await RhReciboPagoDb.ReadAsync(db,config,new(nomina,periodo,icp,person),true,Ct);
        if(!data.IsValid || data.Data is null) return ("","","No se pudo consultar el recibo historico.");
        var source=new ReciboEmailFuente(data.Data,company,person,nomina,periodo,logoHash);
        if(!RhReciboEmailRules.ValidSource(source,company,person,nomina,periodo,icp)) return ("","","El recibo no corresponde a la persona, empresa o filtros seleccionados.");
        var json=RhReciboEmailRules.Serialize(source);return(json,RhReciboEmailRules.Hash(json),"");
    }
    [HttpPost("analizarEnvio")]
    public Task<IActionResult> Analyze(ReciboEmailAnalizar value) => Execute<ReciboEmailLote>(async(cn,company,actor)=> {
        var max=Math.Clamp(config.GetValue("settings:RhReciboEmail:MaxItems",100),1,100);
        if(value.CodigoTipoNomina<=0 || value.CodigoPeriodo<=0 || value.CodigoIcp<0 || value.Personas is null || value.Personas.Count<1 || value.Personas.Count>max || value.Personas.Any(x=>x<=0) || value.Personas.Distinct().Count()!=value.Personas.Count)
            return EmailManualRules.Fail<ReciboEmailLote>($"Seleccione entre 1 y {max} trabajadores distintos y filtros validos.");
        var logo=RhReciboPagoPdfHandler.ResolveLogo(config,env);
        if(logo is null) return EmailManualRules.Fail<ReciboEmailLote>("No se encontro el logo del recibo.");
        var logoHash=RhReciboEmailRules.Hash(await System.IO.File.ReadAllBytesAsync(logo,Ct));
        var data=await RhReciboPagoDb.ReadAsync(db,config,new(value.CodigoTipoNomina,value.CodigoPeriodo,value.CodigoIcp),true,Ct);
        if(!data.IsValid || data.Data is null || data.Data.EsTemporal) return EmailManualRules.Fail<ReciboEmailLote>("No se pudo validar el historico seleccionado.");
        var rows=new List<(int Person,string Name,string Mail,string Error,string Json,string Hash)>();
        foreach(var person in value.Personas.Order())
        {
            var lines=data.Data.Lineas.Where(x=>x.CodigoPersona==person).ToList();
            var source=new ReciboEmailFuente(new(lines,false,data.Data.TipoNomina),company,person,value.CodigoTipoNomina,value.CodigoPeriodo,logoHash);
            if(!RhReciboEmailRules.ValidSource(source,company,person,value.CodigoTipoNomina,value.CodigoPeriodo,value.CodigoIcp))
                return EmailManualRules.Fail<ReciboEmailLote>("La seleccion contiene una persona ajena a la consulta autorizada.");
            var contact=await Contact(cn,company,person,Ct);
            var json=RhReciboEmailRules.Serialize(source);var hash=RhReciboEmailRules.Hash(json);var message=contact.Error;
            if(message.Length==0 && await Previous(cn,company,person,value.CodigoTipoNomina,value.CodigoPeriodo,hash,Ct)>0) message="Recibo con envio anterior. Consulte el lote original para reintentar o reenviar.";
            rows.Add((person,lines[0].Nombre,contact.Correo,message,json,hash));
        }
        var id=Guid.NewGuid().ToString("D");
        var revision=RhReciboEmailRules.Hash(System.Text.Json.JsonSerializer.Serialize(rows.Select(x=>new{x.Person,x.Mail,x.Hash,x.Error})));
        using(var tx=cn.BeginTransaction())
        {
            await Run(cn,"NUEVO",tx,Ct,S("p_ID",id),N("p_EMPRESA",company),N("p_ACTOR",actor),N("p_NOMINA",value.CodigoTipoNomina),N("p_PERIODO",value.CodigoPeriodo),N("p_ICP",value.CodigoIcp),S("p_REV",revision));
            foreach(var row in rows) await Run(cn,"ITEM",tx,Ct,S("p_LOTE",id),N("p_EMPRESA",company),N("p_PERSONA",row.Person),S("p_NOMBRE",row.Name),S("p_CORREO",row.Mail),S("p_ERROR",row.Error),C("p_FUENTE",row.Json),S("p_HASH",row.Hash),S("p_LOGO",logoHash),S("p_SMTP",Guid.NewGuid().ToString("D")));
            tx.Commit();
        }
        return Success(View((await Read(cn,id,company,actor,Ct))!,Enabled,1,100));
    });
    [HttpPost("confirmarEnvio")]
    public Task<IActionResult> Confirm(ReciboEmailConfirmar value) => Execute<ReciboEmailLote>(async(cn,company,actor)=> {
        if(!Enabled) return EmailManualRules.Fail<ReciboEmailLote>("Envio de recibos deshabilitado o SMTP sin configurar.");
        if(!RhReciboEmailRules.ValidKey(value.AnalisisId) || !RhReciboEmailRules.ValidKey(value.Solicitud) || string.IsNullOrEmpty(value.Revision)) return EmailManualRules.Fail<ReciboEmailLote>("Solicitud o analisis invalido.");
        using(var tx=cn.BeginTransaction())
        {
            using var cmd=Cmd(cn,"BLOQUEAR",tx,S("p_ID",value.AnalisisId),N("p_EMPRESA",company),N("p_ACTOR",actor),S("p_SOL",value.Solicitud),S("p_REV",value.Revision));
            var repeated=cmd.Parameters.Add("p_REPETIDO",OracleDbType.Int32,ParameterDirection.Output);await cmd.ExecuteNonQueryAsync(Ct);
            var lot=await Read(cn,value.AnalisisId,company,actor,Ct,tx);
            if(lot is null) return EmailManualRules.Fail<ReciboEmailLote>("Lote no encontrado.");
            if(SupportDb.GetIntOutput(repeated)==0)
            {
                if(!lot.Items.Any(x=>x.Estado=="APTO")) return EmailManualRules.Fail<ReciboEmailLote>("No hay recibos aptos para enviar.");
                var logo=RhReciboPagoPdfHandler.ResolveLogo(config,env);
                if(logo is null) return EmailManualRules.Fail<ReciboEmailLote>("No se encontro el logo.");
                var logoHash=RhReciboEmailRules.Hash(await System.IO.File.ReadAllBytesAsync(logo,Ct));
                foreach(var item in lot.Items.Where(x=>x.Estado=="APTO"))
                {
                    var contact=await Contact(cn,company,item.Persona,Ct);
                    var current=await Source(company,item.Persona,lot.Nomina,lot.Periodo,lot.Icp,logoHash);
                    if(contact.Error.Length>0 || contact.Correo!=item.Correo || current.Error.Length>0 || current.Hash!=item.Huella)
                        return EmailManualRules.Fail<ReciboEmailLote>("Cambio el correo o el recibo de un trabajador. Vuelva a analizar la seleccion.");
                }
                await Run(cn,"CONFIRMAR",tx,Ct,S("p_ID",lot.Id),N("p_EMPRESA",company),S("p_SOL",value.Solicitud));
            }
            tx.Commit();
        }
        return Success(View((await Read(cn,value.AnalisisId,company,actor,Ct))!,Enabled,1,100));
    });
    [HttpPost("getLoteEnvio")]
    public Task<IActionResult> GetLot(ReciboEmailConsultar value) => Execute<ReciboEmailLote>(async(cn,company,actor)=> {
        if(!RhReciboEmailRules.ValidKey(value.LoteId) || value.Page<1 || value.Page>100000 || value.PageSize<1 || value.PageSize>100) return EmailManualRules.Fail<ReciboEmailLote>("Consulta o paginacion invalida.");
        var lot=await Read(cn,value.LoteId,company,actor,Ct);
        return lot is null ? EmailManualRules.Fail<ReciboEmailLote>("Lote no encontrado para este usuario.") : Success(View(lot,Enabled,value.Page,value.PageSize));
    });
    [HttpPost("getLotesEnvio")]
    public Task<IActionResult> History(ReciboEmailHistorial value) => Execute<ReciboEmailLotes>(async(cn,company,actor)=> {
        if(value.Page<1 || value.Page>100000 || value.PageSize<1 || value.PageSize>50) return EmailManualRules.Fail<ReciboEmailLotes>("Paginacion invalida.");
        using var cmd=Cmd(cn,"LOTES",null,N("p_EMPRESA",company),N("p_ACTOR",actor),N("p_PAGE",value.Page),N("p_SIZE",value.PageSize));
        cmd.Parameters.Add("p_ROWS",OracleDbType.RefCursor,ParameterDirection.Output);var total=cmd.Parameters.Add("p_TOTAL",OracleDbType.Int32,ParameterDirection.Output);
        var ids=new List<string>();using(var reader=await cmd.ExecuteReaderAsync(Ct)) while(await reader.ReadAsync(Ct)) ids.Add(reader.SafeGetString("LOTE_ID"));
        var lots=new List<ReciboEmailLote>();foreach(var id in ids) {var lot=await Read(cn,id,company,actor,Ct);if(lot is not null) lots.Add(View(lot,Enabled,1,0));}
        return Success(new ReciboEmailLotes(lots,SupportDb.GetIntOutput(total),value.Page,value.PageSize));
    });
    [HttpPost("reanudarLoteEnvio")]
    public Task<IActionResult> Resume(ReciboEmailConsultar value) => Execute<string>(async(cn,company,actor)=> {
        if(!Enabled || !RhReciboEmailRules.ValidKey(value.LoteId)) return EmailManualRules.Fail<string>("Envio deshabilitado o lote invalido.");
        using var tx=cn.BeginTransaction();await Run(cn,"REANUDAR",tx,Ct,S("p_ID",value.LoteId),N("p_EMPRESA",company),N("p_ACTOR",actor));tx.Commit();using var sis=db.GetSisConnection();await sis.OpenAsync(Ct);await EmailReciboDb.Resume(sis,company,Ct);return Success("SMTP y lotes pausados de la empresa reanudados; reintente explicitamente los items con error.");
    });
    [HttpPost("reintentarEnvio")]
    public Task<IActionResult> Retry(ReciboEmailReintentar value) => Execute<string>(async(cn,company,actor)=> {
        if(!Enabled || value.ItemId<=0 || !RhReciboEmailRules.ValidKey(value.Solicitud)) return EmailManualRules.Fail<string>("Solicitud invalida o envio deshabilitado.");
        using var tx=cn.BeginTransaction();await Run(cn,"REINTENTAR",tx,Ct,N("p_ID",value.ItemId),N("p_EMPRESA",company),N("p_ACTOR",actor),S("p_SOL",value.Solicitud));tx.Commit();return Success("Reintento registrado.");
    });
    private async Task<(RrmLot Lot,RrmItem Item,ReciboEmailDestino Contact)> ResendSource(OracleConnection cn,int company,int actor,int itemId)
    {
        using var find=Cmd(cn,"ITEM_LOTE",null,N("p_ITEM",itemId),N("p_EMPRESA",company),N("p_ACTOR",actor));
        var id=find.Parameters.Add("p_LOTE",OracleDbType.Varchar2,36,null,ParameterDirection.Output);await find.ExecuteNonQueryAsync(Ct);
        var lot=await Read(cn,id.Value.ToString()!,company,actor,Ct) ?? throw new InvalidOperationException("Lote inexistente.");
        var item=lot.Items.Single(x=>x.Id==itemId);var contact=await Contact(cn,company,item.Persona,Ct);
        return(lot,item,contact);
    }
    [HttpPost("analizarReenvio")]
    public Task<IActionResult> ResendPreview(ReciboEmailIdentificar value) => Execute<ReciboEmailReenvioPreview>(async(cn,company,actor)=> {
        var (lot,item,contact)=await ResendSource(cn,company,actor,value.ItemId);
        if(item.Estado!="ACEPTADO_SMTP" || contact.Error.Length>0) return EmailManualRules.Fail<ReciboEmailReenvioPreview>("El recibo no admite reenvio o no tiene un correo unico valido.");
        return Success(new ReciboEmailReenvioPreview(item.Id,item.Nombre,contact.Correo,RhReciboEmailRules.Hash($"{item.Id}|{item.Huella}|{contact.Correo}")));
    },RhReciboEmailRules.ResendPermission);
    [HttpPost("reenviarRecibo")]
    public Task<IActionResult> Resend(ReciboEmailReenviar value) => Execute<ReciboEmailLote>(async(cn,company,actor)=> {
        if(!Enabled || value.ItemId<=0 || !RhReciboEmailRules.ValidKey(value.Solicitud) || string.IsNullOrWhiteSpace(value.Motivo) || value.Motivo.Length>500)
            return EmailManualRules.Fail<ReciboEmailLote>("Indique un motivo de hasta 500 caracteres y una solicitud valida.");
        var (lot,item,contact)=await ResendSource(cn,company,actor,value.ItemId);
        if(value.Revision!=RhReciboEmailRules.Hash($"{item.Id}|{item.Huella}|{contact.Correo}"))
            return EmailManualRules.Fail<ReciboEmailLote>("Cambio el destinatario. Vuelva a revisar el reenvio.");
        var source=await Source(company,item.Persona,lot.Nomina,lot.Periodo,lot.Icp,item.LogoHash);
        if(contact.Error.Length>0 || source.Error.Length>0 || source.Hash!=item.Huella)
            return EmailManualRules.Fail<ReciboEmailLote>("Correo invalido o recibo modificado. Corrija Comunicaciones y vuelva a analizar.");
        string newId;
        using(var tx=cn.BeginTransaction())
        {
            using var cmd=Cmd(cn,"CLONAR",tx,N("p_ITEM",item.Id),N("p_EMPRESA",company),N("p_ACTOR",actor),S("p_SOL",value.Solicitud),S("p_CORREO",contact.Correo),S("p_MOTIVO",value.Motivo.Trim()),S("p_SMTP",Guid.NewGuid().ToString("D")));
            var output=cmd.Parameters.Add("p_LOTE",OracleDbType.Varchar2,36,null,ParameterDirection.Output);await cmd.ExecuteNonQueryAsync(Ct);newId=output.Value.ToString()!;tx.Commit();
        }
        return Success(View((await Read(cn,newId,company,actor,Ct))!,Enabled));
    },RhReciboEmailRules.ResendPermission);

}
