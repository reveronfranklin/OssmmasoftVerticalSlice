using Microsoft.AspNetCore.Mvc;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public static class RhNomPrenominasReintentos
{
    public const string Version="REINTENTO_PRENOMINA_V1";
    public static RhNomCasoPrenomina Caso(RhNomLotePrenomina lot)
    {
        var errors=lot.Documento.Casos.Where(c=>c.Estado=="ERROR").ToList();
        if(lot.Documento.Estado!="BLOQUEADA"||errors.Count!=1||errors[0].IdRun is null||errors[0].IdCapsula is null)
            throw new RhNomError("PRENOMINA_REINTENTO","Se requiere una unica corrida fallida con captura archivada. Cambios de fuentes o configuracion requieren una nueva ejecucion.");
        return errors[0];
    }
    public static string Validar(RhNomLotePrenomina lot,RhNomCasoPrenomina item,RhNomCorrida run,RhNomCorridaDocumento doc)
    {
        var d=lot.Documento;var p=d.Contexto.Periodo;
        var technical=run.Mensaje is "No se pudo completar la simulacion guardada." or "Tiempo de simulacion agotado."
            ||run.Mensaje?.StartsWith("Fallo Oracle al guardar la corrida (ORA-",StringComparison.Ordinal)==true;
        if(Caso(lot)!=item||run.IdRun!=item.IdRun||run.CodigoEmpresa!=d.Empresa||run.Actor!=d.Actor||run.Estado!="ERROR"
            ||run.HashResultado is not null||!technical||doc.Formato!=3||doc.CodigoEmpresa!=d.Empresa||doc.Actor!=d.Actor
            ||doc.Modelo.Hash!=d.Modelo.Hash||doc.EnlaceSueldo?.ModeloSueldo.Hash!=d.ModeloSueldo.Hash
            ||doc.Captura?.Archivo.Cabecera.IdCapsula!=item.IdCapsula||RhNomCorridas.Hash(RhNomCorridas.Serializar(doc,RhNomCorridas.MaxDatosBytes))!=run.HashDatos)
            throw new RhNomError("PRENOMINA_REINTENTO_NO_TECNICO","Solo se reintenta un fallo tecnico sin resultado, conservando las mismas entradas y revisiones.");
        var captured=RhNomCapsulasPrenomina.Leer(doc.Captura!.Archivo.Capsula);var source=captured.FuentesModelo?.Fuente;
        if(source is null||source.CodigoPersona!=item.Persona||source.CodigoRelacionCargo!=item.Relacion||source.CodigoPeriodo!=p.Periodo
            ||source.CodigoTipoNomina!=p.Tipo||source.FechaNomina!=p.Fecha||source.CodigoPresupuesto!=p.Presupuesto)
            throw new RhNomError("PRENOMINA_REINTENTO_CONTEXTO","La corrida fallida no pertenece al caso y periodo del lote.");
        RhNomCorridasEnlaceSueldo.Verificar(doc,d.Empresa,d.Actor);
        return "PRE_"+lot.IdLote+"_"+item.Persona+"_RETRY_"+run.IdRun;
    }
    public static RhNomDocumentoPrenomina Aplicar(RhNomLotePrenomina lot,RhNomCasoPrenomina item,int newRun)
    {
        if(newRun<=0||newRun==item.IdRun||Caso(lot)!=item)throw new RhNomError("PRENOMINA_REINTENTO_ID","Nueva corrida positiva y distinta requerida.");
        return lot.Documento with{Estado="CALCULANDO",ErrorTemporal=null,Casos=lot.Documento.Casos.Select(c=>c==item
            ?c with{IdRun=newRun,Estado="EN_COLA",Motivo=null,HashProyeccion=null,Asignaciones=null,Deducciones=null,Neto=null,Filas=0}:c).ToList()};
    }
}
public sealed class RhNomPrenominaReintentoHandler(ConnectionDB db,IConfiguration config)
{
    public Task<ResultDto<RhNomLotePrenomina>> Recuperar(RhNomPrenominaId q,int company,int actor,CancellationToken ct)=>RhNomCfgDb.Resultado(async()=>{
        if(!config.GetValue<bool>("RhMotorNomina:PrenominaEnabled")||!config.GetValue<bool>("RhMotorNomina:CorridasEnabled")||!config.GetValue<bool>("RhMotorNomina:CorridasCapturaEnabled"))
            throw new RhNomError("PRENOMINA_DESHABILITADA","El reintento de prenomina no esta habilitado.");
        await using var cn=await RhNomCfgDb.Abrir(db,ct);var lot=await RhNomPrenominaDb.Leer(cn,company,actor,q.IdLote,null,ct)??throw new RhNomError("PRENOMINA_NO_EXISTE","Lote no encontrado.");RhNomPrenominas.VersionEsperada(lot,q.VersionEsperada);
        using var tx=cn.BeginTransaction();await RhNomPrenominaDb.Reservar(cn,tx,lot,ct);var item=RhNomPrenominasReintentos.Caso(lot);
        await using var work=await RhNomCfgDb.Abrir(db,ct);var previous=await RhNomCorridasDb.Leer(work,company,actor,item.IdRun!.Value,ct);var text=await RhNomCorridasDb.Documento(work,previous,"ENTRADA",ct)??throw new RhNomError("PRENOMINA_DOCUMENTO","La corrida no tiene entradas archivadas.");
        var doc=RhNomCorridas.Leer(text,previous.HashDatos,company,actor);var key=RhNomPrenominasReintentos.Validar(lot,item,previous,doc);
        var hash=RhNomRecalculosHistoricos.Hash(new{RhNomPrenominasReintentos.Version,Empresa=company,Actor=actor,Lote=lot.IdLote,ReintentoDe=previous.IdRun,previous.HashDatos});
        var existing=await RhNomCorridasDb.PorClave(work,company,actor,key,ct);
        if(existing is not null&&(existing.HashSolicitud!=hash||existing.HashDatos!=previous.HashDatos))throw new RhNomError("CLAVE_REUTILIZADA","El reintento identifica otro contenido.");
        var id=existing?.IdRun??await RhNomCorridasDb.Crear(work,company,actor,key,hash,doc,ct);
        await RhNomPrenominaDb.Guardar(cn,lot,RhNomPrenominasReintentos.Aplicar(lot,item,id),ct,tx);await tx.CommitAsync(ct);RhNomPrenominaIncidencias.Limpiar(company,actor,q.IdLote);
        return new ResultDto<RhNomLotePrenomina>((await RhNomPrenominaDb.Leer(cn,company,actor,q.IdLote,null,ct))!){IsValid=true,Message="Reintento encolado desde las mismas entradas congeladas. Los casos completados se conservaron; la corrida anterior permanece archivada."};
    });
}
public sealed partial class RhNominaController
{
    [HttpPost("prenominaReintentar")][RequestSizeLimit(8192)]
    public Task<IActionResult> PrenominaRetry(RhNomPrenominaId q)=>RunAsync("rh.nomina.simular",(e,a,ct)=>new RhNomPrenominaReintentoHandler(db,config).Recuperar(q,e,a,ct),"rh.nomina.fuentes.datos","rh.nomina.configuracion.editar");
}
