namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public record RhNomPhActualDef(RhNomIntegralPhDef Calculo,RhNomAgregadosPhDef Agregados,RhNomGrupoAcum? GrupoPh,
    List<string> EstadosGrupo,List<string> TiposGrupo,RhNomControlBinding Control);
public record RhNomPlantillaPhActual(List<RhNomVariableSave> Variables,RhNomModeloSave Modelo)
{
    public RhNomModeloSave Solicitud(int id,int expected,string description,IReadOnlyList<RhNomVariable> catalog)
    {
        if(Variables.Any(t=>catalog.Count(v=>v.Codigo==t.Codigo&&v.Tipo==t.Tipo&&v.Unidad==t.Unidad&&v.Origen==t.Origen&&v.Activa)!=1))throw new RhNomError("CATALOGO_PH_INCOMPLETO","Variables activas compatibles con la receta PH requeridas.");
        return Modelo with{ModeloId=id,VersionEsperada=expected,Descripcion=description,Variables=Variables.Select(t=>{var v=catalog.Single(v=>v.Codigo==t.Codigo);return new RhNomVariableRef(v.Id,v.Version);}).ToList()};
    }
}
public static class RhNomMigracionPhActual
{
    public const string Version="PH_ACTUAL_CFG_V1";
    public static RhNomPlantillaPhActual Crear(RhNomPhActualDef d)
    {
        if(d is null||d.Calculo is null)throw new RhNomError("PH_ACTUAL_INVALIDO","Calculo y fuentes PH explicitos requeridos.");
        var vars=RhNomMigracionIntegralPh.Variables.ToList();var synthetic=vars.Select((v,i)=>new RhNomVariable(i+1,v.Codigo,v.Descripcion,v.Tipo,v.Unidad,v.Origen,true,1)).ToList();var calc=d.Calculo;
        var request=RhNomMigracionIntegralPh.Crear(1,0,"Plantilla PH actual",synthetic,calc);var def=RhNomAgregadosPh.ValidarDefinicion(d.Agregados);
        if(def.GrupoSi is {} si)RhNomGruposAcum.ValidarUso(si,calc.Concepto.CodigoEmpresa,calc.Concepto.CodigoTipoNomina,si.FechaReferencia);
        RhNomControles.Validar(d.Control);if(d.Control.CodigoConcepto!=calc.Concepto.CodigoConcepto)throw new RhNomError("CONTROL_PH_DISTINTO","Control del concepto PH destino requerido.");
        var sources=RhNomAgregadosPh.Proveedores.Select(p=>new RhNomFuenteActualBinding(p.Value,p.Key,AgregadosPh:def)).ToList();sources.Add(new("MIG_PH_SAL","SUELDO_RELACION_ACTUAL"));sources.Add(new("MIG_PH_OMIT",RhNomControles.Proveedor,Control:d.Control,ValoresPermitidos:["0","1"]));
        vars.Add(new(0,0,"MIG_PH_SIG","Siglas PH del contexto","TEXTO","TEXTO","ENTRADA"));sources.Add(new("MIG_PH_SIG","SIGLAS_TIPO_NOMINA_ACTUAL",ValoresPermitidos:[calc.Siglas]));
        var formulas=request.Formulas.Select(f=>f.VariableSalida=="MIG_PH_TOTAL"?f with{Condicion=new("TODAS",Condiciones:[f.Condicion!,new("IGUAL","MIG_PH_SIG","TEXTO",[calc.Siglas])])}:f).ToList();
        if(d.GrupoPh is {} group)
        {
            RhNomGruposAcum.ValidarUso(group,calc.Concepto.CodigoEmpresa,calc.Concepto.CodigoTipoNomina,group.FechaReferencia);
            if(group.Codigo!="PH"||group.Conceptos.Contains(calc.Concepto.CodigoConcepto))throw new RhNomError("GRUPO_PH_INVALIDO","Grupo PH sin su propio descuento requerido.");
        }
        if(d.GrupoPh is {Conceptos.Count:>0} ph)sources.Add(new("MIG_PH_GRU","ACUMULADOR_MOV_ACTUAL",Acumulador:RhNomAcumuladores.Validar(new(ph.Conceptos,d.EstadosGrupo,d.TiposGrupo,"TODAS",null,"CERO_EXPLICITO",Grupo:ph))));
        else
        {
            vars=vars.Select(v=>v.Codigo=="MIG_PH_GRU"?v with{Codigo="MIG_PH_GRU_CERO",Origen="CALCULADA"}:v).ToList();formulas=formulas.Select(f=>f with{Expresion=f.Expresion.Replace("[MIG_PH_GRU]","[MIG_PH_GRU_CERO]")}).ToList();formulas.Add(new("MIG_PH_GRU_CERO","0"));
        }
        var catalog=vars.Select((v,i)=>new RhNomVariable(i+1,v.Codigo,v.Descripcion,v.Tipo,v.Unidad,v.Origen,true,1)).ToList();request=request with{Variables=catalog.Select(v=>new RhNomVariableRef(v.Id,v.Version)).ToList(),Formulas=formulas,FuentesActuales=sources,VersionFuentesCliente=RhNomFuentesModelo.Version};RhNomConfiguracion.Snapshot(request,catalog,calc.Concepto.CodigoEmpresa,conceptos:[calc.Concepto]);return new(vars,request);
    }
}
