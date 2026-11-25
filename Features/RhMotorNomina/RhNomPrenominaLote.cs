using System.Data;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record RhNomCrearPrenomina(string ClaveSolicitud,int ModeloId,int Version,int ModeloSueldoId,int VersionSueldo,int CodigoPeriodo,List<RhNomSelectorPrenomina> RetirosPrevios);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record RhNomPrenominaId(int IdLote,int VersionEsperada=0);
public record RhNomCasoPrenomina(int Persona,int Relacion,string Nombre,string Estado,string? Motivo,int? IdCapsula=null,int? IdRun=null,string? HashProyeccion=null,string? Asignaciones=null,string? Deducciones=null,string? Neto=null,int Filas=0);
public record RhNomDocumentoPrenomina(string VersionMetodo,int Empresa,int Actor,RhNomCrearPrenomina Solicitud,RhNomModeloVersion Modelo,RhNomModeloVersion ModeloSueldo,RhNomContextoPrenomina Contexto,string Estado,List<RhNomCasoPrenomina> Casos,int? UsuarioAuditoria=null,string? ErrorTemporal=null)
{
    public bool TemporalGenerado=>Estado=="GUARDADA";
    public bool MovimientosOriginalesModificados=>false;
    public bool PagosAutorizados=>false;
    public int Vacantes=>Contexto.Personas.Count(p=>p.Persona is null or <=0);
    public int Completados=>Casos.Count(c=>c.Estado=="CALCULADO");
    public int Excluidos=>Casos.Count(c=>c.Estado=="EXCLUIDO");
    public string? Asignaciones=>Total(c=>c.Asignaciones);
    public string? Deducciones=>Total(c=>c.Deducciones);
    public string? Neto=>Total(c=>c.Neto);
    string? Total(Func<RhNomCasoPrenomina,string?> get){if(Casos.Any(c=>c.Estado is not ("CALCULADO" or "EXCLUIDO")))return null;var v=RhNomTotalesPrenomina.Exact.Zero;foreach(var c in Casos.Where(c=>c.Estado=="CALCULADO"))v=v.Add(RhNomTotalesPrenomina.Exact.Parse(get(c)!));return v.Text();}
}
public record RhNomLotePrenomina(int IdLote,int Version,string HashSolicitud,RhNomDocumentoPrenomina Documento);
public static class RhNomPrenominas
{
    public const string Version="LOTE_PRENOMINA_V1";
    public static List<RhNomCasoPrenomina> Poblacion(RhNomContextoPrenomina context)
    {
        var p=context.Periodo;if(p.Clase!="N"||p.Cierre is not null||p.Precierre is not null||p.Presupuesto<=0)throw new RhNomError("PRENOMINA_PERIODO","Seleccione un periodo ordinario sin precierre ni cierre y con presupuesto actual.");
        var result=new List<RhNomCasoPrenomina>();
        foreach(var group in context.Personas.Where(x=>x.Persona>0).GroupBy(x=>x.Persona!.Value).OrderBy(x=>x.Key)){
            var person=group.First();string? exclusion=person.Estado is not ("A" or "I" or "R")?"ESTADO_PERSONAL_NO_ELEGIBLE":person.Relacion is null or <=0?"RELACION_INVALIDA":null;
            var blocking=group.Count()!=1?"MULTIPLES_RELACIONES":person.Coincidencias!=1&&exclusion is null?"VISTA_PERSONAL_NO_UNICA":person.Estado=="A"&&!person.Vigente?"SUELDO_RELACION_NO_VIGENTE":null;
            result.Add(new(group.Key,person.Relacion??0,person.Nombre??group.Key.ToString(),blocking is not null?"ERROR":exclusion is not null?"EXCLUIDO":"PENDIENTE",blocking??exclusion));
        }
        if(result.Count is <1 or >2000)throw new RhNomError("PRENOMINA_POBLACION","Se requieren entre 1 y 2000 personas asignadas; no se trunca.");return result;
    }
    public static string Estado(List<RhNomCasoPrenomina> cases)=>cases.Any(c=>c.Estado=="ERROR")?"BLOQUEADA":cases.All(c=>c.Estado is "CALCULADO" or "EXCLUIDO")?cases.Any(c=>c.Estado=="CALCULADO")?"LISTA":"BLOQUEADA":"CALCULANDO";
    public static void VersionEsperada(RhNomLotePrenomina lote,int version){if(version!=lote.Version)throw new RhNomError("PRENOMINA_VERSION","El lote cambio; recargue antes de continuar.");}
}
public sealed class RhNomPrenominaHandler(ConnectionDB db,IConfiguration config)
{
    void Enabled(){if(!config.GetValue<bool>("RhMotorNomina:PrenominaEnabled"))throw new RhNomError("PRENOMINA_DESHABILITADA","La prenomina por periodo no esta habilitada.");}
    public Task<ResultDto<RhNomLotePrenomina>> Crear(RhNomCrearPrenomina q,int company,int actor,CancellationToken ct)=>RhNomCfgDb.Resultado(async()=>{
        Enabled();RhNomArchivoCapsulas.Alcance(company,actor);if(q is null||q.ModeloId<=0||q.Version<=0||q.ModeloSueldoId<=0||q.VersionSueldo<=0||q.CodigoPeriodo<=0)throw new RhNomError("PRENOMINA_SOLICITUD","Modelo, revisiones y periodo positivos requeridos.");
        RhNomArchivoCapsulas.Clave(q.ClaveSolicitud);q=q with{RetirosPrevios=RhNomPrenominasProyeccion.Selectores(q.RetirosPrevios)};var hash=RhNomRecalculosHistoricos.Hash(new{RhNomPrenominas.Version,Empresa=company,Actor=actor,Solicitud=q});
        await using var cn=await RhNomCfgDb.Abrir(db,ct);var prev=await RhNomPrenominaDb.Leer(cn,company,actor,null,q.ClaveSolicitud,ct);if(prev is not null){if(prev.HashSolicitud!=hash)throw new RhNomError("CLAVE_REUTILIZADA","La clave ya corresponde a otra prenomina.");return Ok(prev);}
        var model=await RhNomCfgDb.Revision(cn,company,q.ModeloId,q.Version,ct);var salary=await RhNomCfgDb.Revision(cn,company,q.ModeloSueldoId,q.VersionSueldo,ct);
        if(model.Escenario!="RECALCULA_VARIACIONES"||salary.Escenario!="ADJUNTAR_CARGO"||model.CodigoTipoNomina!=salary.CodigoTipoNomina)throw new RhNomError("PRENOMINA_MODELOS","Revisiones de recalculo y sueldo del mismo tipo requeridas.");
        RhNomContextoPrenomina context;using(var tx=cn.BeginTransaction(IsolationLevel.Serializable)){context=await RhNomPrenominaDb.Contexto(cn,company,model.CodigoTipoNomina,q.CodigoPeriodo,ct);await tx.RollbackAsync(ct);}
        var cases=RhNomPrenominas.Poblacion(context);var doc=new RhNomDocumentoPrenomina(RhNomPrenominas.Version,company,actor,q,model,salary,context,RhNomPrenominas.Estado(cases),cases);
        var id=await RhNomPrenominaDb.Crear(cn,company,actor,hash,doc,ct);return Ok((await RhNomPrenominaDb.Leer(cn,company,actor,id,null,ct))!);
    });
    static ResultDto<RhNomLotePrenomina> Ok(RhNomLotePrenomina lote)=>new(lote){IsValid=true,Message="Prenomina por periodo; movimientos originales conservados y cierre no autorizado."};
    public Task<ResultDto<RhNomLotePrenomina>> Get(RhNomPrenominaId q,int company,int actor,CancellationToken ct)=>RhNomCfgDb.Resultado(async()=>{Enabled();await using var cn=await RhNomCfgDb.Abrir(db,ct);return Ok(RhNomPrenominaIncidencias.Mostrar(await Require(cn,q,company,actor,ct)));});
    static async Task<RhNomLotePrenomina> Require(Oracle.ManagedDataAccess.Client.OracleConnection cn,RhNomPrenominaId q,int company,int actor,CancellationToken ct){if(q is null||q.IdLote<=0)throw new RhNomError("PRENOMINA_ID","Indique lote positivo.");return await RhNomPrenominaDb.Leer(cn,company,actor,q.IdLote,null,ct)??throw new RhNomError("PRENOMINA_NO_EXISTE","Lote no encontrado en el alcance del usuario.");}
    public Task<ResultDto<RhNomLotePrenomina>> Cancelar(RhNomPrenominaId q,int company,int actor,CancellationToken ct)=>RhNomCfgDb.Resultado(async()=>{
        Enabled();await using var cn=await RhNomCfgDb.Abrir(db,ct);var lot=await Require(cn,q,company,actor,ct);RhNomPrenominas.VersionEsperada(lot,q.VersionEsperada);
        if(lot.Documento.Estado is not ("CALCULANDO" or "LISTA" or "BLOQUEADA" or "TEMP_ERROR"))throw new RhNomError("PRENOMINA_CANCELAR","El lote ya esta finalizado o guardando; no se cancela.");
        using(var tx=cn.BeginTransaction()){await RhNomPrenominaDb.Reservar(cn,tx,lot,ct);await RhNomPrenominaDb.Guardar(cn,lot,lot.Documento with{Estado="CANCELADA"},ct,tx);await tx.CommitAsync(ct);}return Ok((await RhNomPrenominaDb.Leer(cn,company,actor,q.IdLote,null,ct))!);
    });
    public Task<ResultDto<RhNomLotePrenomina>> Avanzar(RhNomPrenominaId q,int company,int actor,CancellationToken ct)=>RhNomCfgDb.Resultado(async()=>{
        Enabled();await using var cn=await RhNomCfgDb.Abrir(db,ct);var lote=await Require(cn,q,company,actor,ct);RhNomPrenominas.VersionEsperada(lote,q.VersionEsperada);var d=lote.Documento;
        if(d.Estado!="CALCULANDO")return Ok(lote);
        using var lease=cn.BeginTransaction();await RhNomPrenominaDb.Reservar(cn,lease,lote,ct);
        var cases=d.Casos.ToList();var ix=cases.FindIndex(c=>c.Estado is "PENDIENTE" or "EN_COLA");if(ix<0)throw new RhNomError("PRENOMINA_ESTADO","No hay caso pendiente.");var item=cases[ix];
        try{
            var prefix="PRE_"+lote.IdLote+"_"+item.Persona;var person=d.Contexto.Personas.Single(p=>p.Persona==item.Persona);var recalc=person.Estado=="A";
            if(item.IdCapsula is null){
                ResultDto<RhNomResumenCaptura> captured=recalc?await new RhNomCapturaParaModeloHandler(db,config).Crear(new(prefix+"_CAP",d.Solicitud.ModeloId,d.Solicitud.Version,d.Contexto.Periodo.Periodo,item.Persona,item.Relacion,RhNomPresupuestos.PoliticaContrato),company,actor,ct)
                    :await new RhNomCapturaPrenominaHandler(db,config).Crear(new(prefix+"_CAP",d.Contexto.Periodo.Tipo,d.Contexto.Periodo.Periodo,item.Persona,RhNomPresupuestos.PoliticaContrato),company,actor,ct);
                if(!captured.IsValid||captured.Data is null)throw new RhNomError("PRENOMINA_CAPTURA",captured.Message??"Captura rechazada.");item=item with{IdCapsula=captured.Data.Archivo.IdCapsula};
            }
            RhNomProyeccionPrenomina? projection=null;
            if(!recalc){var archive=await RhNomArchivoCapsulasDb.Leer(cn,company,actor,item.IdCapsula!.Value,ct);projection=RhNomPrenominasProyeccion.Conservar(archive,d.Contexto,item.Relacion);}
            else{
                if(item.IdRun is null){
                    var run=await new RhNomCorridaEnlaceSueldoHandler(db,config).Crear(new(prefix+"_RUN",d.Solicitud.ModeloId,d.Solicitud.Version,item.IdCapsula!.Value,d.Solicitud.ModeloSueldoId,d.Solicitud.VersionSueldo,RhNomEnlacesSueldo.Politica),company,actor,ct);
                    if(!run.IsValid||run.Data is null)throw new RhNomError("PRENOMINA_CORRIDA",run.Message??"Corrida rechazada.");item=item with{IdRun=run.Data.IdRun,Estado="EN_COLA"};
                }
                var header=await RhNomCorridasDb.Leer(cn,company,actor,item.IdRun!.Value,ct);
                if(header.Estado is "ERROR" or "CANCELADO")throw new RhNomError("PRENOMINA_CORRIDA",header.Mensaje??"La corrida termino sin resultado valido.");
                if(header.Estado=="COMPLETADO"){
                    var text=await RhNomCorridasDb.Documento(cn,header,"ENTRADA",ct)??throw new RhNomError("PRENOMINA_DOCUMENTO","Falta entrada de corrida.");var run=RhNomCorridas.Leer(text,header.HashDatos,company,actor);projection=RhNomPrenominasProyeccion.Proyectar(run,d.Contexto,d.Solicitud.RetirosPrevios,ct);
                }
            }
            if(projection is not null){if(!projection.AptaParaTemporal)throw new RhNomError("PRENOMINA_PROYECCION",string.Join(", ",projection.Errores));item=item with{Estado="CALCULADO",HashProyeccion=RhNomRecalculosHistoricos.Hash(projection),Asignaciones=projection.Asignaciones,Deducciones=projection.Deducciones,Neto=projection.Neto,Filas=projection.Filas.Count(f=>f.Incluida),Motivo=null};}
        }catch(RhNomError ex){item=item with{Estado="ERROR",Motivo=ex.Message};}
        cases[ix]=item;await RhNomPrenominaDb.Guardar(cn,lote,d with{Casos=cases,Estado=RhNomPrenominas.Estado(cases)},ct,lease);await lease.CommitAsync(ct);return Ok((await RhNomPrenominaDb.Leer(cn,company,actor,lote.IdLote,null,ct))!);
    });
}
public sealed partial class RhNominaController
{
    [HttpPost("prenominaCrear")][RequestSizeLimit(16384)]
    public Task<IActionResult> PrenominaCreate(RhNomCrearPrenomina q)=>RunAsync("rh.nomina.simular",(e,a,ct)=>new RhNomPrenominaHandler(db,config).Crear(q,e,a,ct),"rh.nomina.fuentes.datos","rh.nomina.configuracion.editar");
    [HttpPost("prenominaConsultar")]
    public Task<IActionResult> PrenominaGet(RhNomPrenominaId q)=>RunAsync("rh.nomina.simular",(e,a,ct)=>new RhNomPrenominaHandler(db,config).Get(q,e,a,ct),"rh.nomina.fuentes.datos");
    [HttpPost("prenominaCancelar")]
    public Task<IActionResult> PrenominaCancel(RhNomPrenominaId q)=>RunAsync("rh.nomina.simular",(e,a,ct)=>new RhNomPrenominaHandler(db,config).Cancelar(q,e,a,ct),"rh.nomina.fuentes.datos","rh.nomina.configuracion.editar");
    [HttpPost("prenominaAvanzar")]
    public Task<IActionResult> PrenominaAdvance(RhNomPrenominaId q)=>RunAsync("rh.nomina.simular",(e,a,ct)=>new RhNomPrenominaHandler(db,config).Avanzar(q,e,a,ct),"rh.nomina.fuentes.datos","rh.nomina.configuracion.editar");
}
public record RhNomPrenominaPersona(int IdLote,int CodigoPersona);
public sealed class RhNomPrenominaDetalleHandler(ConnectionDB db,IConfiguration config)
{
    public Task<ResultDto<RhNomProyeccionPrenomina>> Get(RhNomPrenominaPersona q,int company,int actor,CancellationToken ct)=>RhNomCfgDb.Resultado(async()=>{
        if(!config.GetValue<bool>("RhMotorNomina:PrenominaEnabled"))throw new RhNomError("PRENOMINA_DESHABILITADA","Prenomina no habilitada.");await using var cn=await RhNomCfgDb.Abrir(db,ct);
        var lot=await RhNomPrenominaDb.Leer(cn,company,actor,q.IdLote,null,ct)??throw new RhNomError("PRENOMINA_NO_EXISTE","Lote no encontrado.");var item=lot.Documento.Casos.SingleOrDefault(c=>c.Persona==q.CodigoPersona&&c.Estado=="CALCULADO")??throw new RhNomError("PRENOMINA_PERSONA","Persona calculada no encontrada en este lote.");
        return new ResultDto<RhNomProyeccionPrenomina>(await RhNomPrenominaTemporal.Proyeccion(cn,lot,item,ct)){IsValid=true,Message="Resultado reproducido desde fuentes y revisiones archivadas."};
    });
}
public sealed partial class RhNominaController
{
    [HttpPost("prenominaDetallePersona")]
    public Task<IActionResult> PrenominaDetail(RhNomPrenominaPersona q)=>RunAsync("rh.nomina.simular",(e,a,ct)=>new RhNomPrenominaDetalleHandler(db,config).Get(q,e,a,ct),"rh.nomina.fuentes.datos");
}
