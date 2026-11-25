namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

// Diagnostic migration of the base, not a deduction nor a registered data source.
// Historical/current aggregates are independent explicit inputs. No expected FPS
// output is used as input, and no implicit historical window is selected here.
public static class RhNomMigracionBaseFps
{
    public const string Version="BASE_FPS_UNION_CFG_V1";
    public static readonly IReadOnlyList<RhNomVariableSave> Variables=Array.AsReadOnly(new RhNomVariableSave[]{
        new(0,0,"MIG_FPS_SAL","Sueldo base de relacion","NUMERO","DINERO","ENTRADA"),
        new(0,0,"MIG_FPS_ANT_H","Antiguedad historica V del mismo mes","NUMERO","DINERO","ENTRADA"),
        new(0,0,"MIG_FPS_ANT_A","Antiguedad de movimientos actuales","NUMERO","DINERO","ENTRADA"),
        new(0,0,"MIG_FPS_GRU","Suma de asociaciones FJP y FPS","NUMERO","DINERO","ENTRADA"),
        new(0,0,"MIG_FPS_FACTOR","Factor de base de grupos","NUMERO","FACTOR","PARAMETRO"),
        new(0,0,"MIG_FPS_UNION","Suma de los dos totales distintos","NUMERO","DINERO","CALCULADA"),
        new(0,0,"MIG_FPS_FALL","Sueldo mas antiguedad UNION","NUMERO","DINERO","CALCULADA"),
        new(0,0,"MIG_FPS_GBASE","Base de grupos ponderada","NUMERO","DINERO","CALCULADA"),
        new(0,0,"MIG_FPS_BASE","Base seleccionada para FPS","NUMERO","DINERO","CALCULADA")});
    public static RhNomModeloSave Crear(int modelo,int expected,int tipo,string description,
        IReadOnlyList<RhNomVariable> variables,DateOnly desde,DateOnly? hasta,string factorGrupo)
    {
        if(tipo<=0||RhNomNumeros.Leer(factorGrupo)<=0)throw new RhNomError("BASE_FPS_INVALIDA","Tipo positivo y factor de grupo positivo explicitos requeridos.");
        if(variables is null||variables.Count!=Variables.Count||Variables.Any(t=>variables.Count(v=>v.Codigo==t.Codigo&&v.Tipo==t.Tipo&&v.Unidad==t.Unidad&&v.Origen==t.Origen&&v.Activa)!=1))
            throw new RhNomError("VARIABLES_BASE_FPS_INVALIDAS","Catalogo activo con las nueve variables exactas de la receta requerido.");
        RhNomParametros.Fecha(desde);if(hasta is {} end){RhNomParametros.Fecha(end);if(end<desde)throw new RhNomError("VIGENCIA_INVALIDA","Hasta no puede ser anterior a Desde.");}
        return new(modelo,expected,tipo,"RECALCULA_VARIACIONES",description,variables.Select(v=>new RhNomVariableRef(v.Id,v.Version)).ToList(),
            [new("MIG_FPS_UNION","[MIG_FPS_ANT_H]",new("IGUAL","MIG_FPS_ANT_H",VariableDerecha:"MIG_FPS_ANT_A"),"[MIG_FPS_ANT_H]+[MIG_FPS_ANT_A]"),
             new("MIG_FPS_FALL","[MIG_FPS_SAL]+[MIG_FPS_UNION]"),
             new("MIG_FPS_GBASE","[MIG_FPS_GRU]*[MIG_FPS_FACTOR]"),
             new("MIG_FPS_BASE","[MIG_FPS_FALL]",new("IGUAL","MIG_FPS_GBASE","DINERO",["0"]),"[MIG_FPS_GBASE]")],
            [new("MIG_FPS_P_FACTOR","MIG_FPS_FACTOR",factorGrupo,desde,hasta,tipo)],true,VersionCondicionesCliente:RhNomCondiciones.Version);
    }
}
