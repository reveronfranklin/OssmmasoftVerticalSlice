namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

// A migration recipe, not a calculator or an Oracle-package adapter. The ordinary
// immutable model workflow persists and evaluates the generated graph and sources.
public record RhNomPorcentajeTitulo(int TituloId,string Porcentaje);
public record RhNomProfesionDef(RhNomConceptoCfg Concepto,string Siglas,RhNomAcumuladorActual Base,
    List<RhNomPorcentajeTitulo> Titulos,DateOnly Desde,DateOnly? Hasta,string FactorBase,string Divisor,string Decimales,string PoliticaSinEducacion="ERROR");
public static class RhNomMigracionProfesion
{
    public const string Version="PROFESION_CFG_V1";
    public static readonly IReadOnlyList<RhNomVariableSave> Variables=Array.AsReadOnly(new RhNomVariableSave[]{
        new(0,0,"MIG_PR_MOV","Movimientos base declarados","NUMERO","DINERO","ENTRADA"),
        new(0,0,"MIG_PR_TIT","Titulo de ultima educacion finalizada","NUMERO","NUMERO","ENTRADA"),
        new(0,0,"MIG_PR_SIG","Siglas del tipo capturado","TEXTO","TEXTO","ENTRADA"),
        new(0,0,"MIG_PR_FACTOR","Factor de base","NUMERO","FACTOR","PARAMETRO"),
        new(0,0,"MIG_PR_DIV","Divisor del periodo","NUMERO","FACTOR","PARAMETRO"),
        new(0,0,"MIG_PR_DEC","Decimales","NUMERO","NUMERO","PARAMETRO"),
        new(0,0,"MIG_PR_PORC","Porcentaje por titulo","NUMERO","PORCENTAJE","PARAMETRO"),
        new(0,0,"MIG_PR_BASE","Base profesional","NUMERO","DINERO","CALCULADA"),
        new(0,0,"MIG_PR_MES","Bono mensual redondeado","NUMERO","DINERO","CALCULADA"),
        new(0,0,"MIG_PR_PER","Bono del periodo truncado","NUMERO","DINERO","CALCULADA"),
        new(0,0,"MIG_PR_TOTAL","Bono profesional aplicable","NUMERO","DINERO","CALCULADA")});
    public static RhNomModeloSave Crear(int modelo,int expected,string description,IReadOnlyList<RhNomVariable> variables,RhNomProfesionDef d)
    {
        if(d is null||d.Concepto is null||!d.Concepto.Elegible||d.Concepto.TipoConcepto!="A"||d.Concepto.CodigoEmpresa<=0||d.Concepto.CodigoTipoNomina<=0)
            throw new RhNomError("CONCEPTO_PROF_INVALIDO","Concepto profesional activo/automatico de asignacion requerido.");
        if(d.Siglas is not ("E" or "AN" or "AF" or "C"))throw new RhNomError("TIPO_PROF_INVALIDO","La receta de profesion admite E, AN, AF o C; CE conserva importes manuales.");
        if(d.PoliticaSinEducacion is not ("ERROR" or "NO_APLICA_EXPLICITO"))throw new RhNomError("POLITICA_EDUCACION_INVALIDA","Declare ERROR o NO_APLICA_EXPLICITO para ausencia de educacion.");
        var source=RhNomAcumuladores.Validar(d.Base);
        if(source.Conceptos.Contains(d.Concepto.CodigoConcepto))throw new RhNomError("BASE_PROF_RECURSIVA","El destino profesional no puede pertenecer a su base.");
        if(d.Titulos is null||d.Titulos.Count is <1 or >50||d.Titulos.Any(t=>t is null||t.TituloId<=0||t.TituloId==int.MaxValue)||d.Titulos.Select(t=>t.TituloId).Distinct().Count()!=d.Titulos.Count)
            throw new RhNomError("TITULOS_PROF_INVALIDOS","Declare 1..50 titulos distintos con porcentajes explicitos.");
        foreach(var t in d.Titulos)RhNomNumeros.Leer(t.Porcentaje);
        if(RhNomNumeros.Leer(d.FactorBase)<=0||RhNomNumeros.Leer(d.Divisor)<=0)throw new RhNomError("FACTOR_PROF_INVALIDO","Factor de base y divisor positivos requeridos.");
        var scale=RhNomNumeros.Leer(d.Decimales);
        if(scale<0||scale>28||scale!=decimal.Truncate(scale))throw new RhNomError("ESCALA_PROF_INVALIDA","Decimales enteros entre 0 y 28 requeridos.");
        if(variables is null||variables.Count!=Variables.Count||Variables.Any(t=>variables.Count(v=>v.Codigo==t.Codigo&&v.Tipo==t.Tipo&&v.Unidad==t.Unidad&&v.Origen==t.Origen&&v.Activa)!=1))
            throw new RhNomError("VARIABLES_PROF_INVALIDAS","Catalogo activo con las variables exactas de la receta requerido.");
        RhNomParametro Param(string code,string value)=>new(code,code,value,d.Desde,d.Hasta,d.Concepto.CodigoTipoNomina);
        var titles=d.Titulos.OrderBy(t=>t.TituloId).Select(t=>new RhNomTramo("T_"+t.TituloId,RhNomNumeros.Texto(t.TituloId),RhNomNumeros.Texto((long)t.TituloId+1),t.Porcentaje)).ToList();
        if(d.PoliticaSinEducacion=="NO_APLICA_EXPLICITO")titles.Insert(0,new("SIN_EDUCACION","0","1","0"));
        var request=new RhNomModeloSave(modelo,expected,d.Concepto.CodigoTipoNomina,"RECALCULA_VARIACIONES",description,
            variables.Select(v=>new RhNomVariableRef(v.Id,v.Version)).ToList(),
            [new("MIG_PR_BASE","[MIG_PR_MOV]*[MIG_PR_FACTOR]"),
             new("MIG_PR_MES","ROUND([MIG_PR_BASE]*TASA([MIG_PR_PORC]),[MIG_PR_DEC])"),
             new("MIG_PR_PER","TRUNC([MIG_PR_MES]/[MIG_PR_DIV],[MIG_PR_DEC])"),
             new("MIG_PR_TOTAL","[MIG_PR_PER]",new("TODAS",Condiciones:[
                 new("IGUAL","MIG_PR_SIG","TEXTO",[d.Siglas]),new("MAYOR","MIG_PR_BASE","DINERO",["0"]),new("MAYOR","MIG_PR_PER","DINERO",["0"])]),"0")],
            [Param("MIG_PR_FACTOR",d.FactorBase),Param("MIG_PR_DIV",d.Divisor),Param("MIG_PR_DEC",d.Decimales)],true,
            VersionCondicionesCliente:RhNomCondiciones.Version,
            TablasTramos:[new("MIG_PR_T_PORC","MIG_PR_PORC","MIG_PR_TIT","NUMERO",d.Desde,d.Hasta,d.Concepto.CodigoTipoNomina,
                titles)],VersionTramosCliente:RhNomTramos.Version,
            ReglasConcepto:[new("MIG_PR_REGLA","MIG_PR_TOTAL",d.Concepto.CodigoConcepto,d.Concepto.Huella,"F","CONSERVAR_FIRMADO","CONSERVAR_EXISTENTE","CONSERVAR_EXISTENTE")],VersionReglasCliente:RhNomReglasConcepto.Version,
            FuentesActuales:[new("MIG_PR_MOV","ACUMULADOR_MOV_ACTUAL",Acumulador:source),new("MIG_PR_TIT",d.PoliticaSinEducacion=="ERROR"?"TITULO_EDUC_ULTIMO_REG":RhNomDatosPersonales.UltimoOSinRegistro),new("MIG_PR_SIG","SIGLAS_TIPO_NOMINA_ACTUAL",ValoresPermitidos:[d.Siglas])],VersionFuentesCliente:RhNomFuentesModelo.Version);
        RhNomConfiguracion.Snapshot(request,variables,d.Concepto.CodigoEmpresa,conceptos:[d.Concepto]);
        return request;
    }
}
