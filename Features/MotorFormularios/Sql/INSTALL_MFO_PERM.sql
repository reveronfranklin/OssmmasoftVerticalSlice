-- =============================================================================
-- Motor de Formularios (MFO) - Permisos por usuario y por reporte
-- Requerimiento 16, extension posterior al cierre de la Fase 9.
--
-- Requiere las Fases 1 a 3 y la 9.1 instaladas.
-- Se ejecuta conectado como MFO.
--
-- **Cambia quien puede que.** A partir de aqui MFO_PERMISO (por rol) deja de
-- aplicar: los slices consultan MFO_PERMISO_USR. Los formularios que estuvieran
-- restringidos por rol quedan **abiertos** hasta que se les asigne al menos un
-- usuario, porque la politica del motor sigue siendo "sin permisos definidos,
-- abierto". Conviene revisar que formularios tenian permisos antes de instalar:
--
--   SELECT F.ALIAS, P.ROL_CODIGO, P.ACCION
--     FROM MFO_PERMISO P JOIN MFO_FORMULARIO F ON F.FORMULARIO_ID = P.FORMULARIO_ID
--    ORDER BY F.ALIAS, P.ROL_CODIGO;
-- =============================================================================

SET SERVEROUTPUT ON
SET DEFINE OFF

PROMPT === 17 Tablas de permisos por usuario
@@17_MFO_PERMISO_USR.sql

PROMPT === Procedimientos
@@SP_MFO_PERM_USR_SET.sql
@@SP_MFO_PERM_USR_GET.sql
@@SP_MFO_PERM_REP_SET.sql

PROMPT === Verificacion
SELECT OBJECT_TYPE, OBJECT_NAME, STATUS
  FROM USER_OBJECTS
 WHERE STATUS <> 'VALID'
 ORDER BY OBJECT_TYPE, OBJECT_NAME;

SELECT COUNT(*) AS PROCEDIMIENTOS
  FROM USER_OBJECTS
 WHERE OBJECT_TYPE = 'PROCEDURE' AND OBJECT_NAME LIKE 'SP_MFO%';

PROMPT === Fin. La primera consulta debe salir vacia y PROCEDIMIENTOS debe ser 46.
