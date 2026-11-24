using System.Globalization;
using System.Text.RegularExpressions;

namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public record RhNomDiagnostico(string Codigo, string Mensaje, int? Posicion = null, string? Variable = null);
public record RhNomPaso(int Orden, string Operacion, List<string> Operandos, string Resultado);
public sealed class RhNomError(string code, string message, int? position = null, string? variable = null) : Exception(message)
{
    public RhNomDiagnostico Diagnostico { get; } = new(code, message, position, variable);
}

// Pure calculation: no Oracle, clocks, globals, code generation, SQL, eval or reflection.
public static class RhNomNumeros
{
    public const int MaxDigitos = 28;
    public static string Texto(decimal value) => value.ToString("0.############################", CultureInfo.InvariantCulture);
    public static decimal Leer(string? text)
    {
        if (text is null || text.Length is < 1 or > 100 || !Regex.IsMatch(text, @"\A[+-]?[0-9]+(?:\.[0-9]+)?\z", RegexOptions.CultureInvariant))
            throw new RhNomError("NUMERO_INVALIDO", "Use numero decimal con punto, sin separadores de miles ni exponente.");
        var parts = text.TrimStart('+', '-').Split('.');
        var fraction = parts.Length == 2 ? parts[1] : "";
        var significant = (parts[0] + fraction).TrimStart('0').TrimEnd('0').Length;
        if (fraction.Length > 28 || significant > MaxDigitos
            || !decimal.TryParse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value))
            throw new RhNomError("PRECISION_NO_SOPORTADA", "El prototipo admite hasta 28 cifras significativas y escala 28; no redondea entradas fuera de rango.");
        return value;
    }
    public static decimal Redondear(decimal value, int scale, bool truncar)
    {
        if (scale is < -28 or > 28) throw new RhNomError("ESCALA_INVALIDA", "La escala de ROUND/TRUNC debe ser un entero entre -28 y 28.");
        if (scale >= 0) return decimal.Round(value, scale, truncar ? MidpointRounding.ToZero : MidpointRounding.AwayFromZero);
        decimal factor = 1;
        for (var i = 0; i < -scale; i++) factor *= 10;
        return checked(decimal.Round(value / factor, 0, truncar ? MidpointRounding.ToZero : MidpointRounding.AwayFromZero) * factor);
    }
    public static string Codigo(string? code)
    {
        if (code is null || !Regex.IsMatch(code, @"\A[A-Za-z][A-Za-z0-9_]{0,59}\z", RegexOptions.CultureInvariant))
            throw new RhNomError("VARIABLE_INVALIDA", "Codigo de variable: letra inicial, letras/numeros/guion bajo, maximo 60 caracteres.");
        return code.ToUpperInvariant();
    }
}

