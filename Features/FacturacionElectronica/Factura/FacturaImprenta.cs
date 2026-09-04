namespace OssmmasoftVerticalSlice.Features.FacturacionElectronica;

// T4.7 - los datos de la imprenta digital que exige el numeral 7.14, leidos
// DESDE CONFIGURACION y no de constantes.
//
// El numeral pide tres cosas impresas en cada documento: razon social y RIF de la
// imprenta digital autorizada, MAS la nomenclatura y fecha de la Providencia
// Administrativa de autorizacion.
//
// LOS DOS PRIMEROS EXISTEN HOY. El tercero NO, y no por un descuido: la
// providencia de autorizacion de Ossmmasoft no existe hasta que el SENIAT la
// emita, y eso es FA-1, una dependencia que ningun diseno elimina. Corren 30 dias
// habiles desde que se consignen los recaudos del Art. 26.
//
// Consecuencia de diseno, no impedimento: el dato es CONFIGURACION. El dia que
// llegue la providencia se escribe en appsettings y el sistema empieza a emitir
// documentos legalmente validos SIN RECOMPILAR. Si fuera una constante, ese dia
// habria que compilar, probar y desplegar para poner un texto.
//
// Mientras el dato falte, el documento sale marcado como de prueba. Eso no es una
// degradacion: es lo correcto. Un documento sin el 7.14 no es legalmente valido, y
// decir lo contrario en la base seria escribir una constancia falsa.
public static class FacturaImprenta
{
    // Las tres claves viven bajo settings, como el resto de la configuracion del
    // proyecto. Vacias por defecto en los dos ambientes.
    private const string ClaveRif = "settings:FedImprentaRif";
    private const string ClaveRazonSocial = "settings:FedImprentaRazonSocial";
    private const string ClaveProvidencia = "settings:FedImprentaProvidencia";

    public static FacturaImprentaDatos Leer(IConfiguration config) => new(
        (config[ClaveRif] ?? string.Empty).Trim(),
        (config[ClaveRazonSocial] ?? string.Empty).Trim(),
        (config[ClaveProvidencia] ?? string.Empty).Trim());

    // Explica en una frase por que un documento salio de prueba. Va al usuario y a
    // la bitacora: "de prueba" sin motivo es un estado que nadie sabe como salir.
    public static string MotivoDePrueba(FacturaImprentaDatos datos)
    {
        if (datos.EsDefinitivo)
        {
            return string.Empty;
        }

        var faltan = new List<string>();

        if (string.IsNullOrWhiteSpace(datos.Rif))
        {
            faltan.Add("el RIF de la imprenta digital");
        }

        if (string.IsNullOrWhiteSpace(datos.RazonSocial))
        {
            faltan.Add("su razón social");
        }

        if (string.IsNullOrWhiteSpace(datos.Providencia))
        {
            faltan.Add("la nomenclatura y fecha de la Providencia de autorización del SENIAT");
        }

        return "El documento se emite como PRUEBA porque el numeral 7.14 exige datos que aún no están configurados: "
               + string.Join(", ", faltan)
               + ". Hasta obtener la autorización del SENIAT, ningún documento es legalmente válido.";
    }
}
