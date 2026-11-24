using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Features.Support;
using OssmmasoftVerticalSlice.Helpers;
namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public sealed class RhNomCorridasHandler(ConnectionDB db,IConfiguration config)
{
    public Task<ResultDto<RhNomCorrida>> Crear(RhNomCrearCorrida value,int company,int actor,CancellationToken ct)=>RhNomCfgDb.Resultado(async()=>
    {
        if(!config.GetValue<bool>("RhMotorNomina:CorridasEnabled"))throw new RhNomError("CORRIDAS_DESHABILITADAS","Las corridas persistidas no estan habilitadas en este ambiente.");
        var mode=RhNomCorridas.Modo(value);var requestHash=RhNomCorridas.HashSolicitud(value,company,actor);using var cn=await RhNomCfgDb.Abrir(db,ct);
        var existing=await RhNomCorridasDb.PorClave(cn,company,actor,value.IdempotencyKey,ct);
        if(existing is not null){if(existing.HashSolicitud!=requestHash)throw new RhNomError("CLAVE_REUTILIZADA","La clave idempotente identifica otro contenido.");return new ResultDto<RhNomCorrida>(existing){IsValid=true,Message="Solicitud existente; no se duplico ni recapturo."};}
        var modelId=value.Lote?.ModeloId??value.Actual?.ModeloId??value.Comparacion!.ModeloId;var version=value.Lote?.Version??value.Actual?.Version??value.Comparacion!.Version;
        var model=await RhNomCfgDb.Revision(cn,company,modelId,version,ct);RhNomCorridaDocumento doc;
        if(mode=="LOTE_MANUAL")doc=new(1,company,actor,model,mode,value.Lote! with{Casos=RhNomLotes.Validar(value.Lote!)},null,null,null,null,null,null);
        else if(mode=="ACTUAL")
        {
            var actual=await new RhNomFuentesActualesHandler(db).Handle(value.Actual!,company,ct);
            if(!actual.IsValid||actual.Data is null)return new ResultDto<RhNomCorrida>(default!){IsValid=false,Message=actual.Message};
            doc=new(1,company,actor,model,mode,null,actual.Data.Fuente,actual.Data.Entradas,actual.Data.PoliticaPresupuesto,actual.Data.MotivoPresupuesto,null,null);
        }
        else
        {
            var q=value.Comparacion!;var reference=await new RhNomReferenciaHistoricaHandler(db).Get(new(model.CodigoTipoNomina,q.CodigoPeriodo,q.FechaNomina,q.CodigoPersona),company,ct);
            if(!reference.IsValid||reference.Data is null)return new ResultDto<RhNomCorrida>(default!){IsValid=false,Message=reference.Message};
            RhNomComparacionesPrueba.Comparar(model,q,reference.Data,ct);doc=new(1,company,actor,model,mode,null,null,null,null,null,q,reference.Data);
        }
        var id=await RhNomCorridasDb.Crear(cn,company,actor,value.IdempotencyKey,requestHash,doc,ct);
        return new ResultDto<RhNomCorrida>(await RhNomCorridasDb.Leer(cn,company,actor,id,ct)){IsValid=true,Message="Corrida de simulacion pendiente. No se aplicaron movimientos."};
    });
    public Task<ResultDto<List<RhNomCorrida>>> Listar(RhNomListaQuery q,int company,int actor,CancellationToken ct)=>RhNomCfgDb.Resultado(async()=>
    {var page=RhNomConfiguracion.Lista(q);using var cn=await RhNomCfgDb.Abrir(db,ct);var result=await RhNomCorridasDb.Listar(cn,company,actor,page,ct);return new ResultDto<List<RhNomCorrida>>(result.Rows){IsValid=true,Message="Corridas del usuario autenticado.",Page=page.PageNumber,CantidadRegistros=result.Count,TotalPage=(int)Math.Ceiling(result.Count/(double)page.PageSize)};});
    public Task<ResultDto<RhNomCorrida>> Get(RhNomCorridaId q,int company,int actor,CancellationToken ct)=>RhNomCfgDb.Resultado(async()=>{using var cn=await RhNomCfgDb.Abrir(db,ct);return new ResultDto<RhNomCorrida>(await RhNomCorridasDb.Leer(cn,company,actor,q.IdRun,ct)){IsValid=true,Message="Estado recuperado."};});
    public Task<ResultDto<RhNomCorrida>> Cancelar(RhNomCancelarCorrida q,int company,int actor,CancellationToken ct)=>RhNomCfgDb.Resultado(async()=>{using var cn=await RhNomCfgDb.Abrir(db,ct);await RhNomCorridasDb.Cancelar(cn,company,actor,q,ct);return new ResultDto<RhNomCorrida>(await RhNomCorridasDb.Leer(cn,company,actor,q.IdRun,ct)){IsValid=true,Message="Cancelacion solicitada; confirme estado final. Sin movimientos."};});
    public Task<ResultDto<RhNomCorridaDetalle>> Detalle(RhNomCorridaDetalleQuery q,int company,int actor,CancellationToken ct)=>RhNomCfgDb.Resultado(async()=>
    {
        using var cn=await RhNomCfgDb.Abrir(db,ct);var run=await RhNomCorridasDb.Leer(cn,company,actor,q.IdRun,ct);
        if(q.Modo!=run.Modo)throw new RhNomError("MODO_DISTINTO","El modo solicitado no coincide con la corrida; no se entregan datos.");
        var input=await RhNomCorridasDb.Documento(cn,run,"ENTRADA",ct)??throw new RhNomError("SNAPSHOT_AUSENTE","No hay snapshot protegido.");var doc=RhNomCorridas.Leer(input,run.HashDatos,company,actor);
        var output=await RhNomCorridasDb.Documento(cn,run,"RESULTADO",ct);RhNomCorridaResultado? result=null;
        if(output is not null)try{result=JsonSerializer.Deserialize<RhNomCorridaResultado>(output,RhNomCorridas.Json);}catch(JsonException){throw new RhNomError("RESULTADO_INVALIDO","Resultado guardado no interpretable.");}
        return new ResultDto<RhNomCorridaDetalle>(new(run,doc,result)){IsValid=true,Message="Snapshot y resultado propios recuperados."};
    });
}
public sealed partial class RhNominaController
{
    private static string[] PermisosCorrida(string? mode)=>mode switch {"ACTUAL" or RhNomModelosDesdeCaptura.Modo=>["rh.nomina.fuentes.datos"],"COMPARACION_PRUEBA"=>["rh.nomina.historico.datos"],"LOTE_MANUAL"=>[],_=>["rh.nomina.traza.datos"]};
    [HttpPost("corridasCrear")][RequestSizeLimit(524288)]
    public Task<IActionResult> RunCreate(RhNomCrearCorrida q)=>RunAsync("rh.nomina.simular",(e,a,ct)=>new RhNomCorridasHandler(db,config).Crear(q,e,a,ct),PermisosCorrida(q.Actual is not null?"ACTUAL":q.Comparacion is not null?"COMPARACION_PRUEBA":"LOTE_MANUAL"));
    [HttpPost("corridasGetAll")]
    public Task<IActionResult> RunList(RhNomListaQuery q)=>RunAsync("rh.nomina.simular",(e,a,ct)=>new RhNomCorridasHandler(db,config).Listar(q,e,a,ct));
    [HttpPost("corridasGet")]
    public Task<IActionResult> RunGet(RhNomCorridaId q)=>RunAsync("rh.nomina.simular",(e,a,ct)=>new RhNomCorridasHandler(db,config).Get(q,e,a,ct));
    [HttpPost("corridasCancelar")]
    public Task<IActionResult> RunCancel(RhNomCancelarCorrida q)=>RunAsync("rh.nomina.simular",(e,a,ct)=>new RhNomCorridasHandler(db,config).Cancelar(q,e,a,ct));
    [HttpPost("corridasDetalle")]
    public Task<IActionResult> RunDetail(RhNomCorridaDetalleQuery q)=>RunAsync("rh.nomina.simular",(e,a,ct)=>new RhNomCorridasHandler(db,config).Detalle(q,e,a,ct),PermisosCorrida(q.Modo));
}
public static class RhNomCorridaProcessor
{
    public static async Task<bool> Tick(ConnectionDB db,int company,CancellationToken ct,ILogger? logger=null)
    {
        using var cn=await RhNomCfgDb.Abrir(db,ct);var token=Guid.NewGuid().ToString("N");var claimed=await RhNomCorridasDb.Tomar(cn,company,token,ct);if(claimed.Id<=0)return false;
        var run=await RhNomCorridasDb.Leer(cn,company,claimed.Actor,claimed.Id,ct);
        var timer=System.Diagnostics.Stopwatch.StartNew();var timings=new Dictionary<string,long>();
        void Phase(string name){timings[name]=timer.ElapsedMilliseconds;timer.Restart();}
        try
        {
            var text=await RhNomCorridasDb.Documento(cn,run,"ENTRADA",ct)??throw new RhNomError("SNAPSHOT_AUSENTE","Snapshot de entrada ausente.");
            Phase("lectura");var doc=RhNomCorridas.Leer(text,run.HashDatos,company,claimed.Actor);
            if(doc.Modelo.ModeloId!=run.ModeloId||doc.Modelo.Version!=run.VersionModelo||doc.Modo!=run.Modo)throw new RhNomError("CONTEXTO_DISTINTO","Cabecera y snapshot no coinciden.");
            Phase("validacion");using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);deadline.CancelAfter(TimeSpan.FromMinutes(2));
            long queueMs=0;
            var result=await Task.Run(()=>{queueMs=timer.ElapsedMilliseconds;return RhNomCorridas.Evaluar(doc,deadline.Token);},deadline.Token);
            timings["colaEvaluador"]=queueMs;timings["evaluacion"]=Math.Max(0,timer.ElapsedMilliseconds-queueMs);timer.Restart();
            await RhNomCorridasDb.Terminar(cn,run,token,result,null,ct);Phase("guardado");
        }
        catch(OperationCanceledException) when(ct.IsCancellationRequested){throw;}
        catch(Exception ex)
        {
            var message=ex is RhNomError r?r.Message:ex is OperationCanceledException?"Tiempo de simulacion agotado.":ex is Oracle.ManagedDataAccess.Client.OracleException oracle?$"Fallo Oracle al guardar la corrida (ORA-{oracle.Number:D5}). Reintente desde las mismas entradas.":"No se pudo completar la simulacion guardada.";
            logger?.LogWarning("Corrida {Run}: fallo {Kind}, Oracle={OracleCode}.",run.IdRun,ex.GetType().Name,ex is Oracle.ManagedDataAccess.Client.OracleException oe?oe.Number:0);
            await RhNomCorridasDb.Terminar(cn,run,token,null,message,ct);
        }
        finally
        {
            var total=timings.Values.Sum();
            if(total>=10000)logger?.LogWarning("Worker nomina corrida {Run}: lectura={ReadMs}ms validacion={ValidationMs}ms colaEvaluador={QueueMs}ms evaluacion={EvaluationMs}ms guardado={SaveMs}ms.",
                run.IdRun,timings.GetValueOrDefault("lectura"),timings.GetValueOrDefault("validacion"),timings.GetValueOrDefault("colaEvaluador"),timings.GetValueOrDefault("evaluacion"),timings.GetValueOrDefault("guardado"));
        }
        return true;
    }
}
public sealed class RhNomCorridaWorker(IServiceScopeFactory scopes,IConfiguration config,ILogger<RhNomCorridaWorker> logger):BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while(!stoppingToken.IsCancellationRequested)
        {
            var didWork=false;
            try
            {
                if(config.GetValue<bool>("RhMotorNomina:CorridasEnabled")&&SupportDb.TryGetEmpresa(config,out var company,out _)&&company>0)
                {using var scope=scopes.CreateScope();var database=scope.ServiceProvider.GetRequiredService<ConnectionDB>();didWork=await RhNomCorridaProcessor.Tick(database,company,stoppingToken,logger);didWork=await RhNomPrenominaProcessor.Tick(database,config,company,stoppingToken,logger)||didWork;}
            }
            catch(OperationCanceledException)when(stoppingToken.IsCancellationRequested){break;}
            catch(Exception ex){logger.LogWarning("Worker nomina simulacion: {Kind}, Oracle={OracleCode}.",ex.GetType().Name,ex is Oracle.ManagedDataAccess.Client.OracleException oracle?oracle.Number:0);}
            try{await Task.Delay(TimeSpan.FromSeconds(didWork?1:10),stoppingToken);}catch(OperationCanceledException)when(stoppingToken.IsCancellationRequested){break;}
        }
    }
}
