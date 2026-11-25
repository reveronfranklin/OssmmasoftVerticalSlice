using Microsoft.AspNetCore.Mvc;
using System.Numerics;
using System.Text.Json.Serialization;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public record RhNomAsociacionPrueba(string VariableSalida,int CodigoConcepto,int FrecuenciaId,string TipoMovimiento);
public record RhNomCompararPruebaQuery(int ModeloId,int Version,int CodigoPeriodo,string FechaNomina,int CodigoPersona,
    string HashReferenciaEsperado,List<RhNomValorEntrada> Entradas,List<RhNomAsociacionPrueba> Asociaciones,[property:JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] string? VersionValores=null);
public record RhNomFilaComparacion(string VariableSalida,int CodigoConcepto,int FrecuenciaId,string TipoMovimiento,
    string Estado,string? Esperado,string Obtenido,string? Diferencia,int? CodigoHistorico,string? EstadoRegistro);
public record RhNomComparacionPrueba(int ModeloId,int Version,string HashModelo,string HashReferencia,
    RhNomContextoHistorico Contexto,int CodigoPersona,string Estado,int Coincidentes,
    List<RhNomFilaComparacion> Filas,List<RhNomPartidaReferencia> SinAsociar,List<string> SalidasSinAsociar,RhNomResultadoVigente Simulacion)
{
    public string VersionMetodo {get;init;}=RhNomComparacionesPrueba.Version;
    public string OrigenEntradas {get;init;}="PROPORCIONADAS_PARA_PRUEBA";
    public bool ParidadCertificada=>false;
    public bool EntradasHistoricasVerificadas=>false;
    public bool ClasificacionConceptosVerificada=>false;
    public bool MovimientosAplicados=>false;
}
public static class RhNomComparacionesPrueba
{
    public const string Version="COMPARACION_PRUEBA_V2";
    private static string DiferenciaExacta(decimal obtained,decimal expected)
    {
        var result=checked(obtained-expected);
        static (BigInteger N,int Scale) Parts(decimal value)
        {
            var bits=decimal.GetBits(value);var n=(BigInteger)(uint)bits[0]+((BigInteger)(uint)bits[1]<<32)+((BigInteger)(uint)bits[2]<<64);
            return ((bits[3]&int.MinValue)!=0?-n:n,(bits[3]>>16)&0xff);
        }
        var a=Parts(obtained);var b=Parts(expected);var r=Parts(result);var scale=Math.Max(a.Scale,Math.Max(b.Scale,r.Scale));
        if(a.N*BigInteger.Pow(10,scale-a.Scale)-b.N*BigInteger.Pow(10,scale-b.Scale)!=r.N*BigInteger.Pow(10,scale-r.Scale))
            throw new RhNomError("PRECISION_DIFERENCIA","La diferencia no se representa exactamente como decimal; no se redondea ni se entrega parcial.");
        return RhNomNumeros.Texto(result);
    }
    public static List<RhNomAsociacionPrueba> Validar(RhNomCompararPruebaQuery value)
    {
        if(value.ModeloId<=0||value.Version<=0||value.CodigoPeriodo<=0||value.CodigoPersona<=0)
            throw new RhNomError("CONTEXTO_INVALIDO","Indique modelo/revision/periodo/persona positivos y explicitos.");
        RhNomFuentesHistoricas.Fecha(value.FechaNomina);
        if(value.HashReferenciaEsperado is null||value.HashReferenciaEsperado.Length!=64||value.HashReferenciaEsperado.Any(c=>!char.IsAsciiHexDigit(c)))
            throw new RhNomError("HASH_REFERENCIA_REQUERIDO","Capture primero la referencia y envie su SHA256 esperado.");
        if(value.Asociaciones is null||value.Asociaciones.Count is <1 or >RhNomModelos.MaxFormulas)
            throw new RhNomError("ASOCIACIONES_INVALIDAS","Indique 1..100 asociaciones de prueba.");
        var outputs=new HashSet<string>(StringComparer.Ordinal);var keys=new HashSet<(int,int,string)>();var result=new List<RhNomAsociacionPrueba>();
        foreach(var row in value.Asociaciones)
        {
            if(row is null||row.CodigoConcepto<=0||row.FrecuenciaId<=0||row.TipoMovimiento is not ("F" or "V"))
                throw new RhNomError("ASOCIACION_INVALIDA","Concepto/frecuencia positivos y movimiento F o V requeridos.");
            var code=RhNomNumeros.Codigo(row.VariableSalida);
            if(!outputs.Add(code)||!keys.Add((row.CodigoConcepto,row.FrecuenciaId,row.TipoMovimiento)))
                throw new RhNomError("ASOCIACION_DUPLICADA","Cada salida y clave concepto/frecuencia/movimiento admite una sola asociacion.");
            result.Add(row with{VariableSalida=code});
        }
        if(value.VersionValores is null)RhNomModelos.Entradas(value.Entradas);
        else if(value.VersionValores==RhNomValores.Version)RhNomValores.Estructura(value.Entradas);
        else throw new RhNomError("VERSION_VALORES_INVALIDA","Version de entradas no soportada.");
        return result;
    }
    public static RhNomComparacionPrueba Comparar(RhNomModeloVersion saved,RhNomCompararPruebaQuery value,RhNomReferenciaCapturada reference,CancellationToken ct=default)
    {
        if(saved.Configuracion.VersionUnidades!=RhNomUnidades.Version)
            throw new RhNomError("VALIDACION_UNIDADES_REQUERIDA","La comparacion requiere una revision con unidades validadas.");
        if(reference.Documento.Formato!=1)throw new RhNomError("REFERENCIA_NO_COMPARABLE","Formato de referencia no soportado.");
        var bindings=Validar(value);var date=RhNomFuentesHistoricas.Fecha(value.FechaNomina);
        if(saved.ModeloId!=value.ModeloId||saved.Version!=value.Version||saved.Configuracion.ModeloId!=value.ModeloId
            ||reference.Documento.Contexto!=new RhNomContextoHistorico(saved.Configuracion.CodigoEmpresa,saved.Configuracion.CodigoTipoNomina,value.CodigoPeriodo,date)
            ||reference.Documento.CodigoPersona!=value.CodigoPersona)
            throw new RhNomError("CONTEXTO_DISTINTO","Modelo, persona, empresa, tipo, periodo y fecha deben coincidir exactamente.");
        if(!RhNomReferencias.Verificar(reference)||!string.Equals(reference.Hash,value.HashReferenciaEsperado,StringComparison.OrdinalIgnoreCase))
            throw new RhNomError("REFERENCIA_CAMBIO","La referencia cambio o no coincide con su hash; vuelva a capturar y revisar.");
        // Rebuild validation from actual rows, not from caller-supplied observation flags.
        var verified=RhNomReferencias.Capturar(new(saved.CodigoTipoNomina,value.CodigoPeriodo,value.FechaNomina,value.CodigoPersona),saved.Configuracion.CodigoEmpresa,reference.Documento.Partidas,ct);
        if(!verified.IntegridadEstructural)throw new RhNomError("REFERENCIA_NO_COMPARABLE","La referencia tiene claves/ids/importes invalidos o duplicados. No se consolida por SUM.");
        var money=saved.Configuracion.Variables.Where(v=>v.Origen=="CALCULADA"&&v.Unidad=="DINERO").Select(v=>v.Codigo).ToHashSet(StringComparer.Ordinal);
        foreach(var row in bindings)if(!money.Contains(row.VariableSalida))throw new RhNomError("SALIDA_MONETARIA_REQUERIDA","Solo una salida CALCULADA/DINERO puede asociarse con un importe historico.");
        // The evaluator receives only supplied entries and the saved revision, never expected amounts.
        var simulation=RhNomParametros.Simular(saved,DateOnly.FromDateTime(date),value.Entradas,ct);
        if(simulation.Simulacion.Errores.Count>0){var error=simulation.Simulacion.Errores[0];throw new RhNomError(error.Codigo,error.Mensaje,error.Posicion,error.Variable);}
        var actual=simulation.Simulacion.Resultados.ToDictionary(r=>r.VariableSalida,StringComparer.Ordinal);
        var expected=verified.Documento.Partidas.ToDictionary(r=>(r.CodigoConcepto!.Value,r.FrecuenciaId!.Value,r.TipoMovimiento!));
        var rows=new List<RhNomFilaComparacion>();var boundKeys=new HashSet<(int,int,string)>();
        foreach(var row in bindings)
        {
            ct.ThrowIfCancellationRequested();var key=(row.CodigoConcepto,row.FrecuenciaId,row.TipoMovimiento);boundKeys.Add(key);
            if(!actual.TryGetValue(row.VariableSalida,out var output))throw new RhNomError("SALIDA_SIN_RESULTADO","La salida asociada no tiene resultado de calculo.");
            var got=RhNomNumeros.Leer(output.Resultado);
            if(!expected.TryGetValue(key,out var wanted))rows.Add(new(row.VariableSalida,row.CodigoConcepto,row.FrecuenciaId,row.TipoMovimiento,"SIN_PARTIDA_REFERENCIA",null,output.Resultado,null,null,null));
            else
            {
                var amount=RhNomNumeros.Leer(wanted.Monto);string delta;
                try{delta=DiferenciaExacta(got,amount);}catch(OverflowException){throw new RhNomError("DESBORDAMIENTO","La diferencia excede el rango decimal; no se entrega comparacion parcial.");}
                var state=got==amount?"COINCIDE_IMPORTE":got!=0&&amount!=0&&Math.Sign(got)!=Math.Sign(amount)?"SIGNO_DISTINTO":"MONTO_DISTINTO";
                rows.Add(new(row.VariableSalida,row.CodigoConcepto,row.FrecuenciaId,row.TipoMovimiento,state,wanted.Monto,output.Resultado,delta,wanted.CodigoHistorico,wanted.EstadoRegistro));
            }
        }
        var unmatched=verified.Documento.Partidas.Where(r=>!boundKeys.Contains((r.CodigoConcepto!.Value,r.FrecuenciaId!.Value,r.TipoMovimiento!))).ToList();
        var boundOutputs=bindings.Select(b=>b.VariableSalida).ToHashSet(StringComparer.Ordinal);
        return new(saved.ModeloId,saved.Version,saved.Hash,reference.Hash,reference.Documento.Contexto,value.CodigoPersona,"DIAGNOSTICO_NO_CERTIFICADO",rows.Count(r=>r.Estado=="COINCIDE_IMPORTE"),rows,unmatched,actual.Keys.Where(k=>!boundOutputs.Contains(k)).Order(StringComparer.Ordinal).ToList(),simulation);
    }
}
public sealed class RhNomComparacionPruebaHandler(ConnectionDB db)
{
    public Task<ResultDto<RhNomComparacionPrueba>> Handle(RhNomCompararPruebaQuery value,int company,CancellationToken ct)=>RhNomCfgDb.Resultado(async()=>
    {
        RhNomComparacionesPrueba.Validar(value);RhNomModeloVersion saved;
        using(var cn=await RhNomCfgDb.Abrir(db,ct))saved=await RhNomCfgDb.Revision(cn,company,value.ModeloId,value.Version,ct);
        var reference=await new RhNomReferenciaHistoricaHandler(db).Get(new(saved.CodigoTipoNomina,value.CodigoPeriodo,value.FechaNomina,value.CodigoPersona),company,ct);
        if(!reference.IsValid||reference.Data is null)return new ResultDto<RhNomComparacionPrueba>(default!){IsValid=false,Message=reference.Message};
        var result=RhNomComparacionesPrueba.Comparar(saved,value,reference.Data,ct);
        return new ResultDto<RhNomComparacionPrueba>(result){IsValid=true,Message="Diagnostico numerico calculado. Asociaciones y entradas de prueba; no certifica paridad ni aplica movimientos."};
    });
}
public sealed partial class RhNominaController
{
    [HttpPost("historicoCompararPrueba")]
    [RequestSizeLimit(131072)]
    public Task<IActionResult> HistoricalCompareTest(RhNomCompararPruebaQuery value)=>RunAsync("rh.nomina.historico.datos",(company,_,ct)=>new RhNomComparacionPruebaHandler(db).Handle(value,company,ct),"rh.nomina.simular");
}
