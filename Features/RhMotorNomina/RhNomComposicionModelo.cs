namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public record RhNomBloqueModelo(List<RhNomVariableSave> Variables,RhNomModeloSave Modelo);
public static class RhNomComposicionModelo
{
    // Builds a migration template; persisted revisions remain immutable.
    public static RhNomPlantillaGrupoEtapa Unir(int company,IReadOnlyList<RhNomConceptoCfg> concepts,
        string description,IReadOnlyList<RhNomBloqueModelo> blocks)
    {
        if(blocks is null||blocks.Count is <1 or >8||blocks.Any(b=>b?.Modelo is null||b.Variables is null))
            throw new RhNomError("BLOQUES_MODELO_INVALIDOS","Declare 1..8 bloques explicitos de recalculo.");
        var type=blocks[0].Modelo.CodigoTipoNomina;
        List<RhNomVariable> Catalog(List<RhNomVariableSave> vars)=>vars.Select((v,i)=>new RhNomVariable(i+1,v.Codigo,v.Descripcion,v.Tipo,v.Unidad,v.Origen,v.Activa,1)).ToList();
        List<RhNomVariableRef> Refs(List<RhNomVariableSave> vars)=>Catalog(vars).Select(v=>new RhNomVariableRef(v.Id,v.Version)).ToList();
        var variables=new List<RhNomVariableSave>();var names=new HashSet<string>(StringComparer.Ordinal);
        foreach(var block in blocks)
        {
            var m=block.Modelo;
            if(m.CodigoTipoNomina!=type||m.Escenario!="RECALCULA_VARIACIONES"||!m.ValidarUnidades)
                throw new RhNomError("BLOQUE_MODELO_AJENO","Bloques con el mismo tipo, escenario de recalculo y unidades verificadas requeridos.");
            RhNomConfiguracion.Snapshot(m with{Variables=Refs(block.Variables)},Catalog(block.Variables),company,conceptos:concepts);
            foreach(var v in block.Variables)
            {
                if(!names.Add(RhNomNumeros.Codigo(v.Codigo)))throw new RhNomError("VARIABLE_BLOQUE_DUPLICADA","Los bloques requieren codigos de variable distintos; no se elige uno por orden.");
                variables.Add(v);
            }
        }
        var actions=blocks.SelectMany(b=>b.Modelo.AccionesControl??[]).ToList();
        var request=new RhNomModeloSave(1,0,type,"RECALCULA_VARIACIONES",description,Refs(variables),
            blocks.SelectMany(b=>b.Modelo.Formulas).ToList(),blocks.SelectMany(b=>b.Modelo.Parametros??[]).ToList(),true,
            RhNomCondiciones.Version,blocks.SelectMany(b=>b.Modelo.TablasTramos??[]).ToList(),RhNomTramos.Version,
            blocks.SelectMany(b=>b.Modelo.ReglasConcepto??[]).ToList(),RhNomReglasConcepto.Version,
            blocks.SelectMany(b=>b.Modelo.FuentesActuales??[]).ToList(),RhNomFuentesModelo.Version,
            actions.Count>0?actions:null,actions.Count>0?RhNomAccionesControl.Version:null);
        RhNomConfiguracion.Snapshot(request,Catalog(variables),company,conceptos:concepts);
        return new(variables,request);
    }
}
