-- =============================================================================
-- Facturacion Electronica (FED) - Requerimiento 32 - Fase 6
--
-- Tabla FED_GUIA_DESPACHO y la medida del Art. 10.4 en el detalle. DOC-4.
--
-- POR QUE NO SE LLAMA "NOTA DE ENTREGA". Porque no existe. El Art. 10.1 admite
-- dos denominaciones y solo dos: "orden de entrega" o "guia de despacho". El
-- negocio la llama nota de entrega puertas adentro y el tipo en base es
-- 'entrega' -contrato desde la Fase 4, no se toca-, pero el archivo y la tabla
-- se llaman por lo que el documento es.
--
-- ES UNA EXTENSION 1:0..1 DE FED_DOCUMENTO, como FED_NOTA y a diferencia de
-- FED_RETENCION. La guia SI es un documento fiscal del Art. 7 en lo que el Art.
-- 10.2 remite: tiene numeracion (7.2), numero de control (7.4), fecha (7.6) y
-- datos de la imprenta (7.14). Comparte el id: no es una fila que apunta a un
-- documento, es la mitad de guia de ese documento.
--
-- LO QUE EL ART. 10.2 **NO** REMITE, Y ES LO QUE HAY QUE TENER PRESENTE LEYENDO
-- ESTO. Remite a los numerales 2, 3, 4, 5, 6 y 14 del Art. 7. No al 7.7
-- -adquiriente, que el 10.5 reemplaza por el receptor-, no al 7.8 -descripcion,
-- cantidad y PRECIO, que el 10.4 reemplaza por descripcion y medida-, y no al
-- 7.10, 7.11, 7.12 ni 7.13 -ajustes, base imponible, IVA y total-.
--
-- O sea que una guia de despacho con montos no es un documento incompleto: es un
-- documento que dice algo que la norma no admite que diga (D-40). Eso lo hace
-- cumplir el validador, porque los montos viven en FED_DOCUMENTO y esta tabla no
-- puede alcanzarlos con un CHECK.
--
-- APPEND-ONLY, como todo lo que cuelga de un documento fiscal.
-- =============================================================================

-- Diagnostico antes de crear nada, igual que en FED_NOTA: cuantas guias alcanzo
-- a persistir el hueco que T6.3 y T6.4 cierran. No falla -son documentos de
-- prueba- pero deja el numero en el log en vez de que aparezca como sorpresa
-- despues. No tienen migracion posible: no hay UPDATE sobre FED_DOCUMENTO, asi
-- que a una guia ya emitida no se le puede borrar el IVA que nunca debio tener.
DO $$
DECLARE
    previas   INTEGER;
    con_monto INTEGER;
BEGIN
    SELECT COUNT(*) INTO previas
      FROM FED.FED_DOCUMENTO
     WHERE TIPO_DOCUMENTO = 'entrega';

    SELECT COUNT(*) INTO con_monto
      FROM FED.FED_DOCUMENTO
     WHERE TIPO_DOCUMENTO = 'entrega'
       AND (TOTAL_BASE <> 0 OR TOTAL_IVA <> 0 OR TOTAL_GENERAL <> 0);

    IF previas > 0 THEN
        RAISE NOTICE 'FED_GUIA_DESPACHO: hay % guia(s) emitidas ANTES de esta fase, % de ellas con montos que el Art. 10.2 no admite. Quedan como datos de prueba: no hay UPDATE sobre FED_DOCUMENTO.', previas, con_monto;
    ELSE
        RAISE NOTICE 'FED_GUIA_DESPACHO: no hay guias previas. Nada que arrastrar.';
    END IF;
END $$;

CREATE TABLE IF NOT EXISTS FED.FED_GUIA_DESPACHO (
    -- La guia ES el documento: comparte su id.
    DOCUMENTO_ID     BIGINT       PRIMARY KEY,

    -- POR QUE EL MOTIVO SE EXIGE Y DE DONDE SALE (D-43). No sale del 10.4, que
    -- pide capacidad, peso o volumen y nada mas. Sale del ENCABEZADO del Art.
    -- 10: estos documentos se emiten "unicamente para amparar el traslado de
    -- bienes muebles que no representen ventas". Sin declarar el motivo, el
    -- emisor no tiene con que sostener que su traslado cae dentro de esa
    -- condicion, y esa condicion es la que habilita el documento entero.
    MOTIVO_TRASLADO  VARCHAR(500) NOT NULL,

    -- A donde van los bienes. No lo pide ningun numeral, y por eso es NULO: un
    -- traslado sin destino escrito sigue siendo un documento valido. Se guarda
    -- porque es lo primero que se pregunta al auditar si el traslado ocurrio, y
    -- el Art. 18.2 pide poder auditar.
    DESTINO          VARCHAR(400),

    USUARIO_INS      VARCHAR(60)  NOT NULL DEFAULT CURRENT_USER,
    FECHA_INS        TIMESTAMPTZ  NOT NULL DEFAULT now(),

    CONSTRAINT FED_GUIA_DOC_FK FOREIGN KEY (DOCUMENTO_ID)
        REFERENCES FED.FED_DOCUMENTO (ID),

    -- "Por cualquier causa" no aplica aca: el Art. 10 admite UNA causa, que el
    -- traslado no sea una venta. Un motivo vacio no declara ninguna.
    CONSTRAINT FED_GUIA_MOTIVO_CK CHECK (LENGTH(TRIM(MOTIVO_TRASLADO)) > 0)
);

