using System.Data;
using System.Text.Json;
using Oracle.ManagedDataAccess.Client;
namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;
public static class RhNomEstadosDb
{
    public static OracleCommand Cmd(OracleConnection cn,string operation,OracleTransaction? tx=null)=>new("RH.RH_PKG_NOM_EST."+operation,cn){BindByName=true,CommandType=CommandType.StoredProcedure,CommandTimeout=25,Transaction=tx};
    public static void Claves(OracleCommand c,int company,int model,int version){RhNomCfgDb.Add(c,"p_EMPRESA",OracleDbType.Int32,company);RhNomCfgDb.Add(c,"p_MODELO",OracleDbType.Int32,model);RhNomCfgDb.Add(c,"p_VERSION",OracleDbType.Int32,version);}
    public static void Escritura(OracleCommand c,int actor,string hash,int stateVersion,string reason){RhNomCfgDb.Add(c,"p_ACTOR",OracleDbType.Int32,actor);RhNomCfgDb.Add(c,"p_HASH",OracleDbType.Varchar2,hash);RhNomCfgDb.Add(c,"p_VER_EST",OracleDbType.Int32,stateVersion);RhNomCfgDb.Add(c,"p_MOTIVO",OracleDbType.Varchar2,RhNomValidaciones.Motivo(reason));}
    public static async Task<RhNomEstadoRevision> Leer(OracleConnection cn,int company,int model,int version,CancellationToken ct)
    {
        using var c=Cmd(cn,"LEER");Claves(c,company,model,version);c.Parameters.Add("p_RESULT",OracleDbType.RefCursor,ParameterDirection.Output);using var r=await c.ExecuteReaderAsync(ct);
        if(!await r.ReadAsync(ct))throw new RhNomError("REVISION_NO_ENCONTRADA","Revision no encontrada en la empresa.");
        int? N(string n)=>r.IsDBNull(r.GetOrdinal(n))?null:RhNomCfgDb.Int(r,n);string? S(string n)=>r.IsDBNull(r.GetOrdinal(n))?null:RhNomCfgDb.Text(r,n);DateTime? D(string n)=>r.IsDBNull(r.GetOrdinal(n))?null:DateTime.SpecifyKind(r.GetDateTime(r.GetOrdinal(n)),DateTimeKind.Unspecified);
        var hash=RhNomCfgDb.Text(r,"CONFIG_HASH");RhNomEvidenciaRevision? evidence=null;var length=N("E_LARGO");
        if(length is not null)
        {
            var text=string.Concat(Enumerable.Range(1,8).Select(i=>S("E"+i)??""));if(length is <1 or >16000||text.Length!=length)throw new RhNomError("VALIDACION_ALTERADA","Evidencia incompleta o fuera de limite.");
            try{evidence=JsonSerializer.Deserialize<RhNomEvidenciaRevision>(text,RhNomCorridas.Json)??throw new JsonException();}catch(JsonException){throw new RhNomError("VALIDACION_ALTERADA","Evidencia no interpretable.");}
            RhNomValidaciones.Verificar(evidence,model,version,hash);
            if(evidence.Hash!=S("HASH_VALIDACION")||evidence.Nivel!=S("NIVEL")||evidence.Publicable!=(N("APTA")==1))throw new RhNomError("VALIDACION_ALTERADA","Cabecera y evidencia de validacion difieren.");
        }
        var result=new RhNomEstadoRevision(model,version,hash,RhNomCfgDb.Text(r,"ESTADO"),RhNomCfgDb.Int(r,"VER_ESTADO"),RhNomCfgDb.Int(r,"USUARIO_INS"),N("ACTOR_VAL"),N("ACTOR_PUB"),D("FECHA_VAL"),D("FECHA_PUB"),D("FECHA_RET"),D("DESDE") is {} from?DateOnly.FromDateTime(from):null,D("HASTA") is {} until?DateOnly.FromDateTime(until):null,evidence,S("MOTIVO"));
        if(await r.ReadAsync(ct))throw new RhNomError("ESTADO_AMBIGUO","Estado de revision duplicado.");return result;
    }
}
