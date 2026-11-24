using System.Globalization;
namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public record RhNomValorTipado(string Tipo,string Unidad,string Valor);
public static class RhNomValores
{
    public const string Version="VALORES_TIPADOS_V1";
    public static string TipoUnidad(string unit)=>unit is "FECHA" or "BOOLEANO" or "TEXTO"?unit:"NUMERO";
    public static void ValidarTipo(string type,string unit,string origin)
    {
        if(type=="NUMERO")RhNomDimension.De(unit);
        else if(type is not ("FECHA" or "BOOLEANO" or "TEXTO")||unit!=type||origin=="CALCULADA")throw new RhNomError("TIPO_NO_SOPORTADO","FECHA/BOOLEANO/TEXTO requieren unidad del mismo nombre y origen ENTRADA/PARAMETRO; CALCULADA solo NUMERO.");
    }
    public static string Normalizar(string type,string? value)
    {
        if(type=="NUMERO")return RhNomNumeros.Texto(RhNomNumeros.Leer(value));
        if(value is null)throw new RhNomError("VALOR_TIPADO_NULO","Valor tipado requerido; no se usa default implicito.");
        if(type=="FECHA")
        {
            if(!DateOnly.TryParseExact(value,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var date)||date.Year<1900)throw new RhNomError("FECHA_TIPADA_INVALIDA","Fecha de calendario desde 1900, exactamente yyyy-MM-dd, sin hora/zona.");
            return date.ToString("yyyy-MM-dd",CultureInfo.InvariantCulture);
        }
        if(type=="BOOLEANO")return value is "true" or "false"?value:throw new RhNomError("BOOLEANO_INVALIDO","Booleano literal true/false en minusculas; no se convierte 0/1 ni texto libre.");
        if(type=="TEXTO")
        {
            if(value.Length is <1 or >100||value!=value.Trim()||value.Any(c=>!char.IsLetterOrDigit(c)&&!" _./-".Contains(c)))throw new RhNomError("TEXTO_RESTRINGIDO_INVALIDO","Texto de 1..100 letras/numeros, espacios internos o _ . / -, sin espacios extremos; comparacion exacta ordinal.");
            return value;
        }
        throw new RhNomError("TIPO_NO_SOPORTADO","Tipo de valor no admitido.");
    }
    public static int Comparar(string type,string left,string right)=>type=="NUMERO"?RhNomNumeros.Leer(left).CompareTo(RhNomNumeros.Leer(right)):StringComparer.Ordinal.Compare(Normalizar(type,left),Normalizar(type,right));
    public static void Operador(string type,string op)
    {
        if(type is "BOOLEANO" or "TEXTO"&&op is not ("IGUAL" or "DISTINTO" or "EN"))throw new RhNomError("OPERADOR_TIPO_INCOMPATIBLE","BOOLEANO/TEXTO solo permiten IGUAL, DISTINTO y EN.");
    }
    public static List<RhNomValorEntrada> Estructura(IReadOnlyList<RhNomValorEntrada>? entries)
    {
        if(entries is null||entries.Count>RhNomModelos.MaxEntradas)throw new RhNomError("ENTRADAS_INVALIDAS","Hasta 200 entradas requeridas.");
        var codes=new HashSet<string>(StringComparer.Ordinal);var result=new List<RhNomValorEntrada>();
        foreach(var entry in entries)
        {
            if(entry is null||entry.Valor is null||entry.Valor.Length is <1 or >100)throw new RhNomError("ENTRADA_INVALIDA","Entrada no nula con valor de 1..100 caracteres requerido.");
            var code=RhNomNumeros.Codigo(entry.Codigo);if(!codes.Add(code))throw new RhNomError("VARIABLE_DUPLICADA","Entrada duplicada.");result.Add(new(code,entry.Valor));
        }
        return result;
    }
    public static Dictionary<string,RhNomValorTipado> Entradas(IReadOnlyList<RhNomValorEntrada>? entries,IReadOnlyList<RhNomVariableUnidad> catalog)
    {
        var vars=catalog.ToDictionary(v=>v.Codigo,StringComparer.Ordinal);var result=new Dictionary<string,RhNomValorTipado>(StringComparer.Ordinal);
        foreach(var entry in Estructura(entries))
        {
            if(!vars.TryGetValue(entry.Codigo,out var variable)||variable.Origen=="CALCULADA")throw new RhNomError("ENTRADA_NO_DECLARADA","Entrada no declarada o CALCULADA.");
            ValidarTipo(variable.Tipo,variable.Unidad,variable.Origen);result.Add(entry.Codigo,new(variable.Tipo,variable.Unidad,Normalizar(variable.Tipo,entry.Valor)));
        }
        return result;
    }
    public static List<RhNomVariableUnidad> Catalogo(IReadOnlyList<RhNomVariable> vars)=>vars.Select(v=>new RhNomVariableUnidad(v.Codigo,v.Unidad,v.Origen,v.Tipo)).ToList();
}
