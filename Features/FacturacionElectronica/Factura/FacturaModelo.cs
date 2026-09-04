namespace OssmmasoftVerticalSlice.Features.FacturacionElectronica;

// Formas compartidas de la emision de un documento fiscal.
//
// Van en su propio archivo, dentro de Factura/, y no en el archivo de la
// operacion como manda el estandar para un request comun. La razon es que aca no
// hay un solo consumidor: el validador, el calculo de totales y el handler de
// emision necesitan la misma forma, y ponerla en uno de ellos obligaria a los
// otros dos a depender del archivo de una operacion.
//
// La subcarpeta sigue el patron dominante del repo para features grandes:
// MotorFormularios se parte en Catalogo, Definicion, Motor, Reportes y
// Respuestas, y hasta tiene un Shared por subcarpeta. Contabilidad y Support
// hacen lo mismo. El namespace se mantiene plano al nivel de la feature, tal como
// en esos modulos.

// Un renglon del documento. Arts. 7.8, 7.9 y 7.10.
public record FacturaRenglonCommand(
    string Descripcion,
    decimal Cantidad,
    decimal Precio,
    decimal Alicuota,
    bool Exento = false,
    string Codigo = "",
    string BienesEntregados = "",
    string AjusteDescripcion = "",
    decimal AjusteValor = 0);

// La solicitud de emision.
//
// No lleva numeracion: la genera el sistema (D-21). Un emisor en modo 'externa'
// la trae, y para ese caso esta NumeracionExterna, que el handler solo acepta si
// el emisor esta declarado en ese modo.
//
// Tampoco lleva los datos del emisor ni los de la imprenta: los primeros salen de
// FED_EMISOR y los segundos de configuracion (T4.7). Pedirlos en el request seria
// dejar que quien llama decida que se estampa en un documento fiscal.
public record FacturaEmitirCommand(
    long EmisorId,
    string TipoDocumento,
    List<FacturaRenglonCommand> Renglones,
    string Serie = "",
    string NumeracionExterna = "",
    string AdqNombre = "",
    string AdqRif = "",
    string AdqDocumentoId = "",
    string UsuarioIns = "",
    string ClaveIdempotencia = "");

// Resultado de validar. Falla con la lista de numerales incumplidos, no con el
// primero: quien corrige el documento necesita ver todo lo que le falta, no
// descubrirlo de a uno por intento.
public record FacturaValidacion(bool EsValida, List<string> Faltantes)
{
    public string Mensaje => EsValida
        ? string.Empty
        : "El documento no cumple el Artículo 7: " + string.Join(" ", Faltantes);
}

// Totales calculados, listos para persistir. Uno por alicuota mas los agregados.
public record FacturaTotales(
    decimal TotalExento,
    decimal TotalBase,
    decimal TotalIva,
    decimal TotalGeneral,
    List<FacturaTotalPorAlicuota> PorAlicuota,
    List<decimal> RenglonTotales);

public record FacturaTotalPorAlicuota(decimal Alicuota, decimal BaseImponible, decimal MontoIva);

// Datos de la imprenta digital que exige el numeral 7.14.
public record FacturaImprentaDatos(string Rif, string RazonSocial, string Providencia)
{
    // Sin la nomenclatura y fecha de la Providencia de autorizacion, el documento
    // no es legalmente valido: el 7.14 la exige impresa. Hasta que el SENIAT
    // autorice a Ossmmasoft ese dato no existe, y el documento sale de prueba.
    public bool EsDefinitivo =>
        !string.IsNullOrWhiteSpace(Rif)
        && !string.IsNullOrWhiteSpace(RazonSocial)
        && !string.IsNullOrWhiteSpace(Providencia);
}
