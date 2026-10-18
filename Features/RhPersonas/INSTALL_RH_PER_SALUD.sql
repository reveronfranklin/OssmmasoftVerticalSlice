-- SQL*Plus/SQLcl, execute as RH from this directory before publishing legacy.
WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
DECLARE
    v_count NUMBER;
    v_type VARCHAR2(30);
    v_precision NUMBER;
    v_scale NUMBER;
    v_nullable VARCHAR2(1);
BEGIN
    SELECT COUNT(*) INTO v_count FROM ALL_TAB_COLUMNS
     WHERE OWNER = 'RH' AND TABLE_NAME = 'RH_PERSONAS' AND COLUMN_NAME = 'ESTADO_SALUD_ID';
    IF v_count = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE RH.RH_PERSONAS ADD (ESTADO_SALUD_ID NUMBER(5,0))';
    ELSE
        SELECT DATA_TYPE, DATA_PRECISION, DATA_SCALE, NULLABLE
          INTO v_type, v_precision, v_scale, v_nullable FROM ALL_TAB_COLUMNS
         WHERE OWNER = 'RH' AND TABLE_NAME = 'RH_PERSONAS' AND COLUMN_NAME = 'ESTADO_SALUD_ID';
        IF v_type <> 'NUMBER' OR NVL(v_precision, -1) <> 5 OR NVL(v_scale, -1) <> 0 OR v_nullable <> 'Y' THEN
            RAISE_APPLICATION_ERROR(-20003, 'ESTADO_SALUD_ID debe ser NUMBER(5,0) nullable.');
        END IF;
    END IF;
END;
/
@@SP_RH_PER_SALUD_VAL.sql
@@TR_RH_PER_SALUD.sql
DECLARE
    v_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_count FROM ALL_ERRORS WHERE OWNER = 'RH'
       AND NAME IN ('SP_RH_PER_SALUD_VAL', 'TR_RH_PER_SALUD') AND ATTRIBUTE = 'ERROR';
    IF v_count > 0 THEN
        RAISE_APPLICATION_ERROR(-20004, 'Revisar ALL_ERRORS: objetos de salud invalidos.');
    END IF;
END;
/
