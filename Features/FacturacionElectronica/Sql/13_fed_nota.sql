-- =============================================================================
-- Facturacion Electronica (FED) - Requerimiento 32 - Fase 5
--
-- Tabla FED_NOTA: lo que hace que una nota de debito o de credito sea una nota
-- y no otra factura suelta. Providencia SNAT/2011/00071, Arts. 22 y 23.
--
--   Art. 22  Las notas se emiten cuando ventas o servicios "quedaren sin efecto
--            parcial o totalmente U ORIGINAREN UN AJUSTE, POR CUALQUIER CAUSA, y
--            por las cuales se otorgaron facturas".
--
--   Art. 23  Las notas cumplen los requisitos del Art. 13 o del Art. 15, salvo
--            el numeral 1, e "IGUALMENTE DEBEN HACER REFERENCIA A LA FECHA,
--            NUMERO Y MONTO DE LA FACTURA QUE SOPORTO LA OPERACION".
--
-- Es una tabla aparte y no columnas nulables en FED_DOCUMENTO, siguiendo el
-- criterio que ModeloDatos.md ya habia fijado: una extension 1:0..1. Tres
-- columnas nulables en la tabla principal se llenarian solo para dos de los
-- cuatro tipos de documento, y una columna nulable no puede exigir nada. Aca la
-- fila existe o no existe, y cuando existe todo es NOT NULL.
--
-- POR QUE HAY INSTANTANEA Y NO SOLO LA FORANEA (D-29). El Art. 23 pide que la
-- nota HAGA REFERENCIA a la fecha, numero y monto de la factura: eso es
-- contenido impreso del documento fiscal, no el resultado de un JOIN. Mismo
-- principio que ya rige para la razon social del emisor y los datos de la
-- imprenta: lo que se estampo es un hecho historico y no puede cambiar porque
-- manana cambie la fila de origen. Y el fondo es el Art. 41: una nota cuya
-- referencia impresa cambia sola es una enmendadura.
--
-- POR QUE EL VINCULO VIVE ACA Y NO EN LA BITACORA (D-28). FED_BITACORA tiene una
-- sola columna DOCUMENTO_ID con foranea, asi que el id del documento corregido
-- solo cabria dentro del DETALLE jsonb: sin foranea que garantice que existe y
-- sin indice para buscarlo. La pregunta "que notas afectan al documento X" es la
-- que sostiene la vista de estado y se consulta en cada listado.
--
-- SIN UNIQUE EN DOCUMENTO_ORIGEN_ID, Y ES A PROPOSITO. El Art. 22 admite el
-- ajuste PARCIAL, asi que varias notas sobre la misma factura son legales. La
-- unica defensa contra el abuso es la validacion de saldo de D-34, y por eso esa
-- validacion no es opcional: sin ella se pueden emitir diez notas de credito por
-- el total de la misma factura, y en una tabla sin DELETE ni UPDATE eso no queda
-- mal, queda mal PARA SIEMPRE.
--
-- APPEND-ONLY, como todo lo que cuelga del documento.
-- =============================================================================

-- Diagnostico antes de crear nada: cuantas notas alcanzo a persistir el hueco
-- que T5.3 cerro. No falla -son documentos de prueba- pero deja el numero en el
-- log en vez de que aparezca como sorpresa tres semanas despues. Si alguna
-- existiera, no tiene migracion posible: no se le puede inventar un documento
-- origen, y no hay UPDATE sobre FED_DOCUMENTO para corregirla.
DO $$
DECLARE
    huerfanas INTEGER;
BEGIN
    SELECT COUNT(*) INTO huerfanas
      FROM FED.FED_DOCUMENTO
     WHERE TIPO_DOCUMENTO IN ('debito', 'credito');

    IF huerfanas > 0 THEN
        RAISE NOTICE 'FED_NOTA: hay % documento(s) de tipo nota emitidos ANTES de que existiera el vinculo. No tienen migracion posible; quedan como datos de prueba.', huerfanas;
    ELSE
        RAISE NOTICE 'FED_NOTA: no hay notas previas al vinculo. Nada que arrastrar.';
    END IF;
