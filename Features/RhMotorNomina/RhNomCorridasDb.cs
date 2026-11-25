using System.Data;
using Oracle.ManagedDataAccess.Client;
namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;
public static class RhNomCorridasDb
{
    private static OracleCommand Cmd(OracleConnection cn,string name)=>new("RH.RH_PKG_NOM_RUN."+name,cn){BindByName=true,CommandType=CommandType.StoredProcedure,CommandTimeout=25};
    private static void Contexto(OracleCommand c,int company,int actor){RhNomCfgDb.Add(c,"p_EMPRESA",OracleDbType.Int32,company);RhNomCfgDb.Add(c,"p_ACTOR",OracleDbType.Int32,actor);}
    private static RhNomCorrida Map(IDataReader r)
    {
        DateTime? Date(string name)=>r.IsDBNull(r.GetOrdinal(name))?null:DateTime.SpecifyKind(r.GetDateTime(r.GetOrdinal(name)),DateTimeKind.Unspecified);
        string? Text(string name)=>r.IsDBNull(r.GetOrdinal(name))?null:RhNomCfgDb.Text(r,name);
        return new(RhNomCfgDb.Int(r,"ID_RUN"),RhNomCfgDb.Int(r,"CODIGO_EMPRESA"),RhNomCfgDb.Int(r,"ACTOR"),RhNomCfgDb.Int(r,"ID_MODELO"),RhNomCfgDb.Int(r,"VER_MODELO"),RhNomCfgDb.Text(r,"MODO"),RhNomCfgDb.Text(r,"ESTADO"),RhNomCfgDb.Text(r,"CLAVE_SOL"),RhNomCfgDb.Text(r,"HASH_SOL"),RhNomCfgDb.Text(r,"HASH_DATOS"),Text("HASH_RESULT"),Text("MENSAJE"),RhNomCfgDb.Int(r,"VERSION"),RhNomCfgDb.Int(r,"CANCELAR")==1,RhNomCfgDb.Int(r,"INTENTOS"),Date("FECHA_INS")!.Value,Date("FECHA_INICIO"),Date("FECHA_FIN"));
    }
    public static async Task<RhNomCorrida?> PorClave(OracleConnection cn,int company,int actor,string key,CancellationToken ct)
    {using var c=Cmd(cn,"POR_CLAVE");Contexto(c,company,actor);RhNomCfgDb.Add(c,"p_CLAVE",OracleDbType.Varchar2,key);c.Parameters.Add("p_RESULT",OracleDbType.RefCursor,ParameterDirection.Output);using var r=await c.ExecuteReaderAsync(ct);return await r.ReadAsync(ct)?Map(r):null;}
    public static async Task<RhNomCorrida> Leer(OracleConnection cn,int company,int actor,int id,CancellationToken ct)
    {
        if(id<=0)throw new RhNomError("CORRIDA_REQUERIDA","Indique corrida positiva.");
        using var c=Cmd(cn,"LEER");Contexto(c,company,actor);RhNomCfgDb.Add(c,"p_ID",OracleDbType.Int32,id);c.Parameters.Add("p_RESULT",OracleDbType.RefCursor,ParameterDirection.Output);using var r=await c.ExecuteReaderAsync(ct);
        if(!await r.ReadAsync(ct))throw new RhNomError("CORRIDA_NO_ENCONTRADA","Corrida no encontrada en el alcance del usuario.");return Map(r);
    }
    public static async Task<(List<RhNomCorrida> Rows,int Count)> Listar(OracleConnection cn,int company,int actor,RhNomListaQuery page,CancellationToken ct)
    {
        RhNomConfiguracion.Lista(page);using var c=Cmd(cn,"LISTAR");Contexto(c,company,actor);RhNomCfgDb.Add(c,"p_PAGE",OracleDbType.Int32,page.PageNumber);RhNomCfgDb.Add(c,"p_SIZE",OracleDbType.Int32,page.PageSize);c.Parameters.Add("p_RESULT",OracleDbType.RefCursor,ParameterDirection.Output);var total=RhNomCfgDb.Out(c,"p_TOTAL");var rows=new List<RhNomCorrida>();
        using(var r=await c.ExecuteReaderAsync(ct))while(await r.ReadAsync(ct))rows.Add(Map(r));return(rows,int.Parse(total.Value.ToString()!));
    }
    public static string ValidarDocumento(string text,int length,string hash,RhNomCorrida run,string type)
    {
        var limit=type=="ENTRADA"?RhNomCorridas.MaxDatosBytes:type=="RESULTADO"?RhNomCorridas.MaxResultadoBytes:0;
        if(limit==0||length<=0||length>limit||text.Length!=length||System.Text.Encoding.UTF8.GetByteCount(text)>limit
            ||RhNomCorridas.Hash(text)!=hash||hash!=(type=="ENTRADA"?run.HashDatos:run.HashResultado))
            throw new RhNomError("DOCUMENTO_ALTERADO","Documento de corrida incompleto, alterado o fuera de limite.");
        return text;
    }
    public static async Task<string?> Documento(OracleConnection cn,RhNomCorrida run,string type,CancellationToken ct)
    {
        if(type is not ("ENTRADA" or "RESULTADO"))throw new RhNomError("DOCUMENTO_INVALIDO","Indique entrada o resultado.");
        using var c=new OracleCommand("RH.SP_RH_NOM_RUN_DOC",cn){BindByName=true,CommandType=CommandType.StoredProcedure,CommandTimeout=25};
        Contexto(c,run.CodigoEmpresa,run.Actor);RhNomCfgDb.Add(c,"p_ID",OracleDbType.Int32,run.IdRun);RhNomCfgDb.Add(c,"p_TIPO",OracleDbType.Varchar2,type);c.Parameters.Add("p_RESULT",OracleDbType.RefCursor,ParameterDirection.Output);
        using var r=await c.ExecuteReaderAsync(ct);
        if(!await r.ReadAsync(ct))return null;
        if(r.IsDBNull(r.GetOrdinal("DOCUMENTO")))
        {
            if(type=="ENTRADA"||!r.IsDBNull(r.GetOrdinal("LARGO"))||!r.IsDBNull(r.GetOrdinal("HUELLA")))throw new RhNomError("DOCUMENTO_ALTERADO","Documento nulo con metadata incompatible.");
            return null;
        }
        var length=RhNomCfgDb.Int(r,"LARGO");var limit=type=="ENTRADA"?RhNomCorridas.MaxDatosBytes:RhNomCorridas.MaxResultadoBytes;
        if(length<=0||length>limit)throw new RhNomError("LIMITE_DOCUMENTO","El documento excede el limite; no se carga ni trunca.");
        var hash=RhNomCfgDb.Text(r,"HUELLA");using var clob=r.GetOracleClob(r.GetOrdinal("DOCUMENTO"));var text=clob.Value;
        if(await r.ReadAsync(ct))throw new RhNomError("DOCUMENTO_DUPLICADO","Documento de corrida ambiguo.");
        return ValidarDocumento(text,length,hash,run,type);
    }
    public static async Task<int> Crear(OracleConnection cn,int company,int actor,string key,string requestHash,RhNomCorridaDocumento doc,CancellationToken ct)
    {
        var text=RhNomCorridas.Serializar(doc,RhNomCorridas.MaxDatosBytes);
        return await RhNomCfgDb.Guardar(cn,async tx=>{using var c=Cmd(cn,"CREAR");c.Transaction=tx;Contexto(c,company,actor);
            RhNomCfgDb.Add(c,"p_MODELO",OracleDbType.Int32,doc.Modelo.ModeloId);RhNomCfgDb.Add(c,"p_VERSION",OracleDbType.Int32,doc.Modelo.Version);RhNomCfgDb.Add(c,"p_MODO",OracleDbType.Varchar2,doc.Modo);RhNomCfgDb.Add(c,"p_CLAVE",OracleDbType.Varchar2,key);RhNomCfgDb.Add(c,"p_HASH",OracleDbType.Varchar2,requestHash);RhNomCfgDb.Add(c,"p_DATOS",OracleDbType.Clob,text);RhNomCfgDb.Add(c,"p_HASH_DATOS",OracleDbType.Varchar2,RhNomCorridas.Hash(text));var output=RhNomCfgDb.Out(c,"p_ID");await c.ExecuteNonQueryAsync(ct);return int.Parse(output.Value.ToString()!);},ct);
    }
    public static async Task Cancelar(OracleConnection cn,int company,int actor,RhNomCancelarCorrida q,CancellationToken ct)
    {
        if(q.IdRun<=0||q.VersionEsperada<=0)throw new RhNomError("VERSION_INVALIDA","Corrida/version positivas requeridas.");
        await RhNomCfgDb.Guardar(cn,async tx=>{using var c=Cmd(cn,"CANCELAR");c.Transaction=tx;Contexto(c,company,actor);RhNomCfgDb.Add(c,"p_ID",OracleDbType.Int32,q.IdRun);RhNomCfgDb.Add(c,"p_VERSION",OracleDbType.Int32,q.VersionEsperada);await c.ExecuteNonQueryAsync(ct);return true;},ct);
    }
    public static async Task<(int Id,int Actor)> Tomar(OracleConnection cn,int company,string token,CancellationToken ct)
    {
        return await RhNomCfgDb.Guardar(cn,async tx=>{using var c=Cmd(cn,"TOMAR");c.Transaction=tx;RhNomCfgDb.Add(c,"p_EMPRESA",OracleDbType.Int32,company);RhNomCfgDb.Add(c,"p_TOKEN",OracleDbType.Varchar2,token);var id=RhNomCfgDb.Out(c,"p_ID");var actor=RhNomCfgDb.Out(c,"p_ACTOR");await c.ExecuteNonQueryAsync(ct);return(int.Parse(id.Value.ToString()!),int.Parse(actor.Value.ToString()!));},ct);
    }
    public static async Task Terminar(OracleConnection cn,RhNomCorrida run,string token,RhNomCorridaResultado? result,string? failure,CancellationToken ct)
    {
        var text=result is null?null:RhNomCorridas.Serializar(result,RhNomCorridas.MaxResultadoBytes);var state=result?.IsValid==true?"COMPLETADO":"ERROR";
        await RhNomCfgDb.Guardar(cn,async tx=>{using var c=Cmd(cn,"TERMINAR");c.Transaction=tx;RhNomCfgDb.Add(c,"p_EMPRESA",OracleDbType.Int32,run.CodigoEmpresa);RhNomCfgDb.Add(c,"p_ID",OracleDbType.Int32,run.IdRun);RhNomCfgDb.Add(c,"p_TOKEN",OracleDbType.Varchar2,token);RhNomCfgDb.Add(c,"p_ESTADO",OracleDbType.Varchar2,state);RhNomCfgDb.Add(c,"p_RESULT",OracleDbType.Clob,text);RhNomCfgDb.Add(c,"p_HASH",OracleDbType.Varchar2,text is null?null:RhNomCorridas.Hash(text));RhNomCfgDb.Add(c,"p_MENSAJE",OracleDbType.Varchar2,failure??(state=="COMPLETADO"?"Simulacion completada sin movimientos.":"Simulacion rechazada; revise diagnosticos."));await c.ExecuteNonQueryAsync(ct);return true;},ct);
    }
}
