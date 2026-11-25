using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record RhNomCasoHistorico(string CasoId,RhNomRecalcularHistorico Solicitud);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public record RhNomMatrizHistoricaQuery(List<int> TiposEsperados,List<RhNomCasoHistorico> Casos);
public record RhNomMuestraHistorica(string CasoId,int ModeloId,int Version,int? CodigoTipoNomina,int CodigoPeriodo,string FechaNomina,
    int CodigoPersona,int CodigoRelacionCargo,bool Comparado,bool CoincideCompleto,int Coincidentes,int Diferencias,int PartidasSinAsociar,
    int SalidasSinAsociar,string? HashEntradas,string? HashReferencia,string? Error);
public record RhNomResumenTipoHist(int CodigoTipoNomina,int Casos,int NominasReconstruidas,int NominasComparadas,int CasosConError,
    int CasosCoincidentesCompletos,int PartidasSinAsociar,int SalidasSinAsociar,bool MuestraSuficiente);
public record RhNomResultadoMatrizHist(string Estado,List<int> TiposEsperados,List<RhNomResumenTipoHist> Tipos,List<RhNomMuestraHistorica> Casos,
    bool MuestreoCompleto,bool CoincidenciaCompleta,string HashLote)
{
    public string VersionMetodo=>RhNomMatricesHistoricas.Version;
    public bool ParidadCertificada=>false;
    public bool MovimientosAplicados=>false;
}
public static class RhNomMatricesHistoricas
{
    public const string Version="MATRIZ_HISTORICA_V1";
    public const int MaxCasos=20;
    public static RhNomMatrizHistoricaQuery Validar(RhNomMatrizHistoricaQuery q)
    {
        if(q.TiposEsperados is null||q.TiposEsperados.Count is <1 or >20||q.TiposEsperados.Any(t=>t<=0)||q.TiposEsperados.Distinct().Count()!=q.TiposEsperados.Count)throw new RhNomError("TIPOS_MUESTRA_INVALIDOS","Declare 1..20 tipos esperados positivos y distintos; los ausentes se reportan.");
        if(q.Casos is null||q.Casos.Count is <1 or >MaxCasos)throw new RhNomError("LIMITE_CASOS_HISTORICOS","Indique 1..20 casos historicos por matriz.");
        var ids=new HashSet<string>(StringComparer.Ordinal);var contexts=new HashSet<(int,int,int,string,int,int)>();var rows=new List<RhNomCasoHistorico>();
        foreach(var row in q.Casos)
        {
            if(row?.Solicitud is not {} r)throw new RhNomError("CASO_HISTORICO_INVALIDO","Solicitud historica requerida por caso.");
            var id=RhNomNumeros.Codigo(row.CasoId);RhNomFuentesHistoricas.Fecha(r.FechaNomina);
            if(!ids.Add(id)||!contexts.Add((r.ModeloId,r.Version,r.CodigoPeriodo,r.FechaNomina,r.CodigoPersona,r.CodigoRelacionCargo)))throw new RhNomError("CASO_HISTORICO_DUPLICADO","Identificadores y contexto/persona/cargo/modelo deben ser distintos por caso.");
            if(r.ModeloId<=0||r.Version<=0||r.CodigoPeriodo<=0||r.CodigoPersona<=0||r.CodigoRelacionCargo<=0||r.PoliticaFuentes!="EQUIVALENTES_GUARDADOS")throw new RhNomError("CASO_HISTORICO_INVALIDO","Contexto positivo y politica explicita requeridos.");
            if(r.Comparar&&(r.HashReferenciaEsperado is null||r.HashReferenciaEsperado.Length!=64||r.HashReferenciaEsperado.Any(c=>!char.IsAsciiHexDigit(c))))throw new RhNomError("HASH_REFERENCIA_REQUERIDO","Cada comparacion requiere su hash de referencia capturado.");
            rows.Add(row with{CasoId=id});
        }
        return new(q.TiposEsperados.Order().ToList(),rows);
    }
    public static RhNomResultadoMatrizHist Resumir(List<int> expected,List<RhNomMuestraHistorica> cases)
    {
        var types=expected.Select(type=>
        {
            var rows=cases.Where(c=>c.CodigoTipoNomina==type).ToList();var valid=rows.Where(c=>c.Error is null).ToList();
            var reconstructed=valid.Select(c=>(c.CodigoPeriodo,c.FechaNomina)).Distinct().Count();var compared=valid.Where(c=>c.Comparado).Select(c=>(c.CodigoPeriodo,c.FechaNomina)).Distinct().Count();
            return new RhNomResumenTipoHist(type,rows.Count,reconstructed,compared,rows.Count(r=>r.Error is not null),valid.Count(r=>r.CoincideCompleto),valid.Sum(r=>r.PartidasSinAsociar),valid.Sum(r=>r.SalidasSinAsociar),compared>=2);
        }).ToList();
        var complete=types.All(t=>t.MuestraSuficiente);var hasErrors=cases.Any(c=>c.Error is not null);var match=complete&&!hasErrors&&cases.Count>0&&cases.All(c=>c.Comparado&&c.CoincideCompleto);
        var state=hasErrors?"DIAGNOSTICO_CON_ERRORES":complete?"DIAGNOSTICO_EVALUADO":"MUESTREO_INCOMPLETO";
        return new(state,expected,types,cases,complete,match,RhNomRecalculosHistoricos.Hash(new{Version,TiposEsperados=expected,Casos=cases}));
    }
}
public sealed class RhNomMatrizHistoricaHandler(ConnectionDB db)
{
    public Task<ResultDto<RhNomResultadoMatrizHist>> Handle(RhNomMatrizHistoricaQuery value,int company,CancellationToken ct)=>RhNomCfgDb.Resultado(async()=>
    {
        var q=RhNomMatricesHistoricas.Validar(value);var rows=new List<RhNomMuestraHistorica>();var models=new Dictionary<(int,int),RhNomModeloVersion>();var steps=0;
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);deadline.CancelAfter(TimeSpan.FromSeconds(60));
        try
        {
            foreach(var row in q.Casos)
            {
                deadline.Token.ThrowIfCancellationRequested();var r=row.Solicitud;int? type=null;string? error=null;RhNomRecalculoHistorico? result=null;
                if(!models.TryGetValue((r.ModeloId,r.Version),out var model))
                {
                    var loaded=await new RhNomVersionesHandler(db).Get(new(r.ModeloId,r.Version),company,deadline.Token);
                    if(loaded.IsValid&&loaded.Data is not null){model=loaded.Data;models.Add((r.ModeloId,r.Version),model);}else error=loaded.Message;
                }
                if(model is not null)
                {
                    type=model.CodigoTipoNomina;
                    if(!q.TiposEsperados.Contains(type.Value))error="El tipo del modelo no pertenece a los tipos esperados de la matriz.";
                    else {var response=await new RhNomRecalculoHistoricoHandler(db).Handle(r,company,deadline.Token);if(response.IsValid)result=response.Data;else error=response.Message;}
                }
                deadline.Token.ThrowIfCancellationRequested();
                if(result is not null)
                {
                    static int Steps(RhNomResultadoVigente sim)=>sim.Parametros.Count+sim.Simulacion.Resultados.Sum(x=>x.Pasos.Count+(x.Aplicabilidad?.Pasos.Count??0));
                    steps+=Steps(result.Simulacion)+(result.Comparacion is {} c?Steps(c.Simulacion):0);
                    if(steps>RhNomLotes.MaxPasos)throw new RhNomError("LIMITE_PASOS_MATRIZ","La matriz supera 10000 pasos; reduzca casos o formulas.");
                }
                var comparison=result?.Comparacion;var match=comparison is not null&&comparison.Filas.Count>0&&comparison.Filas.All(f=>f.Estado=="COINCIDE_IMPORTE")&&comparison.SinAsociar.Count==0&&comparison.SalidasSinAsociar.Count==0;
                rows.Add(new(row.CasoId,r.ModeloId,r.Version,type,r.CodigoPeriodo,r.FechaNomina,r.CodigoPersona,r.CodigoRelacionCargo,comparison is not null,match,comparison?.Coincidentes??0,comparison?.Filas.Count(f=>f.Estado!="COINCIDE_IMPORTE")??0,comparison?.SinAsociar.Count??0,comparison?.SalidasSinAsociar.Count??0,result?.HashEntradas,comparison?.HashReferencia,error??(result is null?"No se obtuvo resultado de este caso.":null)));
            }
        }
        catch(OperationCanceledException)when(!ct.IsCancellationRequested){throw new RhNomError("TIEMPO_MATRIZ_AGOTADO","La matriz excedio 60 segundos; reduzca los casos.");}
        var report=RhNomMatricesHistoricas.Resumir(q.TiposEsperados,rows);
        return new ResultDto<RhNomResultadoMatrizHist>(report){IsValid=true,Message="Matriz diagnostica evaluada; revise errores, cobertura y coincidencias. No certifica paridad."};
    });
}
public sealed partial class RhNominaController
{
    [HttpPost("historicoRecalcularLote")][RequestSizeLimit(65536)]
    public Task<IActionResult> RecalculateHistoricalMatrix(RhNomMatrizHistoricaQuery q)=>RunAsync("rh.nomina.historico.datos",(e,_,ct)=>new RhNomMatrizHistoricaHandler(db).Handle(q,e,ct),"rh.nomina.simular");
}
