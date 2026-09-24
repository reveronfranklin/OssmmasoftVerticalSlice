-- =============================================================================
-- Facturacion Electronica (FED) - Requerimiento 32 - Fase M, TM.4
--
-- Tabla FED_EMISOR_CUPO. Cupo de documentos por emisor: cuantos numeros de
-- control puede consumir hasta que renueve. Es una regla COMERCIAL de la
-- imprenta, no de la Providencia 102: ningun articulo la pide ni la prohibe.
-- Decision D-54.
--
-- Una fila por carga o renovacion, y la tabla es de SOLO INSERCION: recibe
-- SELECT + INSERT del ALTER DEFAULT PRIVILEGES del script 00 y ningun UPDATE en
-- GRANTS_FED_APP.sql. Renovar no sobrescribe el cupo anterior, le suma una fila:
-- asi queda trazado cuando se renovo, cuanto y quien.
--
-- Que descuenta del cupo: todo numero de control del emisor -factura, notas,
-- guia y la asignacion manual sin documento (D-16)-. La retencion no, porque no
-- lleva numero de control (D-37).
--
-- Como se calcula el disponible, y por que CONSUMIDO_AL_CARGAR:
--
--     disponible = SUM(CANTIDAD) - (consumido_actual - MIN(CONSUMIDO_AL_CARGAR))
--
-- "consumido" es la posicion del ultimo numero asignado en la secuencia del
-- emisor: IDENTIFICADOR * 99999999 + SECUENCIAL, leida de FED_EMISOR_CONTADOR.
-- Esa fila se BLOQUEA tanto al asignar como al cargar un cupo, asi que el valor
-- guardado en CONSUMIDO_AL_CARGAR es exacto. No se usan fechas: comparar
-- FECHA_ASIGNACION contra la fecha de la carga falla cuando una transaccion que
-- empezo antes confirma despues.
--
-- El MIN es la base del PRIMER cupo: lo consumido antes de que el emisor tuviera
-- cupo no cuenta. Un emisor SIN ninguna fila queda sin limite, que es como
-- siguen funcionando los emisores que ya existian.
--
-- Reejecutable: CREATE ... IF NOT EXISTS.
-- =============================================================================

CREATE TABLE IF NOT EXISTS FED.FED_EMISOR_CUPO (
    ID                   BIGINT       GENERATED ALWAYS AS IDENTITY PRIMARY KEY,

    EMISOR_ID            BIGINT       NOT NULL,

    -- Cuantos numeros de control agrega esta carga. El tope es el de un
    -- secuencial del Art. 30: un cupo mayor no tiene sentido operativo.
    CANTIDAD             INTEGER      NOT NULL,

    -- Posicion de la secuencia del emisor al momento de cargar, leida bajo el
    -- bloqueo del contador. 0 = el emisor todavia no habia recibido numeros.
    CONSUMIDO_AL_CARGAR  BIGINT       NOT NULL,

    USUARIO_INS          VARCHAR(50)  NOT NULL,
    FECHA_INS            TIMESTAMPTZ  NOT NULL DEFAULT now(),

    CONSTRAINT FED_EMI_CUPO_EMISOR_FK FOREIGN KEY (EMISOR_ID)
        REFERENCES FED.FED_EMISOR (ID),

    CONSTRAINT FED_EMI_CUPO_CANT_CK  CHECK (CANTIDAD BETWEEN 1 AND 99999999),
    CONSTRAINT FED_EMI_CUPO_BASE_CK  CHECK (CONSUMIDO_AL_CARGAR >= 0)
);

CREATE INDEX IF NOT EXISTS FED_EMI_CUPO_EMISOR_IX
    ON FED.FED_EMISOR_CUPO (EMISOR_ID);

COMMENT ON TABLE  FED.FED_EMISOR_CUPO                     IS 'Cupo de documentos por emisor: una fila por carga o renovacion, solo insercion. Sin filas = sin limite. D-54, Fase M.';
COMMENT ON COLUMN FED.FED_EMISOR_CUPO.CANTIDAD            IS 'Numeros de control que agrega esta carga.';
COMMENT ON COLUMN FED.FED_EMISOR_CUPO.CONSUMIDO_AL_CARGAR IS 'IDENTIFICADOR * 99999999 + SECUENCIAL del contador del emisor al cargar, leido bajo su bloqueo. El MIN es la base del primer cupo.';
