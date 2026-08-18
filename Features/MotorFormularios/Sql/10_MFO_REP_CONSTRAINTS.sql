-- =============================================================================
-- Motor de Formularios (MFO) - Fase 1 - Restricciones del modo reporte
-- Requerimiento 16.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- Claves primarias y unicas
-- -----------------------------------------------------------------------------
ALTER TABLE MFO_REPORTE     ADD CONSTRAINT PK_MFO_REPORTE     PRIMARY KEY (REPORTE_ID);
ALTER TABLE MFO_REP_PARAM   ADD CONSTRAINT PK_MFO_REP_PARAM   PRIMARY KEY (REP_PARAM_ID);
ALTER TABLE MFO_REP_COLUMNA ADD CONSTRAINT PK_MFO_REP_COLUMNA PRIMARY KEY (REP_COLUMNA_ID);
ALTER TABLE MFO_REP_EJEC    ADD CONSTRAINT PK_MFO_REP_EJEC    PRIMARY KEY (REP_EJEC_ID);

ALTER TABLE MFO_REPORTE     ADD CONSTRAINT UK_MFO_REPORTE_CLAVE UNIQUE (FORMULARIO_ID, CLAVE);
ALTER TABLE MFO_REP_PARAM   ADD CONSTRAINT UK_MFO_REP_PARAM_NOM UNIQUE (REPORTE_ID, NOMBRE_PARAM);
ALTER TABLE MFO_REP_COLUMNA ADD CONSTRAINT UK_MFO_REP_COL_NOM   UNIQUE (REPORTE_ID, NOMBRE_COL);

-- -----------------------------------------------------------------------------
-- CHECK - columnas nuevas de MFO_FORMULARIO
-- -----------------------------------------------------------------------------
ALTER TABLE MFO_FORMULARIO ADD CONSTRAINT CK_MFO_FORM_MODO_USO
    CHECK (MODO_USO IN ('CAPTURA', 'PARAMETROS', 'MIXTO'));
ALTER TABLE MFO_FORMULARIO ADD CONSTRAINT CK_MFO_FORM_REG_EJEC
    CHECK (REGISTRA_EJEC IN ('S', 'N'));

-- -----------------------------------------------------------------------------
-- CHECK - MFO_REPORTE
-- -----------------------------------------------------------------------------
ALTER TABLE MFO_REPORTE ADD CONSTRAINT CK_MFO_REPORTE_TIPO_EJ
    CHECK (TIPO_EJEC IN ('ENDPOINT', 'SP_CURSOR'));
ALTER TABLE MFO_REPORTE ADD CONSTRAINT CK_MFO_REPORTE_ORIENT
    CHECK (ORIENTACION IN ('VERTICAL', 'HORIZONTAL'));
ALTER TABLE MFO_REPORTE ADD CONSTRAINT CK_MFO_REPORTE_ACTIVO
    CHECK (ACTIVO IN ('S', 'N'));

-- -----------------------------------------------------------------------------
-- CHECK - MFO_REP_PARAM
--
-- CK_MFO_REP_PARAM_COHER es el que sostiene el modelo de origen: exactamente una
-- de las tres fuentes poblada, y la que corresponda al ORIGEN declarado. Sin el,
-- una fila podria decir ORIGEN='SISTEMA' y traer ademas un CAMPO_ID, dejando
-- ambiguo de donde sale el valor, que es justo lo que no puede quedar ambiguo en
-- un parametro que decide de que empresa son los datos.
-- -----------------------------------------------------------------------------
ALTER TABLE MFO_REP_PARAM ADD CONSTRAINT CK_MFO_REP_PARAM_ORIGEN
    CHECK (ORIGEN IN ('CAMPO', 'FIJO', 'SISTEMA'));

ALTER TABLE MFO_REP_PARAM ADD CONSTRAINT CK_MFO_REP_PARAM_SIS
    CHECK (CLAVE_SISTEMA IS NULL
        OR CLAVE_SISTEMA IN ('CODIGO_EMPRESA', 'USUARIO', 'FECHA_ACTUAL', 'IP_ORIGEN'));

