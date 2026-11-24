using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public record RhNomConsolidarQuery(List<int> Corridas);
public record RhNomOrigenConsolidado(int IdRun,int ModeloId,int Version,string HashDatos,string HashModelo,
    int CodigoRelacionCargo,string HashFuente,RhNomPartidaPropuesta? Partida,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] RhNomDecisionControl? DecisionControl=null);
public record RhNomDestinoConsolidado(int CodigoConcepto,int FrecuenciaId,string TipoMovimiento,
    string Estado,List<RhNomOrigenConsolidado> Origenes);
public record RhNomConsolidacion(int CodigoEmpresa,int CodigoTipoNomina,int CodigoPeriodo,DateTime FechaNomina,
    int CodigoPresupuesto,int CodigoPersona,string Escenario,List<RhNomDestinoConsolidado> Destinos,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] string? VersionDecisionesControl=null)
{
    public string VersionMetodo=>RhNomConsolidaciones.Version;
    public bool TieneConflictos=>Destinos.Any(d=>d.Estado!="DESTINO_UNICO");
    public bool AplicacionDisponible=>false;
    public bool MovimientosAplicados=>false;
    public bool ParidadHistoricaCertificada=>false;
}
// Read-only preparation: RH_MOV_NOMINA has no cargo/period in its physical key.
// Do not sum repeated salaries or infer segment/proration policies from dates.
public static class RhNomConsolidaciones
{
    public const string Version="CONSOLIDACION_DIAGNOSTICA_V1";
    public const int MaxCorridas=16;
    public const string VersionDecisiones="PLAN_DECISIONES_CONTROL_V1";
    public static (int CodigoConcepto,int FrecuenciaId,string TipoMovimiento) Destino(RhNomOrigenConsolidado origin)
    {
        if(origin is null||(origin.Partida is null)==(origin.DecisionControl is null))
            throw new RhNomError("ORIGEN_PROPUESTA_INVALIDO","Cada origen contiene una partida monetaria o una decision de control, exclusivamente.");
        if(origin.Partida is {} p)return(p.CodigoConcepto,p.FrecuenciaId,p.TipoMovimiento);
        var d=origin.DecisionControl!;
        if(d.CodigoConcepto<=0||d.FrecuenciaId<=0||d.TipoMovimiento is not ("F" or "V")||d.ValorControl is not ("0" or "1")||d.Accion is not ("RETIRAR_GENERADO" or "CONSERVAR_EXISTENTE"))
            throw new RhNomError("DECISION_PLAN_INVALIDA","Decision de control incompleta o incompatible con retiro/conservacion.");
        RhNomNumeros.Codigo(d.Clave);RhNomNumeros.Codigo(d.CodigoVariableControl);
        return(d.CodigoConcepto,d.FrecuenciaId,d.TipoMovimiento);
    }
    public static void VerificarDecisiones(RhNomConsolidacion c)
    {
        if(c.Destinos is null||c.Destinos.Any(d=>d is null||d.Origenes is null||d.Origenes.Count==0))throw new RhNomError("DESTINOS_PLAN_INVALIDOS","Destinos con origenes completos requeridos.");
        var has=false;
        foreach(var d in c.Destinos)foreach(var o in d.Origenes){var key=Destino(o);if(key!=(d.CodigoConcepto,d.FrecuenciaId,d.TipoMovimiento))throw new RhNomError("PROPUESTA_AJENA","La propuesta no corresponde al destino consolidado.");has|=o.DecisionControl is not null;}
        if(has?c.VersionDecisionesControl!=VersionDecisiones:c.VersionDecisionesControl is not null)
            throw new RhNomError("VERSION_DECISIONES_PLAN_INVALIDA","Version de decisiones del consolidado ausente o incompatible.");
    }
    public static int[] Validar(RhNomConsolidarQuery q)
    {
        if(q.Corridas is null||q.Corridas.Count is <1 or >MaxCorridas||q.Corridas.Any(id=>id<=0)||q.Corridas.Distinct().Count()!=q.Corridas.Count)
            throw new RhNomError("CORRIDAS_CONSOLIDACION_INVALIDAS","Indique de 1 a 16 corridas distintas y positivas.");
        return q.Corridas.Order().ToArray();
    }
    public static RhNomConsolidacion Preparar(IReadOnlyList<RhNomCorridaDetalle> details,int company,int actor,CancellationToken ct=default)
    {
        Validar(new(details.Select(d=>d.Corrida.IdRun).ToList()));
        RhNomCargoActual? context=null;string? scenario=null;
        var sources=new Dictionary<int,string>();var rows=new List<RhNomOrigenConsolidado>();
        foreach(var detail in details.OrderBy(d=>d.Corrida.IdRun))
        {
            ct.ThrowIfCancellationRequested();var run=detail.Corrida;var doc=detail.Entrada;
            if(run.CodigoEmpresa!=company||run.Actor!=actor||run.Modo!="ACTUAL"||run.Estado!="COMPLETADO"||doc.Modo!="ACTUAL"||doc.FuenteActual is null||run.ModeloId!=doc.Modelo.ModeloId||run.VersionModelo!=doc.Modelo.Version)
                throw new RhNomError("CORRIDA_NO_CONSOLIDABLE","Se requieren corridas actuales completadas, propias y con cabecera consistente.");
            // Verify the immutable input, then replay it; never trust client/saved output amounts.
            var checkedDoc=RhNomCorridas.Leer(RhNomCorridas.Serializar(doc,RhNomCorridas.MaxDatosBytes),run.HashDatos,company,actor);
            var replay=RhNomCorridas.Evaluar(checkedDoc,ct);
            if(!replay.IsValid||replay.Actual is null||(replay.Actual.Simulacion.ProyeccionConceptos is null&&replay.Actual.Simulacion.AccionesControl is not {Count:>0}))
                throw new RhNomError("PROYECCION_REQUERIDA","La corrida debe reproducirse con reglas de concepto validas.");
            var source=checkedDoc.FuenteActual!;
            if(context is null){context=source;scenario=checkedDoc.Modelo.Escenario;}
            if(source.CodigoEmpresa!=context.CodigoEmpresa||source.CodigoTipoNomina!=context.CodigoTipoNomina||source.CodigoPeriodo!=context.CodigoPeriodo||source.FechaNomina!=context.FechaNomina||source.CodigoPresupuesto!=context.CodigoPresupuesto||source.CodigoPersona!=context.CodigoPersona||checkedDoc.Modelo.Escenario!=scenario)
                throw new RhNomError("CONTEXTO_CONSOLIDACION_DISTINTO","Consolide una misma empresa, persona, tipo, periodo, fecha, presupuesto y escenario.");
            var hashSource=RhNomFuentesActuales.Hash(source);
            if(sources.TryGetValue(source.CodigoRelacionCargo,out var previous)&&previous!=hashSource)
                throw new RhNomError("FUENTES_CARGO_DISTINTAS","Un mismo cargo tiene capturas de contexto diferentes; recapture una base consistente.");
            sources[source.CodigoRelacionCargo]=hashSource;
            foreach(var p in replay.Actual.Simulacion.ProyeccionConceptos?.Partidas??[])
                rows.Add(new(run.IdRun,run.ModeloId,run.VersionModelo,run.HashDatos,doc.Modelo.Hash,source.CodigoRelacionCargo,hashSource,p));
            foreach(var decision in replay.Actual.Simulacion.AccionesControl??[])
                rows.Add(new(run.IdRun,run.ModeloId,run.VersionModelo,run.HashDatos,doc.Modelo.Hash,source.CodigoRelacionCargo,hashSource,null,decision));
        }
        var destinations=rows.GroupBy(Destino)
            .OrderBy(g=>g.Key.CodigoConcepto).ThenBy(g=>g.Key.FrecuenciaId).ThenBy(g=>g.Key.TipoMovimiento,StringComparer.Ordinal)
            .Select(g=>new RhNomDestinoConsolidado(g.Key.CodigoConcepto,g.Key.FrecuenciaId,g.Key.TipoMovimiento,
                g.Count()==1?"DESTINO_UNICO":g.Select(r=>r.CodigoRelacionCargo).Distinct().Count()>1?"CONFLICTO_CARGOS_SIN_SEGMENTOS":"CONFLICTO_DESTINO_REPETIDO",g.ToList())).ToList();
        return new(company,context!.CodigoTipoNomina,context.CodigoPeriodo,context.FechaNomina,context.CodigoPresupuesto,context.CodigoPersona,scenario!,destinations,rows.Any(o=>o.DecisionControl is not null)?VersionDecisiones:null);
    }
}
public sealed class RhNomConsolidacionHandler(ConnectionDB db,IConfiguration config)
{
    public Task<ResultDto<RhNomConsolidacion>> Handle(RhNomConsolidarQuery q,int company,int actor,CancellationToken ct)=>RhNomCfgDb.Resultado(async()=>
    {
        var ids=RhNomConsolidaciones.Validar(q);var details=new List<RhNomCorridaDetalle>();
        foreach(var id in ids)
        {
            var result=await new RhNomCorridasHandler(db,config).Detalle(new(id,"ACTUAL"),company,actor,ct);
            if(!result.IsValid||result.Data is null)return new ResultDto<RhNomConsolidacion>(default!){IsValid=false,Message=result.Message};
            details.Add(result.Data);
        }
        var consolidated=RhNomConsolidaciones.Preparar(details,company,actor,ct);
        return new ResultDto<RhNomConsolidacion>(consolidated){IsValid=true,Message=consolidated.TieneConflictos?"Diagnostico completado con destinos en conflicto. No se sumaron ni aplicaron movimientos.":"Destinos unicos identificados. No autoriza ni aplica movimientos."};
    });
}
public sealed partial class RhNominaController
{
    [HttpPost("corridasConsolidarDiagnostico")][RequestSizeLimit(16384)]
    public Task<IActionResult> Consolidate(RhNomConsolidarQuery q)=>RunAsync("rh.nomina.simular",(e,a,ct)=>new RhNomConsolidacionHandler(db,config).Handle(q,e,a,ct),"rh.nomina.fuentes.datos");
}
