-- =============================================================================
-- Facturacion Electronica (FED) - Requerimiento 32 - Fase 6B
--
-- Tabla FED_RETENCION_DETALLE: que facturas retiene cada comprobante.
--
-- Un comprobante de retencion no retiene "una factura": retiene las facturas de
-- un periodo. Los numerales 5, 6 y 8 del Art. 11 son por documento retenido:
--
--   11.5  Numero de CONTROL de la factura o nota de debito
--   11.6  Numero de la factura o nota de debito
--   11.8  Monto total del documento, base imponible, impuesto causado y monto
--         retenido
--
-- Son los mismos cuatro montos que el comprobante agrega en su cabecera, pero
-- ahi son el total y aca el desglose. Es la misma razon por la que
-- FED_DOC_IMPUESTO existe en vez de ser columnas: un desglose de longitud
-- variable no entra en columnas fijas.
--
-- POR QUE EL DOCUMENTO RETENIDO SE GUARDA COMO DATOS Y NO COMO FORANEA. Porque
-- el Art. 11 pide el NUMERO y el NUMERO DE CONTROL de la factura, no una
-- referencia: son contenido impreso del comprobante. Y sobre todo porque la
-- factura retenida puede no estar en este sistema. El agente de retencion retiene
-- las facturas que le emiten SUS PROVEEDORES, y esos proveedores no son emisores
-- de esta imprenta: sus documentos no viven en FED_DOCUMENTO.
--
-- Es la diferencia con FED_NOTA, donde el documento corregido SI es propio y por
-- eso lleva foranea ademas de instantanea (D-28, D-29).
--
-- APPEND-ONLY, como todo lo que cuelga de un documento fiscal.
-- =============================================================================

CREATE TABLE IF NOT EXISTS FED.FED_RETENCION_DETALLE (
    ID                BIGINT        GENERATED ALWAYS AS IDENTITY PRIMARY KEY,

    RETENCION_ID      BIGINT        NOT NULL,

    -- Orden de aparicion en el comprobante impreso.
    ORDEN             INTEGER       NOT NULL,

    -- 11.6 - numero de la factura o nota de debito retenida.
    DOCUMENTO_NUMERO  VARCHAR(40)   NOT NULL,

    -- 11.5 - su numero de control. Es el de la factura del proveedor, no uno
    -- que asigne esta imprenta.
    DOCUMENTO_CONTROL VARCHAR(20)   NOT NULL,

    -- Fecha del documento retenido. No la pide un numeral con ese nombre, pero
    -- sin ella el periodo de imposicion del 11.7 no se puede sustentar.
    DOCUMENTO_FECHA   DATE          NOT NULL,

    -- 11.8 - los cuatro montos, por documento retenido.
    MONTO_TOTAL       NUMERIC(18,2) NOT NULL DEFAULT 0,
    BASE_IMPONIBLE    NUMERIC(18,2) NOT NULL DEFAULT 0,
    IMPUESTO_CAUSADO  NUMERIC(18,2) NOT NULL DEFAULT 0,
    MONTO_RETENIDO    NUMERIC(18,2) NOT NULL DEFAULT 0,

    -- El porcentaje aplicado. No lo pide el Art. 11, pero sin el no se puede
    -- reconstruir como se llego al monto retenido, y el Art. 18.2 pide poder
    -- auditar. Se guarda, no se referencia: mismo criterio que la alicuota en
    -- FED_DOC_IMPUESTO (D-22).
    PORCENTAJE        NUMERIC(5,2)  NOT NULL DEFAULT 0,

    CONSTRAINT FED_RET_DET_RET_FK FOREIGN KEY (RETENCION_ID)
        REFERENCES FED.FED_RETENCION (ID),

    -- Un mismo documento no se retiene dos veces en el mismo comprobante: serian
    -- dos retenciones del mismo hecho, y el total de la cabecera no cerraria
    -- contra el desglose.
    CONSTRAINT FED_RET_DET_UK UNIQUE (RETENCION_ID, DOCUMENTO_CONTROL, DOCUMENTO_NUMERO),

    CONSTRAINT FED_RET_DET_ORDEN_CK CHECK (ORDEN > 0),

    CONSTRAINT FED_RET_DET_MONTOS_CK CHECK (
        MONTO_TOTAL >= 0 AND BASE_IMPONIBLE >= 0
        AND IMPUESTO_CAUSADO >= 0 AND MONTO_RETENIDO >= 0),

    -- No se puede retener mas de lo que el documento causo.
    CONSTRAINT FED_RET_DET_RETENIDO_CK CHECK (MONTO_RETENIDO <= IMPUESTO_CAUSADO),

    CONSTRAINT FED_RET_DET_PORC_CK CHECK (PORCENTAJE >= 0 AND PORCENTAJE <= 100)
);

CREATE INDEX IF NOT EXISTS FED_RET_DET_RET_IX
    ON FED.FED_RETENCION_DETALLE (RETENCION_ID);

COMMENT ON TABLE  FED.FED_RETENCION_DETALLE                   IS 'Documentos retenidos por un comprobante. Numerales 5, 6 y 8 del Art. 11. APPEND-ONLY.';
COMMENT ON COLUMN FED.FED_RETENCION_DETALLE.DOCUMENTO_CONTROL IS 'Art. 11.5: el numero de control de la factura del PROVEEDOR, que no es emisor de esta imprenta.';
COMMENT ON COLUMN FED.FED_RETENCION_DETALLE.PORCENTAJE        IS 'No lo pide el Art. 11, pero sin el no se puede auditar como se llego al monto retenido (Art. 18.2).';
