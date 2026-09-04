using System.Data;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.FacturacionElectronica;

// SQL de la emision, parametrizado y nunca interpolado, como manda el estandar.
//
// Va en la subcarpeta y no en FacturacionElectronicaShared.cs por el mismo
// criterio con el que existe MotorFormularios/Reportes/MfoReporteShared.cs: el
// SQL de un subdominio vive con su subdominio. Shared quedo con lo transversal
// -emisores, numero de control, reporte mensual- y esto es de la factura.

// Lo que devuelve una emision. El numero de control y la numeracion del documento
// viajan separados a proposito: son dos datos distintos que la norma pide por
// separado, y juntarlos en un solo campo obligaria al consumidor a partirlos.
public record FacturaEmitidaResponse(
    long DocumentoId,
    string Numeracion,
    string NumeracionConSerie,
    string Serie,
    string TipoDocumento,
    string Denominacion,
    string NumeroControl,
    string NumeroControlTexto,
    string RangoNumerosControl,
    string FechaEmision8d,
    string HoraEmision,
    string FechaAsignacion8d,
    decimal TotalExento,
    decimal TotalBase,
    decimal TotalIva,
    decimal TotalGeneral,
    bool EsPrueba,
    string MotivoPrueba,
    string LeyendaProvidencia,
    bool YaExistia);

// Instantanea del emisor, leida al emitir. Art. 29.3: la imprenta estampa estos
// datos en el documento, y lo estampado es un hecho historico.
public record FacturaEmisorDatos(
    long Id,
    string Rif,
    string RazonSocial,
    string Domicilio,
    string Estado,
    string ModoNumeracion);

public static class FacturaDb
{
    // ------------------------------------------------------------------
    // Lecturas previas
    // ------------------------------------------------------------------

    public const string SqlEmisorParaEmitir = @"
        SELECT ID, RIF, RAZON_SOCIAL, DOMICILIO_FISCAL, ESTADO, MODO_NUMERACION
        FROM FED.FED_EMISOR
        WHERE ID = @emisor_id;";

    // Idempotencia (T4.9), camino rapido: si esta solicitud ya produjo un
    // documento, se devuelve ese y no se abre nada.
    public const string SqlDocumentoPorClave = @"
        SELECT ID, TIPO_DOCUMENTO, SERIE, NUMERACION, EMITIDO_EN,
               TOTAL_EXENTO, TOTAL_BASE, TOTAL_IVA, TOTAL_GENERAL, ES_PRUEBA
        FROM FED.FED_DOCUMENTO
        WHERE EMISOR_ID = @emisor_id AND CLAVE_IDEMPOTENCIA = @clave;";

    // ------------------------------------------------------------------
    // Numeracion del documento (Art. 7.2, decision D-21)
    // ------------------------------------------------------------------

    // Crea la fila del contador si no existe y la BLOQUEA, en una sentencia.
    //
    // DO UPDATE y no DO NOTHING, por la misma razon que en el contador del numero
    // de control: con DO NOTHING, si otra transaccion acaba de insertar esta fila
    // y todavia no confirmo, esta no la ve y el RETURNING vuelve vacio. DO UPDATE
    // toma el bloqueo y espera. El bloqueo es por emisor, tipo y serie: dos
    // emisores emiten en paralelo sin esperarse.
    public const string SqlContadorDocBloquear = @"
        INSERT INTO FED.FED_EMISOR_DOC_CONTADOR (EMISOR_ID, TIPO_DOCUMENTO, SERIE)
        VALUES (@emisor_id, @tipo_documento, @serie)
        ON CONFLICT (EMISOR_ID, TIPO_DOCUMENTO, SERIE) DO UPDATE
            SET FECHA_UPD = FED.FED_EMISOR_DOC_CONTADOR.FECHA_UPD
        RETURNING ULTIMO_NUMERO;";

    public const string SqlContadorDocActualizar = @"
        UPDATE FED.FED_EMISOR_DOC_CONTADOR SET
            ULTIMO_NUMERO = @ultimo_numero,
            FECHA_UPD     = now()
        WHERE EMISOR_ID = @emisor_id AND TIPO_DOCUMENTO = @tipo_documento AND SERIE = @serie;";

    // ------------------------------------------------------------------
    // Escrituras del documento
    // ------------------------------------------------------------------

