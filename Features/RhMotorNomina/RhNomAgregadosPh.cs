using System.Data;
using System.Text.Json;
using Oracle.ManagedDataAccess.Client;
namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public record RhNomAgregadosPhDef(RhNomGrupoAcum? GrupoSi,List<int> ConceptosSueldoSi,List<int> ConceptosSueldoAf,
    List<int> FrecuenciasExcluidas,string PoliticaNulos);
public record RhNomAgregadosPhDoc(int Formato,RhNomCargoActual Fuente,RhNomAgregadosPhDef Definicion,
    RhNomReferenciaIntegralCapturada Referencia,List<RhNomPartidaAcum> Actuales,List<RhNomPartidaAcumHist> Historicas);
public record RhNomAgregadosPhCapturados(RhNomAgregadosPhDoc Documento,SortedDictionary<string,string> Valores,string Hash)
{
    public string VersionMetodo=>RhNomAgregadosPh.Version;
    public bool EstadoHistoricoVerificado=>false;
}
// Independent source aggregation only. Factors/rates, branch selection and the
// ambiguity policy stay in the ordinary configurable formula graph.
public static class RhNomAgregadosPh
{
    public const string Version="AGREGADOS_INTEGRAL_PH_V1";
    public static readonly IReadOnlyDictionary<string,string> Proveedores=new System.Collections.ObjectModel.ReadOnlyDictionary<string,string>(new Dictionary<string,string>{
        ["PH_SI_ACT_EXCL"]="MIG_PH_ACT_EX",["PH_SI_HIST_TOTAL"]="MIG_PH_HIST",["PH_SI_HIST_MIN"]="MIG_PH_HMIN",["PH_SI_HIST_MAX"]="MIG_PH_HMAX",["PH_SI_ACT_SUELDO"]="MIG_PH_ACT_SS",["PH_SI_ACT_OTROS"]="MIG_PH_ACT_OT",["PH_SI_HEV_SUELDO"]="MIG_PH_HEV_SS",["PH_SI_HEV_OTROS"]="MIG_PH_HEV_OT",["PH_AF_ACT_SUELDO"]="MIG_PH_AF_SS"});
    public static string Descripcion(string provider)=>provider switch{
        "PH_SI_ACT_EXCL"=>"PH: actual SI F/V sin las frecuencias excluidas",
        "PH_SI_HIST_TOTAL"=>"PH: total historico SI del mes de referencia",
        "PH_SI_HIST_MIN"=>"PH: menor importe historico SI del mes de referencia",
        "PH_SI_HIST_MAX"=>"PH: mayor importe historico SI del mes de referencia",
        "PH_SI_ACT_SUELDO"=>"PH: actual SI F/V de conceptos de sueldo",
        "PH_SI_ACT_OTROS"=>"PH: actual SI F/V de otros conceptos",
        "PH_SI_HEV_SUELDO"=>"PH: historico SI E/V de conceptos de sueldo",
        "PH_SI_HEV_OTROS"=>"PH: historico SI E/V de otros conceptos",
        "PH_AF_ACT_SUELDO"=>"PH: actual de conceptos de sueldo configurados para AF",
        _=>throw new RhNomError("PROVEEDOR_PH_INVALIDO","Proveedor PH no registrado.")};
    public static RhNomAgregadosPhDef Validar(RhNomAgregadosPhDef d,RhNomCargoActual source)
    {
        var result=ValidarDefinicion(d);RhNomGruposAcum.ValidarUso(result.GrupoSi,source.CodigoEmpresa,source.CodigoTipoNomina,source.FechaNomina);return result;
    }
    public static RhNomAgregadosPhDef ValidarDefinicion(RhNomAgregadosPhDef d)
    {
        if(d is null||d.PoliticaNulos!="CERO_NVL_EXPLICITO")throw new RhNomError("AGREGADOS_PH_INVALIDOS","Definicion, contexto y politica CERO_NVL_EXPLICITO requeridos.");
        List<int> Ids(List<int> ids){if(ids is null||ids.Count>100||ids.Any(i=>i<=0)||ids.Distinct().Count()!=ids.Count)throw new RhNomError("IDS_PH_INVALIDOS","Hasta 100 ids positivos distintos por conjunto.");return ids.Order().ToList();}
        var salary=Ids(d.ConceptosSueldoSi);var af=Ids(d.ConceptosSueldoAf);var frequencies=Ids(d.FrecuenciasExcluidas);
        if(d.GrupoSi is not null)RhNomGruposAcum.Verificar(d.GrupoSi);
        if(d.GrupoSi is {} g&&g.Codigo!="SI"||salary.Any(id=>d.GrupoSi is null||!d.GrupoSi.Conceptos.Contains(id)))throw new RhNomError("GRUPO_SI_INVALIDO","Grupo SI y subconjunto explicito de conceptos sueldo requeridos.");
        if(d.GrupoSi is not null&&frequencies.Count==0)throw new RhNomError("FRECUENCIA_PH_REQUERIDA","Declare las frecuencias excluidas del actual SI; no se infieren ids.");
        if((d.GrupoSi?.Conceptos??[]).Union(af).Count()>100)throw new RhNomError("LIMITE_CONCEPTOS_PH","Hasta 100 conceptos en la lectura combinada SI/AF.");
        return d with{ConceptosSueldoSi=salary,ConceptosSueldoAf=af,FrecuenciasExcluidas=frequencies};
    }
    public static RhNomAgregadosPhCapturados Capturar(RhNomAgregadosPhDoc doc,CancellationToken ct=default)
    {
        if(doc is null||doc.Formato!=1||doc.Fuente is null||doc.Fuente.CodigoEmpresa<=0||doc.Fuente.CodigoTipoNomina<=0||doc.Fuente.CodigoPersona<=0||doc.Fuente.CodigoRelacionCargo<=0||doc.Fuente.CodigoPeriodo<=0||doc.Fuente.FechaNomina.Kind!=DateTimeKind.Unspecified)throw new RhNomError("CONTEXTO_PH_INVALIDO","Contexto individual explicito requerido.");
        var source=doc.Fuente;var d=Validar(doc.Definicion,source);var context=RhNomAcumuladoresHistoricos.Contexto(source);RhNomReferenciaIntegral.Verificar(doc.Referencia,context);
        if(doc.Actuales is null||doc.Historicas is null||doc.Actuales.Count+doc.Historicas.Count>5000||doc.Actuales.Count>2000||doc.Historicas.Count>2000)throw new RhNomError("LIMITE_FILAS_PH","Hasta 2000 filas por origen; no se trunca.");
        var si=(d.GrupoSi?.Conceptos??[]).ToHashSet();var allowed=si.Union(d.ConceptosSueldoAf).ToHashSet();var sums=new Dictionary<string,decimal>{{"MIG_PH_ACT_EX",0},{"MIG_PH_HIST",0},{"MIG_PH_ACT_SS",0},{"MIG_PH_ACT_OT",0},{"MIG_PH_HEV_SS",0},{"MIG_PH_HEV_OT",0},{"MIG_PH_AF_SS",0}};
        var ids=new HashSet<int>();var histIds=new HashSet<int>();decimal? minimum=null,maximum=null;
        decimal Amount(RhNomPartidaAcum row,HashSet<int> seen,HashSet<int> scope)
        {
            if(row is null||row.Id is null or <=0||!seen.Add(row.Id.Value)||row.CodigoEmpresa!=source.CodigoEmpresa||row.CodigoTipoNomina!=source.CodigoTipoNomina||row.CodigoPersona!=source.CodigoPersona||row.CodigoConcepto is null||!scope.Contains(row.CodigoConcepto.Value))throw new RhNomError("FILA_PH_INVALIDA","Fila duplicada, sin id o ajena al alcance PH.");
            return row.Monto is null?0:RhNomNumeros.Leer(row.Monto);
        }
        int Multiplicity(int id)=>d.GrupoSi!.Filas.Count(r=>r.CodigoConcepto==id);
        void Sum(string key,decimal amount,int count=1){for(int i=0;i<count;i++)sums[key]=RhNomAcumuladores.SumarExacto(sums[key],amount);}
        try{
            foreach(var row in doc.Actuales.OrderBy(r=>r?.Id))
            {
                ct.ThrowIfCancellationRequested();var amount=Amount(row,ids,allowed);var id=row.CodigoConcepto!.Value;
                // AF source has no movement/frequency/status filter in the legacy
                // branch. The configured concept set, not a hardcoded module id,
                // defines its scope. SI current branches include only F/V.
                if(d.ConceptosSueldoAf.Contains(id))Sum("MIG_PH_AF_SS",amount);
                if(si.Contains(id)&&row.TipoMovimiento is "F" or "V")
                {
                    var mult=Multiplicity(id);Sum(d.ConceptosSueldoSi.Contains(id)?"MIG_PH_ACT_SS":"MIG_PH_ACT_OT",amount,mult);
                    // Oracle NULL <> excluded_frequency is unknown, not true.
                    if(row.FrecuenciaId is not null&&!d.FrecuenciasExcluidas.Contains(row.FrecuenciaId.Value))Sum("MIG_PH_ACT_EX",amount,mult);
                }
            }
            var periodDates=new Dictionary<int,(DateTime,DateTime)>();
            foreach(var row in doc.Historicas.OrderBy(r=>r?.Partida?.Id))
            {
                ct.ThrowIfCancellationRequested();var date=doc.Referencia.FechaReferencia;
                if(row is null||row.CodigoPeriodo<=0||row.CodigoPeriodo==source.CodigoPeriodo||row.FechaNomina.Kind!=DateTimeKind.Unspecified||row.FechaNomina.Year!=date.Year||row.FechaNomina.Month!=date.Month||row.FechaCierre is null||row.FechaCierre.Value.Kind!=DateTimeKind.Unspecified)throw new RhNomError("HISTORICO_PH_INVALIDO","Historico fuera del mes de referencia, del periodo objetivo o sin cierre.");
                var pair=(row.FechaNomina,row.FechaCierre.Value);if(periodDates.TryGetValue(row.CodigoPeriodo,out var prior)&&prior!=pair)throw new RhNomError("PERIODO_PH_AMBIGUO","Fechas de periodo historico incompatibles.");periodDates[row.CodigoPeriodo]=pair;
                var amount=Amount(row.Partida,histIds,si);var id=row.Partida.CodigoConcepto!.Value;var mult=Multiplicity(id);
                Sum("MIG_PH_HIST",amount,mult);minimum=minimum is null?amount:Math.Min(minimum.Value,amount);maximum=maximum is null?amount:Math.Max(maximum.Value,amount);
                if(row.Partida.TipoMovimiento is "E" or "V")Sum(d.ConceptosSueldoSi.Contains(id)?"MIG_PH_HEV_SS":"MIG_PH_HEV_OT",amount,mult);
            }
        }catch(OverflowException){throw new RhNomError("DESBORDAMIENTO_PH","Los agregados exceden decimal; no se entrega un resultado parcial.");}
        // Empty aggregates correspond to the declared NVL policy. Positive factor
        // scaling cannot change the candidate signs used by the formula graph.
        sums["MIG_PH_HMIN"]=minimum??0;sums["MIG_PH_HMAX"]=maximum??0;
        var values=new SortedDictionary<string,string>(sums.ToDictionary(k=>k.Key,k=>RhNomNumeros.Texto(k.Value)),StringComparer.Ordinal);
        var canonical=doc with{Definicion=d,Actuales=doc.Actuales.OrderBy(r=>r.Id).ToList(),Historicas=doc.Historicas.OrderBy(r=>r.Partida.Id).ToList()};
        var hash=RhNomCorridas.Hash(JsonSerializer.Serialize(new{Documento=canonical,Valores=values,VersionMetodo=Version},RhNomCorridas.Json));return new(canonical,values,hash);
    }
    public static void Verificar(RhNomAgregadosPhCapturados value,RhNomCargoActual source)
    {
        if(value?.Documento?.Fuente!=source||value.Valores is null)throw new RhNomError("CONTEXTO_AGREGADO_PH_DISTINTO","Agregados ajenos a la fuente actual.");
        var expected=Capturar(value.Documento);
        if(expected.Hash!=value.Hash||!expected.Valores.SequenceEqual(value.Valores))throw new RhNomError("AGREGADO_PH_ALTERADO","Filas, referencia o agregados PH alterados.");
    }
    public static async Task<RhNomAgregadosPhCapturados> Leer(OracleConnection cn,RhNomCargoActual source,RhNomAgregadosPhDef definition,CancellationToken ct)
    {
        var d=Validar(definition,source);var reference=await RhNomReferenciaIntegral.Leer(cn,RhNomAcumuladoresHistoricos.Contexto(source),ct);var current=new List<RhNomPartidaAcum>();var history=new List<RhNomPartidaAcumHist>();
        foreach(var origin in new[]{"ACTUAL","HISTORICO"})
        {
            var concepts=(origin=="ACTUAL"?(d.GrupoSi?.Conceptos??[]).Union(d.ConceptosSueldoAf):(d.GrupoSi?.Conceptos??[])).Order().ToList();if(concepts.Count==0)continue;
            using var c=new OracleCommand("RH.SP_RH_NOM_PH_FUE",cn){CommandType=CommandType.StoredProcedure,BindByName=true,CommandTimeout=25};
            RhNomCfgDb.Add(c,"p_EMPRESA",OracleDbType.Int32,source.CodigoEmpresa);RhNomCfgDb.Add(c,"p_TIPO",OracleDbType.Int32,source.CodigoTipoNomina);RhNomCfgDb.Add(c,"p_PERIODO",OracleDbType.Int32,source.CodigoPeriodo);RhNomCfgDb.Add(c,"p_FECHA",OracleDbType.Date,source.FechaNomina);RhNomCfgDb.Add(c,"p_PERSONA",OracleDbType.Int32,source.CodigoPersona);RhNomCfgDb.Add(c,"p_REFERENCIA",OracleDbType.Date,reference.FechaReferencia);RhNomCfgDb.Add(c,"p_ORIGEN",OracleDbType.Varchar2,origin);RhNomCfgDb.Add(c,"p_CONCEPTOS",OracleDbType.Varchar2,string.Join(",",concepts));c.Parameters.Add("p_RESULT",OracleDbType.RefCursor,ParameterDirection.Output);
            using var r=await c.ExecuteReaderAsync(ct);while(await r.ReadAsync(ct))
            {
                if((origin=="ACTUAL"?current.Count:history.Count)>=2000)throw new RhNomError("LIMITE_FILAS_PH","Lectura PH supera 2000 filas por origen; no se trunca.");
                int? N(string code)=>r.IsDBNull(r.GetOrdinal(code))?null:RhNomCfgDb.Int(r,code);string? S(string code)=>r.IsDBNull(r.GetOrdinal(code))?null:RhNomCfgDb.Text(r,code);DateTime D(string code)=>DateTime.SpecifyKind(r.GetDateTime(r.GetOrdinal(code)),DateTimeKind.Unspecified);
                var row=new RhNomPartidaAcum(N("ID_FILA"),RhNomCfgDb.Int(r,"CODIGO_EMPRESA"),RhNomCfgDb.Int(r,"CODIGO_TIPO_NOMINA"),RhNomCfgDb.Int(r,"CODIGO_PERSONA"),N("CODIGO_CONCEPTO"),N("FRECUENCIA_ID"),S("TIPO"),S("STATUS"),null,RhNomReferencias.NormalizarMontoOracle(S("MONTO_TEXTO")));
                if(origin=="ACTUAL")current.Add(row);
                else{if(RhNomCfgDb.Int(r,"PERIODOS")!=1||r.IsDBNull(r.GetOrdinal("FECHA_CIERRE")))throw new RhNomError("PERIODO_PH_AMBIGUO","Historico sin periodo unico cerrado.");history.Add(new(RhNomCfgDb.Int(r,"CODIGO_PERIODO"),D("FECHA_NOMINA"),D("FECHA_CIERRE"),row));}
            }
        }
        return Capturar(new(1,source,d,reference,current,history),ct);
    }
}
