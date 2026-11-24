using System.Text.Json.Serialization;
namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public record RhNomTramo(string Clave,string? Desde,string? Hasta,string Valor);
public record RhNomTablaTramos(string Clave,string CodigoVariable,string CodigoEntrada,string UnidadEntrada,
    DateOnly Desde,DateOnly? Hasta,int? CodigoTipoNomina,List<RhNomTramo> Tramos);
public record RhNomTramoElegido(string Clave,string CodigoEntrada,string UnidadEntrada,string ValorEntrada,
    string? Desde,string? Hasta)
{
    public string VersionMetodo=>RhNomTramos.Version;
    public bool DesdeIncluido=>true;
    public bool HastaIncluido=>false;
}

// Fixed-value lookup, never progressive accumulation or implicit interpolation.
public static class RhNomTramos
{
    public const string Version="TRAMOS_NUMERICOS_V1";
    public const int MaxTablas=100,MaxTramosTabla=50,MaxTramosTotal=500;
    public static List<RhNomTablaTramos> Validar(IReadOnlyList<RhNomTablaTramos>? tables,
        IReadOnlyList<RhNomVariable> variables,int payrollType,IReadOnlyList<RhNomParametro> parameters,CancellationToken ct=default)
    {
        if(tables is null)return [];
        if(tables.Count>MaxTablas)throw new RhNomError("LIMITE_TABLAS_TRAMOS","Se admiten hasta 100 tablas de tramos por revision.");
        var catalog=variables.ToDictionary(v=>v.Codigo,StringComparer.Ordinal);var keys=parameters.Select(p=>p.Clave).ToHashSet(StringComparer.Ordinal);
        var result=new List<RhNomTablaTramos>();var total=0;
        foreach(var table in tables)
        {
            ct.ThrowIfCancellationRequested();
            if(table is null)throw new RhNomError("TABLA_TRAMOS_INVALIDA","Tabla de tramos nula.");
            var key=RhNomNumeros.Codigo(table.Clave);var output=RhNomNumeros.Codigo(table.CodigoVariable);var input=RhNomNumeros.Codigo(table.CodigoEntrada);
            if(!keys.Add(key))throw new RhNomError("CLAVE_PARAMETRO_DUPLICADA","Las reglas simples y tablas requieren claves unicas.");
            if(!catalog.TryGetValue(output,out var parameter)||parameter.Origen!="PARAMETRO"||parameter.Tipo!="NUMERO")throw new RhNomError("PARAMETRO_NO_DECLARADO","La salida de una tabla debe ser PARAMETRO.");
            if(!catalog.TryGetValue(input,out var axis)||axis.Origen!="ENTRADA"||axis.Tipo!="NUMERO"||axis.Unidad!=table.UnidadEntrada)
                throw new RhNomError("EJE_TRAMOS_INVALIDO","El eje debe ser una ENTRADA numerica con unidad literal coincidente.");
            RhNomParametros.Fecha(table.Desde);
            if(table.Hasta is {} end){RhNomParametros.Fecha(end);if(end<table.Desde)throw new RhNomError("VIGENCIA_INVALIDA","Hasta no puede ser anterior a Desde.");}
            if(table.CodigoTipoNomina is {} type&&(type<=0||type!=payrollType))throw new RhNomError("AMBITO_PARAMETRO_INVALIDO","El tipo debe coincidir con el modelo.");
            if(table.Tramos is null||table.Tramos.Count is <1 or >MaxTramosTabla|| (total+=table.Tramos.Count)>MaxTramosTotal)
                throw new RhNomError("LIMITE_TRAMOS","Indique 1..50 tramos por tabla, hasta 500 en la revision.");
            var rows=new List<RhNomTramo>();var rowKeys=new HashSet<string>(StringComparer.Ordinal);
            foreach(var row in table.Tramos)
            {
                ct.ThrowIfCancellationRequested();
                if(row is null)throw new RhNomError("TRAMO_INVALIDO","Tramo nulo.");
                var rowKey=RhNomNumeros.Codigo(row.Clave);
                if(!rowKeys.Add(rowKey))throw new RhNomError("CLAVE_TRAMO_DUPLICADA","Clave de tramo repetida en la tabla.");
                var low=row.Desde is null?(decimal?)null:RhNomNumeros.Leer(row.Desde);
                var high=row.Hasta is null?(decimal?)null:RhNomNumeros.Leer(row.Hasta);
                if(low is {} l&&high is {} h&&l>=h)throw new RhNomError("RANGO_TRAMO_INVALIDO","Desde debe ser menor que Hasta; limite superior excluido.");
                rows.Add(new(rowKey,low is {} lo?RhNomNumeros.Texto(lo):null,high is {} hi?RhNomNumeros.Texto(hi):null,RhNomNumeros.Texto(RhNomNumeros.Leer(row.Valor))));
            }
            rows=rows.OrderBy(r=>r.Desde is null?(decimal?)null:RhNomNumeros.Leer(r.Desde)).ThenBy(r=>r.Clave,StringComparer.Ordinal).ToList();
            for(var i=1;i<rows.Count;i++)
                if(rows[i-1].Hasta is null||rows[i].Desde is null||RhNomNumeros.Leer(rows[i].Desde)<RhNomNumeros.Leer(rows[i-1].Hasta))
                    throw new RhNomError("TRAMOS_SUPERPUESTOS","Los rangos de una tabla no pueden superponerse.");
            result.Add(table with{Clave=key,CodigoVariable=output,CodigoEntrada=input,Tramos=rows});
        }
        // One eligible rule per priority/date, independently of the numeric axis.
        var intervals=parameters.Select(p=>(p.CodigoVariable,p.CodigoTipoNomina,p.Desde,p.Hasta))
            .Concat(result.Select(t=>(t.CodigoVariable,t.CodigoTipoNomina,t.Desde,t.Hasta)));
        foreach(var group in intervals.GroupBy(r=>(r.CodigoVariable,r.CodigoTipoNomina)))
        {
            DateOnly? previousEnd=null;var first=true;
            foreach(var rule in group.OrderBy(r=>r.Desde))
            {
                ct.ThrowIfCancellationRequested();
                if(!first&&(previousEnd is null||rule.Desde<=previousEnd))throw new RhNomError("VIGENCIAS_SUPERPUESTAS","Reglas simples/tablas superpuestas para el mismo parametro y prioridad.");
                first=false;previousEnd=rule.Hasta;
            }
        }
        return result.OrderBy(t=>t.CodigoVariable,StringComparer.Ordinal).ThenBy(t=>t.CodigoTipoNomina).ThenBy(t=>t.Desde).ThenBy(t=>t.Clave,StringComparer.Ordinal).ToList();
    }
    public static (RhNomTramo Row,RhNomTramoElegido Trace) Elegir(RhNomTablaTramos table,IReadOnlyDictionary<string,decimal>? inputs,CancellationToken ct=default)
    {
        ct.ThrowIfCancellationRequested();
        if(inputs is null||!inputs.TryGetValue(table.CodigoEntrada,out var value))throw new RhNomError("EJE_TRAMOS_SIN_VALOR",$"Falta [{table.CodigoEntrada}] para resolver [{table.CodigoVariable}].",variable:table.CodigoEntrada);
        var matches=table.Tramos.Where(r=>(r.Desde is null||value>=RhNomNumeros.Leer(r.Desde))&&(r.Hasta is null||value<RhNomNumeros.Leer(r.Hasta))).ToList();
        if(matches.Count!=1)throw new RhNomError(matches.Count==0?"TRAMO_SIN_COBERTURA":"TRAMO_AMBIGUO","El valor del eje requiere exactamente un tramo en la tabla vigente; no se usa fallback.",variable:table.CodigoEntrada);
        var row=matches[0];return(row,new(row.Clave,table.CodigoEntrada,table.UnidadEntrada,RhNomNumeros.Texto(value),row.Desde,row.Hasta));
    }
}
