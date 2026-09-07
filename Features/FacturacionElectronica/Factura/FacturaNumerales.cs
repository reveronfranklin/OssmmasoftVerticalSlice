namespace OssmmasoftVerticalSlice.Features.FacturacionElectronica;

// T5.9 - el numeral deja de ser parte del mensaje y pasa a ser dato (D-35).
//
// EL PROBLEMA QUE RESUELVE. El validador tenia el codigo del numeral SOLDADO
// dentro del texto: "7.7: falta el RIF del adquiriente...". Mientras el numeral
// vive dentro de la cadena, validar contra otro articulo obliga a duplicar la
// cadena, y duplicar la cadena obliga a duplicar la condicion. Con dieciseis
// condiciones eso son dieciseis copias que se separan la primera vez que alguien
// corrige una sola.
//
// POR QUE HAY TRES ARTICULOS. El Art. 7 de la Providencia SNAT/2024/000102 rige
// la factura, que es lo que emitimos como Rol B. Pero el Art. 8 de esa misma
// norma manda que las notas cumplan la SNAT/2011/00071, cuyo Art. 23 remite al
// Art. 13 -contribuyentes ordinarios del IVA- o al Art. 15 -no ordinarios-
// "segun sea el caso". Son tres conjuntos de numerales para el mismo contenido.
//
// Y ES EL MISMO CONTENIDO: el Art. 7 de la 102 es el Art. 13 de la 00071 puesto
// al dia para medios digitales. Cambia el soporte y quien asigna el numero de
// control, no lo que el documento tiene que decir. La equivalencia numeral por
// numeral esta escrita en MarcoNormativo.md seccion 3.1; aca solo se codifica.
public static class FacturaNumerales
{
    // Los conceptos que el validador comprueba. Se nombran por LO QUE VERIFICAN
    // y no por su numeral, que es justamente lo que los hace reusables entre
    // articulos.
    public enum Concepto
    {
        TipoDocumento,
        AdquirienteIdentificado,
        AdquirienteNombrado,
        RenglonesPresentes,
        RenglonDescripcion,
        RenglonPrecio,
        RenglonCantidad,
        ExentoConAlicuota,
        AlicuotaFueraDeRango,
        AjusteSinDescripcion,
        MonedaSinTasa
    }

    // Los tres articulos aplicables.
    public const string Art7 = "7";
    public const string Art13 = "13";
    public const string Art15 = "15";

    // El mapa. Una fila por concepto, una columna por articulo.
    //
    // Cadena vacia significa QUE ESE ARTICULO NO TIENE UN NUMERAL PARA ESE
    // CONCEPTO, y no que no haya que verificarlo: el Art. 15 no discrimina IVA
    // porque rige a quien no es contribuyente ordinario, pero la coherencia entre
    // "exento" y una alicuota mayor que cero sigue siendo un dato incoherente que
    // no debe entrar. En ese caso el mensaje cita el articulo sin numeral.
    //
    // [Interpretacion] Que un contribuyente formal no deberia cobrar IVA en
    // absoluto se sigue del Art. 15 -no tiene numerales de base imponible ni de
    // impuesto-, pero la norma no lo prohibe con esas palabras y este modulo no
    // lo rechaza: seria inventar una obligacion. Queda declarado como insumo de
    // negocio, no resuelto en silencio.
    private static readonly Dictionary<Concepto, (string Art7, string Art13, string Art15)> Mapa = new()
    {
        [Concepto.TipoDocumento]           = ("7.1",  "13.1",  "15.1"),
        [Concepto.AdquirienteIdentificado] = ("7.7",  "13.7",  "15.8"),
        [Concepto.AdquirienteNombrado]     = ("7.7",  "13.7",  "15.8"),
        [Concepto.RenglonesPresentes]      = ("7.8",  "13.8",  "15.9"),
        [Concepto.RenglonDescripcion]      = ("7.8",  "13.8",  "15.9"),
        [Concepto.RenglonPrecio]           = ("7.8",  "13.8",  "15.9"),
        [Concepto.RenglonCantidad]         = ("7.8",  "13.8",  "15.9"),
        [Concepto.ExentoConAlicuota]       = ("7.11", "13.10", ""),
        [Concepto.AlicuotaFueraDeRango]    = ("7.11", "13.10", ""),
        [Concepto.AjusteSinDescripcion]    = ("7.10", "13.9",  "15.10"),
        [Concepto.MonedaSinTasa]           = ("",     "13.14", "15.11")
    };

    // El numeral de un concepto para el articulo que aplica. Cadena vacia cuando
    // ese articulo no lo enumera.
    public static string Numeral(Concepto concepto, string articulo)
    {
        if (!Mapa.TryGetValue(concepto, out var fila))
        {
            return string.Empty;
        }

        return articulo switch
        {
            Art13 => fila.Art13,
            Art15 => fila.Art15,
            _     => fila.Art7
        };
    }

    // Que articulo aplica segun el tipo de contribuyente del emisor (D-31). El
    // Art. 23 de la 00071 dice "segun sea el caso", y el caso es este.
    public static string ArticuloParaNota(string tipoContribuyente) =>
        tipoContribuyente == "ordinario" ? Art13 : Art15;

    // El nombre de la norma, para que el mensaje diga cual se incumple.
    public static string Norma(string articulo) => articulo switch
    {
        Art13 or Art15 => "Providencia SNAT/2011/00071",
        _              => "Providencia SNAT/2024/000102"
    };
}
