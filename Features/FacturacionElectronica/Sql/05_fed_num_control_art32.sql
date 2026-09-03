-- =============================================================================
-- Facturacion Electronica (FED) - Requerimiento 32 - Fase 3
--
-- Columnas que el registro del Articulo 32 necesita sobre FED_NUM_CONTROL.
-- Aditivo: no toca ni una columna existente.
--
-- Va ANTES que la vista del script 06, que las lee.
--
-- POR QUE DOS COLUMNAS PARA EL NUMERAL 6 (decision D-15). El Art. 32 de la 102
-- copio su registro del Art. 12 de la Providencia SNAT/2018/0141, numeral por
-- numeral. Pero en el sexto la 0141 dice, literal:
--
--     "Identificacion de la factura emitida por la prestacion de SU servicio"
--
-- Ese posesivo no deja lugar a duda: es la factura que la IMPRENTA le emite al
-- emisor para cobrarle. La 102 quito el "su" y agrego "la venta de bienes", con
-- lo que admite ademas la lectura de que sea el documento fiscal del cliente.
-- Sin el texto de Gaceta no hay como desempatarlas, y cada una lleva a un modelo
-- distinto. Se guardan las dos: una columna cuesta nada, y equivocarse cuesta
-- rehacer el registro que se le reporta al SENIAT.
--
-- POR QUE ESTADO_CONCILIACION (decision D-16). Asignar un numero sin documento
-- es valido, no un hueco del diseno. El Art. 7.5 expresa los numeros asignados
-- "desde el N ... hasta el N ...", que es un rango y por tanto preexiste a los
-- documentos que lo consumen; el Art. 7.15 pide la fecha de asignacion ADEMAS de
-- la fecha de emision del 7.6, lo que solo tiene sentido si pueden diferir; y el
-- Art. 12.5 de la 0141 numera formatos y formas libres, que en la imprenta
-- fisica existen antes que cualquier documento. Lo que si hace falta es que esa
-- situacion sea visible, no silenciosa.
-- =============================================================================

ALTER TABLE FED.FED_NUM_CONTROL
    -- Numeral 7: clausula abierta. jsonb para que el SENIAT pueda pedir un campo
    -- nuevo sin que eso sea una migracion estructural.
    ADD COLUMN IF NOT EXISTS DATOS_ADICIONALES   JSONB,

    -- Numeral 6, lectura de la 0141: la factura de Ossmmasoft al emisor.
    ADD COLUMN IF NOT EXISTS FACTURA_SERVICIO    VARCHAR(60),

    -- D-16. Se deriva de si hay documento, pero se guarda explicito para poder
    -- filtrarlo e indexarlo sin recalcularlo en cada consulta.
    ADD COLUMN IF NOT EXISTS ESTADO_CONCILIACION VARCHAR(20)
        DEFAULT 'sin_documento' NOT NULL;

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'fed_num_control_concil_ck'
          AND conrelid = 'fed.fed_num_control'::regclass
    ) THEN
        ALTER TABLE FED.FED_NUM_CONTROL
            ADD CONSTRAINT FED_NUM_CONTROL_CONCIL_CK
            CHECK (ESTADO_CONCILIACION IN ('sin_documento', 'conciliado'));
    END IF;
END
$$;

-- Coherencia entre las dos columnas: si hay documento, el estado es conciliado.
-- Se aplica a lo que ya existe; de aqui en mas lo mantiene el handler.
UPDATE FED.FED_NUM_CONTROL
   SET ESTADO_CONCILIACION = 'conciliado'
 WHERE DOCUMENTO_ID IS NOT NULL
   AND ESTADO_CONCILIACION <> 'conciliado';

-- Art. 32 numeral 1: el registro se consulta por RIF del emisor. El RIF vive en
-- FED_EMISOR y ya es UNIQUE, asi que el join esta cubierto; lo que falta es
-- poder recorrer las asignaciones pendientes de conciliar sin escanear la tabla.
CREATE INDEX IF NOT EXISTS FED_NUM_CONTROL_CONCIL_IX
    ON FED.FED_NUM_CONTROL (ESTADO_CONCILIACION)
    WHERE ESTADO_CONCILIACION = 'sin_documento';

COMMENT ON COLUMN FED.FED_NUM_CONTROL.DATOS_ADICIONALES   IS 'Art. 32 numeral 7: clausula abierta. Se extiende sin migracion.';
COMMENT ON COLUMN FED.FED_NUM_CONTROL.FACTURA_SERVICIO    IS 'Art. 32 numeral 6, lectura de la 0141: la factura que la imprenta emite al emisor por su servicio. Ver D-15.';
COMMENT ON COLUMN FED.FED_NUM_CONTROL.ESTADO_CONCILIACION IS 'sin_documento = numero asignado que todavia no ampara un documento. Es valido (D-16), pero tiene que verse.';
