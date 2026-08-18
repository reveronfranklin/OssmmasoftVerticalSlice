-- =============================================================================
-- Motor de Formularios (MFO) - Instalacion de la Fase 3 (capa PL/SQL de datos)
-- Requerimiento 16.
--
-- Requiere las Fases 1 y 2 instaladas.
-- Se ejecuta conectado como MFO.
-- =============================================================================

SET SERVEROUTPUT ON
SET DEFINE OFF

PROMPT === 14 Paquete de tipos de arreglo
@@14_MFO_PKG_ARRAYS.sql

PROMPT === Respuestas
@@SP_MFO_RESP_CREATE.sql
@@SP_MFO_RESP_VAL_SAVE.sql
@@SP_MFO_RESP_SUBMIT.sql
@@SP_MFO_RESP_GET_BY_ID.sql
@@SP_MFO_RESP_SEARCH.sql
@@SP_MFO_RESP_ANULAR.sql
@@SP_MFO_RESP_DELETE.sql
@@SP_MFO_RESP_EXPORT.sql

PROMPT === Bitacora
@@SP_MFO_AUD_INS.sql

PROMPT === Verificacion
SELECT OBJECT_TYPE, OBJECT_NAME, STATUS
  FROM USER_OBJECTS
 WHERE STATUS <> 'VALID'
 ORDER BY OBJECT_TYPE, OBJECT_NAME;

SELECT COUNT(*) AS PROCEDIMIENTOS
  FROM USER_OBJECTS
 WHERE OBJECT_TYPE = 'PROCEDURE'
   AND OBJECT_NAME LIKE 'SP_MFO%';

PROMPT === Fin. La primera consulta debe salir vacia y PROCEDIMIENTOS debe ser 34.
