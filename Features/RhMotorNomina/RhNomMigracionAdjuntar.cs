namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

// Source-derived migration recipe. Runtime calculations use only the persisted
// graph/parameters/providers, never this class or the original Oracle package.
public record RhNomAdjuntarRol(string Rol,RhNomConceptoCfg Concepto,bool BasePorGrupo,List<string> Grupos);
public record RhNomAdjuntarFactores(string BaseGrupo,string Anual,string Semanas,string Divisor,string Decimales,string PorcentajePH,int DiaIso);
public record RhNomAdjuntarDef(int Tipo,string Siglas,List<RhNomAdjuntarRol> Roles,List<RhNomGrupoAcum> Grupos,
    List<RhNomFrecuenciaSueldo> Frecuencias,RhNomAdjuntarFactores Factores,DateOnly Desde,DateOnly? Hasta,
    string PoliticaParametros,List<string> EstadosMovimiento,List<string> TiposMovimiento,bool UsaSuplencia=false);
public record RhNomPlantillaAdjuntar(int Empresa,int Tipo,List<RhNomVariableSave> Variables,List<RhNomFormula> Formulas,
    List<RhNomParametro> Parametros,List<RhNomTablaTramos> Tablas,List<RhNomReglaConceptoSave> Reglas,List<RhNomFuenteActualBinding> Fuentes)
{
    public RhNomModeloSave Solicitud(int model,int expected,string description,IReadOnlyList<RhNomVariable> catalog)
    {
        if(Variables.Any(t=>catalog.Count(v=>v.Codigo==t.Codigo&&v.Tipo==t.Tipo&&v.Unidad==t.Unidad&&v.Origen==t.Origen&&v.Activa)!=1))
            throw new RhNomError("CATALOGO_MIG_INCOMPLETO","Faltan variables activas con tipo/unidad/origen de la plantilla.");
        var chosen=Variables.Select(t=>catalog.Single(v=>v.Codigo==t.Codigo)).ToList();
        return new(model,expected,Tipo,"ADJUNTAR_CARGO",description,chosen.Select(v=>new RhNomVariableRef(v.Id,v.Version)).ToList(),Formulas,Parametros,true,RhNomCondiciones.Version,Tablas,RhNomTramos.Version,Reglas,RhNomReglasConcepto.Version,Fuentes,RhNomFuentesModelo.Version);
    }
}
public static class RhNomMigracionAdjuntar
{
    public const string Version="ADJUNTAR_CFG_V1";
    public static RhNomPlantillaAdjuntar Crear(RhNomAdjuntarDef d,int company)
    {
        if(d is null||company<=0||d.Tipo<=0||d.Roles is null||d.Roles.Count is <1 or >6||d.Roles.Any(r=>r is null||r.Concepto is null||r.Grupos is null)
            ||d.Roles.Select(r=>r.Rol).Distinct().Count()!=d.Roles.Count||d.Roles.Select(r=>r.Concepto.CodigoConcepto).Distinct().Count()!=d.Roles.Count
            ||!d.Roles.Any(r=>r.Rol=="SS")||d.Roles.Any(r=>r.Rol is not ("SS" or "SSC" or "SSO" or "PF" or "PH" or "FPS")))
            throw new RhNomError("ROLES_MIG_INVALIDOS","Declare SS y hasta seis roles distintos con conceptos unicos de la migracion.");
        var siglas=RhNomValores.Normalizar("TEXTO",d.Siglas);RhNomParametros.Fecha(d.Desde);
        if(d.Hasta is {} until){RhNomParametros.Fecha(until);if(until<d.Desde)throw new RhNomError("VIGENCIA_INVALIDA","Vigencia de migracion invalida.");}
        if(d.Grupos is null||d.Grupos.Count>10||d.Grupos.Any(g=>g is null)||d.Grupos.Select(g=>g.TipoAcumuladoId).Distinct().Count()!=d.Grupos.Count)
            throw new RhNomError("GRUPOS_MIG_INVALIDOS","Hasta diez grupos capturados distintos por plantilla.");
        foreach(var group in d.Grupos)RhNomGruposAcum.ValidarUso(group,company,d.Tipo,group.FechaReferencia);
        foreach(var role in d.Roles)
        {
            if(role.Concepto.CodigoEmpresa!=company||role.Concepto.CodigoTipoNomina!=d.Tipo||!role.Concepto.Elegible||role.Concepto.TipoConcepto!=(role.Rol is "SS" or "SSC"?"A":"D"))throw new RhNomError("CONCEPTO_MIG_INVALIDO","Concepto activo/automatico de empresa/tipo y signo de rol requerido.");
            if(role.Grupos.Count>5||role.Grupos.Distinct().Count()!=role.Grupos.Count||role.Grupos.Any(g=>g is not ("SSO" or "PF" or "PH" or "FJP" or "FPS"))||!role.BasePorGrupo&&role.Grupos.Count>0||role.Rol is "SS" or "SSC"&&role.BasePorGrupo)
                throw new RhNomError("BASE_MIG_INVALIDA","Defina sueldo o grupos explicitos para cada descuento.");
            if(d.PoliticaParametros is not ("HASTA_NULA" or "FECHA_CONTEXTO"))throw new RhNomError("VIGENCIA_PARAM_INVALIDA","Politica de parametros requerida.");
        }
        var f=d.Factores??throw new RhNomError("FACTORES_MIG_REQUERIDOS","Factores explicitos requeridos.");
        foreach(var value in new[]{f.BaseGrupo,f.Anual,f.Semanas,f.Divisor})if(RhNomNumeros.Leer(value)<=0)throw new RhNomError("FACTOR_MIG_INVALIDO","Factores/divisores deben ser positivos.");
        var scale=RhNomNumeros.Leer(f.Decimales);if(scale!=decimal.Truncate(scale)||scale is < -28 or >28||f.DiaIso is <1 or >7)throw new RhNomError("CALENDARIO_MIG_INVALIDO","Escala entera -28..28 y dia ISO 1..7 requeridos.");
        RhNomNumeros.Leer(f.PorcentajePH);
        if(d.Frecuencias is null||d.Frecuencias.Count is <1 or >50||d.Frecuencias.Any(v=>v is null||v.FrecuenciaId<=0||v.FrecuenciaId==int.MaxValue||RhNomNumeros.Leer(v.Numerador)<=0||RhNomNumeros.Leer(v.Denominador)<=0)||d.Frecuencias.Select(v=>v.FrecuenciaId).Distinct().Count()!=d.Frecuencias.Count)throw new RhNomError("FRECUENCIAS_MIG_INVALIDAS","Frecuencias distintas y factores de sueldo positivos requeridos.");
        var vars=new Dictionary<string,RhNomVariableSave>(StringComparer.Ordinal);var formulas=new List<RhNomFormula>();var parameters=new List<RhNomParametro>();var sources=new List<RhNomFuenteActualBinding>();var rules=new List<RhNomReglaConceptoSave>();
        string Code(string suffix)=>"MIG_ADJ_"+suffix;
        string Token(string suffix)=>"["+Code(suffix)+"]";
        void Var(string suffix,string unit,string origin,string type="NUMERO") {var code=Code(suffix);var value=new RhNomVariableSave(0,0,code,"Migracion diagnostica adjuntar "+suffix,type,unit,origin);if(vars.TryGetValue(code,out var prior)&&prior!=value)throw new RhNomError("VARIABLE_MIG_DUPLICADA","Variables incompatibles en plantilla.");vars[code]=value;}
        void Formula(string suffix,string expression,RhNomCondicion? condition=null,string? alternative=null){Var(suffix,"DINERO","CALCULADA");formulas.Add(new(Code(suffix),expression,condition,alternative));}
        void Param(string suffix,string unit,string value){Var(suffix,unit,"PARAMETRO");parameters.Add(new(Code("P_"+suffix),Code(suffix),value,d.Desde,d.Hasta,d.Tipo));}
        void Input(string suffix,string unit,string provider,RhNomParametroConceptoBinding? param=null,int? iso=null){Var(suffix,unit,"ENTRADA",unit=="TEXTO"?"TEXTO":"NUMERO");sources.Add(new(Code(suffix),provider,DiaSemanaIso:iso,ParametroConcepto:param));}
        RhNomCondicion ContextCondition()=>new("IGUAL",Code("SIGLAS"),"TEXTO",[siglas]);
        var owned=d.Roles.ToDictionary(r=>r.Concepto.CodigoConcepto,r=>Code(r.Rol));
        Input("MES","DINERO","SUELDO_RELACION_ACTUAL");Input("FREC","NUMERO","FRECUENCIA_PAGO_ACTUAL");Input("SIGLAS","TEXTO","SIGLAS_TIPO_NOMINA_ACTUAL");sources[^1]=sources[^1] with{ValoresPermitidos=[siglas]};
        Param("DEC","NUMERO",f.Decimales);Var("SAL_NUM","NUMERO","PARAMETRO");Var("SAL_DEN","NUMERO","PARAMETRO");
        Formula("SAL_PER",$"ROUND({Token("MES")}*{Token("SAL_NUM")}/{Token("SAL_DEN")},{Token("DEC")})");
        var suplencia=d.UsaSuplencia||d.Roles.Any(r=>r.Rol=="SSC");if(suplencia){Input("SUPL","NUMERO","EXTRA1_RELACION_ACTUAL");sources[^1]=sources[^1] with{ValorSiNulo="0",ValoresPermitidos=d.Roles.Any(r=>r.Rol=="SSC")?null:["0"]};}
        foreach(var role in d.Roles.Where(r=>r.Rol is "SS" or "SSC"))
        {
            var condition=suplencia?new RhNomCondicion("TODAS",Condiciones:[ContextCondition(),new(role.Rol=="SS"?"IGUAL":"DISTINTO",Code("SUPL"),"NUMERO",["0"])]):ContextCondition();
            Formula(role.Rol,Token("SAL_PER"),condition,"0");
        }
        var requested=d.Roles.Where(r=>r.BasePorGrupo).SelectMany(r=>r.Grupos).ToHashSet(StringComparer.Ordinal);
        var groups=d.Grupos.Where(g=>requested.Contains(g.Codigo)).OrderBy(g=>g.TipoAcumuladoId).ToList();
        foreach(var group in groups)
        {
            var suffix="G_"+group.TipoAcumuladoId;var own=new List<string>();
            foreach(var id in group.Conceptos.Where(owned.ContainsKey).Order())
            {
                var multiplicity=group.Filas.Count(r=>r.CodigoConcepto==id);
                if(multiplicity>1){var factor="MULT_"+group.TipoAcumuladoId+"_"+id;var output="PESO_"+group.TipoAcumuladoId+"_"+id;Param(factor,"FACTOR",RhNomNumeros.Texto(multiplicity));Formula(output,"["+owned[id]+"]*"+Token(factor));own.Add(Code(output));}else own.Add(owned[id]);
            }
            if(group.Conceptos.Count==0){Formula(suffix,"0");continue;}
            var external="EXT_"+group.TipoAcumuladoId;Var(external,"DINERO","ENTRADA");
            var filter=RhNomAcumuladores.Validar(new(group.Conceptos,d.EstadosMovimiento,d.TiposMovimiento,"TODAS",null,"CERO_EXPLICITO",group.Conceptos.Where(owned.ContainsKey).ToList(),group));
            sources.Add(new(Code(external),"ACUMULADOR_MOV_ACTUAL",Acumulador:filter));
            Formula(suffix,Token(external)+(own.Count==0?"":"+SUMA_SALIDAS("+string.Join(",",own.Select(v=>"["+v+"]"))+")"));
        }
        if(d.Roles.Any(r=>r.BasePorGrupo))Param("FACTOR_BASE","FACTOR",f.BaseGrupo);
        if(d.Roles.Any(r=>r.Rol is "SSO" or "PF")){Param("ANUAL","FACTOR",f.Anual);Param("SEMANAS","CANTIDAD",f.Semanas);Input("OCURRENCIAS","CANTIDAD","DIA_ISO_MES_NOMINA",iso:f.DiaIso);}
        foreach(var role in d.Roles.Where(r=>r.Rol is not ("SS" or "SSC")))
        {
            var name=role.Rol;var basis="BASE_"+name;
            if(role.BasePorGrupo)
            {
                var members=groups.Where(g=>role.Grupos.Contains(g.Codigo)).Select(g=>Token("G_"+g.TipoAcumuladoId)).ToList();
                var total="ACUM_"+name;Formula(total,members.Count==0?"0":"SUMA_SALIDAS("+string.Join(",",members)+")*"+Token("FACTOR_BASE"));
                Formula(basis,Token(total),new("DISTINTO",Code(total),"DINERO",["0"]),Token("MES"));
            }
            else Formula(basis,Token("MES"));
            Param("DIV_"+name,"FACTOR",f.Divisor);
            var monthly="MES_"+name;
            if(name=="PH")
            {
                Param("PORC_PH","PORCENTAJE",f.PorcentajePH);
                Formula(monthly,$"-ROUND({Token(basis)}*TASA({Token("PORC_PH")}),{Token("DEC")})");
                Formula(name,$"TRUNC({Token(monthly)}/{Token("DIV_"+name)},{Token("DEC")})",ContextCondition(),"0");
            }
            else
            {
                Input("PORC_"+name,"PORCENTAJE","PARAM_CON_PORCENTAJE",new(role.Concepto.CodigoConcepto,d.PoliticaParametros));
                if(name is "SSO" or "PF")
                {
                    Input("TOPE_"+name,"DINERO","PARAM_CON_TOPE",new(role.Concepto.CodigoConcepto,d.PoliticaParametros));
                    Formula(monthly,$"-MIN({Token(basis)}*TASA({Token("PORC_"+name)}),{Token("TOPE_"+name)}*TASA({Token("PORC_"+name)}))");
                    var prorated="PROR_"+name;Formula(prorated,$"ROUND({Token(monthly)}*{Token("ANUAL")}/{Token("SEMANAS")}*{Token("OCURRENCIAS")},{Token("DEC")})");
                    Formula(name,$"{(name=="SSO"?"ROUND":"TRUNC")}({Token(prorated)}/{Token("DIV_"+name)},{Token("DEC")})",ContextCondition(),"0");
                }
                else {Formula(monthly,$"-{Token(basis)}*TASA({Token("PORC_"+name)})");Formula(name,$"ROUND({Token(monthly)}/{Token("DIV_"+name)},{Token("DEC")})",ContextCondition(),"0");}
            }
        }
        foreach(var role in d.Roles)rules.Add(new(Code("R_"+role.Rol),Code(role.Rol),role.Concepto.CodigoConcepto,role.Concepto.Huella,"F","CONSERVAR_FIRMADO","REGISTRAR_CERO","CONSERVAR_EXISTENTE"));
        RhNomTablaTramos Table(string suffix,bool numerator)=>new(Code("T_"+suffix),Code(suffix),Code("FREC"),"NUMERO",d.Desde,d.Hasta,d.Tipo,d.Frecuencias.OrderBy(v=>v.FrecuenciaId).Select(v=>new RhNomTramo("F_"+v.FrecuenciaId,RhNomNumeros.Texto(v.FrecuenciaId),RhNomNumeros.Texto((long)v.FrecuenciaId+1),numerator?v.Numerador:v.Denominador)).ToList());
        var result=new RhNomPlantillaAdjuntar(company,d.Tipo,vars.Values.OrderBy(v=>v.Codigo,StringComparer.Ordinal).ToList(),formulas,parameters,[Table("SAL_NUM",true),Table("SAL_DEN",false)],rules,sources);
        // Validate before writing even the variable catalog: graph, units, schemas and policy.
        var synthetic=result.Variables.Select((v,i)=>new RhNomVariable(i+1,v.Codigo,v.Descripcion,v.Tipo,v.Unidad,v.Origen,true,1)).ToList();
        RhNomConfiguracion.Snapshot(result.Solicitud(1,0,"Plantilla diagnostica",synthetic),synthetic,company,conceptos:d.Roles.Select(r=>r.Concepto).ToList());
        return result;
    }
}
