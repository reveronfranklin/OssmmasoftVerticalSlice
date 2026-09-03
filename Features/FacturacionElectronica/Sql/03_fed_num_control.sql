-- =============================================================================
-- Facturacion Electronica (FED) - Requerimiento 32 - Fase 2
--
-- Tabla FED_NUM_CONTROL. Es el corazon del Rol A: aqui vive cada numero de
-- control asignado, y aqui se sostiene INV-1 -nunca dos numeros de control
-- distintos para el mismo documento de un mismo emisor, Art. 34.2, causal de
-- revocatoria de la autorizacion de Ossmmasoft como imprenta digital-.
--
-- La unicidad la garantiza la BASE DE DATOS, no la aplicacion. Los dos UNIQUE de
-- abajo son la ultima linea de defensa; el bloqueo del contador (script 04) es la
-- primera. Ninguna de las dos alcanza sola.
--
-- Art. 30, formato del numero: la frase "N de Control", un identificador de dos
-- digitos y un secuencial de hasta ocho. Se guardan separados y no concatenados
-- para poder consultarlos, ordenarlos y detectar huecos.
--
-- Vale lo mismo que en el script 01: identificadores calificados con el schema, y
-- MAYUSCULA SIN COMILLAS por la decision D-14 -PostgreSQL pliega a minuscula, asi
-- que la tabla existe fisicamente como fed.fed_num_control-.
--
-- DESVIACION DECLARADA (R7 de CodeStandards). El MER de ModeloDatos.md declara
-- DOCUMENTO_ID y REPORTE_ID como claves foraneas, pero las tablas a las que
-- apuntan nacen despues: FED_REPORTE_MENSUAL en la Fase 3 (script 06) y
-- FED_DOCUMENTO en la Fase 4 (script 07). Declararlas aqui haria que este script
-- no corriera, y adelantar esas tablas metaria en la fase de mayor riesgo trabajo
-- que no le pertenece. Se crean como columnas con su UNIQUE -que es lo que INV-1
-- necesita- y cada fase posterior agrega SU PROPIA foranea por ALTER TABLE, en su
-- script. La integridad referencial llega completa; solo llega por partes.
-- =============================================================================

CREATE TABLE IF NOT EXISTS FED.FED_NUM_CONTROL (
    ID                BIGINT      GENERATED ALWAYS AS IDENTITY PRIMARY KEY,

    -- Art. 30: la secuencia es "consecutiva y unica para cada emisor". Por eso el
    -- emisor entra en la clave de unicidad y no es un dato descriptivo.
    EMISOR_ID         BIGINT      NOT NULL,

    -- El documento al que se le asigno este numero. Nulo mientras el Rol B no
    -- exista: en la Fase 2 el endpoint puede asignar sin documento todavia. En
    -- PostgreSQL varios NULL no chocan entre si en un UNIQUE, asi que la
    -- restriccion de abajo permite muchas asignaciones sin documento y prohibe
    -- dos numeros para un mismo documento, que es exactamente INV-1.
    DOCUMENTO_ID      BIGINT,

    -- Art. 30: identificador de dos digitos, inicia en 00. Rota al agotarse el
    -- secuencial, decision D-2.
    IDENTIFICADOR     CHAR(2)     NOT NULL,

    -- Art. 30: secuencial de hasta ocho digitos, inicia en 1.
    SECUENCIAL        INTEGER     NOT NULL,

    TIPO_DOCUMENTO    VARCHAR(20) NOT NULL,

    -- Art. 32 numeral 2, y Art. 7.15: la fecha de asignacion se estampa en el
    -- documento en ocho digitos.
    FECHA_ASIGNACION  TIMESTAMPTZ NOT NULL DEFAULT now(),

    -- Periodo mensual en el que se reporto al SENIAT. Nulo = aun no reportado.
    -- La foranea a FED_REPORTE_MENSUAL la agrega la Fase 3.
    REPORTE_ID        BIGINT,

    USUARIO_INS       VARCHAR(50) NOT NULL,
    FECHA_INS         TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT FED_NUM_CONTROL_EMISOR_FK FOREIGN KEY (EMISOR_ID)
        REFERENCES FED.FED_EMISOR (ID),

    -- INV-1, primera mitad: un emisor no puede recibir dos veces el mismo par
    -- identificador + secuencial.
    CONSTRAINT FED_NUM_CONTROL_SEC_UK  UNIQUE (EMISOR_ID, IDENTIFICADOR, SECUENCIAL),

    -- INV-1, segunda mitad: un documento no puede llevar dos numeros de control.
    CONSTRAINT FED_NUM_CONTROL_DOC_UK  UNIQUE (DOCUMENTO_ID),

    CONSTRAINT FED_NUM_CONTROL_IDENT_CK CHECK (IDENTIFICADOR ~ '^[0-9]{2}$'),
    CONSTRAINT FED_NUM_CONTROL_SEC_CK   CHECK (SECUENCIAL BETWEEN 1 AND 99999999),
    CONSTRAINT FED_NUM_CONTROL_TIPO_CK  CHECK (TIPO_DOCUMENTO IN ('factura', 'debito', 'credito', 'entrega'))
);

-- Art. 32 numeral 1: el registro se consulta e indexa por RIF del emisor. Aqui el
-- acceso natural es por emisor y fecha de asignacion, que es como lo pide el
-- reporte mensual del Art. 29.7 y la vista de consulta de T2.9.
CREATE INDEX IF NOT EXISTS FED_NUM_CONTROL_EMI_FEC_IX
    ON FED.FED_NUM_CONTROL (EMISOR_ID, FECHA_ASIGNACION);

COMMENT ON TABLE  FED.FED_NUM_CONTROL                  IS 'Numeros de control asignados por la imprenta digital. Art. 30 y 32. Sostiene INV-1. Requerimiento 32.';
COMMENT ON COLUMN FED.FED_NUM_CONTROL.DOCUMENTO_ID     IS 'Documento al que se asigno. UNIQUE: dos numeros para el mismo documento es la causal del Art. 34.2. Foranea a FED_DOCUMENTO en la Fase 4.';
COMMENT ON COLUMN FED.FED_NUM_CONTROL.IDENTIFICADOR    IS 'Art. 30: dos digitos, inicia en 00, rota al agotarse el secuencial (D-2).';
COMMENT ON COLUMN FED.FED_NUM_CONTROL.SECUENCIAL       IS 'Art. 30: hasta ocho digitos, inicia en 1.';
COMMENT ON COLUMN FED.FED_NUM_CONTROL.REPORTE_ID       IS 'Periodo en que se informo al SENIAT. Nulo = pendiente. Foranea a FED_REPORTE_MENSUAL en la Fase 3.';
