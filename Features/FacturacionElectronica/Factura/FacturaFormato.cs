using System.Globalization;

namespace OssmmasoftVerticalSlice.Features.FacturacionElectronica;

// T4.6 - los formatos que la norma fija LITERALMENTE.
//
// Estos no son criterios de presentacion: son requisitos de contenido. El Art. 7
// dice como tiene que verse cada uno, y un documento que los incumple es un
// documento que incumple la Providencia. Por eso viven en un solo lugar y no
// dispersos en la vista, el PDF y el correo, que es como tres formatos distintos
// terminan en tres pantallas distintas.
//
// Todo lo de aca es puro: entra un dato, sale una cadena. Sin base, sin
// configuracion y sin fecha del sistema, para que se pueda comprobar.
public static class FacturaFormato
{
    // Cultura invariante a proposito: la norma fija los separadores -punto en la
    // hora, parentesis en la marca-, no la cultura del servidor. Con es-VE o en-US
    // el resultado tiene que ser el mismo.
    private static readonly CultureInfo Invariante = CultureInfo.InvariantCulture;

    // Art. 7.6 - fecha de emision "en ocho digitos, formato DDMMAAAA, que pueden
    // ir separados". Se emite sin separadores: ocho digitos es lo unico que la
    // norma exige siempre, y agregarlos es opcional.
    public static string FechaOchoDigitos(DateTime fecha) =>
        fecha.ToString("ddMMyyyy", Invariante);

    // Art. 7.6 - hora "en formato HH.MM.SS, indicando a.m. o p.m."
    //
    // Los separadores son PUNTOS, no dos puntos, y la indicacion a.m./p.m. obliga
    // a reloj de doce horas. Es el detalle que se escribe mal por costumbre: casi
    // todo el mundo formatea HH:mm:ss.
    public static string HoraConMeridiano(DateTime fecha)
    {
        string hora = fecha.ToString("hh.mm.ss", Invariante);
        string meridiano = fecha.Hour < 12 ? "a.m." : "p.m.";

        return $"{hora} {meridiano}";
    }

    // Art. 7.8 - "el caracter E separado por un espacio en blanco y entre
    // parentesis": (E). Va junto a la descripcion o al precio del renglon exento,
    // exonerado o no gravado.
    //
    // El dato guardado es el hecho -EXENTO boolean-; el literal se arma aca. Al
    // reves -guardar "(E)" en la descripcion- haria imposible filtrar por exento
    // sin buscar una cadena, y ademas ensuciaria el dato con su presentacion.
    public const string MarcaExento = "(E)";

    public static string ConMarcaExento(string texto, bool exento) =>
        exento ? $"{texto} {MarcaExento}" : texto;

    // Art. 7.5 - "total de los numeros de control asignados, expresado desde el
    // N ... hasta el N ...".
    //
    // La decision D-1 fija asignacion POR DOCUMENTO y no por lote, asi que en la
    // practica el mismo numero es el inicio y el fin. La redaccion del numeral se
    // satisface igual: un rango de uno sigue siendo un rango.
    public static string RangoNumerosControl(string numeroDesde, string numeroHasta) =>
        $"desde el N° {numeroDesde} hasta el N° {numeroHasta}";

    public static string RangoNumerosControl(string numeroUnico) =>
        RangoNumerosControl(numeroUnico, numeroUnico);

    // Art. 13 - cuando el emisor carece de un sistema de facturacion centralizado,
    // la numeracion va "precedida de la palabra serie" seguida de los caracteres
    // que la identifiquen.
    //
    // Sin serie devuelve la numeracion sola: la palabra "serie" solo aparece
    // cuando hay una, y agregarla siempre seria inventar un requisito.
    public static string NumeracionConSerie(string serie, string numeracion) =>
        string.IsNullOrWhiteSpace(serie)
            ? numeracion
            : $"serie {serie.Trim()} {numeracion}";

    // Art. 12 - todos los documentos deben "indicar que se emiten conforme a lo
    // dispuesto en esta Providencia Administrativa". Aplica a DOC-1 a DOC-4 sin
    // excepcion (RF-B.4.1), no es un caso especial.
    public const string LeyendaProvidencia =
        "Emitida conforme a lo dispuesto en la Providencia Administrativa SNAT/2024/000102";

    // Art. 10.3 - la nota de entrega debe llevar esta expresion literal.
    public const string LeyendaSinCreditoFiscal = "sin derecho a Crédito Fiscal";

    // Art. 7.11 - la base imponible se discrimina "indicando el porcentaje
    // aplicable". El porcentaje se muestra tal como se aplico.
    public static string PorcentajeAlicuota(decimal alicuota) =>
        alicuota.ToString("0.##", Invariante) + " %";

    // Denominacion del documento. Los literales los fija la norma y no se
    // traducen ni se adornan: Art. 7.1 para la factura, Art. 10.1 para la nota de
    // entrega -que legalmente NO se llama nota de entrega-.
    public static string Denominacion(string tipoDocumento) => tipoDocumento switch
    {
        "factura" => "FACTURA",
        "debito"  => "NOTA DE DÉBITO",
        "credito" => "NOTA DE CRÉDITO",

        // Art. 10.1: "orden de entrega" o "guia de despacho", segun corresponda.
        // "Nota de entrega" NO es una denominacion valida bajo la Providencia 102,
        // aunque el negocio la llame asi puertas adentro.
        "entrega" => "GUÍA DE DESPACHO",
        _         => tipoDocumento.ToUpperInvariant()
    };
}
