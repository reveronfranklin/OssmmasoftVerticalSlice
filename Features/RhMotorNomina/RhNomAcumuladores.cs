using System.Data;
using System.Numerics;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.Helpers;
namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public record RhNomAcumuladorActual(List<int> Conceptos,List<string> Estados,List<string> TiposMovimiento,
    string PoliticaFrecuencias,List<int>? Frecuencias,string SinFilas,List<int>? ConceptosExcluidos=null,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] RhNomGrupoAcum? Grupo=null);
public record RhNomPartidaAcum(int? Id,int CodigoEmpresa,int CodigoTipoNomina,int CodigoPersona,int? CodigoConcepto,
    int? FrecuenciaId,string? TipoMovimiento,string? Estado,string? Complemento,string? Monto);
public record RhNomFilaAcum(RhNomPartidaAcum Partida,bool Incluida,string Motivo,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] int? Multiplicidad=null);
public record RhNomAcumulacion(int CodigoEmpresa,int CodigoTipoNomina,int CodigoPeriodo,DateTime FechaNomina,int CodigoPersona,
    RhNomAcumuladorActual Definicion,List<RhNomFilaAcum> Filas,string Total,string Hash)
{
    public string Origen=>"RH_MOV_NOMINA_ACTUAL";
    public string VersionMetodo=>RhNomAcumuladores.Version;
    public bool EstadoHistoricoVerificado=>false;
    public bool IncluyeSalidasCorrida=>false;
}
public static class RhNomAcumuladores
{
    public const string Version="ACUMULADORES_ACTUALES_V1";
    public const int MaxFilas=2000,MaxDefiniciones=10,MaxFilasGlobal=5000;
    public static RhNomAcumuladorActual Validar(RhNomAcumuladorActual q)
    {
        static List<int> Ids(List<int>? ids,bool required)
        {
            if(ids is null){if(required)throw new RhNomError("CONCEPTOS_REQUERIDOS","Indique conceptos positivos explicitos.");return [];}
            if(ids.Count>100||(required&&ids.Count==0)||ids.Any(i=>i<=0)||ids.Distinct().Count()!=ids.Count)throw new RhNomError("FILTRO_ACUMULADOR_INVALIDO","Hasta 100 ids positivos distintos por filtro.");
            return ids.Order().ToList();
        }
        if(q is null)throw new RhNomError("ACUMULADOR_INVALIDO","Definicion requerida.");
        var concepts=Ids(q.Conceptos,true);var excluded=Ids(q.ConceptosExcluidos,false);var frequencies=Ids(q.Frecuencias,false);
        if(excluded.Any(i=>!concepts.Contains(i)))throw new RhNomError("EXCLUSION_INVALIDA","Las exclusiones deben pertenecer al conjunto de conceptos declarado.");
        if(q.Estados is null||q.Estados.Count is <1 or >10||q.Estados.Any(s=>s is null||s.Length is <1 or >8||s!="<NULL>"&&s.Any(c=>!char.IsAsciiLetterOrDigit(c)))||q.Estados.Distinct(StringComparer.Ordinal).Count()!=q.Estados.Count)
            throw new RhNomError("ESTADOS_ACUMULADOR_INVALIDOS","Declare 1..10 estados distintos; <NULL> incluye expresamente estado nulo.");
        if(q.TiposMovimiento is null||q.TiposMovimiento.Count is <1 or >3||q.TiposMovimiento.Any(t=>t is not ("E" or "F" or "V"))||q.TiposMovimiento.Distinct().Count()!=q.TiposMovimiento.Count)
            throw new RhNomError("TIPOS_ACUMULADOR_INVALIDOS","Declare movimientos E, F y/o V sin duplicados.");
        if(q.PoliticaFrecuencias is not ("TODAS" or "LISTA")||q.PoliticaFrecuencias=="TODAS"&&frequencies.Count>0||q.PoliticaFrecuencias=="LISTA"&&frequencies.Count==0)
            throw new RhNomError("FRECUENCIAS_ACUMULADOR_INVALIDAS","Elija TODAS sin lista o LISTA con frecuencias positivas.");
        if(q.SinFilas is not ("ERROR" or "CERO_EXPLICITO"))throw new RhNomError("VACIO_ACUMULADOR_INVALIDO","Elija ERROR o CERO_EXPLICITO si ninguna partida resulta incluida.");
        if(q.Grupo is not null){RhNomGruposAcum.Verificar(q.Grupo);if(!concepts.SequenceEqual(q.Grupo.Conceptos))throw new RhNomError("CONCEPTOS_GRUPO_DISTINTOS","Conceptos deben coincidir con el grupo congelado; use exclusiones explicitas.");}
        return q with{Conceptos=concepts,ConceptosExcluidos=excluded,Frecuencias=frequencies,Estados=q.Estados.Order(StringComparer.Ordinal).ToList(),TiposMovimiento=q.TiposMovimiento.Order(StringComparer.Ordinal).ToList()};
    }
    internal static decimal SumarExacto(decimal a,decimal b)
    {
        static (BigInteger N,int S) Parts(decimal n){var p=decimal.GetBits(n);var v=(BigInteger)(uint)p[0]+((BigInteger)(uint)p[1]<<32)+((BigInteger)(uint)p[2]<<64);return((p[3]&int.MinValue)!=0?-v:v,(p[3]>>16)&255);}
        var sum=checked(a+b);var x=Parts(a);var y=Parts(b);var z=Parts(sum);var scale=Math.Max(x.S,Math.Max(y.S,z.S));
        if(x.N*BigInteger.Pow(10,scale-x.S)+y.N*BigInteger.Pow(10,scale-y.S)!=z.N*BigInteger.Pow(10,scale-z.S))throw new RhNomError("PRECISION_ACUMULADOR","La suma pierde precision decimal; no se redondea implicitamente.");
        return sum;
    }
    public static RhNomAcumulacion Capturar(RhNomAcumuladorActual value,RhNomCargoActual source,IReadOnlyList<RhNomPartidaAcum> rows,CancellationToken ct=default)=>
        Capturar(value,new RhNomContextoHistorico(source.CodigoEmpresa,source.CodigoTipoNomina,source.CodigoPeriodo,source.FechaNomina),source.CodigoPersona,rows,ct);
    public static RhNomAcumulacion Capturar(RhNomAcumuladorActual value,RhNomContextoHistorico context,int person,IReadOnlyList<RhNomPartidaAcum> rows,CancellationToken ct=default)
    {
        if(context is null||context.CodigoEmpresa<=0||context.CodigoTipoNomina<=0||context.CodigoPeriodo is null or <=0||person<=0||context.FechaNomina.Kind!=DateTimeKind.Unspecified||rows is null)
            throw new RhNomError("CONTEXTO_ACUMULADOR_INVALIDO","Contexto Oracle y persona positivos requeridos.");
        var q=Validar(value);RhNomGruposAcum.ValidarUso(q.Grupo,context.CodigoEmpresa,context.CodigoTipoNomina,context.FechaNomina);if(rows.Count>MaxFilas)throw new RhNomError("LIMITE_ACUMULADOR","La lectura excede 2000 partidas; reduzca conceptos.");
        var ids=new HashSet<int>();var trace=new List<RhNomFilaAcum>();var total=0m;var included=0;
        foreach(var row in rows.OrderBy(r=>r?.Id))
        {
            ct.ThrowIfCancellationRequested();
            if(row is null||row.Id is null or <=0||!ids.Add(row.Id.Value)||row.CodigoEmpresa!=context.CodigoEmpresa||row.CodigoTipoNomina!=context.CodigoTipoNomina||row.CodigoPersona!=person||row.CodigoConcepto is null||!q.Conceptos.Contains(row.CodigoConcepto.Value))
                throw new RhNomError("PARTIDA_ACUMULADOR_INVALIDA","Partida duplicada, sin id o ajena al alcance; no se deduplica ni agrupa silenciosamente.");
            var reason=q.ConceptosExcluidos!.Contains(row.CodigoConcepto.Value)?"CONCEPTO_EXCLUIDO":!q.Estados.Contains(row.Estado??"<NULL>")?"ESTADO_EXCLUIDO":!q.TiposMovimiento.Contains(row.TipoMovimiento??"")?"MOVIMIENTO_EXCLUIDO":q.PoliticaFrecuencias=="LISTA"&&(row.FrecuenciaId is null||!q.Frecuencias!.Contains(row.FrecuenciaId.Value))?"FRECUENCIA_EXCLUIDA":"INCLUIDA";
            int? multiplicity=q.Grupo?.PoliticaDatos=="LEGACY_OBSERVADO"?q.Grupo.Filas.Count(r=>r.CodigoConcepto==row.CodigoConcepto):null;
            if(reason=="INCLUIDA")
            {
                if(row.FrecuenciaId is null or <=0)throw new RhNomError("FRECUENCIA_PARTIDA_INVALIDA","Una partida incluida carece de frecuencia valida.");
                var amount=RhNomNumeros.Leer(row.Monto);try{for(var occurrence=0;occurrence<(multiplicity??1);occurrence++)total=SumarExacto(total,amount);}catch(OverflowException){throw new RhNomError("DESBORDAMIENTO_ACUMULADOR","La suma excede decimal; no se entrega parcial.");}included++;
            }
            trace.Add(new(row,reason=="INCLUIDA",reason,multiplicity));
        }
        if(included==0&&q.SinFilas=="ERROR")throw new RhNomError("ACUMULADOR_SIN_PARTIDAS","Ninguna partida incluida y no se autorizo cero explicito.");
        var result=new RhNomAcumulacion(context.CodigoEmpresa,context.CodigoTipoNomina,context.CodigoPeriodo.Value,context.FechaNomina,person,q,trace,RhNomNumeros.Texto(total),"");
        return result with{Hash=Huella(result)};
    }
    public static string Huella(RhNomAcumulacion r)=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new{r.CodigoEmpresa,r.CodigoTipoNomina,r.CodigoPeriodo,r.FechaNomina,r.CodigoPersona,r.Definicion,r.Filas,r.Total,r.Origen,r.VersionMetodo},new JsonSerializerOptions(JsonSerializerDefaults.Web))));
    public static void Verificar(RhNomAcumulacion r,RhNomCargoActual source)
    {
        var restored=Capturar(r.Definicion,source,r.Filas.Select(f=>f.Partida).ToList());
        if(r.Hash!=Huella(r)||r.Hash!=restored.Hash)throw new RhNomError("ACUMULADOR_ALTERADO","Contexto, filtros, filas o total del acumulador no coinciden con su huella.");
    }
    public static async Task<RhNomAcumulacion> Leer(OracleConnection cn,RhNomCargoActual source,RhNomAcumuladorActual definition,CancellationToken ct)
    {
        var q=Validar(definition);using var c=new OracleCommand("RH.SP_RH_NOM_ACU_ACT",cn){BindByName=true,CommandType=CommandType.StoredProcedure,CommandTimeout=25};
        RhNomCfgDb.Add(c,"p_EMPRESA",OracleDbType.Int32,source.CodigoEmpresa);RhNomCfgDb.Add(c,"p_TIPO",OracleDbType.Int32,source.CodigoTipoNomina);RhNomCfgDb.Add(c,"p_PERIODO",OracleDbType.Int32,source.CodigoPeriodo);RhNomCfgDb.Add(c,"p_FECHA",OracleDbType.Date,source.FechaNomina);RhNomCfgDb.Add(c,"p_PERSONA",OracleDbType.Int32,source.CodigoPersona);RhNomCfgDb.Add(c,"p_CONCEPTOS",OracleDbType.Varchar2,string.Join(",",q.Conceptos));c.Parameters.Add("p_RESULT",OracleDbType.RefCursor,ParameterDirection.Output);
        var rows=new List<RhNomPartidaAcum>();using(var r=await c.ExecuteReaderAsync(ct))while(await r.ReadAsync(ct))
        {
            if(rows.Count>=MaxFilas)throw new RhNomError("LIMITE_ACUMULADOR","La lectura excede 2000 partidas; no se entrega suma truncada.");
            int? N(string n)=>r.IsDBNull(r.GetOrdinal(n))?null:RhNomCfgDb.Int(r,n);string? S(string n)=>r.IsDBNull(r.GetOrdinal(n))?null:RhNomCfgDb.Text(r,n);
            rows.Add(new(N("CODIGO_MOV_NOMINA"),RhNomCfgDb.Int(r,"CODIGO_EMPRESA"),RhNomCfgDb.Int(r,"CODIGO_TIPO_NOMINA"),RhNomCfgDb.Int(r,"CODIGO_PERSONA"),N("CODIGO_CONCEPTO"),N("FRECUENCIA_ID"),S("TIPO"),S("STATUS"),S("COMPLEMENTO_CONCEPTO"),RhNomReferencias.NormalizarMontoOracle(S("MONTO_TEXTO"))));
        }
        return Capturar(q,source,rows,ct);
    }
}
