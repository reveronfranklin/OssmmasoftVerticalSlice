namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public record RhNomFactoresSemanal(string Grupo,string Anual,string Semanas,string Divisor,string Decimales,int DiaIso);
public record RhNomDeduccionDef(string Rol,RhNomConceptoCfg Concepto,string Siglas,RhNomGrupoAcum? Grupo,
    List<string> Estados,List<string> Tipos,DateOnly Desde,DateOnly? Hasta,RhNomFactoresSemanal Factores,string PoliticaParametros,RhNomControlBinding Control);
public record RhNomPlantillaDeduccion(List<RhNomVariableSave> Variables,RhNomModeloSave Modelo)
{
    public RhNomModeloSave Solicitud(int id,int expected,string description,IReadOnlyList<RhNomVariable> catalog)
    {
        if(Variables.Any(t=>catalog.Count(v=>v.Codigo==t.Codigo&&v.Tipo==t.Tipo&&v.Unidad==t.Unidad&&v.Origen==t.Origen&&v.Activa)!=1))throw new RhNomError("CATALOGO_DED_INCOMPLETO","Variables activas de deduccion requeridas.");
        return Modelo with{ModeloId=id,VersionEsperada=expected,Descripcion=description,Variables=Variables.Select(t=>{var v=catalog.Single(v=>v.Codigo==t.Codigo);return new RhNomVariableRef(v.Id,v.Version);}).ToList()};
    }
}
public static class RhNomMigracionDeduccion
{
    public const string Version="SSO_PF_VARIACIONES_CFG_V1";
    public static RhNomPlantillaDeduccion Crear(RhNomDeduccionDef d)
    {
        if(d is null||d.Rol is not ("SSO" or "PF")||d.Concepto is null||!d.Concepto.Elegible||d.Concepto.TipoConcepto!="D"||d.Siglas is not ("E" or "C" or "O" or "JO" or "AF" or "AN")||d.Rol=="PF"&&d.Siglas=="JO")throw new RhNomError("DEDUCCION_MIG_INVALIDA","SSO/PF automatico de deduccion y siglas compatibles requeridos.");
        var f=d.Factores;if(f is null)throw new RhNomError("FACTORES_DED_INVALIDOS","Factores explicitos requeridos.");
        foreach(var v in new[]{f.Grupo,f.Anual,f.Semanas,f.Divisor})if(RhNomNumeros.Leer(v)<=0)throw new RhNomError("FACTORES_DED_INVALIDOS","Factores y divisores positivos requeridos.");
        var scale=RhNomNumeros.Leer(f.Decimales);if(scale is <0 or >28||scale!=decimal.Truncate(scale)||f.DiaIso is <1 or >7)throw new RhNomError("CALENDARIO_DED_INVALIDO","Decimales enteros 0..28 y dia ISO 1..7 requeridos.");
        RhNomParametrosConcepto.Validar(new(d.Concepto.CodigoConcepto,d.PoliticaParametros));RhNomControles.Validar(d.Control);if(d.Control.CodigoConcepto!=d.Concepto.CodigoConcepto)throw new RhNomError("CONTROL_DED_DISTINTO","Control del concepto destino requerido.");
        string C(string suffix)=>"MIG_RV_"+d.Rol+"_"+suffix;string T(string suffix)=>"["+C(suffix)+"]";
        var vars=new List<RhNomVariableSave>();var formulas=new List<RhNomFormula>();var parameters=new List<RhNomParametro>();var sources=new List<RhNomFuenteActualBinding>();
        void V(string suffix,string unit,string origin,string type="NUMERO")=>vars.Add(new(0,0,C(suffix),"Variaciones "+d.Rol+" "+suffix,type,unit,origin));
        void Input(string suffix,string unit,string provider){V(suffix,unit,"ENTRADA",unit=="TEXTO"?"TEXTO":"NUMERO");sources.Add(new(C(suffix),provider));}
        void Param(string suffix,string unit,string value){V(suffix,unit,"PARAMETRO");parameters.Add(new(C("P_"+suffix),C(suffix),value,d.Desde,d.Hasta,d.Concepto.CodigoTipoNomina));}
        void Formula(string suffix,string expression,RhNomCondicion? condition=null,string? alternate=null){V(suffix,"DINERO","CALCULADA");formulas.Add(new(C(suffix),expression,condition,alternate));}
        Input("SAL","DINERO","SUELDO_RELACION_ACTUAL");Input("SIG","TEXTO","SIGLAS_TIPO_NOMINA_ACTUAL");sources[^1]=sources[^1] with{ValoresPermitidos=[d.Siglas]};
        Input("OMIT","NUMERO",RhNomControles.Proveedor);sources[^1]=sources[^1] with{Control=d.Control,ValoresPermitidos=["0","1"]};
        Input("PORC","PORCENTAJE","PARAM_CON_PORCENTAJE");sources[^1]=sources[^1] with{ParametroConcepto=new(d.Concepto.CodigoConcepto,d.PoliticaParametros)};
        Input("TOPE","DINERO","PARAM_CON_TOPE");sources[^1]=sources[^1] with{ParametroConcepto=new(d.Concepto.CodigoConcepto,d.PoliticaParametros)};
        Input("OCUR","CANTIDAD","DIA_ISO_MES_NOMINA");sources[^1]=sources[^1] with{DiaSemanaIso=f.DiaIso};
        var groupExpression="0";
        if(d.Grupo is {} group)
        {
            RhNomGruposAcum.ValidarUso(group,d.Concepto.CodigoEmpresa,d.Concepto.CodigoTipoNomina,group.FechaReferencia);
            if(group.Codigo!=d.Rol||group.Conceptos.Contains(d.Concepto.CodigoConcepto))throw new RhNomError("GRUPO_DED_INVALIDO","Grupo del rol sin su propio descuento requerido.");
            if(group.Conceptos.Count>0){Input("MOV_GRU","DINERO","ACUMULADOR_MOV_ACTUAL");sources[^1]=sources[^1] with{Acumulador=RhNomAcumuladores.Validar(new(group.Conceptos,d.Estados,d.Tipos,"TODAS",null,"CERO_EXPLICITO",Grupo:group))};groupExpression=T("MOV_GRU");}
        }
        Param("FACTOR","FACTOR",f.Grupo);Param("ANUAL","FACTOR",f.Anual);Param("SEM","CANTIDAD",f.Semanas);Param("DIV","FACTOR",f.Divisor);Param("DEC","NUMERO",f.Decimales);
        Formula("GRU",groupExpression=="0"?"0":groupExpression+"*"+T("FACTOR"));Formula("BASE",T("SAL"),new("IGUAL",C("GRU"),"DINERO",["0"]),T("GRU"));
        Formula("MES","-MIN("+T("BASE")+"*TASA("+T("PORC")+"),"+T("TOPE")+"*TASA("+T("PORC")+"))");
        Formula("SEMANA","ROUND("+T("MES")+"*"+T("ANUAL")+"/"+T("SEM")+"*"+T("OCUR")+","+T("DEC")+")");
        Formula("TOTAL",(d.Rol=="SSO"?"ROUND":"TRUNC")+"("+T("SEMANA")+"/"+T("DIV")+","+T("DEC")+")",new("TODAS",Condiciones:[new("IGUAL",C("OMIT"),"NUMERO",["0"]),new("IGUAL",C("SIG"),"TEXTO",[d.Siglas])]),"0");
        var catalog=vars.Select((v,i)=>new RhNomVariable(i+1,v.Codigo,v.Descripcion,v.Tipo,v.Unidad,v.Origen,true,1)).ToList();
        var request=new RhNomModeloSave(1,0,d.Concepto.CodigoTipoNomina,"RECALCULA_VARIACIONES","Plantilla variaciones "+d.Rol,catalog.Select(v=>new RhNomVariableRef(v.Id,v.Version)).ToList(),formulas,parameters,true,RhNomCondiciones.Version,VersionTramosCliente:RhNomTramos.Version,ReglasConcepto:[new(C("R"),C("TOTAL"),d.Concepto.CodigoConcepto,d.Concepto.Huella,"F","CONSERVAR_FIRMADO","REGISTRAR_CERO","CONSERVAR_EXISTENTE")],VersionReglasCliente:RhNomReglasConcepto.Version,FuentesActuales:sources,VersionFuentesCliente:RhNomFuentesModelo.Version);
        RhNomConfiguracion.Snapshot(request,catalog,d.Concepto.CodigoEmpresa,conceptos:[d.Concepto]);return new(vars,request);
    }
}
