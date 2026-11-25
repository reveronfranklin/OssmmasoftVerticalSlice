namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public record RhNomPlantillaGrupoEtapa(List<RhNomVariableSave> Variables,RhNomModeloSave Modelo)
{
    public RhNomModeloSave Solicitud(int id,int expected,string description,IReadOnlyList<RhNomVariable> catalog)
    {
        if(Variables.Any(t=>catalog.Count(v=>v.Codigo==t.Codigo&&v.Tipo==t.Tipo&&v.Unidad==t.Unidad&&v.Origen==t.Origen&&v.Activa)!=1))
            throw new RhNomError("CATALOGO_ETAPA_INCOMPLETO","Registre las variables activas de la plantilla antes de guardar.");
        return Modelo with{ModeloId=id,VersionEsperada=expected,Descripcion=description,Variables=Variables.Select(t=>{var v=catalog.Single(v=>v.Codigo==t.Codigo);return new RhNomVariableRef(v.Id,v.Version);}).ToList()};
    }
}
public static class RhNomComposicionGrupo
{
    public static void VerificarCapturas(RhNomSnapshot snapshot)
    {
        foreach(var source in snapshot.FuentesActuales??[])
        {
            if(source.Acumulador?.Grupo is null)continue;
            foreach(var rule in snapshot.ReglasConcepto??[])
                if(source.Acumulador.ConceptosExcluidos?.Contains(rule.Concepto.CodigoConcepto)==true)
                    ValidarEfectiva(rule,snapshot.Formulas.Single(f=>f.VariableSalida==rule.VariableSalida));
        }
    }
    private static void ValidarEfectiva(RhNomReglaConcepto rule,RhNomFormula formula)
    {
            if(rule.AccionCero is "CONSERVAR_EXISTENTE" or "OMITIR"||rule.AccionNoAplica is "CONSERVAR_EXISTENTE" or "OMITIR")
                throw new RhNomError("SALIDA_EFECTIVA_NO_DEFINIDA","Una salida que conserva un movimiento anterior no puede reemplazarlo por su resultado calculado en otro grupo.");
            if(rule.AccionNoAplica=="RETIRAR_GENERADO"&&formula.Condicion is not null&&formula.ExpresionAlternativa?.Trim()!="0")
                throw new RhNomError("RETIRO_CON_ALTERNATIVA","La salida retirada requiere alternativa cero explicita antes de incorporarla a un grupo posterior.");
    }
    // Operates on migration templates only, never on a persisted revision/catalog.
    public static RhNomPlantillaGrupoEtapa Aplicar(List<RhNomVariableSave> definitions,RhNomModeloSave request,
        int company,IReadOnlyList<RhNomConceptoCfg> concepts,string sourceCode,string prefix,
        string stage,List<RhNomSalidaGrupo> outputs,DateOnly from,DateOnly? until)
    {
        sourceCode=RhNomNumeros.Codigo(sourceCode);prefix=RhNomNumeros.Codigo(prefix);
        List<RhNomVariable> Catalog(List<RhNomVariableSave> vars)=>vars.Select((v,i)=>new RhNomVariable(i+1,v.Codigo,v.Descripcion,v.Tipo,v.Unidad,v.Origen,v.Activa,1)).ToList();
        RhNomModeloSave Bind(RhNomModeloSave r,List<RhNomVariable> c)=>r with{Variables=c.Select(v=>new RhNomVariableRef(v.Id,v.Version)).ToList()};
        var original=RhNomConfiguracion.Snapshot(Bind(request,Catalog(definitions)),Catalog(definitions),company,conceptos:concepts);
        var source=original.FuentesActuales?.SingleOrDefault(s=>s.CodigoVariable==sourceCode);
        if(source?.Proveedor!="ACUMULADOR_MOV_ACTUAL"||source.Acumulador?.Grupo is not {} group)
            throw new RhNomError("GRUPO_COMPUESTO_REQUERIDO","Seleccione una entrada de acumulador con grupo capturado.");
        if(outputs is null||outputs.Any(o=>o is null||original.ReglasConcepto?.Count(r=>r.Concepto.CodigoConcepto==o.CodigoConcepto&&r.VariableSalida==RhNomNumeros.Codigo(o.VariableSalida))!=1))
            throw new RhNomError("SALIDA_GRUPO_SIN_REGLA","Cada salida nueva debe tener regla monetaria del mismo concepto en el modelo compuesto.");
        var plan=RhNomGruposEtapa.Preparar(group,company,request.CodigoTipoNomina,group.FechaReferencia,stage,source.Acumulador.Estados,source.Acumulador.TiposMovimiento,outputs);
        foreach(var term in plan.Salidas)
        {
            var rule=original.ReglasConcepto!.Single(r=>r.Concepto.CodigoConcepto==term.CodigoConcepto&&r.VariableSalida==term.VariableSalida);
            var formula=original.Formulas.Single(f=>f.VariableSalida==term.VariableSalida);
            ValidarEfectiva(rule,formula);
        }
        if((source.Acumulador.ConceptosExcluidos?.Count??0)>0||source.ValoresPermitidos is not null||source.Acumulador.PoliticaFrecuencias!="TODAS"||source.Acumulador.SinFilas!="CERO_EXPLICITO")
            throw new RhNomError("FILTRO_COMPUESTO_NO_ADMITIDO","La plantilla requiere TODAS/CERO_EXPLICITO sin exclusiones anteriores; no se descartan filtros.");
        var external=prefix+"_EXT";var total=prefix+"_TOTAL";
        var vars=definitions.Where(v=>v.Codigo!=sourceCode).ToList();
        void Add(string code,string unit,string origin){RhNomNumeros.Codigo(code);if(definitions.Any(v=>v.Codigo==code)||vars.Any(v=>v.Codigo==code))throw new RhNomError("CODIGO_COMPUESTO_DUPLICADO","Use un prefijo nuevo para las variables compuestas.");vars.Add(new(0,0,code,"Grupo por etapa "+code,"NUMERO",unit,origin));}
        Add(external,"DINERO","ENTRADA");Add(total,"DINERO","CALCULADA");
        var parameters=(request.Parametros??[]).ToList();var terms=new List<string>();
        foreach(var term in plan.Salidas)
        {
            var weight=prefix+"_P_"+term.CodigoConcepto;Add(weight,"FACTOR","PARAMETRO");
            parameters.Add(new(weight,weight,RhNomNumeros.Texto(term.Multiplicidad),from,until,request.CodigoTipoNomina));
            terms.Add("["+term.VariableSalida+"]*["+weight+"]");
        }
        string Rewrite(string value)=>value.Replace("["+sourceCode+"]","["+total+"]",StringComparison.Ordinal);
        RhNomCondicion? Condition(RhNomCondicion? c)=>c is null?null:c with{Variable=c.Variable==sourceCode?total:c.Variable,VariableDerecha=c.VariableDerecha==sourceCode?total:c.VariableDerecha,Condiciones=c.Condiciones?.Select(x=>Condition(x)!).ToList()};
        var formulas=request.Formulas.Select(f=>f with{Expresion=Rewrite(f.Expresion),ExpresionAlternativa=f.ExpresionAlternativa is null?null:Rewrite(f.ExpresionAlternativa),Condicion=Condition(f.Condicion)}).ToList();
        formulas.Add(new(total,"["+external+"]"+(terms.Count==0?"":"+"+string.Join("+",terms))));
        var sources=original.FuentesActuales!.Where(s=>s.CodigoVariable!=sourceCode).ToList();sources.Add(source with{CodigoVariable=external,Acumulador=plan.Externos});
        var result=Bind(request with{Formulas=formulas,Parametros=parameters,FuentesActuales=sources},Catalog(vars));
        RhNomConfiguracion.Snapshot(result,Catalog(vars),company,conceptos:concepts);
        return new(vars,result);
    }
}
