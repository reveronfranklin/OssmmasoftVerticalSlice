-- =============================================================================
-- Facturacion Electronica (FED) - Requerimiento 32 - Fase 3
--
-- Tabla FED_REPORTE_MENSUAL. Aqui se sostiene INV-2:
--
--   Nunca dejar de informar al SENIAT la totalidad de numeros de control
--   asignados en un periodo mensual. DOS periodos omitidos en un ano calendario,
--   consecutivos o no, bastan para la revocatoria, y el Art. 34.3 no exige
--   sancion previa.
--
-- LA DECISION QUE GOBIERNA ESTA TABLA: la fila del periodo se crea POR
-- ADELANTADO, en estado pendiente, y el job la marca. No se crea al enviar.
--
-- La diferencia no es cosmetica. Si la fila naciera al enviar, un periodo omitido
-- seria una fila AUSENTE, y nadie mira lo que no esta: habria que salir a
-- calcular que meses faltan. Creandola por adelantado, un periodo omitido es una
-- fila VISIBLE en estado vencido, que aparece sola en la pantalla de control y
-- dispara la alerta. Se convierte una ausencia -invisible- en un estado -visible-.
--
-- El plazo sale del Art. 29.7 de la 102 y coincide con el Art. 12 de la 0141:
-- diez dias continuos siguientes a la finalizacion de cada mes, "con
-- independencia de no haber asignado ningun numero de control". Por eso
-- CANTIDAD_REPORTADA admite cero y cero NO significa que no haya que reportar:
-- significa que se reporto que no hubo nada.
-- =============================================================================

CREATE TABLE IF NOT EXISTS FED.FED_REPORTE_MENSUAL (
    ID                  BIGINT      GENERATED ALWAYS AS IDENTITY PRIMARY KEY,

    -- AAAAMM. El UNIQUE es lo que hace idempotente al job: dos ticks sobre el
    -- mismo periodo no pueden crear dos filas, sin importar el reloj.
    PERIODO             CHAR(6)     NOT NULL,

    FECHA_CIERRE        DATE        NOT NULL,

    -- Art. 29.7: cierre + 10 dias continuos. Continuos, no habiles.
    FECHA_VENCE         DATE        NOT NULL,

    -- Nulo = todavia no se envio. No se usa un booleano: la fecha responde
    -- "cuando" ademas de "si", y eso es lo que hay que poder probarle al SENIAT.
    ENVIADO_EN          TIMESTAMPTZ,

    -- Cero es un valor valido y esperado.
    CANTIDAD_REPORTADA  INTEGER     NOT NULL DEFAULT 0,

    -- Ver la nota sobre los cuatro estados al pie de este script.
    ESTADO              VARCHAR(20) NOT NULL DEFAULT 'pendiente',

    -- Ultimo intento y su error, para que un fallo repetido no sea invisible.
    ULTIMO_INTENTO_EN   TIMESTAMPTZ,
    ULTIMO_ERROR        VARCHAR(500),

    USUARIO_INS         VARCHAR(50) NOT NULL DEFAULT 'sistema',
    FECHA_INS           TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT FED_REPORTE_MENSUAL_PER_UK UNIQUE (PERIODO),
    CONSTRAINT FED_REPORTE_MENSUAL_PER_CK CHECK (PERIODO ~ '^[0-9]{6}$'),
    CONSTRAINT FED_REPORTE_MENSUAL_EST_CK CHECK (ESTADO IN ('pendiente', 'generado', 'enviado', 'vencido')),
    CONSTRAINT FED_REPORTE_MENSUAL_CANT_CK CHECK (CANTIDAD_REPORTADA >= 0),

    -- Un periodo enviado tiene que tener fecha de envio, y uno sin enviar no
    -- puede tenerla. Evita el estado imposible que despues nadie sabe leer.
    CONSTRAINT FED_REPORTE_MENSUAL_ENV_CK
        CHECK ((ESTADO = 'enviado' AND ENVIADO_EN IS NOT NULL)
            OR (ESTADO <> 'enviado' AND ENVIADO_EN IS NULL))
);

