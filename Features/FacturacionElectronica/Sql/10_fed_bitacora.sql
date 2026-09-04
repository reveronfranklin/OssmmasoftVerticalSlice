-- =============================================================================
-- Facturacion Electronica (FED) - Requerimiento 32 - Fase 4
--
-- Tabla FED_BITACORA. Es el Articulo 18.2 al pie de la letra:
--
--   "auditoria electronica de toda ACCION efectuada para la emision,
--    modificacion o anulacion" de los documentos.
--
-- Tres cosas se siguen de esa redaccion y estan en el diseno:
--
-- 1. Dice "toda accion", no "todo cambio". Se registra tambien lo que no cambio
--    nada: un intento rechazado es una accion efectuada.
-- 2. Nombra la ANULACION como accion posible. Por eso el documento no lleva
--    columna de estado mutable: su estado se deriva de aca, que es donde la norma
--    quiere que viva el rastro. Ver el encabezado del script 09.
-- 3. Es append-only por definicion. Una bitacora que se puede editar no es una
--    bitacora. El rol de aplicacion recibe SELECT e INSERT y nada mas.
--
-- Tambien sostiene RNF-6 y es el cimiento de la superficie de auditoria de la
-- Fase 8 (ALC-4): un tercero con credenciales de solo lectura tiene que poder
-- reconstruir que paso sin tocar el resto del sistema.
-- =============================================================================

CREATE TABLE IF NOT EXISTS FED.FED_BITACORA (
    ID              BIGINT       GENERATED ALWAYS AS IDENTITY PRIMARY KEY,

    -- Nulo cuando la accion no llego a producir documento: un intento de emision
    -- rechazado por el validador tambien es una accion efectuada, y perderlo
    -- seria perder justo el rastro que sirve para explicar un hueco.
    DOCUMENTO_ID    BIGINT,

    -- El emisor siempre se sabe, incluso si el documento no llego a existir.
    EMISOR_ID       BIGINT,

    ACCION          VARCHAR(20)  NOT NULL,

    OCURRIDO_EN     TIMESTAMPTZ  NOT NULL DEFAULT now(),

    -- Quien. Hoy llega del request; cuando se saque de los claims del JWT
    -- -pendiente declarado del modulo- cambia el origen, no la columna.
    USUARIO         VARCHAR(50)  NOT NULL,

    -- El que, en jsonb: que se intento, con que datos, y por que fallo si fallo.
    -- jsonb y no columnas fijas porque cada accion tiene su propia forma, y el
    -- Art. 18.2 no enumera el contenido.
    DETALLE         JSONB,

    CONSTRAINT FED_BITACORA_DOCUMENTO_FK FOREIGN KEY (DOCUMENTO_ID)
        REFERENCES FED.FED_DOCUMENTO (ID),

    CONSTRAINT FED_BITACORA_EMISOR_FK FOREIGN KEY (EMISOR_ID)
        REFERENCES FED.FED_EMISOR (ID),

    -- Las tres acciones que nombra el Art. 18.2, mas el intento rechazado, que es
    -- accion efectuada aunque no haya modificado nada.
    CONSTRAINT FED_BITACORA_ACCION_CK
        CHECK (ACCION IN ('emision', 'modificacion', 'anulacion', 'rechazo'))
);

-- Reconstruir la historia de un documento es la consulta que va a hacer el
-- SENIAT, y tiene que ser barata.
CREATE INDEX IF NOT EXISTS FED_BITACORA_DOC_IX
    ON FED.FED_BITACORA (DOCUMENTO_ID, OCURRIDO_EN);

CREATE INDEX IF NOT EXISTS FED_BITACORA_EMI_FEC_IX
    ON FED.FED_BITACORA (EMISOR_ID, OCURRIDO_EN);

COMMENT ON TABLE  FED.FED_BITACORA         IS 'Auditoria del Art. 18.2: toda accion de emision, modificacion o anulacion. APPEND-ONLY: sin UPDATE ni DELETE para el rol de aplicacion. Sostiene RNF-6.';
COMMENT ON COLUMN FED.FED_BITACORA.ACCION  IS 'emision | modificacion | anulacion | rechazo. Las tres primeras las nombra el Art. 18.2; rechazo se agrega porque un intento fallido tambien es una accion efectuada.';
COMMENT ON COLUMN FED.FED_BITACORA.DETALLE IS 'jsonb: cada accion tiene su forma y el Art. 18.2 no enumera el contenido.';
