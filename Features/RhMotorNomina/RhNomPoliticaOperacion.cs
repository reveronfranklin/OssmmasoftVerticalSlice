namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

public static class RhNomPoliticaOperacion
{
    public static int[] TiposManuales(IConfiguration config)
    {
        var section=config.GetSection("RhMotorNomina:TiposNominaManuales");
        if(section.Value is not null)throw new RhNomError("POLITICA_MANUAL_INVALIDA","Tipos manuales debe ser una lista, no un valor escalar.");
        var children=section.GetChildren().Take(101).ToList();var values=new int[children.Count];
        for(var i=0;i<children.Count;i++)
        {
            if(children[i].Key!=i.ToString(System.Globalization.CultureInfo.InvariantCulture)||!int.TryParse(children[i].Value,System.Globalization.NumberStyles.None,System.Globalization.CultureInfo.InvariantCulture,out values[i]))
                throw new RhNomError("POLITICA_MANUAL_INVALIDA","Configuracion de tipos manuales invalida; operacion automatica bloqueada.");
        }
        if(values.Length>100||values.Any(v=>v<=0)||values.Distinct().Count()!=values.Length)throw new RhNomError("POLITICA_MANUAL_INVALIDA","Declare hasta 100 tipos manuales positivos distintos.");
        return values.Order().ToArray();
    }
    public static void PermitirAutomatica(IConfiguration config,int type)
    {
        if(type<=0)throw new RhNomError("TIPO_NOMINA_REQUERIDO","Tipo positivo requerido.");
        if(TiposManuales(config).Contains(type))throw new RhNomError("TIPO_NOMINA_MANUAL","El tipo conserva sus importes manuales; no se permite publicacion ni aplicacion automatica.");
    }
}
