using Microsoft.AspNetCore.Mvc;
using System.Globalization;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public sealed class RhNomAplicacionHandler(ConnectionDB db,IConfiguration config)
{
    public Task<ResultDto<RhNomReciboFinanciero>> Ejecutar(RhNomEjecutarPlan q,int company,int actor,CancellationToken ct)=>RhNomCfgDb.Resultado(async()=>
    {
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);deadline.CancelAfter(TimeSpan.FromSeconds(30));ct=deadline.Token;
        var requestHash=RhNomTransaccion.Validar(q,company,actor);await using var cn=await RhNomCfgDb.Abrir(db,ct);
        var existing=await RhNomTransaccionDb.Solicitud(cn,company,actor,q.ClaveSolicitud,requestHash,q.IdPlan,ct);
        if(existing is not null)return new ResultDto<RhNomReciboFinanciero>(existing){IsValid=true,Message="Operacion original confirmada; no se ejecutaron nuevos cambios."};
        if(!config.GetValue<bool>("RhMotorNomina:AplicacionEnabled"))throw new RhNomError("APLICACION_DESHABILITADA","La ejecucion financiera no esta habilitada en este ambiente.");
        RhNomTransaccion.PerfilDeProveedores(q.IdRevertido is not null);
        var saved=await RhNomPlanesGuardadosDb.Leer(cn,await RhNomPlanesGuardadosDb.Cabecera(cn,company,actor,q.IdPlan,ct),company,actor,ct);
        if(saved.Cabecera.HashDocumento!=q.HashPlanEsperado)throw new RhNomError("HASH_PLAN_DISTINTO","El plan no coincide con el hash esperado.");
        var context=saved.Diagnostico.Consolidacion;var model=RhNomTransaccion.ModeloUnico(context);var reversal=q.IdRevertido is not null;
        if(!reversal)RhNomPoliticaOperacion.PermitirAutomatica(config,context.CodigoTipoNomina);
        var receipt=await RhNomCfgDb.Guardar(cn,async tx=>
        {
            var started=await RhNomTransaccionDb.Iniciar(cn,saved,actor,reversal,ct);
            var retry=await RhNomTransaccionDb.Solicitud(cn,company,actor,q.ClaveSolicitud,requestHash,q.IdPlan,ct);if(retry is not null)return retry;
            var events=await RhNomLibroFinancieroDb.Eventos(cn,company,context.CodigoTipoNomina,context.CodigoPersona,ct);
            var state=RhNomLibroFinanciero.Reconstruir(events,company,context.CodigoTipoNomina,context.CodigoPersona,ct);
            var capture=await RhNomMovimientosPlanDb.Leer(cn,company,context.CodigoTipoNomina,context.CodigoPersona,ct);
            if(capture.HayAmbitoAjeno)throw new RhNomError("INDICE_UNICO_OTRO_AMBITO","Hay movimientos de otro ambito; operacion bloqueada.");
            var changes=new List<RhNomCambioAplicado>();
            if(!reversal)
            {
                var publication=await RhNomEstadosDb.Leer(cn,company,model.Modelo,model.Version,ct);
                RhNomTransaccion.Publicacion(publication,model.Modelo,model.Version,model.Hash,context.FechaNomina);
                await RevalidarEvidencia(cn,publication,company,ct);
                var details=new List<RhNomCorridaDetalle>();
                foreach(var id in context.Destinos.SelectMany(d=>d.Origenes).Select(o=>o.IdRun).Distinct().Order())
                {
                    var run=await RhNomCorridasDb.Leer(cn,company,actor,id,ct);
                    var text=await RhNomCorridasDb.Documento(cn,run,"ENTRADA",ct)??throw new RhNomError("CORRIDA_SIN_DATOS","Corrida sin entradas.");
                    var doc=RhNomCorridas.Leer(text,run.HashDatos,company,actor);var s=doc.FuenteActual??throw new RhNomError("FUENTE_ACTUAL_REQUERIDA","Fuente actual requerida.");
                    var fresh=await RhNomFuentesActualesHandler.Capturar(cn,new(doc.Modelo.ModeloId,doc.Modelo.Version,s.CodigoPeriodo,s.FechaNomina.ToString("yyyy-MM-dd'T'HH:mm:ss",CultureInfo.InvariantCulture),s.CodigoPresupuesto,s.CodigoPersona,s.CodigoRelacionCargo,null,doc.PoliticaPresupuesto??"",doc.MotivoPresupuesto),company,ct);
                    if(!fresh.IsValid||fresh.Data is null)throw new RhNomError("RECALCULO_TX_INVALIDO",fresh.Message);
                    RhNomTransaccion.FuentesIguales(doc,fresh.Data);
                    var catalog=await RhNomConceptosCatalogoHandler.Lote(cn,company,context.CodigoTipoNomina,(doc.Modelo.Configuracion.ReglasConcepto??[]).Select(RhNomReglasConcepto.Solicitud).ToList(),ct,doc.Modelo.Configuracion.AccionesControl?.Select(RhNomAccionesControl.Solicitud).ToList());
                    RhNomTransaccion.RevalidarCatalogo(doc.Modelo.Configuracion,catalog,company,context.CodigoTipoNomina);
                    details.Add(new(run,doc,null));
                }
                var repeated=RhNomConsolidaciones.Preparar(details,company,actor,ct);
                if(RhNomRecalculosHistoricos.Hash(repeated)!=RhNomRecalculosHistoricos.Hash(context)||RhNomRecalculosHistoricos.Hash(capture.Imagenes.OrderBy(r=>r.Id).ToArray())!=saved.Diagnostico.HashMovimientos)
                    throw new RhNomError("PLAN_DESACTUALIZADO","La proyeccion o las imagenes actuales cambiaron; guarde un plan nuevo.");
                var plan=RhNomPlanesFinancieros.Preparar(repeated,capture.Imagenes,state.Propiedades,ct);
                if(plan.TieneBloqueos)throw new RhNomError("PLAN_FINANCIERO_BLOQUEADO","La aplicacion contiene destinos bloqueados o movimientos no propios.");
                foreach(var step in plan.Pasos)
                {
                    var id=step.Accion=="PROPONER_CREACION"?await RhNomTransaccionDb.NuevoId(cn,ct):step.Antes?.Id??0;
                    var change=RhNomCambiosFinancieros.Aplicar(step,context,id,actor,started.Clock);if(change is not null)changes.Add(change);
                }
            }
            else
            {
                var original=events.SingleOrDefault(e=>e.IdEvento==q.IdRevertido)??throw new RhNomError("EVENTO_NO_ENCONTRADO","Aplicacion a revertir no encontrada.");
                if(original.Documento.IdPlan!=q.IdPlan||original.Documento.HashPlan!=q.HashPlanEsperado)throw new RhNomError("REVERSION_OTRO_PLAN","La aplicacion no corresponde al plan indicado.");
                // Validate actual images against the current owned image, including all audit/extras.
                foreach(var old in original.Documento.Cambios)
                {
                    var id=(old.Despues??old.Antes)!.Id;var current=capture.Imagenes.SingleOrDefault(r=>r.Id==id);
                    if(old.Despues is not null&&(current is null||!state.Propiedades.Any(p=>p.UltimaImagen.Id==id&&p.HashUltimaImagen==RhNomPlanesFinancieros.Hash(current))))
                        throw new RhNomError("REVERSION_MOV_CAMBIO","Un movimiento propio cambio o desaparecio despues de aplicarse.");
                    if(old.Despues is null&&current is not null)throw new RhNomError("REVERSION_ID_OCUPADO","La identidad retirada esta ocupada.");
                    changes.Add(RhNomCambiosFinancieros.Revertir(old,current,actor,started.Clock));
                }
                var reversePlan=RhNomPlanesFinancieros.Revertir(changes.Select(c=>new RhNomCambioAplicado(c.Accion=="CREAR"?"RETIRAR":c.Accion=="RETIRAR"?"CREAR":"ACTUALIZAR",c.Despues,c.Antes)).ToList(),capture.Imagenes,ct);
                if(reversePlan.TieneBloqueos)throw new RhNomError("REVERSION_BLOQUEADA","Las imagenes o claves fisicas bloquean la reversion.");
            }
            if(changes.Count==0)throw new RhNomError("PLAN_SIN_CAMBIOS","El plan no requiere cambios financieros; no se registra una aplicacion vacia.");
            var d=new RhNomLibroDocumento(RhNomLibroFinanciero.Version,company,context.CodigoTipoNomina,context.CodigoPersona,context.CodigoPeriodo,context.FechaNomina,context.CodigoPresupuesto,actor,q.IdPlan,q.HashPlanEsperado,reversal?"REVERSION":"APLICACION",q.IdRevertido,changes,RhNomValidaciones.Motivo(q.Motivo));
            RhNomTransaccion.CapacidadLibro(events,d);
            var proposed=new RhNomLibroEvento(started.Id,state.UltimoEvento,state.UltimoHash,started.Clock,RhNomLibroFinanciero.Hash(d),"",d);proposed=proposed with{HashEvento=RhNomLibroFinanciero.Huella(proposed)};
            RhNomLibroFinanciero.Reconstruir(events.Append(proposed).ToList(),company,context.CodigoTipoNomina,context.CodigoPersona,ct);
            foreach(var change in changes){ct.ThrowIfCancellationRequested();await RhNomTransaccionDb.Cambiar(cn,change,ct);}
            var actual=await RhNomMovimientosPlanDb.Leer(cn,company,context.CodigoTipoNomina,context.CodigoPersona,ct);
            RhNomCambiosFinancieros.VerificarResultado(capture.Imagenes,changes,actual);
            // Read back actual NUMBER/audit images instead of signing proposed strings.
            var actualChanges=changes.Select(c=>c.Despues is null?c:c with{Despues=actual.Imagenes.Single(r=>r.Id==c.Despues.Id)}).ToList();d=d with{Cambios=actualChanges};
            RhNomTransaccion.CapacidadLibro(events,d);
            var confirmed=proposed with{Documento=d,HashDocumento=RhNomLibroFinanciero.Hash(d)};confirmed=confirmed with{HashEvento=RhNomLibroFinanciero.Huella(confirmed)};
            RhNomLibroFinanciero.Reconstruir(events.Append(confirmed).ToList(),company,context.CodigoTipoNomina,context.CodigoPersona,ct);
            await RhNomTransaccionDb.Registrar(cn,q,requestHash,confirmed,ct);
            return new RhNomReciboFinanciero(confirmed.IdEvento,q.IdPlan,requestHash,confirmed.HashEvento,confirmed.FechaBd);
        },ct);
        return new ResultDto<RhNomReciboFinanciero>(receipt){IsValid=true,Message="Operacion confirmada atomicamente con su evento financiero."};
    });
    static async Task RevalidarEvidencia(OracleConnection cn,RhNomEstadoRevision state,int company,CancellationToken ct)
    {
        if(state.AutorValidacion is not >0||state.Evidencia is null)throw new RhNomError("AUTOR_VALIDACION_REQUERIDO","Falta autor de la evidencia publicada.");
        var model=await RhNomCfgDb.Revision(cn,company,state.ModeloId,state.Version,ct);
        var cases=new List<(RhNomCorrida,RhNomCorridaDocumento,RhNomCorridaResultado)>();
        foreach(var proof in state.Evidencia.Pruebas)
        {
            var run=await RhNomCorridasDb.Leer(cn,company,state.AutorValidacion.Value,proof.IdRun,ct);
            var input=await RhNomCorridasDb.Documento(cn,run,"ENTRADA",ct)??throw new RhNomError("EVIDENCIA_SIN_ENTRADA","Prueba sin entrada inmutable.");
            var output=await RhNomCorridasDb.Documento(cn,run,"RESULTADO",ct)??throw new RhNomError("EVIDENCIA_SIN_RESULTADO","Prueba sin resultado inmutable.");
            var doc=RhNomCorridas.Leer(input,run.HashDatos,company,state.AutorValidacion.Value);
            var replay=RhNomCorridas.Evaluar(doc,ct);
            if(!replay.IsValid||RhNomCorridas.Hash(RhNomCorridas.Serializar(replay,RhNomCorridas.MaxResultadoBytes))!=run.HashResultado)
                throw new RhNomError("EVIDENCIA_NO_REPRODUCIBLE","La prueba publicada no se reproduce con el motor actual.");
            // Documento already verifies the stored result; replay is independently compared.
            if(RhNomCorridas.Hash(output)!=run.HashResultado)throw new RhNomError("EVIDENCIA_ALTERADA","Resultado publicado alterado.");
            cases.Add((run,doc,replay));
        }
        var fresh=RhNomValidaciones.Capturar(model,cases,company,state.AutorValidacion.Value,aplicacionDisponible:true);
        if(!fresh.Publicable||fresh.Hash!=state.Evidencia.Hash)throw new RhNomError("EVIDENCIA_FINANCIERA_DESACTUALIZADA","La evidencia publicada cambio o no acredita paridad integral.");
    }

}
public sealed partial class RhNominaController
{
    [HttpPost("planesEjecutar")][RequestSizeLimit(4096)]
    public Task<IActionResult> ExecuteFinancialPlan(RhNomEjecutarPlan q)=>RunAsync(q.IdRevertido is null?"rh.nomina.ejecutar":"rh.nomina.revertir",(e,a,ct)=>new RhNomAplicacionHandler(db,config).Ejecutar(q,e,a,ct),"rh.nomina.fuentes.datos");
}
