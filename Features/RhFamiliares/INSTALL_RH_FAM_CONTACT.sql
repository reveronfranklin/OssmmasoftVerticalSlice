-- Requirement 47. Existing columns only; no ALTER TABLE or data migration.
-- Run as RH from this directory with SQL*Plus/SQLcl before publishing the API.
WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
DECLARE
    v_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_count FROM ALL_TAB_COLUMNS
     WHERE OWNER = 'RH' AND TABLE_NAME = 'RH_FAMILIARES'
       AND DATA_TYPE = 'VARCHAR2' AND NULLABLE = 'Y'
       AND ((COLUMN_NAME = 'NUMERO_COMUNICACION' AND CHAR_LENGTH = 50)
         OR (COLUMN_NAME = 'EMAIL' AND CHAR_LENGTH = 100));
    IF v_count <> 2 THEN
        RAISE_APPLICATION_ERROR(-20001, 'Verificar columnas de contacto RH_FAMILIARES');
    END IF;
END;
/
@@SP_RH_FAM_SAVE.sql
DECLARE
    v_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_count FROM ALL_OBJECTS
     WHERE OWNER = 'RH' AND OBJECT_NAME = 'SP_RH_FAM_SAVE'
       AND OBJECT_TYPE = 'PROCEDURE' AND STATUS = 'VALID';
    IF v_count <> 1 THEN
        RAISE_APPLICATION_ERROR(-20002, 'Revisar ALL_ERRORS: SP_RH_FAM_SAVE invalido');
    END IF;
END;
/
