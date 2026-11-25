using System.Data;
using System.Text;
using System.Text.RegularExpressions;
using Oracle.ManagedDataAccess.Client;
namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

// Internal storage boundary only. A future authenticated capture handler must obtain
// actor/company and sources on the server; a client-supplied capsule is not trusted.
public record RhNomCapsulaCabecera(int IdCapsula,int CodigoEmpresa,int Actor,string ClaveSolicitud,
    string HashSolicitud,string HashDocumento,string VersionDocumento,RhNomAmbitoCapsula Ambito,DateTime FechaIns);
public record RhNomCapsulaArchivada(RhNomCapsulaCabecera Cabecera,RhNomCapsula Capsula)
{
    public bool MovimientosAplicados=>false;
    public bool OrigenHistoricoVerificado=>false;
    public bool AutorCapturaAutenticadoVerificado=>false;
}
public static class RhNomArchivoCapsulas
{
    public const string Version="ARCHIVO_CAPSULA_V1";
    public static void Alcance(int company,int actor)
    {
        if(company<=0||actor<=0)throw new RhNomError("ARCHIVO_CAPSULA_ALCANCE","Empresa y actor positivos requeridos.");
    }
    public static void Clave(string key)
    {
        if(key is null||key.Length is <1 or >80||!Regex.IsMatch(key,@"\A[A-Za-z0-9:_-]+\z",RegexOptions.CultureInvariant))
            throw new RhNomError("ARCHIVO_CAPSULA_CLAVE","Clave de 1..80 caracteres ASCII alfanumericos, guion, dos puntos o guion bajo requerida.");
    }
    public static string HashSolicitud(int company,int actor,string key,RhNomCapsula capsule)
    {
        Alcance(company,actor);Clave(key);var d=RhNomCapsulasPrenomina.Leer(capsule);
        if(d.Ambito.Periodo.CodigoEmpresa!=company)throw new RhNomError("ARCHIVO_CAPSULA_AJENA","Capsula de otra empresa.");
        return RhNomRecalculosHistoricos.Hash(new{Version,CodigoEmpresa=company,Actor=actor,ClaveSolicitud=key,Ambito=d.Ambito,HashDocumento=capsule.Hash});
    }
    public static RhNomCapsulaArchivada Verificar(RhNomCapsulaCabecera header,RhNomCapsula capsule,int company,int actor)
    {
        Alcance(company,actor);
        if(header is null||header.IdCapsula<=0||header.CodigoEmpresa!=company||header.Actor!=actor||header.FechaIns.Kind!=DateTimeKind.Unspecified)
            throw new RhNomError("ARCHIVO_CAPSULA_AJENA","Archivo no pertenece al usuario o su cabecera es invalida.");
        var d=RhNomCapsulasPrenomina.Leer(capsule);
        if(header.Ambito?.Periodo is null||header.Ambito.Periodo.FechaNomina.Kind!=DateTimeKind.Unspecified||header.Ambito!=d.Ambito||header.VersionDocumento!=d.Version
            ||header.HashDocumento!=capsule.Hash||header.HashSolicitud!=HashSolicitud(company,actor,header.ClaveSolicitud,capsule))
            throw new RhNomError("ARCHIVO_CAPSULA_ALTERADO","Cabecera, contexto y documento del archivo no coinciden.");
        return new(header,capsule);
    }
}
public static class RhNomArchivoCapsulasDb
{
    static OracleCommand Cmd(OracleConnection cn,string operation)=>new("RH.RH_PKG_NOM_CAP_ARC."+operation,cn){BindByName=true,CommandType=CommandType.StoredProcedure,CommandTimeout=25};
    static void Scope(OracleCommand c,int company,int actor){RhNomArchivoCapsulas.Alcance(company,actor);RhNomCfgDb.Add(c,"p_EMPRESA",OracleDbType.Int32,company);RhNomCfgDb.Add(c,"p_ACTOR",OracleDbType.Int32,actor);}
    static DateTime Date(IDataReader r,string name)=>DateTime.SpecifyKind(r.GetDateTime(r.GetOrdinal(name)),DateTimeKind.Unspecified);
    static RhNomCapsulaCabecera Map(IDataReader r)=>new(RhNomCfgDb.Int(r,"ID_CAP"),RhNomCfgDb.Int(r,"CODIGO_EMPRESA"),RhNomCfgDb.Int(r,"ACTOR"),RhNomCfgDb.Text(r,"CLAVE_SOL"),
        RhNomCfgDb.Text(r,"HASH_SOL"),RhNomCfgDb.Text(r,"HASH_DOC"),RhNomCfgDb.Text(r,"VERSION_DOC"),
        new(new(RhNomCfgDb.Int(r,"CODIGO_EMPRESA"),RhNomCfgDb.Int(r,"CODIGO_TIPO_NOMINA"),RhNomCfgDb.Int(r,"CODIGO_PERIODO"),Date(r,"FECHA_NOMINA"),RhNomCfgDb.Int(r,"NUM_PERIODO")),RhNomCfgDb.Int(r,"CODIGO_PRESUPUESTO"),RhNomCfgDb.Int(r,"CODIGO_PERSONA")),Date(r,"FECHA_INS"));
    public static async Task<RhNomCapsulaCabecera?> PorClave(OracleConnection cn,int company,int actor,string key,CancellationToken ct)
    {
        RhNomArchivoCapsulas.Clave(key);using var c=Cmd(cn,"POR_CLAVE");Scope(c,company,actor);RhNomCfgDb.Add(c,"p_CLAVE",OracleDbType.Varchar2,key);
        c.Parameters.Add("p_RESULT",OracleDbType.RefCursor,ParameterDirection.Output);using var r=await c.ExecuteReaderAsync(ct);return await r.ReadAsync(ct)?Map(r):null;
    }
    public static async Task<RhNomCapsulaArchivada> Leer(OracleConnection cn,int company,int actor,int id,CancellationToken ct)
    {
        if(id<=0)throw new RhNomError("ARCHIVO_CAPSULA_ID","Indique una capsula positiva.");
        RhNomCapsulaCabecera header;
        using(var c=Cmd(cn,"LEER"))
        {
            Scope(c,company,actor);RhNomCfgDb.Add(c,"p_ID",OracleDbType.Int32,id);c.Parameters.Add("p_RESULT",OracleDbType.RefCursor,ParameterDirection.Output);
            using var r=await c.ExecuteReaderAsync(ct);header=await r.ReadAsync(ct)?Map(r):throw new RhNomError("ARCHIVO_CAPSULA_NO_EXISTE","Capsula no encontrada en el alcance del usuario.");
        }
        using var doc=new OracleCommand("RH.SP_RH_NOM_CAP_DOC",cn){BindByName=true,CommandType=CommandType.StoredProcedure,CommandTimeout=25};Scope(doc,company,actor);RhNomCfgDb.Add(doc,"p_ID",OracleDbType.Int32,id);doc.Parameters.Add("p_RESULT",OracleDbType.RefCursor,ParameterDirection.Output);
        using var reader=await doc.ExecuteReaderAsync(ct);
        if(!await reader.ReadAsync(ct)||reader.IsDBNull(reader.GetOrdinal("DOCUMENTO")))throw new RhNomError("ARCHIVO_CAPSULA_INCOMPLETO","Documento de capsula ausente.");
        var length=RhNomCfgDb.Int(reader,"LARGO");if(length is <1 or >RhNomCapsulasPrenomina.MaxBytes)throw new RhNomError("ARCHIVO_CAPSULA_INCOMPLETO","Documento de capsula fuera de limite.");
        using var clob=reader.GetOracleClob(reader.GetOrdinal("DOCUMENTO"));var text=clob.Value;
        if(text.Length!=length||Encoding.UTF8.GetByteCount(text)>RhNomCapsulasPrenomina.MaxBytes||RhNomCfgDb.Text(reader,"HUELLA")!=header.HashDocumento||RhNomCorridas.Hash(text)!=header.HashDocumento||await reader.ReadAsync(ct))throw new RhNomError("ARCHIVO_CAPSULA_INCOMPLETO","Documento y huella de capsula inconsistentes.");
        return RhNomArchivoCapsulas.Verificar(header,new(text,header.HashDocumento),company,actor);
    }
    public static async Task<RhNomCapsulaArchivada> Guardar(OracleConnection cn,int company,int actor,string key,RhNomCapsula capsule,CancellationToken ct)
    {
        var hash=RhNomArchivoCapsulas.HashSolicitud(company,actor,key,capsule);var d=RhNomCapsulasPrenomina.Leer(capsule);var a=d.Ambito;var p=a.Periodo;
        var id=await RhNomCfgDb.Guardar(cn,async tx=>
        {
            using var c=Cmd(cn,"CREAR");c.Transaction=tx;Scope(c,company,actor);RhNomCfgDb.Add(c,"p_CLAVE",OracleDbType.Varchar2,key);RhNomCfgDb.Add(c,"p_HASH_SOL",OracleDbType.Varchar2,hash);
            RhNomCfgDb.Add(c,"p_TIPO",OracleDbType.Int32,p.CodigoTipoNomina);RhNomCfgDb.Add(c,"p_PERIODO",OracleDbType.Int32,p.CodigoPeriodo);RhNomCfgDb.Add(c,"p_FECHA",OracleDbType.Date,p.FechaNomina);
            RhNomCfgDb.Add(c,"p_NUM_PERIODO",OracleDbType.Int32,p.NumeroPeriodo);RhNomCfgDb.Add(c,"p_PRESUPUESTO",OracleDbType.Int32,a.CodigoPresupuesto);RhNomCfgDb.Add(c,"p_PERSONA",OracleDbType.Int32,a.CodigoPersona);
            RhNomCfgDb.Add(c,"p_VERSION",OracleDbType.Varchar2,d.Version);RhNomCfgDb.Add(c,"p_DOC",OracleDbType.Clob,capsule.Json);RhNomCfgDb.Add(c,"p_HASH_DOC",OracleDbType.Varchar2,capsule.Hash);
            var output=RhNomCfgDb.Out(c,"p_ID");await c.ExecuteNonQueryAsync(ct);return int.Parse(output.Value.ToString()!,System.Globalization.CultureInfo.InvariantCulture);
        },ct);
        var stored=await Leer(cn,company,actor,id,ct);
        if(stored.Cabecera.HashSolicitud!=hash||stored.Capsula!=capsule)throw new RhNomError("ARCHIVO_CAPSULA_REINTENTO","La clave identifica otra captura; no se reemplaza la original.");
        return stored;
    }
}
