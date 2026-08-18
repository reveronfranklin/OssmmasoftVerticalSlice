-- =============================================================================
-- Motor de Formularios (MFO) - Instalacion de la Fase 2 (capa PL/SQL de definicion)
-- Requerimiento 16.
--
-- Requiere la Fase 1 instalada (@INSTALL_MFO.sql).
-- Se ejecuta conectado como MFO.
-- =============================================================================

SET SERVEROUTPUT ON
SET DEFINE OFF

PROMPT === 13 Tipos de objeto (hallazgos de validacion)
@@13_MFO_TIPOS_OBJETO.sql

PROMPT === Catalogo
@@SP_MFO_TIPO_GET_ALL.sql

PROMPT === Formulario
@@SP_MFO_FORM_GET_ALL.sql
@@SP_MFO_FORM_GET_BY_ID.sql
@@SP_MFO_FORM_CREATE.sql
@@SP_MFO_FORM_UPDATE.sql
@@SP_MFO_FORM_ESTADO.sql

PROMPT === Version
@@SP_MFO_VER_CREATE.sql
@@SP_MFO_VER_CLONE.sql
@@SP_MFO_VER_VALIDAR.sql
-- PUBLICAR invoca a VALIDAR, asi que va despues.
@@SP_MFO_VER_PUBLICAR.sql
@@SP_MFO_VER_ARCHIVAR.sql
@@SP_MFO_VER_GET_FULL.sql

PROMPT === Secciones y campos
@@SP_MFO_SEC_UPSERT.sql
@@SP_MFO_SEC_DELETE.sql
@@SP_MFO_CAMPO_UPSERT.sql
@@SP_MFO_CAMPO_DELETE.sql
@@SP_MFO_CAMPO_REORDER.sql

PROMPT === Opciones, reglas y condiciones
@@SP_MFO_OPCION_UPSERT.sql
@@SP_MFO_OPCION_DELETE.sql
@@SP_MFO_REGLA_UPSERT.sql
@@SP_MFO_REGLA_DELETE.sql
@@SP_MFO_COND_UPSERT.sql
@@SP_MFO_COND_DELETE.sql

PROMPT === Permisos
@@SP_MFO_PERMISO_SET.sql
@@SP_MFO_PERMISO_GET.sql

PROMPT === Verificacion
SELECT OBJECT_TYPE, OBJECT_NAME, STATUS
  FROM USER_OBJECTS
 WHERE STATUS <> 'VALID'
 ORDER BY OBJECT_TYPE, OBJECT_NAME;

SELECT COUNT(*) AS PROCEDIMIENTOS
  FROM USER_OBJECTS
 WHERE OBJECT_TYPE = 'PROCEDURE'
   AND OBJECT_NAME LIKE 'SP_MFO%';

PROMPT === Fin. La primera consulta debe salir vacia y PROCEDIMIENTOS debe ser 25.
