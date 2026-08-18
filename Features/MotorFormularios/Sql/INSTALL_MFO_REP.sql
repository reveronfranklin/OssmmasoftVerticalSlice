-- =============================================================================
-- Motor de Formularios (MFO) - Instalacion de la Fase 9.1
-- (capa PL/SQL del modo "parametros de reporte") mas la segunda semilla.
-- Requerimiento 16.
--
-- Requiere las Fases 1, 2 y 3 instaladas (INSTALL_MFO.sql, INSTALL_MFO_DEF.sql,
-- INSTALL_MFO_DAT.sql). Las tablas del modo reporte -09, 10, 11- y el enlace
-- sembrado -12- ya vienen con la Fase 1; aqui solo se agregan los
-- procedimientos que las operan.
--
-- Se ejecuta conectado como MFO.
--
-- NOTA DE SEGURIDAD, por si este script se lee aislado: ninguno de estos
-- procedimientos ejecuta nada que venga de la base. No hay -ni debe haber- un
-- SP_MFO_REP_EJECUTAR que reciba el nombre de un procedimiento y lo invoque. La
-- ejecucion vive en C#, detras de la lista blanca de MfoRegistroReportes.cs.
-- =============================================================================

SET SERVEROUTPUT ON
SET DEFINE OFF

PROMPT === Configuracion de reportes
@@SP_MFO_REP_GET_BY_FORM.sql
@@SP_MFO_REP_UPSERT.sql
@@SP_MFO_REP_DELETE.sql
@@SP_MFO_REP_PARAM_UPSERT.sql
@@SP_MFO_REP_PARAM_DEL.sql
@@SP_MFO_REP_COL_UPSERT.sql

PROMPT === Bitacora de ejecuciones
@@SP_MFO_REP_EJEC_INS.sql
@@SP_MFO_REP_EJEC_LIST.sql
@@SP_MFO_REP_ULTIMOS.sql

PROMPT === Segunda semilla (formulario de captura con repetibles y condiciones)
@@15_MFO_SEMILLA_CAP.sql

PROMPT === Verificacion
SELECT OBJECT_TYPE, OBJECT_NAME, STATUS
  FROM USER_OBJECTS
 WHERE STATUS <> 'VALID'
 ORDER BY OBJECT_TYPE, OBJECT_NAME;

SELECT COUNT(*) AS PROCEDIMIENTOS
  FROM USER_OBJECTS
 WHERE OBJECT_TYPE = 'PROCEDURE'
   AND OBJECT_NAME LIKE 'SP_MFO%';

SELECT ALIAS, MODO_USO, ESTADO, VERSION_PUBL_ID
  FROM MFO_FORMULARIO
 ORDER BY ALIAS;

PROMPT === Fin. La primera consulta debe salir vacia, PROCEDIMIENTOS debe ser 43
PROMPT === y deben aparecer los formularios REP_BM1 (PARAMETROS) y SOL_MANT (CAPTURA).
