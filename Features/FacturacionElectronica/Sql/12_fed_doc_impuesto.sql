-- =============================================================================
-- Facturacion Electronica (FED) - Requerimiento 32 - Fase 4
--
-- Tabla FED_DOC_IMPUESTO: base imponible e IVA DISCRIMINADOS POR ALICUOTA.
-- Articulos 7.11 y 7.12.
--
--   7.11  base imponible discriminada por alicuota, indicando el porcentaje
--         aplicable, mas el monto total exento o exonerado
--   7.12  monto total del IVA, discriminado por alicuota
--
-- Es una tabla y no columnas del documento, y la razon esta en la palabra
-- "discriminada": la norma no pide un total, pide un desglose, y un desglose de
-- longitud variable no entra en columnas fijas. Una factura con dos alicuotas
-- produce dos filas; con tres, tres. Con columnas habria que adivinar cuantas
-- alicuotas va a tener el pais, y ya cambiaron antes.
--
-- La tabla salio al armar el MER: ninguna tarea la preveia, aunque T4.5 pedia
-- calcular esos montos. Estaba el calculo sin donde guardarlo.
--
-- APPEND-ONLY, como todo lo que cuelga del documento.
-- =============================================================================

CREATE TABLE IF NOT EXISTS FED.FED_DOC_IMPUESTO (
    ID              BIGINT        GENERATED ALWAYS AS IDENTITY PRIMARY KEY,

    DOCUMENTO_ID    BIGINT        NOT NULL,

    -- El porcentaje aplicable que el 7.11 obliga a indicar. Guardado, no
    -- referenciado (D-22).
    ALICUOTA        NUMERIC(5,2)  NOT NULL,

    BASE_IMPONIBLE  NUMERIC(18,2) NOT NULL DEFAULT 0,
    MONTO_IVA       NUMERIC(18,2) NOT NULL DEFAULT 0,

    CONSTRAINT FED_DOC_IMPUESTO_DOC_FK FOREIGN KEY (DOCUMENTO_ID)
        REFERENCES FED.FED_DOCUMENTO (ID),

    -- Una fila por alicuota y no mas: dos filas de la misma tasa en un documento
    -- serian dos desgloses del mismo hecho, y el del 7.11 no cerraria contra el
    -- total del 7.13.
    CONSTRAINT FED_DOC_IMPUESTO_UK UNIQUE (DOCUMENTO_ID, ALICUOTA),

    CONSTRAINT FED_DOC_IMPUESTO_ALIC_CK CHECK (ALICUOTA >= 0 AND ALICUOTA <= 100),
    CONSTRAINT FED_DOC_IMPUESTO_BASE_CK CHECK (BASE_IMPONIBLE >= 0),
    CONSTRAINT FED_DOC_IMPUESTO_IVA_CK  CHECK (MONTO_IVA >= 0)
);

CREATE INDEX IF NOT EXISTS FED_DOC_IMPUESTO_DOC_IX
    ON FED.FED_DOC_IMPUESTO (DOCUMENTO_ID);

COMMENT ON TABLE  FED.FED_DOC_IMPUESTO          IS 'Desglose por alicuota del Art. 7.11 y 7.12. Una fila por tasa. APPEND-ONLY.';
COMMENT ON COLUMN FED.FED_DOC_IMPUESTO.ALICUOTA IS 'El porcentaje aplicable que el Art. 7.11 obliga a indicar. Guardado y no referenciado (D-22).';
