-- =============================================================================
-- Facturacion Electronica (FED) - Requerimiento 32 - Fase 5
--
-- Vista FED_V_DOCUMENTO_ESTADO: el estado y el saldo de un documento fiscal,
-- DERIVADOS. Decisiones D-27, D-32 y D-34.
--
-- POR QUE UNA VISTA Y NO UNA COLUMNA. Es lo que resolvio T5.2 leyendo la
-- Providencia SNAT/2011/00071:
--
--   Art. 41  Los documentos emitidos "no deben tener tachaduras ni
--            enmendaduras". Prohibe alterar el documento.
--   Art. 36  Los originales de los documentos ANULADOS, junto con su copia, se
--            conservan a disposicion del SENIAT. Un documento anulado SIGUE
--            EXISTIENDO: la anulacion no lo borra ni lo reescribe.
--
-- 09_fed_documento.sql ya habia dejado escrito que la tabla no lleva columna de
-- estado "y es a proposito", cerrando con: "si la 0071 obliga a otra cosa, se
-- agrega entonces con su justificacion escrita". NO OBLIGA. Lo contrario: lo
-- prohibe.
--
-- Y no es solo doctrina. GRANTS_FED_APP.sql no le da UPDATE al rol de
-- aplicacion sobre FED_DOCUMENTO, asi que una columna de estado seria imposible
-- de escribir sin reabrir lo que D-20 cerro. El estado como vista no es la
-- opcion elegante: es la unica que el sistema admite.
--
-- QUE HACE QUE UN DOCUMENTO ESTE ANULADO (D-32). El acto declarado, no la
-- aritmetica. El Art. 22 distingue operaciones que "quedaren sin efecto parcial
-- o totalmente" DE las que "originaren un ajuste": una nota de credito por el
-- importe exacto de una devolucion total es aritmeticamente identica a una
-- anulacion y no es el mismo acto juridico. Por eso el estado se decide por la
-- fila 'anulacion' de la bitacora -que la nota escribe cuando declara su
-- intencion- y NO por que el saldo llegue a cero.
--
-- El saldo se expone igual, porque es informacion real y es lo que sostiene la
-- validacion de D-34: una nota de credito no puede exceder lo que queda. Pero
-- el saldo NO decide el estado.
--
-- POR QUE LA CONSULTA ES INDEXABLE. La pregunta "que notas afectan al documento
-- X" pasa por FED_NOTA.DOCUMENTO_ORIGEN_ID, que tiene su indice; nunca por el
-- jsonb de la bitacora. Y la fila de anulacion apunta con su columna foranea AL
-- DOCUMENTO ANULADO, que es justo el id por el que se busca, servido por el
-- indice FED_BITACORA_DOC_IX que ya existia.
--
-- NO ES ESCRIBIBLE, igual que FED_V_REGISTRO_ART32: tiene agregados, y eso ya
-- se lo impide PostgreSQL. Se deja dicho para que nadie lo intente.
-- =============================================================================

CREATE OR REPLACE VIEW FED.FED_V_DOCUMENTO_ESTADO AS
SELECT
    d.ID                                          AS DOCUMENTO_ID,
    d.EMISOR_ID,
    d.TIPO_DOCUMENTO,
    d.NUMERACION,
    d.MONEDA,
    d.TOTAL_GENERAL,

    COALESCE(n.CANTIDAD_NOTAS, 0)                 AS CANTIDAD_NOTAS,
    COALESCE(n.NOTAS_CREDITO, 0)                  AS NOTAS_CREDITO,
    COALESCE(n.NOTAS_DEBITO, 0)                   AS NOTAS_DEBITO,
    COALESCE(n.MONTO_CREDITO, 0)                  AS MONTO_CREDITO,
    COALESCE(n.MONTO_DEBITO, 0)                   AS MONTO_DEBITO,

    -- El saldo pendiente. Una nota de credito resta y una de debito suma: los
    -- montos de las notas son siempre POSITIVOS -el signo lo lleva la
    -- denominacion, no el importe, igual que en papel-, asi que el sentido lo
    -- pone esta resta y no el dato.
    --
    -- Es el numero que sostiene D-34: una nota de credito no puede exceder lo
    -- que queda. Sin esa validacion se pueden emitir diez notas por el total de
    -- la misma factura, y en una tabla sin DELETE eso no queda mal: queda mal
    -- para siempre.
    d.TOTAL_GENERAL
        - COALESCE(n.MONTO_CREDITO, 0)
        + COALESCE(n.MONTO_DEBITO, 0)             AS SALDO,

    (b.DOCUMENTO_ID IS NOT NULL)                  AS TIENE_ANULACION,
    b.OCURRIDO_EN                                 AS ANULADO_EN,

    -- El orden importa: un documento anulado que ademas tiene notas es anulado.
    -- La anulacion es el estado terminal.
    CASE
        WHEN b.DOCUMENTO_ID IS NOT NULL        THEN 'anulado'
        WHEN COALESCE(n.CANTIDAD_NOTAS, 0) > 0 THEN 'ajustado'
        ELSE 'emitido'
    END                                           AS ESTADO

FROM FED.FED_DOCUMENTO d

LEFT JOIN (
    SELECT
        nt.DOCUMENTO_ORIGEN_ID                                                   AS ORIGEN_ID,
        COUNT(*)                                                                 AS CANTIDAD_NOTAS,
        COUNT(*) FILTER (WHERE doc.TIPO_DOCUMENTO = 'credito')                   AS NOTAS_CREDITO,
        COUNT(*) FILTER (WHERE doc.TIPO_DOCUMENTO = 'debito')                    AS NOTAS_DEBITO,
        COALESCE(SUM(doc.TOTAL_GENERAL) FILTER (WHERE doc.TIPO_DOCUMENTO = 'credito'), 0) AS MONTO_CREDITO,
        COALESCE(SUM(doc.TOTAL_GENERAL) FILTER (WHERE doc.TIPO_DOCUMENTO = 'debito'), 0)  AS MONTO_DEBITO
    FROM FED.FED_NOTA nt
    JOIN FED.FED_DOCUMENTO doc ON doc.ID = nt.DOCUMENTO_ID
    GROUP BY nt.DOCUMENTO_ORIGEN_ID
) n ON n.ORIGEN_ID = d.ID

-- La primera anulacion es la que cuenta. Anular dos veces no anula mas.
LEFT JOIN (
    SELECT DOCUMENTO_ID, MIN(OCURRIDO_EN) AS OCURRIDO_EN
    FROM FED.FED_BITACORA
    WHERE ACCION = 'anulacion' AND DOCUMENTO_ID IS NOT NULL
    GROUP BY DOCUMENTO_ID
) b ON b.DOCUMENTO_ID = d.ID;

COMMENT ON VIEW FED.FED_V_DOCUMENTO_ESTADO IS
    'Estado y saldo derivados del documento. Nunca una columna (D-27, Arts. 36 y 41 de la SNAT/2011/00071). El estado lo decide el acto declarado, no el saldo (D-32). NO ESCRIBIBLE.';
