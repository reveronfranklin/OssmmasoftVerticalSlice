-- Run ONLY after integrated tests and dependency/trigger review from DIAG_CNT_CMP_PEND.
-- Default remains disabled on installation. Never disable deployed triggers to pass this check.
WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
DECLARE
    v_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_count FROM ALL_OBJECTS WHERE OWNER = 'CNT' AND STATUS <> 'VALID'
       AND (OBJECT_NAME LIKE 'SP_CNT_%' OR OBJECT_NAME IN ('CNT_CMP_GUARD', 'CNT_PKG_CONTABILIDAD', 'CNT_CMP_WRITE_TRG', 'CNT_DET_WRITE_TRG', 'CNT_PER_PEND_TRG', 'CNT_V_MAYOR_ANALITICO', 'CNT_V_MOV_AUXILIAR'));
    IF v_count > 0 THEN RAISE_APPLICATION_ERROR(-20001, 'Existen objetos CNT invalidos.'); END IF;
    SELECT COUNT(*) INTO v_count FROM ALL_TRIGGERS WHERE TABLE_OWNER = 'CNT' AND STATUS = 'ENABLED'
       AND TABLE_NAME IN ('CNT_COMPROBANTES', 'CNT_DETALLE_COMPROBANTE')
       AND TRIGGER_NAME NOT IN ('CNT_CMP_WRITE_TRG', 'CNT_DET_WRITE_TRG');
    IF v_count > 0 THEN
        RAISE_APPLICATION_ERROR(-20001, 'Existen triggers adicionales. Adapte sus efectos contables y revise esta activacion antes de habilitar pendientes.');
    END IF;
    SELECT COUNT(*) INTO v_count FROM ALL_TRIGGERS WHERE OWNER = 'CNT' AND STATUS = 'ENABLED'
       AND TRIGGER_NAME IN ('CNT_CMP_WRITE_TRG', 'CNT_DET_WRITE_TRG', 'CNT_PER_PEND_TRG');
    IF v_count <> 3 THEN RAISE_APPLICATION_ERROR(-20001, 'Faltan los triggers de proteccion CNT 37.'); END IF;
    UPDATE CNT.CNT_CMP_CTL SET HABILITADO = 1 WHERE CODIGO = 1;
END;
/
COMMIT;
