-- =============================================================================
-- Motor de Formularios (MFO) - Fase 1 - Secuencias e indices del modo reporte
-- Requerimiento 16.
-- =============================================================================

CREATE SEQUENCE SEQ_MFO_REPORTE     START WITH 1 INCREMENT BY 1 NOCYCLE;
CREATE SEQUENCE SEQ_MFO_REP_PARAM   START WITH 1 INCREMENT BY 1 NOCYCLE;
CREATE SEQUENCE SEQ_MFO_REP_COLUMNA START WITH 1 INCREMENT BY 1 NOCYCLE;
CREATE SEQUENCE SEQ_MFO_REP_EJEC    START WITH 1 INCREMENT BY 1 NOCYCLE;

-- Bitacora por fecha: es la consulta de la pantalla de auditoria.
CREATE INDEX IDX_MFO_REP_EJEC_FEC ON MFO_REP_EJEC (FECHA_INICIO);

-- "Mis ultimas ejecuciones", que alimenta el reejecutar con los parametros de la
-- vez pasada. USUARIO primero porque siempre se filtra por el usuario actual.
CREATE INDEX IDX_MFO_REP_EJEC_USR ON MFO_REP_EJEC (USUARIO, FECHA_INICIO);
