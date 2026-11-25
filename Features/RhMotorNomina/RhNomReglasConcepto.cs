using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public record RhNomConceptoCfg(int CodigoEmpresa,int CodigoTipoNomina,int CodigoConcepto,string Codigo,string Denominacion,
    string TipoConcepto,int? FrecuenciaId,string Estado,int? Automatico)
{
    public string Huella=>Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new{CodigoEmpresa,CodigoTipoNomina,CodigoConcepto,Codigo,Denominacion,TipoConcepto,FrecuenciaId,Estado,Automatico})));
    public bool Elegible=>TipoConcepto is "A" or "D"&&Estado=="A"&&Automatico==1&&FrecuenciaId>0;
}
public record RhNomReglaConceptoSave(string Clave,string VariableSalida,int CodigoConcepto,string HashConceptoEsperado,
    string TipoMovimiento,string PoliticaSigno,string AccionCero,string AccionNoAplica);
public record RhNomReglaConcepto(string Clave,string VariableSalida,RhNomConceptoCfg Concepto,string HashConceptoCapturado,
    string TipoMovimiento,string PoliticaSigno,string AccionCero,string AccionNoAplica);
public record RhNomPartidaPropuesta(string ClaveRegla,string VariableSalida,int CodigoConcepto,int FrecuenciaId,string TipoConcepto,
    string TipoMovimiento,string ResultadoCalculado,string Accion,string? MontoPropuesto);