public sealed partial class RhNomExpresion
{
    public const int MaxLongitud = 4000;
    public const int MaxNodos = 500;
    public const int MaxProfundidad = 64;
    private readonly Nodo root;
    public IReadOnlyList<string> Dependencias { get; }
    public string Normalizada { get; }
    public IReadOnlyList<string> SalidasSumadas { get; }
    private RhNomExpresion(Nodo root)
    {
        this.root = root;
        Dependencias = root.Variables().Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
        Normalizada = root.Mostrar(null);
        SalidasSumadas=Sumas(root).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    }
    private static IEnumerable<string> Sumas(Nodo node)=>node switch {
        Funcion f=>f.Nombre=="SUMA_SALIDAS"?f.Args.Cast<Variable>().Select(v=>v.Nombre):f.Args.SelectMany(Sumas),
        Binario b=>Sumas(b.Izquierda).Concat(Sumas(b.Derecha)),Unario u=>Sumas(u.Hijo),_=>[]};
    public static RhNomExpresion Compilar(string? expression) => new(new Parser(expression).Leer());
    public (decimal Resultado, string Sustituida, List<RhNomPaso> Pasos) Evaluar(IReadOnlyDictionary<string, decimal> values, CancellationToken ct = default)
 => EvaluarInterno(values,ct,null);
    internal (decimal Resultado,string Sustituida,List<RhNomPaso> Pasos) EvaluarGeneradas(IReadOnlyDictionary<string,decimal> values,CancellationToken ct,IReadOnlySet<string> generadas)=>EvaluarInterno(values,ct,generadas);
    private (decimal Resultado,string Sustituida,List<RhNomPaso> Pasos) EvaluarInterno(IReadOnlyDictionary<string,decimal> values,CancellationToken ct,IReadOnlySet<string>? generadas)
    {
        foreach (var name in Dependencias)
            if (!values.ContainsKey(name)) throw new RhNomError("VARIABLE_SIN_VALOR", $"Falta el valor de [{name}].", variable: name);
        if(SalidasSumadas.Any(v=>generadas?.Contains(v)!=true))throw new RhNomError("SALIDA_NO_GENERADA","SUMA_SALIDAS requiere resultados ya generados por el modelo en esta corrida.");
        var context = new Contexto(values, ct);
        try { return (root.Evaluar(context), root.Mostrar(values), context.Pasos); }
        catch (OverflowException) { throw new RhNomError("DESBORDAMIENTO", "El resultado excede el rango decimal admitido."); }
    }
    private sealed class Contexto(IReadOnlyDictionary<string, decimal> values, CancellationToken ct)
    {
        public IReadOnlyDictionary<string, decimal> Valores => values;
        public List<RhNomPaso> Pasos { get; } = [];
        public void Comprobar() => ct.ThrowIfCancellationRequested();
        public decimal Paso(string operation, decimal[] args, decimal result)
        {
            Comprobar();
            Pasos.Add(new(Pasos.Count + 1, operation, args.Select(RhNomNumeros.Texto).ToList(), RhNomNumeros.Texto(result)));
            return result;
        }
    }
    private abstract record Nodo(int Profundidad)
    {
        public abstract decimal Evaluar(Contexto c);
        public abstract string Mostrar(IReadOnlyDictionary<string, decimal>? values);
        public abstract IEnumerable<string> Variables();
    }
    private sealed record Numero(decimal Value) : Nodo(1)
    {
        public override decimal Evaluar(Contexto c) { c.Comprobar(); return Value; }
        public override string Mostrar(IReadOnlyDictionary<string, decimal>? values) => RhNomNumeros.Texto(Value);
        public override IEnumerable<string> Variables() => [];
    }
    private sealed record Variable(string Nombre) : Nodo(1)
    {
        public override decimal Evaluar(Contexto c) { c.Comprobar(); return c.Valores[Nombre]; }
        public override string Mostrar(IReadOnlyDictionary<string, decimal>? values) => values is null ? $"[{Nombre}]" : "(" + RhNomNumeros.Texto(values[Nombre]) + ")";
        public override IEnumerable<string> Variables() => [Nombre];
    }
    private sealed record Unario(char Signo, Nodo Hijo) : Nodo(Hijo.Profundidad + 1)
    {
        public override decimal Evaluar(Contexto c)
        {
            var value = Hijo.Evaluar(c);
            return c.Paso(Signo == '-' ? "NEGAR" : "POSITIVO", [value], Signo == '-' ? checked(-value) : value);
        }
        public override string Mostrar(IReadOnlyDictionary<string, decimal>? values) => "(" + Signo + Hijo.Mostrar(values) + ")";
        public override IEnumerable<string> Variables() => Hijo.Variables();
    }
    private sealed record Binario(char Operador, Nodo Izquierda, Nodo Derecha) : Nodo(Math.Max(Izquierda.Profundidad, Derecha.Profundidad) + 1)
    {
        public override decimal Evaluar(Contexto c)
        {
            var a = Izquierda.Evaluar(c); var b = Derecha.Evaluar(c);
            var result = Operador switch
            {
                '+' => checked(a + b), '-' => checked(a - b), '*' => checked(a * b),
                '/' when b == 0 => throw new RhNomError("DIVISION_POR_CERO", "No se puede dividir entre cero."),
                '/' => a / b, _ => throw new InvalidOperationException()
            };
            if (Operador is '*' or '/' && a != 0 && b != 0 && result == 0)
                throw new RhNomError("PRECISION_NO_SOPORTADA", "La operacion pierde un valor no nulo por quedar fuera de la escala decimal.");
            return c.Paso(Operador.ToString(), [a, b], result);
        }
        public override string Mostrar(IReadOnlyDictionary<string, decimal>? values) => "(" + Izquierda.Mostrar(values) + " " + Operador + " " + Derecha.Mostrar(values) + ")";
        public override IEnumerable<string> Variables() => Izquierda.Variables().Concat(Derecha.Variables());
    }
    private sealed record Funcion(string Nombre, List<Nodo> Args) : Nodo(Args.Max(a => a.Profundidad) + 1)
    {
        public override decimal Evaluar(Contexto c)
        {
            var values = Args.Select(a => a.Evaluar(c)).ToArray();
            decimal result;
            if (Nombre is "ROUND" or "TRUNC")
            {
                if (values[1] != decimal.Truncate(values[1]) || values[1] is < -28 or > 28)
                    throw new RhNomError("ESCALA_INVALIDA", "La escala de ROUND/TRUNC debe ser un entero entre -28 y 28.");
                result = RhNomNumeros.Redondear(values[0], (int)values[1], Nombre == "TRUNC");
            }
            else if (Nombre is "TASA" or "PORCENTAJE")
            {
                result=Nombre=="TASA" ? values[0]/100m : checked(values[0]*100m);
                if(values[0]!=0 && result==0)throw new RhNomError("PRECISION_NO_SOPORTADA","La conversion de porcentaje pierde un valor no nulo.");
            }
            else if(Nombre=="SUMA_SALIDAS")result=values.Aggregate(0m,RhNomAcumuladores.SumarExacto);
            else result = Nombre switch { "MIN" => Math.Min(values[0], values[1]), "MAX" => Math.Max(values[0], values[1]), "ABS" => Math.Abs(values[0]), _ => throw new InvalidOperationException() };
            return c.Paso(Nombre, values, result);
        }
        public override string Mostrar(IReadOnlyDictionary<string, decimal>? values) => Nombre + "(" + string.Join(", ", Args.Select(a => a.Mostrar(values))) + ")";
        public override IEnumerable<string> Variables() => Args.SelectMany(a => a.Variables());
    }
    private sealed class Parser
    {
        private readonly string text;
        private int position, count, nesting;
        public Parser(string? expression)
        {
            if (string.IsNullOrWhiteSpace(expression) || expression.Length > MaxLongitud)
                throw new RhNomError("LONGITUD_INVALIDA", $"Expresion requerida, maximo {MaxLongitud} caracteres.");
            if (expression.Contains("--", StringComparison.Ordinal) || expression.Contains("/*", StringComparison.Ordinal)
                || expression.Contains("*/", StringComparison.Ordinal))
                throw new RhNomError("SINTAXIS_INVALIDA", "No se permiten comentarios ni codigo SQL.");
            text = expression;
        }
        private RhNomError Error(string message) => new("SINTAXIS_INVALIDA", message, position + 1);
        private void Space() { while (position < text.Length && char.IsWhiteSpace(text[position])) position++; }
        private bool Match(char value)
        {
            Space(); if (position == text.Length || text[position] != value) return false;
            position++; return true;
        }
        private Nodo Add(Nodo node)
        {
            if (++count > MaxNodos || node.Profundidad > MaxProfundidad)
                throw new RhNomError("LIMITE_EXPRESION", "La expresion excede los limites de nodos o profundidad.", position + 1);
            return node;
        }
        public Nodo Leer()
        {
            var node = Sum(); Space();
            if (position != text.Length) throw Error("Caracter u operacion no permitida.");
            return node;
        }
        private Nodo Sum()
        {
            var node = Product();
            while (true)
            {
                if (Match('+')) node = Add(new Binario('+', node, Product()));
                else if (Match('-')) node = Add(new Binario('-', node, Product()));
                else return node;
            }
        }
        private Nodo Product()
        {
            var node = Unary();
            while (true)
            {
                if (Match('*')) node = Add(new Binario('*', node, Unary()));
                else if (Match('/')) node = Add(new Binario('/', node, Unary()));
                else return node;
            }
        }
        private Nodo Unary()
        {
            if (++nesting > MaxProfundidad) throw Error("Demasiados niveles de expresion.");
            try
            {
                if (Match('+')) return Add(new Unario('+', Unary()));
                if (Match('-')) return Add(new Unario('-', Unary()));
                return Atom();
            }
            finally { nesting--; }
        }
        private Nodo Atom()
        {
            Space();
            if (Match('(')) { var inside = Sum(); if (!Match(')')) throw Error("Falta cerrar parentesis."); return inside; }
            if (Match('['))
            {
                var start = position;
                while (position < text.Length && text[position] != ']') position++;
                if (position == text.Length) throw Error("Falta cerrar variable con ].");
                var name = RhNomNumeros.Codigo(text[start..position]); position++;
                return Add(new Variable(name));
            }
            if (position < text.Length && char.IsAsciiDigit(text[position]))
            {
                var start = position;
                while (position < text.Length && (char.IsAsciiDigit(text[position]) || text[position] == '.')) position++;
                return Add(new Numero(RhNomNumeros.Leer(text[start..position])));
            }
            if (position < text.Length && char.IsAsciiLetter(text[position]))
            {
                var start = position;
                while (position < text.Length && (char.IsAsciiLetter(text[position])||text[position]=='_')) position++;
                var name = text[start..position].ToUpperInvariant();
                if (name is not ("MIN" or "MAX" or "ABS" or "ROUND" or "TRUNC" or "TASA" or "PORCENTAJE" or "SUMA_SALIDAS")) throw Error("Funcion no permitida.");
                if (!Match('(')) throw Error("Falta abrir parentesis de funcion.");
                var args = new List<Nodo> { Sum() };
                while (Match(','))
                {
                    if (args.Count >= (name=="SUMA_SALIDAS"?RhNomSalidasCorrida.MaxSumandos:2)) throw Error("Demasiados argumentos.");
                    args.Add(Sum());
                }
                if (!Match(')')) throw Error("Falta cerrar funcion.");
                if (name=="SUMA_SALIDAS"&&(args.Any(a=>a is not Variable)||args.Cast<Variable>().Select(v=>v.Nombre).Distinct(StringComparer.Ordinal).Count()!=args.Count))throw Error("SUMA_SALIDAS admite referencias distintas a variables CALCULADA/DINERO, sin literales ni expresiones.");
                if (name!="SUMA_SALIDAS"&&args.Count != (name is "ABS" or "TASA" or "PORCENTAJE" ? 1 : 2)) throw Error("Cantidad incorrecta de argumentos.");
                return Add(new Funcion(name, args));
            }
            throw Error("Se esperaba numero, variable, funcion o parentesis.");
        }
    }
}
