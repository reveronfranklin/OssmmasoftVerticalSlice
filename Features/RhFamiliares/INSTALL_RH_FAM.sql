-- Run with SQL*Plus/SQLcl as RH from this directory.
WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
DECLARE
    v_count NUMBER;
    v_type VARCHAR2(30);
    v_precision NUMBER;
    v_scale NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_count FROM ALL_TAB_COLUMNS
     WHERE OWNER = 'RH' AND TABLE_NAME = 'RH_FAMILIARES'
       AND COLUMN_NAME = 'ESTADO_SALUD_ID';
    IF v_count = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE RH.RH_FAMILIARES ADD (ESTADO_SALUD_ID NUMBER(5,0))';
    ELSE
        SELECT DATA_TYPE, DATA_PRECISION, DATA_SCALE INTO v_type, v_precision, v_scale
          FROM ALL_TAB_COLUMNS WHERE OWNER = 'RH' AND TABLE_NAME = 'RH_FAMILIARES'
           AND COLUMN_NAME = 'ESTADO_SALUD_ID';
        IF v_type <> 'NUMBER' OR NVL(v_precision, -1) <> 5 OR NVL(v_scale, -1) <> 0 THEN
            RAISE_APPLICATION_ERROR(-20001, 'ESTADO_SALUD_ID debe ser NUMBER(5,0)');
        END IF;
    END IF;
END;
/
-- RH needs a direct SELECT grant on SIS.SIS_USUARIOS for audit resolution.
@@SP_RH_FAM_GET_PER.sql
@@SP_RH_FAM_SAVE.sql
@@SP_RH_FAM_DEL.sql
DECLARE
    v_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_count FROM ALL_ERRORS WHERE OWNER = 'RH'
       AND NAME IN ('SP_RH_FAM_GET_PER', 'SP_RH_FAM_SAVE', 'SP_RH_FAM_DEL')
       AND TYPE = 'PROCEDURE' AND ATTRIBUTE = 'ERROR';
    IF v_count > 0 THEN
        RAISE_APPLICATION_ERROR(-20002, 'Revisar ALL_ERRORS: procedimientos RH_FAM invalidos');
    END IF;
END;
/
