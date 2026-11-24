using Microsoft.AspNetCore.Mvc;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public readonly record struct RhNomDimension(int Dinero,int Dias,int Cantidad,int Porcentaje)
{
    public static RhNomDimension De(string unit)=>unit switch {
        "DINERO"=>new(1,0,0,0),"DIAS"=>new(0,1,0,0),"CANTIDAD"=>new(0,0,1,0),"PORCENTAJE"=>new(0,0,0,1),
        "NUMERO" or "FACTOR" or "TASA"=>default,_=>throw new RhNomError("UNIDAD_INVALIDA","Unidad no soportada en el analisis dimensional.")};
    public RhNomDimension Combinar(RhNomDimension other,int sign)=>new(Dinero+sign*other.Dinero,Dias+sign*other.Dias,Cantidad+sign*other.Cantidad,Porcentaje+sign*other.Porcentaje);
    public override string ToString()=>$"DINERO^{Dinero} DIAS^{Dias} CANTIDAD^{Cantidad} PORCENTAJE^{Porcentaje}";
}
public record RhNomVariableUnidad(string Codigo,string Unidad,string Origen,string Tipo="NUMERO");
public record RhNomUnidadesQuery(List<RhNomVariableUnidad> Variables,List<RhNomFormula> Formulas);
public record RhNomFormulaUnidad(string VariableSalida,string UnidadDeclarada,string DimensionInferida,string Normalizada);
public record RhNomInformeUnidades(string Estado,string VersionMetodo,List<RhNomFormulaUnidad> Formulas,List<RhNomDiagnostico> Errores);
public static class RhNomUnidades
{
    public const string Version="DIMENSIONES_V1";
    public static RhNomInformeUnidades Validar(IReadOnlyList<RhNomVariableUnidad>? variables,IReadOnlyList<RhNomFormula>? formulas,CancellationToken ct=default)
    {
        if(variables is null || variables.Count is <1 or >200)throw new RhNomError("VARIABLES_INVALIDAS","Indique 1..200 variables con unidades declaradas.");
        var catalog=new Dictionary<string,RhNomVariableUnidad>(StringComparer.Ordinal);var dimensions=new Dictionary<string,RhNomDimension>(StringComparer.Ordinal);
        foreach(var variable in variables)
        {
            ct.ThrowIfCancellationRequested();
            if(variable is null || variable.Origen is not ("ENTRADA" or "PARAMETRO" or "CALCULADA"))throw new RhNomError("VARIABLE_INVALIDA","Variable u origen invalido.");
            var code=RhNomNumeros.Codigo(variable.Codigo);
            if(!catalog.TryAdd(code,variable))throw new RhNomError("VARIABLE_DUPLICADA","Codigo de variable duplicado.");
            RhNomValores.ValidarTipo(variable.Tipo,variable.Unidad,variable.Origen);
            if(variable.Tipo=="NUMERO")dimensions.Add(code,RhNomDimension.De(variable.Unidad));
        }
        var plan=RhNomModelos.Planificar(formulas,catalog.Where(v=>v.Value.Origen!="CALCULADA").Select(v=>v.Key).ToArray(),ct);
        if(!catalog.Where(v=>v.Value.Origen=="CALCULADA").Select(v=>v.Key).ToHashSet(StringComparer.Ordinal).SetEquals(plan.Formulas.Keys))
            throw new RhNomError("SALIDAS_INVALIDAS","Cada CALCULADA requiere una formula y cada salida una variable CALCULADA.");
        var rows=new List<RhNomFormulaUnidad>();
        foreach(var code in plan.Orden)
        {
            ct.ThrowIfCancellationRequested();
            try{
                plan.Formulas[code].ValidarUnidad(dimensions,dimensions[code],ct);
                if(plan.Alternativas.TryGetValue(code,out var alternative))alternative.ValidarUnidad(dimensions,dimensions[code],ct);
                if(plan.Condiciones.TryGetValue(code,out var condition))RhNomCondiciones.ValidarUnidades(condition,catalog);
            }
            catch(RhNomError ex){throw new RhNomError(ex.Diagnostico.Codigo,$"[{code}]: {ex.Message}",ex.Diagnostico.Posicion,code);}
            rows.Add(new(code,catalog[code].Unidad,dimensions[code].ToString(),plan.Formulas[code].Normalizada));
        }
        return new("UNIDADES_DECLARADAS_COMPATIBLES",Version,rows,[]);
    }
}
public sealed partial class RhNomExpresion
{
    private record UnidadNodo(RhNomDimension Dimension,bool CeroLiteral=false);
    public bool UsaConversionUnidades=>Conversion(root);
    private static bool Conversion(Nodo node)=>node switch {
        Funcion f=>f.Nombre is "TASA" or "PORCENTAJE" || f.Args.Any(Conversion),
        Binario b=>Conversion(b.Izquierda)||Conversion(b.Derecha),Unario u=>Conversion(u.Hijo),_=>false};
    public void ValidarUnidad(IReadOnlyDictionary<string,RhNomDimension> units,RhNomDimension expected,CancellationToken ct=default)
    {
        var inferred=Inferir(root,units,ct);
        if(!inferred.CeroLiteral && inferred.Dimension!=expected)
            throw new RhNomError("UNIDAD_SALIDA_INCOMPATIBLE",$"Dimension inferida {inferred.Dimension}; salida declarada {expected}.");
    }
    private static UnidadNodo Compatibles(UnidadNodo a,UnidadNodo b)
    {
        if(a.CeroLiteral)return b;if(b.CeroLiteral)return a;
        if(a.Dimension!=b.Dimension)throw new RhNomError("UNIDADES_INCOMPATIBLES",$"No se pueden sumar/restar/comparar {a.Dimension} y {b.Dimension}.");
        return new(a.Dimension);
    }
    private static UnidadNodo Inferir(Nodo node,IReadOnlyDictionary<string,RhNomDimension> units,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        switch(node)
        {
            case Numero n:return new(default,n.Value==0);
            case Variable v:return units.TryGetValue(v.Nombre,out var d)?new(d):throw new RhNomError("UNIDAD_NO_DECLARADA",$"Falta unidad de [{v.Nombre}].");
            case Unario u:return Inferir(u.Hijo,units,ct);
            case Binario b:
                var left=Inferir(b.Izquierda,units,ct);var right=Inferir(b.Derecha,units,ct);
                return b.Operador switch {'+' or '-'=>Compatibles(left,right),'*'=>new(left.Dimension.Combinar(right.Dimension,1)),
                    '/'=>new(left.Dimension.Combinar(right.Dimension,-1)),_=>throw new InvalidOperationException()};
            case Funcion f:
                var args=f.Args.Select(a=>Inferir(a,units,ct)).ToList();
                if(f.Nombre=="SUMA_SALIDAS")
                {
                    if(args.Any(a=>a.Dimension!=RhNomDimension.De("DINERO")))throw new RhNomError("UNIDAD_SUMANDO_INVALIDA","SUMA_SALIDAS requiere sumandos DINERO.");
                    return new(RhNomDimension.De("DINERO"));
                }
                if(f.Nombre is "MIN" or "MAX")return new(Compatibles(args[0],args[1]).Dimension);
                if(f.Nombre is "ROUND" or "TRUNC")
                {
                    if(args[1].Dimension!=default)throw new RhNomError("UNIDAD_ESCALA_INVALIDA","La escala de redondeo debe ser adimensional.");
                    return new(args[0].Dimension);
                }
                if(f.Nombre=="TASA")
                {
                    if(args[0].Dimension!=RhNomDimension.De("PORCENTAJE"))throw new RhNomError("CONVERSION_UNIDAD_INVALIDA","TASA requiere PORCENTAJE y divide su valor entre 100.");
                    return new(default);
                }
                if(f.Nombre=="PORCENTAJE")
                {
                    if(args[0].Dimension!=default)throw new RhNomError("CONVERSION_UNIDAD_INVALIDA","PORCENTAJE requiere valor adimensional y lo multiplica por 100.");
                    return new(RhNomDimension.De("PORCENTAJE"));
                }
                return new(args[0].Dimension); // ABS preserves dimension, not the literal-zero exception.
            default:throw new InvalidOperationException();
        }
    }
}
public sealed partial class RhNominaController
{
    [HttpPost("unidadesValidate")]
    [RequestSizeLimit(524288)]
    public Task<IActionResult> ValidateUnits(RhNomUnidadesQuery value)=>Run("rh.nomina.formulas.validar",ct=>
    {
        try{return new ResultDto<RhNomInformeUnidades>(RhNomUnidades.Validar(value.Variables,value.Formulas,ct)){IsValid=true,Message="Unidades declaradas compatibles. No certifica datos ni reglas de negocio."};}
        catch(RhNomError ex){return new ResultDto<RhNomInformeUnidades>(new("INVALIDO",RhNomUnidades.Version,[],[ex.Diagnostico])){IsValid=false,Message=ex.Message};}
    });
}
