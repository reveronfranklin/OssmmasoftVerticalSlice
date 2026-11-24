using System.Data;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public static class RhNomPrenominaTemporal
{
    static OracleCommand Cmd(OracleConnection cn,string operation)=>RhNomPrenominaDb.Cmd(cn,"RH.RH_PKG_NOM_PRE_TMP."+operation);
    static void Scope(OracleCommand c,RhNomLotePrenomina lot){var d=lot.Documento;RhNomCfgDb.Add(c,"p_EMPRESA",OracleDbType.Int32,d.Empresa);RhNomCfgDb.Add(c,"p_TIPO",OracleDbType.Int32,d.Contexto.Periodo.Tipo);RhNomCfgDb.Add(c,"p_PERIODO",OracleDbType.Int32,d.Contexto.Periodo.Periodo);RhNomCfgDb.Add(c,"p_ACTOR",OracleDbType.Int32,d.Actor);RhNomCfgDb.Add(c,"p_LOTE",OracleDbType.Int32,lot.IdLote);}
    public static async Task<string> HashTemporal(OracleConnection cn,RhNomDocumentoPrenomina d,CancellationToken ct)
    {
        using var c=RhNomPrenominaDb.Cmd(cn,"RH.SP_RH_NOM_PRE_FUE");RhNomCfgDb.Add(c,"p_EMPRESA",OracleDbType.Int32,d.Empresa);RhNomCfgDb.Add(c,"p_TIPO",OracleDbType.Int32,d.Contexto.Periodo.Tipo);RhNomCfgDb.Add(c,"p_PERIODO",OracleDbType.Int32,d.Contexto.Periodo.Periodo);RhNomCfgDb.Add(c,"p_ORIGEN",OracleDbType.Varchar2,"TEMPORAL");c.Parameters.Add("p_RESULT",OracleDbType.RefCursor,ParameterDirection.Output);
        using var r=await c.ExecuteReaderAsync(ct);var rows=new List<List<string?>>();while(await r.ReadAsync(ct)){
            if(rows.Count>=100000)throw new RhNomError("PRENOMINA_TEMP_LIMITE","Temporal excede el limite; no se trunca.");var values=new List<string?>();for(int i=0;i<r.FieldCount;i++)values.Add(r.IsDBNull(i)?null:r.GetFieldType(i)==typeof(DateTime)?r.GetDateTime(i).ToString("yyyy-MM-dd'T'HH:mm:ss",System.Globalization.CultureInfo.InvariantCulture):r.GetFieldType(i)==typeof(string)?r.GetString(i):r.GetOracleDecimal(i).ToString());rows.Add(values);
        }
        return RhNomRecalculosHistoricos.Hash(rows);
    }
    public static async Task<RhNomProyeccionPrenomina> Proyeccion(OracleConnection cn,RhNomLotePrenomina lot,RhNomCasoPrenomina item,CancellationToken ct)
    {
        var d=lot.Documento;RhNomProyeccionPrenomina projected;
        if(item.IdRun is {} id){var run=await RhNomCorridasDb.Leer(cn,d.Empresa,d.Actor,id,ct);if(run.Estado!="COMPLETADO")throw new RhNomError("PRENOMINA_RUN","Corrida no completada.");var text=await RhNomCorridasDb.Documento(cn,run,"ENTRADA",ct)??throw new RhNomError("PRENOMINA_RUN","Entrada ausente.");projected=RhNomPrenominasProyeccion.Proyectar(RhNomCorridas.Leer(text,run.HashDatos,d.Empresa,d.Actor),d.Contexto,d.Solicitud.RetirosPrevios,ct);}
        else projected=RhNomPrenominasProyeccion.Conservar(await RhNomArchivoCapsulasDb.Leer(cn,d.Empresa,d.Actor,item.IdCapsula!.Value,ct),d.Contexto,item.Relacion);
        if(!projected.AptaParaTemporal||RhNomRecalculosHistoricos.Hash(projected)!=item.HashProyeccion)throw new RhNomError("PRENOMINA_PROYECCION_CAMBIO","El resultado ya no coincide con el lote revisado.");return projected;
    }
    static async Task<RhNomCapsulaDoc> Original(OracleConnection cn,RhNomDocumentoPrenomina d,RhNomCasoPrenomina item,CancellationToken ct){
        if(item.IdRun is {} id){var run=await RhNomCorridasDb.Leer(cn,d.Empresa,d.Actor,id,ct);var text=await RhNomCorridasDb.Documento(cn,run,"ENTRADA",ct)??throw new RhNomError("PRENOMINA_RUN","Entrada ausente.");return RhNomCapsulasPrenomina.Leer(RhNomCorridas.Leer(text,run.HashDatos,d.Empresa,d.Actor).Captura!.Archivo.Capsula);}
        return RhNomCapsulasPrenomina.Leer((await RhNomArchivoCapsulasDb.Leer(cn,d.Empresa,d.Actor,item.IdCapsula!.Value,ct)).Capsula);
    }
    public static async Task Guardar(ConnectionDB db,OracleConnection cn,RhNomLotePrenomina lot,IConfiguration config,int? auditUser,CancellationToken ct)
    {
        var d=lot.Documento;if(d.Estado!="GUARDANDO"||RhNomPrenominas.Estado(d.Casos)!="LISTA")throw new RhNomError("PRENOMINA_NO_LISTA","Solo se guarda un lote completo sin errores.");
        var plans=new List<(RhNomCasoPrenomina Item,RhNomCapsulaDoc Original,RhNomProyeccionPrenomina Resultado)>();
        foreach(var item in d.Casos.Where(c=>c.Estado=="CALCULADO"))plans.Add((item,await Original(cn,d,item,ct),await Proyeccion(cn,lot,item,ct)));
        // Read-only snapshot and write transaction use the same Oracle identity.
        // The writer holds every existing NOWAIT/source/temporary lock. Oracle can
        // reject large heap inserts under SERIALIZABLE even without a competitor;
        // keep that isolation on the source snapshot, not on heap allocation.
        await using var source=await RhNomCfgDb.Abrir(db,ct);
        RhNomPrenominaIdentidades.Validar(await RhNomPrenominaIdentidades.Leer(cn,ct),await RhNomPrenominaIdentidades.Leer(source,ct));
        using var tx=cn.BeginTransaction(IsolationLevel.ReadCommitted);
        try{
            using(var c=Cmd(cn,"BLOQUEAR")){c.Transaction=tx;Scope(c,lot);RhNomCfgDb.Add(c,"p_REVERTIR",OracleDbType.Int32,0);c.Parameters.Add("p_HASH",OracleDbType.Varchar2,64).Direction=ParameterDirection.Output;await c.ExecuteNonQueryAsync(ct);}
            using var snapshot=source.BeginTransaction(IsolationLevel.Serializable);
            var context=await RhNomPrenominaDb.Contexto(source,d.Empresa,d.Contexto.Periodo.Tipo,d.Contexto.Periodo.Periodo,ct);
            if(RhNomPrenominasProyeccion.HashContexto(context)!=RhNomPrenominasProyeccion.HashContexto(d.Contexto))throw new RhNomError("PRENOMINA_FUENTES_CAMBIO","Cambio el periodo, personal, catalogo o frecuencias; cree una nueva prenomina.");
            var declarations=RhNomCapturasServidor.Politica(config,context.Periodo.Tipo);
            foreach(var plan in plans){
                var old=plan.Original;var request=new RhNomCapturarPrenomina("VERIFICAR",context.Periodo.Tipo,context.Periodo.Periodo,plan.Item.Persona,RhNomPresupuestos.PoliticaContrato);
                var fresh=await RhNomCapturaFuentesDb.Leer(source,snapshot,request,d.Empresa,declarations,ct);
                var raw=RhNomCapsulasPrenomina.Capturar(fresh.Ambito,old.Definicion,fresh.Movimientos,fresh.Metadatos,declarations,fresh.Periodo);
                var prior=RhNomCapsulasPrenomina.Capturar(old.Ambito,old.Definicion,new(old.Imagenes,old.HayAmbitoAjeno),old.Metadatos,old.DeclaracionesManuales,old.PeriodoCapturado);
                if(raw.Hash!=prior.Hash)throw new RhNomError("PRENOMINA_VARIACIONES_CAMBIO","Cambiaron movimientos o personal de la persona "+plan.Item.Persona+"; recalcule el periodo.");
                if(old.FuentesModelo is {} sources){
                    var inputs=await RhNomFuentesActualesHandler.LeerEntradas(source,d.Modelo,new(d.Modelo.ModeloId,d.Modelo.Version,context.Periodo.Periodo,context.Periodo.Fecha.ToString("yyyy-MM-dd'T'HH:mm:ss",System.Globalization.CultureInfo.InvariantCulture),context.Periodo.Presupuesto,plan.Item.Persona,plan.Item.Relacion,null,RhNomPresupuestos.PoliticaContrato),d.Empresa,ct);
                    var current=new RhNomFuentesModeloCapturadas(d.Modelo.ModeloId,d.Modelo.Version,d.Modelo.Hash,RhNomFuentesActuales.Version,inputs.Fuente,inputs.Entradas);
                    if(RhNomPrenominasProyeccion.HashMaterial(current)!=RhNomPrenominasProyeccion.HashMaterial(sources))throw new RhNomError("PRENOMINA_BASES_CAMBIO","Cambiaron bases, controles o parametros de la persona "+plan.Item.Persona+"; recalcule el periodo.");
                }
            }
            foreach(var plan in plans)foreach(var row in plan.Resultado.Filas.Where(f=>f.Incluida)){
                using var c=Cmd(cn,"FILA");c.Transaction=tx;RhNomCfgDb.Add(c,"p_EMPRESA",OracleDbType.Int32,d.Empresa);RhNomCfgDb.Add(c,"p_TIPO",OracleDbType.Int32,context.Periodo.Tipo);RhNomCfgDb.Add(c,"p_PERIODO",OracleDbType.Int32,context.Periodo.Periodo);RhNomCfgDb.Add(c,"p_FECHA",OracleDbType.Date,context.Periodo.Fecha);
                RhNomCfgDb.Add(c,"p_PERSONA",OracleDbType.Int32,row.Persona);RhNomCfgDb.Add(c,"p_CONCEPTO",OracleDbType.Int32,row.Concepto);RhNomCfgDb.Add(c,"p_FRECUENCIA",OracleDbType.Int32,row.Frecuencia);RhNomCfgDb.Add(c,"p_CLASE",OracleDbType.Varchar2,row.Tipo);RhNomCfgDb.Add(c,"p_MONTO",OracleDbType.Decimal,RhNomTransaccion.Numero(row.Monto));RhNomCfgDb.Add(c,"p_STATUS",OracleDbType.Varchar2,row.Estado);RhNomCfgDb.Add(c,"p_COMPLEMENTO",OracleDbType.Varchar2,row.Complemento);RhNomCfgDb.Add(c,"p_EXTRA1",OracleDbType.Varchar2,row.Extra1);RhNomCfgDb.Add(c,"p_EXTRA2",OracleDbType.Varchar2,row.Extra2);RhNomCfgDb.Add(c,"p_EXTRA3",OracleDbType.Varchar2,row.Extra3);RhNomCfgDb.Add(c,"p_ORIGEN",OracleDbType.Int32,row.MovimientoOrigen);RhNomCfgDb.Add(c,"p_USUARIO",OracleDbType.Int32,auditUser);await c.ExecuteNonQueryAsync(ct);
            }
            var hash=await HashTemporal(cn,d,ct);using(var c=Cmd(cn,"FINALIZAR")){c.Transaction=tx;Scope(c,lot);RhNomCfgDb.Add(c,"p_HASH",OracleDbType.Varchar2,hash);RhNomCfgDb.Add(c,"p_USUARIO",OracleDbType.Int32,auditUser);await c.ExecuteNonQueryAsync(ct);}
            // Journal and temporal commit together. No outer helper opens another transaction.
            await EstadoEnTransaccion(cn,tx,lot,"GUARDADA",null,ct);await tx.CommitAsync(ct);
        }catch{await tx.RollbackAsync(CancellationToken.None);throw;}
    }
    public static async Task EstadoEnTransaccion(OracleConnection cn,OracleTransaction tx,RhNomLotePrenomina lot,string state,string? error,CancellationToken ct){
        var d=lot.Documento with{Estado=state,ErrorTemporal=error};var text=RhNomCorridas.Serializar(d,2097152);using var c=RhNomPrenominaDb.Cmd(cn,"RH.RH_PKG_NOM_PRE.GUARDAR");c.Transaction=tx;RhNomPrenominaDb.Scope(c,d.Empresa,d.Actor);RhNomCfgDb.Add(c,"p_ID",OracleDbType.Int32,lot.IdLote);RhNomCfgDb.Add(c,"p_VERSION",OracleDbType.Int32,lot.Version);RhNomCfgDb.Add(c,"p_ESTADO",OracleDbType.Varchar2,state);RhNomCfgDb.Add(c,"p_DOC",OracleDbType.Clob,text);RhNomCfgDb.Add(c,"p_HASH_DOC",OracleDbType.Varchar2,RhNomCorridas.Hash(text));await c.ExecuteNonQueryAsync(ct);
    }
    public static async Task Revertir(OracleConnection cn,RhNomLotePrenomina lot,CancellationToken ct){
        if(lot.Documento.Estado!="GUARDADA")throw new RhNomError("PRENOMINA_REVERTIR","El lote no tiene temporal guardado.");using var tx=cn.BeginTransaction(IsolationLevel.ReadCommitted);try{
            string expected;using(var c=Cmd(cn,"BLOQUEAR")){c.Transaction=tx;Scope(c,lot);RhNomCfgDb.Add(c,"p_REVERTIR",OracleDbType.Int32,1);var hash=c.Parameters.Add("p_HASH",OracleDbType.Varchar2,64);hash.Direction=ParameterDirection.Output;await c.ExecuteNonQueryAsync(ct);expected=hash.Value.ToString()!;}
            if(await HashTemporal(cn,lot.Documento,ct)!=expected)throw new RhNomError("PRENOMINA_TEMP_CAMBIO","El temporal fue modificado por otro proceso; no se revierte.");
            using(var c=Cmd(cn,"REVERTIR")){c.Transaction=tx;Scope(c,lot);RhNomCfgDb.Add(c,"p_HASH",OracleDbType.Varchar2,expected);await c.ExecuteNonQueryAsync(ct);}await EstadoEnTransaccion(cn,tx,lot,"REVERTIDA",null,ct);await tx.CommitAsync(ct);
        }catch{await tx.RollbackAsync(CancellationToken.None);throw;}
    }
}
public sealed class RhNomPrenominaGuardarHandler(ConnectionDB db,IConfiguration config)
{
    public Task<ResultDto<RhNomLotePrenomina>> Solicitar(RhNomPrenominaId q,int company,int actor,bool reverse,CancellationToken ct)=>RhNomCfgDb.Resultado(async()=>{
        if(!config.GetValue<bool>("RhMotorNomina:PrenominaEnabled")||!config.GetValue<bool>("RhMotorNomina:PrenominaTemporalEnabled"))throw new RhNomError("PRENOMINA_TEMP_DESHABILITADA","El temporal de prenomina no esta habilitado.");
        await using var cn=await RhNomCfgDb.Abrir(db,ct);var lot=await RhNomPrenominaDb.Leer(cn,company,actor,q.IdLote,null,ct)??throw new RhNomError("PRENOMINA_NO_EXISTE","Lote no encontrado.");RhNomPrenominas.VersionEsperada(lot,q.VersionEsperada);
        if(reverse)await RhNomPrenominaTemporal.Revertir(cn,lot,ct);
        else if(lot.Documento.Estado=="LISTA")await RhNomPrenominaDb.Guardar(cn,lot,lot.Documento with{Estado="GUARDANDO",UsuarioAuditoria=actor,ErrorTemporal=null},ct);
        else if(lot.Documento.Estado is not ("GUARDANDO" or "GUARDADA"))throw new RhNomError("PRENOMINA_NO_LISTA","Calcule y revise todas las personas antes de guardar.");
        return new ResultDto<RhNomLotePrenomina>((await RhNomPrenominaDb.Leer(cn,company,actor,q.IdLote,null,ct))!){IsValid=true,Message=reverse?"Temporal revertido y fecha de prenomina restaurada.":"Guardado en cola; espere la confirmacion del servidor."};
    });
}
public sealed partial class RhNominaController
{
    [HttpPost("prenominaGuardarTemporal")]
    public Task<IActionResult> PrenominaSave(RhNomPrenominaId q)=>RunAsync("rh.nomina.ejecutar",(e,a,ct)=>new RhNomPrenominaGuardarHandler(db,config).Solicitar(q,e,a,false,ct),"rh.nomina.fuentes.datos");
    [HttpPost("prenominaRevertirTemporal")]
    public Task<IActionResult> PrenominaReverse(RhNomPrenominaId q)=>RunAsync("rh.nomina.revertir",(e,a,ct)=>new RhNomPrenominaGuardarHandler(db,config).Solicitar(q,e,a,true,ct),"rh.nomina.fuentes.datos");
}
