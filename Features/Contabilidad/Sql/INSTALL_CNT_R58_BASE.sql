-- R58 foundation only. Not the final accounting correction.
-- Current SP_CNT_AUT_CONF includes the approved Oracle/API sign conversion.
-- Do not install this batch alone against the old API. Deploy the matching
-- API adapter and SP_CNT_CMP_GET_ALL/GET_ID together; see req58/SignosCNT.Verificado.md.
-- This base installer is not the coordinated sign publication/rollback tool.
-- Run as CNT with SQL*Plus/SQLcl in a reviewed maintenance window.
-- Oracle DDL commits: preserve the previous source of every replaced object.
-- No grants, policy changes or accounting data updates.
WHENEVER SQLERROR EXIT SQL.SQLCODE
SET DEFINE OFF
DECLARE
BEGIN
    IF SYS_CONTEXT('USERENV', 'DB_NAME') <> 'XE'
       OR SYS_CONTEXT('USERENV', 'SERVER_HOST') <> '6d88ba053190'
       OR SYS_CONTEXT('USERENV', 'SESSION_USER') <> 'CNT' THEN
        RAISE_APPLICATION_ERROR(-20058, 'R58: destino distinto de CNT QA; ejecucion detenida.');
    END IF;
END;
/
@@SP_CNT_AUT_PRES_GET.sql
SHOW ERRORS PROCEDURE CNT.SP_CNT_AUT_PRES_GET
@@SP_CNT_AUT_BUILD_OP.sql
SHOW ERRORS PROCEDURE CNT.SP_CNT_AUT_BUILD_OP
@@SP_CNT_AUT_BUILD_COMP02.sql
SHOW ERRORS PROCEDURE CNT.SP_CNT_AUT_BUILD_COMP02
@@SP_CNT_AUT_BUILD_COMP.sql
SHOW ERRORS PROCEDURE CNT.SP_CNT_AUT_BUILD_COMP
@@SP_CNT_AUT_BUILD_CH_RET.sql
SHOW ERRORS PROCEDURE CNT.SP_CNT_AUT_BUILD_CH_RET
@@SP_CNT_AUT_PREV.sql
SHOW ERRORS PROCEDURE CNT.SP_CNT_AUT_PREV
@@SP_CNT_AUT_CONF.sql
SHOW ERRORS PROCEDURE CNT.SP_CNT_AUT_CONF
DECLARE
    v_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_count
      FROM USER_OBJECTS
     WHERE OBJECT_TYPE = 'PROCEDURE'
       AND OBJECT_NAME IN ('SP_CNT_AUT_PRES_GET', 'SP_CNT_AUT_BUILD_OP',
           'SP_CNT_AUT_BUILD_COMP', 'SP_CNT_AUT_BUILD_COMP02', 'SP_CNT_AUT_BUILD_CH_RET',
           'SP_CNT_AUT_PREV', 'SP_CNT_AUT_CONF')
       AND STATUS = 'VALID';
    IF v_count <> 7 THEN
        RAISE_APPLICATION_ERROR(-20058, 'R58: revise errores de compilacion; instalacion incompleta.');
    END IF;
END;
/
