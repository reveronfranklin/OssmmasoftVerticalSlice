CREATE OR REPLACE PACKAGE CNT.CNT_CMP_GUARD AS
    PROCEDURE LOCK_ALL;
    PROCEDURE ALLOW_WRITE(p_cmp NUMBER, p_empresa NUMBER);
    FUNCTION CAN_WRITE(p_cmp NUMBER, p_empresa NUMBER) RETURN BOOLEAN;
    PROCEDURE CHECK_EDIT(p_cmp NUMBER, p_empresa NUMBER, p_version NUMBER, p_usuario NUMBER, p_automatico NUMBER, p_operacion VARCHAR2);
    PROCEDURE FINISH_EDIT(p_cmp NUMBER, p_empresa NUMBER, p_usuario NUMBER);
    PROCEDURE AUDIT_CMP(p_cmp NUMBER, p_empresa NUMBER, p_usuario NUMBER, p_operacion VARCHAR2);
END;
/
CREATE OR REPLACE PACKAGE BODY CNT.CNT_CMP_GUARD AS
    g_cmp NUMBER;
    g_empresa NUMBER;
    g_tx VARCHAR2(100);
    PROCEDURE ALLOW_WRITE(p_cmp NUMBER, p_empresa NUMBER) IS
    BEGIN
        g_cmp := p_cmp;
        g_empresa := p_empresa;
        g_tx := DBMS_TRANSACTION.LOCAL_TRANSACTION_ID(TRUE);
    END;
    FUNCTION CAN_WRITE(p_cmp NUMBER, p_empresa NUMBER) RETURN BOOLEAN IS
    BEGIN
        RETURN NVL(g_cmp, -1) = p_cmp AND NVL(g_empresa, -1) = p_empresa
            AND g_tx = DBMS_TRANSACTION.LOCAL_TRANSACTION_ID(TRUE);
    END;
    PROCEDURE LOCK_ALL IS
    BEGIN
        -- Same order in all writers and cierre. Protects even missing daily headers.
        -- Conservative Oracle 10g strategy: serializes CNT writes across companies.
        LOCK TABLE CNT.CNT_PERIODOS IN EXCLUSIVE MODE;
        LOCK TABLE CNT.CNT_COMPROBANTES IN EXCLUSIVE MODE;
        LOCK TABLE CNT.CNT_DETALLE_COMPROBANTE IN EXCLUSIVE MODE;
    END;

    PROCEDURE AUDIT_CMP(p_cmp NUMBER, p_empresa NUMBER, p_usuario NUMBER, p_operacion VARCHAR2) IS
        v_id VARCHAR2(32) := RAWTOHEX(SYS_GUID());
    BEGIN
        INSERT INTO CNT.CNT_CMP_AUD
        SELECT v_id, CODIGO_COMPROBANTE, CODIGO_EMPRESA, VERSION_CMP, ESTADO_CMP,
               p_operacion, p_usuario, SYSDATE, NUMERO_COMPROBANTE, OBSERVACION
          FROM CNT.CNT_COMPROBANTES WHERE CODIGO_COMPROBANTE = p_cmp AND CODIGO_EMPRESA = p_empresa;
        INSERT INTO CNT.CNT_CMP_AUD_DET
        SELECT v_id, CODIGO_DETALLE_COMPROBANTE, CODIGO_MAYOR, CODIGO_AUXILIAR,
               MONTO, REFERENCIA1, REFERENCIA2, REFERENCIA3, DESCRIPCION
          FROM CNT.CNT_DETALLE_COMPROBANTE WHERE CODIGO_COMPROBANTE = p_cmp AND CODIGO_EMPRESA = p_empresa;
    END;

    PROCEDURE CHECK_EDIT(p_cmp NUMBER, p_empresa NUMBER, p_version NUMBER, p_usuario NUMBER, p_automatico NUMBER, p_operacion VARCHAR2) IS
        v_cmp CNT.CNT_COMPROBANTES%ROWTYPE;
        v_cierre DATE;
        v_links NUMBER;
    BEGIN
        LOCK_ALL;
        SELECT * INTO v_cmp FROM CNT.CNT_COMPROBANTES
         WHERE CODIGO_COMPROBANTE = p_cmp AND CODIGO_EMPRESA = p_empresa FOR UPDATE;
        SELECT FECHA_CIERRE INTO v_cierre FROM CNT.CNT_PERIODOS
         WHERE CODIGO_PERIODO = v_cmp.CODIGO_PERIODO AND CODIGO_EMPRESA = p_empresa;
        IF v_cierre IS NOT NULL THEN
            RAISE_APPLICATION_ERROR(-20001, 'El periodo esta cerrado.');
        END IF;
        IF v_cmp.EXTRA1 = 'AUTOMATICO' AND NVL(p_automatico, 0) <> 1 THEN
            RAISE_APPLICATION_ERROR(-20001, 'No tiene permiso para editar comprobantes automaticos.');
        END IF;
        SELECT COUNT(*) INTO v_links FROM CNT.CNT_DETALLE_LIBRO
         WHERE CODIGO_IDENTIFICADOR = p_cmp AND CODIGO_EMPRESA = p_empresa
           AND TIPO_DOCUMENTO_ID = v_cmp.TIPO_COMPROBANTE_ID
           AND NVL(ORIGEN_ID, -1) = NVL(v_cmp.ORIGEN_ID, -1);
        IF v_links > 0 THEN
            RAISE_APPLICATION_ERROR(-20001, 'El comprobante tiene movimientos en libro banco. Resuelva esas relaciones antes de modificarlo.');
        END IF;
        IF p_version IS NULL OR p_version <> v_cmp.VERSION_CMP THEN
            RAISE_APPLICATION_ERROR(-20001, 'El comprobante fue modificado. Recargue antes de guardar.');
        END IF;
        ALLOW_WRITE(p_cmp, p_empresa);
        AUDIT_CMP(p_cmp, p_empresa, p_usuario, p_operacion || '_ANTES');
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            RAISE_APPLICATION_ERROR(-20001, 'Comprobante o periodo no encontrado para la empresa.');
    END;

    PROCEDURE FINISH_EDIT(p_cmp NUMBER, p_empresa NUMBER, p_usuario NUMBER) IS
        v_estado VARCHAR2(12);
        v_total NUMBER;
        v_count NUMBER;
        v_invalid NUMBER;
    BEGIN
        SELECT ESTADO_CMP INTO v_estado FROM CNT.CNT_COMPROBANTES
         WHERE CODIGO_COMPROBANTE = p_cmp AND CODIGO_EMPRESA = p_empresa;
        SELECT COUNT(*), NVL(SUM(ROUND(d.MONTO, 2)), 0),
               NVL(SUM(CASE WHEN a.CODIGO_AUXILIAR IS NULL OR m.CODIGO_MAYOR IS NULL
                   OR a.FECHA_FIN_VIGENCIA < TRUNC(SYSDATE) OR d.MONTO IS NULL OR ROUND(d.MONTO,2) = 0 THEN 1 ELSE 0 END), 0)
          INTO v_count, v_total, v_invalid
          FROM CNT.CNT_DETALLE_COMPROBANTE d
          LEFT JOIN CNT.CNT_AUXILIARES a ON a.CODIGO_AUXILIAR = d.CODIGO_AUXILIAR
               AND a.CODIGO_MAYOR = d.CODIGO_MAYOR AND a.CODIGO_EMPRESA = p_empresa
          LEFT JOIN CNT.CNT_MAYORES m ON m.CODIGO_MAYOR = d.CODIGO_MAYOR AND m.CODIGO_EMPRESA = p_empresa
         WHERE d.CODIGO_COMPROBANTE = p_cmp AND d.CODIGO_EMPRESA = p_empresa;
        IF v_count = 0 OR v_invalid > 0 THEN
            RAISE_APPLICATION_ERROR(-20001, 'El comprobante debe tener detalles y cuentas validas.');
        END IF;
        IF v_estado = 'FINALIZADO' AND v_total <> 0 THEN
            RAISE_APPLICATION_ERROR(-20001, 'Un comprobante finalizado debe permanecer cuadrado.');
        END IF;
        UPDATE CNT.CNT_COMPROBANTES
           SET VERSION_CMP = VERSION_CMP + 1,
               AJUSTE_MANUAL = CASE WHEN EXTRA1 = 'AUTOMATICO' THEN 1 ELSE AJUSTE_MANUAL END,
               USUARIO_UPD = p_usuario, FECHA_UPD = SYSDATE
         WHERE CODIGO_COMPROBANTE = p_cmp AND CODIGO_EMPRESA = p_empresa;
        AUDIT_CMP(p_cmp, p_empresa, p_usuario, 'GUARDADO');
        -- Preclose accumulators must be rebuilt after any change or finalization.
        UPDATE CNT.CNT_PERIODOS SET FECHA_PRECIERRE = NULL, USUARIO_PRECIERRE = NULL
         WHERE CODIGO_PERIODO = (SELECT CODIGO_PERIODO FROM CNT.CNT_COMPROBANTES
                  WHERE CODIGO_COMPROBANTE = p_cmp AND CODIGO_EMPRESA = p_empresa)
           AND CODIGO_EMPRESA = p_empresa;
    END;
