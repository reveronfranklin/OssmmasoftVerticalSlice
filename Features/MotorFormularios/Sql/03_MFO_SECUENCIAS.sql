-- =============================================================================
-- Motor de Formularios (MFO) - Fase 1 - Secuencias del nucleo (13)
-- Requerimiento 16.
--
-- Oracle 10g no tiene IDENTITY: cada PK se alimenta de su secuencia, invocada
-- desde el procedimiento que inserta. No se usan triggers BEFORE INSERT para
-- asignarlas, porque los procedimientos necesitan devolver el ID generado.
--
-- CACHE 20 es el valor por defecto y es el adecuado: los huecos en la secuencia
-- no importan porque ninguno de estos IDs es visible para el usuario -el
-- identificador funcional es ALIAS, CLAVE o NUMERO de version-.
-- =============================================================================

CREATE SEQUENCE SEQ_MFO_TIPO_CAMPO START WITH 1 INCREMENT BY 1 NOCYCLE;
CREATE SEQUENCE SEQ_MFO_FORMULARIO START WITH 1 INCREMENT BY 1 NOCYCLE;
CREATE SEQUENCE SEQ_MFO_VERSION    START WITH 1 INCREMENT BY 1 NOCYCLE;
CREATE SEQUENCE SEQ_MFO_SECCION    START WITH 1 INCREMENT BY 1 NOCYCLE;
CREATE SEQUENCE SEQ_MFO_CAMPO      START WITH 1 INCREMENT BY 1 NOCYCLE;
CREATE SEQUENCE SEQ_MFO_OPCION     START WITH 1 INCREMENT BY 1 NOCYCLE;
CREATE SEQUENCE SEQ_MFO_REGLA      START WITH 1 INCREMENT BY 1 NOCYCLE;
CREATE SEQUENCE SEQ_MFO_CONDICION  START WITH 1 INCREMENT BY 1 NOCYCLE;
CREATE SEQUENCE SEQ_MFO_RESPUESTA  START WITH 1 INCREMENT BY 1 NOCYCLE;
CREATE SEQUENCE SEQ_MFO_VALOR      START WITH 1 INCREMENT BY 1 NOCYCLE;
CREATE SEQUENCE SEQ_MFO_ADJUNTO    START WITH 1 INCREMENT BY 1 NOCYCLE;
CREATE SEQUENCE SEQ_MFO_PERMISO    START WITH 1 INCREMENT BY 1 NOCYCLE;
CREATE SEQUENCE SEQ_MFO_AUDITORIA  START WITH 1 INCREMENT BY 1 NOCYCLE;
