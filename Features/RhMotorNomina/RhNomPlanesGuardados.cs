using System.Data;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public record RhNomGuardarPlan(string ClaveSolicitud,List<int> Corridas);
public record RhNomLeerPlan(int IdPlan);
public record RhNomPlanCabecera(int IdPlan,int CodigoEmpresa,int Actor,string ClaveSolicitud,string HashSolicitud,string HashDocumento,DateTime FechaIns);
public record RhNomPlanDocumento(string Version,int CodigoEmpresa,int Actor,List<int> Corridas,
    RhNomConsolidacion Consolidacion,RhNomCapturaMovimientos Captura);
public record RhNomDiagnosticoArchivado(RhNomConsolidacion Consolidacion,RhNomPlanFinanciero Plan,string HashMovimientos,int CantidadMovimientos)
{
    public bool PlanPersistido=>true;
    public bool PropiedadRegistrada=>false;
    public bool ParidadCertificada=>false;
    public bool AplicacionDisponible=>false;
    public string Advertencia=>"Captura archivada. No representa el estado actual ni acredita propiedad sobre movimientos.";
}
public record RhNomPlanGuardado(RhNomPlanCabecera Cabecera,RhNomDiagnosticoArchivado Diagnostico)
{
    public static RhNomPlanGuardado Desde(RhNomPlanCabecera header,RhNomDiagnosticoFinanciero report)=>new(header,new(report.Consolidacion,report.Plan,report.HashMovimientos,report.CantidadMovimientos));
    public bool PlanPersistido=>true;
    public bool MovimientosAplicados=>false;
    public bool PropiedadRegistrada=>false;
    public bool AplicacionDisponible=>false;
    public bool ParidadCertificada=>false;
}
public static class RhNomPlanesGuardados
{
    public const string Version="PLAN_GUARDADO_V1";
    public const int MaxBytes=4*1024*1024;
    public static int[] Validar(RhNomGuardarPlan q,int company,int actor)
    {
        if(company<=0||actor<=0||q.ClaveSolicitud is null||q.ClaveSolicitud.Length is <1 or >80||!Regex.IsMatch(q.ClaveSolicitud,@"\A[A-Za-z0-9:_-]+\z"))
            throw new RhNomError("SOLICITUD_PLAN_INVALIDA","Clave de 1..80 caracteres alfanumericos, guion, dos puntos o guion bajo requerida.");
        return RhNomConsolidaciones.Validar(new(q.Corridas));
    }
    public static string HashSolicitud(RhNomGuardarPlan q,int company,int actor)=>RhNomRecalculosHistoricos.Hash(new{Version,CodigoEmpresa=company,Actor=actor,Corridas=Validar(q,company,actor)});
    public static string Serializar(RhNomPlanDocumento d)=>RhNomCorridas.Serializar(d,MaxBytes);
    public static RhNomDiagnosticoFinanciero Verificar(RhNomPlanDocumento d,int company,int actor,CancellationToken ct=default)
    {
        if(company<=0||actor<=0||d.Version!=Version||d.CodigoEmpresa!=company||d.Actor!=actor||d.Consolidacion is null||d.Captura is null||d.Captura.Imagenes is null||d.Consolidacion.CodigoEmpresa!=company||d.Consolidacion.Destinos is null||d.Consolidacion.Destinos.Any(x=>x is null||x.Origenes is null||x.Origenes.Any(o=>o is null)))
            throw new RhNomError("DOCUMENTO_PLAN_AJENO","Version o alcance del documento de plan invalido.");
        RhNomConsolidaciones.VerificarDecisiones(d.Consolidacion);
        var ids=RhNomConsolidaciones.Validar(new(d.Corridas));
        if(!d.Corridas.SequenceEqual(ids)||!d.Consolidacion.Destinos.SelectMany(x=>x.Origenes).Select(o=>o.IdRun).Distinct().Order().SequenceEqual(ids))
            throw new RhNomError("CORRIDAS_PLAN_DISTINTAS","Las corridas del documento no corresponden exactamente a sus origenes.");
        if(d.Consolidacion.Destinos.SelectMany(x=>x.Origenes).Any(o=>o.ModeloId<=0||o.Version<=0||o.CodigoRelacionCargo<=0||!HashValido(o.HashDatos)||!HashValido(o.HashModelo)||!HashValido(o.HashFuente)))
            throw new RhNomError("ORIGEN_PLAN_INVALIDO","Origen de corrida incompleto en el plan.");
        var report=RhNomMovimientosPlanDb.Preparar(d.Consolidacion,d.Captura,ct);
        if(JsonSerializer.SerializeToUtf8Bytes(report,RhNomCorridas.Json).Length>2*1024*1024-4096)throw new RhNomError("PLAN_RESPUESTA_EXCESIVA","La traza excede el limite de respuesta; reduzca el alcance antes de guardar.");
        return report;
    }
    static bool HashValido(string value)=>value is not null&&Regex.IsMatch(value,@"\A[A-F0-9]{64}\z");
    public static RhNomDiagnosticoFinanciero Leer(string text,RhNomPlanCabecera header,int company,int actor,CancellationToken ct=default)
    {
        if(header.IdPlan<=0||header.CodigoEmpresa!=company||header.Actor!=actor||header.FechaIns.Kind!=DateTimeKind.Unspecified||Encoding.UTF8.GetByteCount(text)>MaxBytes||RhNomCorridas.Hash(text)!=header.HashDocumento)
            throw new RhNomError("DOCUMENTO_PLAN_ALTERADO","Plan incompleto, alterado o ajeno al usuario.");
        RhNomPlanDocumento doc;
        try{doc=JsonSerializer.Deserialize<RhNomPlanDocumento>(text,RhNomCorridas.Json)??throw new JsonException();}
        catch(JsonException){throw new RhNomError("DOCUMENTO_PLAN_INVALIDO","No se pudo interpretar el plan guardado.");}
        var report=Verificar(doc,company,actor,ct);
        if(HashSolicitud(new(header.ClaveSolicitud,doc.Corridas),company,actor)!=header.HashSolicitud)
            throw new RhNomError("SOLICITUD_PLAN_ALTERADA","La solicitud guardada no coincide con las corridas del documento.");
        return report;
    }
}
public static class RhNomPlanesGuardadosDb
{
    static OracleCommand Cmd(OracleConnection cn,string name)=>new("RH.RH_PKG_NOM_FIN_PLAN."+name,cn){BindByName=true,CommandType=CommandType.StoredProcedure,CommandTimeout=25};
    static void Scope(OracleCommand c,int company,int actor){RhNomCfgDb.Add(c,"p_EMPRESA",OracleDbType.Int32,company);RhNomCfgDb.Add(c,"p_ACTOR",OracleDbType.Int32,actor);}
    static RhNomPlanCabecera Map(IDataReader r)=>new(RhNomCfgDb.Int(r,"ID_PLAN"),RhNomCfgDb.Int(r,"CODIGO_EMPRESA"),RhNomCfgDb.Int(r,"ACTOR"),RhNomCfgDb.Text(r,"CLAVE_SOL"),RhNomCfgDb.Text(r,"HASH_SOL"),RhNomCfgDb.Text(r,"HASH_DOC"),DateTime.SpecifyKind(r.GetDateTime(r.GetOrdinal("FECHA_INS")),DateTimeKind.Unspecified));
    public static async Task<RhNomPlanCabecera?> PorClave(OracleConnection cn,int company,int actor,string key,CancellationToken ct)
    {using var c=Cmd(cn,"POR_CLAVE");Scope(c,company,actor);RhNomCfgDb.Add(c,"p_CLAVE",OracleDbType.Varchar2,key);c.Parameters.Add("p_RESULT",OracleDbType.RefCursor,ParameterDirection.Output);using var r=await c.ExecuteReaderAsync(ct);return await r.ReadAsync(ct)?Map(r):null;}
    public static async Task<RhNomPlanCabecera> Cabecera(OracleConnection cn,int company,int actor,int id,CancellationToken ct)
    {
        if(id<=0)throw new RhNomError("PLAN_REQUERIDO","Indique plan positivo.");
        using var c=Cmd(cn,"LEER");Scope(c,company,actor);RhNomCfgDb.Add(c,"p_ID",OracleDbType.Int32,id);c.Parameters.Add("p_RESULT",OracleDbType.RefCursor,ParameterDirection.Output);using var r=await c.ExecuteReaderAsync(ct);
        return await r.ReadAsync(ct)?Map(r):throw new RhNomError("PLAN_NO_ENCONTRADO","Plan no encontrado en el alcance del usuario.");
    }
    public static async Task<RhNomPlanGuardado> Leer(OracleConnection cn,RhNomPlanCabecera header,int company,int actor,CancellationToken ct)
    {
        if(header.CodigoEmpresa!=company||header.Actor!=actor)throw new RhNomError("PLAN_AJENO","Plan ajeno al usuario.");
        using var c=Cmd(cn,"DOCUMENTO");Scope(c,company,actor);RhNomCfgDb.Add(c,"p_ID",OracleDbType.Int32,header.IdPlan);c.Parameters.Add("p_RESULT",OracleDbType.RefCursor,ParameterDirection.Output);
        using var r=await c.ExecuteReaderAsync(ct);var text=new StringBuilder();var part=1;int? length=null;
        while(await r.ReadAsync(ct))
        {
            var count=RhNomCfgDb.Int(r,"LARGO");var chunk=RhNomCfgDb.Text(r,"FRAGMENTO");
            if(RhNomCfgDb.Int(r,"PARTE")!=part++||count is <1 or >RhNomPlanesGuardados.MaxBytes||chunk.Length==0||text.Length+chunk.Length>count||length is not null&&length!=count||RhNomCfgDb.Text(r,"HUELLA")!=header.HashDocumento)
                throw new RhNomError("PLAN_INCOMPLETO","Fragmentos del plan inconsistentes.");
            length=count;text.Append(chunk);
        }
        if(length is null||text.Length!=length)throw new RhNomError("PLAN_INCOMPLETO","Documento del plan incompleto.");
        return RhNomPlanGuardado.Desde(header,RhNomPlanesGuardados.Leer(text.ToString(),header,company,actor,ct));
    }
    public static async Task<int> Crear(OracleConnection cn,int company,int actor,RhNomGuardarPlan q,RhNomPlanDocumento doc,CancellationToken ct)
    {
        RhNomPlanesGuardados.Verificar(doc,company,actor,ct);
        if(!RhNomPlanesGuardados.Validar(q,company,actor).SequenceEqual(doc.Corridas))throw new RhNomError("SOLICITUD_PLAN_DISTINTA","Solicitud y documento corresponden a otras corridas.");
        var text=RhNomPlanesGuardados.Serializar(doc);
        return await RhNomCfgDb.Guardar(cn,async tx=>{using var c=Cmd(cn,"CREAR");c.Transaction=tx;Scope(c,company,actor);
            RhNomCfgDb.Add(c,"p_CLAVE",OracleDbType.Varchar2,q.ClaveSolicitud);RhNomCfgDb.Add(c,"p_HASH_SOL",OracleDbType.Varchar2,RhNomPlanesGuardados.HashSolicitud(q,company,actor));
            RhNomCfgDb.Add(c,"p_DOC",OracleDbType.Clob,text);RhNomCfgDb.Add(c,"p_HASH_DOC",OracleDbType.Varchar2,RhNomCorridas.Hash(text));var id=RhNomCfgDb.Out(c,"p_ID");await c.ExecuteNonQueryAsync(ct);return int.Parse(id.Value.ToString()!);},ct);
    }
    public static async Task<ResultDto<List<RhNomPlanCabecera>>> Listar(OracleConnection cn,int company,int actor,RhNomListaQuery q,CancellationToken ct)
    {
        RhNomConfiguracion.Lista(q);if(!string.IsNullOrWhiteSpace(q.SearchText))throw new RhNomError("BUSQUEDA_PLAN_NO_ADMITIDA","El listado de planes no admite busqueda de texto.");
        using var c=Cmd(cn,"LISTAR");Scope(c,company,actor);RhNomCfgDb.Add(c,"p_PAGE",OracleDbType.Int32,q.PageNumber);RhNomCfgDb.Add(c,"p_SIZE",OracleDbType.Int32,q.PageSize);
        var total=RhNomCfgDb.Out(c,"p_TOTAL");c.Parameters.Add("p_RESULT",OracleDbType.RefCursor,ParameterDirection.Output);var rows=new List<RhNomPlanCabecera>();using(var r=await c.ExecuteReaderAsync(ct))while(await r.ReadAsync(ct))rows.Add(Map(r));return RhNomCfgDb.Pagina(rows,q,total);
    }
}
public sealed class RhNomPlanesGuardadosHandler(ConnectionDB db,IConfiguration config)
{
    public Task<ResultDto<RhNomPlanGuardado>> Crear(RhNomGuardarPlan q,int company,int actor,CancellationToken ct)=>RhNomCfgDb.Resultado(async()=>
    {
        var ids=RhNomPlanesGuardados.Validar(q,company,actor);var requestHash=RhNomPlanesGuardados.HashSolicitud(q,company,actor);
        await using var cn=await RhNomCfgDb.Abrir(db,ct);
        var existing=await RhNomPlanesGuardadosDb.PorClave(cn,company,actor,q.ClaveSolicitud,ct);
        if(existing is not null){if(existing.HashSolicitud!=requestHash)throw new RhNomError("CLAVE_PLAN_REUTILIZADA","La clave identifica otras corridas; use una clave nueva.");return new ResultDto<RhNomPlanGuardado>(await RhNomPlanesGuardadosDb.Leer(cn,existing,company,actor,ct)){IsValid=true,Message="Plan previo original recuperado; no se recapturaron movimientos."};}
        var result=await new RhNomConsolidacionHandler(db,config).Handle(new(ids.ToList()),company,actor,ct);
        if(!result.IsValid||result.Data is null)return new ResultDto<RhNomPlanGuardado>(default!){IsValid=false,Message=result.Message};
        var capture=await RhNomMovimientosPlanDb.Leer(cn,company,result.Data.CodigoTipoNomina,result.Data.CodigoPersona,ct);
        var doc=new RhNomPlanDocumento(RhNomPlanesGuardados.Version,company,actor,ids.ToList(),result.Data,capture);
        var id=await RhNomPlanesGuardadosDb.Crear(cn,company,actor,q,doc,ct);
        return new ResultDto<RhNomPlanGuardado>(await RhNomPlanesGuardadosDb.Leer(cn,await RhNomPlanesGuardadosDb.Cabecera(cn,company,actor,id,ct),company,actor,ct)){IsValid=true,Message="Plan previo guardado inmutable. No aplica pagos ni acredita propiedad de movimientos."};
    });
    public Task<ResultDto<RhNomPlanGuardado>> Leer(RhNomLeerPlan q,int company,int actor,CancellationToken ct)=>RhNomCfgDb.Resultado(async()=>{await using var cn=await RhNomCfgDb.Abrir(db,ct);return new ResultDto<RhNomPlanGuardado>(await RhNomPlanesGuardadosDb.Leer(cn,await RhNomPlanesGuardadosDb.Cabecera(cn,company,actor,q.IdPlan,ct),company,actor,ct)){IsValid=true,Message="Plan previo archivado; no representa el estado actual de los movimientos."};});
    public Task<ResultDto<List<RhNomPlanCabecera>>> Listar(RhNomListaQuery q,int company,int actor,CancellationToken ct)=>RhNomCfgDb.Resultado(async()=>{await using var cn=await RhNomCfgDb.Abrir(db,ct);return await RhNomPlanesGuardadosDb.Listar(cn,company,actor,q,ct);});
}
public sealed partial class RhNominaController
{
    [HttpPost("planesFinancierosCrear")][RequestSizeLimit(16384)]
    public Task<IActionResult> SaveFinancialPlan(RhNomGuardarPlan q)=>RunAsync("rh.nomina.simular",(e,a,ct)=>new RhNomPlanesGuardadosHandler(db,config).Crear(q,e,a,ct),"rh.nomina.fuentes.datos");
    [HttpPost("planesFinancierosDetalle")][RequestSizeLimit(4096)]
    public Task<IActionResult> SavedFinancialPlan(RhNomLeerPlan q)=>RunAsync("rh.nomina.simular",(e,a,ct)=>new RhNomPlanesGuardadosHandler(db,config).Leer(q,e,a,ct),"rh.nomina.fuentes.datos");
    [HttpPost("planesFinancierosGetAll")][RequestSizeLimit(4096)]
    public Task<IActionResult> SavedFinancialPlans(RhNomListaQuery q)=>RunAsync("rh.nomina.simular",(e,a,ct)=>new RhNomPlanesGuardadosHandler(db,config).Listar(q,e,a,ct),"rh.nomina.fuentes.datos");
}
