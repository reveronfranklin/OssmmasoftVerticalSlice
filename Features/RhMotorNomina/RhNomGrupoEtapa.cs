namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public record RhNomSalidaGrupo(int CodigoConcepto,string VariableSalida);
public record RhNomTerminoGrupo(int CodigoConcepto,string VariableSalida,int Multiplicidad);
public record RhNomGrupoEtapa(string Etapa,RhNomAcumuladorActual Externos,List<RhNomTerminoGrupo> Salidas)
{
    public string VersionMetodo=>"GRUPO_ETAPA_V1";
}

// Migration-time partition only. Does not read Oracle or infer a processing stage.
// The composed model must declare the output variables and their explicit weights.
public static class RhNomGruposEtapa
{
    public static RhNomGrupoEtapa Preparar(RhNomGrupoAcum group,int company,int type,DateTime date,
        string stage,List<string> states,List<string> kinds,IReadOnlyList<RhNomSalidaGrupo> outputs)
    {
        if(stage is not ("PREVIA_BORRADOS" or "POSTERIOR_SALIDAS"))
            throw new RhNomError("ETAPA_GRUPO_INVALIDA","Declare etapa previa a borrados o posterior a salidas; no se infiere por concepto.");
        if(group is null||group.Conceptos.Count==0)
            throw new RhNomError("GRUPO_ETAPA_VACIO","La particion requiere un grupo capturado no vacio.");
        RhNomGruposAcum.ValidarUso(group,company,type,date);
        if(outputs is null||outputs.Count>50||outputs.Any(x=>x is null||x.CodigoConcepto<=0)
            ||outputs.Select(x=>x.CodigoConcepto).Distinct().Count()!=outputs.Count)
            throw new RhNomError("SALIDAS_GRUPO_INVALIDAS","Hasta 50 conceptos de salida positivos y unicos requeridos.");
        var normalized=outputs.Select(x=>x with{VariableSalida=RhNomNumeros.Codigo(x.VariableSalida)}).ToList();
        if(normalized.Select(x=>x.VariableSalida).Distinct(StringComparer.Ordinal).Count()!=normalized.Count)
            throw new RhNomError("SALIDAS_GRUPO_INVALIDAS","Cada concepto requiere una salida distinta.");
        var terms=stage=="PREVIA_BORRADOS"?new List<RhNomTerminoGrupo>():normalized
            .Where(x=>group.Conceptos.Contains(x.CodigoConcepto)).OrderBy(x=>x.CodigoConcepto)
            .Select(x=>new RhNomTerminoGrupo(x.CodigoConcepto,x.VariableSalida,group.Filas.Count(f=>f.CodigoConcepto==x.CodigoConcepto))).ToList();
        var filter=RhNomAcumuladores.Validar(new(group.Conceptos,states,kinds,"TODAS",null,"CERO_EXPLICITO",
            terms.Select(x=>x.CodigoConcepto).ToList(),group));
        return new(stage,filter,terms);
    }
}
