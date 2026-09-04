-- =============================================================================
-- Facturacion Electronica (FED) - Requerimiento 32 - Fase 4
--
-- La numeracion PROPIA del documento (Art. 7.2), que no es el numero de control.
--
-- Son dos numeraciones distintas y hasta la Fase 3 solo estaba resuelta una:
--
--   Numero de control      lo asigna la IMPRENTA    Arts. 7.4 y 30
--   Numeracion del doc.    es del EMISOR            Art. 7.2
--
-- Decision D-21. La numeracion del documento la genera nuestro sistema cuando
-- actua como Rol B -sistema de emision-, con un contador por emisor, tipo de
-- documento y serie. Un tercero que solo nos compra numeros de control emite
-- desde su propio sistema y trae la suya: ahi no se puede inventar.
--
-- EL MODO SE FIJA EN EL EMISOR, NO POR PETICION. Si se decidiera peticion por
-- peticion, un mismo emisor podria alternar entre los dos modos sobre la misma
-- serie y chocar la numeracion. Esa colision es INV-3, la causal del Art. 21.2
-- que le cuesta la autorizacion al cliente emisor.
--
-- La serie entra en la clave porque el Art. 13 la exige cuando el emisor carece
-- de un sistema de facturacion centralizado: numeracion "precedida de la palabra
-- serie" mas los caracteres que la identifiquen. Un emisor sin series usa la
-- serie vacia y el contador funciona igual.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- Modo de numeracion del emisor. Aditivo.
-- -----------------------------------------------------------------------------
ALTER TABLE FED.FED_EMISOR
    ADD COLUMN IF NOT EXISTS MODO_NUMERACION VARCHAR(20) DEFAULT 'sistema' NOT NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'fed_emisor_modo_num_ck'
          AND conrelid = 'fed.fed_emisor'::regclass
    ) THEN
        ALTER TABLE FED.FED_EMISOR
            ADD CONSTRAINT FED_EMISOR_MODO_NUM_CK
            CHECK (MODO_NUMERACION IN ('sistema', 'externa'));
    END IF;
END
$$;

COMMENT ON COLUMN FED.FED_EMISOR.MODO_NUMERACION IS
    'sistema = la numeracion del Art. 7.2 la genera este modulo (Rol B). externa = la trae el emisor, que emite desde su propio sistema (Rol A). Ver D-21.';

-- -----------------------------------------------------------------------------
-- Contador de numeracion, uno por emisor + tipo de documento + serie.
--
-- Es otra granularidad que FED_EMISOR_CONTADOR: aquel lleva UN contador por
-- emisor para el numero de control -la secuencia le pertenece al RIF, Art. 30-,
-- este lleva uno por cada combinacion de tipo y serie, porque el Art. 7.2 pide la
-- numeracion consecutiva y unica del documento y el Art. 13 la parte por series.
--
-- Mismo mecanismo de bloqueo que el del numero de control, y por la misma razon:
-- SELECT MAX() + 1 esta roto bajo concurrencia, y eso ya quedo demostrado en la
-- Fase 2 -la forma prohibida dejo 17 asignaciones de 300-.
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS FED.FED_EMISOR_DOC_CONTADOR (
    EMISOR_ID       BIGINT      NOT NULL,
    TIPO_DOCUMENTO  VARCHAR(20) NOT NULL,

    -- Cadena vacia y no NULL: forma parte de la clave primaria, y en PostgreSQL
    -- dos NULL no son iguales entre si. Con NULL, un emisor sin series tendria
    -- una fila nueva por cada asignacion.
    SERIE           VARCHAR(20) NOT NULL DEFAULT '',

    -- Ultimo numero asignado. Nace en 0 y la primera emision da 1.
    ULTIMO_NUMERO   BIGINT      NOT NULL DEFAULT 0,

    FECHA_UPD       TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT FED_EMI_DOC_CONT_PK PRIMARY KEY (EMISOR_ID, TIPO_DOCUMENTO, SERIE),

    CONSTRAINT FED_EMI_DOC_CONT_EMISOR_FK FOREIGN KEY (EMISOR_ID)
        REFERENCES FED.FED_EMISOR (ID),

    CONSTRAINT FED_EMI_DOC_CONT_TIPO_CK
        CHECK (TIPO_DOCUMENTO IN ('factura', 'debito', 'credito', 'entrega')),

    CONSTRAINT FED_EMI_DOC_CONT_NUM_CK CHECK (ULTIMO_NUMERO >= 0)
);

COMMENT ON TABLE FED.FED_EMISOR_DOC_CONTADOR IS
    'Ultimo numero de documento por emisor, tipo y serie (Art. 7.2 y Art. 13). Su fila se bloquea al emitir. No confundir con FED_EMISOR_CONTADOR, que es del numero de control.';

-- Los permisos del rol de aplicacion NO van aca: viven todos juntos en
-- GRANTS_FED_APP.sql, que es el script que se reejecuta cuando una fase agrega
-- tablas. Partirlos entre archivos es como se termina con un permiso que nadie
-- sabe donde se otorgo.