    public const string SqlDocumentoInsert = @"
        INSERT INTO FED.FED_DOCUMENTO
            (EMISOR_ID, TIPO_DOCUMENTO, SERIE, NUMERACION,
             EMISOR_RIF, EMISOR_RAZON_SOCIAL, EMISOR_DOMICILIO,
             ADQ_NOMBRE, ADQ_RIF, ADQ_DOCUMENTO_ID,
             TOTAL_EXENTO, TOTAL_BASE, TOTAL_IVA, TOTAL_GENERAL,
             IMPRENTA_RIF, IMPRENTA_RAZON_SOCIAL, IMPRENTA_PROVIDENCIA, ES_PRUEBA,
             CLAVE_IDEMPOTENCIA, USUARIO_INS)
        VALUES
            (@emisor_id, @tipo_documento, @serie, @numeracion,
             @emisor_rif, @emisor_razon_social, @emisor_domicilio,
             @adq_nombre, @adq_rif, @adq_documento_id,
             @total_exento, @total_base, @total_iva, @total_general,
             @imprenta_rif, @imprenta_razon_social, @imprenta_providencia, @es_prueba,
             @clave_idempotencia, @usuario_ins)
        RETURNING ID, EMITIDO_EN;";

    public const string SqlDetalleInsert = @"
        INSERT INTO FED.FED_DOCUMENTO_DETALLE
            (DOCUMENTO_ID, ORDEN, DESCRIPCION, CODIGO, CANTIDAD, PRECIO, ALICUOTA, EXENTO,
             BIENES_ENTREGADOS, AJUSTE_DESCRIPCION, AJUSTE_VALOR, TOTAL_RENGLON)
        VALUES
            (@documento_id, @orden, @descripcion, @codigo, @cantidad, @precio, @alicuota, @exento,
             @bienes_entregados, @ajuste_descripcion, @ajuste_valor, @total_renglon);";

    public const string SqlImpuestoInsert = @"
        INSERT INTO FED.FED_DOC_IMPUESTO (DOCUMENTO_ID, ALICUOTA, BASE_IMPONIBLE, MONTO_IVA)
        VALUES (@documento_id, @alicuota, @base_imponible, @monto_iva);";

    // Art. 18.2: auditoria de toda accion. Se usa tanto para la emision como para
    // el rechazo, que tambien es una accion efectuada.
    public const string SqlBitacoraInsert = @"
        INSERT INTO FED.FED_BITACORA (DOCUMENTO_ID, EMISOR_ID, ACCION, USUARIO, DETALLE)
        VALUES (@documento_id, @emisor_id, @accion, @usuario, @detalle::jsonb);";

    // ------------------------------------------------------------------
    // Listado de documentos emitidos (T4.10)
    // ------------------------------------------------------------------

    public const string SqlDocumentoGetAll = @"
        SELECT
            d.ID, d.EMISOR_ID, d.TIPO_DOCUMENTO, d.SERIE, d.NUMERACION, d.EMITIDO_EN,
            d.EMISOR_RIF, d.EMISOR_RAZON_SOCIAL,
            d.ADQ_NOMBRE, d.ADQ_RIF, d.ADQ_DOCUMENTO_ID,
            d.TOTAL_EXENTO, d.TOTAL_BASE, d.TOTAL_IVA, d.TOTAL_GENERAL, d.ES_PRUEBA,
            COALESCE(nc.IDENTIFICADOR || '-' || LPAD(nc.SECUENCIAL::text, 8, '0'), '') AS NUMERO_CONTROL,
            COUNT(*) OVER() AS TOTAL_REGISTROS
        FROM FED.FED_DOCUMENTO d
        LEFT JOIN FED.FED_NUM_CONTROL nc ON nc.DOCUMENTO_ID = d.ID
        WHERE (@emisor_id = 0 OR d.EMISOR_ID = @emisor_id)
          AND (@tipo_documento = '' OR d.TIPO_DOCUMENTO = @tipo_documento)
        ORDER BY d.EMITIDO_EN DESC, d.ID DESC
        LIMIT @page_size OFFSET @row_offset;";

    // ------------------------------------------------------------------
    // Mapeo
    // ------------------------------------------------------------------

    public static FacturaEmisorDatos MapEmisor(IDataReader reader) => new(
        reader.SafeGetInt64("id"),
        reader.SafeGetString("rif"),
        reader.SafeGetString("razon_social"),
        reader.SafeGetString("domicilio_fiscal"),
        reader.SafeGetString("estado"),
        reader.SafeGetString("modo_numeracion"));

    // El siguiente numero del documento. Igual que en el numero de control, nunca
    // sale de un MAX(): sale de la fila del contador ya bloqueada.
    public static string SiguienteNumeracion(long ultimoNumero) => (ultimoNumero + 1).ToString();
}
