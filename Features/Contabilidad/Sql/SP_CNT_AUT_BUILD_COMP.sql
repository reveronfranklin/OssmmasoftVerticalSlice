CREATE OR REPLACE PROCEDURE CNT.SP_CNT_AUT_BUILD_COMP (
    p_RUN_ID              IN VARCHAR2,
    p_ORIGEN_ID           IN NUMBER,
    p_FECHA_DESDE         IN DATE,
    p_FECHA_HASTA         IN DATE,
    p_CODIGO_PRESUPUESTO  IN NUMBER,
    p_CODIGO_EMPRESA      IN NUMBER,
    p_Message             OUT VARCHAR2,
    p_TIPO_COMPROBANTE_ID IN NUMBER DEFAULT NULL
) AS
    v_tipo_codigo VARCHAR2(10);
BEGIN
    -- Keep the seven existing positional arguments; new callers pass the type by name.
    IF p_TIPO_COMPROBANTE_ID IS NULL THEN
        p_Message := 'El tipo de comprobante es requerido por el constructor automatico.';
        RETURN;
    END IF;
    BEGIN
        SELECT TRIM(CODIGO) INTO v_tipo_codigo
          FROM CNT.CNT_DESCRIPTIVAS
         WHERE DESCRIPCION_ID = p_TIPO_COMPROBANTE_ID
           AND TITULO_ID = 5
           AND (CODIGO_EMPRESA = p_CODIGO_EMPRESA OR CODIGO_EMPRESA IS NULL);
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            p_Message := 'El tipo de comprobante no existe para la empresa configurada.';
            RETURN;
    END;
    IF v_tipo_codigo IS NULL OR NOT REGEXP_LIKE(v_tipo_codigo, '^[0-9]+$') THEN
        p_Message := 'El tipo de comprobante no tiene un codigo numerico valido.';
        RETURN;
    END IF;
    IF p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('COMP', 'ODC')
       AND LTRIM(v_tipo_codigo, '0') = '2' THEN
        CNT.SP_CNT_AUT_BUILD_COMP02(
            p_RUN_ID, p_FECHA_DESDE, p_FECHA_HASTA,
            p_CODIGO_PRESUPUESTO, p_CODIGO_EMPRESA, p_Message
        );
        RETURN;
    END IF;
    DELETE FROM CNT.CNT_AUT_LINE_WRK
     WHERE RUN_ID = p_RUN_ID
        OR FECHA_REGISTRO < SYSDATE - 1;

    INSERT INTO CNT.CNT_AUT_LINE_WRK (
        RUN_ID,
        SECUENCIA,
        CODIGO_MAYOR,
        MAYOR,
        CODIGO_AUXILIAR,
        AUXILIAR,
        REFERENCIA1,
        REFERENCIA2,
        REFERENCIA3,
        DESCRIPCION,
        MONTO
    )
    SELECT p_RUN_ID,
           ROWNUM,
           q.CODIGO_MAYOR,
           q.NUMERO_MAYOR,
           q.CODIGO_AUXILIAR,
           q.SEGMENTO1 || '-' || q.SEGMENTO2 || ' ' || q.DENOMINACION,
           SUBSTR(q.NUMERO_COMPROMISO, 1, 20),
           TO_CHAR(q.FECHA_COMPROMISO, 'DD/MM/RRRR'),
           NULL,
           SUBSTRB('(' || q.TIPO_COMPROMISO || ') ' || q.MOTIVO, 1, 200),
           CASE
               WHEN p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('COMP', 'ODC')
                    THEN CASE WHEN q.COLUMNA_BALANCE = 'H' THEN -ABS(NVL(q.MONTO_COMPROMISO, 0)) ELSE ABS(NVL(q.MONTO_COMPROMISO, 0)) END
               ELSE CASE WHEN q.COLUMNA_BALANCE = 'D' THEN -ABS(NVL(q.MONTO_COMPROMISO, 0)) ELSE ABS(NVL(q.MONTO_COMPROMISO, 0)) END
           END
      FROM (
            SELECT CM.COLUMNA_BALANCE,
                   AC.CODIGO_COMPROMISO,
                   AC.NUMERO_COMPROMISO,
                   AC.FECHA_COMPROMISO,
                   AP.NOMBRE_PROVEEDOR,
                   SUM(APC.MONTO) MONTO_COMPROMISO,
                   CASE WHEN CM.COLUMNA_BALANCE = 'H' THEN NVL(CA.CODIGO_MAYOR, 9) ELSE NVL(CA.CODIGO_MAYOR, 3) END CODIGO_MAYOR,
                   CA.CODIGO_AUXILIAR,
                   CASE WHEN CM.COLUMNA_BALANCE = 'H' THEN NVL(CM.NUMERO_MAYOR, '300') ELSE NVL(CM.NUMERO_MAYOR, '103') END NUMERO_MAYOR,
                   NVL(CA.SEGMENTO1, '001') SEGMENTO1,
                   NVL(CA.SEGMENTO2, '000') SEGMENTO2,
                   NVL(CA.DENOMINACION, '999') DENOMINACION,
                   RTRIM(AC.MOTIVO) MOTIVO,
                   'CP' TIPO_COMPROMISO
              FROM PRE.PRE_COMPROMISOS AC,
                   PRE.PRE_DETALLE_COMPROMISOS PDC,
                   PRE.PRE_PUC_COMPROMISOS APC,
                   ADM.ADM_PROVEEDORES AP,
                   PRE.PRE_V_SALDOS PVS,
                   CNT.CNT_MAYORES CM,
                   CNT.CNT_AUXILIARES CA
             WHERE AC.CODIGO_PRESUPUESTO = p_CODIGO_PRESUPUESTO
               AND AC.CODIGO_EMPRESA = p_CODIGO_EMPRESA
               AND AC.CODIGO_COMPROMISO = PDC.CODIGO_COMPROMISO
               AND PDC.CODIGO_DETALLE_COMPROMISO = APC.CODIGO_DETALLE_COMPROMISO
               AND AP.CODIGO_PROVEEDOR = AC.CODIGO_PROVEEDOR
               AND PVS.CODIGO_SALDO = APC.CODIGO_SALDO
               AND CM.COLUMNA_BALANCE IN ('D', 'H')
               AND CM.CODIGO_MAYOR = CA.CODIGO_MAYOR
               AND CA.CODIGO_PROVEEDOR = AC.CODIGO_PROVEEDOR
               AND AC.CODIGO_PROVEEDOR NOT IN (8127, 8128)
               AND (
                    (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('COMP', 'ODC')
                     AND AC.STATUS <> 'AN'
                     AND AC.FECHA_COMPROMISO >= p_FECHA_DESDE
                     AND AC.FECHA_COMPROMISO < p_FECHA_HASTA + 1)
                    OR
                    (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ANCOMP', 'ODC')
                     AND AC.STATUS = 'AN'
                     AND TRUNC(AC.FECHA_UPD) >= p_FECHA_DESDE
                     AND TRUNC(AC.FECHA_UPD) <= p_FECHA_HASTA)
               )
             GROUP BY CM.COLUMNA_BALANCE,
                      AC.CODIGO_COMPROMISO,
                      AC.NUMERO_COMPROMISO,
                      AC.FECHA_COMPROMISO,
                      AP.NOMBRE_PROVEEDOR,
                      CASE WHEN CM.COLUMNA_BALANCE = 'H' THEN NVL(CA.CODIGO_MAYOR, 9) ELSE NVL(CA.CODIGO_MAYOR, 3) END,
                      CA.CODIGO_AUXILIAR,
                      CASE WHEN CM.COLUMNA_BALANCE = 'H' THEN NVL(CM.NUMERO_MAYOR, '300') ELSE NVL(CM.NUMERO_MAYOR, '103') END,
                      NVL(CA.SEGMENTO1, '001'),
                      NVL(CA.SEGMENTO2, '000'),
                      NVL(CA.DENOMINACION, '999'),
                      RTRIM(AC.MOTIVO)
        ) q
     WHERE NVL(q.MONTO_COMPROMISO, 0) <> 0
     ORDER BY q.TIPO_COMPROMISO, q.CODIGO_COMPROMISO, q.COLUMNA_BALANCE DESC;

    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        DELETE FROM CNT.CNT_AUT_LINE_WRK
         WHERE RUN_ID = p_RUN_ID;
        p_Message := SQLERRM;
END;
/
