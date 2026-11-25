namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public record RhNomFpsDef(RhNomConceptoCfg Concepto,string Siglas,List<int> ConceptosAntiguedad,
    List<RhNomGrupoAcum> Grupos,List<string> EstadosMovimiento,List<string> TiposMovimiento,
    DateOnly Desde,DateOnly? Hasta,string FactorGrupo,string Divisor,string Decimales,string PoliticaParametros,RhNomControlBinding Control);
public record RhNomPlantillaFps(List<RhNomVariableSave> Variables,RhNomModeloSave Modelo)
{
    public RhNomModeloSave Solicitud(int modelo,int expected,string description,IReadOnlyList<RhNomVariable> catalog)
    {
        if(Variables.Any(t=>catalog.Count(v=>v.Codigo==t.Codigo&&v.Tipo==t.Tipo&&v.Unidad==t.Unidad&&v.Origen==t.Origen&&v.Activa)!=1))throw new RhNomError("CATALOGO_FPS_INCOMPLETO","Catalogo activo compatible con la receta FPS requerido.");
        return Modelo with{ModeloId=modelo,VersionEsperada=expected,Descripcion=description,Variables=Variables.Select(t=>{var v=catalog.Single(v=>v.Codigo==t.Codigo);return new RhNomVariableRef(v.Id,v.Version);}).ToList()};
    }
}
public static class RhNomMigracionFps
{
    public const string Version="FPS_VARIACIONES_CFG_V1";
    public static RhNomPlantillaFps Crear(RhNomFpsDef d)
    {
        if(d is null||d.Concepto is null||!d.Concepto.Elegible||d.Concepto.TipoConcepto!="D"||d.Siglas is not ("E" or "AF" or "AN"))throw new RhNomError("CONCEPTO_FPS_INVALIDO","Concepto FPS automatico de deduccion y siglas E/AF/AN requeridos.");
        if(d.ConceptosAntiguedad is null||d.ConceptosAntiguedad.Count>100||d.ConceptosAntiguedad.Contains(d.Concepto.CodigoConcepto))throw new RhNomError("ANTIGUEDAD_FPS_INVALIDA","Declare conceptos de antiguedad explicitos, sin incluir FPS.");
        if(d.Grupos is null||d.Grupos.Count>2||d.Grupos.Any(g=>g is null||g.Codigo is not ("FJP" or "FPS"))||d.Grupos.Select(g=>g.Codigo).Distinct().Count()!=d.Grupos.Count)throw new RhNomError("GRUPOS_FPS_INVALIDOS","Hasta dos grupos capturados distintos FJP/FPS.");
        RhNomParametrosConcepto.Validar(new(d.Concepto.CodigoConcepto,d.PoliticaParametros));RhNomControles.Validar(d.Control);
        if(d.Control.CodigoConcepto!=d.Concepto.CodigoConcepto)throw new RhNomError("CONTROL_FPS_DISTINTO","Control del mismo concepto destino requerido.");
        var scale=RhNomNumeros.Leer(d.Decimales);if(scale is <0 or >28||scale!=decimal.Truncate(scale)||RhNomNumeros.Leer(d.Divisor)<=0)throw new RhNomError("FACTOR_FPS_INVALIDO","Divisor positivo y decimales enteros 0..28 requeridos.");
        var vars=RhNomMigracionBaseFps.Variables.Select(v=>v.Codigo=="MIG_FPS_GRU"||d.ConceptosAntiguedad.Count==0&&v.Codigo is "MIG_FPS_ANT_H" or "MIG_FPS_ANT_A"?v with{Origen="CALCULADA"}:v).ToList();
        vars.AddRange([new(0,0,"MIG_FPS_PORC","Porcentaje de FPS capturado","NUMERO","PORCENTAJE","ENTRADA"),new(0,0,"MIG_FPS_OMIT","Control de omision FPS","NUMERO","NUMERO","ENTRADA"),new(0,0,"MIG_FPS_SIG","Siglas del tipo capturado","TEXTO","TEXTO","ENTRADA"),new(0,0,"MIG_FPS_DIV","Divisor de FPS","NUMERO","FACTOR","PARAMETRO"),new(0,0,"MIG_FPS_DEC","Decimales FPS","NUMERO","NUMERO","PARAMETRO"),new(0,0,"MIG_FPS_TOTAL","Descuento FPS del periodo","NUMERO","DINERO","CALCULADA")]);
        var baseCatalog=RhNomMigracionBaseFps.Variables.Select((v,i)=>new RhNomVariable(i+1,v.Codigo,v.Descripcion,v.Tipo,v.Unidad,v.Origen,true,1)).ToList();
        var request=RhNomMigracionBaseFps.Crear(1,0,d.Concepto.CodigoTipoNomina,"Plantilla FPS",baseCatalog,d.Desde,d.Hasta,d.FactorGrupo);
        var formulas=request.Formulas.ToList();var sources=new List<RhNomFuenteActualBinding>{new("MIG_FPS_SAL","SUELDO_RELACION_ACTUAL"),new("MIG_FPS_PORC","PARAM_CON_PORCENTAJE",ParametroConcepto:new(d.Concepto.CodigoConcepto,d.PoliticaParametros)),new("MIG_FPS_OMIT",RhNomControles.Proveedor,Control:d.Control),new("MIG_FPS_SIG","SIGLAS_TIPO_NOMINA_ACTUAL",ValoresPermitidos:[d.Siglas])};
        if(d.ConceptosAntiguedad.Count==0){formulas.Add(new("MIG_FPS_ANT_A","0"));formulas.Add(new("MIG_FPS_ANT_H","0"));}
        else
        {
            sources.Add(new("MIG_FPS_ANT_A","ACUMULADOR_MOV_ACTUAL",Acumulador:RhNomAcumuladores.Validar(new(d.ConceptosAntiguedad,d.EstadosMovimiento,d.TiposMovimiento,"TODAS",null,"CERO_EXPLICITO"))));
            sources.Add(new("MIG_FPS_ANT_H",RhNomAcumuladoresHistoricos.ProveedorMes,AcumuladorHistorico:new(0,RhNomAcumuladores.Validar(new(d.ConceptosAntiguedad,d.EstadosMovimiento,["V"],"TODAS",null,"CERO_EXPLICITO")),"MES_NOMINA")));
        }
        var terms=new List<string>();
        foreach(var group in d.Grupos.OrderBy(g=>g.Codigo,StringComparer.Ordinal))
        {
            RhNomGruposAcum.ValidarUso(group,d.Concepto.CodigoEmpresa,d.Concepto.CodigoTipoNomina,group.FechaReferencia);
            if(group.Conceptos.Contains(d.Concepto.CodigoConcepto))throw new RhNomError("BASE_FPS_RECURSIVA","La base FPS no puede incluir su propio descuento.");
            if(group.Conceptos.Count==0)continue;
            var code="MIG_FPS_G_"+group.TipoAcumuladoId;vars.Add(new(0,0,code,"Grupo FPS capturado","NUMERO","DINERO","ENTRADA"));terms.Add("["+code+"]");
            sources.Add(new(code,"ACUMULADOR_MOV_ACTUAL",Acumulador:RhNomAcumuladores.Validar(new(group.Conceptos,d.EstadosMovimiento,d.TiposMovimiento,"TODAS",null,"CERO_EXPLICITO",Grupo:group))));
        }
        formulas.Add(new("MIG_FPS_GRU",terms.Count==0?"0":string.Join("+",terms)));
        formulas.Add(new("MIG_FPS_TOTAL","ROUND(-[MIG_FPS_BASE]*TASA([MIG_FPS_PORC])/[MIG_FPS_DIV],[MIG_FPS_DEC])",new("TODAS",Condiciones:[new("IGUAL","MIG_FPS_OMIT","NUMERO",["0"]),new("IGUAL","MIG_FPS_SIG","TEXTO",[d.Siglas])]),"0"));
        var parameters=request.Parametros!.ToList();parameters.AddRange([new("MIG_FPS_P_DIV","MIG_FPS_DIV",d.Divisor,d.Desde,d.Hasta,d.Concepto.CodigoTipoNomina),new("MIG_FPS_P_DEC","MIG_FPS_DEC",d.Decimales,d.Desde,d.Hasta,d.Concepto.CodigoTipoNomina)]);
        // Catalog variable origins are global: explicit-zero outputs need their own
        // codes instead of changing the origin of another type's captured input.
        if(d.ConceptosAntiguedad.Count==0)
        {
            string Code(string code)=>code is "MIG_FPS_ANT_H" or "MIG_FPS_ANT_A"?code+"_CERO":code;
            RhNomCondicion? Condition(RhNomCondicion? c)=>c is null?null:c with{Variable=c.Variable is null?null:Code(c.Variable),VariableDerecha=c.VariableDerecha is null?null:Code(c.VariableDerecha),Condiciones=c.Condiciones?.Select(x=>Condition(x)!).ToList()};
            string Expression(string value)=>value.Replace("[MIG_FPS_ANT_H]","[MIG_FPS_ANT_H_CERO]").Replace("[MIG_FPS_ANT_A]","[MIG_FPS_ANT_A_CERO]");
            vars=vars.Select(v=>v with{Codigo=Code(v.Codigo)}).ToList();
            formulas=formulas.Select(f=>f with{VariableSalida=Code(f.VariableSalida),Expresion=Expression(f.Expresion),Condicion=Condition(f.Condicion),ExpresionAlternativa=f.ExpresionAlternativa is null?null:Expression(f.ExpresionAlternativa)}).ToList();
        }
        var catalog=vars.Select((v,i)=>new RhNomVariable(i+1,v.Codigo,v.Descripcion,v.Tipo,v.Unidad,v.Origen,true,1)).ToList();
        request=request with{VersionTramosCliente=RhNomTramos.Version,Variables=catalog.Select(v=>new RhNomVariableRef(v.Id,v.Version)).ToList(),Formulas=formulas,Parametros=parameters,ReglasConcepto=[new("MIG_FPS_R","MIG_FPS_TOTAL",d.Concepto.CodigoConcepto,d.Concepto.Huella,"F","CONSERVAR_FIRMADO","REGISTRAR_CERO","CONSERVAR_EXISTENTE")],VersionReglasCliente=RhNomReglasConcepto.Version,FuentesActuales=sources,VersionFuentesCliente=RhNomFuentesModelo.Version};
        RhNomConfiguracion.Snapshot(request,catalog,d.Concepto.CodigoEmpresa,conceptos:[d.Concepto]);return new(vars,request);
    }
}