END $$;

CREATE TABLE IF NOT EXISTS FED.FED_NOTA (
    -- La nota ES un documento fiscal: comparte su id. No es una fila con id
    -- propio que apunte a un documento, es la mitad de nota de ese documento.
    DOCUMENTO_ID        BIGINT        PRIMARY KEY,

    -- Lo que corrige. Art. 23.
    DOCUMENTO_ORIGEN_ID BIGINT        NOT NULL,

    -- Art. 22: "por cualquier causa" no significa sin causa. Si la norma admite
    -- cualquier motivo, el sistema tiene que exigir que se diga cual.
    MOTIVO              VARCHAR(500)  NOT NULL,

    -- LA INTENCION, DECLARADA (D-32). El Art. 22 distingue dos supuestos:
    -- operaciones que "quedaren sin efecto parcial o totalmente" U que
    -- "originaren un ajuste". Una nota de credito por el importe exacto de una
    -- devolucion total es ARITMETICAMENTE IDENTICA a una anulacion y no es el
    -- mismo acto juridico. Deducir la intencion del saldo borraria una
    -- distincion que la norma hace, asi que la intencion se registra.
    --
    -- Solo tiene sentido en una nota de CREDITO. El CHECK no puede verlo porque
    -- el tipo vive en FED_DOCUMENTO, asi que lo comprueba el handler.
    ES_ANULACION        BOOLEAN       NOT NULL DEFAULT FALSE,

    -- La instantanea del Art. 23. Se copia al emitir y no se vuelve a mirar el
    -- origen para imprimirla.
    ORIGEN_NUMERACION   VARCHAR(40)   NOT NULL,
    ORIGEN_FECHA_8D     CHAR(8)       NOT NULL,
    ORIGEN_TOTAL        NUMERIC(18,2) NOT NULL,

    -- La moneda del origen entra en la instantanea porque sin ella el "monto" del
    -- Art. 23 no significa nada: comparar un total en divisas contra una factura
    -- de moneda desconocida no es una referencia, es un numero.
    ORIGEN_MONEDA       CHAR(3)       NOT NULL DEFAULT 'VES',

    USUARIO_INS         VARCHAR(50)   NOT NULL DEFAULT 'sistema',
    FECHA_INS           TIMESTAMPTZ   NOT NULL DEFAULT now(),

    CONSTRAINT FED_NOTA_DOC_FK FOREIGN KEY (DOCUMENTO_ID)
        REFERENCES FED.FED_DOCUMENTO (ID),

    CONSTRAINT FED_NOTA_ORIGEN_FK FOREIGN KEY (DOCUMENTO_ORIGEN_ID)
        REFERENCES FED.FED_DOCUMENTO (ID),

    -- Una nota no se corrige a si misma. Sin esto, un id repetido en el request
    -- crearia un documento que se referencia solo y la vista de estado entraria
    -- en un ciclo.
    CONSTRAINT FED_NOTA_NO_PROPIA_CK CHECK (DOCUMENTO_ID <> DOCUMENTO_ORIGEN_ID),

    CONSTRAINT FED_NOTA_MOTIVO_CK CHECK (LENGTH(TRIM(MOTIVO)) > 0),

    -- Ocho digitos, igual que el Art. 7.6 de la 102 y el 13.6 de la 00071.
    CONSTRAINT FED_NOTA_FECHA_CK CHECK (ORIGEN_FECHA_8D ~ '^[0-9]{8}$'),

    CONSTRAINT FED_NOTA_TOTAL_CK CHECK (ORIGEN_TOTAL >= 0)
);

