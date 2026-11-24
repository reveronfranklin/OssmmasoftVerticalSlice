using System.Data;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Oracle.ManagedDataAccess.Client;
namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public record RhNomAcumHistoricoDef(int MesesAnteriores,RhNomAcumuladorActual Filtros,
    [property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] string? Ventana=null);
public record RhNomPartidaAcumHist(int CodigoPeriodo,DateTime FechaNomina,DateTime? FechaCierre,RhNomPartidaAcum Partida);
public record RhNomSumaHistorica(List<RhNomFilaAcum> Filas,string Total);
public record RhNomAcumHistorico(int CodigoEmpresa,int CodigoTipoNomina,int CodigoPeriodo,DateTime FechaNomina,int CodigoPersona,
    RhNomAcumHistoricoDef Definicion,DateTime Desde,DateTime HastaExclusiva,List<RhNomPartidaAcumHist> Partidas,
    RhNomSumaHistorica Calculo,string Hash)
{
    public string Origen=>Definicion.Ventana is null?"RH_HISTORICO_NOMINA_MESES_ANT":"RH_HISTORICO_NOMINA_MES";
    public string VersionMetodo=>Definicion.Ventana is null?RhNomAcumuladoresHistoricos.Version:RhNomAcumuladoresHistoricos.VersionMes;
    public bool EstadoHistoricoVerificado=>false;
    public bool IncluyeSalidasCorrida=>false;
}
public static class RhNomAcumuladoresHistoricos
{
    public const string Version="ACUMULADORES_HISTORICOS_V1",Proveedor="ACUMULADOR_HIST_MESES_ANT";
    public const string ProveedorMes="ACUMULADOR_HIST_MES_NOMINA",VersionMes="ACUMULADORES_MES_NOMINA_V1";
    public static bool EsProveedor(string value)=>value is Proveedor or ProveedorMes;
    public static string ProveedorPara(RhNomAcumHistoricoDef d)=>d.Ventana is null?Proveedor:ProveedorMes;
    public static void ValidarProveedor(string provider,RhNomAcumHistoricoDef d)
    {
        Validar(d);if(provider!=ProveedorPara(d))throw new RhNomError("VENTANA_PROVEEDOR_DISTINTA","Proveedor y ventana historica deben coincidir explicitamente.");
    }
    public static RhNomAcumHistoricoDef Validar(RhNomAcumHistoricoDef q)
    {
        if(q is null||q.Filtros is null||q.Ventana is not (null or "MES_NOMINA")||q.Ventana is null&&q.MesesAnteriores is <1 or >24||q.Ventana=="MES_NOMINA"&&q.MesesAnteriores!=0)throw new RhNomError("VENTANA_HISTORICA_INVALIDA","Indique 1..24 meses anteriores, o ventana MES_NOMINA con mesesAnteriores=0, y filtros explicitos.");
        return q with{Filtros=RhNomAcumuladores.Validar(q.Filtros)};
    }
    public static RhNomAcumHistorico Capturar(RhNomAcumHistoricoDef definition,RhNomContextoHistorico context,int person,IReadOnlyList<RhNomPartidaAcumHist> rows,CancellationToken ct=default)
    {
        var def=Validar(definition);
        if(context.CodigoEmpresa<=0||context.CodigoTipoNomina<=0||context.CodigoPeriodo is null or <=0||person<=0||context.FechaNomina.Kind!=DateTimeKind.Unspecified)
            throw new RhNomError("CONTEXTO_INVALIDO","Contexto explicito y persona positivos requeridos para acumulacion historica.");
        if(rows.Count>RhNomAcumuladores.MaxFilas)throw new RhNomError("LIMITE_ACUMULADOR","Hasta 2000 partidas historicas, sin truncar.");
        var until=new DateTime(context.FechaNomina.Year,context.FechaNomina.Month,1);DateTime since;
        try{since=def.Ventana is null?until.AddMonths(-def.MesesAnteriores):until;if(def.Ventana is not null)until=since.AddMonths(1);}catch(ArgumentOutOfRangeException){throw new RhNomError("VENTANA_HISTORICA_INVALIDA","Ventana anterior fuera del rango de fechas.");}
        var closes=new Dictionary<int,(DateTime,DateTime)>();
        foreach(var row in rows)
        {
            ct.ThrowIfCancellationRequested();
            if(row is null||row.CodigoPeriodo<=0||row.CodigoPeriodo==context.CodigoPeriodo||row.FechaNomina.Kind!=DateTimeKind.Unspecified||row.FechaNomina<since||row.FechaNomina>=until||row.FechaCierre is null||row.FechaCierre.Value.Kind!=DateTimeKind.Unspecified)
                throw new RhNomError("PARTIDA_HISTORICA_INVALIDA","Partida fuera de ventana, del periodo objetivo o sin cierre verificable.");
            var pair=(row.FechaNomina,row.FechaCierre.Value);
            if(closes.TryGetValue(row.CodigoPeriodo,out var previous)&&previous!=pair)throw new RhNomError("PERIODO_HISTORICO_AMBIGUO","Un periodo historico tiene fechas/cierres incompatibles.");
            closes[row.CodigoPeriodo]=pair;
        }
        var ordered=rows.OrderBy(r=>r.Partida?.Id).ToList();
        // Reuse exact arithmetic/filtering only. The original historical rows and their window
        // remain separately captured; Calculo is not evidence of current movements.
        var source=ContextoActual(context,person);
        var sum=RhNomAcumuladores.Capturar(def.Filtros,source,ordered.Select(r=>r.Partida).ToList(),ct);
        var result=new RhNomAcumHistorico(context.CodigoEmpresa,context.CodigoTipoNomina,context.CodigoPeriodo.Value,context.FechaNomina,person,def,since,until,ordered,new(sum.Filas,sum.Total),"");
        return result with{Hash=Huella(result)};
    }
    private static RhNomCargoActual ContextoActual(RhNomContextoHistorico c,int person)=>new(c.CodigoEmpresa,c.CodigoTipoNomina,c.CodigoPeriodo!.Value,c.FechaNomina,1,person,1,null,"0",null,null,null,null,null,"",null);
    public static RhNomContextoHistorico Contexto(RhNomCargoActual source)=>new(source.CodigoEmpresa,source.CodigoTipoNomina,source.CodigoPeriodo,source.FechaNomina);
    public static string Huella(RhNomAcumHistorico r)=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new{r.CodigoEmpresa,r.CodigoTipoNomina,r.CodigoPeriodo,r.FechaNomina,r.CodigoPersona,r.Definicion,r.Desde,r.HastaExclusiva,r.Partidas,r.Calculo,r.Origen,r.VersionMetodo},new JsonSerializerOptions(JsonSerializerDefaults.Web))));
    public static void Verificar(RhNomAcumHistorico r,RhNomContextoHistorico context,int person)
    {
        var restored=Capturar(r.Definicion,context,person,r.Partidas);
        if(r.Hash!=Huella(r)||r.Hash!=restored.Hash)throw new RhNomError("ACUMULADOR_HIST_ALTERADO","Contexto, ventana, filtros, filas o suma historica alterados.");
    }
    public static async Task<RhNomAcumHistorico> Leer(OracleConnection cn,RhNomContextoHistorico context,int person,RhNomAcumHistoricoDef definition,CancellationToken ct)
    {
        var def=Validar(definition);var empty=Capturar(def with{Filtros=def.Filtros with{SinFilas="CERO_EXPLICITO"}},context,person,[]);using var c=new OracleCommand(def.Ventana is null?"RH.SP_RH_NOM_ACU_HIST":"RH.SP_RH_NOM_ACU_MES",cn){BindByName=true,CommandType=CommandType.StoredProcedure,CommandTimeout=25};
        RhNomCfgDb.Add(c,"p_EMPRESA",OracleDbType.Int32,context.CodigoEmpresa);RhNomCfgDb.Add(c,"p_TIPO",OracleDbType.Int32,context.CodigoTipoNomina);RhNomCfgDb.Add(c,"p_PERIODO",OracleDbType.Int32,context.CodigoPeriodo);RhNomCfgDb.Add(c,"p_FECHA",OracleDbType.Date,context.FechaNomina);RhNomCfgDb.Add(c,"p_PERSONA",OracleDbType.Int32,person);RhNomCfgDb.Add(c,"p_MESES",OracleDbType.Int32,def.MesesAnteriores);RhNomCfgDb.Add(c,"p_DESDE",OracleDbType.Date,empty.Desde);RhNomCfgDb.Add(c,"p_CONCEPTOS",OracleDbType.Varchar2,string.Join(",",def.Filtros.Conceptos));c.Parameters.Add("p_RESULT",OracleDbType.RefCursor,ParameterDirection.Output);
        var rows=new List<RhNomPartidaAcumHist>();using(var r=await c.ExecuteReaderAsync(ct))while(await r.ReadAsync(ct))
        {
            if(rows.Count>=RhNomAcumuladores.MaxFilas)throw new RhNomError("LIMITE_ACUMULADOR","La lectura historica supera 2000 partidas; no se entrega parcial.");
            int? N(string n)=>r.IsDBNull(r.GetOrdinal(n))?null:RhNomCfgDb.Int(r,n);string? S(string n)=>r.IsDBNull(r.GetOrdinal(n))?null:RhNomCfgDb.Text(r,n);
            DateTime D(string n)=>DateTime.SpecifyKind(r.GetDateTime(r.GetOrdinal(n)),DateTimeKind.Unspecified);
            if(RhNomCfgDb.Int(r,"PERIODOS")!=1||r.IsDBNull(r.GetOrdinal("FECHA_CIERRE")))throw new RhNomError("PERIODO_HISTORICO_AMBIGUO","Partida sin periodo unico cerrado; no se ignora ni multiplica.");
            rows.Add(new(RhNomCfgDb.Int(r,"CODIGO_PERIODO"),D("FECHA_NOMINA"),D("FECHA_CIERRE"),new(N("CODIGO_HISTORICO_NOMINA"),RhNomCfgDb.Int(r,"CODIGO_EMPRESA"),RhNomCfgDb.Int(r,"CODIGO_TIPO_NOMINA"),RhNomCfgDb.Int(r,"CODIGO_PERSONA"),N("CODIGO_CONCEPTO"),N("FRECUENCIA_ID"),S("TIPO"),S("STATUS"),null,RhNomReferencias.NormalizarMontoOracle(S("MONTO_TEXTO")))));
        }
        return Capturar(def,context,person,rows,ct);
    }
}
