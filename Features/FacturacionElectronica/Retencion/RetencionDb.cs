using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.FacturacionElectronica;

// Un documento retenido dentro del comprobante. Numerales 5, 6 y 8 del Art. 11.
public record RetencionDocumentoCommand(
    string DocumentoNumero,
    string DocumentoControl,
    DateTime DocumentoFecha,
    decimal MontoTotal,
    decimal BaseImponible,
    decimal ImpuestoCausado,
    decimal MontoRetenido,
    decimal Porcentaje = 0);

// Lo que devuelve la emision de un comprobante.
//
// NO tiene NumeroControl, y esa ausencia es el rasgo que define a este
// documento: el Art. 11 no remite a los numerales 4 y 5 del Art. 7. Lo que
// aparece es NumeroControl DEL DOCUMENTO RETENIDO, dentro de cada fila del
// detalle (numeral 11.5).
public record RetencionEmitidaResponse(
    long RetencionId,
    string Numeracion,
    string Periodo,
    string FechaEmision8d,
    string HoraEmision,
    string AgenteRif,
    string AgenteRazonSocial,
    string ProveedorRif,
    string ProveedorRazonSocial,
    decimal TotalDocumentos,
    decimal TotalBase,
    decimal TotalImpuesto,
    decimal TotalRetenido,
    int CantidadDocumentos,
    bool EsPrueba,
    string MotivoPrueba,
    bool YaExistia);

// Totales del comprobante, agregados del detalle.
public record RetencionTotales(
    decimal TotalDocumentos,
    decimal TotalBase,
    decimal TotalImpuesto,
    decimal TotalRetenido);

// SQL del subdominio de comprobantes de retencion. Fase 6B.
public static class RetencionDb
{
    // El contador del secuencial, bloqueado. Mismo idioma que los otros dos del
    // modulo: INSERT ... ON CONFLICT DO UPDATE con un no-op, que crea la fila si
    // no existe y la bloquea si existe, en una sola instruccion.
    //
    // DO NOTHING no sirve: no devuelve la fila cuando ya existia, y esa carrera
    // ya se pago una vez en la Fase 2.
    //
    // El no-op va sobre FECHA_UPD y NO sobre la clave: el rol de aplicacion tiene
    // UPDATE de columna solo sobre ULTIMO_NUMERO y FECHA_UPD, porque mover un
    // contador de agente o de mes seria reescribir a quien pertenece una
    // numeracion ya usada. Escribir la clave en el no-op da permission denied, y
    // el contador del numero de control ya resolvia esto igual desde la Fase 2.
    public const string SqlContadorBloquear = @"
        INSERT INTO FED.FED_RETENCION_CONTADOR (EMISOR_ID, PERIODO, ULTIMO_NUMERO)
        VALUES (@emisor_id, @periodo, 0)
        ON CONFLICT (EMISOR_ID, PERIODO) DO UPDATE
            SET FECHA_UPD = FED.FED_RETENCION_CONTADOR.FECHA_UPD
        RETURNING ULTIMO_NUMERO;";

    public const string SqlContadorActualizar = @"
        UPDATE FED.FED_RETENCION_CONTADOR SET
            ULTIMO_NUMERO = @ultimo_numero,
            FECHA_UPD     = now()
        WHERE EMISOR_ID = @emisor_id AND PERIODO = @periodo;";

    public const string SqlRetencionInsert = @"
        INSERT INTO FED.FED_RETENCION
            (EMISOR_ID, NUMERACION, PERIODO,
             AGENTE_RIF, AGENTE_RAZON_SOCIAL, AGENTE_DOMICILIO,
             PROVEEDOR_RIF, PROVEEDOR_RAZON_SOCIAL, PROVEEDOR_DOMICILIO, PROVEEDOR_CORREO,
             TOTAL_DOCUMENTOS, TOTAL_BASE, TOTAL_IMPUESTO, TOTAL_RETENIDO,
             IMPRENTA_RIF, IMPRENTA_RAZON_SOCIAL, IMPRENTA_PROVIDENCIA, ES_PRUEBA,
             CLAVE_IDEMPOTENCIA, USUARIO_INS)
        VALUES
            (@emisor_id, @numeracion, @periodo,
             @agente_rif, @agente_razon_social, @agente_domicilio,
             @proveedor_rif, @proveedor_razon_social, @proveedor_domicilio, @proveedor_correo,
             @total_documentos, @total_base, @total_impuesto, @total_retenido,
             @imprenta_rif, @imprenta_razon_social, @imprenta_providencia, @es_prueba,
             @clave_idempotencia, @usuario_ins)
        RETURNING ID, EMITIDO_EN;";

