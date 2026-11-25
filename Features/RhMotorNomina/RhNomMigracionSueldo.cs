namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

// Migration recipe only: all coefficients and destination metadata are explicit,
// persisted by the ordinary model/version workflow, never inferred from amounts.
public record RhNomFrecuenciaSueldo(int FrecuenciaId,string Numerador,string Denominador);
public static class RhNomMigracionSueldo
{
    public const string Version="SUELDO_BASICO_CFG_V1";
    public static readonly IReadOnlyList<RhNomVariableSave> Variables=Array.AsReadOnly(new RhNomVariableSave[]{
        new(0,0,"MIG_SB_MES","Sueldo de relacion","NUMERO","DINERO","ENTRADA"),
        new(0,0,"MIG_SB_FREQ","Frecuencia de pago","NUMERO","NUMERO","ENTRADA"),
        new(0,0,"MIG_SB_NUM","Numerador por frecuencia","NUMERO","NUMERO","PARAMETRO"),
        new(0,0,"MIG_SB_DEN","Denominador por frecuencia","NUMERO","NUMERO","PARAMETRO"),
        new(0,0,"MIG_SB_DEC","Decimales de redondeo","NUMERO","NUMERO","PARAMETRO"),
        new(0,0,"MIG_SB_TOTAL","Sueldo basico del periodo","NUMERO","DINERO","CALCULADA")});
    public static RhNomModeloSave Crear(int modeloId,int versionEsperada,int tipo,string descripcion,
        IReadOnlyList<RhNomVariable> variables,RhNomConceptoCfg concepto,
        IReadOnlyList<RhNomFrecuenciaSueldo> frecuencias,DateOnly desde,DateOnly? hasta,string decimales)
    {
        if(frecuencias is null||frecuencias.Count is <1 or >50||frecuencias.Any(f=>f is null||f.FrecuenciaId<=0||f.FrecuenciaId==int.MaxValue)
            ||frecuencias.Select(f=>f.FrecuenciaId).Distinct().Count()!=frecuencias.Count)
            throw new RhNomError("FRECUENCIAS_MIG_INVALIDAS","Declare 1..50 frecuencias distintas con factores explicitos.");
        foreach(var f in frecuencias)if(RhNomNumeros.Leer(f.Numerador)<=0||RhNomNumeros.Leer(f.Denominador)<=0)
            throw new RhNomError("FACTOR_MIG_INVALIDO","Numerador y denominador deben ser positivos.");
        var decimals=RhNomNumeros.Leer(decimales);
        if(decimals<0||decimals>28||decimal.Truncate(decimals)!=decimals)throw new RhNomError("ESCALA_MIG_INVALIDA","Decimales debe ser un entero entre 0 y 28.");
        if(variables.Count!=Variables.Count||Variables.Any(template=>!variables.Any(v=>v.Codigo==template.Codigo&&v.Tipo==template.Tipo&&v.Unidad==template.Unidad&&v.Origen==template.Origen&&v.Activa)))
            throw new RhNomError("VARIABLES_MIG_INVALIDAS","El catalogo requiere las seis variables activas con tipo/unidad/origen de la receta.");
        RhNomTablaTramos Tabla(string code,bool numerator)=>new(code,code,"MIG_SB_FREQ","NUMERO",desde,hasta,tipo,
            frecuencias.OrderBy(f=>f.FrecuenciaId).Select(f=>new RhNomTramo("F_"+f.FrecuenciaId,
                RhNomNumeros.Texto(f.FrecuenciaId),RhNomNumeros.Texto((long)f.FrecuenciaId+1),numerator?f.Numerador:f.Denominador)).ToList());
        return new(modeloId,versionEsperada,tipo,"ADJUNTAR_CARGO",descripcion,
            variables.Select(v=>new RhNomVariableRef(v.Id,v.Version)).ToList(),
            [new("MIG_SB_TOTAL","ROUND([MIG_SB_MES]*[MIG_SB_NUM]/[MIG_SB_DEN],[MIG_SB_DEC])")],
            [new("MIG_SB_ESCALA","MIG_SB_DEC",RhNomNumeros.Texto(decimals),desde,hasta,tipo)],true,
            VersionCondicionesCliente:RhNomCondiciones.Version,TablasTramos:[Tabla("MIG_SB_NUM",true),Tabla("MIG_SB_DEN",false)],VersionTramosCliente:RhNomTramos.Version,
            ReglasConcepto:[new("MIG_SB_REGLA","MIG_SB_TOTAL",concepto.CodigoConcepto,concepto.Huella,"F","CONSERVAR_FIRMADO","REGISTRAR_CERO","CONSERVAR_EXISTENTE")],
            VersionReglasCliente:RhNomReglasConcepto.Version,
            FuentesActuales:[new("MIG_SB_MES","SUELDO_RELACION_ACTUAL"),new("MIG_SB_FREQ","FRECUENCIA_PAGO_ACTUAL")],VersionFuentesCliente:RhNomFuentesModelo.Version);
    }
}
