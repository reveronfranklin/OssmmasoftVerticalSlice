namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public record RhNomCondicion(string Operador,string? Variable=null,string? Unidad=null,List<string>? Valores=null,
    List<RhNomCondicion>? Condiciones=null,string? VariableDerecha=null);
public record RhNomPasoCondicion(string Ruta,string Operador,string? Variable,string? ValorVariable,
    string? VariableDerecha,string? ValorDerecha,List<string> Valores,bool Resultado);
public record RhNomAplicabilidad(bool Resultado,string Rama,List<RhNomPasoCondicion> Pasos)
{
    public string VersionMetodo {get;init;}=RhNomCondiciones.Version;
}
public static class RhNomCondiciones
{
    public const string Version="CONDICIONES_TIPADAS_V2",VersionAnterior="CONDICIONES_NUMERICAS_V1";
    public const int MaxNodos=100,MaxProfundidad=8,MaxHijos=20,MaxValores=50;
    public static RhNomCondicion Normalizar(RhNomCondicion value,CancellationToken ct=default)
    {
        var count=0;
        RhNomCondicion Visit(RhNomCondicion node,int depth)
        {
            ct.ThrowIfCancellationRequested();
            if(node is null || ++count>MaxNodos || depth>MaxProfundidad)
                throw new RhNomError("LIMITE_CONDICION","Condicion nula o fuera del limite de 100 nodos/8 niveles.");
            if(node.Operador is "TODAS" or "ALGUNA" or "NO")
            {
                if(node.Variable is not null || node.Unidad is not null || node.Valores is not null || node.VariableDerecha is not null
                    || node.Condiciones is null || node.Condiciones.Count<1 || node.Condiciones.Count>MaxHijos || (node.Operador=="NO"&&node.Condiciones.Count!=1))
                    throw new RhNomError("CONDICION_INVALIDA","TODAS/ALGUNA requieren 1..20 hijos y NO exactamente uno, sin operandos de comparacion.");
                return new(node.Operador,Condiciones:node.Condiciones.Select(c=>Visit(c,depth+1)).ToList());
            }
            if(node.Operador is not ("IGUAL" or "DISTINTO" or "MAYOR" or "MAYOR_IGUAL" or "MENOR" or "MENOR_IGUAL" or "ENTRE" or "EN") || node.Condiciones is not null)
                throw new RhNomError("OPERADOR_CONDICION_INVALIDO","Operador de condicion no admitido o campos incompatibles.");
            var code=RhNomNumeros.Codigo(node.Variable);
            if(node.VariableDerecha is not null)
            {
                if(node.Operador is "ENTRE" or "EN" || node.Valores is not null || node.Unidad is not null)
                    throw new RhNomError("CONDICION_INVALIDA","Comparacion entre variables no admite valores/unidad literal, ENTRE o EN.");
                return new(node.Operador,code,VariableDerecha:RhNomNumeros.Codigo(node.VariableDerecha));
            }
            var type=RhNomValores.TipoUnidad(node.Unidad??"");if(type=="NUMERO")RhNomDimension.De(node.Unidad??"");RhNomValores.Operador(type,node.Operador);
            if(node.Valores is null || node.Valores.Count<1 || node.Valores.Count>MaxValores
                || (node.Operador=="ENTRE"&&node.Valores.Count!=2) || (node.Operador is not ("ENTRE" or "EN")&&node.Valores.Count!=1))
                throw new RhNomError("CONDICION_INVALIDA","Indique un valor, dos para ENTRE o 1..50 para EN.");
            var values=node.Valores.Select(v=>RhNomValores.Normalizar(type,v)).ToList();
            if(node.Operador=="ENTRE"&&RhNomValores.Comparar(type,values[0],values[1])>0)throw new RhNomError("RANGO_CONDICION_INVALIDO","Limite inferior mayor que el superior.");
            if(node.Operador=="EN"&&values.Distinct().Count()!=values.Count)throw new RhNomError("VALOR_CONDICION_DUPLICADO","EN no admite valores numericos repetidos.");
            return new(node.Operador,code,node.Unidad,values);
        }
        return Visit(value,1);
    }
    public static IEnumerable<string> Dependencias(RhNomCondicion value)
    {
        if(value.Variable is not null)yield return value.Variable;
        if(value.VariableDerecha is not null)yield return value.VariableDerecha;
        if(value.Condiciones is not null)foreach(var child in value.Condiciones)foreach(var code in Dependencias(child))yield return code;
    }
    public static void ValidarUnidades(RhNomCondicion value,IReadOnlyDictionary<string,RhNomVariableUnidad> catalog)
    {
        if(value.Condiciones is not null){foreach(var child in value.Condiciones)ValidarUnidades(child,catalog);return;}
        if(!catalog.TryGetValue(value.Variable!,out var left))throw new RhNomError("VARIABLE_CONDICION_INEXISTENTE","Variable de condicion no declarada.");
        RhNomValores.Operador(left.Tipo,value.Operador);
        if(value.VariableDerecha is not null)
        {
            if(!catalog.TryGetValue(value.VariableDerecha,out var right)||left.Tipo!=right.Tipo||(left.Tipo=="NUMERO"?RhNomDimension.De(left.Unidad)!=RhNomDimension.De(right.Unidad):left.Unidad!=right.Unidad))
                throw new RhNomError("UNIDADES_CONDICION_INCOMPATIBLES","Las variables comparadas requieren dimensiones compatibles.");
        }
        else if(left.Unidad!=value.Unidad)throw new RhNomError("UNIDADES_CONDICION_INCOMPATIBLES","La unidad del literal debe coincidir exactamente con la variable de condicion.");
    }
    public static RhNomAplicabilidad Evaluar(RhNomCondicion value,IReadOnlyDictionary<string,decimal> values,CancellationToken ct=default)=>
        Evaluar(value,values.ToDictionary(v=>v.Key,v=>new RhNomValorTipado("NUMERO","NUMERO",RhNomNumeros.Texto(v.Value)),StringComparer.Ordinal),ct);
    public static RhNomAplicabilidad Evaluar(RhNomCondicion value,IReadOnlyDictionary<string,RhNomValorTipado> values,CancellationToken ct=default)
    {
        var condition=Normalizar(value,ct);var steps=new List<RhNomPasoCondicion>();
        RhNomValorTipado Read(string code)=>values.TryGetValue(code,out var v)?v:throw new RhNomError("VARIABLE_CONDICION_SIN_VALOR",$"Falta [{code}] para evaluar la condicion.",variable:code);
        bool Visit(RhNomCondicion node,string path)
        {
            ct.ThrowIfCancellationRequested();bool result;string? left=null,right=null;var literals=node.Valores??[];
            if(node.Condiciones is not null)
            {
                var results=node.Condiciones.Select((child,index)=>Visit(child,path+"."+(index+1))).ToArray();
                result=node.Operador switch {"TODAS"=>results.All(x=>x),"ALGUNA"=>results.Any(x=>x),"NO"=>!results[0],_=>throw new InvalidOperationException()};
            }
            else
            {
                var a=Read(node.Variable!);left=RhNomValores.Normalizar(a.Tipo,a.Valor);RhNomValores.Operador(a.Tipo,node.Operador);
                if(node.VariableDerecha is null&&RhNomValores.TipoUnidad(node.Unidad!)!=a.Tipo)throw new RhNomError("TIPOS_CONDICION_INCOMPATIBLES","Literal y variable tienen tipos distintos.");
                var other=node.VariableDerecha is null?null:Read(node.VariableDerecha);
                if(other is not null&&other.Tipo!=a.Tipo)throw new RhNomError("TIPOS_CONDICION_INCOMPATIBLES","Variables de condicion tienen tipos distintos.");
                var b=other is null?literals[0]:RhNomValores.Normalizar(other.Tipo,other.Valor);if(other is not null)right=b;
                var compare=RhNomValores.Comparar(a.Tipo,left,b);
                result=node.Operador switch {"IGUAL"=>compare==0,"DISTINTO"=>compare!=0,"MAYOR"=>compare>0,"MAYOR_IGUAL"=>compare>=0,"MENOR"=>compare<0,"MENOR_IGUAL"=>compare<=0,"ENTRE"=>compare>=0&&RhNomValores.Comparar(a.Tipo,left,literals[1])<=0,"EN"=>literals.Any(v=>RhNomValores.Comparar(a.Tipo,left,v)==0),_=>throw new InvalidOperationException()};
            }
            steps.Add(new(path,node.Operador,node.Variable,left,node.VariableDerecha,right,literals,result));return result;
        }
        var applies=Visit(condition,"1");return new(applies,applies?"PRINCIPAL":"ALTERNATIVA",steps);
    }
}
