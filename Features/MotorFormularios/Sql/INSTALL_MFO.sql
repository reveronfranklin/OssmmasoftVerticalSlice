-- =============================================================================
-- Motor de Formularios (MFO) - Instalacion completa de la Fase 1
-- Requerimiento 16.
--
-- Uso:
--   1. Conectado como DBA, una sola vez:  @00_MFO_USUARIO.sql
--   2. Conectado como MFO:                @INSTALL_MFO.sql
--
-- Para reinstalar limpio durante el desarrollo: @99_MFO_DROP.sql y repetir el
-- paso 2.
-- =============================================================================

SET SERVEROUTPUT ON
SET DEFINE OFF

PROMPT === 01 Tablas del nucleo
@@01_MFO_TABLAS.sql

PROMPT === 02 Restricciones del nucleo
@@02_MFO_CONSTRAINTS.sql

PROMPT === 03 Secuencias
@@03_MFO_SECUENCIAS.sql

PROMPT === 04 Indices
@@04_MFO_INDICES.sql

PROMPT === 05 Triggers de inmutabilidad
@@05_MFO_TRIGGERS.sql

PROMPT === 06 Vistas
@@06_MFO_VISTAS.sql

PROMPT === 07 Semilla de tipos de campo
@@07_MFO_SEMILLA_TIPOS.sql

PROMPT === 09 Tablas del modo reporte
@@09_MFO_REP_TABLAS.sql

PROMPT === 10 Restricciones del modo reporte
@@10_MFO_REP_CONSTRAINTS.sql

PROMPT === 11 Secuencias e indices del modo reporte
@@11_MFO_REP_SEQ_IDX.sql

-- Las semillas van al final: 08 necesita los tipos de campo de 07, y 12 necesita
-- las columnas MODO_USO/REGISTRA_EJEC que agrega 09 y el formulario de 08.
PROMPT === 08 Formulario de referencia REP_BM1
@@08_MFO_SEMILLA_DEMO.sql

PROMPT === 12 Enlace del reporte de referencia
@@12_MFO_REP_SEMILLA.sql

PROMPT === Verificacion
SELECT COUNT(*) AS OBJETOS_INVALIDOS FROM USER_OBJECTS WHERE STATUS <> 'VALID';
SELECT COUNT(*) AS TIPOS_CAMPO FROM MFO_TIPO_CAMPO WHERE ACTIVO = 'S';
SELECT ALIAS, MODO_USO, VERSION_PUBL_ID FROM MFO_FORMULARIO;
SELECT CLAVE, TIPO_EJEC, CLAVE_REGISTRO FROM MFO_REPORTE;

PROMPT === Fin. OBJETOS_INVALIDOS debe ser 0 y TIPOS_CAMPO debe ser 19.
