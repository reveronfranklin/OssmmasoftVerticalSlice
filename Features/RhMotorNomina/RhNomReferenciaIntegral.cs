using System.Data;
using System.Globalization;
using System.Text.Json;
using Oracle.ManagedDataAccess.Client;
namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public record RhNomPeriodoIntegral(int PeriodoId,DateTime FechaNomina,int Periodo,DateTime FechaPrecierre,string Frecuencia);
public record RhNomReferenciaIntegralDoc(int Formato,RhNomContextoHistorico Contexto,DateTime RelojOracle,List<RhNomPeriodoIntegral> Candidatos);
public record RhNomReferenciaIntegralCapturada(RhNomReferenciaIntegralDoc Documento,DateTime FechaReferencia,string Origen,string Hash)
{
    public string VersionMetodo=>RhNomReferenciaIntegral.Version;
    public bool EstadoHistoricoVerificado=>false;
}
// Reads the temporal selection only. It neither calls RH_SUELDO nor treats the
// current Oracle clock/periods as proof of an original historical calculation.
public static class RhNomReferenciaIntegral
{
    public const string Version="REFERENCIA_INTEGRAL_V1";
    public static RhNomReferenciaIntegralCapturada Capturar(RhNomReferenciaIntegralDoc doc)
    {
        static bool OracleDate(DateTime d)=>d.Kind==DateTimeKind.Unspecified&&d.Year>=1900;
        if(doc is null||doc.Formato!=1||doc.Contexto is null||doc.Contexto.CodigoEmpresa<=0||doc.Contexto.CodigoTipoNomina<=0||doc.Contexto.CodigoPeriodo is null or <=0||!OracleDate(doc.Contexto.FechaNomina)||!OracleDate(doc.RelojOracle)||doc.RelojOracle!=doc.RelojOracle.Date)
            throw new RhNomError("REFERENCIA_INTEGRAL_INVALIDA","Contexto explicito y reloj Oracle truncado sin conversion de zona requeridos.");
        if(doc.Candidatos is null||doc.Candidatos.Count>50||doc.Candidatos.Any(p=>p is null||p.PeriodoId<=0||p.Periodo<0||!OracleDate(p.FechaNomina)||!OracleDate(p.FechaPrecierre)||string.IsNullOrEmpty(p.Frecuencia)||p.Frecuencia[..1]!=p.Periodo.ToString(CultureInfo.InvariantCulture)))
            throw new RhNomError("PERIODO_REF_INVALIDO","Candidatos validos del ultimo precierre y frecuencia/periodo coincidentes requeridos.");
        // Duplicate join rows are ambiguity too; never select first, largest id or
        // silently deduplicate metadata that made the original SELECT ambiguous.
        if(doc.Candidatos.Count>1)throw new RhNomError("PERIODO_REF_AMBIGUO","Mas de un candidato del ultimo precierre; no se sustituye por el reloj ni por una fila arbitraria.");
        var date=doc.Candidatos.Count==0?doc.RelojOracle:doc.Candidatos[0].FechaNomina;
        var origin=doc.Candidatos.Count==0?"RELOJ_ORACLE":"PERIODO_ABIERTO_PRECIERRE";
        var hash=RhNomCorridas.Hash(JsonSerializer.Serialize(new{Documento=doc,FechaReferencia=date,Origen=origin,VersionMetodo=Version},RhNomCorridas.Json));
        return new(doc,date,origin,hash);
    }
    public static void Verificar(RhNomReferenciaIntegralCapturada value,RhNomContextoHistorico context)
    {
        if(value is null||value.Documento?.Contexto!=context)throw new RhNomError("CONTEXTO_REF_DISTINTO","Referencia temporal ajena al contexto.");
        var expected=Capturar(value.Documento);
        if(expected.Hash!=value.Hash||expected.FechaReferencia!=value.FechaReferencia||expected.Origen!=value.Origen)throw new RhNomError("REFERENCIA_INTEGRAL_ALTERADA","Reloj, periodo, origen o fecha de referencia alterados.");
    }
    public static async Task<RhNomReferenciaIntegralCapturada> Leer(OracleConnection cn,RhNomContextoHistorico context,CancellationToken ct)
    {
        using var c=new OracleCommand("RH.SP_RH_NOM_REF_INT",cn){CommandType=CommandType.StoredProcedure,BindByName=true,CommandTimeout=25};
        RhNomCfgDb.Add(c,"p_EMPRESA",OracleDbType.Int32,context.CodigoEmpresa);RhNomCfgDb.Add(c,"p_TIPO",OracleDbType.Int32,context.CodigoTipoNomina);RhNomCfgDb.Add(c,"p_PERIODO",OracleDbType.Int32,context.CodigoPeriodo);RhNomCfgDb.Add(c,"p_FECHA",OracleDbType.Date,context.FechaNomina);c.Parameters.Add("p_RESULT",OracleDbType.RefCursor,ParameterDirection.Output);
        DateTime? clock=null;var rows=new List<RhNomPeriodoIntegral>();using var r=await c.ExecuteReaderAsync(ct);
        while(await r.ReadAsync(ct))
        {
            DateTime D(string code)=>DateTime.SpecifyKind(r.GetDateTime(r.GetOrdinal(code)),DateTimeKind.Unspecified);
            var candidate=RhNomCfgDb.Int(r,"ES_CANDIDATO");
            if(candidate==0){if(clock is not null)throw new RhNomError("RELOJ_REF_DUPLICADO","Lectura temporal con mas de un reloj.");clock=D("RELOJ_ORACLE");}
            else if(candidate==1){if(rows.Count>=50)throw new RhNomError("LIMITE_REFERENCIA_INTEGRAL","Mas de 50 candidatos; no se trunca.");rows.Add(new(RhNomCfgDb.Int(r,"CODIGO_PERIODO"),D("FECHA_NOMINA"),RhNomCfgDb.Int(r,"PERIODO"),D("FECHA_PRECIERRE"),RhNomCfgDb.Text(r,"FRECUENCIA")));}
            else throw new RhNomError("FILA_REF_INVALIDA","Clase de fila temporal no reconocida.");
        }
        if(clock is null)throw new RhNomError("RELOJ_REF_AUSENTE","No se recibio el reloj Oracle; no se usa el reloj del cliente o servidor web.");
        return Capturar(new(1,context,clock.Value,rows));
    }
}