-- El indice que sostiene la vista de estado y la pregunta del listado: que notas
-- afectan a este documento (D-28).
CREATE INDEX IF NOT EXISTS FED_NOTA_ORIGEN_IX
    ON FED.FED_NOTA (DOCUMENTO_ORIGEN_ID);

-- =============================================================================
-- La invariante como propiedad del motor (D-36)
--
-- Un documento de tipo 'debito' o 'credito' NO PUEDE EXISTIR sin su fila en
-- FED_NOTA. Es lo mismo que ya vale para el numero de control: un documento
-- fiscal incompleto no debe poder existir ni un instante.
--
-- Es un CONSTRAINT TRIGGER DIFERIDO porque los dos INSERT ocurren en la misma
-- transaccion y en orden: primero el documento, despues la nota. Un trigger
-- inmediato fallaria en el primero, cuando la nota todavia no puede existir. Un
-- CHECK no sirve: no puede mirar otra tabla.
--
-- ES LA PRIMERA PIEZA DE ESTE TIPO EN EL MODULO, y se declara como tal. La
-- filosofia del modulo es que las invariantes sean propiedades del motor y no
-- promesas del codigo -asi se sostienen el append-only del Art. 18.2 y la
-- inmutabilidad del Art. 41-, pero un trigger es una pieza movil que hay que
-- conocer al leer el esquema.
--
-- Se crea propiedad de fed, no de fed_app, y no necesita GRANT. Y como solo
-- dispara sobre el INSERT, NO revalida filas preexistentes: no falla por las
-- notas que el hueco pudo dejar antes de T5.3.
-- =============================================================================

CREATE OR REPLACE FUNCTION FED.FED_NOTA_EXIGIR_VINCULO()
RETURNS TRIGGER
LANGUAGE plpgsql
AS $$
BEGIN
    IF NEW.TIPO_DOCUMENTO IN ('debito', 'credito')
       AND NOT EXISTS (SELECT 1 FROM FED.FED_NOTA WHERE DOCUMENTO_ID = NEW.ID)
    THEN
        RAISE EXCEPTION
            'El documento % es de tipo % y no tiene su fila en FED_NOTA. El Art. 23 de la SNAT/2011/00071 exige la referencia a la factura que soporto la operacion.',
            NEW.ID, NEW.TIPO_DOCUMENTO;
    END IF;

    RETURN NULL;
END $$;

DROP TRIGGER IF EXISTS FED_DOCUMENTO_NOTA_TG ON FED.FED_DOCUMENTO;

CREATE CONSTRAINT TRIGGER FED_DOCUMENTO_NOTA_TG
    AFTER INSERT ON FED.FED_DOCUMENTO
    DEFERRABLE INITIALLY DEFERRED
    FOR EACH ROW
    EXECUTE FUNCTION FED.FED_NOTA_EXIGIR_VINCULO();

COMMENT ON TABLE  FED.FED_NOTA                     IS 'Mitad de nota de un documento fiscal. Arts. 22 y 23 de la SNAT/2011/00071. APPEND-ONLY.';
COMMENT ON COLUMN FED.FED_NOTA.DOCUMENTO_ORIGEN_ID IS 'La factura que soporto la operacion (Art. 23).';
COMMENT ON COLUMN FED.FED_NOTA.MOTIVO              IS 'Art. 22: la norma admite cualquier causa, pero exige que exista.';
COMMENT ON COLUMN FED.FED_NOTA.ES_ANULACION        IS 'Intencion declarada, no deducida del saldo (D-32). Solo valida en nota de credito; lo comprueba el handler.';
COMMENT ON COLUMN FED.FED_NOTA.ORIGEN_NUMERACION   IS 'Instantanea del Art. 23: es contenido impreso, no un JOIN (D-29).';
COMMENT ON COLUMN FED.FED_NOTA.ORIGEN_MONEDA       IS 'Sin la moneda, el monto del Art. 23 no es una referencia sino un numero.';