ALTER TABLE MFO_REP_PARAM ADD CONSTRAINT CK_MFO_REP_PARAM_COHER
    CHECK ((ORIGEN = 'CAMPO'   AND CAMPO_ID IS NOT NULL AND VALOR_FIJO IS NULL     AND CLAVE_SISTEMA IS NULL)
        OR (ORIGEN = 'FIJO'    AND CAMPO_ID IS NULL     AND VALOR_FIJO IS NOT NULL AND CLAVE_SISTEMA IS NULL)
        OR (ORIGEN = 'SISTEMA' AND CAMPO_ID IS NULL     AND VALOR_FIJO IS NULL     AND CLAVE_SISTEMA IS NOT NULL));

ALTER TABLE MFO_REP_PARAM ADD CONSTRAINT CK_MFO_REP_PARAM_TIPO
    CHECK (TIPO_DATO IN ('TEXTO', 'NUMERO', 'FECHA'));
ALTER TABLE MFO_REP_PARAM ADD CONSTRAINT CK_MFO_REP_PARAM_OBLIG
    CHECK (OBLIGATORIO IN ('S', 'N'));

-- -----------------------------------------------------------------------------
-- CHECK - MFO_REP_COLUMNA
-- -----------------------------------------------------------------------------
ALTER TABLE MFO_REP_COLUMNA ADD CONSTRAINT CK_MFO_REP_COL_ALINEA
    CHECK (ALINEACION IN ('IZQ', 'CEN', 'DER'));
ALTER TABLE MFO_REP_COLUMNA ADD CONSTRAINT CK_MFO_REP_COL_TOTAL
    CHECK (TOTALIZAR IN ('S', 'N'));
ALTER TABLE MFO_REP_COLUMNA ADD CONSTRAINT CK_MFO_REP_COL_AGRUPAR
    CHECK (AGRUPAR IN ('S', 'N'));
ALTER TABLE MFO_REP_COLUMNA ADD CONSTRAINT CK_MFO_REP_COL_VISIBLE
    CHECK (VISIBLE IN ('S', 'N'));

-- -----------------------------------------------------------------------------
-- CHECK - MFO_REP_EJEC
-- TRUNCADO distingue el caso en que se alcanzo MAX_FILAS, que de otro modo se
-- confundiria con un reporte legitimamente corto.
-- -----------------------------------------------------------------------------
ALTER TABLE MFO_REP_EJEC ADD CONSTRAINT CK_MFO_REP_EJEC_RESULT
    CHECK (RESULTADO IN ('OK', 'ERROR', 'VACIO', 'TRUNCADO'));

-- -----------------------------------------------------------------------------
-- Claves foraneas
--
-- MFO_REP_EJEC no lleva cascada desde MFO_REPORTE: la bitacora de ejecuciones
-- debe sobrevivir al retiro de un reporte, igual que MFO_AUDITORIA sobrevive a
-- lo auditado. Retirar un reporte se hace con ACTIVO='N', no borrandolo.
-- -----------------------------------------------------------------------------
ALTER TABLE MFO_REPORTE ADD CONSTRAINT FK_MFO_REPORTE_FORM
    FOREIGN KEY (FORMULARIO_ID) REFERENCES MFO_FORMULARIO (FORMULARIO_ID);

ALTER TABLE MFO_REP_PARAM ADD CONSTRAINT FK_MFO_REP_PARAM_REP
    FOREIGN KEY (REPORTE_ID) REFERENCES MFO_REPORTE (REPORTE_ID) ON DELETE CASCADE;
ALTER TABLE MFO_REP_PARAM ADD CONSTRAINT FK_MFO_REP_PARAM_CAMPO
    FOREIGN KEY (CAMPO_ID) REFERENCES MFO_CAMPO (CAMPO_ID);

ALTER TABLE MFO_REP_COLUMNA ADD CONSTRAINT FK_MFO_REP_COL_REP
    FOREIGN KEY (REPORTE_ID) REFERENCES MFO_REPORTE (REPORTE_ID) ON DELETE CASCADE;

ALTER TABLE MFO_REP_EJEC ADD CONSTRAINT FK_MFO_REP_EJEC_REP
    FOREIGN KEY (REPORTE_ID) REFERENCES MFO_REPORTE (REPORTE_ID);
ALTER TABLE MFO_REP_EJEC ADD CONSTRAINT FK_MFO_REP_EJEC_RESP
    FOREIGN KEY (RESPUESTA_ID) REFERENCES MFO_RESPUESTA (RESPUESTA_ID);
