CREATE OR REPLACE PROCEDURE CNT.SP_CNT_AUT_BUILD_OP (
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
           SUBSTR(q.NUMERO_ORDEN_PAGO, 1, 20),
           TO_CHAR(q.FECHA_ORDEN_PAGO, 'DD/MM/RRRR'),
           NULL,
           SUBSTRB('(' || q.TIPO_COMPROMISO || ') ' || q.MOTIVO, 1, 200),
           CASE WHEN q.COLUMNA_BALANCE = 'D' THEN -ABS(NVL(q.MONTO, 0)) ELSE ABS(NVL(q.MONTO, 0)) END
      FROM (
            SELECT *
              FROM (
                    SELECT 'D' COLUMNA_BALANCE,
                           AC.CODIGO_ORDEN_PAGO,
                           AC.NUMERO_ORDEN_PAGO,
                           AC.FECHA_ORDEN_PAGO,
                           AP.NOMBRE_PROVEEDOR,
                           SUM(APC.MONTO) MONTO,
                           NVL(CA.CODIGO_MAYOR, 3) CODIGO_MAYOR,
                           NVL(CA.CODIGO_AUXILIAR, 1094) CODIGO_AUXILIAR,
                           NVL(CM.NUMERO_MAYOR, '103') NUMERO_MAYOR,
                           NVL(CA.SEGMENTO1, '001') SEGMENTO1,
                           NVL(CA.SEGMENTO2, '000') SEGMENTO2,
                           NVL(CA.DENOMINACION, '999') DENOMINACION,
                           RTRIM(AC.MOTIVO) MOTIVO,
                           CASE WHEN p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ANODPAUT', 'ODC') THEN 'AN' ELSE 'OP' END TIPO_COMPROMISO
                      FROM ADM.ADM_ORDEN_PAGO AC,
                           ADM.ADM_PUC_ORDEN_PAGO APC,
                           ADM.ADM_PROVEEDORES AP,
                           CNT.CNT_MAYORES CM,
                           CNT.CNT_AUXILIARES CA
                     WHERE AC.CODIGO_PRESUPUESTO = p_CODIGO_PRESUPUESTO
                       AND AC.CODIGO_EMPRESA = p_CODIGO_EMPRESA
                       AND APC.CODIGO_ORDEN_PAGO = AC.CODIGO_ORDEN_PAGO
                       AND AP.CODIGO_PROVEEDOR = AC.CODIGO_PROVEEDOR
                       AND CM.CODIGO_MAYOR = 3
                       AND CM.CODIGO_MAYOR = CA.CODIGO_MAYOR
                       AND CA.CODIGO_PROVEEDOR = AC.CODIGO_PROVEEDOR
                       AND CA.CODIGO_AUXILIAR NOT IN (
                            SELECT DISTINCT NUE.CODIGO_AUXILIAR
                              FROM CNT.CNT_AUXILIARES_PUC NUE,
                                   ADM.ADM_PUC_ORDEN_PAGO APC2
                             WHERE APC2.CODIGO_PUC = NUE.CODIGO_PUC
                               AND APC2.CODIGO_ORDEN_PAGO = AC.CODIGO_ORDEN_PAGO
                       )
                       AND NVL(ADM.ADM_F_DESCRIPTIVAS_ID('CODIGO', AC.TIPO_ORDEN_PAGO_ID, AC.CODIGO_EMPRESA), ' ') <> 'A'
                       AND EXISTS (
                            SELECT 1
                              FROM ADM.ADM_COMPROMISO_OP COMOP
                             WHERE COMOP.CODIGO_ORDEN_PAGO = AC.CODIGO_ORDEN_PAGO
                               AND COMOP.CODIGO_EMPRESA = AC.CODIGO_EMPRESA
                               AND NVL(ADM.ADM_F_DESCRIPTIVAS_ID('CODIGO', COMOP.ORIGEN_COMPROMISO_ID, COMOP.CODIGO_EMPRESA), ' ') <> 'CON'
                       )
                       AND (
                            (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ODPAUT', 'ODC')
                             AND AC.STATUS <> 'AN'
                             AND AC.FECHA_ORDEN_PAGO >= p_FECHA_DESDE
                             AND AC.FECHA_ORDEN_PAGO < p_FECHA_HASTA + 1)
                            OR
                            (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ANODPAUT', 'ODC')
                             AND AC.STATUS = 'AN'
                             AND TRUNC(AC.FECHA_UPD) >= p_FECHA_DESDE
                             AND TRUNC(AC.FECHA_UPD) <= p_FECHA_HASTA)
                       )
                     GROUP BY AC.CODIGO_ORDEN_PAGO,
                              AC.NUMERO_ORDEN_PAGO,
                              AC.FECHA_ORDEN_PAGO,
                              AP.NOMBRE_PROVEEDOR,
                              NVL(CA.CODIGO_MAYOR, 3),
                              NVL(CA.CODIGO_AUXILIAR, 1094),
                              NVL(CM.NUMERO_MAYOR, '103'),
                              NVL(CA.SEGMENTO1, '001'),
                              NVL(CA.SEGMENTO2, '000'),
                              NVL(CA.DENOMINACION, '999'),
                              RTRIM(AC.MOTIVO)
                    UNION ALL
                    SELECT 'H' COLUMNA_BALANCE,
                           AC.CODIGO_ORDEN_PAGO,
                           AC.NUMERO_ORDEN_PAGO,
                           AC.FECHA_ORDEN_PAGO,
                           AP.NOMBRE_PROVEEDOR,
                           SUM(APC.MONTO) MONTO,
                           NVL(CA.CODIGO_MAYOR, 1) CODIGO_MAYOR,
                           NVL(CA.CODIGO_AUXILIAR, 1094) CODIGO_AUXILIAR,
                           NVL(CM.NUMERO_MAYOR, '101') NUMERO_MAYOR,
                           NVL(CA.SEGMENTO1, '001') SEGMENTO1,
                           NVL(CA.SEGMENTO2, '000') SEGMENTO2,
                           NVL(CA.DENOMINACION, '999') DENOMINACION,
                           RTRIM(AC.MOTIVO) MOTIVO,
                           CASE WHEN p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ANODPAUT', 'ODC') THEN 'AN' ELSE 'OP' END TIPO_COMPROMISO
                      FROM ADM.ADM_ORDEN_PAGO AC,
                           ADM.ADM_PUC_ORDEN_PAGO APC,
                           ADM.ADM_PROVEEDORES AP,
                           CNT.CNT_MAYORES CM,
                           CNT.CNT_AUXILIARES CA
                     WHERE AC.CODIGO_PRESUPUESTO = p_CODIGO_PRESUPUESTO
                       AND AC.CODIGO_EMPRESA = p_CODIGO_EMPRESA
                       AND APC.CODIGO_ORDEN_PAGO = AC.CODIGO_ORDEN_PAGO
                       AND AP.CODIGO_PROVEEDOR = AC.CODIGO_PROVEEDOR
                       AND CM.CODIGO_MAYOR = 1
                       AND CM.CODIGO_MAYOR = CA.CODIGO_MAYOR
                       AND CA.CODIGO_PROVEEDOR = AC.CODIGO_PROVEEDOR
                       AND CA.CODIGO_AUXILIAR NOT IN (
                            SELECT DISTINCT NUE.CODIGO_AUXILIAR
                              FROM CNT.CNT_AUXILIARES_PUC NUE,
                                   ADM.ADM_PUC_ORDEN_PAGO APC2
                             WHERE APC2.CODIGO_PUC = NUE.CODIGO_PUC
                               AND APC2.CODIGO_ORDEN_PAGO = AC.CODIGO_ORDEN_PAGO
                       )
                       AND NVL(ADM.ADM_F_DESCRIPTIVAS_ID('CODIGO', AC.TIPO_ORDEN_PAGO_ID, AC.CODIGO_EMPRESA), ' ') <> 'A'
                       AND EXISTS (
                            SELECT 1
                              FROM ADM.ADM_COMPROMISO_OP COMOP
                             WHERE COMOP.CODIGO_ORDEN_PAGO = AC.CODIGO_ORDEN_PAGO
                               AND COMOP.CODIGO_EMPRESA = AC.CODIGO_EMPRESA
                               AND NVL(ADM.ADM_F_DESCRIPTIVAS_ID('CODIGO', COMOP.ORIGEN_COMPROMISO_ID, COMOP.CODIGO_EMPRESA), ' ') <> 'CON'
                       )
                       AND (
                            (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ODPAUT', 'ODC')
                             AND AC.STATUS <> 'AN'
                             AND AC.FECHA_ORDEN_PAGO >= p_FECHA_DESDE
                             AND AC.FECHA_ORDEN_PAGO < p_FECHA_HASTA + 1)
                            OR
                            (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ANODPAUT', 'ODC')
                             AND AC.STATUS = 'AN'
                             AND TRUNC(AC.FECHA_UPD) >= p_FECHA_DESDE
                             AND TRUNC(AC.FECHA_UPD) <= p_FECHA_HASTA)
                       )
                     GROUP BY AC.CODIGO_ORDEN_PAGO,
                              AC.NUMERO_ORDEN_PAGO,
                              AC.FECHA_ORDEN_PAGO,
                              AP.NOMBRE_PROVEEDOR,
                              NVL(CA.CODIGO_MAYOR, 1),
                              NVL(CA.CODIGO_AUXILIAR, 1094),
                              NVL(CM.NUMERO_MAYOR, '101'),
                              NVL(CA.SEGMENTO1, '001'),
                              NVL(CA.SEGMENTO2, '000'),
                              NVL(CA.DENOMINACION, '999'),
                              RTRIM(AC.MOTIVO)
                    UNION ALL
                    SELECT 'D' COLUMNA_BALANCE,
                           AC.CODIGO_ORDEN_PAGO,
                           AC.NUMERO_ORDEN_PAGO,
                           AC.FECHA_ORDEN_PAGO,
                           ADM.ADM_F_PROVEEDOR_ID('NOMBRE_PROVEEDOR', AC.CODIGO_PROVEEDOR) NOMBRE_PROVEEDOR,
                           SUM(APC.MONTO) MONTO,
                           NVL(CA.CODIGO_MAYOR, 3) CODIGO_MAYOR,
                           NVL(CA.CODIGO_AUXILIAR, 1094) CODIGO_AUXILIAR,
                           NVL(CM.NUMERO_MAYOR, '103') NUMERO_MAYOR,
                           NVL(CA.SEGMENTO1, '001') SEGMENTO1,
                           NVL(CA.SEGMENTO2, '000') SEGMENTO2,
                           NVL(CA.DENOMINACION, '999') DENOMINACION,
                           RTRIM(AC.MOTIVO) MOTIVO,
                           CASE WHEN p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ANODPAUT', 'ODC') THEN 'AN' ELSE 'OP' END TIPO_COMPROMISO
                      FROM ADM.ADM_ORDEN_PAGO AC,
                           ADM.ADM_PUC_ORDEN_PAGO APC,
                           CNT.CNT_MAYORES CM,
                           CNT.CNT_AUXILIARES CA
                     WHERE AC.CODIGO_PRESUPUESTO = p_CODIGO_PRESUPUESTO
                       AND AC.CODIGO_EMPRESA = p_CODIGO_EMPRESA
                       AND APC.CODIGO_ORDEN_PAGO = AC.CODIGO_ORDEN_PAGO
                       AND CM.CODIGO_MAYOR = 3
                       AND CM.CODIGO_MAYOR = CA.CODIGO_MAYOR
                       AND CA.CODIGO_AUXILIAR IN (
                            SELECT DISTINCT NUE.CODIGO_AUXILIAR
                              FROM CNT.CNT_AUXILIARES_PUC NUE,
                                   ADM.ADM_PUC_ORDEN_PAGO APC2
                             WHERE APC2.CODIGO_PUC = NUE.CODIGO_PUC
                               AND APC2.CODIGO_ORDEN_PAGO = AC.CODIGO_ORDEN_PAGO
                       )
                       AND NVL(ADM.ADM_F_DESCRIPTIVAS_ID('CODIGO', AC.TIPO_ORDEN_PAGO_ID, AC.CODIGO_EMPRESA), ' ') <> 'A'
                       AND (
                            (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ODPAUT', 'ODC') AND AC.STATUS <> 'AN' AND AC.FECHA_ORDEN_PAGO >= p_FECHA_DESDE AND AC.FECHA_ORDEN_PAGO < p_FECHA_HASTA + 1)
                            OR
                            (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ANODPAUT', 'ODC') AND AC.STATUS = 'AN' AND TRUNC(AC.FECHA_UPD) >= p_FECHA_DESDE AND TRUNC(AC.FECHA_UPD) <= p_FECHA_HASTA)
                       )
                     GROUP BY AC.CODIGO_ORDEN_PAGO, AC.NUMERO_ORDEN_PAGO, AC.FECHA_ORDEN_PAGO, AC.CODIGO_PROVEEDOR,
                              NVL(CA.CODIGO_MAYOR, 3), NVL(CA.CODIGO_AUXILIAR, 1094), NVL(CM.NUMERO_MAYOR, '103'),
                              NVL(CA.SEGMENTO1, '001'), NVL(CA.SEGMENTO2, '000'), NVL(CA.DENOMINACION, '999'), RTRIM(AC.MOTIVO)
                    UNION ALL
                    SELECT 'H' COLUMNA_BALANCE,
                           AC.CODIGO_ORDEN_PAGO,
                           AC.NUMERO_ORDEN_PAGO,
                           AC.FECHA_ORDEN_PAGO,
                           ADM.ADM_F_PROVEEDOR_ID('NOMBRE_PROVEEDOR', AC.CODIGO_PROVEEDOR) NOMBRE_PROVEEDOR,
                           SUM(APC.MONTO) MONTO,
                           NVL(CA.CODIGO_MAYOR, 1) CODIGO_MAYOR,
                           NVL(CA.CODIGO_AUXILIAR, 1094) CODIGO_AUXILIAR,
                           NVL(CM.NUMERO_MAYOR, '101') NUMERO_MAYOR,
                           NVL(CA.SEGMENTO1, '001') SEGMENTO1,
                           NVL(CA.SEGMENTO2, '000') SEGMENTO2,
                           NVL(CA.DENOMINACION, '999') DENOMINACION,
                           RTRIM(AC.MOTIVO) MOTIVO,
                           CASE WHEN p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ANODPAUT', 'ODC') THEN 'AN' ELSE 'OP' END TIPO_COMPROMISO
                      FROM ADM.ADM_ORDEN_PAGO AC,
                           ADM.ADM_PUC_ORDEN_PAGO APC,
                           CNT.CNT_MAYORES CM,
                           CNT.CNT_AUXILIARES CA
                     WHERE AC.CODIGO_PRESUPUESTO = p_CODIGO_PRESUPUESTO
                       AND AC.CODIGO_EMPRESA = p_CODIGO_EMPRESA
                       AND APC.CODIGO_ORDEN_PAGO = AC.CODIGO_ORDEN_PAGO
                       AND CM.CODIGO_MAYOR = 1
                       AND CM.CODIGO_MAYOR = CA.CODIGO_MAYOR
                       AND CA.CODIGO_AUXILIAR IN (
                            SELECT DISTINCT NUE.CODIGO_AUXILIAR
                              FROM CNT.CNT_AUXILIARES_PUC NUE,
                                   ADM.ADM_PUC_ORDEN_PAGO APC2
                             WHERE APC2.CODIGO_PUC = NUE.CODIGO_PUC
                               AND APC2.CODIGO_ORDEN_PAGO = AC.CODIGO_ORDEN_PAGO
                       )
                       AND NVL(ADM.ADM_F_DESCRIPTIVAS_ID('CODIGO', AC.TIPO_ORDEN_PAGO_ID, AC.CODIGO_EMPRESA), ' ') <> 'A'
                       AND (
                            (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ODPAUT', 'ODC') AND AC.STATUS <> 'AN' AND AC.FECHA_ORDEN_PAGO >= p_FECHA_DESDE AND AC.FECHA_ORDEN_PAGO < p_FECHA_HASTA + 1)
                            OR
                            (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ANODPAUT', 'ODC') AND AC.STATUS = 'AN' AND TRUNC(AC.FECHA_UPD) >= p_FECHA_DESDE AND TRUNC(AC.FECHA_UPD) <= p_FECHA_HASTA)
                       )
                     GROUP BY AC.CODIGO_ORDEN_PAGO, AC.NUMERO_ORDEN_PAGO, AC.FECHA_ORDEN_PAGO, AC.CODIGO_PROVEEDOR,
                              NVL(CA.CODIGO_MAYOR, 1), NVL(CA.CODIGO_AUXILIAR, 1094), NVL(CM.NUMERO_MAYOR, '101'),
                              NVL(CA.SEGMENTO1, '001'), NVL(CA.SEGMENTO2, '000'), NVL(CA.DENOMINACION, '999'), RTRIM(AC.MOTIVO)
                    UNION ALL
                    SELECT 'D' COLUMNA_BALANCE,
                           AC.CODIGO_ORDEN_PAGO,
                           AC.NUMERO_ORDEN_PAGO,
                           AC.FECHA_ORDEN_PAGO,
                           AP.NOMBRE_PROVEEDOR || ' (CAJA CHICA)' NOMBRE_PROVEEDOR,
                           SUM(APC.MONTO) MONTO,
                           NVL(CA.CODIGO_MAYOR, 11) CODIGO_MAYOR,
                           NVL(CA.CODIGO_AUXILIAR, 1094) CODIGO_AUXILIAR,
                           NVL(CM.NUMERO_MAYOR, '126') NUMERO_MAYOR,
                           NVL(CA.SEGMENTO1, '001') SEGMENTO1,
                           NVL(CA.SEGMENTO2, '000') SEGMENTO2,
                           NVL(CA.DENOMINACION, '999') DENOMINACION,
                           RTRIM(AC.MOTIVO) MOTIVO,
                           CASE WHEN p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ANODPAUT', 'ODC') THEN 'AN' ELSE 'OP' END TIPO_COMPROMISO
                      FROM ADM.ADM_ORDEN_PAGO AC,
                           ADM.ADM_BENEFICIARIOS_OP APC,
                           ADM.ADM_PROVEEDORES AP,
                           CNT.CNT_MAYORES CM,
                           CNT.CNT_AUXILIARES CA
                     WHERE AC.CODIGO_PRESUPUESTO = p_CODIGO_PRESUPUESTO
                       AND AC.CODIGO_EMPRESA = p_CODIGO_EMPRESA
                       AND APC.CODIGO_ORDEN_PAGO = AC.CODIGO_ORDEN_PAGO
                       AND AP.CODIGO_PROVEEDOR = AC.CODIGO_PROVEEDOR
                       AND CM.CODIGO_MAYOR = 11
                       AND CM.CODIGO_MAYOR = CA.CODIGO_MAYOR
                       AND CA.CODIGO_PROVEEDOR = AC.CODIGO_PROVEEDOR
                       AND ADM.ADM_F_DESCRIPTIVAS_ID('CODIGO', AC.TIPO_ORDEN_PAGO_ID, AC.CODIGO_EMPRESA) = 'A'
                       AND (
                            (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ODPAUT', 'ODC') AND AC.STATUS <> 'AN' AND AC.FECHA_ORDEN_PAGO >= p_FECHA_DESDE AND AC.FECHA_ORDEN_PAGO < p_FECHA_HASTA + 1)
                            OR
                            (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ANODPAUT', 'ODC') AND AC.STATUS = 'AN' AND TRUNC(AC.FECHA_UPD) >= p_FECHA_DESDE AND TRUNC(AC.FECHA_UPD) <= p_FECHA_HASTA)
                       )
                     GROUP BY AC.CODIGO_ORDEN_PAGO, AC.NUMERO_ORDEN_PAGO, AC.FECHA_ORDEN_PAGO, AP.NOMBRE_PROVEEDOR,
                              NVL(CA.CODIGO_MAYOR, 11), NVL(CA.CODIGO_AUXILIAR, 1094), NVL(CM.NUMERO_MAYOR, '126'),
                              NVL(CA.SEGMENTO1, '001'), NVL(CA.SEGMENTO2, '000'), NVL(CA.DENOMINACION, '999'), RTRIM(AC.MOTIVO)
                    UNION ALL
                    SELECT 'H' COLUMNA_BALANCE,
                           AC.CODIGO_ORDEN_PAGO,
                           AC.NUMERO_ORDEN_PAGO,
                           AC.FECHA_ORDEN_PAGO,
                           AP.NOMBRE_PROVEEDOR || ' (CAJA CHICA)' NOMBRE_PROVEEDOR,
                           SUM(APC.MONTO) MONTO,
                           NVL(CA.CODIGO_MAYOR, 1) CODIGO_MAYOR,
                           NVL(CA.CODIGO_AUXILIAR, 1094) CODIGO_AUXILIAR,
                           NVL(CM.NUMERO_MAYOR, '101') NUMERO_MAYOR,
                           NVL(CA.SEGMENTO1, '001') SEGMENTO1,
                           NVL(CA.SEGMENTO2, '000') SEGMENTO2,
                           NVL(CA.DENOMINACION, '999') DENOMINACION,
                           RTRIM(AC.MOTIVO) MOTIVO,
                           CASE WHEN p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ANODPAUT', 'ODC') THEN 'AN' ELSE 'OP' END TIPO_COMPROMISO
                      FROM ADM.ADM_ORDEN_PAGO AC,
                           ADM.ADM_BENEFICIARIOS_OP APC,
                           ADM.ADM_PROVEEDORES AP,
                           CNT.CNT_MAYORES CM,
                           CNT.CNT_AUXILIARES CA
                     WHERE AC.CODIGO_PRESUPUESTO = p_CODIGO_PRESUPUESTO
                       AND AC.CODIGO_EMPRESA = p_CODIGO_EMPRESA
                       AND APC.CODIGO_ORDEN_PAGO = AC.CODIGO_ORDEN_PAGO
                       AND AP.CODIGO_PROVEEDOR = AC.CODIGO_PROVEEDOR
                       AND CM.CODIGO_MAYOR = 1
                       AND CM.CODIGO_MAYOR = CA.CODIGO_MAYOR
                       AND CA.CODIGO_PROVEEDOR = AC.CODIGO_PROVEEDOR
                       AND ADM.ADM_F_DESCRIPTIVAS_ID('CODIGO', AC.TIPO_ORDEN_PAGO_ID, AC.CODIGO_EMPRESA) = 'A'
                       AND (
                            (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ODPAUT', 'ODC') AND AC.STATUS <> 'AN' AND AC.FECHA_ORDEN_PAGO >= p_FECHA_DESDE AND AC.FECHA_ORDEN_PAGO < p_FECHA_HASTA + 1)
                            OR
                            (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ANODPAUT', 'ODC') AND AC.STATUS = 'AN' AND TRUNC(AC.FECHA_UPD) >= p_FECHA_DESDE AND TRUNC(AC.FECHA_UPD) <= p_FECHA_HASTA)
                       )
                     GROUP BY AC.CODIGO_ORDEN_PAGO, AC.NUMERO_ORDEN_PAGO, AC.FECHA_ORDEN_PAGO, AP.NOMBRE_PROVEEDOR,
                              NVL(CA.CODIGO_MAYOR, 1), NVL(CA.CODIGO_AUXILIAR, 1094), NVL(CM.NUMERO_MAYOR, '101'),
                              NVL(CA.SEGMENTO1, '001'), NVL(CA.SEGMENTO2, '000'), NVL(CA.DENOMINACION, '999'), RTRIM(AC.MOTIVO)
                    UNION ALL
                    SELECT 'D' COLUMNA_BALANCE,
                           AC.CODIGO_ORDEN_PAGO,
                           AC.NUMERO_ORDEN_PAGO,
                           AC.FECHA_ORDEN_PAGO,
                           AP.NOMBRE_PROVEEDOR,
                           SUM(APC.MONTO) MONTO,
                           NVL(CA.CODIGO_MAYOR, 12) CODIGO_MAYOR,
                           NVL(CA.CODIGO_AUXILIAR, 1094) CODIGO_AUXILIAR,
                           NVL(CM.NUMERO_MAYOR, '127') NUMERO_MAYOR,
                           NVL(CA.SEGMENTO1, '001') SEGMENTO1,
                           NVL(CA.SEGMENTO2, '000') SEGMENTO2,
                           NVL(CA.DENOMINACION, '999') DENOMINACION,
                           RTRIM(AC.MOTIVO) MOTIVO,
                           CASE WHEN p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ANODPAUT', 'ODC') THEN 'AN' ELSE 'OP' END TIPO_COMPROMISO
                      FROM ADM.ADM_ORDEN_PAGO AC,
                           ADM.ADM_PUC_ORDEN_PAGO APC,
                           ADM.ADM_PROVEEDORES AP,
                           CNT.CNT_MAYORES CM,
                           CNT.CNT_AUXILIARES CA
                     WHERE AC.CODIGO_PRESUPUESTO = p_CODIGO_PRESUPUESTO
                       AND AC.CODIGO_EMPRESA = p_CODIGO_EMPRESA
                       AND APC.CODIGO_ORDEN_PAGO = AC.CODIGO_ORDEN_PAGO
                       AND AP.CODIGO_PROVEEDOR = AC.CODIGO_PROVEEDOR
                       AND CM.CODIGO_MAYOR = 12
                       AND CM.CODIGO_MAYOR = CA.CODIGO_MAYOR
                       AND CA.CODIGO_PROVEEDOR = AC.CODIGO_PROVEEDOR
                       AND EXISTS (
                            SELECT 1
                              FROM ADM.ADM_COMPROMISO_OP COMOP
                             WHERE COMOP.CODIGO_ORDEN_PAGO = AC.CODIGO_ORDEN_PAGO
                               AND COMOP.CODIGO_EMPRESA = AC.CODIGO_EMPRESA
                               AND ADM.ADM_F_DESCRIPTIVAS_ID('CODIGO', COMOP.ORIGEN_COMPROMISO_ID, COMOP.CODIGO_EMPRESA) = 'CON'
                       )
                       AND (
                            (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ODPAUT', 'ODC') AND AC.STATUS <> 'AN' AND AC.FECHA_ORDEN_PAGO >= p_FECHA_DESDE AND AC.FECHA_ORDEN_PAGO < p_FECHA_HASTA + 1)
                            OR
                            (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ANODPAUT', 'ODC') AND AC.STATUS = 'AN' AND TRUNC(AC.FECHA_UPD) >= p_FECHA_DESDE AND TRUNC(AC.FECHA_UPD) <= p_FECHA_HASTA)
                       )
                     GROUP BY AC.CODIGO_ORDEN_PAGO, AC.NUMERO_ORDEN_PAGO, AC.FECHA_ORDEN_PAGO, AP.NOMBRE_PROVEEDOR,
                              NVL(CA.CODIGO_MAYOR, 12), NVL(CA.CODIGO_AUXILIAR, 1094), NVL(CM.NUMERO_MAYOR, '127'),
                              NVL(CA.SEGMENTO1, '001'), NVL(CA.SEGMENTO2, '000'), NVL(CA.DENOMINACION, '999'), RTRIM(AC.MOTIVO)
                    UNION ALL
                    SELECT 'H' COLUMNA_BALANCE,
                           AC.CODIGO_ORDEN_PAGO,
                           AC.NUMERO_ORDEN_PAGO,
                           AC.FECHA_ORDEN_PAGO,
                           AP.NOMBRE_PROVEEDOR,
                           SUM(APC.MONTO) MONTO,
                           NVL(CA.CODIGO_MAYOR, 1) CODIGO_MAYOR,
                           NVL(CA.CODIGO_AUXILIAR, 1094) CODIGO_AUXILIAR,
                           NVL(CM.NUMERO_MAYOR, '101') NUMERO_MAYOR,
                           NVL(CA.SEGMENTO1, '001') SEGMENTO1,
                           NVL(CA.SEGMENTO2, '000') SEGMENTO2,
                           NVL(CA.DENOMINACION, '999') DENOMINACION,
                           RTRIM(AC.MOTIVO) MOTIVO,
                           CASE WHEN p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ANODPAUT', 'ODC') THEN 'AN' ELSE 'OP' END TIPO_COMPROMISO
                      FROM ADM.ADM_ORDEN_PAGO AC,
                           ADM.ADM_PUC_ORDEN_PAGO APC,
                           ADM.ADM_PROVEEDORES AP,
                           CNT.CNT_MAYORES CM,
                           CNT.CNT_AUXILIARES CA
                     WHERE AC.CODIGO_PRESUPUESTO = p_CODIGO_PRESUPUESTO
                       AND AC.CODIGO_EMPRESA = p_CODIGO_EMPRESA
                       AND APC.CODIGO_ORDEN_PAGO = AC.CODIGO_ORDEN_PAGO
                       AND AP.CODIGO_PROVEEDOR = AC.CODIGO_PROVEEDOR
                       AND CM.CODIGO_MAYOR = 1
                       AND CM.CODIGO_MAYOR = CA.CODIGO_MAYOR
                       AND CA.CODIGO_PROVEEDOR = AC.CODIGO_PROVEEDOR
                       AND EXISTS (
                            SELECT 1
                              FROM ADM.ADM_COMPROMISO_OP COMOP
                             WHERE COMOP.CODIGO_ORDEN_PAGO = AC.CODIGO_ORDEN_PAGO
                               AND COMOP.CODIGO_EMPRESA = AC.CODIGO_EMPRESA
                               AND ADM.ADM_F_DESCRIPTIVAS_ID('CODIGO', COMOP.ORIGEN_COMPROMISO_ID, COMOP.CODIGO_EMPRESA) = 'CON'
                       )
                       AND (
                            (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ODPAUT', 'ODC') AND AC.STATUS <> 'AN' AND AC.FECHA_ORDEN_PAGO >= p_FECHA_DESDE AND AC.FECHA_ORDEN_PAGO < p_FECHA_HASTA + 1)
                            OR
                            (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ANODPAUT', 'ODC') AND AC.STATUS = 'AN' AND TRUNC(AC.FECHA_UPD) >= p_FECHA_DESDE AND TRUNC(AC.FECHA_UPD) <= p_FECHA_HASTA)
                       )
                     GROUP BY AC.CODIGO_ORDEN_PAGO, AC.NUMERO_ORDEN_PAGO, AC.FECHA_ORDEN_PAGO, AP.NOMBRE_PROVEEDOR,
                              NVL(CA.CODIGO_MAYOR, 1), NVL(CA.CODIGO_AUXILIAR, 1094), NVL(CM.NUMERO_MAYOR, '101'),
                              NVL(CA.SEGMENTO1, '001'), NVL(CA.SEGMENTO2, '000'), NVL(CA.DENOMINACION, '999'), RTRIM(AC.MOTIVO)
              )
             WHERE (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ODPAUT', 'ODC') OR COLUMNA_BALANCE IN ('D', 'H'))
        ) q
     WHERE NVL(q.MONTO, 0) <> 0
     ORDER BY q.TIPO_COMPROMISO, q.CODIGO_ORDEN_PAGO, q.CODIGO_MAYOR;

    IF p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ANODPAUT', 'ODC') THEN
        UPDATE CNT.CNT_AUT_LINE_WRK
           SET MONTO = -MONTO
         WHERE RUN_ID = p_RUN_ID;
    END IF;

    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        DELETE FROM CNT.CNT_AUT_LINE_WRK
         WHERE RUN_ID = p_RUN_ID;
        p_Message := SQLERRM;
END;
/
