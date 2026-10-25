-- Reparacion acotada: OP 18409, documento 25705, presupuesto 20.
-- Ejecutar despues del despliegue del backend y SP_REP_ORD_PAGO_RET_GET.
-- No cambia importes, catalogos, estado, fecha ni numero de comprobante.
-- No confirma la transaccion: verificar el resultado y ejecutar COMMIT o ROLLBACK.
DECLARE
    v_cantidad NUMBER;
    v_op NUMBER;
    v_ret NUMBER;
    v_doc ADM.ADM_DOCUMENTOS_OP%ROWTYPE;
    v_fila ADM.ADM_RETENCIONES_OP%ROWTYPE;
    v_iva VARCHAR2(100);
    v_por VARCHAR2(100);
BEGIN
    SAVEPOINT fix_iva_op;
    SELECT CODIGO_ORDEN_PAGO INTO v_op
      FROM ADM.ADM_ORDEN_PAGO
     WHERE CODIGO_ORDEN_PAGO = 18409 AND CODIGO_PRESUPUESTO = 20
       FOR UPDATE;

    SELECT COUNT(*) INTO v_cantidad FROM ADM.ADM_DOCUMENTOS_OP
     WHERE CODIGO_ORDEN_PAGO = v_op;
    IF v_cantidad <> 1 THEN
        RAISE_APPLICATION_ERROR(-20001, 'La OP no tiene exactamente un documento; requiere revision');
    END IF;
    SELECT * INTO v_doc FROM ADM.ADM_DOCUMENTOS_OP
     WHERE CODIGO_DOCUMENTO_OP = 25705 AND CODIGO_ORDEN_PAGO = v_op
       AND CODIGO_PRESUPUESTO = 20 FOR UPDATE;
    SELECT TRIM(EXTRA1) INTO v_iva FROM ADM.ADM_DESCRIPTIVAS
     WHERE DESCRIPCION_ID = v_doc.TIPO_IMPUESTO_ID;
    SELECT TRIM(EXTRA1) INTO v_por FROM ADM.ADM_DESCRIPTIVAS
     WHERE DESCRIPCION_ID = v_doc.ESTATUS_FISCO_ID;
    IF NVL(v_iva, '?') NOT IN ('8', '8.0', '8.00')
       OR NVL(v_por, '?') NOT IN ('100', '100.0', '100.00')
       OR NVL(v_doc.TIPO_IMPUESTO_ID, -1) <> 672
       OR NVL(v_doc.MONTO_DOCUMENTO, -1) <> 234240
       OR NVL(v_doc.BASE_IMPONIBLE, -1) <> 216888.89
       OR NVL(v_doc.MONTO_IMPUESTO, -1) <> 17351.11
       OR NVL(v_doc.MONTO_RETENIDO, -1) <> 17351.11
       OR NVL(v_doc.MONTO_IMPUESTO_EXENTO, 0) <> 0 THEN
        RAISE_APPLICATION_ERROR(-20002, 'El documento no coincide con la evidencia; no se modifica');
    END IF;

    SELECT COUNT(*), MIN(R.CODIGO_RETENCION_OP)
      INTO v_cantidad, v_ret
      FROM ADM.ADM_RETENCIONES_OP R JOIN ADM.ADM_DESCRIPTIVAS D
        ON D.DESCRIPCION_ID = R.TIPO_RETENCION_ID
     WHERE R.CODIGO_ORDEN_PAGO = v_op AND D.CODIGO = 'IVA';
    IF v_cantidad <> 1 THEN
        RAISE_APPLICATION_ERROR(-20003, 'No existe una unica retencion IVA; requiere revision');
    END IF;
    SELECT * INTO v_fila FROM ADM.ADM_RETENCIONES_OP
     WHERE CODIGO_RETENCION_OP = v_ret FOR UPDATE;
    IF NVL(v_fila.POR_RETENCION, -1) <> 100
       OR NVL(v_fila.BASE_IMPONIBLE, -1) <> v_doc.BASE_IMPONIBLE
       OR NVL(v_fila.MONTO_RETENCION, -1) <> v_doc.MONTO_RETENIDO THEN
        RAISE_APPLICATION_ERROR(-20004, 'Importes de retencion no coinciden; no se modifica');
    END IF;
    IF NVL(v_fila.EXTRA4, '?') <> 'IVA_DOCUMENTO'
       AND (v_fila.EXTRA1 IS NOT NULL OR v_fila.EXTRA2 IS NOT NULL
            OR v_fila.EXTRA3 IS NOT NULL OR v_fila.EXTRA4 IS NOT NULL) THEN
        RAISE_APPLICATION_ERROR(-20005, 'Los campos EXTRA estan ocupados; requiere revision');
    END IF;

    UPDATE ADM.ADM_RETENCIONES_OP
       SET EXTRA1 = '672', EXTRA2 = '8', EXTRA3 = 'I.V.A. (8%)',
           EXTRA4 = 'IVA_DOCUMENTO'
     WHERE CODIGO_RETENCION_OP = v_ret;
    DBMS_OUTPUT.PUT_LINE('OP 18409: alicuota de retencion vinculada a documento 25705. Pendiente COMMIT.');
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK TO fix_iva_op;
        RAISE;
END;
/
