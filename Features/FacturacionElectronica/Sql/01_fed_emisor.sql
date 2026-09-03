-- =============================================================================
-- Facturacion Electronica (FED) - Requerimiento 32 - Fase 1
--
-- Tabla FED_EMISOR. El emisor es la raiz del modulo: la secuencia de numero de
-- control le pertenece (Art. 30, "consecutiva y unica para cada emisor").
--
-- Se ejecuta conectado como el rol fed, no como superusuario. El bootstrap
-- (script 00) ya dejo el schema con fed como propietario.
--
-- Identificadores calificados con el schema a proposito: sin calificar, un
-- search_path distinto crearia los objetos en public, que es de report-server, y
-- DROP SCHEMA FED CASCADE no los limpiaria. Sin comillas, tambien a proposito:
-- PostgreSQL los pliega a minusculas de forma pareja.
-- =============================================================================

CREATE TABLE IF NOT EXISTS FED.FED_EMISOR (
    ID                BIGINT       GENERATED ALWAYS AS IDENTITY PRIMARY KEY,

    -- Art. 30: la secuencia de numero de control pertenece al RIF. El UNIQUE no
    -- es una preferencia de diseno, es lo que sostiene la unicidad por emisor.
    RIF               VARCHAR(20)  NOT NULL,

    -- Art. 29.3: la imprenta digital estampa estos datos en el documento.
    RAZON_SOCIAL      VARCHAR(200) NOT NULL,
    DOMICILIO_FISCAL  VARCHAR(300) NOT NULL,
    CORREO            VARCHAR(150),

    ESTADO            VARCHAR(20)  NOT NULL DEFAULT 'activo',

    -- Auditoria de alta y modificacion, con la nomenclatura del proyecto.
    USUARIO_INS       VARCHAR(50)  NOT NULL,
    FECHA_INS         TIMESTAMPTZ  NOT NULL DEFAULT now(),
    USUARIO_UPD       VARCHAR(50),
    FECHA_UPD         TIMESTAMPTZ,

    CONSTRAINT FED_EMISOR_RIF_UK     UNIQUE (RIF),
    CONSTRAINT FED_EMISOR_ESTADO_CK  CHECK (ESTADO IN ('activo', 'inactivo'))
);

COMMENT ON TABLE  FED.FED_EMISOR      IS 'Emisores autorizados a los que la imprenta digital presta servicio. Requerimiento 32.';
COMMENT ON COLUMN FED.FED_EMISOR.RIF  IS 'Art. 30: la secuencia de numero de control es unica por emisor, atada a este RIF.';
