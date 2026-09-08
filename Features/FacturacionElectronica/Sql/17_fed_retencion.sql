-- =============================================================================
-- Facturacion Electronica (FED) - Requerimiento 32 - Fase 6B
--
-- Tabla FED_RETENCION: el comprobante de retencion del Art. 11 de la
-- Providencia SNAT/2024/000102, mas su contador.
--
-- POR QUE ESTE DOCUMENTO ENTRO AL ALCANCE. Estaba declarado fuera como FA-6,
-- condicionado a resolver si los clientes eran agentes de retencion. Lo son, y
-- no hizo falta preguntarlo: Features/ReporteComprobanteIva/ ya devuelve
-- NombreAgenteRetencion, RifAgenteRetencion y NumeroComprobante atados a la
-- orden de pago. El ERP ya les produce ese comprobante hoy. Dejarlo afuera
-- significaba que, con el resto de sus documentos en medios digitales,
-- siguieran emitiendo este por la via vieja.
--
-- POR QUE TABLA PROPIA Y NO UN QUINTO TIPO DE FED_DOCUMENTO (D-37). Porque el
-- Art. 11 NO remite a los numerales 4 y 5 del Art. 7, como si hace el Art. 10.2
-- para la guia de despacho:
--
--   EL COMPROBANTE DE RETENCION NO LLEVA NUMERO DE CONTROL PROPIO.
--
-- Su numeral 5 pide el numero de control DE LA FACTURA QUE SE RETIENE, no uno
-- suyo. Su identificacion propia es la numeracion de catorce caracteres del
-- 11.1. Meterlo en FED_DOCUMENTO habria obligado a tres cosas malas a la vez:
-- relajar el CHECK del adquiriente -aca hay proveedor, no adquiriente-, darle
-- otra semantica a NUMERACION -ahi es la del Art. 7.2, aca el formato de
-- catorce caracteres- y romper el supuesto de que todo documento tiene numero
-- de control. Ese ultimo no es teorico: la grilla de documentos ya marca como
-- ANOMALIA un documento sin numero de control, y cada comprobante habria
-- aparecido marcado en rojo.
--
-- PERO EL ROL A INTERVIENE IGUAL (D-39). El numeral 11.9 exige "los datos de la
-- imprenta digital autorizada". Es el unico documento del alcance donde la
-- imprenta aporta identidad sin aportar numeracion, y por eso la instantanea de
-- imprenta esta aca igual que en FED_DOCUMENTO.
--
-- APPEND-ONLY, como todo documento fiscal.
-- =============================================================================

CREATE TABLE IF NOT EXISTS FED.FED_RETENCION (
    ID                    BIGINT        GENERATED ALWAYS AS IDENTITY PRIMARY KEY,

    -- El agente de retencion es el emisor del comprobante (11.2).
    EMISOR_ID             BIGINT        NOT NULL,

    -- 11.1 - catorce caracteres, AAAAMMSSSSSSSS. No es el numero de control:
    -- este documento no tiene uno.
    NUMERACION            CHAR(14)      NOT NULL,

    -- 11.7 - periodo de imposicion, AAAAMM. Se guarda aparte de la numeracion
    -- aunque este embebido en ella: filtrar por un prefijo de CHAR(14) no usa
    -- indice, y el reporte por periodo es la consulta natural de este documento.
    PERIODO               CHAR(6)       NOT NULL,

    -- 11.3 - fecha de emision Y de entrega. Son dos fechas distintas y el
    -- numeral pide las dos; la de entrega puede no existir todavia.
    EMITIDO_EN            TIMESTAMPTZ   NOT NULL DEFAULT now(),
    ENTREGADO_EN          TIMESTAMPTZ,

    -- 11.2 - instantanea del agente de retencion, por el mismo principio del
    -- Art. 29.3 que rige en FED_DOCUMENTO: lo estampado es un hecho historico.
    AGENTE_RIF            VARCHAR(20)   NOT NULL,
    AGENTE_RAZON_SOCIAL   VARCHAR(200)  NOT NULL,
    AGENTE_DOMICILIO      VARCHAR(300)  NOT NULL,

    -- 11.4 - datos del proveedor, con su correo, que el numeral pide expreso.
    PROVEEDOR_RIF         VARCHAR(20)   NOT NULL,
    PROVEEDOR_RAZON_SOCIAL VARCHAR(200) NOT NULL,
    PROVEEDOR_DOMICILIO   VARCHAR(300),
    PROVEEDOR_CORREO      VARCHAR(150),

    -- 11.8 - los cuatro montos, agregados del detalle. Se guardan y no se
    -- calculan al leer, igual que los totales de FED_DOCUMENTO: lo impreso no
    -- puede cambiar porque cambie una suma.
    TOTAL_DOCUMENTOS      NUMERIC(18,2) NOT NULL DEFAULT 0,
    TOTAL_BASE            NUMERIC(18,2) NOT NULL DEFAULT 0,
    TOTAL_IMPUESTO        NUMERIC(18,2) NOT NULL DEFAULT 0,
    TOTAL_RETENIDO        NUMERIC(18,2) NOT NULL DEFAULT 0,

    -- 11.9 - datos de la imprenta digital autorizada. Mismo tratamiento que en
    -- FED_DOCUMENTO: mientras el SENIAT no autorice, van vacios y el documento
    -- es de prueba.
    IMPRENTA_RIF          VARCHAR(20),
    IMPRENTA_RAZON_SOCIAL VARCHAR(200),
    IMPRENTA_PROVIDENCIA  VARCHAR(150),
    ES_PRUEBA             BOOLEAN       NOT NULL DEFAULT TRUE,

    -- Misma proteccion contra el doble envio que la emision de documentos: sin
    -- ella, un doble clic retiene dos veces la misma factura.
    CLAVE_IDEMPOTENCIA    VARCHAR(80),

    USUARIO_INS           VARCHAR(50)   NOT NULL,
    FECHA_INS             TIMESTAMPTZ   NOT NULL DEFAULT now(),

    CONSTRAINT FED_RETENCION_EMISOR_FK FOREIGN KEY (EMISOR_ID)
        REFERENCES FED.FED_EMISOR (ID),

    -- Dos comprobantes con el mismo numero para el mismo agente serian dos
    -- documentos fiscales identificados igual. Es la misma clase de invariante
    -- que INV-3 sostiene para la factura.
    CONSTRAINT FED_RETENCION_UK UNIQUE (EMISOR_ID, NUMERACION),

    CONSTRAINT FED_RETENCION_IDEM_UK UNIQUE (EMISOR_ID, CLAVE_IDEMPOTENCIA),

    -- 11.1 literal: catorce digitos, y los seis primeros son un periodo valido.
    CONSTRAINT FED_RETENCION_NUM_CK CHECK (NUMERACION ~ '^[0-9]{14}$'),
    CONSTRAINT FED_RETENCION_PERIODO_CK CHECK (PERIODO ~ '^[0-9]{6}$'),

    -- La numeracion embebe el periodo: si difirieran, el documento se
    -- contradiria a si mismo.
    CONSTRAINT FED_RETENCION_COHER_CK CHECK (SUBSTR(NUMERACION, 1, 6) = PERIODO),

    CONSTRAINT FED_RETENCION_MONTOS_CK CHECK (
        TOTAL_DOCUMENTOS >= 0 AND TOTAL_BASE >= 0
        AND TOTAL_IMPUESTO >= 0 AND TOTAL_RETENIDO >= 0),

    -- No se puede retener mas impuesto del que se causo.
    CONSTRAINT FED_RETENCION_RETENIDO_CK CHECK (TOTAL_RETENIDO <= TOTAL_IMPUESTO),

    -- Mismo criterio que FED_DOCUMENTO: un comprobante DEFINITIVO no puede
    -- existir sin los datos de la imprenta del numeral 11.9.
    CONSTRAINT FED_RETENCION_IMPRENTA_CK CHECK (
        ES_PRUEBA
        OR (IMPRENTA_RIF IS NOT NULL AND IMPRENTA_RAZON_SOCIAL IS NOT NULL
            AND IMPRENTA_PROVIDENCIA IS NOT NULL))
);

