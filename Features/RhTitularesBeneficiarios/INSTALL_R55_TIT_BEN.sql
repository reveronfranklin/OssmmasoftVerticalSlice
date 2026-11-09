-- Run from this feature folder as RH after installing R47.
-- Grants are not changed by this installer.
WHENEVER SQLERROR EXIT SQL.SQLCODE
@@RH_V_TIT_BEN_KEYS.sql
@@SP_RH_TIT_BEN_GET.sql
DECLARE
    v_COUNT NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_COUNT FROM USER_OBJECTS
     WHERE OBJECT_NAME IN ('RH_V_TIT_BEN_KEYS', 'SP_RH_TIT_BEN_GET')
       AND STATUS = 'VALID';
    IF v_COUNT <> 2 THEN
        RAISE_APPLICATION_ERROR(-20055, 'Objetos R55 invalidos. Revisar USER_ERRORS.');
    END IF;
END;
/