public record RhNomProyeccionConceptos(List<RhNomPartidaPropuesta> Partidas)
{
    public string VersionMetodo=>RhNomReglasConcepto.Version;
    public string OrigenClasificacion=>"CATALOGO_ACTUAL_CAPTURADO_EN_BORRADOR";
    public bool ClasificacionHistoricaVerificada=>false;
    public bool MovimientosAplicados=>false;
}
public static class RhNomReglasConcepto
{
    public const string Version="REGLAS_CONCEPTO_V1";
    public static RhNomReglaConceptoSave Solicitud(RhNomReglaConcepto r)=>new(r.Clave,r.VariableSalida,r.Concepto.CodigoConcepto,r.HashConceptoCapturado,r.TipoMovimiento,r.PoliticaSigno,r.AccionCero,r.AccionNoAplica);
    public static List<RhNomReglaConcepto> Validar(IReadOnlyList<RhNomReglaConceptoSave>? rules,IReadOnlyList<RhNomVariable> vars,
        IReadOnlyList<RhNomConceptoCfg>? catalog,int company,int type,CancellationToken ct=default)
    {
        if(rules is null||rules.Count==0)return [];
        if(rules.Count>100)throw new RhNomError("LIMITE_REGLAS","Se admiten hasta 100 reglas de concepto.");
        if(catalog is null||catalog.Any(c=>c is null)||catalog.Select(c=>c.CodigoConcepto).Distinct().Count()!=catalog.Count)
            throw new RhNomError("CATALOGO_REQUERIDO","Se requiere catalogo unico de conceptos para validar reglas.");
        var concepts=catalog.ToDictionary(c=>c.CodigoConcepto);var outputs=vars.Where(v=>v.Origen=="CALCULADA"&&v.Unidad=="DINERO").Select(v=>v.Codigo).ToHashSet(StringComparer.Ordinal);
        var keys=new HashSet<string>(StringComparer.Ordinal);var used=new HashSet<string>(StringComparer.Ordinal);var targets=new HashSet<(int,int,string)>();var result=new List<RhNomReglaConcepto>();
        foreach(var r in rules)
        {
            ct.ThrowIfCancellationRequested();if(r is null)throw new RhNomError("REGLA_INVALIDA","Regla nula.");
            var key=RhNomNumeros.Codigo(r.Clave);var code=RhNomNumeros.Codigo(r.VariableSalida);
            if(!keys.Add(key)||!used.Add(code))throw new RhNomError("REGLA_DUPLICADA","Clave y salida deben ser unicas por regla.");
            if(!outputs.Contains(code))throw new RhNomError("SALIDA_MONETARIA_REQUERIDA","La regla requiere salida CALCULADA/DINERO.");
            if(!concepts.TryGetValue(r.CodigoConcepto,out var concept)||concept.CodigoEmpresa!=company||concept.CodigoTipoNomina!=type||!concept.Elegible)
                throw new RhNomError("CONCEPTO_NO_ELEGIBLE","Concepto actual de la empresa/tipo, activo, automatico, A/D y con frecuencia requerido.");
            if(!string.Equals(r.HashConceptoEsperado,concept.Huella,StringComparison.Ordinal))throw new RhNomError("CONCEPTO_CAMBIO","La metadata del concepto cambio; seleccione y revise nuevamente.");
            if(r.TipoMovimiento is not ("F" or "V")||r.PoliticaSigno!="CONSERVAR_FIRMADO"
                ||r.AccionCero is not ("REGISTRAR_CERO" or "OMITIR" or "CONSERVAR_EXISTENTE" or "RETIRAR_GENERADO")
                ||r.AccionNoAplica is not ("REGISTRAR_ALTERNATIVA" or "OMITIR" or "CONSERVAR_EXISTENTE" or "RETIRAR_GENERADO"))
                throw new RhNomError("POLITICA_REGLA_INVALIDA","Indique movimiento, signo firmado y acciones explicitas admitidas.");
            if(!targets.Add((concept.CodigoConcepto,concept.FrecuenciaId!.Value,r.TipoMovimiento)))throw new RhNomError("DESTINO_REGLA_DUPLICADO","La clave concepto/frecuencia/movimiento no puede repetirse.");
            result.Add(new(key,code,concept,concept.Huella,r.TipoMovimiento,r.PoliticaSigno,r.AccionCero,r.AccionNoAplica));
        }
        return result.OrderBy(r=>r.Clave,StringComparer.Ordinal).ToList();
    }
    public static RhNomProyeccionConceptos Proyectar(RhNomSnapshot snapshot,RhNomResultadoSimulacion simulation,CancellationToken ct=default)
    {
        if(simulation.Errores.Count>0)throw new RhNomError("SIMULACION_INVALIDA","No se proyectan resultados parciales.");
        if(snapshot.VersionReglas!=Version||snapshot.VersionUnidades!=RhNomUnidades.Version)throw new RhNomError("VERSION_REGLAS_INVALIDA","Revision de reglas/unidades incompatible.");
        var stored=snapshot.ReglasConcepto??[];
        if(stored.Any(r=>r is null||r.Concepto is null||r.HashConceptoCapturado!=r.Concepto.Huella))throw new RhNomError("REGLAS_ALTERADAS","La metadata de reglas no coincide con su huella.");
        var rules=Validar(stored.Select(Solicitud).ToList(),snapshot.Variables,stored.Select(r=>r.Concepto).Distinct().ToList(),snapshot.CodigoEmpresa,snapshot.CodigoTipoNomina,ct);
        var results=simulation.Resultados.ToDictionary(r=>r.VariableSalida,StringComparer.Ordinal);var rows=new List<RhNomPartidaPropuesta>();
        foreach(var rule in rules)
        {
            ct.ThrowIfCancellationRequested();if(!results.TryGetValue(rule.VariableSalida,out var row))throw new RhNomError("SALIDA_SIN_RESULTADO","La regla no tiene resultado.");
            var amount=RhNomNumeros.Leer(row.Resultado);string action;
            if(row.Aplicabilidad?.Resultado==false&&rule.AccionNoAplica!="REGISTRAR_ALTERNATIVA")action=rule.AccionNoAplica;
            else if(amount==0)action=rule.AccionCero;
            else action="REGISTRAR_RESULTADO";
            rows.Add(new(rule.Clave,rule.VariableSalida,rule.Concepto.CodigoConcepto,rule.Concepto.FrecuenciaId!.Value,rule.Concepto.TipoConcepto,rule.TipoMovimiento,row.Resultado,action,
                action is "REGISTRAR_RESULTADO" or "REGISTRAR_CERO"?row.Resultado:null));
        }
        return new(rows);
    }
}