COMMENT ON TABLE  FED.FED_GUIA_DESPACHO                 IS 'Extension del Art. 10 sobre FED_DOCUMENTO, para el tipo entrega. APPEND-ONLY.';
COMMENT ON COLUMN FED.FED_GUIA_DESPACHO.MOTIVO_TRASLADO IS 'No lo pide el 10.4: lo pide el encabezado del Art. 10, que solo admite traslados que no representen ventas (D-43).';
COMMENT ON COLUMN FED.FED_GUIA_DESPACHO.DESTINO         IS 'No lo pide ningun numeral. Se guarda porque el Art. 18.2 exige poder auditar el traslado.';

-- =============================================================================
-- Art. 10.4 en el detalle: la medida de los bienes que se trasladan
--
-- "La descripcion de los bienes que se trasladan, senalando su capacidad, peso o
-- volumen". La descripcion ya existe en FED_DOCUMENTO_DETALLE.DESCRIPCION y se
-- reusa; lo que falta es la medida.
--
-- ESTRUCTURADA Y NO TEXTO LIBRE (D-42). Con un campo de texto, 'asdf' cumple el
-- numeral y el validador no puede decir nada. Con el tipo acotado a los tres que
-- la norma nombra, mas valor y unidad, el numeral se vuelve comprobable. Es el
-- mismo criterio de D-35: un numeral es un dato, no un comentario.
--
-- ADITIVO Y NULO PARA LOS DEMAS. La factura y las notas no trasladan bienes y no
-- usan estas columnas. Por eso van aca y no en una tabla aparte: el renglon de
-- una guia es un renglon, no otra cosa, y partirlo obligaria a leer dos tablas
-- para imprimir una linea.
-- =============================================================================

ALTER TABLE FED.FED_DOCUMENTO_DETALLE
    ADD COLUMN IF NOT EXISTS MEDIDA_TIPO   VARCHAR(10);

ALTER TABLE FED.FED_DOCUMENTO_DETALLE
    ADD COLUMN IF NOT EXISTS MEDIDA_VALOR  NUMERIC(18,4);

ALTER TABLE FED.FED_DOCUMENTO_DETALLE
    ADD COLUMN IF NOT EXISTS MEDIDA_UNIDAD VARCHAR(20);

DO $$
BEGIN
    -- Los tres que el numeral nombra, y nada mas. Si manana la norma admite otro,
    -- se agrega aca y queda la fecha del cambio en el control de versiones.
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fed_doc_det_medida_tipo_ck') THEN
        ALTER TABLE FED.FED_DOCUMENTO_DETALLE
            ADD CONSTRAINT FED_DOC_DET_MEDIDA_TIPO_CK
            CHECK (MEDIDA_TIPO IS NULL OR MEDIDA_TIPO IN ('capacidad', 'peso', 'volumen'));
    END IF;

    -- Coherencia de las tres: o la medida esta completa o no esta. Una unidad sin
    -- valor, o un valor sin unidad, no senala nada.
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fed_doc_det_medida_ck') THEN
        ALTER TABLE FED.FED_DOCUMENTO_DETALLE
            ADD CONSTRAINT FED_DOC_DET_MEDIDA_CK
            CHECK (
                (MEDIDA_TIPO IS NULL AND MEDIDA_VALOR IS NULL AND MEDIDA_UNIDAD IS NULL)
                OR (MEDIDA_TIPO IS NOT NULL AND MEDIDA_VALOR IS NOT NULL AND MEDIDA_VALOR > 0
                    AND MEDIDA_UNIDAD IS NOT NULL AND LENGTH(TRIM(MEDIDA_UNIDAD)) > 0)
            );
    END IF;
END $$;

COMMENT ON COLUMN FED.FED_DOCUMENTO_DETALLE.MEDIDA_TIPO   IS 'Art. 10.4: capacidad, peso o volumen. Nulo en los documentos que no trasladan bienes.';
COMMENT ON COLUMN FED.FED_DOCUMENTO_DETALLE.MEDIDA_VALOR  IS 'Art. 10.4: cuanto. Estructurado y no texto libre para que el numeral sea comprobable (D-42).';
COMMENT ON COLUMN FED.FED_DOCUMENTO_DETALLE.MEDIDA_UNIDAD IS 'Art. 10.4: en que unidad. kg, litros, m3, la que corresponda al tipo.';
