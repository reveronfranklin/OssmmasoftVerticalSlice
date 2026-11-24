using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public record RhNomCasoLote(string CasoId,DateOnly FechaReferencia,List<RhNomValorEntrada> Entradas);
public record RhNomLoteQuery(int ModeloId,int Version,List<RhNomCasoLote> Casos,[property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] string? VersionValores=null);
public record RhNomCasoSimulado(string CasoId,RhNomResultadoVigente Resultado);
public record RhNomErrorLote(string? CasoId,string Codigo,string Mensaje,string? Variable=null);
public record RhNomResultadoLote(int ModeloId,int Version,string HashModelo,string? HashLote,string Estado,
    int CasosSolicitados,List<RhNomCasoSimulado> Casos,List<RhNomErrorLote> Errores)
{
    public string VersionMetodo {get;init;}=RhNomLotes.Version;
    public string OrigenEntradas=>"PROPORCIONADAS_PARA_PRUEBA";
    public bool ParidadCertificada=>false;
    public bool MovimientosAplicados=>false;
}
public static class RhNomLotes
{
    public const string Version="SIMULACION_LOTE_V2";
    public const int MaxCasos=50,MaxValores=1000,MaxPasos=10000;
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web);
    public static List<RhNomCasoLote> Validar(RhNomLoteQuery value,CancellationToken ct=default)
    {
        if(value.ModeloId<=0||value.Version<=0)throw new RhNomError("VERSION_INVALIDA","Indique modelo y revision positivos.");
        if(value.Casos is null||value.Casos.Count is <1 or >MaxCasos)throw new RhNomError("LIMITE_CASOS","Indique 1..50 casos de prueba por lote.");
        if(value.VersionValores is not null&&value.VersionValores!=RhNomValores.Version)throw new RhNomError("VERSION_VALORES_INVALIDA","Version de entradas de lote no soportada.");
        var keys=new HashSet<string>(StringComparer.Ordinal);var normalized=new List<RhNomCasoLote>();var count=0;
        foreach(var row in value.Casos)
        {
            ct.ThrowIfCancellationRequested();
            if(row is null)throw new RhNomError("CASO_INVALIDO","Caso de prueba nulo.");
            var key=RhNomNumeros.Codigo(row.CasoId);
            if(!keys.Add(key))throw new RhNomError("CASO_DUPLICADO","Cada caso requiere identificador unico, sin distinguir mayusculas.");
            RhNomParametros.Fecha(row.FechaReferencia);
            if(row.Entradas is null||(count+=row.Entradas.Count)>MaxValores)throw new RhNomError("LIMITE_VALORES_LOTE","El lote admite hasta 1000 valores de entrada.");
            var inputs=value.VersionValores is null?RhNomModelos.Entradas(row.Entradas).Select(v=>new RhNomValorEntrada(v.Key,RhNomNumeros.Texto(v.Value))).ToList():RhNomValores.Estructura(row.Entradas);
            normalized.Add(new(key,row.FechaReferencia,inputs.OrderBy(v=>v.Codigo,StringComparer.Ordinal).ToList()));
        }
        return normalized;
    }
    public static RhNomResultadoLote Simular(RhNomModeloVersion saved,RhNomLoteQuery value,CancellationToken ct=default)
    {
        string? hash=null;string? current=null;
        RhNomResultadoLote Fail(RhNomErrorLote error)=>new(saved.ModeloId,saved.Version,saved.Hash,hash,"INVALIDO",value.Casos?.Count??0,[],[error]);
        try
        {
            var cases=Validar(value,ct);
            if(saved.ModeloId!=value.ModeloId||saved.Version!=value.Version||saved.Configuracion.ModeloId!=saved.ModeloId)
                throw new RhNomError("CONTEXTO_DISTINTO","La revision cargada debe coincidir exactamente con la del lote.");
            var document=new {VersionMetodo=Version,saved.Configuracion.CodigoEmpresa,saved.Configuracion.CodigoTipoNomina,saved.ModeloId,saved.Version,HashModelo=saved.Hash,Casos=cases};
            hash=Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(document,Json)));
            var results=new List<RhNomCasoSimulado>();var steps=0;
            foreach(var row in cases)
            {
                ct.ThrowIfCancellationRequested();current=row.CasoId;
                var response=RhNomParametros.Simular(saved,row.FechaReferencia,row.Entradas,ct);
                if(response.Simulacion.Errores.Count>0)
                {
                    var error=response.Simulacion.Errores[0];return Fail(new(current,error.Codigo,error.Mensaje,error.Variable));
                }
                steps+=response.Simulacion.Resultados.Sum(r=>r.Pasos.Count+(r.Aplicabilidad?.Pasos.Count??0));
                // Parameter lookup is work too; simple rules and table selections count toward the budget.
                steps+=response.Parametros.Count;
                if(steps>MaxPasos)throw new RhNomError("LIMITE_PASOS_LOTE","El lote supera 10000 pasos; reduzca casos o formulas.");
                results.Add(new(row.CasoId,response));
            }
            return new(saved.ModeloId,saved.Version,saved.Hash,hash,"SIMULACION_LOTE_COMPLETA",cases.Count,results,[]);
        }
        catch(RhNomError ex){return Fail(new(current,ex.Diagnostico.Codigo,ex.Message,ex.Diagnostico.Variable));}
        catch(OverflowException){return Fail(new(current,"DESBORDAMIENTO","El lote excede el rango decimal admitido."));}
    }
}
public sealed class RhNomLoteHandler(ConnectionDB db)
{
    public Task<ResultDto<RhNomResultadoLote>> Handle(RhNomLoteQuery value,int company,CancellationToken ct)=>RhNomCfgDb.Resultado(async()=>
    {
        RhNomLotes.Validar(value,ct);
        using var cn=await RhNomCfgDb.Abrir(db,ct);
        var saved=await RhNomCfgDb.Revision(cn,company,value.ModeloId,value.Version,ct);
        var response=RhNomLotes.Simular(saved,value,ct);
        return new ResultDto<RhNomResultadoLote>(response){IsValid=response.Errores.Count==0,Message=response.Errores.Count==0?
            "Lote de prueba simulado. Sin movimientos ni certificacion historica.":response.Errores[0].Mensaje};
    });
}
public sealed partial class RhNominaController
{
    [HttpPost("modelosSimularLote")]
    [RequestSizeLimit(524288)]
    public Task<IActionResult> SimulateBatch(RhNomLoteQuery value)=>RunAsync("rh.nomina.simular",(company,_,ct)=>new RhNomLoteHandler(db).Handle(value,company,ct));
}
