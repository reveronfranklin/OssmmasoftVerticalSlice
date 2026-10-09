-- These guards also protect automatics from legacy writers without version control.
-- Authorization is tied to an Oracle transaction ID, never to a pooled connection lifetime.
CREATE OR REPLACE TRIGGER CNT.CNT_CMP_WRITE_TRG
BEFORE INSERT OR UPDATE OR DELETE ON CNT.CNT_COMPROBANTES
FOR EACH ROW
DECLARE
    v_cmp NUMBER;
    v_empresa NUMBER;
    v_periodo NUMBER;
    v_fecha DATE;
    v_cierre DATE;
BEGIN
    IF DELETING THEN
        v_cmp := :OLD.CODIGO_COMPROBANTE;
        v_empresa := :OLD.CODIGO_EMPRESA;
        v_periodo := :OLD.CODIGO_PERIODO;
        v_fecha := :OLD.FECHA_COMPROBANTE;
    ELSE
        v_cmp := :NEW.CODIGO_COMPROBANTE;
        v_empresa := :NEW.CODIGO_EMPRESA;
        v_periodo := :NEW.CODIGO_PERIODO;
        v_fecha := :NEW.FECHA_COMPROBANTE;
    END IF;
    IF NVL(:OLD.EXTRA1, ' ') = 'AUTOMATICO' OR NVL(:NEW.EXTRA1, ' ') = 'AUTOMATICO'
        OR NVL(:NEW.ESTADO_CMP, 'FINALIZADO') = 'PENDIENTE' THEN
        IF NOT CNT.CNT_CMP_GUARD.CAN_WRITE(v_cmp, v_empresa) THEN
            RAISE_APPLICATION_ERROR(-20001, 'El automatico requiere el flujo CNT con permiso y control de version.');
        END IF;
        IF UPDATING AND :OLD.EXTRA1 = 'AUTOMATICO' AND (
            :NEW.CODIGO_EMPRESA <> :OLD.CODIGO_EMPRESA OR :NEW.CODIGO_PERIODO <> :OLD.CODIGO_PERIODO
            OR :NEW.TIPO_COMPROBANTE_ID <> :OLD.TIPO_COMPROBANTE_ID
            OR :NEW.FECHA_COMPROBANTE <> :OLD.FECHA_COMPROBANTE
            OR NVL(:NEW.ORIGEN_ID, -1) <> NVL(:OLD.ORIGEN_ID, -1)
            OR NVL(:NEW.EXTRA1, ' ') <> 'AUTOMATICO' OR :NEW.NUMERO_COMPROBANTE <> :OLD.NUMERO_COMPROBANTE) THEN
            RAISE_APPLICATION_ERROR(-20001, 'No puede cambiar la identidad del comprobante automatico.');
        END IF;
    END IF;
    SELECT FECHA_CIERRE INTO v_cierre FROM CNT.CNT_PERIODOS
     WHERE CODIGO_PERIODO = v_periodo AND CODIGO_EMPRESA = v_empresa
       AND v_fecha BETWEEN FECHA_DESDE AND FECHA_HASTA FOR UPDATE;
    IF v_cierre IS NOT NULL THEN
        RAISE_APPLICATION_ERROR(-20001, 'El periodo esta cerrado.');
    END IF;
END;
/
CREATE OR REPLACE TRIGGER CNT.CNT_DET_WRITE_TRG
BEFORE INSERT OR UPDATE OR DELETE ON CNT.CNT_DETALLE_COMPROBANTE
FOR EACH ROW
DECLARE
    v_cmp NUMBER;
    v_empresa NUMBER;
    v_auto VARCHAR2(30);
    v_periodo NUMBER;
    v_cierre DATE;
BEGIN
    IF DELETING THEN
        v_cmp := :OLD.CODIGO_COMPROBANTE;
        v_empresa := :OLD.CODIGO_EMPRESA;
    ELSE
        v_cmp := :NEW.CODIGO_COMPROBANTE;
        v_empresa := :NEW.CODIGO_EMPRESA;
    END IF;
    IF UPDATING AND (v_cmp <> :OLD.CODIGO_COMPROBANTE OR v_empresa <> :OLD.CODIGO_EMPRESA) THEN
        RAISE_APPLICATION_ERROR(-20001, 'No puede mover un detalle a otro comprobante o empresa.');
    END IF;
    SELECT c.EXTRA1, p.FECHA_CIERRE, p.CODIGO_PERIODO INTO v_auto, v_cierre, v_periodo FROM CNT.CNT_COMPROBANTES c
      JOIN CNT.CNT_PERIODOS p ON p.CODIGO_PERIODO = c.CODIGO_PERIODO AND p.CODIGO_EMPRESA = c.CODIGO_EMPRESA
     WHERE c.CODIGO_COMPROBANTE = v_cmp AND c.CODIGO_EMPRESA = v_empresa;
    SELECT FECHA_CIERRE INTO v_cierre FROM CNT.CNT_PERIODOS
     WHERE CODIGO_PERIODO = v_periodo AND CODIGO_EMPRESA = v_empresa FOR UPDATE;
    IF v_auto = 'AUTOMATICO' AND NOT CNT.CNT_CMP_GUARD.CAN_WRITE(v_cmp, v_empresa) THEN
        RAISE_APPLICATION_ERROR(-20001, 'El automatico requiere el flujo CNT con permiso y control de version.');
    END IF;
    IF v_cierre IS NOT NULL THEN
        RAISE_APPLICATION_ERROR(-20001, 'El periodo esta cerrado.');
    END IF;
END;
/
-- Also protect period catalog and any legacy direct close operation.
CREATE OR REPLACE TRIGGER CNT.CNT_PER_PEND_TRG
BEFORE UPDATE OF FECHA_CIERRE ON CNT.CNT_PERIODOS
FOR EACH ROW
DECLARE
    v_count NUMBER;
BEGIN
    IF :NEW.FECHA_CIERRE IS NOT NULL THEN
        SELECT COUNT(*) INTO v_count FROM CNT.CNT_COMPROBANTES
         WHERE CODIGO_PERIODO = :NEW.CODIGO_PERIODO AND CODIGO_EMPRESA = :NEW.CODIGO_EMPRESA
           AND ESTADO_CMP = 'PENDIENTE';
        IF v_count > 0 THEN
            RAISE_APPLICATION_ERROR(-20001, 'No puede cerrar un periodo con comprobantes pendientes.');
        END IF;
    END IF;
END;
/