    public const string SqlDetalleInsert = @"
        INSERT INTO FED.FED_RETENCION_DETALLE
            (RETENCION_ID, ORDEN, DOCUMENTO_NUMERO, DOCUMENTO_CONTROL, DOCUMENTO_FECHA,
             MONTO_TOTAL, BASE_IMPONIBLE, IMPUESTO_CAUSADO, MONTO_RETENIDO, PORCENTAJE)
        VALUES
            (@retencion_id, @orden, @documento_numero, @documento_control, @documento_fecha,
             @monto_total, @base_imponible, @impuesto_causado, @monto_retenido, @porcentaje);";

    // Idempotencia, camino rapido: si esta solicitud ya produjo un comprobante,
    // se devuelve ese y no se emite otro.
    public const string SqlRetencionPorClave = @"
        SELECT r.ID, r.NUMERACION, r.PERIODO, r.EMITIDO_EN,
               r.AGENTE_RIF, r.AGENTE_RAZON_SOCIAL, r.PROVEEDOR_RIF, r.PROVEEDOR_RAZON_SOCIAL,
               r.TOTAL_DOCUMENTOS, r.TOTAL_BASE, r.TOTAL_IMPUESTO, r.TOTAL_RETENIDO, r.ES_PRUEBA,
               (SELECT COUNT(*) FROM FED.FED_RETENCION_DETALLE d WHERE d.RETENCION_ID = r.ID)
                   AS CANTIDAD_DOCUMENTOS
        FROM FED.FED_RETENCION r
        WHERE r.EMISOR_ID = @emisor_id AND r.CLAVE_IDEMPOTENCIA = @clave;";

    public const string SqlRetencionGetAll = @"
        SELECT r.ID, r.EMISOR_ID, r.NUMERACION, r.PERIODO, r.EMITIDO_EN,
               r.AGENTE_RIF, r.AGENTE_RAZON_SOCIAL, r.PROVEEDOR_RIF, r.PROVEEDOR_RAZON_SOCIAL,
               r.TOTAL_DOCUMENTOS, r.TOTAL_BASE, r.TOTAL_IMPUESTO, r.TOTAL_RETENIDO, r.ES_PRUEBA,
               (SELECT COUNT(*) FROM FED.FED_RETENCION_DETALLE d WHERE d.RETENCION_ID = r.ID)
                   AS CANTIDAD_DOCUMENTOS,
               COUNT(*) OVER() AS TOTAL_REGISTROS
        FROM FED.FED_RETENCION r
        WHERE (@emisor_id = 0 OR r.EMISOR_ID = @emisor_id)
          AND (@periodo = '' OR r.PERIODO = @periodo)
        ORDER BY r.EMITIDO_EN DESC, r.ID DESC
        LIMIT @page_size OFFSET @row_offset;";

    // Art. 11.1: catorce caracteres, AAAAMMSSSSSSSS. El formato lo fija la norma
    // y se arma en un solo lugar, igual que el del numero de control.
    public static string FormatearNumeracion(string periodo, long secuencial) =>
        periodo + secuencial.ToString("D8");

    // El periodo, legible. Se guarda AAAAMM porque asi lo embebe la numeracion
    // del 11.1, pero un agente que revisa sus retenciones lee "09/2026".
    public static string FormatearPeriodo(string periodo) =>
        periodo is { Length: 6 } ? $"{periodo[4..]}/{periodo[..4]}" : periodo;

    // Los totales del comprobante son la suma del desglose. Se calculan aca y no
    // se piden en el request: pedirlos seria dejar que quien llama declare un
    // total que no cierra contra sus propias lineas.
    public static RetencionTotales Sumar(List<RetencionDocumentoCommand> documentos) => new(
        documentos.Sum(d => d.MontoTotal),
        documentos.Sum(d => d.BaseImponible),
        documentos.Sum(d => d.ImpuestoCausado),
        documentos.Sum(d => d.MontoRetenido));

    public static RetencionEmitidaResponse MapPorClave(IDataReader reader, FacturaImprentaDatos imprenta)
    {
        DateTime emitidoEn = reader.GetDateTime(reader.GetOrdinal("emitido_en"));

        return new RetencionEmitidaResponse(
            reader.SafeGetInt64("id"),
            reader.SafeGetString("numeracion"),
            reader.SafeGetString("periodo"),
            FacturaFormato.FechaOchoDigitos(emitidoEn),
            FacturaFormato.HoraConMeridiano(emitidoEn),
            reader.SafeGetString("agente_rif"),
            reader.SafeGetString("agente_razon_social"),
            reader.SafeGetString("proveedor_rif"),
            reader.SafeGetString("proveedor_razon_social"),
            reader.SafeGetDecimal("total_documentos"),
            reader.SafeGetDecimal("total_base"),
            reader.SafeGetDecimal("total_impuesto"),
            reader.SafeGetDecimal("total_retenido"),
            (int)reader.SafeGetInt64("cantidad_documentos"),
            reader.GetBoolean(reader.GetOrdinal("es_prueba")),
            FacturaImprenta.MotivoDePrueba(imprenta, "11.9"),
            YaExistia: true);
    }
}
