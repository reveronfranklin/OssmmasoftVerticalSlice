using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Features.Support;
using OssmmasoftVerticalSlice.Helpers;
using System.Security.Claims;
using System.Text;

namespace OssmmasoftVerticalSlice.Features.DatabaseDeployment;

[ApiController,Authorize,Route("api/DatabaseDeployment")]
public class DatabaseDeploymentController(DeploymentHandler handler):ControllerBase
{
    [HttpPost("config")]
    public async Task<IActionResult> Config()=>Ok(await handler.Config(User,Request));
    [HttpPost("check")]
    public async Task<IActionResult> Check(DeploymentCompare value)=>Ok(await handler.Check(value,User,Request));
    [HttpPost("compare")]
    public async Task<IActionResult> Compare(DeploymentCompare value)=>Ok(await handler.Compare(value,User,Request));
    [HttpPost("history")]
    public async Task<IActionResult> History()=>Ok(await handler.History(User,Request));
    [HttpPost("get")]
    public async Task<IActionResult> Get(DeploymentId value)=>Ok(await handler.Get(value,User,Request));
    [HttpPost("object")]
    public async Task<IActionResult> Object(DeploymentObjectQuery value)=>Ok(await handler.Object(value,User,Request));
    [HttpPost("prepare")]
    public async Task<IActionResult> Prepare(DeploymentPrepare value)=>Ok(await handler.Prepare(value,User,Request));
    [HttpPost("download")]
    public async Task<IActionResult> Download(DeploymentObjectQuery value)
    {
        var result=await handler.Download(value,User,Request);
        return result.IsValid?File(Encoding.UTF8.GetBytes(result.Data ?? ""),"application/sql",$"R54-{value.Key}.sql"):Ok(result);
    }
    [HttpPost("execute")]
    public IActionResult Execute()=>Ok(new ResultDto<string>(""){IsValid=false,Message="Ejecucion no disponible en la fase 1. No se ejecutaron cambios."});
}
public class DeploymentHandler(ConnectionDB db,IConfiguration config,DeploymentMetadata metadata,DeploymentStore store,DeploymentQueue queue)
{
    private const string ComparePermission="sistema.despliegues.comparar";
    private const string PreparePermission="sistema.despliegues.preparar";
    private static ResultDto<T> Fail<T>(string text)=>new(default!){IsValid=false,Message=text};
    private static ResultDto<T> Ok<T>(T data)=>new(data){IsValid=true,Message="Success"};
    private async Task<int> Actor(ClaimsPrincipal user,HttpRequest request,string permission)
    {
        if(!SupportDb.TryGetEmpresa(config,out var company,out _))throw new InvalidOperationException("Empresa no configurada.");
        using var cn=db.GetSisConnection();await cn.OpenAsync(request.HttpContext.RequestAborted);
        var actor=await SupportSecurity.ResolveAuthenticatedUserAsync(cn,company,user,request);
        if(!actor.IsValid)throw new InvalidOperationException("Sesion no autorizada.");
        if(await SupportSecurity.IsSuperUserAsync(cn,actor.Data,company))return actor.Data;
        var rights=await SupportSecurity.GetPermissionsAsync(cn,actor.Data,company);
        if(!SupportSecurity.HasAny(rights,permission))throw new InvalidOperationException("No tiene permiso para esta operacion de puesta en produccion.");
        return actor.Data;
    }
    private async Task<ResultDto<T>> Run<T>(ClaimsPrincipal user,HttpRequest request,string permission,Func<int,Task<T>> action,bool requireEnabled=true)
    {
        try
        {
            var actor=await Actor(user,request,permission);
            if(requireEnabled && !DeploymentOptions.Read(config).Enabled)throw new InvalidOperationException("Comparacion deshabilitada. Configure DatabaseDeployment en el servidor de produccion.");
            return Ok(await action(actor));
        }
        catch(InvalidOperationException ex){return Fail<T>(ex.Message);}
        catch(Exception){return Fail<T>("No se pudo completar la operacion. Revise acceso a SIS, perfiles de metadatos y almacenamiento privado.");}
    }
    private List<string> Schemas(List<string>? values)
    {
        var options=DeploymentOptions.Read(config);
        if(values is null || values.Count is <1 or >9 || values.Distinct().Count()!=values.Count || values.Any(s=>!DeploymentRules.SchemaAllowed(s,options.AllowedSchemas)))
            throw new InvalidOperationException("Seleccione esquemas permitidos. BMC y RHC estan excluidos.");
        return values.Order(StringComparer.Ordinal).ToList();
    }
    private async Task<DeploymentRecord> Owned(string id,int actor)
    {
        if(!DeploymentRules.ValidId(id))throw new InvalidOperationException("Identificador invalido.");
        var record=await store.Read(id);
        if(record is null || record.Actor!=actor)throw new InvalidOperationException("Comparacion no encontrada o no autorizada.");
        return record;
    }
    public Task<ResultDto<DeploymentAvailability>> Config(ClaimsPrincipal u,HttpRequest r)=>Run(u,r,ComparePermission,_=>{
        var o=DeploymentOptions.Read(config);
        return Task.FromResult(new DeploymentAvailability(o.Enabled,false,o.AllowedSchemas.Where(x=>DeploymentRules.SchemaAllowed(x,o.AllowedSchemas)).ToList(),DeploymentRules.KnownTypes.ToList(),"Fase 1: solo metadatos y planes. La ejecucion esta deshabilitada por codigo."));},false);
    public Task<ResultDto<List<DeploymentConnectionResult>>> Check(DeploymentCompare value,ClaimsPrincipal u,HttpRequest r)=>Run(u,r,ComparePermission,async _=>{
        var schemas=Schemas(value.Schemas);var rows=new List<DeploymentConnectionResult>();
        foreach(var schema in schemas)
        {
            var s=await metadata.Check(schema,true,r.HttpContext.RequestAborted);var t=await metadata.Check(schema,false,r.HttpContext.RequestAborted);
            if(s.Identidad is not null && s.Identidad.DatabaseId==t.Identidad?.DatabaseId)
            {s=s with{Validado=false,Mensaje="Mismo destino/esquema: comparacion bloqueada."};t=t with{Validado=false,Mensaje="Mismo destino/esquema: comparacion bloqueada."};}
            rows.Add(s);rows.Add(t);
        }
        return rows;
    });
    public Task<ResultDto<DeploymentSummary>> Compare(DeploymentCompare value,ClaimsPrincipal u,HttpRequest r)=>Run(u,r,ComparePermission,async actor=>{
        var schemas=Schemas(value.Schemas);
        if(!DeploymentRules.ValidId(value.Solicitud) || value.Types is null || value.Types.Count==0 || value.Types.Count>DeploymentRules.KnownTypes.Length || value.Types.Distinct().Count()!=value.Types.Count || value.Types.Any(x=>!DeploymentRules.KnownTypes.Contains(x)))throw new InvalidOperationException("Solicitud o tipos de objetos invalidos.");
        var types=value.Types.Order(StringComparer.Ordinal).ToList();var hash=DeploymentRules.Hash(string.Join('|',schemas)+"/"+string.Join('|',types));
        var (record,created)=await store.Create(new DeploymentRecord{Id=value.Solicitud,Actor=actor,RequestHash=hash,Version=DeploymentOptions.Read(config).BuildVersion(),Schemas=schemas,Types=types},r.HttpContext.RequestAborted);
        if(created && !queue.Enqueue(record.Id)){record.Status="ERROR";record.Message="Cola ocupada. Inicie otra solicitud.";await store.Save(record);}
        return DeploymentRules.Summary(record);
    });
    public Task<ResultDto<List<DeploymentSummary>>> History(ClaimsPrincipal u,HttpRequest r)=>Run(u,r,ComparePermission,async actor=>(await store.List(actor)).Select(DeploymentRules.Summary).ToList(),false);
    public Task<ResultDto<DeploymentSummary>> Get(DeploymentId value,ClaimsPrincipal u,HttpRequest r)=>Run(u,r,ComparePermission,async actor=>DeploymentRules.Summary(await Owned(value.Id,actor)),false);
    public Task<ResultDto<object>> Object(DeploymentObjectQuery value,ClaimsPrincipal u,HttpRequest r)=>Run<object>(u,r,ComparePermission,async actor=>{
        var record=await Owned(value.Id,actor);
        var source=record.Source.SingleOrDefault(x=>x.Key==value.Key);var target=record.Target.SingleOrDefault(x=>x.Key==value.Key);
        if(source is null && target is null)throw new InvalidOperationException("Objeto no encontrado.");
        return new{Pruebas=source,Produccion=target};
    },false);
    public Task<ResultDto<DeploymentPlan>> Prepare(DeploymentPrepare value,ClaimsPrincipal u,HttpRequest r)=>Run(u,r,PreparePermission,async actor=>{
        var record=await Owned(value.Id,actor);
        if(value.Objects is null)throw new InvalidOperationException("Seleccione objetos.");
        return await store.Prepare(record.Id,actor,value.Objects,record.Version);
    });
    public Task<ResultDto<string>> Download(DeploymentObjectQuery value,ClaimsPrincipal u,HttpRequest r)=>Run(u,r,PreparePermission,async actor=>{
        var record=await Owned(value.Id,actor);var plan=record.Plans.SingleOrDefault(p=>p.Id==value.Key)??throw new InvalidOperationException("Plan no encontrado.");return plan.Script;
    },false);
}
