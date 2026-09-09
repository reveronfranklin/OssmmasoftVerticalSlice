-- =============================================================================
-- Facturacion Electronica (FED) - Requerimiento 32 - Fase 8 (T8.4)
--
-- Tabla FED_CONTINGENCIA: bandeja de recepcion y conciliacion de documentos
-- fisicos emitidos por el emisor durante una contingencia (Art. 16).
--
-- POR QUE ESTA TABLA (D-4). El Art. 16 dice que los sujetos pasivos "podran
-- utilizar" medidas de contingencia -son opcionales para el emisor- en tres
-- escenarios: falla de internet, falla del dispositivo movil y falla del
-- servicio electrico. Superada la contingencia, el emisor debe REGISTRAR EN EL
-- SISTEMA los comprobantes fisicos emitidos. Esa obligacion de registro no es
-- opcional para el Rol A: es quien recibe la notificacion. D-4 dejo la app
-- movil y el modo offline fuera de esta version -son opcionales segun el
-- propio articulo-, pero esta bandeja si se construye.
--
-- LA NUMERACION FISICA NO ES UN NUMERO DE CONTROL. El Art. 16.3 dice que va
-- "precedida de la palabra contingencia seguida de caracteres que la
-- identifiquen y diferencien": es un dato que el emisor declara sobre su
-- talonario fisico, no algo que este modulo asigna. Por eso NUMERACION_FISICA
-- es texto libre con esa restriccion, y no se toca FED_EMISOR_CONTADOR ni
-- FED_NUM_CONTROL para nada de esta tabla.
--
-- CONCILIAR ES ASOCIAR, NO EMITIR. DOCUMENTO_ID queda nulo hasta que alguien
-- registre en el sistema el documento que corresponde a ese fisico -que puede
-- o no pasar por facturaCreate, segun si el emisor quiere reconstruirlo como
-- documento digital-. Esta tabla solo dice "esto se noto por fuera" y despues
-- "esto es lo que le corresponde adentro"; no crea el documento por su cuenta.
--
-- APPEND-ONLY salvo la conciliacion, que completa un dato pendiente -mismo
-- criterio que ENTREGADO_EN en FED_RETENCION-, no enmienda el registro de
-- notificacion.
-- =============================================================================

CREATE TABLE IF NOT EXISTS FED.FED_CONTINGENCIA (
    ID                     BIGINT        GENERATED ALWAYS AS IDENTITY PRIMARY KEY,

    EMISOR_ID              BIGINT        NOT NULL,

    -- Art. 16.3 - "contingencia" + caracteres que la identifiquen y diferencien.
    -- Declarada por el emisor, no asignada por este modulo.
    NUMERACION_FISICA      VARCHAR(50)   NOT NULL,

    FECHA_EMISION_FISICA   DATE          NOT NULL,

    -- Los tres escenarios del Art. 16: falla de internet, del dispositivo
    -- movil, o del servicio electrico.
    ESCENARIO              VARCHAR(20)   NOT NULL,

    -- Cuando el emisor notifica a la imprenta digital (Art. 16, paso 2 de la
    -- regularizacion).
    NOTIFICADO_EN          TIMESTAMPTZ   NOT NULL DEFAULT now(),

    -- Nulo hasta que se asocia con el documento que lo regulariza adentro del
    -- sistema.
    CONCILIADO_EN          TIMESTAMPTZ,
    DOCUMENTO_ID           BIGINT,
    USUARIO_CONCILIA       VARCHAR(50),

    USUARIO_INS            VARCHAR(50)   NOT NULL,
    FECHA_INS              TIMESTAMPTZ   NOT NULL DEFAULT now(),

    CONSTRAINT FED_CONTING_EMISOR_FK FOREIGN KEY (EMISOR_ID)
        REFERENCES FED.FED_EMISOR (ID),

    CONSTRAINT FED_CONTING_DOC_FK FOREIGN KEY (DOCUMENTO_ID)
        REFERENCES FED.FED_DOCUMENTO (ID),

    -- La misma numeracion fisica notificada dos veces para el mismo emisor
    -- seria la misma contingencia contada por duplicado.
    CONSTRAINT FED_CONTING_UK UNIQUE (EMISOR_ID, NUMERACION_FISICA),

    CONSTRAINT FED_CONTING_ESCENARIO_CK
        CHECK (ESCENARIO IN ('internet', 'dispositivo', 'electrico')),

    -- Precedida de "contingencia", literal del Art. 16.3.
    CONSTRAINT FED_CONTING_NUM_CK
        CHECK (NUMERACION_FISICA ~* '^contingencia'),

    -- Las dos mitades de la conciliacion llegan juntas o no llegan.
    CONSTRAINT FED_CONTING_CONCILIA_CK CHECK (
        (CONCILIADO_EN IS NULL AND DOCUMENTO_ID IS NULL AND USUARIO_CONCILIA IS NULL)
        OR (CONCILIADO_EN IS NOT NULL AND DOCUMENTO_ID IS NOT NULL AND USUARIO_CONCILIA IS NOT NULL)
    )
);

CREATE INDEX IF NOT EXISTS FED_CONTING_EMISOR_IX
    ON FED.FED_CONTINGENCIA (EMISOR_ID);

-- Para la bandeja: lo pendiente de conciliar es la consulta natural de esta
-- tabla. Parcial porque solo indexa lo que todavia hace falta mirar.
CREATE INDEX IF NOT EXISTS FED_CONTING_PENDIENTE_IX
    ON FED.FED_CONTINGENCIA (EMISOR_ID)
    WHERE CONCILIADO_EN IS NULL;

COMMENT ON TABLE  FED.FED_CONTINGENCIA IS
    'Bandeja de recepcion y conciliacion de documentos fisicos de contingencia (Art. 16, D-4). Notificacion append-only; la conciliacion completa un dato pendiente, no enmienda.';
COMMENT ON COLUMN FED.FED_CONTINGENCIA.NUMERACION_FISICA IS
    'Art. 16.3: precedida de "contingencia" mas caracteres que la identifiquen y diferencien. Declarada por el emisor.';
COMMENT ON COLUMN FED.FED_CONTINGENCIA.DOCUMENTO_ID IS
    'Nulo hasta la conciliacion. El documento que regulariza el fisico notificado, si el emisor lo reconstruye en el sistema.';
