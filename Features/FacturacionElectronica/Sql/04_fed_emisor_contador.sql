-- =============================================================================
-- Facturacion Electronica (FED) - Requerimiento 32 - Fase 2
--
-- Tabla FED_EMISOR_CONTADOR. Una fila por emisor, que guarda el ULTIMO numero de
-- control asignado. Existe para una sola cosa: ser la fila que se bloquea.
--
-- Por que una tabla y no SELECT MAX()+1 sobre FED_NUM_CONTROL. Con MAX, dos
-- peticiones simultaneas leen el mismo maximo antes de que cualquiera inserte, y
-- las dos calculan el mismo siguiente valor: una inserta y la otra choca contra el
-- UNIQUE. Eso convierte una operacion normal en un error, y bajo carga el error
-- deja de ser raro. Con una fila por emisor y SELECT ... FOR UPDATE, la segunda
-- peticion espera a que la primera confirme y lee el valor ya actualizado.
--
-- El bloqueo es POR EMISOR, no global: dos emisores distintos asignan en paralelo
-- sin esperarse. Es la diferencia entre un cuello de botella y una cola por
-- cliente.
--
-- Valores iniciales, Art. 30: la primera asignacion de un emisor debe dar
-- identificador 00 y secuencial 1. Aqui se guarda el ULTIMO asignado, no el
-- proximo, asi que la fila nace en 00 / 0 y la primera asignacion la lleva a
-- 00 / 1. Guardar el ultimo y no el proximo evita tener que decidir que significa
-- el valor inicial de una fila que todavia no asigno nada.
--
-- Rotacion, decision D-2: al pasar de 99999999 se incrementa el identificador y el
-- secuencial vuelve a 1. El Art. 30 fija el inicio pero no dice cuando incrementa
-- el identificador; esta es la lectura que preserva la consecutividad por emisor.
-- El CHECK del secuencial admite 0 solo porque es el estado "todavia no asigno
-- nada"; FED_NUM_CONTROL no admite 0, ahi el minimo es 1.
-- =============================================================================

CREATE TABLE IF NOT EXISTS FED.FED_EMISOR_CONTADOR (
    EMISOR_ID      BIGINT      PRIMARY KEY,

    IDENTIFICADOR  CHAR(2)     NOT NULL DEFAULT '00',
    SECUENCIAL     INTEGER     NOT NULL DEFAULT 0,

    FECHA_UPD      TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT FED_EMI_CONTADOR_EMISOR_FK FOREIGN KEY (EMISOR_ID)
        REFERENCES FED.FED_EMISOR (ID),

    CONSTRAINT FED_EMI_CONTADOR_IDENT_CK CHECK (IDENTIFICADOR ~ '^[0-9]{2}$'),
    CONSTRAINT FED_EMI_CONTADOR_SEC_CK   CHECK (SECUENCIAL BETWEEN 0 AND 99999999)
);

COMMENT ON TABLE  FED.FED_EMISOR_CONTADOR               IS 'Ultimo numero de control asignado por emisor. Su fila es la que se bloquea con SELECT ... FOR UPDATE al asignar. Requerimiento 32, Fase 2.';
COMMENT ON COLUMN FED.FED_EMISOR_CONTADOR.IDENTIFICADOR IS 'Art. 30: dos digitos, inicia en 00. Rota al agotarse el secuencial (D-2).';
COMMENT ON COLUMN FED.FED_EMISOR_CONTADOR.SECUENCIAL    IS 'Ultimo secuencial asignado. 0 = el emisor todavia no recibio ninguno; la primera asignacion da 1.';
