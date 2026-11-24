namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

// Declarative diagnostic recipe. Aggregates/clock policy must be supplied with
// independent provenance; this class neither calls RH_SUELDO nor invents sources.
public record RhNomIntegralPhDef(RhNomConceptoCfg Concepto,string Siglas,DateOnly Desde,DateOnly? Hasta,
    string FactorSueldo,string FactorGrupo,string Porcentaje,string Divisor,string Decimales,string Anual,string Semanas,string DiasSemana);
public static class RhNomMigracionIntegralPh
{
    public const string Version="INTEGRAL_PH_CFG_V1";
    public const string PoliticaAmbigua="SUELDO_RELACION_VIGENTE";
    public static readonly IReadOnlyList<RhNomVariableSave> Variables=Array.AsReadOnly(new RhNomVariableSave[]{
        new(0,0,"MIG_PH_SAL","Sueldo vigente de relacion","NUMERO","DINERO","ENTRADA"),
        new(0,0,"MIG_PH_HMIN","Minimo historico SI del mes de referencia","NUMERO","DINERO","ENTRADA"),
        new(0,0,"MIG_PH_HMAX","Maximo historico SI del mes de referencia","NUMERO","DINERO","ENTRADA"),
        new(0,0,"MIG_PH_ACT_EX","Actual SI F/V sin frecuencia excluida","NUMERO","DINERO","ENTRADA"),
        new(0,0,"MIG_PH_HIST","Historico SI completo del mes de referencia","NUMERO","DINERO","ENTRADA"),
        new(0,0,"MIG_PH_ACT_SS","Actual SI F/V de conceptos SUELDO","NUMERO","DINERO","ENTRADA"),
        new(0,0,"MIG_PH_ACT_OT","Actual SI F/V de otros conceptos","NUMERO","DINERO","ENTRADA"),
        new(0,0,"MIG_PH_HEV_SS","Historico SI E/V de conceptos SUELDO","NUMERO","DINERO","ENTRADA"),
        new(0,0,"MIG_PH_HEV_OT","Historico SI E/V de otros conceptos","NUMERO","DINERO","ENTRADA"),
        new(0,0,"MIG_PH_AF_SS","Actual de conceptos SUELDO configurados AF","NUMERO","DINERO","ENTRADA"),
        new(0,0,"MIG_PH_GRU","Total de asociaciones del grupo PH","NUMERO","DINERO","ENTRADA"),
        new(0,0,"MIG_PH_OMIT","Control de omision PH","NUMERO","NUMERO","ENTRADA"),
        new(0,0,"MIG_PH_F_SS","Factor de ponderacion de SUELDO","NUMERO","FACTOR","PARAMETRO"),
        new(0,0,"MIG_PH_F_GRU","Factor de grupos PH","NUMERO","FACTOR","PARAMETRO"),
        new(0,0,"MIG_PH_PORC","Porcentaje PH","NUMERO","PORCENTAJE","PARAMETRO"),
        new(0,0,"MIG_PH_DIV","Divisor PH","NUMERO","FACTOR","PARAMETRO"),
        new(0,0,"MIG_PH_DEC","Decimales PH","NUMERO","NUMERO","PARAMETRO"),
        new(0,0,"MIG_PH_ANUAL","Factor anual","NUMERO","FACTOR","PARAMETRO"),
        new(0,0,"MIG_PH_SEM","Factor semanas","NUMERO","FACTOR","PARAMETRO"),
        new(0,0,"MIG_PH_DIAS","Factor dias por semana","NUMERO","FACTOR","PARAMETRO"),
        new(0,0,"MIG_PH_NORMAL","Integral sin ambiguedad","NUMERO","DINERO","CALCULADA"),
        new(0,0,"MIG_PH_INTEGRAL","Integral con politica de ambiguedad","NUMERO","DINERO","CALCULADA"),
        new(0,0,"MIG_PH_GBASE","Base de grupo PH","NUMERO","DINERO","CALCULADA"),
        new(0,0,"MIG_PH_BASE","Base PH elegida","NUMERO","DINERO","CALCULADA"),
        new(0,0,"MIG_PH_MES","PH mensual redondeado","NUMERO","DINERO","CALCULADA"),
        new(0,0,"MIG_PH_TOTAL","PH del periodo truncado","NUMERO","DINERO","CALCULADA")});
    public static RhNomModeloSave Crear(int model,int expected,string description,IReadOnlyList<RhNomVariable> variables,RhNomIntegralPhDef d)
    {
        if(d is null||d.Concepto is null||!d.Concepto.Elegible||d.Concepto.TipoConcepto!="D"||d.Siglas is not ("E" or "C" or "AN" or "AF" or "O"))throw new RhNomError("PH_MIG_INVALIDO","Concepto PH automatico de deduccion y siglas compatibles requeridos.");
        if(variables is null||variables.Count!=Variables.Count||Variables.Any(t=>variables.Count(v=>v.Codigo==t.Codigo&&v.Tipo==t.Tipo&&v.Unidad==t.Unidad&&v.Origen==t.Origen&&v.Activa)!=1))throw new RhNomError("VARIABLES_PH_INVALIDAS","Catalogo exacto de la receta integral PH requerido.");
        foreach(var value in new[]{d.FactorSueldo,d.FactorGrupo,d.Divisor,d.Anual,d.Semanas,d.DiasSemana})if(RhNomNumeros.Leer(value)<=0)throw new RhNomError("FACTOR_PH_INVALIDO","Factores y divisores positivos requeridos.");
        RhNomNumeros.Leer(d.Porcentaje);var scale=RhNomNumeros.Leer(d.Decimales);if(scale is <0 or >28||scale!=decimal.Truncate(scale))throw new RhNomError("ESCALA_PH_INVALIDA","Decimales enteros 0..28 requeridos.");
        RhNomParametro P(string code,string value)=>new(code,code,value,d.Desde,d.Hasta,d.Concepto.CodigoTipoNomina);
        var full="ROUND([MIG_PH_ACT_EX]+[MIG_PH_HIST],[MIG_PH_DEC])";
        var weighted="ROUND([MIG_PH_ACT_SS]*[MIG_PH_F_SS]+[MIG_PH_ACT_OT]+[MIG_PH_HEV_SS]*[MIG_PH_F_SS]+[MIG_PH_HEV_OT],[MIG_PH_DEC])";
        RhNomFormula normal=d.Siglas switch{
            "E"=>new("MIG_PH_NORMAL",full),
            "AF"=>new("MIG_PH_NORMAL","ROUND([MIG_PH_AF_SS],[MIG_PH_DEC])"),
            "O"=>new("MIG_PH_NORMAL","ROUND([MIG_PH_SAL]*[MIG_PH_ANUAL]/([MIG_PH_DIAS]*[MIG_PH_SEM]),[MIG_PH_DEC])"),
            _=>new("MIG_PH_NORMAL",full,new("MAYOR","MIG_PH_HMIN","DINERO",["0"]),weighted)};
        // Only C/AN have an active unordered first-row branch. E overwrites its
        // first-row query; AF/O follow independent paths and must not inherit it.
        RhNomFormula integral=d.Siglas is "C" or "AN"?new("MIG_PH_INTEGRAL","ROUND([MIG_PH_SAL],[MIG_PH_DEC])",new("TODAS",Condiciones:[new("MAYOR","MIG_PH_HMAX","DINERO",["0"]),new("MENOR_IGUAL","MIG_PH_HMIN","DINERO",["0"])]),"[MIG_PH_NORMAL]"):new("MIG_PH_INTEGRAL","[MIG_PH_NORMAL]");
        return new(model,expected,d.Concepto.CodigoTipoNomina,"RECALCULA_VARIACIONES",description,variables.Select(v=>new RhNomVariableRef(v.Id,v.Version)).ToList(),
            [normal,integral,new("MIG_PH_GBASE","[MIG_PH_GRU]*[MIG_PH_F_GRU]"),new("MIG_PH_BASE","[MIG_PH_INTEGRAL]",new("IGUAL","MIG_PH_GBASE","DINERO",["0"]),"[MIG_PH_GBASE]"),new("MIG_PH_MES","-ROUND([MIG_PH_BASE]*TASA([MIG_PH_PORC]),[MIG_PH_DEC])"),new("MIG_PH_TOTAL","TRUNC([MIG_PH_MES]/[MIG_PH_DIV],[MIG_PH_DEC])",new("IGUAL","MIG_PH_OMIT","NUMERO",["0"]),"0")],
            [P("MIG_PH_F_SS",d.FactorSueldo),P("MIG_PH_F_GRU",d.FactorGrupo),P("MIG_PH_PORC",d.Porcentaje),P("MIG_PH_DIV",d.Divisor),P("MIG_PH_DEC",d.Decimales),P("MIG_PH_ANUAL",d.Anual),P("MIG_PH_SEM",d.Semanas),P("MIG_PH_DIAS",d.DiasSemana)],true,RhNomCondiciones.Version,VersionTramosCliente:RhNomTramos.Version,ReglasConcepto:[new("MIG_PH_R","MIG_PH_TOTAL",d.Concepto.CodigoConcepto,d.Concepto.Huella,"F","CONSERVAR_FIRMADO","REGISTRAR_CERO","CONSERVAR_EXISTENTE")],VersionReglasCliente:RhNomReglasConcepto.Version);
    }
}