CREATE INDEX IF NOT EXISTS FED_RETENCION_EMI_PER_IX
    ON FED.FED_RETENCION (EMISOR_ID, PERIODO);

-- -----------------------------------------------------------------------------
-- Contador del secuencial (D-38)
--
-- Una fila por agente y periodo. El secuencial REINICIA CADA MES, y eso es una
-- interpretacion declarada: el Art. 11.1 dice que reinicia "si se supera dicha
-- cantidad" -los ocho digitos-, pero si no reiniciara cada mes el AAAAMM del
-- prefijo no cumpliria ninguna funcion. Si un revisor lee lo contrario, lo que
-- cambia es la clave de esta tabla, no el documento.
--
-- Mismo mecanismo de bloqueo que los otros dos contadores del modulo, y por la
-- misma razon: SELECT MAX() + 1 esta roto bajo concurrencia, y eso ya quedo
-- demostrado en la Fase 2 -la forma prohibida dejo 17 asignaciones de 300-.
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS FED.FED_RETENCION_CONTADOR (
    EMISOR_ID     BIGINT      NOT NULL,
    PERIODO       CHAR(6)     NOT NULL,

    -- Ultimo secuencial asignado. Nace en 0 y el primero del mes da 1.
    ULTIMO_NUMERO BIGINT      NOT NULL DEFAULT 0,

    FECHA_UPD     TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT FED_RETENCION_CONT_PK PRIMARY KEY (EMISOR_ID, PERIODO),

    CONSTRAINT FED_RETENCION_CONT_EMI_FK FOREIGN KEY (EMISOR_ID)
        REFERENCES FED.FED_EMISOR (ID),

    CONSTRAINT FED_RETENCION_CONT_PER_CK CHECK (PERIODO ~ '^[0-9]{6}$'),

    -- Ocho digitos: el formato del 11.1 no admite mas.
    CONSTRAINT FED_RETENCION_CONT_NUM_CK
        CHECK (ULTIMO_NUMERO >= 0 AND ULTIMO_NUMERO <= 99999999)
);

COMMENT ON TABLE  FED.FED_RETENCION            IS 'Comprobante de retencion del Art. 11 de la SNAT/2024/000102. NO lleva numero de control (D-37). APPEND-ONLY.';
COMMENT ON COLUMN FED.FED_RETENCION.NUMERACION IS 'Art. 11.1: catorce caracteres AAAAMMSSSSSSSS. No es un numero de control.';
COMMENT ON COLUMN FED.FED_RETENCION.PERIODO    IS 'Art. 11.7. Redundante con el prefijo de NUMERACION a proposito: un prefijo de CHAR no se indexa.';
COMMENT ON TABLE  FED.FED_RETENCION_CONTADOR   IS 'Ultimo secuencial por agente y periodo. Reinicia cada mes (D-38, interpretacion declarada). Su fila se bloquea al emitir.';
