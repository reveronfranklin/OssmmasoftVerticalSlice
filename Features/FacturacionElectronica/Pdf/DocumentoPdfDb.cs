using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.FacturacionElectronica;

// Fase 7 - lo que hace falta para IMPRIMIR un documento, que no es lo mismo que
// lo que hace falta para listarlo.
//
// El listado muestra ocho columnas; la representacion grafica tiene que llevar
// los quince numerales del Art. 7, y para eso necesita cosas que el listado
// nunca pidio: el domicilio fiscal del emisor (7.3), los datos de la imprenta
// (7.14), la fecha de asignacion del numero de control (7.15), el desglose por
// alicuota (7.11 y 7.12) y los renglones completos (7.8).
//
// Por eso hay un SQL propio y no se reusa SqlDocumentoGetAll: pedirle a la
// consulta del listado que traiga todo esto la volveria lenta para su uso real,
// que es paginar.

// La cabecera, tal como se imprime.
public record DocumentoImpresionCabecera(
    long Id,
    string TipoDocumento,
    string Denominacion,
    string Serie,
    string Numeracion,
    string NumeracionConSerie,
    string NumeroControl,
    string RangoNumerosControl,
    string FechaEmision8d,
    string HoraEmision,
    string FechaAsignacion8d,
    string EmisorRif,
    string EmisorRazonSocial,
    string EmisorDomicilio,
    string AdqNombre,
    string AdqRif,
    string AdqDocumentoId,
    decimal TotalExento,
    decimal TotalBase,
    decimal TotalIva,
    decimal TotalGeneral,
    string Moneda,
    decimal TasaCambio,
    decimal TotalMoneda,
    string ImprentaRif,
    string ImprentaRazonSocial,
    string ImprentaProvidencia,
    bool EsPrueba,

    // Propios de la nota (Art. 23 de la SNAT/2011/00071). Vacios en los demas.
    string NotaMotivo,
    string NotaOrigenNumeracion,
    string NotaOrigenFecha8d,
    decimal NotaOrigenTotal,
    string NotaOrigenMoneda,

    // Propios de la guia de despacho (Art. 10). Vacios en los demas.
    string GuiaMotivoTraslado,
    string GuiaDestino);

// Un renglon impreso. Los campos de medida solo los usa la guia.
public record DocumentoImpresionRenglon(
    int Orden,
    string Descripcion,
    string Codigo,
    decimal Cantidad,
    decimal Precio,
    decimal Alicuota,
    bool Exento,
    string BienesEntregados,
    string AjusteDescripcion,
    decimal AjusteValor,
    decimal TotalRenglon,
    string MedidaTipo,
    decimal MedidaValor,
    string MedidaUnidad);

// Una fila del desglose por alicuota. Arts. 7.11 y 7.12.
public record DocumentoImpresionImpuesto(decimal Alicuota, decimal BaseImponible, decimal MontoIva);

public record DocumentoImpresion(
    DocumentoImpresionCabecera Cabecera,
    List<DocumentoImpresionRenglon> Renglones,
    List<DocumentoImpresionImpuesto> Impuestos);

public static class DocumentoPdfDb
{
    // LEFT JOIN a las tres extensiones, no INNER. Un documento es factura, nota o
    // guia, nunca las tres, asi que dos de los tres bloques siempre vienen nulos.
    // Traerlos en una consulta y no en tres evita dos viajes por documento.
    public const string SqlDocumentoParaImprimir = @"
        SELECT d.ID, d.TIPO_DOCUMENTO, d.SERIE, d.NUMERACION, d.EMITIDO_EN,
               d.EMISOR_RIF, d.EMISOR_RAZON_SOCIAL, d.EMISOR_DOMICILIO,
               d.ADQ_NOMBRE, d.ADQ_RIF, d.ADQ_DOCUMENTO_ID,
               d.TOTAL_EXENTO, d.TOTAL_BASE, d.TOTAL_IVA, d.TOTAL_GENERAL,
               d.MONEDA, d.TASA_CAMBIO, d.TOTAL_MONEDA,
               d.IMPRENTA_RIF, d.IMPRENTA_RAZON_SOCIAL, d.IMPRENTA_PROVIDENCIA, d.ES_PRUEBA,
               COALESCE(nc.IDENTIFICADOR || '-' || LPAD(nc.SECUENCIAL::text, 8, '0'), '') AS NUMERO_CONTROL,
               nc.FECHA_ASIGNACION,
               COALESCE(n.MOTIVO, '')            AS NOTA_MOTIVO,
               COALESCE(n.ORIGEN_NUMERACION, '') AS NOTA_ORIGEN_NUMERACION,
               COALESCE(n.ORIGEN_FECHA_8D, '')   AS NOTA_ORIGEN_FECHA_8D,
               COALESCE(n.ORIGEN_TOTAL, 0)       AS NOTA_ORIGEN_TOTAL,
               COALESCE(n.ORIGEN_MONEDA, '')     AS NOTA_ORIGEN_MONEDA,
               COALESCE(g.MOTIVO_TRASLADO, '')   AS GUIA_MOTIVO_TRASLADO,
               COALESCE(g.DESTINO, '')           AS GUIA_DESTINO
        FROM FED.FED_DOCUMENTO d
        LEFT JOIN FED.FED_NUM_CONTROL    nc ON nc.DOCUMENTO_ID = d.ID
        LEFT JOIN FED.FED_NOTA           n  ON n.DOCUMENTO_ID  = d.ID
        LEFT JOIN FED.FED_GUIA_DESPACHO  g  ON g.DOCUMENTO_ID  = d.ID
        WHERE d.ID = @documento_id;";