END;
/
CREATE OR REPLACE PROCEDURE CNT.SP_CNT_CMP_FINALIZAR (
    p_CODIGO_COMPROBANTE IN NUMBER,
    p_CODIGO_EMPRESA IN NUMBER,
    p_USUARIO_ID IN NUMBER,
    p_VERSION_CMP IN NUMBER,
    p_VERSION_OUT OUT NUMBER,
    p_Message OUT VARCHAR2
) AS
    v_estado VARCHAR2(12);
    v_total NUMBER;
    v_auto VARCHAR2(30);
BEGIN
    SAVEPOINT cnt_finalizar;
    CNT.CNT_CMP_GUARD.LOCK_ALL;
    SELECT ESTADO_CMP, VERSION_CMP, EXTRA1 INTO v_estado, p_VERSION_OUT, v_auto
      FROM CNT.CNT_COMPROBANTES WHERE CODIGO_COMPROBANTE = p_CODIGO_COMPROBANTE
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA FOR UPDATE;
    IF v_auto <> 'AUTOMATICO' OR v_auto IS NULL THEN
        p_Message := 'Esta operacion requiere un comprobante automatico.';
        RETURN;
    END IF;
    SELECT COUNT(*) INTO v_total FROM CNT.CNT_PERIODOS p
      JOIN CNT.CNT_COMPROBANTES c ON c.CODIGO_PERIODO = p.CODIGO_PERIODO AND c.CODIGO_EMPRESA = p.CODIGO_EMPRESA
     WHERE c.CODIGO_COMPROBANTE = p_CODIGO_COMPROBANTE AND c.CODIGO_EMPRESA = p_CODIGO_EMPRESA AND p.FECHA_CIERRE IS NULL;
    IF v_total <> 1 THEN
        RAISE_APPLICATION_ERROR(-20001, 'El periodo esta cerrado.');
    END IF;
    -- Retry of successful transition with its original version has no new effects.
    IF v_estado = 'FINALIZADO' AND p_VERSION_CMP IN (p_VERSION_OUT, p_VERSION_OUT - 1) THEN
        p_Message := 'Success';
        RETURN;
    END IF;
    CNT.CNT_CMP_GUARD.CHECK_EDIT(p_CODIGO_COMPROBANTE, p_CODIGO_EMPRESA, p_VERSION_CMP, p_USUARIO_ID, 1, 'FINALIZAR');
    SELECT NVL(SUM(ROUND(MONTO, 2)), 0) INTO v_total FROM CNT.CNT_DETALLE_COMPROBANTE
     WHERE CODIGO_COMPROBANTE = p_CODIGO_COMPROBANTE AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;
    IF v_total <> 0 THEN
        RAISE_APPLICATION_ERROR(-20001, 'El comprobante no esta cuadrado.');
    END IF;
    UPDATE CNT.CNT_COMPROBANTES SET ESTADO_CMP = 'FINALIZADO'
     WHERE CODIGO_COMPROBANTE = p_CODIGO_COMPROBANTE AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;
    CNT.CNT_CMP_GUARD.FINISH_EDIT(p_CODIGO_COMPROBANTE, p_CODIGO_EMPRESA, p_USUARIO_ID);
    SELECT VERSION_CMP INTO p_VERSION_OUT FROM CNT.CNT_COMPROBANTES
     WHERE CODIGO_COMPROBANTE = p_CODIGO_COMPROBANTE AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;
    CNT.CNT_CMP_GUARD.AUDIT_CMP(p_CODIGO_COMPROBANTE, p_CODIGO_EMPRESA, p_USUARIO_ID, 'FINALIZADO');
    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK TO cnt_finalizar;
        p_Message := SQLERRM;
END;
/
