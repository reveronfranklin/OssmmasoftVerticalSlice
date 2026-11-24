using System.Globalization;
using System.Text.Json.Serialization;

namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public record RhNomParametro(string Clave, string CodigoVariable, string Valor, DateOnly Desde,
    DateOnly? Hasta = null, int? CodigoTipoNomina = null);
public record RhNomParametroResuelto(string Clave, string CodigoVariable, string Valor, string Unidad,
    string Ambito, DateOnly Desde, DateOnly? Hasta, int? CodigoTipoNomina,
    [property: JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] RhNomTramoElegido? Tramo = null);
public record RhNomSimularVigente(int ModeloId, int Version, DateOnly FechaReferencia, List<RhNomValorEntrada> Entradas);
public record RhNomResultadoVigente(int ModeloId, int Version, string Hash, DateOnly FechaReferencia,
    List<RhNomParametroResuelto> Parametros, RhNomResultadoSimulacion Simulacion,
    [property: JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] RhNomProyeccionConceptos? ProyeccionConceptos = null,
    [property: JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] List<RhNomDecisionControl>? AccionesControl = null);

// Pure date-based resolver. No clock, salary access or implicit parameter fallback.
public static class RhNomParametros
{
    public const int MaxReglas = 500;
    public static List<RhNomParametro> Validar(IReadOnlyList<RhNomParametro>? rules,
        IReadOnlyList<RhNomVariable> variables, int payrollType, CancellationToken ct = default)
    {
        if (rules is null) return [];
        if (rules.Count > MaxReglas)
            throw new RhNomError("LIMITE_PARAMETROS", "Se admiten hasta 500 reglas de parametros por revision.");
        var catalog = variables.ToDictionary(v => v.Codigo, StringComparer.Ordinal);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<RhNomParametro>();
        foreach (var rule in rules)
        {
            ct.ThrowIfCancellationRequested();
            if (rule is null) throw new RhNomError("PARAMETRO_INVALIDO", "Regla de parametro nula.");
            var key = RhNomNumeros.Codigo(rule.Clave);
            var code = RhNomNumeros.Codigo(rule.CodigoVariable);
            if (!keys.Add(key)) throw new RhNomError("CLAVE_PARAMETRO_DUPLICADA", "Cada regla requiere una clave unica en la revision.");
            if (!catalog.TryGetValue(code, out var variable) || variable.Origen != "PARAMETRO")
                throw new RhNomError("PARAMETRO_NO_DECLARADO", $"[{code}] debe estar declarado como PARAMETRO en el modelo.");
            Fecha(rule.Desde);
            if (rule.Hasta is { } end)
            {
                Fecha(end);
                if (end < rule.Desde) throw new RhNomError("VIGENCIA_INVALIDA", "Hasta no puede ser anterior a Desde.");
            }
            if (rule.CodigoTipoNomina is { } type && (type <= 0 || type != payrollType))
                throw new RhNomError("AMBITO_PARAMETRO_INVALIDO", "El tipo especifico debe coincidir con el tipo de nomina del modelo.");
            var value = RhNomValores.Normalizar(variable.Tipo,rule.Valor);
            result.Add(rule with { Clave = key, CodigoVariable = code, Valor = value });
        }
        // Inclusive intervals. Same-priority overlaps are rejected even when values match.
        foreach (var group in result.GroupBy(r => (r.CodigoVariable, r.CodigoTipoNomina)))
        {
            RhNomParametro? previous = null;
            foreach (var rule in group.OrderBy(r => r.Desde))
            {
                ct.ThrowIfCancellationRequested();
                if (previous is not null && (previous.Hasta is null || rule.Desde <= previous.Hasta.Value))
                    throw new RhNomError("VIGENCIAS_SUPERPUESTAS", $"[{rule.CodigoVariable}] tiene vigencias superpuestas en el mismo ambito.");
                previous = rule;
            }
        }
        return result.OrderBy(r => r.CodigoVariable, StringComparer.Ordinal)
            .ThenBy(r => r.CodigoTipoNomina).ThenBy(r => r.Desde).ThenBy(r => r.Clave, StringComparer.Ordinal).ToList();
    }
    public static void Fecha(DateOnly date)
    {
        if (date.Year < 1900) throw new RhNomError("FECHA_REFERENCIA_INVALIDA", "Indique una fecha explicita desde 1900, en formato yyyy-MM-dd.");
    }
    public static List<RhNomParametroResuelto> Resolver(RhNomSnapshot snapshot, DateOnly date, CancellationToken ct = default, IReadOnlyDictionary<string,decimal>? inputs = null)
    {
        Fecha(date);
        var rules = Validar(snapshot.Parametros, snapshot.Variables, snapshot.CodigoTipoNomina, ct);
        var tables=RhNomTramos.Validar(snapshot.TablasTramos,snapshot.Variables,snapshot.CodigoTipoNomina,rules,ct);
        var result = new List<RhNomParametroResuelto>();
        foreach (var variable in snapshot.Variables.Where(v => v.Origen == "PARAMETRO").OrderBy(v => v.Codigo, StringComparer.Ordinal))
        {
            ct.ThrowIfCancellationRequested();
            var candidates=rules.Where(r=>r.CodigoVariable==variable.Codigo&&r.Desde<=date&&(r.Hasta is null||date<=r.Hasta.Value)).ToList();
            var candidatesTables=tables.Where(r=>r.CodigoVariable==variable.Codigo&&r.Desde<=date&&(r.Hasta is null||date<=r.Hasta.Value)).ToList();
            var hasSpecific=candidates.Any(r=>r.CodigoTipoNomina==snapshot.CodigoTipoNomina)||candidatesTables.Any(r=>r.CodigoTipoNomina==snapshot.CodigoTipoNomina);
            int? selectedType=hasSpecific?snapshot.CodigoTipoNomina:null;
            var eligible=candidates.Where(r=>r.CodigoTipoNomina==selectedType).ToList();
            var eligibleTables=candidatesTables.Where(r=>r.CodigoTipoNomina==selectedType).ToList();
            if(eligible.Count+eligibleTables.Count==0)throw new RhNomError("PARAMETRO_SIN_VIGENCIA",$"[{variable.Codigo}] no tiene valor vigente para la fecha solicitada.");
            if(eligible.Count+eligibleTables.Count!=1)throw new RhNomError("PARAMETRO_AMBIGUO",$"[{variable.Codigo}] tiene mas de un valor de igual prioridad.");
            if(eligibleTables.Count==1)
            {
                var chosen=eligibleTables[0];var match=RhNomTramos.Elegir(chosen,inputs,ct);
                result.Add(new(chosen.Clave,variable.Codigo,match.Row.Valor,variable.Unidad,hasSpecific?"TIPO_NOMINA":"GENERAL",chosen.Desde,chosen.Hasta,chosen.CodigoTipoNomina,match.Trace));
            }
            else
            {
                var chosen=eligible[0];
                result.Add(new(chosen.Clave,variable.Codigo,chosen.Valor,variable.Unidad,hasSpecific?"TIPO_NOMINA":"GENERAL",chosen.Desde,chosen.Hasta,chosen.CodigoTipoNomina));
            }
        }
        return result;
    }
    public static RhNomResultadoVigente Simular(RhNomModeloVersion saved, DateOnly date,
        IReadOnlyList<RhNomValorEntrada> entries, CancellationToken ct = default)
    {
        var typed = RhNomValores.Entradas(entries,RhNomValores.Catalogo(saved.Configuracion.Variables));
        var inputs=typed.Where(v=>v.Value.Tipo=="NUMERO").ToDictionary(v=>v.Key,v=>RhNomNumeros.Leer(v.Value.Valor),StringComparer.Ordinal);
        var expected = saved.Configuracion.Variables.Where(v => v.Origen == "ENTRADA").Select(v => v.Codigo).ToHashSet(StringComparer.Ordinal);
        if (!expected.SetEquals(typed.Keys))
            throw new RhNomError("ENTRADAS_INCOMPATIBLES", "Envie solo las ENTRADA de la revision. Los PARAMETRO se resuelven por vigencia y no pueden sustituirse en esta operacion.");
        var parameters = Resolver(saved.Configuracion, date, ct, inputs);
        var complete = entries.Concat(parameters.Select(p => new RhNomValorEntrada(p.CodigoVariable, p.Valor))).ToList();
        var response = RhNomConfiguracion.Simular(saved.Configuracion, complete, ct);
        // Do not expose a partial parameter resolution as an apparently successful run.
        var projection=response.Errores.Count==0&&saved.Configuracion.ReglasConcepto?.Count>0?RhNomReglasConcepto.Proyectar(saved.Configuracion,response,ct):null;
        return new(saved.ModeloId, saved.Version, saved.Hash, date, response.Errores.Count == 0 ? parameters : [], response,projection,response.Errores.Count==0&&saved.Configuracion.AccionesControl?.Count>0?RhNomAccionesControl.Proyectar(saved.Configuracion,entries):null);
    }
}
