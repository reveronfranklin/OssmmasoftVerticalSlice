using System.Text.Json.Serialization;
namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public record RhNomValorEntrada(string Codigo, string? Valor);
public record RhNomFormula(string VariableSalida, string Expresion,
    [property: JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] RhNomCondicion? Condicion=null,
    [property: JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] string? ExpresionAlternativa=null);
public record RhNomResultadoFormula(string VariableSalida, string Normalizada, string Sustituida,
    string Resultado, List<string> Dependencias, List<RhNomPaso> Pasos,
    [property: JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] RhNomAplicabilidad? Aplicabilidad=null);
public record RhNomResultadoValidacion(string Estado, string? Normalizada, List<string> Dependencias,
    RhNomResultadoFormula? Evaluacion, List<RhNomDiagnostico> Errores);
public record RhNomResultadoSimulacion(string Estado, List<string> OrdenCalculo,
    List<RhNomResultadoFormula> Resultados, List<RhNomDiagnostico> Errores);

public static class RhNomModelos
{
    public const int MaxFormulas = 100;
    public const int MaxEntradas = 200;
    public const int MaxPasos = 10000;
    public static Dictionary<string, decimal> Entradas(IReadOnlyList<RhNomValorEntrada>? entries)
    {
        if (entries is null || entries.Count > MaxEntradas)
            throw new RhNomError("ENTRADAS_INVALIDAS", $"Envie una lista de hasta {MaxEntradas} entradas numericas.");
        var result = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            if (entry is null) throw new RhNomError("ENTRADA_INVALIDA", "Entrada nula.");
            var code = RhNomNumeros.Codigo(entry.Codigo);
            if (!result.TryAdd(code, RhNomNumeros.Leer(entry.Valor)))
                throw new RhNomError("VARIABLE_DUPLICADA", $"La variable [{code}] esta duplicada.", variable: code);
        }
        return result;
    }
    public static RhNomResultadoFormula Evaluar(string name, RhNomExpresion formula, IReadOnlyDictionary<string, decimal> inputs, CancellationToken ct)
    {
        var value = formula.Evaluar(inputs, ct);
        return new(name, formula.Normalizada, value.Sustituida, RhNomNumeros.Texto(value.Resultado), formula.Dependencias.ToList(), value.Pasos);
    }
    public record Plan(IReadOnlyDictionary<string, RhNomExpresion> Formulas, List<string> Orden,
        IReadOnlyDictionary<string,RhNomExpresion> Alternativas,IReadOnlyDictionary<string,RhNomCondicion> Condiciones);
    public static Plan Planificar(IReadOnlyList<RhNomFormula>? definitions, IReadOnlyCollection<string> knownInputs, CancellationToken ct = default)
    {
        if (definitions is null || definitions.Count is < 1 or > MaxFormulas)
            throw new RhNomError("MODELO_INVALIDO", $"El modelo debe contener entre 1 y {MaxFormulas} formulas.");
        var inputs = knownInputs.ToHashSet(StringComparer.Ordinal);
        var formulas = new Dictionary<string, RhNomExpresion>(StringComparer.Ordinal);
        var alternatives=new Dictionary<string,RhNomExpresion>(StringComparer.Ordinal);var conditions=new Dictionary<string,RhNomCondicion>(StringComparer.Ordinal);
        foreach (var definition in definitions)
        {
            ct.ThrowIfCancellationRequested();
            if (definition is null) throw new RhNomError("FORMULA_INVALIDA", "Formula nula.");
            var name = RhNomNumeros.Codigo(definition.VariableSalida);
            if (inputs.Contains(name) || formulas.ContainsKey(name))
                throw new RhNomError("SALIDA_DUPLICADA", $"[{name}] debe tener un unico productor y no ser entrada a la vez.", variable: name);
            try {
                formulas.Add(name, RhNomExpresion.Compilar(definition.Expresion));
                if(definition.Condicion is null && definition.ExpresionAlternativa is not null)throw new RhNomError("ALTERNATIVA_SIN_CONDICION","La formula alternativa requiere condicion.");
                if(definition.Condicion is not null)
                {
                    conditions.Add(name,RhNomCondiciones.Normalizar(definition.Condicion,ct));
                    if(string.IsNullOrWhiteSpace(definition.ExpresionAlternativa))throw new RhNomError("ALTERNATIVA_REQUERIDA","Indique una formula alternativa explicita; no se usa cero por defecto.");
                    alternatives.Add(name,RhNomExpresion.Compilar(definition.ExpresionAlternativa));
                }
            }
            catch (RhNomError ex) { throw new RhNomError(ex.Diagnostico.Codigo, ex.Message, ex.Diagnostico.Posicion, name); }
        }
        foreach(var expression in formulas.Values.Concat(alternatives.Values))
            foreach(var source in expression.SalidasSumadas)
                if(!formulas.ContainsKey(source))throw new RhNomError("SUMANDO_NO_CALCULADO","SUMA_SALIDAS no admite entradas ni parametros; cada sumando requiere formula en esta corrida.",variable:source);
        var order = new List<string>();
        var visiting = new List<string>();
        var done = new HashSet<string>(StringComparer.Ordinal);
        void Visit(string name)
        {
            ct.ThrowIfCancellationRequested();
            if (done.Contains(name)) return;
            var cycle = visiting.IndexOf(name);
            if (cycle >= 0)
                throw new RhNomError("DEPENDENCIA_CIRCULAR", "Dependencia circular: " + string.Join(" -> ", visiting.Skip(cycle).Append(name)), variable: name);
            visiting.Add(name);
            foreach (var dependency in formulas[name].Dependencias.Concat(alternatives.TryGetValue(name,out var alternate)?alternate.Dependencias:[]).Concat(conditions.TryGetValue(name,out var condition)?RhNomCondiciones.Dependencias(condition):[]).Distinct(StringComparer.Ordinal))
            {
                if (formulas.ContainsKey(dependency)) Visit(dependency);
                else if (!inputs.Contains(dependency))
                    throw new RhNomError("VARIABLE_SIN_VALOR", $"[{name}] requiere [{dependency}], que no tiene entrada ni formula.", variable: dependency);
            }
            visiting.RemoveAt(visiting.Count - 1);
            done.Add(name); order.Add(name);
        }
        foreach (var name in formulas.Keys.Order(StringComparer.Ordinal)) Visit(name);
        return new(formulas, order,alternatives,conditions);
    }
    public static RhNomResultadoSimulacion Simular(IReadOnlyList<RhNomFormula>? definitions,
        IReadOnlyList<RhNomValorEntrada>? entries, CancellationToken ct = default,IReadOnlyList<RhNomVariableUnidad>? catalog=null)
    {
        try
        {
            if (definitions is null || definitions.Count is < 1 or > MaxFormulas)
                throw new RhNomError("MODELO_INVALIDO", $"El modelo debe contener entre 1 y {MaxFormulas} formulas.");
            var typed=catalog is null?Entradas(entries).ToDictionary(v=>v.Key,v=>new RhNomValorTipado("NUMERO","NUMERO",RhNomNumeros.Texto(v.Value)),StringComparer.Ordinal):RhNomValores.Entradas(entries,catalog);
            var inputs=typed.Where(v=>v.Value.Tipo=="NUMERO").ToDictionary(v=>v.Key,v=>RhNomNumeros.Leer(v.Value.Valor),StringComparer.Ordinal);
            var plan = Planificar(definitions, typed.Keys.ToList(), ct);
            var formulas = plan.Formulas; var order = plan.Orden;
            if(formulas.Values.Concat(plan.Alternativas.Values).Any(f=>f.SalidasSumadas.Count>0))
            {
                if(catalog is null)throw new RhNomError("UNIDADES_SALIDAS_REQUERIDAS","SUMA_SALIDAS requiere catalogo con unidades y origen.");
                RhNomUnidades.Validar(catalog,definitions,ct);
            }
            var generadas=new HashSet<string>(StringComparer.Ordinal);
            var results = new List<RhNomResultadoFormula>();
            var steps = 0;
            foreach (var name in order)
            {
                ct.ThrowIfCancellationRequested();
                RhNomResultadoFormula result;
                try {
                    var selected=formulas[name];RhNomAplicabilidad? applicability=null;
                    if(plan.Condiciones.TryGetValue(name,out var condition))
                    {
                        applicability=RhNomCondiciones.Evaluar(condition,typed,ct);
                        if(!applicability.Resultado)selected=plan.Alternativas[name];
                    }
                    var evaluation=selected.EvaluarGeneradas(inputs,ct,generadas);
                    result=new(name,selected.Normalizada,evaluation.Sustituida,RhNomNumeros.Texto(evaluation.Resultado),selected.Dependencias.ToList(),evaluation.Pasos,applicability);
                }
                catch (RhNomError ex) { throw new RhNomError(ex.Diagnostico.Codigo, ex.Message, ex.Diagnostico.Posicion, name); }
                steps += result.Pasos.Count+(result.Aplicabilidad?.Pasos.Count??0);
                if (steps > MaxPasos) throw new RhNomError("LIMITE_MODELO", "El modelo supera el limite de pasos de calculo.");
                inputs.Add(name, decimal.Parse(result.Resultado, System.Globalization.CultureInfo.InvariantCulture));typed.Add(name,new("NUMERO",catalog?.Single(v=>v.Codigo==name).Unidad??"NUMERO",result.Resultado)); results.Add(result);generadas.Add(name);
            }
            return new("SIMULACION_EXPRESIONES_COMPLETA", order, results, []);
        }
        catch (RhNomError ex) { return new("INVALIDO", [], [], [ex.Diagnostico]); }
        catch (OverflowException) { return new("INVALIDO", [], [], [new("DESBORDAMIENTO", "El modelo excede el rango decimal admitido.")]); }
    }
}
