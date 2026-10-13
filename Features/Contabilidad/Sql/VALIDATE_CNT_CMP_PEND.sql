-- Oracle test environment ONLY. Supply an existing unbalanced PENDIENTE fixture.
-- sqlplus: DEFINE CMP_TEST = <id>, EMP_TEST = <company>, USR_TEST = <user>
-- This smoke test never commits. Concurrency and accounting acceptance need two sessions.
SET SERVEROUTPUT ON
WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
DECLARE
    v_cmp NUMBER := &CMP_TEST;
    v_empresa NUMBER := &EMP_TEST;
    v_usuario NUMBER := &USR_TEST;
    v_version NUMBER;
    v_after NUMBER;
    v_estado VARCHAR2(12);
    v_message VARCHAR2(4000);
    v_total NUMBER;
    v_count NUMBER;
BEGIN
    SAVEPOINT cnt_smoke;
    CNT.CNT_CMP_GUARD.LOCK_ALL;
    SELECT VERSION_CMP, ESTADO_CMP INTO v_version, v_estado FROM CNT.CNT_COMPROBANTES
     WHERE CODIGO_COMPROBANTE = v_cmp AND CODIGO_EMPRESA = v_empresa;
    SELECT NVL(SUM(MONTO),0) INTO v_total FROM CNT.CNT_DETALLE_COMPROBANTE
     WHERE CODIGO_COMPROBANTE = v_cmp AND CODIGO_EMPRESA = v_empresa;
    IF v_estado <> 'PENDIENTE' OR v_total = 0 THEN
        RAISE_APPLICATION_ERROR(-20001, 'La fixture debe ser un pendiente descuadrado.');
    END IF;
    CNT.SP_CNT_CMP_FINALIZAR(v_cmp, v_empresa, v_usuario, v_version, v_after, v_message);
    IF v_message = 'Success' THEN RAISE_APPLICATION_ERROR(-20001, 'Se finalizo un descuadrado.'); END IF;
    SELECT VERSION_CMP, ESTADO_CMP INTO v_after, v_estado FROM CNT.CNT_COMPROBANTES
     WHERE CODIGO_COMPROBANTE = v_cmp AND CODIGO_EMPRESA = v_empresa;
    IF v_after <> v_version OR v_estado <> 'PENDIENTE' THEN
        RAISE_APPLICATION_ERROR(-20001, 'Finalizacion fallida cambio estado o version.');
    END IF;
    BEGIN
        CNT.CNT_CMP_GUARD.CHECK_EDIT(v_cmp, v_empresa, v_version + 1, v_usuario, 1, 'SMOKE');
        RAISE_APPLICATION_ERROR(-20002, 'Se acepto una version obsoleta.');
    EXCEPTION WHEN OTHERS THEN
        IF SQLCODE <> -20001 THEN RAISE; END IF;
    END;
    CNT.CNT_CMP_GUARD.CHECK_EDIT(v_cmp, v_empresa, v_version, v_usuario, 1, 'SMOKE');
    CNT.CNT_CMP_GUARD.FINISH_EDIT(v_cmp, v_empresa, v_usuario);
    SELECT VERSION_CMP, ESTADO_CMP INTO v_after, v_estado FROM CNT.CNT_COMPROBANTES
     WHERE CODIGO_COMPROBANTE = v_cmp AND CODIGO_EMPRESA = v_empresa;
    IF v_after <> v_version + 1 OR v_estado <> 'PENDIENTE' THEN
        RAISE_APPLICATION_ERROR(-20001, 'Guardar avance finalizo implicitamente o no incremento version.');
    END IF;
    SELECT COUNT(*) INTO v_count FROM CNT.CNT_V_MAYOR_ANALITICO WHERE CODIGO_COMPROBANTE = v_cmp AND CODIGO_EMPRESA = v_empresa;
    IF v_count > 0 THEN RAISE_APPLICATION_ERROR(-20001, 'Mayor incluyo el pendiente.'); END IF;
    SELECT COUNT(*) INTO v_count FROM CNT.CNT_V_MOV_AUXILIAR WHERE CODIGO_COMPROBANTE = v_cmp AND CODIGO_EMPRESA = v_empresa;
    IF v_count > 0 THEN RAISE_APPLICATION_ERROR(-20001, 'Movimiento auxiliar incluyo el pendiente.'); END IF;
    ROLLBACK TO cnt_smoke;
    DBMS_OUTPUT.PUT_LINE('Smoke CNT 37 correcto; todos los cambios revertidos.');
EXCEPTION
    WHEN OTHERS THEN ROLLBACK; RAISE;
END;
/
ROLLBACK;
