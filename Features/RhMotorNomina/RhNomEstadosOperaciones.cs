using System.Data;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;
public record RhNomAuditoriaEstadosQuery(int ModeloId,int Version,int PageNumber=1,int PageSize=20);
public sealed class RhNomEstadosHandler(ConnectionDB db,IConfiguration config)
{
    private void Habilitado(){if(!config.GetValue<bool>("RhMotorNomina:VersionesEnabled"))throw new RhNomError("ESTADOS_NO_HABILITADOS","Estados de revision no habilitados en este ambiente.");}
    public Task<ResultDto<RhNomEstadoRevision>> Get(RhNomEstadoQuery q,int company,CancellationToken ct)=>RhNomCfgDb.Resultado(async()=>{Habilitado();using var cn=await RhNomCfgDb.Abrir(db,ct);return new ResultDto<RhNomEstadoRevision>(await RhNomEstadosDb.Leer(cn,company,q.ModeloId,q.Version,ct)){IsValid=true,Message="Estado de revision recuperado."};});
    private async Task<RhNomEvidenciaRevision> Evidencia(OracleConnection cn,RhNomModeloVersion model,List<int> ids,int company,int actor,CancellationToken ct)
    {
        var cases=new List<(RhNomCorrida,RhNomCorridaDocumento,RhNomCorridaResultado)>();
        foreach(var id in RhNomValidaciones.Corridas(ids))
        {
            var run=await RhNomCorridasDb.Leer(cn,company,actor,id,ct);
            if(run.Estado!="COMPLETADO")throw new RhNomError("PRUEBA_NO_COMPLETADA","Las pruebas deben estar completadas sin errores.");
            var input=await RhNomCorridasDb.Documento(cn,run,"ENTRADA",ct)??throw new RhNomError("PRUEBA_SIN_DATOS","Falta snapshot de prueba.");
            var output=await RhNomCorridasDb.Documento(cn,run,"RESULTADO",ct)??throw new RhNomError("PRUEBA_SIN_RESULTADO","Falta resultado de prueba.");
            RhNomCorridaResultado result;try{result=JsonSerializer.Deserialize<RhNomCorridaResultado>(output,RhNomCorridas.Json)??throw new JsonException();}catch(JsonException){throw new RhNomError("PRUEBA_ALTERADA","Resultado no interpretable.");}
            cases.Add((run,RhNomCorridas.Leer(input,run.HashDatos,company,actor),result));
        }
        // Current comparisons intentionally do not certify historical provenance or full coverage.
        // The transaction adapter is not installed; no appsetting/client boolean overrides this gate.
        return RhNomValidaciones.Capturar(model,cases,company,actor,aplicacionDisponible:false);
    }
    public Task<ResultDto<RhNomEstadoRevision>> Validar(RhNomValidarRevision q,int company,int actor,CancellationToken ct)=>RhNomCfgDb.Resultado(async()=>
    {
        Habilitado();RhNomValidaciones.Contexto(q.ModeloId,q.Version,q.HashModeloEsperado,q.VersionEstadoEsperada);RhNomValidaciones.Motivo(q.Motivo);
        using var cn=await RhNomCfgDb.Abrir(db,ct);var model=await RhNomCfgDb.Revision(cn,company,q.ModeloId,q.Version,ct);if(model.Hash!=q.HashModeloEsperado)throw new RhNomError("HASH_MODELO_DISTINTO","La revision no coincide con el hash esperado.");
        var evidence=await Evidencia(cn,model,q.IdCorridas,company,actor,ct);var text=RhNomCorridas.Serializar(evidence,16000);
        await RhNomCfgDb.Guardar(cn,async tx=>{using var c=RhNomEstadosDb.Cmd(cn,"VALIDAR",tx);RhNomEstadosDb.Claves(c,company,q.ModeloId,q.Version);RhNomEstadosDb.Escritura(c,actor,model.Hash,q.VersionEstadoEsperada,q.Motivo);
            RhNomCfgDb.Add(c,"p_EVIDENCIA",OracleDbType.Clob,text);RhNomCfgDb.Add(c,"p_HASH_VAL",OracleDbType.Varchar2,evidence.Hash);RhNomCfgDb.Add(c,"p_NIVEL",OracleDbType.Varchar2,evidence.Nivel);RhNomCfgDb.Add(c,"p_APTA",OracleDbType.Int32,evidence.Publicable?1:0);await c.ExecuteNonQueryAsync(ct);return true;},ct);
        return new ResultDto<RhNomEstadoRevision>(await RhNomEstadosDb.Leer(cn,company,q.ModeloId,q.Version,ct)){IsValid=true,Message="Validacion tecnica registrada y auditada. No certifica paridad ni publica reglas."};
    });
    public Task<ResultDto<RhNomEstadoRevision>> Publicar(RhNomPublicarRevision q,int company,int actor,CancellationToken ct)=>RhNomCfgDb.Resultado(async()=>
    {
        Habilitado();using var cn=await RhNomCfgDb.Abrir(db,ct);var model=await RhNomCfgDb.Revision(cn,company,q.ModeloId,q.Version,ct);RhNomPoliticaOperacion.PermitirAutomatica(config,model.CodigoTipoNomina);var state=await RhNomEstadosDb.Leer(cn,company,q.ModeloId,q.Version,ct);RhNomValidaciones.Publicar(state,q,actor);
        var fresh=await Evidencia(cn,model,state.Evidencia!.Pruebas.Select(p=>p.IdRun).ToList(),company,state.AutorValidacion!.Value,ct);if(fresh.Hash!=state.Evidencia.Hash||!fresh.Publicable)throw new RhNomError("VALIDACION_DESACTUALIZADA","Revalidacion de evidencia no permite publicar.");
        await RhNomCfgDb.Guardar(cn,async tx=>{using var c=RhNomEstadosDb.Cmd(cn,"PUBLICAR",tx);RhNomEstadosDb.Claves(c,company,q.ModeloId,q.Version);RhNomEstadosDb.Escritura(c,actor,q.HashModeloEsperado,q.VersionEstadoEsperada,q.Motivo);RhNomCfgDb.Add(c,"p_HASH_VAL",OracleDbType.Varchar2,q.HashValidacionEsperado);RhNomCfgDb.Add(c,"p_DESDE",OracleDbType.Date,q.Desde.ToDateTime(TimeOnly.MinValue));RhNomCfgDb.Add(c,"p_HASTA",OracleDbType.Date,q.Hasta?.ToDateTime(TimeOnly.MinValue));await c.ExecuteNonQueryAsync(ct);return true;},ct);
        return new ResultDto<RhNomEstadoRevision>(await RhNomEstadosDb.Leer(cn,company,q.ModeloId,q.Version,ct)){IsValid=true,Message="Revision publicada con vigencia explicita."};
    });
    public Task<ResultDto<RhNomEstadoRevision>> Retirar(RhNomRetirarRevision q,int company,int actor,CancellationToken ct)=>RhNomCfgDb.Resultado(async()=>
    {
        Habilitado();RhNomValidaciones.Contexto(q.ModeloId,q.Version,q.HashModeloEsperado,q.VersionEstadoEsperada);RhNomValidaciones.Motivo(q.Motivo);using var cn=await RhNomCfgDb.Abrir(db,ct);
        await RhNomCfgDb.Guardar(cn,async tx=>{using var c=RhNomEstadosDb.Cmd(cn,"RETIRAR",tx);RhNomEstadosDb.Claves(c,company,q.ModeloId,q.Version);RhNomEstadosDb.Escritura(c,actor,q.HashModeloEsperado,q.VersionEstadoEsperada,q.Motivo);await c.ExecuteNonQueryAsync(ct);return true;},ct);
        return new ResultDto<RhNomEstadoRevision>(await RhNomEstadosDb.Leer(cn,company,q.ModeloId,q.Version,ct)){IsValid=true,Message="Revision retirada. No revierte movimientos ni altera snapshots de corridas."};
    });
    public Task<ResultDto<List<RhNomAuditoriaRevision>>> Auditoria(RhNomAuditoriaEstadosQuery q,int company,CancellationToken ct)=>RhNomCfgDb.Resultado(async()=>
    {
        Habilitado();var page=RhNomConfiguracion.Lista(new(q.PageNumber,q.PageSize));using var cn=await RhNomCfgDb.Abrir(db,ct);using var c=RhNomEstadosDb.Cmd(cn,"AUDITORIA");RhNomEstadosDb.Claves(c,company,q.ModeloId,q.Version);RhNomCfgDb.Add(c,"p_PAGE",OracleDbType.Int32,page.PageNumber);RhNomCfgDb.Add(c,"p_SIZE",OracleDbType.Int32,page.PageSize);var count=RhNomCfgDb.Out(c,"p_TOTAL");c.Parameters.Add("p_RESULT",OracleDbType.RefCursor,ParameterDirection.Output);var rows=new List<RhNomAuditoriaRevision>();
        using(var r=await c.ExecuteReaderAsync(ct))while(await r.ReadAsync(ct))rows.Add(new(RhNomCfgDb.Int(r,"ID_EVENTO"),RhNomCfgDb.Text(r,"ACCION"),RhNomCfgDb.Text(r,"ESTADO_ANT"),RhNomCfgDb.Text(r,"ESTADO_NUEVO"),RhNomCfgDb.Int(r,"ACTOR"),DateTime.SpecifyKind(r.GetDateTime(r.GetOrdinal("FECHA")),DateTimeKind.Unspecified),RhNomCfgDb.Text(r,"MOTIVO"),r.IsDBNull(r.GetOrdinal("HASH_VALIDACION"))?null:RhNomCfgDb.Text(r,"HASH_VALIDACION")));
        return RhNomCfgDb.Pagina(rows,page,count);
    });
}
public sealed partial class RhNominaController
{
    [HttpPost("modelosEstadoGet")]
    public Task<IActionResult> RevisionState(RhNomEstadoQuery q)=>RunAsync("rh.nomina.configuracion.leer",(e,_,ct)=>new RhNomEstadosHandler(db,config).Get(q,e,ct));
    [HttpPost("modelosValidarRevision")][RequestSizeLimit(16384)]
    public Task<IActionResult> ValidateRevision(RhNomValidarRevision q)=>RunAsync("rh.nomina.validar",(e,a,ct)=>new RhNomEstadosHandler(db,config).Validar(q,e,a,ct));
    [HttpPost("modelosPublicarRevision")][RequestSizeLimit(16384)]
    public Task<IActionResult> PublishRevision(RhNomPublicarRevision q)=>RunAsync("rh.nomina.publicar",(e,a,ct)=>new RhNomEstadosHandler(db,config).Publicar(q,e,a,ct));
    [HttpPost("modelosRetirarRevision")][RequestSizeLimit(16384)]
    public Task<IActionResult> WithdrawRevision(RhNomRetirarRevision q)=>RunAsync("rh.nomina.publicar",(e,a,ct)=>new RhNomEstadosHandler(db,config).Retirar(q,e,a,ct));
    [HttpPost("modelosAuditoriaEstados")]
    public Task<IActionResult> AuditRevision(RhNomAuditoriaEstadosQuery q)=>RunAsync("rh.nomina.configuracion.leer",(e,_,ct)=>new RhNomEstadosHandler(db,config).Auditoria(q,e,ct));
}
