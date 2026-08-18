-- =============================================================================
-- Motor de Formularios (MFO) - Fase 1 - Rollback completo
-- Requerimiento 16.
--
-- Deja el schema MFO vacio para reinstalar limpio durante el desarrollo.
-- NO ejecutar en un ambiente con datos reales: se lleva las respuestas.
--
-- El orden es el inverso de las dependencias. Los DROP TABLE llevan CASCADE
-- CONSTRAINTS para que no importe el orden entre tablas que se referencian
-- mutuamente (MFO_FORMULARIO <-> MFO_VERSION), y PURGE para no dejarlas en la
-- papelera, donde estorbarian a la reinstalacion.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- Triggers y vistas primero: dependen de las tablas.
-- -----------------------------------------------------------------------------
DROP TRIGGER TRG_MFO_SEC_LOCK;
DROP TRIGGER TRG_MFO_CAMPO_LOCK;
DROP TRIGGER TRG_MFO_OPCION_LOCK;
DROP TRIGGER TRG_MFO_REGLA_LOCK;
DROP TRIGGER TRG_MFO_COND_LOCK;
DROP TRIGGER TRG_MFO_VER_LOCK;

DROP VIEW MFO_V_DEF_PUBL;
DROP VIEW MFO_V_RESP_PLANA;
DROP VIEW MFO_V_CAMPO_USO;

-- -----------------------------------------------------------------------------
-- Tablas del modo reporte
-- -----------------------------------------------------------------------------
DROP TABLE MFO_REP_EJEC    CASCADE CONSTRAINTS PURGE;
DROP TABLE MFO_REP_COLUMNA CASCADE CONSTRAINTS PURGE;
DROP TABLE MFO_REP_PARAM   CASCADE CONSTRAINTS PURGE;
DROP TABLE MFO_REPORTE     CASCADE CONSTRAINTS PURGE;

-- -----------------------------------------------------------------------------
-- Tablas del nucleo
-- -----------------------------------------------------------------------------
DROP TABLE MFO_AUDITORIA   CASCADE CONSTRAINTS PURGE;
DROP TABLE MFO_PERMISO     CASCADE CONSTRAINTS PURGE;
DROP TABLE MFO_ADJUNTO     CASCADE CONSTRAINTS PURGE;
DROP TABLE MFO_VALOR       CASCADE CONSTRAINTS PURGE;
DROP TABLE MFO_RESPUESTA   CASCADE CONSTRAINTS PURGE;
DROP TABLE MFO_CONDICION   CASCADE CONSTRAINTS PURGE;
DROP TABLE MFO_REGLA       CASCADE CONSTRAINTS PURGE;
DROP TABLE MFO_OPCION      CASCADE CONSTRAINTS PURGE;
DROP TABLE MFO_CAMPO       CASCADE CONSTRAINTS PURGE;
DROP TABLE MFO_SECCION     CASCADE CONSTRAINTS PURGE;
DROP TABLE MFO_VERSION     CASCADE CONSTRAINTS PURGE;
DROP TABLE MFO_FORMULARIO  CASCADE CONSTRAINTS PURGE;
DROP TABLE MFO_TIPO_CAMPO  CASCADE CONSTRAINTS PURGE;

-- -----------------------------------------------------------------------------
-- Secuencias
-- -----------------------------------------------------------------------------
DROP SEQUENCE SEQ_MFO_TIPO_CAMPO;
DROP SEQUENCE SEQ_MFO_FORMULARIO;
DROP SEQUENCE SEQ_MFO_VERSION;
DROP SEQUENCE SEQ_MFO_SECCION;
DROP SEQUENCE SEQ_MFO_CAMPO;
DROP SEQUENCE SEQ_MFO_OPCION;
DROP SEQUENCE SEQ_MFO_REGLA;
DROP SEQUENCE SEQ_MFO_CONDICION;
DROP SEQUENCE SEQ_MFO_RESPUESTA;
DROP SEQUENCE SEQ_MFO_VALOR;
DROP SEQUENCE SEQ_MFO_ADJUNTO;
DROP SEQUENCE SEQ_MFO_PERMISO;
DROP SEQUENCE SEQ_MFO_AUDITORIA;
DROP SEQUENCE SEQ_MFO_REPORTE;
DROP SEQUENCE SEQ_MFO_REP_PARAM;
DROP SEQUENCE SEQ_MFO_REP_COLUMNA;
DROP SEQUENCE SEQ_MFO_REP_EJEC;
