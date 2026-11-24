using System.Data;
using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Features.Support;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public record RhNomRevisionGuardada(int ModeloId, int Version, string Hash, string Estado = "BORRADOR_NO_PUBLICADO");
public sealed class RhNomVersionesHandler(ConnectionDB db,IConfiguration? config=null)
{
    public Task<ResultDto<List<RhNomModeloCatalogo>>> GetAll(RhNomListaQuery value,int company,CancellationToken ct) =>
        RhNomCfgDb.Resultado(()=>GetAllCore(value,company,ct));
    private async Task<ResultDto<List<RhNomModeloCatalogo>>> GetAllCore(RhNomListaQuery value,int company,CancellationToken ct)
    {
        value = RhNomConfiguracion.Lista(value);
        using var cn = await RhNomCfgDb.Abrir(db,ct); using var cmd = RhNomCfgDb.Comando(cn,"MODELOS_GET");
        RhNomCfgDb.Paginacion(cmd,company,value); var total = RhNomCfgDb.Out(cmd,"p_TOTAL");
        var rows = new List<RhNomModeloCatalogo>();
        using (var reader = await cmd.ExecuteReaderAsync(ct)) while (await reader.ReadAsync(ct))
            rows.Add(new(RhNomCfgDb.Int(reader,"ID_MODELO"),RhNomCfgDb.Text(reader,"DESCRIPCION"),RhNomCfgDb.Int(reader,"ULT_VERSION")));
        return RhNomCfgDb.Pagina(rows,value,total);
    }
    public Task<ResultDto<RhNomModeloVersion>> Get(RhNomModeloVersionQuery value,int company,CancellationToken ct) =>
        RhNomCfgDb.Resultado(()=>GetCore(value,company,ct));
    private async Task<ResultDto<RhNomModeloVersion>> GetCore(RhNomModeloVersionQuery value,int company,CancellationToken ct)
    {
        using var cn = await RhNomCfgDb.Abrir(db,ct);
        var model=await RhNomCfgDb.Revision(cn,company,value.ModeloId,value.Version,ct);
        if(config?.GetValue<bool>("RhMotorNomina:VersionesEnabled")==true)model=model with{Estado=(await RhNomEstadosDb.Leer(cn,company,value.ModeloId,value.Version,ct)).Estado};
        return new(model) {IsValid=true,Message="Revision guardada recuperada."};
    }
    public Task<ResultDto<RhNomRevisionGuardada>> Save(RhNomModeloSave value,int company,int actor,CancellationToken ct) =>
        RhNomCfgDb.Resultado(()=>SaveCore(value,company,actor,ct));
    private async Task<ResultDto<RhNomRevisionGuardada>> SaveCore(RhNomModeloSave value,int company,int actor,CancellationToken ct)
    {
        if (value.Variables is null || value.Variables.Count is < 1 or > 200 || value.Variables.Any(v=>v is null || v.Id<=0 || v.VersionEsperada<=0)
            || value.Variables.Select(v=>v.Id).Distinct().Count()!=value.Variables.Count)
            throw new RhNomError("VARIABLES_INVALIDAS","Indique 1..200 referencias distintas con id/version.");
        using var cn = await RhNomCfgDb.Abrir(db,ct);
        var vars = new List<RhNomVariable>();
        using (var cmd = RhNomCfgDb.Comando(cn,"VARS_LOTE"))
        {
            RhNomCfgDb.Add(cmd,"p_EMPRESA",OracleDbType.Int32,company);
            RhNomCfgDb.Add(cmd,"p_IDS",OracleDbType.Varchar2,string.Join(",",value.Variables.Select(v=>v.Id.ToString(CultureInfo.InvariantCulture))));
            cmd.Parameters.Add("p_RESULT",OracleDbType.RefCursor,ParameterDirection.Output);
            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct)) vars.Add(RhNomCfgDb.Variable(reader));
        }
        RhNomSnapshot? previous = null;
        if (value.VersionEsperada > 0)
            previous = (await RhNomCfgDb.Revision(cn,company,value.ModeloId,value.VersionEsperada,ct)).Configuracion;
        RhNomConfiguracion.ValidarCliente(value,previous);
        await RhNomGruposAcum.Revalidar(cn,value.FuentesActuales,company,value.CodigoTipoNomina,ct);
        var concepts = await RhNomConceptosCatalogoHandler.Lote(cn,company,value.CodigoTipoNomina,value.ReglasConcepto,ct,value.AccionesControl);
        var snapshot = RhNomConfiguracion.Snapshot(value,vars,company,ct,concepts);
        var content = RhNomConfiguracion.Serializar(snapshot);
        var version = await RhNomCfgDb.Guardar(cn,async tx =>
        {
            using var cmd = RhNomCfgDb.Comando(cn,"VER_SAVE",tx);
            RhNomCfgDb.Add(cmd,"p_EMPRESA",OracleDbType.Int32,company); RhNomCfgDb.Add(cmd,"p_ACTOR",OracleDbType.Int32,actor);
            RhNomCfgDb.Add(cmd,"p_MODELO",OracleDbType.Int32,value.ModeloId); RhNomCfgDb.Add(cmd,"p_VERSION",OracleDbType.Int32,value.VersionEsperada);
            RhNomCfgDb.Add(cmd,"p_NOMINA",OracleDbType.Int32,value.CodigoTipoNomina); RhNomCfgDb.Add(cmd,"p_ESCENARIO",OracleDbType.Varchar2,snapshot.Escenario);
            RhNomCfgDb.Add(cmd,"p_DESCRIPCION",OracleDbType.Varchar2,snapshot.Descripcion); RhNomCfgDb.Add(cmd,"p_CONFIG",OracleDbType.Clob,content.Text);
            RhNomCfgDb.Add(cmd,"p_HASH",OracleDbType.Varchar2,content.Hash);
            var refs = ","+string.Join(",",snapshot.Variables.Select(v=>v.Id.ToString(CultureInfo.InvariantCulture)+":"+v.Version.ToString(CultureInfo.InvariantCulture)))+",";
            RhNomCfgDb.Add(cmd,"p_REFS",OracleDbType.Clob,refs); RhNomCfgDb.Add(cmd,"p_CANT_VAR",OracleDbType.Int32,snapshot.Variables.Count);
            var output = RhNomCfgDb.Out(cmd,"p_VERSION_OUT"); await cmd.ExecuteNonQueryAsync(ct);
            return SupportDb.GetIntOutput(output);
        },ct);
        return new(new(value.ModeloId,version,content.Hash)) {IsValid=true,Message="Nueva revision de borrador guardada. No publicada ni aplicada."};
    }
    public Task<ResultDto<RhNomResultadoVigente>> SimulateEffective(RhNomSimularVigente value,int company,CancellationToken ct) =>
        RhNomCfgDb.Resultado(async () =>
        {
            RhNomParametros.Fecha(value.FechaReferencia);
            using var cn = await RhNomCfgDb.Abrir(db,ct);
            var saved = await RhNomCfgDb.Revision(cn,company,value.ModeloId,value.Version,ct);
            var result = RhNomParametros.Simular(saved,value.FechaReferencia,value.Entradas,ct);
            return new ResultDto<RhNomResultadoVigente>(result) {IsValid=result.Simulacion.Errores.Count==0,
                Message=result.Simulacion.Errores.Count==0 ? "Revision simulada con parametros vigentes de borrador. No se aplicaron movimientos." : result.Simulacion.Errores[0].Mensaje};
        });
    public Task<ResultDto<RhNomResultadoSimulacion>> Simulate(RhNomModeloVersionSimular value,int company,CancellationToken ct) =>
        RhNomCfgDb.Resultado(()=>SimulateCore(value,company,ct));
    private async Task<ResultDto<RhNomResultadoSimulacion>> SimulateCore(RhNomModeloVersionSimular value,int company,CancellationToken ct)
    {
        using var cn = await RhNomCfgDb.Abrir(db,ct);
        var saved = await RhNomCfgDb.Revision(cn,company,value.ModeloId,value.Version,ct);
        if(saved.Configuracion.AccionesControl?.Count>0)throw new RhNomError("SIMULACION_CON_ACCIONES_REQUERIDA","Use simulacion vigente o contexto actual para obtener tambien las decisiones de control.");
        var response = RhNomConfiguracion.Simular(saved.Configuracion,value.Entradas,ct);
        return new(response) {IsValid=response.Errores.Count==0,Message=response.Errores.Count==0?
            "Revision simulada con entradas de prueba. No se aplicaron movimientos.":response.Errores[0].Mensaje};
    }
}
public sealed partial class RhNominaController
{
    [HttpPost("modelosGetAll")]
    public Task<IActionResult> ModelsGetAll(RhNomListaQuery value) =>
        RunAsync("rh.nomina.configuracion.leer",(company,_,ct)=>new RhNomVersionesHandler(db).GetAll(value,company,ct));
    [HttpPost("modelosVersionGet")]
    public Task<IActionResult> ModelVersionGet(RhNomModeloVersionQuery value) =>
        RunAsync("rh.nomina.configuracion.leer",(company,_,ct)=>new RhNomVersionesHandler(db,config).Get(value,company,ct));
    [HttpPost("modelosSave")]
    [RequestSizeLimit(1048576)]
    public Task<IActionResult> ModelSave(RhNomModeloSave value) =>
        RunAsync("rh.nomina.configuracion.editar",(company,actor,ct)=>new RhNomVersionesHandler(db).Save(value,company,actor,ct));
    [HttpPost("modelosSimularVigente")]
    [RequestSizeLimit(65536)]
    public Task<IActionResult> ModelSimulateEffective(RhNomSimularVigente value) =>
        RunAsync("rh.nomina.simular",(company,_,ct)=>new RhNomVersionesHandler(db).SimulateEffective(value,company,ct));
    [HttpPost("modelosSimularVersion")]
    [RequestSizeLimit(65536)]
    public Task<IActionResult> ModelSimulateVersion(RhNomModeloVersionSimular value) =>
        RunAsync("rh.nomina.simular",(company,_,ct)=>new RhNomVersionesHandler(db).Simulate(value,company,ct));
}