    public const string SqlRenglonesParaImprimir = @"
        SELECT ORDEN, DESCRIPCION, COALESCE(CODIGO, '') AS CODIGO, CANTIDAD, PRECIO, ALICUOTA, EXENTO,
               COALESCE(BIENES_ENTREGADOS, '')  AS BIENES_ENTREGADOS,
               COALESCE(AJUSTE_DESCRIPCION, '') AS AJUSTE_DESCRIPCION,
               AJUSTE_VALOR, TOTAL_RENGLON,
               COALESCE(MEDIDA_TIPO, '')   AS MEDIDA_TIPO,
               COALESCE(MEDIDA_VALOR, 0)   AS MEDIDA_VALOR,
               COALESCE(MEDIDA_UNIDAD, '') AS MEDIDA_UNIDAD
        FROM FED.FED_DOCUMENTO_DETALLE
        WHERE DOCUMENTO_ID = @documento_id
        ORDER BY ORDEN;";

    public const string SqlImpuestosParaImprimir = @"
        SELECT ALICUOTA, BASE_IMPONIBLE, MONTO_IVA
        FROM FED.FED_DOC_IMPUESTO
        WHERE DOCUMENTO_ID = @documento_id
        ORDER BY ALICUOTA;";

    public static DocumentoImpresionCabecera MapCabecera(IDataReader reader)
    {
        DateTime emitidoEn = reader.GetDateTime(reader.GetOrdinal("emitido_en"));
        int ordinalAsignacion = reader.GetOrdinal("fecha_asignacion");

        DateTime fechaAsignacion = reader.IsDBNull(ordinalAsignacion)
            ? emitidoEn
            : reader.GetDateTime(ordinalAsignacion);

        string tipo = reader.SafeGetString("tipo_documento");
        string serie = reader.SafeGetString("serie");
        string numeracion = reader.SafeGetString("numeracion");
        string numeroControl = reader.SafeGetString("numero_control");

        return new DocumentoImpresionCabecera(
            reader.SafeGetInt64("id"),
            tipo,
            FacturaFormato.Denominacion(tipo),
            serie,
            numeracion,
            FacturaFormato.NumeracionConSerie(serie, numeracion),
            numeroControl,
            numeroControl.Length > 0 ? FacturaFormato.RangoNumerosControl(numeroControl) : string.Empty,
            FacturaFormato.FechaOchoDigitos(emitidoEn),
            FacturaFormato.HoraConMeridiano(emitidoEn),
            FacturaFormato.FechaOchoDigitos(fechaAsignacion),
            reader.SafeGetString("emisor_rif"),
            reader.SafeGetString("emisor_razon_social"),
            reader.SafeGetString("emisor_domicilio"),
            reader.SafeGetString("adq_nombre"),
            reader.SafeGetString("adq_rif"),
            reader.SafeGetString("adq_documento_id"),
            reader.SafeGetDecimal("total_exento"),
            reader.SafeGetDecimal("total_base"),
            reader.SafeGetDecimal("total_iva"),
            reader.SafeGetDecimal("total_general"),
            reader.SafeGetString("moneda"),
            reader.SafeGetDecimal("tasa_cambio"),
            reader.SafeGetDecimal("total_moneda"),
            reader.SafeGetString("imprenta_rif"),
            reader.SafeGetString("imprenta_razon_social"),
            reader.SafeGetString("imprenta_providencia"),
            reader.SafeGetBoolean("es_prueba"),
            reader.SafeGetString("nota_motivo"),
            reader.SafeGetString("nota_origen_numeracion"),
            reader.SafeGetString("nota_origen_fecha_8d"),
            reader.SafeGetDecimal("nota_origen_total"),
            reader.SafeGetString("nota_origen_moneda"),
            reader.SafeGetString("guia_motivo_traslado"),
            reader.SafeGetString("guia_destino"));
    }

    public static DocumentoImpresionRenglon MapRenglon(IDataReader reader) => new(
        reader.SafeGetInt32("orden"),
        reader.SafeGetString("descripcion"),
        reader.SafeGetString("codigo"),
        reader.SafeGetDecimal("cantidad"),
        reader.SafeGetDecimal("precio"),
        reader.SafeGetDecimal("alicuota"),
        reader.SafeGetBoolean("exento"),
        reader.SafeGetString("bienes_entregados"),
        reader.SafeGetString("ajuste_descripcion"),
        reader.SafeGetDecimal("ajuste_valor"),
        reader.SafeGetDecimal("total_renglon"),
        reader.SafeGetString("medida_tipo"),
        reader.SafeGetDecimal("medida_valor"),
        reader.SafeGetString("medida_unidad"));

    public static DocumentoImpresionImpuesto MapImpuesto(IDataReader reader) => new(
        reader.SafeGetDecimal("alicuota"),
        reader.SafeGetDecimal("base_imponible"),
        reader.SafeGetDecimal("monto_iva"));
}
