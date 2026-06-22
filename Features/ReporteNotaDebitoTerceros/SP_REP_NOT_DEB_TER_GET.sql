CREATE OR REPLACE PROCEDURE ADM.SP_REP_NOT_DEB_TER_GET (
    p_CodigoLotePago IN NUMBER,
    p_CodigoPago     IN NUMBER,
    p_ResultSet      OUT SYS_REFCURSOR,
    p_Message        OUT VARCHAR2,
    p_TotalRecords   OUT NUMBER
) AS
BEGIN
    SELECT COUNT(*)
      INTO p_TotalRecords
      FROM ADM.ADM_V_NOTAS_TERCEROS V
     WHERE V.CODIGO_LOTE_PAGO = p_CodigoLotePago
       AND (NVL(p_CodigoPago, 0) = 0 OR V.CODIGO_CHEQUE = p_CodigoPago);

    OPEN p_ResultSet FOR
        SELECT
            V.CODIGO_LOTE_PAGO,
            V.CODIGO_CHEQUE,
            TO_CHAR(V.NUMERO_CHEQUE) NUMERO_CHEQUE,
            V.FECHA_CHEQUE,
            NVL(V.NOMBRE, '') NOMBRE,
            NVL(V.NO_CUENTA, '') NO_CUENTA,
            NVL(V.PAGAR_A_LA_ORDEN_DE, '') PAGAR_A_LA_ORDEN_DE,
            NVL(V.MOTIVO, '') MOTIVO,
            NVL(V.MONTO, 0) MONTO,
            NVL(V.DETALLE_OP_ICP_PUC, '') DETALLE_OP_ICP_PUC,
            NVL(V.MONTO_OP_ICP_PUC, 0) MONTO_OP_ICP_PUC,
            NVL(V.DETALLE_IMP_RET, '') DETALLE_IMP_RET,
            NVL(V.MONTO_IMP_RET, 0) MONTO_IMP_RET
          FROM ADM.ADM_V_NOTAS_TERCEROS V
         WHERE V.CODIGO_LOTE_PAGO = p_CodigoLotePago
           AND (NVL(p_CodigoPago, 0) = 0 OR V.CODIGO_CHEQUE = p_CodigoPago)
         ORDER BY V.CODIGO_CHEQUE;

    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SUBSTR(SQLERRM, 1, 4000);
        p_TotalRecords := 0;
        OPEN p_ResultSet FOR
            SELECT
                CAST(NULL AS NUMBER) CODIGO_LOTE_PAGO,
                CAST(NULL AS NUMBER) CODIGO_CHEQUE,
                CAST(NULL AS VARCHAR2(100)) NUMERO_CHEQUE,
                CAST(NULL AS DATE) FECHA_CHEQUE,
                CAST(NULL AS VARCHAR2(4000)) NOMBRE,
                CAST(NULL AS VARCHAR2(100)) NO_CUENTA,
                CAST(NULL AS VARCHAR2(4000)) PAGAR_A_LA_ORDEN_DE,
                CAST(NULL AS VARCHAR2(4000)) MOTIVO,
                CAST(NULL AS NUMBER) MONTO,
                CAST(NULL AS VARCHAR2(4000)) DETALLE_OP_ICP_PUC,
                CAST(NULL AS NUMBER) MONTO_OP_ICP_PUC,
                CAST(NULL AS VARCHAR2(4000)) DETALLE_IMP_RET,
                CAST(NULL AS NUMBER) MONTO_IMP_RET
              FROM DUAL
             WHERE 1 = 0;
END SP_REP_NOT_DEB_TER_GET;
/