-- La pantalla de control y el job preguntan lo mismo: que hay pendiente y
-- vencido. Se indexa esa pregunta.
CREATE INDEX IF NOT EXISTS FED_REPORTE_MENSUAL_EST_IX
    ON FED.FED_REPORTE_MENSUAL (ESTADO, FECHA_VENCE);

-- -----------------------------------------------------------------------------
-- Foranea diferida desde la Fase 2.
--
-- El script 03 creo FED_NUM_CONTROL.REPORTE_ID como columna suelta porque la
-- tabla a la que apunta no existia todavia. Declararla alli habria hecho que el
-- script no corriera. Se declaro la deuda y se paga aqui.
-- -----------------------------------------------------------------------------
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'fed_num_control_reporte_fk'
          AND conrelid = 'fed.fed_num_control'::regclass
    ) THEN
        ALTER TABLE FED.FED_NUM_CONTROL
            ADD CONSTRAINT FED_NUM_CONTROL_REPORTE_FK
            FOREIGN KEY (REPORTE_ID) REFERENCES FED.FED_REPORTE_MENSUAL (ID);
    END IF;
END
$$;

-- -----------------------------------------------------------------------------
-- LOS CUATRO ESTADOS, Y POR QUE SON CUATRO Y NO TRES (decision D-19)
--
--   pendiente  el periodo existe y su reporte todavia no se genero
--   generado   el reporte esta calculado y sus numeros atados a esta fila, pero
--              NO se transmitio al SENIAT
--   enviado    transmitido, con constancia en ENVIADO_EN
--   vencido    paso la FECHA_VENCE sin transmitir
--
-- El plan original tenia tres estados. Falta "generado" por una razon que no es
-- teorica: HOY NO EXISTE CANAL PARA TRANSMITIRLE AL SENIAT. El Art. 29.7 dice que
-- la informacion se remite "en los terminos y condiciones que se establezca en el
-- Portal Fiscal", y esas especificaciones no estan en nuestras manos -es el mismo
-- hueco que RES-5 y que el dato de autorizacion del 7.14-.
--
-- Sin este estado habria que elegir entre dos mentiras: marcar "enviado" algo que
-- nunca se envio, en la tabla con la que se le prueba al SENIAT que se reporto; o
-- dejarlo "pendiente" y que todo caiga en "vencido", con la alerta sonando para
-- siempre hasta volverse ruido que nadie mira. "generado" dice la verdad: el
-- trabajo del sistema esta hecho y falta el canal.
--
-- Cuando exista la integracion con el Portal Fiscal, el paso de generado a
-- enviado es lo unico que hay que agregar.
-- -----------------------------------------------------------------------------

COMMENT ON TABLE  FED.FED_REPORTE_MENSUAL                    IS 'Un periodo mensual por fila, creada POR ADELANTADO. Sostiene INV-2: un periodo omitido es una fila visible, no una ausencia. Art. 29.7.';
COMMENT ON COLUMN FED.FED_REPORTE_MENSUAL.ESTADO             IS 'pendiente | generado | enviado | vencido. "generado" = calculado pero no transmitido: hoy no hay canal al Portal Fiscal. Ver D-19.';
COMMENT ON COLUMN FED.FED_REPORTE_MENSUAL.PERIODO            IS 'AAAAMM. El UNIQUE es lo que hace idempotente al job del reporte.';
COMMENT ON COLUMN FED.FED_REPORTE_MENSUAL.FECHA_VENCE        IS 'Art. 29.7: cierre del mes mas diez dias CONTINUOS.';
COMMENT ON COLUMN FED.FED_REPORTE_MENSUAL.CANTIDAD_REPORTADA IS 'Cero es valido: significa que se reporto que no hubo asignaciones, no que no haya que reportar.';
