DECLARE
    v_count NUMBER;
BEGIN
    SELECT COUNT(*)
      INTO v_count
      FROM USER_SEQUENCES
     WHERE SEQUENCE_NAME = 'SEQ_CNT_AUT_LOG';

    IF v_count = 0 THEN
        EXECUTE IMMEDIATE 'CREATE SEQUENCE CNT.SEQ_CNT_AUT_LOG START WITH 1 INCREMENT BY 1 NOCACHE';
    END IF;
END;
/

DECLARE
    v_count NUMBER;
BEGIN
    SELECT COUNT(*)
      INTO v_count
      FROM USER_TABLES
     WHERE TABLE_NAME = 'CNT_AUTOMATICO_LOG';

    IF v_count = 0 THEN
        EXECUTE IMMEDIATE '
            CREATE TABLE CNT.CNT_AUTOMATICO_LOG (
                CODIGO_LOG NUMBER NOT NULL,
                OPERACION VARCHAR2(20) NOT NULL,
                CODIGO_PERIODO NUMBER,
                TIPO_COMPROBANTE_ID NUMBER,
                ORIGEN_ID NUMBER,
                FECHA_DESDE DATE,
                FECHA_HASTA DATE,
                FECHA_COMPROBANTE DATE,
                CODIGO_COMPROBANTE NUMBER,
                NUMERO_COMPROBANTE VARCHAR2(20),
                CANTIDAD_LINEAS NUMBER,
                TOTAL_DEBE NUMBER(18,2),
                TOTAL_HABER NUMBER(18,2),
                USUARIO_ID NUMBER,
                CODIGO_EMPRESA NUMBER,
                ESTADO VARCHAR2(20),
                MENSAJE VARCHAR2(4000),
                FECHA_REGISTRO DATE DEFAULT SYSDATE NOT NULL,
                CONSTRAINT PK_CNT_AUTOMATICO_LOG PRIMARY KEY (CODIGO_LOG)
            )';
    END IF;
END;
/

DECLARE
    v_count NUMBER;
BEGIN
    SELECT COUNT(*)
      INTO v_count
      FROM USER_TABLES
     WHERE TABLE_NAME = 'CNT_AUT_LINE_TMP';

    IF v_count = 0 THEN
        EXECUTE IMMEDIATE '
            CREATE GLOBAL TEMPORARY TABLE CNT.CNT_AUT_LINE_TMP (
                SECUENCIA NUMBER,
                CODIGO_MAYOR NUMBER,
                MAYOR VARCHAR2(400),
                CODIGO_AUXILIAR NUMBER,
                AUXILIAR VARCHAR2(400),
                REFERENCIA1 VARCHAR2(20),
                REFERENCIA2 VARCHAR2(20),
                REFERENCIA3 VARCHAR2(20),
                DESCRIPCION VARCHAR2(200),
                MONTO NUMBER(18,2)
            ) ON COMMIT DELETE ROWS';
    END IF;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_AUT_LOG_INS (
    p_OPERACION           IN VARCHAR2,
    p_CODIGO_PERIODO      IN NUMBER,
    p_TIPO_COMPROBANTE_ID IN NUMBER,
    p_ORIGEN_ID           IN NUMBER,
    p_FECHA_DESDE         IN DATE,
    p_FECHA_HASTA         IN DATE,
    p_FECHA_COMPROBANTE   IN DATE,
    p_CODIGO_COMPROBANTE  IN NUMBER,
    p_NUMERO_COMPROBANTE  IN VARCHAR2,
    p_CANTIDAD_LINEAS     IN NUMBER,
    p_TOTAL_DEBE          IN NUMBER,
    p_TOTAL_HABER         IN NUMBER,
    p_USUARIO_ID          IN NUMBER,
    p_CODIGO_EMPRESA      IN NUMBER,
    p_ESTADO              IN VARCHAR2,
    p_MENSAJE             IN VARCHAR2
) AS
    PRAGMA AUTONOMOUS_TRANSACTION;
BEGIN
    INSERT INTO CNT.CNT_AUTOMATICO_LOG (
        CODIGO_LOG,
        OPERACION,
        CODIGO_PERIODO,
        TIPO_COMPROBANTE_ID,
        ORIGEN_ID,
        FECHA_DESDE,
        FECHA_HASTA,
        FECHA_COMPROBANTE,
        CODIGO_COMPROBANTE,
        NUMERO_COMPROBANTE,
        CANTIDAD_LINEAS,
        TOTAL_DEBE,
        TOTAL_HABER,
        USUARIO_ID,
        CODIGO_EMPRESA,
        ESTADO,
        MENSAJE,
        FECHA_REGISTRO
    ) VALUES (
        CNT.SEQ_CNT_AUT_LOG.NEXTVAL,
        SUBSTR(p_OPERACION, 1, 20),
        p_CODIGO_PERIODO,
        p_TIPO_COMPROBANTE_ID,
        p_ORIGEN_ID,
        p_FECHA_DESDE,
        p_FECHA_HASTA,
        p_FECHA_COMPROBANTE,
        p_CODIGO_COMPROBANTE,
        SUBSTR(p_NUMERO_COMPROBANTE, 1, 20),
        p_CANTIDAD_LINEAS,
        p_TOTAL_DEBE,
        p_TOTAL_HABER,
        p_USUARIO_ID,
        p_CODIGO_EMPRESA,
        SUBSTR(p_ESTADO, 1, 20),
        SUBSTR(p_MENSAJE, 1, 4000),
        SYSDATE
    );

    COMMIT;
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_AUT_BUILD_OP (
    p_ORIGEN_ID           IN NUMBER,
    p_FECHA_DESDE         IN DATE,
    p_FECHA_HASTA         IN DATE,
    p_CODIGO_PRESUPUESTO  IN NUMBER,
    p_CODIGO_EMPRESA      IN NUMBER,
    p_Message             OUT VARCHAR2
) AS
BEGIN
    DELETE FROM CNT.CNT_AUT_LINE_TMP;

    INSERT INTO CNT.CNT_AUT_LINE_TMP (
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
    SELECT ROWNUM,
           q.CODIGO_MAYOR,
           q.NUMERO_MAYOR,
           q.CODIGO_AUXILIAR,
           q.SEGMENTO1 || '-' || q.SEGMENTO2 || ' ' || q.DENOMINACION,
           SUBSTR(q.NUMERO_ORDEN_PAGO, 1, 20),
           TO_CHAR(q.FECHA_ORDEN_PAGO, 'DD/MM/RRRR'),
           NULL,
           SUBSTR('(' || q.TIPO_COMPROMISO || ') ' || q.MOTIVO, 1, 200),
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
        UPDATE CNT.CNT_AUT_LINE_TMP
           SET MONTO = -MONTO;
    END IF;

    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        DELETE FROM CNT.CNT_AUT_LINE_TMP;
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_CAT_DESC_GET (
    p_TITULO_ID      IN NUMBER,
    p_SEARCH_TEXT    IN VARCHAR2,
    p_CODIGO_EMPRESA IN NUMBER,
    p_ResultSet      OUT SYS_REFCURSOR,
    p_Message        OUT VARCHAR2
) AS
BEGIN
    OPEN p_ResultSet FOR
        SELECT DESCRIPCION_ID AS ID,
               CODIGO,
               DESCRIPCION,
               EXTRA1,
               EXTRA2,
               EXTRA3
          FROM CNT.CNT_DESCRIPTIVAS
         WHERE TITULO_ID = p_TITULO_ID
           AND (CODIGO_EMPRESA IS NULL OR CODIGO_EMPRESA = p_CODIGO_EMPRESA)
           AND (p_SEARCH_TEXT IS NULL
                OR UPPER(CODIGO) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%'
                OR UPPER(DESCRIPCION) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%')
         ORDER BY CODIGO, DESCRIPCION;

    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_AUT_PREV (
    p_CODIGO_PERIODO      IN NUMBER,
    p_TIPO_COMPROBANTE_ID IN NUMBER,
    p_ORIGEN_ID           IN NUMBER,
    p_FECHA_DESDE         IN DATE,
    p_FECHA_HASTA         IN DATE,
    p_USUARIO_ID          IN NUMBER,
    p_CODIGO_EMPRESA      IN NUMBER,
    p_ResultSet           OUT SYS_REFCURSOR,
    p_Message             OUT VARCHAR2
) AS
    v_dummy              NUMBER;
    v_codigo_presupuesto NUMBER;

    PROCEDURE open_empty_result IS
    BEGIN
        OPEN p_ResultSet FOR
            SELECT CAST(NULL AS NUMBER) SECUENCIA,
                   CAST(NULL AS NUMBER) CODIGO_MAYOR,
                   CAST(NULL AS VARCHAR2(400)) MAYOR,
                   CAST(NULL AS NUMBER) CODIGO_AUXILIAR,
                   CAST(NULL AS VARCHAR2(400)) AUXILIAR,
                   CAST(NULL AS VARCHAR2(20)) REFERENCIA1,
                   CAST(NULL AS VARCHAR2(20)) REFERENCIA2,
                   CAST(NULL AS VARCHAR2(20)) REFERENCIA3,
                   CAST(NULL AS VARCHAR2(200)) DESCRIPCION,
                   CAST(NULL AS NUMBER) MONTO
              FROM DUAL
             WHERE 1 = 0;
    END;
BEGIN
    IF p_FECHA_DESDE IS NULL OR p_FECHA_HASTA IS NULL THEN
        p_Message := 'El rango de fechas es requerido.';
        open_empty_result;
        RETURN;
    END IF;

    IF p_FECHA_DESDE > p_FECHA_HASTA THEN
        p_Message := 'La fecha desde no puede ser mayor que la fecha hasta.';
        open_empty_result;
        RETURN;
    END IF;

    SELECT 1
      INTO v_dummy
      FROM CNT.CNT_PERIODOS
     WHERE CODIGO_PERIODO = p_CODIGO_PERIODO
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA
       AND FECHA_CIERRE IS NULL;

    SELECT 1
      INTO v_dummy
      FROM CNT.CNT_DESCRIPTIVAS
     WHERE DESCRIPCION_ID = p_TIPO_COMPROBANTE_ID
       AND TITULO_ID = 5
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    SELECT 1
      INTO v_dummy
      FROM CNT.CNT_DESCRIPTIVAS
     WHERE DESCRIPCION_ID = p_ORIGEN_ID
       AND TITULO_ID = 1
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    BEGIN
        SELECT CODIGO_PRESUPUESTO
          INTO v_codigo_presupuesto
          FROM PRE.PRE_PRESUPUESTOS
         WHERE p_FECHA_DESDE BETWEEN FECHA_DESDE AND FECHA_HASTA;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            v_codigo_presupuesto := NULL;
    END;

    IF p_ORIGEN_ID IN (
        CNT.CNT_DESCRIPTIVA_ID('ODPAUT', 'ODC'),
        CNT.CNT_DESCRIPTIVA_ID('ANODPAUT', 'ODC')
    ) THEN
        IF v_codigo_presupuesto IS NULL THEN
            open_empty_result;
            p_Message := 'No existe presupuesto vigente para el rango indicado.';
            RETURN;
        END IF;

        CNT.SP_CNT_AUT_BUILD_OP(
            p_ORIGEN_ID,
            p_FECHA_DESDE,
            p_FECHA_HASTA,
            v_codigo_presupuesto,
            p_CODIGO_EMPRESA,
            p_Message
        );

        IF NVL(p_Message, 'Error') <> 'Success' THEN
            open_empty_result;
            RETURN;
        END IF;

        OPEN p_ResultSet FOR
            SELECT SECUENCIA,
                   CODIGO_MAYOR,
                   MAYOR,
                   CODIGO_AUXILIAR,
                   AUXILIAR,
                   REFERENCIA1,
                   REFERENCIA2,
                   REFERENCIA3,
                   DESCRIPCION,
                   MONTO
              FROM CNT.CNT_AUT_LINE_TMP
             ORDER BY SECUENCIA;

        p_Message := 'Success';
        RETURN;
    END IF;

    IF p_ORIGEN_ID IN (
        CNT.CNT_DESCRIPTIVA_ID('COMPORDCOM', 'ODC'),
        CNT.CNT_DESCRIPTIVA_ID('ANCOMPORDC', 'ODC')
    ) THEN
        IF v_codigo_presupuesto IS NULL THEN
            open_empty_result;
            p_Message := 'No existe presupuesto vigente para el rango indicado.';
            RETURN;
        END IF;

        OPEN p_ResultSet FOR
            SELECT ROWNUM SECUENCIA,
                   ordered_rows.CODIGO_MAYOR,
                   ordered_rows.MAYOR,
                   ordered_rows.CODIGO_AUXILIAR,
                   ordered_rows.AUXILIAR,
                   ordered_rows.REFERENCIA1,
                   ordered_rows.REFERENCIA2,
                   ordered_rows.REFERENCIA3,
                   ordered_rows.DESCRIPCION,
                   ordered_rows.MONTO
              FROM (
                    SELECT x.CODIGO_MAYOR,
                           x.NUMERO_MAYOR MAYOR,
                           x.CODIGO_AUXILIAR,
                           x.SEGMENTO1 || '-' || x.SEGMENTO2 || ' ' || x.DENOMINACION AUXILIAR,
                           SUBSTR(x.NUMERO_COMPROMISO, 1, 20) REFERENCIA1,
                           TO_CHAR(x.FECHA_COMPROMISO, 'DD/MM/RRRR') REFERENCIA2,
                           NULL REFERENCIA3,
                           SUBSTR('(' || x.TIPO_COMPROMISO || ') ' || x.MOTIVO, 1, 200) DESCRIPCION,
                           CASE
                               WHEN p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('COMPORDCOM', 'ODC') AND x.CODIGO_MAYOR = 9 THEN -ABS(NVL(x.MONTO_COMPROMISO, 0))
                               WHEN p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('COMPORDCOM', 'ODC') THEN ABS(NVL(x.MONTO_COMPROMISO, 0))
                               WHEN x.CODIGO_MAYOR = 9 THEN ABS(NVL(x.MONTO_COMPROMISO, 0))
                               ELSE -ABS(NVL(x.MONTO_COMPROMISO, 0))
                           END MONTO
                      FROM (
                            SELECT A.CODIGO_ORDEN_COMPRA CODIGO_COMPROMISO,
                                   A.NUMERO_ORDEN_COMPRA NUMERO_COMPROMISO,
                                   A.FECHA_ORDEN_COMPRA FECHA_COMPROMISO,
                                   A.CODIGO_PROVEEDOR,
                                   B.NOMBRE_PROVEEDOR,
                                   NULL FINANCIADO_POR,
                                   NVL(SUM(D.MONTO), 0) MONTO_COMPROMISO,
                                   NVL(CA.CODIGO_MAYOR, 9) CODIGO_MAYOR,
                                   NVL(CA.CODIGO_AUXILIAR, 1094) CODIGO_AUXILIAR,
                                   NVL(CA.SEGMENTO1, '001') SEGMENTO1,
                                   NVL(CA.SEGMENTO2, '000') SEGMENTO2,
                                   NVL(CA.DENOMINACION, '999') DENOMINACION,
                                   NVL(CM.NUMERO_MAYOR, '300') NUMERO_MAYOR,
                                   NULL STATUS,
                                   A.MOTIVO,
                                   'OC' TIPO_COMPROMISO
                              FROM ADM.ADM_ORDEN_COMPRA A,
                                   ADM.ADM_PROVEEDORES B,
                                   ADM.ADM_DETALLE_ORDEN_COMPRA C,
                                   ADM.ADM_PUC_ORDEN_COMPRA D,
                                   CNT.CNT_MAYORES CM,
                                   CNT.CNT_AUXILIARES CA
                             WHERE A.CODIGO_PRESUPUESTO = v_codigo_presupuesto
                               AND B.CODIGO_PROVEEDOR = A.CODIGO_PROVEEDOR
                               AND C.CODIGO_ORDEN_COMPRA(+) = A.CODIGO_ORDEN_COMPRA
                               AND D.CODIGO_DETALLE_ORDEN_COMPRA(+) = C.CODIGO_DETALLE_ORDEN_COMPRA
                               AND CM.CODIGO_MAYOR = 9
                               AND CM.CODIGO_MAYOR = CA.CODIGO_MAYOR
                               AND CA.CODIGO_PROVEEDOR = B.CODIGO_PROVEEDOR
                               AND (
                                    (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('COMPORDCOM', 'ODC')
                                     AND A.FECHA_ORDEN_COMPRA >= p_FECHA_DESDE
                                     AND A.FECHA_ORDEN_COMPRA < p_FECHA_HASTA + 1)
                                    OR
                                    (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ANCOMPORDC', 'ODC')
                                     AND A.FECHA_UPD >= p_FECHA_DESDE
                                     AND A.FECHA_UPD < p_FECHA_HASTA + 1)
                               )
                             GROUP BY A.CODIGO_ORDEN_COMPRA,
                                      A.NUMERO_ORDEN_COMPRA,
                                      A.FECHA_ORDEN_COMPRA,
                                      A.CODIGO_PROVEEDOR,
                                      B.NOMBRE_PROVEEDOR,
                                      NULL,
                                      NVL(CA.CODIGO_MAYOR, 9),
                                      NVL(CA.CODIGO_AUXILIAR, 1094),
                                      NVL(CA.SEGMENTO1, '001'),
                                      NVL(CA.SEGMENTO2, '000'),
                                      NVL(CA.DENOMINACION, '999'),
                                      NVL(CM.NUMERO_MAYOR, '300'),
                                      NULL,
                                      A.MOTIVO,
                                      'OC'
                            UNION ALL
                            SELECT A.CODIGO_ORDEN_COMPRA CODIGO_COMPROMISO,
                                   A.NUMERO_ORDEN_COMPRA NUMERO_COMPROMISO,
                                   A.FECHA_ORDEN_COMPRA FECHA_COMPROMISO,
                                   A.CODIGO_PROVEEDOR,
                                   B.NOMBRE_PROVEEDOR,
                                   NULL FINANCIADO_POR,
                                   NVL(SUM(D.MONTO - D.MONTO_ANULADO), 0) MONTO_COMPROMISO,
                                   NVL(CA.CODIGO_MAYOR, 3) CODIGO_MAYOR,
                                   NVL(CA.CODIGO_AUXILIAR, 1094) CODIGO_AUXILIAR,
                                   NVL(CA.SEGMENTO1, '001') SEGMENTO1,
                                   NVL(CA.SEGMENTO2, '000') SEGMENTO2,
                                   NVL(CA.DENOMINACION, '999') DENOMINACION,
                                   NVL(CM.NUMERO_MAYOR, '103') NUMERO_MAYOR,
                                   NULL STATUS,
                                   A.MOTIVO,
                                   'OC' TIPO_COMPROMISO
                              FROM ADM.ADM_ORDEN_COMPRA A,
                                   ADM.ADM_PROVEEDORES B,
                                   ADM.ADM_DETALLE_ORDEN_COMPRA C,
                                   ADM.ADM_PUC_ORDEN_COMPRA D,
                                   CNT.CNT_MAYORES CM,
                                   CNT.CNT_AUXILIARES CA
                             WHERE A.CODIGO_PRESUPUESTO = v_codigo_presupuesto
                               AND B.CODIGO_PROVEEDOR = A.CODIGO_PROVEEDOR
                               AND C.CODIGO_ORDEN_COMPRA(+) = A.CODIGO_ORDEN_COMPRA
                               AND D.CODIGO_DETALLE_ORDEN_COMPRA(+) = C.CODIGO_DETALLE_ORDEN_COMPRA
                               AND CM.CODIGO_MAYOR = 3
                               AND CM.CODIGO_MAYOR = CA.CODIGO_MAYOR
                               AND CA.CODIGO_PROVEEDOR = B.CODIGO_PROVEEDOR
                               AND (
                                    (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('COMPORDCOM', 'ODC')
                                     AND A.FECHA_ORDEN_COMPRA >= p_FECHA_DESDE
                                     AND A.FECHA_ORDEN_COMPRA < p_FECHA_HASTA + 1)
                                    OR
                                    (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ANCOMPORDC', 'ODC')
                                     AND A.FECHA_UPD >= p_FECHA_DESDE
                                     AND A.FECHA_UPD < p_FECHA_HASTA + 1)
                               )
                             GROUP BY A.CODIGO_ORDEN_COMPRA,
                                      A.NUMERO_ORDEN_COMPRA,
                                      A.FECHA_ORDEN_COMPRA,
                                      A.CODIGO_PROVEEDOR,
                                      B.NOMBRE_PROVEEDOR,
                                      NULL,
                                      NVL(CA.CODIGO_MAYOR, 3),
                                      NVL(CA.CODIGO_AUXILIAR, 1094),
                                      NVL(CA.SEGMENTO1, '001'),
                                      NVL(CA.SEGMENTO2, '000'),
                                      NVL(CA.DENOMINACION, '999'),
                                      NVL(CM.NUMERO_MAYOR, '103'),
                                      NULL,
                                      A.MOTIVO,
                                      'OC'
                      ) x
                     WHERE NVL(x.MONTO_COMPROMISO, 0) <> 0
                     ORDER BY x.TIPO_COMPROMISO, x.CODIGO_COMPROMISO, x.MONTO_COMPROMISO DESC
              ) ordered_rows;

        p_Message := 'Success';
        RETURN;
    END IF;

    IF p_ORIGEN_ID IN (
        CNT.CNT_DESCRIPTIVA_ID('COMPCONTOB', 'ODC'),
        CNT.CNT_DESCRIPTIVA_ID('ANCOCONTOB', 'ODC')
    ) THEN
        IF v_codigo_presupuesto IS NULL THEN
            open_empty_result;
            p_Message := 'No existe presupuesto vigente para el rango indicado.';
            RETURN;
        END IF;

        OPEN p_ResultSet FOR
            SELECT ROWNUM SECUENCIA,
                   ordered_rows.CODIGO_MAYOR,
                   ordered_rows.MAYOR,
                   ordered_rows.CODIGO_AUXILIAR,
                   ordered_rows.AUXILIAR,
                   ordered_rows.REFERENCIA1,
                   ordered_rows.REFERENCIA2,
                   ordered_rows.REFERENCIA3,
                   ordered_rows.DESCRIPCION,
                   ordered_rows.MONTO
              FROM (
                    SELECT x.CODIGO_MAYOR,
                           x.NUMERO_MAYOR MAYOR,
                           x.CODIGO_AUXILIAR,
                           x.SEGMENTO1 || '-' || x.SEGMENTO2 || ' ' || x.DENOMINACION AUXILIAR,
                           SUBSTR(x.NUMERO_COMPROMISO, 1, 20) REFERENCIA1,
                           TO_CHAR(x.FECHA_COMPROMISO, 'DD/MM/RRRR') REFERENCIA2,
                           NULL REFERENCIA3,
                           SUBSTR('(' || x.TIPO_COMPROMISO || ') ' || x.MOTIVO, 1, 200) DESCRIPCION,
                           CASE
                               WHEN p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('COMPCONTOB', 'ODC') AND x.CODIGO_MAYOR = 9 THEN -ABS(NVL(x.MONTO_COMPROMISO, 0))
                               WHEN p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('COMPCONTOB', 'ODC') THEN ABS(NVL(x.MONTO_COMPROMISO, 0))
                               WHEN x.CODIGO_MAYOR = 9 THEN ABS(NVL(x.MONTO_COMPROMISO, 0))
                               ELSE -ABS(NVL(x.MONTO_COMPROMISO, 0))
                           END MONTO
                      FROM (
                            SELECT A.CODIGO_CONTRATO CODIGO_COMPROMISO,
                                   A.NUMERO_CONTRATO NUMERO_COMPROMISO,
                                   A.FECHA_CONTRATO FECHA_COMPROMISO,
                                   A.CODIGO_PROVEEDOR,
                                   B.NOMBRE_PROVEEDOR,
                                   NULL FINANCIADO_POR,
                                   A.MONTO_CONTRATO MONTO_COMPROMISO,
                                   NVL(CA.CODIGO_MAYOR, 9) CODIGO_MAYOR,
                                   NVL(CA.CODIGO_AUXILIAR, 1094) CODIGO_AUXILIAR,
                                   NVL(CA.SEGMENTO1, '001') SEGMENTO1,
                                   NVL(CA.SEGMENTO2, '000') SEGMENTO2,
                                   NVL(CA.DENOMINACION, '999') DENOMINACION,
                                   NVL(CM.NUMERO_MAYOR, '300') NUMERO_MAYOR,
                                   NULL STATUS,
                                   SUBSTR(A.OBRA, 1, 200) MOTIVO,
                                   'CO' TIPO_COMPROMISO
                              FROM ADM.ADM_CONTRATOS A,
                                   ADM.ADM_PROVEEDORES B,
                                   CNT.CNT_MAYORES CM,
                                   CNT.CNT_AUXILIARES CA
                             WHERE A.CODIGO_PRESUPUESTO = v_codigo_presupuesto
                               AND B.CODIGO_PROVEEDOR = A.CODIGO_PROVEEDOR
                               AND CM.CODIGO_MAYOR = 9
                               AND CM.CODIGO_MAYOR = CA.CODIGO_MAYOR
                               AND CA.CODIGO_PROVEEDOR = B.CODIGO_PROVEEDOR
                               AND A.MONTO_CONTRATO <> 0
                               AND (
                                    (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('COMPCONTOB', 'ODC')
                                     AND A.STATUS <> 'AN'
                                     AND TRUNC(A.FECHA_INS) >= p_FECHA_DESDE
                                     AND TRUNC(A.FECHA_INS) <= p_FECHA_HASTA)
                                    OR
                                    (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ANCOCONTOB', 'ODC')
                                     AND A.STATUS = 'AN'
                                     AND TRUNC(A.FECHA_INS) >= p_FECHA_DESDE
                                     AND TRUNC(A.FECHA_INS) <= p_FECHA_HASTA)
                               )
                               AND EXISTS (
                                    SELECT 1
                                      FROM ADM.ADM_PUC_CONTRATO APC
                                     WHERE APC.CODIGO_CONTRATO = A.CODIGO_CONTRATO
                               )
                            UNION ALL
                            SELECT A.CODIGO_CONTRATO CODIGO_COMPROMISO,
                                   A.NUMERO_CONTRATO NUMERO_COMPROMISO,
                                   A.FECHA_CONTRATO FECHA_COMPROMISO,
                                   A.CODIGO_PROVEEDOR,
                                   B.NOMBRE_PROVEEDOR,
                                   NULL FINANCIADO_POR,
                                   A.MONTO_CONTRATO MONTO_COMPROMISO,
                                   NVL(CA.CODIGO_MAYOR, 3) CODIGO_MAYOR,
                                   NVL(CA.CODIGO_AUXILIAR, 1094) CODIGO_AUXILIAR,
                                   NVL(CA.SEGMENTO1, '001') SEGMENTO1,
                                   NVL(CA.SEGMENTO2, '000') SEGMENTO2,
                                   NVL(CA.DENOMINACION, '999') DENOMINACION,
                                   NVL(CM.NUMERO_MAYOR, '103') NUMERO_MAYOR,
                                   NULL STATUS,
                                   SUBSTR(A.OBRA, 1, 200) MOTIVO,
                                   'CO' TIPO_COMPROMISO
                              FROM ADM.ADM_CONTRATOS A,
                                   ADM.ADM_PROVEEDORES B,
                                   CNT.CNT_MAYORES CM,
                                   CNT.CNT_AUXILIARES CA
                             WHERE A.CODIGO_PRESUPUESTO = v_codigo_presupuesto
                               AND B.CODIGO_PROVEEDOR = A.CODIGO_PROVEEDOR
                               AND CM.CODIGO_MAYOR = 3
                               AND CM.CODIGO_MAYOR = CA.CODIGO_MAYOR
                               AND CA.CODIGO_PROVEEDOR = B.CODIGO_PROVEEDOR
                               AND A.MONTO_CONTRATO <> 0
                               AND (
                                    (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('COMPCONTOB', 'ODC')
                                     AND A.STATUS <> 'AN'
                                     AND TRUNC(A.FECHA_INS) >= p_FECHA_DESDE
                                     AND TRUNC(A.FECHA_INS) <= p_FECHA_HASTA)
                                    OR
                                    (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ANCOCONTOB', 'ODC')
                                     AND A.STATUS = 'AN'
                                     AND A.FECHA_UPD >= p_FECHA_DESDE
                                     AND A.FECHA_UPD < p_FECHA_HASTA + 1
                                     AND TRUNC(A.FECHA_INS) >= p_FECHA_DESDE
                                     AND TRUNC(A.FECHA_INS) <= p_FECHA_HASTA)
                               )
                               AND EXISTS (
                                    SELECT 1
                                      FROM ADM.ADM_PUC_CONTRATO APC
                                     WHERE APC.CODIGO_CONTRATO = A.CODIGO_CONTRATO
                               )
                      ) x
                     WHERE NVL(x.MONTO_COMPROMISO, 0) <> 0
                     ORDER BY x.TIPO_COMPROMISO, x.CODIGO_COMPROMISO, x.MONTO_COMPROMISO DESC
              ) ordered_rows;

        p_Message := 'Success';
        RETURN;
    END IF;

    open_empty_result;
    p_Message := 'El origen seleccionado no tiene reglas de generacion automatica configuradas.';
EXCEPTION
    WHEN NO_DATA_FOUND THEN
        open_empty_result;
        p_Message := 'Periodo, tipo u origen no valido para el proceso automatico.';
    WHEN OTHERS THEN
        open_empty_result;
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_AUT_CONF (
    p_CODIGO_PERIODO      IN NUMBER,
    p_TIPO_COMPROBANTE_ID IN NUMBER,
    p_ORIGEN_ID           IN NUMBER,
    p_FECHA_DESDE         IN DATE,
    p_FECHA_HASTA         IN DATE,
    p_USUARIO_ID          IN NUMBER,
    p_CODIGO_EMPRESA      IN NUMBER,
    p_FECHA_COMPROBANTE   IN DATE,
    p_OBSERVACION         IN VARCHAR2,
    p_CODIGO_COMPROBANTE  OUT NUMBER,
    p_NUMERO_COMPROBANTE  OUT VARCHAR2,
    p_CANTIDAD_LINEAS     OUT NUMBER,
    p_TOTAL_DEBE          OUT NUMBER,
    p_TOTAL_HABER         OUT NUMBER,
    p_Message             OUT VARCHAR2
) AS
    v_dummy              NUMBER;
    v_codigo_presupuesto NUMBER;
    v_codigo_detalle     NUMBER;
    v_det_message        VARCHAR2(4000);
    v_cmp_message        VARCHAR2(4000);

    PROCEDURE cleanup_comprobante IS
    BEGIN
        IF NVL(p_CODIGO_COMPROBANTE, 0) > 0 THEN
            DELETE FROM CNT.CNT_DETALLE_COMPROBANTE
             WHERE CODIGO_COMPROBANTE = p_CODIGO_COMPROBANTE
               AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

            DELETE FROM CNT.CNT_COMPROBANTES
             WHERE CODIGO_COMPROBANTE = p_CODIGO_COMPROBANTE
               AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;
        END IF;
    END;
BEGIN
    p_CODIGO_COMPROBANTE := 0;
    p_NUMERO_COMPROBANTE := NULL;
    p_CANTIDAD_LINEAS := 0;
    p_TOTAL_DEBE := 0;
    p_TOTAL_HABER := 0;

    IF p_FECHA_DESDE IS NULL OR p_FECHA_HASTA IS NULL THEN
        p_Message := 'El rango de fechas es requerido.';
        RETURN;
    END IF;

    IF p_FECHA_DESDE > p_FECHA_HASTA THEN
        p_Message := 'La fecha desde no puede ser mayor que la fecha hasta.';
        RETURN;
    END IF;

    IF p_FECHA_COMPROBANTE IS NULL THEN
        p_Message := 'La fecha del comprobante es requerida.';
        RETURN;
    END IF;

    SELECT 1
      INTO v_dummy
      FROM CNT.CNT_PERIODOS
     WHERE CODIGO_PERIODO = p_CODIGO_PERIODO
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA
       AND p_FECHA_COMPROBANTE BETWEEN FECHA_DESDE AND FECHA_HASTA
       AND FECHA_CIERRE IS NULL;

    SELECT 1
      INTO v_dummy
      FROM CNT.CNT_DESCRIPTIVAS
     WHERE DESCRIPCION_ID = p_TIPO_COMPROBANTE_ID
       AND TITULO_ID = 5
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    SELECT 1
      INTO v_dummy
      FROM CNT.CNT_DESCRIPTIVAS
     WHERE DESCRIPCION_ID = p_ORIGEN_ID
       AND TITULO_ID = 1
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    BEGIN
        SELECT CODIGO_PRESUPUESTO
          INTO v_codigo_presupuesto
          FROM PRE.PRE_PRESUPUESTOS
         WHERE p_FECHA_DESDE BETWEEN FECHA_DESDE AND FECHA_HASTA;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            v_codigo_presupuesto := NULL;
    END;

    IF p_ORIGEN_ID IN (
        CNT.CNT_DESCRIPTIVA_ID('ODPAUT', 'ODC'),
        CNT.CNT_DESCRIPTIVA_ID('ANODPAUT', 'ODC')
    ) THEN
        IF v_codigo_presupuesto IS NULL THEN
            p_Message := 'No existe presupuesto vigente para el rango indicado.';
            RETURN;
        END IF;

        CNT.SP_CNT_AUT_BUILD_OP(
            p_ORIGEN_ID,
            p_FECHA_DESDE,
            p_FECHA_HASTA,
            v_codigo_presupuesto,
            p_CODIGO_EMPRESA,
            p_Message
        );

        IF NVL(p_Message, 'Error') <> 'Success' THEN
            RETURN;
        END IF;

        CNT.SP_CNT_CMP_INS(
            p_CODIGO_PERIODO,
            p_TIPO_COMPROBANTE_ID,
            p_FECHA_COMPROBANTE,
            p_ORIGEN_ID,
            p_OBSERVACION,
            p_USUARIO_ID,
            p_CODIGO_EMPRESA,
            p_CODIGO_COMPROBANTE,
            v_cmp_message
        );

        IF NVL(v_cmp_message, 'Error') <> 'Success' OR NVL(p_CODIGO_COMPROBANTE, 0) = 0 THEN
            p_Message := v_cmp_message;
            RETURN;
        END IF;

        SELECT NUMERO_COMPROBANTE
          INTO p_NUMERO_COMPROBANTE
          FROM CNT.CNT_COMPROBANTES
         WHERE CODIGO_COMPROBANTE = p_CODIGO_COMPROBANTE
           AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

        FOR line IN (
            SELECT CODIGO_MAYOR,
                   CODIGO_AUXILIAR,
                   REFERENCIA1,
                   REFERENCIA2,
                   REFERENCIA3,
                   DESCRIPCION,
                   MONTO
              FROM CNT.CNT_AUT_LINE_TMP
             ORDER BY SECUENCIA
        ) LOOP
            CNT.SP_CNT_DET_INS(
                p_CODIGO_COMPROBANTE,
                line.CODIGO_MAYOR,
                line.CODIGO_AUXILIAR,
                line.REFERENCIA1,
                line.REFERENCIA2,
                line.REFERENCIA3,
                line.DESCRIPCION,
                line.MONTO,
                p_USUARIO_ID,
                p_CODIGO_EMPRESA,
                v_codigo_detalle,
                v_det_message
            );

            IF NVL(v_det_message, 'Error') <> 'Success' THEN
                cleanup_comprobante;
                p_Message := v_det_message;
                RETURN;
            END IF;

            p_CANTIDAD_LINEAS := p_CANTIDAD_LINEAS + 1;
            p_TOTAL_DEBE := p_TOTAL_DEBE + CASE WHEN line.MONTO < 0 THEN ABS(line.MONTO) ELSE 0 END;
            p_TOTAL_HABER := p_TOTAL_HABER + CASE WHEN line.MONTO > 0 THEN line.MONTO ELSE 0 END;
        END LOOP;

        IF p_CANTIDAD_LINEAS = 0 THEN
            cleanup_comprobante;
            p_CODIGO_COMPROBANTE := 0;
            p_NUMERO_COMPROBANTE := NULL;
            p_Message := 'El proceso automatico no genero lineas para el rango indicado.';
            RETURN;
        END IF;

        IF ROUND(NVL(p_TOTAL_DEBE, 0) - NVL(p_TOTAL_HABER, 0), 2) <> 0 THEN
            cleanup_comprobante;
            p_CODIGO_COMPROBANTE := 0;
            p_NUMERO_COMPROBANTE := NULL;
            p_Message := 'El comprobante automatico no esta balanceado.';
            RETURN;
        END IF;

        UPDATE CNT.CNT_COMPROBANTES
           SET EXTRA1 = 'AUTOMATICO',
               USUARIO_UPD = p_USUARIO_ID,
               FECHA_UPD = SYSDATE
         WHERE CODIGO_COMPROBANTE = p_CODIGO_COMPROBANTE
           AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

        p_Message := 'Success';
        RETURN;
    END IF;

    IF p_ORIGEN_ID IN (
        CNT.CNT_DESCRIPTIVA_ID('COMPORDCOM', 'ODC'),
        CNT.CNT_DESCRIPTIVA_ID('ANCOMPORDC', 'ODC')
    ) THEN
        IF v_codigo_presupuesto IS NULL THEN
            p_Message := 'No existe presupuesto vigente para el rango indicado.';
            RETURN;
        END IF;

        CNT.SP_CNT_CMP_INS(
            p_CODIGO_PERIODO,
            p_TIPO_COMPROBANTE_ID,
            p_FECHA_COMPROBANTE,
            p_ORIGEN_ID,
            p_OBSERVACION,
            p_USUARIO_ID,
            p_CODIGO_EMPRESA,
            p_CODIGO_COMPROBANTE,
            v_cmp_message
        );

        IF NVL(v_cmp_message, 'Error') <> 'Success' OR NVL(p_CODIGO_COMPROBANTE, 0) = 0 THEN
            p_Message := v_cmp_message;
            RETURN;
        END IF;

        SELECT NUMERO_COMPROBANTE
          INTO p_NUMERO_COMPROBANTE
          FROM CNT.CNT_COMPROBANTES
         WHERE CODIGO_COMPROBANTE = p_CODIGO_COMPROBANTE
           AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

        FOR line IN (
            SELECT ordered_rows.CODIGO_MAYOR,
                   ordered_rows.CODIGO_AUXILIAR,
                   ordered_rows.REFERENCIA1,
                   ordered_rows.REFERENCIA2,
                   ordered_rows.REFERENCIA3,
                   ordered_rows.DESCRIPCION,
                   ordered_rows.MONTO
              FROM (
                    SELECT x.CODIGO_MAYOR,
                           x.CODIGO_AUXILIAR,
                           SUBSTR(x.NUMERO_COMPROMISO, 1, 20) REFERENCIA1,
                           TO_CHAR(x.FECHA_COMPROMISO, 'DD/MM/RRRR') REFERENCIA2,
                           NULL REFERENCIA3,
                           SUBSTR('(' || x.TIPO_COMPROMISO || ') ' || x.MOTIVO, 1, 200) DESCRIPCION,
                           CASE
                               WHEN p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('COMPORDCOM', 'ODC') AND x.CODIGO_MAYOR = 9 THEN -ABS(NVL(x.MONTO_COMPROMISO, 0))
                               WHEN p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('COMPORDCOM', 'ODC') THEN ABS(NVL(x.MONTO_COMPROMISO, 0))
                               WHEN x.CODIGO_MAYOR = 9 THEN ABS(NVL(x.MONTO_COMPROMISO, 0))
                               ELSE -ABS(NVL(x.MONTO_COMPROMISO, 0))
                           END MONTO,
                           x.TIPO_COMPROMISO,
                           x.CODIGO_COMPROMISO,
                           x.MONTO_COMPROMISO
                      FROM (
                            SELECT A.CODIGO_ORDEN_COMPRA CODIGO_COMPROMISO,
                                   A.NUMERO_ORDEN_COMPRA NUMERO_COMPROMISO,
                                   A.FECHA_ORDEN_COMPRA FECHA_COMPROMISO,
                                   A.CODIGO_PROVEEDOR,
                                   B.NOMBRE_PROVEEDOR,
                                   NULL FINANCIADO_POR,
                                   NVL(SUM(D.MONTO), 0) MONTO_COMPROMISO,
                                   NVL(CA.CODIGO_MAYOR, 9) CODIGO_MAYOR,
                                   NVL(CA.CODIGO_AUXILIAR, 1094) CODIGO_AUXILIAR,
                                   NVL(CA.SEGMENTO1, '001') SEGMENTO1,
                                   NVL(CA.SEGMENTO2, '000') SEGMENTO2,
                                   NVL(CA.DENOMINACION, '999') DENOMINACION,
                                   NVL(CM.NUMERO_MAYOR, '300') NUMERO_MAYOR,
                                   NULL STATUS,
                                   A.MOTIVO,
                                   'OC' TIPO_COMPROMISO
                              FROM ADM.ADM_ORDEN_COMPRA A,
                                   ADM.ADM_PROVEEDORES B,
                                   ADM.ADM_DETALLE_ORDEN_COMPRA C,
                                   ADM.ADM_PUC_ORDEN_COMPRA D,
                                   CNT.CNT_MAYORES CM,
                                   CNT.CNT_AUXILIARES CA
                             WHERE A.CODIGO_PRESUPUESTO = v_codigo_presupuesto
                               AND B.CODIGO_PROVEEDOR = A.CODIGO_PROVEEDOR
                               AND C.CODIGO_ORDEN_COMPRA(+) = A.CODIGO_ORDEN_COMPRA
                               AND D.CODIGO_DETALLE_ORDEN_COMPRA(+) = C.CODIGO_DETALLE_ORDEN_COMPRA
                               AND CM.CODIGO_MAYOR = 9
                               AND CM.CODIGO_MAYOR = CA.CODIGO_MAYOR
                               AND CA.CODIGO_PROVEEDOR = B.CODIGO_PROVEEDOR
                               AND (
                                    (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('COMPORDCOM', 'ODC')
                                     AND A.FECHA_ORDEN_COMPRA >= p_FECHA_DESDE
                                     AND A.FECHA_ORDEN_COMPRA < p_FECHA_HASTA + 1)
                                    OR
                                    (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ANCOMPORDC', 'ODC')
                                     AND A.FECHA_UPD >= p_FECHA_DESDE
                                     AND A.FECHA_UPD < p_FECHA_HASTA + 1)
                               )
                             GROUP BY A.CODIGO_ORDEN_COMPRA,
                                      A.NUMERO_ORDEN_COMPRA,
                                      A.FECHA_ORDEN_COMPRA,
                                      A.CODIGO_PROVEEDOR,
                                      B.NOMBRE_PROVEEDOR,
                                      NULL,
                                      NVL(CA.CODIGO_MAYOR, 9),
                                      NVL(CA.CODIGO_AUXILIAR, 1094),
                                      NVL(CA.SEGMENTO1, '001'),
                                      NVL(CA.SEGMENTO2, '000'),
                                      NVL(CA.DENOMINACION, '999'),
                                      NVL(CM.NUMERO_MAYOR, '300'),
                                      NULL,
                                      A.MOTIVO,
                                      'OC'
                            UNION ALL
                            SELECT A.CODIGO_ORDEN_COMPRA CODIGO_COMPROMISO,
                                   A.NUMERO_ORDEN_COMPRA NUMERO_COMPROMISO,
                                   A.FECHA_ORDEN_COMPRA FECHA_COMPROMISO,
                                   A.CODIGO_PROVEEDOR,
                                   B.NOMBRE_PROVEEDOR,
                                   NULL FINANCIADO_POR,
                                   NVL(SUM(D.MONTO - D.MONTO_ANULADO), 0) MONTO_COMPROMISO,
                                   NVL(CA.CODIGO_MAYOR, 3) CODIGO_MAYOR,
                                   NVL(CA.CODIGO_AUXILIAR, 1094) CODIGO_AUXILIAR,
                                   NVL(CA.SEGMENTO1, '001') SEGMENTO1,
                                   NVL(CA.SEGMENTO2, '000') SEGMENTO2,
                                   NVL(CA.DENOMINACION, '999') DENOMINACION,
                                   NVL(CM.NUMERO_MAYOR, '103') NUMERO_MAYOR,
                                   NULL STATUS,
                                   A.MOTIVO,
                                   'OC' TIPO_COMPROMISO
                              FROM ADM.ADM_ORDEN_COMPRA A,
                                   ADM.ADM_PROVEEDORES B,
                                   ADM.ADM_DETALLE_ORDEN_COMPRA C,
                                   ADM.ADM_PUC_ORDEN_COMPRA D,
                                   CNT.CNT_MAYORES CM,
                                   CNT.CNT_AUXILIARES CA
                             WHERE A.CODIGO_PRESUPUESTO = v_codigo_presupuesto
                               AND B.CODIGO_PROVEEDOR = A.CODIGO_PROVEEDOR
                               AND C.CODIGO_ORDEN_COMPRA(+) = A.CODIGO_ORDEN_COMPRA
                               AND D.CODIGO_DETALLE_ORDEN_COMPRA(+) = C.CODIGO_DETALLE_ORDEN_COMPRA
                               AND CM.CODIGO_MAYOR = 3
                               AND CM.CODIGO_MAYOR = CA.CODIGO_MAYOR
                               AND CA.CODIGO_PROVEEDOR = B.CODIGO_PROVEEDOR
                               AND (
                                    (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('COMPORDCOM', 'ODC')
                                     AND A.FECHA_ORDEN_COMPRA >= p_FECHA_DESDE
                                     AND A.FECHA_ORDEN_COMPRA < p_FECHA_HASTA + 1)
                                    OR
                                    (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ANCOMPORDC', 'ODC')
                                     AND A.FECHA_UPD >= p_FECHA_DESDE
                                     AND A.FECHA_UPD < p_FECHA_HASTA + 1)
                               )
                             GROUP BY A.CODIGO_ORDEN_COMPRA,
                                      A.NUMERO_ORDEN_COMPRA,
                                      A.FECHA_ORDEN_COMPRA,
                                      A.CODIGO_PROVEEDOR,
                                      B.NOMBRE_PROVEEDOR,
                                      NULL,
                                      NVL(CA.CODIGO_MAYOR, 3),
                                      NVL(CA.CODIGO_AUXILIAR, 1094),
                                      NVL(CA.SEGMENTO1, '001'),
                                      NVL(CA.SEGMENTO2, '000'),
                                      NVL(CA.DENOMINACION, '999'),
                                      NVL(CM.NUMERO_MAYOR, '103'),
                                      NULL,
                                      A.MOTIVO,
                                      'OC'
                      ) x
                     WHERE NVL(x.MONTO_COMPROMISO, 0) <> 0
                     ORDER BY x.TIPO_COMPROMISO, x.CODIGO_COMPROMISO, x.MONTO_COMPROMISO DESC
              ) ordered_rows
        ) LOOP
            CNT.SP_CNT_DET_INS(
                p_CODIGO_COMPROBANTE,
                line.CODIGO_MAYOR,
                line.CODIGO_AUXILIAR,
                line.REFERENCIA1,
                line.REFERENCIA2,
                line.REFERENCIA3,
                line.DESCRIPCION,
                line.MONTO,
                p_USUARIO_ID,
                p_CODIGO_EMPRESA,
                v_codigo_detalle,
                v_det_message
            );

            IF NVL(v_det_message, 'Error') <> 'Success' THEN
                cleanup_comprobante;
                p_Message := v_det_message;
                RETURN;
            END IF;

            p_CANTIDAD_LINEAS := p_CANTIDAD_LINEAS + 1;
            p_TOTAL_DEBE := p_TOTAL_DEBE + CASE WHEN line.MONTO < 0 THEN ABS(line.MONTO) ELSE 0 END;
            p_TOTAL_HABER := p_TOTAL_HABER + CASE WHEN line.MONTO > 0 THEN line.MONTO ELSE 0 END;
        END LOOP;

        IF p_CANTIDAD_LINEAS = 0 THEN
            cleanup_comprobante;
            p_CODIGO_COMPROBANTE := 0;
            p_NUMERO_COMPROBANTE := NULL;
            p_Message := 'El proceso automatico no genero lineas para el rango indicado.';
            RETURN;
        END IF;

        IF ROUND(NVL(p_TOTAL_DEBE, 0) - NVL(p_TOTAL_HABER, 0), 2) <> 0 THEN
            cleanup_comprobante;
            p_CODIGO_COMPROBANTE := 0;
            p_NUMERO_COMPROBANTE := NULL;
            p_Message := 'El comprobante automatico no esta balanceado.';
            RETURN;
        END IF;

        UPDATE CNT.CNT_COMPROBANTES
           SET EXTRA1 = 'AUTOMATICO',
               USUARIO_UPD = p_USUARIO_ID,
               FECHA_UPD = SYSDATE
         WHERE CODIGO_COMPROBANTE = p_CODIGO_COMPROBANTE
           AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

        p_Message := 'Success';
        RETURN;
    END IF;

    IF p_ORIGEN_ID IN (
        CNT.CNT_DESCRIPTIVA_ID('COMPCONTOB', 'ODC'),
        CNT.CNT_DESCRIPTIVA_ID('ANCOCONTOB', 'ODC')
    ) THEN
        IF v_codigo_presupuesto IS NULL THEN
            p_Message := 'No existe presupuesto vigente para el rango indicado.';
            RETURN;
        END IF;

        CNT.SP_CNT_CMP_INS(
            p_CODIGO_PERIODO,
            p_TIPO_COMPROBANTE_ID,
            p_FECHA_COMPROBANTE,
            p_ORIGEN_ID,
            p_OBSERVACION,
            p_USUARIO_ID,
            p_CODIGO_EMPRESA,
            p_CODIGO_COMPROBANTE,
            v_cmp_message
        );

        IF NVL(v_cmp_message, 'Error') <> 'Success' OR NVL(p_CODIGO_COMPROBANTE, 0) = 0 THEN
            p_Message := v_cmp_message;
            RETURN;
        END IF;

        SELECT NUMERO_COMPROBANTE
          INTO p_NUMERO_COMPROBANTE
          FROM CNT.CNT_COMPROBANTES
         WHERE CODIGO_COMPROBANTE = p_CODIGO_COMPROBANTE
           AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

        FOR line IN (
            SELECT ordered_rows.CODIGO_MAYOR,
                   ordered_rows.CODIGO_AUXILIAR,
                   ordered_rows.REFERENCIA1,
                   ordered_rows.REFERENCIA2,
                   ordered_rows.REFERENCIA3,
                   ordered_rows.DESCRIPCION,
                   ordered_rows.MONTO
              FROM (
                    SELECT x.CODIGO_MAYOR,
                           x.CODIGO_AUXILIAR,
                           SUBSTR(x.NUMERO_COMPROMISO, 1, 20) REFERENCIA1,
                           TO_CHAR(x.FECHA_COMPROMISO, 'DD/MM/RRRR') REFERENCIA2,
                           NULL REFERENCIA3,
                           SUBSTR('(' || x.TIPO_COMPROMISO || ') ' || x.MOTIVO, 1, 200) DESCRIPCION,
                           CASE
                               WHEN p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('COMPCONTOB', 'ODC') AND x.CODIGO_MAYOR = 9 THEN -ABS(NVL(x.MONTO_COMPROMISO, 0))
                               WHEN p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('COMPCONTOB', 'ODC') THEN ABS(NVL(x.MONTO_COMPROMISO, 0))
                               WHEN x.CODIGO_MAYOR = 9 THEN ABS(NVL(x.MONTO_COMPROMISO, 0))
                               ELSE -ABS(NVL(x.MONTO_COMPROMISO, 0))
                           END MONTO,
                           x.TIPO_COMPROMISO,
                           x.CODIGO_COMPROMISO,
                           x.MONTO_COMPROMISO
                      FROM (
                            SELECT A.CODIGO_CONTRATO CODIGO_COMPROMISO,
                                   A.NUMERO_CONTRATO NUMERO_COMPROMISO,
                                   A.FECHA_CONTRATO FECHA_COMPROMISO,
                                   A.CODIGO_PROVEEDOR,
                                   B.NOMBRE_PROVEEDOR,
                                   NULL FINANCIADO_POR,
                                   A.MONTO_CONTRATO MONTO_COMPROMISO,
                                   NVL(CA.CODIGO_MAYOR, 9) CODIGO_MAYOR,
                                   NVL(CA.CODIGO_AUXILIAR, 1094) CODIGO_AUXILIAR,
                                   NVL(CA.SEGMENTO1, '001') SEGMENTO1,
                                   NVL(CA.SEGMENTO2, '000') SEGMENTO2,
                                   NVL(CA.DENOMINACION, '999') DENOMINACION,
                                   NVL(CM.NUMERO_MAYOR, '300') NUMERO_MAYOR,
                                   NULL STATUS,
                                   SUBSTR(A.OBRA, 1, 200) MOTIVO,
                                   'CO' TIPO_COMPROMISO
                              FROM ADM.ADM_CONTRATOS A,
                                   ADM.ADM_PROVEEDORES B,
                                   CNT.CNT_MAYORES CM,
                                   CNT.CNT_AUXILIARES CA
                             WHERE A.CODIGO_PRESUPUESTO = v_codigo_presupuesto
                               AND B.CODIGO_PROVEEDOR = A.CODIGO_PROVEEDOR
                               AND CM.CODIGO_MAYOR = 9
                               AND CM.CODIGO_MAYOR = CA.CODIGO_MAYOR
                               AND CA.CODIGO_PROVEEDOR = B.CODIGO_PROVEEDOR
                               AND A.MONTO_CONTRATO <> 0
                               AND (
                                    (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('COMPCONTOB', 'ODC')
                                     AND A.STATUS <> 'AN'
                                     AND TRUNC(A.FECHA_INS) >= p_FECHA_DESDE
                                     AND TRUNC(A.FECHA_INS) <= p_FECHA_HASTA)
                                    OR
                                    (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ANCOCONTOB', 'ODC')
                                     AND A.STATUS = 'AN'
                                     AND TRUNC(A.FECHA_INS) >= p_FECHA_DESDE
                                     AND TRUNC(A.FECHA_INS) <= p_FECHA_HASTA)
                               )
                               AND EXISTS (
                                    SELECT 1
                                      FROM ADM.ADM_PUC_CONTRATO APC
                                     WHERE APC.CODIGO_CONTRATO = A.CODIGO_CONTRATO
                               )
                            UNION ALL
                            SELECT A.CODIGO_CONTRATO CODIGO_COMPROMISO,
                                   A.NUMERO_CONTRATO NUMERO_COMPROMISO,
                                   A.FECHA_CONTRATO FECHA_COMPROMISO,
                                   A.CODIGO_PROVEEDOR,
                                   B.NOMBRE_PROVEEDOR,
                                   NULL FINANCIADO_POR,
                                   A.MONTO_CONTRATO MONTO_COMPROMISO,
                                   NVL(CA.CODIGO_MAYOR, 3) CODIGO_MAYOR,
                                   NVL(CA.CODIGO_AUXILIAR, 1094) CODIGO_AUXILIAR,
                                   NVL(CA.SEGMENTO1, '001') SEGMENTO1,
                                   NVL(CA.SEGMENTO2, '000') SEGMENTO2,
                                   NVL(CA.DENOMINACION, '999') DENOMINACION,
                                   NVL(CM.NUMERO_MAYOR, '103') NUMERO_MAYOR,
                                   NULL STATUS,
                                   SUBSTR(A.OBRA, 1, 200) MOTIVO,
                                   'CO' TIPO_COMPROMISO
                              FROM ADM.ADM_CONTRATOS A,
                                   ADM.ADM_PROVEEDORES B,
                                   CNT.CNT_MAYORES CM,
                                   CNT.CNT_AUXILIARES CA
                             WHERE A.CODIGO_PRESUPUESTO = v_codigo_presupuesto
                               AND B.CODIGO_PROVEEDOR = A.CODIGO_PROVEEDOR
                               AND CM.CODIGO_MAYOR = 3
                               AND CM.CODIGO_MAYOR = CA.CODIGO_MAYOR
                               AND CA.CODIGO_PROVEEDOR = B.CODIGO_PROVEEDOR
                               AND A.MONTO_CONTRATO <> 0
                               AND (
                                    (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('COMPCONTOB', 'ODC')
                                     AND A.STATUS <> 'AN'
                                     AND TRUNC(A.FECHA_INS) >= p_FECHA_DESDE
                                     AND TRUNC(A.FECHA_INS) <= p_FECHA_HASTA)
                                    OR
                                    (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ANCOCONTOB', 'ODC')
                                     AND A.STATUS = 'AN'
                                     AND A.FECHA_UPD >= p_FECHA_DESDE
                                     AND A.FECHA_UPD < p_FECHA_HASTA + 1
                                     AND TRUNC(A.FECHA_INS) >= p_FECHA_DESDE
                                     AND TRUNC(A.FECHA_INS) <= p_FECHA_HASTA)
                               )
                               AND EXISTS (
                                    SELECT 1
                                      FROM ADM.ADM_PUC_CONTRATO APC
                                     WHERE APC.CODIGO_CONTRATO = A.CODIGO_CONTRATO
                               )
                      ) x
                     WHERE NVL(x.MONTO_COMPROMISO, 0) <> 0
                     ORDER BY x.TIPO_COMPROMISO, x.CODIGO_COMPROMISO, x.MONTO_COMPROMISO DESC
              ) ordered_rows
        ) LOOP
            CNT.SP_CNT_DET_INS(
                p_CODIGO_COMPROBANTE,
                line.CODIGO_MAYOR,
                line.CODIGO_AUXILIAR,
                line.REFERENCIA1,
                line.REFERENCIA2,
                line.REFERENCIA3,
                line.DESCRIPCION,
                line.MONTO,
                p_USUARIO_ID,
                p_CODIGO_EMPRESA,
                v_codigo_detalle,
                v_det_message
            );

            IF NVL(v_det_message, 'Error') <> 'Success' THEN
                cleanup_comprobante;
                p_Message := v_det_message;
                RETURN;
            END IF;

            p_CANTIDAD_LINEAS := p_CANTIDAD_LINEAS + 1;
            p_TOTAL_DEBE := p_TOTAL_DEBE + CASE WHEN line.MONTO < 0 THEN ABS(line.MONTO) ELSE 0 END;
            p_TOTAL_HABER := p_TOTAL_HABER + CASE WHEN line.MONTO > 0 THEN line.MONTO ELSE 0 END;
        END LOOP;

        IF p_CANTIDAD_LINEAS = 0 THEN
            cleanup_comprobante;
            p_CODIGO_COMPROBANTE := 0;
            p_NUMERO_COMPROBANTE := NULL;
            p_Message := 'El proceso automatico no genero lineas para el rango indicado.';
            RETURN;
        END IF;

        IF ROUND(NVL(p_TOTAL_DEBE, 0) - NVL(p_TOTAL_HABER, 0), 2) <> 0 THEN
            cleanup_comprobante;
            p_CODIGO_COMPROBANTE := 0;
            p_NUMERO_COMPROBANTE := NULL;
            p_Message := 'El comprobante automatico no esta balanceado.';
            RETURN;
        END IF;

        UPDATE CNT.CNT_COMPROBANTES
           SET EXTRA1 = 'AUTOMATICO',
               USUARIO_UPD = p_USUARIO_ID,
               FECHA_UPD = SYSDATE
         WHERE CODIGO_COMPROBANTE = p_CODIGO_COMPROBANTE
           AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

        p_Message := 'Success';
        RETURN;
    END IF;

    p_Message := 'El origen seleccionado no tiene reglas de generacion automatica configuradas.';
EXCEPTION
    WHEN NO_DATA_FOUND THEN
        cleanup_comprobante;
        p_Message := 'No existe un periodo abierto para la fecha del comprobante.';
    WHEN OTHERS THEN
        cleanup_comprobante;
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_CMP_GET_ALL (
    p_PageSize            IN NUMBER,
    p_PageNumber          IN NUMBER,
    p_SearchText          IN VARCHAR2,
    p_CODIGO_PERIODO      IN NUMBER,
    p_TIPO_COMPROBANTE_ID IN NUMBER,
    p_ORIGEN_ID           IN NUMBER,
    p_FECHA_DESDE         IN DATE,
    p_FECHA_HASTA         IN DATE,
    p_CODIGO_EMPRESA      IN NUMBER,
    p_ResultSet           OUT SYS_REFCURSOR,
    p_Message             OUT VARCHAR2,
    p_TotalRecords        OUT NUMBER,
    p_TotalPages          OUT NUMBER
) AS
    v_page_size   NUMBER := NVL(NULLIF(p_PageSize, 0), 10);
    v_page_number NUMBER := NVL(NULLIF(p_PageNumber, 0), 1);
    v_start_row   NUMBER;
    v_end_row     NUMBER;
BEGIN
    v_start_row := ((v_page_number - 1) * v_page_size) + 1;
    v_end_row := v_page_number * v_page_size;

    SELECT COUNT(*)
      INTO p_TotalRecords
      FROM CNT.CNT_COMPROBANTES c
      LEFT JOIN CNT.CNT_PERIODOS p ON p.CODIGO_PERIODO = c.CODIGO_PERIODO
      LEFT JOIN CNT.CNT_DESCRIPTIVAS t ON t.DESCRIPCION_ID = c.TIPO_COMPROBANTE_ID
      LEFT JOIN CNT.CNT_DESCRIPTIVAS o ON o.DESCRIPCION_ID = c.ORIGEN_ID
     WHERE c.CODIGO_EMPRESA = p_CODIGO_EMPRESA
       AND (p_CODIGO_PERIODO IS NULL OR c.CODIGO_PERIODO = p_CODIGO_PERIODO)
       AND (p_TIPO_COMPROBANTE_ID IS NULL OR c.TIPO_COMPROBANTE_ID = p_TIPO_COMPROBANTE_ID)
       AND (p_ORIGEN_ID IS NULL OR c.ORIGEN_ID = p_ORIGEN_ID)
       AND (p_FECHA_DESDE IS NULL OR c.FECHA_COMPROBANTE >= p_FECHA_DESDE)
       AND (p_FECHA_HASTA IS NULL OR c.FECHA_COMPROBANTE < p_FECHA_HASTA + 1)
       AND (p_SearchText IS NULL
            OR UPPER(c.NUMERO_COMPROBANTE) LIKE '%' || UPPER(p_SearchText) || '%'
            OR UPPER(c.OBSERVACION) LIKE '%' || UPPER(p_SearchText) || '%'
            OR UPPER(t.DESCRIPCION) LIKE '%' || UPPER(p_SearchText) || '%'
            OR UPPER(o.DESCRIPCION) LIKE '%' || UPPER(p_SearchText) || '%');

    p_TotalPages := CASE WHEN p_TotalRecords = 0 THEN 0 ELSE CEIL(p_TotalRecords / v_page_size) END;

    OPEN p_ResultSet FOR
        SELECT *
          FROM (
                SELECT q.*, ROWNUM rn
                  FROM (
                        SELECT c.CODIGO_COMPROBANTE,
                               c.CODIGO_PERIODO,
                               p.NOMBRE_PERIODO AS PERIODO,
                               c.TIPO_COMPROBANTE_ID,
                               t.DESCRIPCION AS TIPO_COMPROBANTE,
                               c.NUMERO_COMPROBANTE,
                               c.FECHA_COMPROBANTE,
                               c.ORIGEN_ID,
                               o.DESCRIPCION AS ORIGEN,
                               c.OBSERVACION,
                               NVL(d.TOTAL_DEBE, 0) AS TOTAL_DEBE,
                               NVL(d.TOTAL_HABER, 0) AS TOTAL_HABER,
                               CASE WHEN NVL(c.EXTRA1, ' ') = 'AUTOMATICO' THEN 1 ELSE 0 END AS ES_AUTOMATICO,
                               c.CODIGO_EMPRESA
                          FROM CNT.CNT_COMPROBANTES c
                          LEFT JOIN CNT.CNT_PERIODOS p ON p.CODIGO_PERIODO = c.CODIGO_PERIODO
                          LEFT JOIN CNT.CNT_DESCRIPTIVAS t ON t.DESCRIPCION_ID = c.TIPO_COMPROBANTE_ID
                          LEFT JOIN CNT.CNT_DESCRIPTIVAS o ON o.DESCRIPCION_ID = c.ORIGEN_ID
                          LEFT JOIN (
                                SELECT CODIGO_COMPROBANTE,
                                       SUM(CASE WHEN MONTO < 0 THEN ABS(MONTO) ELSE 0 END) AS TOTAL_DEBE,
                                       SUM(CASE WHEN MONTO > 0 THEN MONTO ELSE 0 END) AS TOTAL_HABER
                                  FROM CNT.CNT_DETALLE_COMPROBANTE
                                 WHERE CODIGO_EMPRESA = p_CODIGO_EMPRESA
                                 GROUP BY CODIGO_COMPROBANTE
                          ) d ON d.CODIGO_COMPROBANTE = c.CODIGO_COMPROBANTE
                         WHERE c.CODIGO_EMPRESA = p_CODIGO_EMPRESA
                           AND (p_CODIGO_PERIODO IS NULL OR c.CODIGO_PERIODO = p_CODIGO_PERIODO)
                           AND (p_TIPO_COMPROBANTE_ID IS NULL OR c.TIPO_COMPROBANTE_ID = p_TIPO_COMPROBANTE_ID)
                           AND (p_ORIGEN_ID IS NULL OR c.ORIGEN_ID = p_ORIGEN_ID)
                           AND (p_FECHA_DESDE IS NULL OR c.FECHA_COMPROBANTE >= p_FECHA_DESDE)
                           AND (p_FECHA_HASTA IS NULL OR c.FECHA_COMPROBANTE < p_FECHA_HASTA + 1)
                           AND (p_SearchText IS NULL
                                OR UPPER(c.NUMERO_COMPROBANTE) LIKE '%' || UPPER(p_SearchText) || '%'
                                OR UPPER(c.OBSERVACION) LIKE '%' || UPPER(p_SearchText) || '%'
                                OR UPPER(t.DESCRIPCION) LIKE '%' || UPPER(p_SearchText) || '%'
                                OR UPPER(o.DESCRIPCION) LIKE '%' || UPPER(p_SearchText) || '%')
                         ORDER BY c.FECHA_COMPROBANTE DESC, c.NUMERO_COMPROBANTE DESC
                  ) q
                 WHERE ROWNUM <= v_end_row
          )
         WHERE rn >= v_start_row;

    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SQLERRM;
        p_TotalRecords := 0;
        p_TotalPages := 0;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_CMP_GET_ID (
    p_CODIGO_COMPROBANTE IN NUMBER,
    p_CODIGO_EMPRESA     IN NUMBER,
    p_ResultSet          OUT SYS_REFCURSOR,
    p_Message            OUT VARCHAR2
) AS
BEGIN
    OPEN p_ResultSet FOR
        SELECT c.CODIGO_COMPROBANTE,
               c.CODIGO_PERIODO,
               p.NOMBRE_PERIODO AS PERIODO,
               c.TIPO_COMPROBANTE_ID,
               t.DESCRIPCION AS TIPO_COMPROBANTE,
               c.NUMERO_COMPROBANTE,
               c.FECHA_COMPROBANTE,
               c.ORIGEN_ID,
               o.DESCRIPCION AS ORIGEN,
               c.OBSERVACION,
               NVL(d.TOTAL_DEBE, 0) AS TOTAL_DEBE,
               NVL(d.TOTAL_HABER, 0) AS TOTAL_HABER,
               CASE WHEN NVL(c.EXTRA1, ' ') = 'AUTOMATICO' THEN 1 ELSE 0 END AS ES_AUTOMATICO,
               c.CODIGO_EMPRESA
          FROM CNT.CNT_COMPROBANTES c
          LEFT JOIN CNT.CNT_PERIODOS p ON p.CODIGO_PERIODO = c.CODIGO_PERIODO
          LEFT JOIN CNT.CNT_DESCRIPTIVAS t ON t.DESCRIPCION_ID = c.TIPO_COMPROBANTE_ID
          LEFT JOIN CNT.CNT_DESCRIPTIVAS o ON o.DESCRIPCION_ID = c.ORIGEN_ID
          LEFT JOIN (
                SELECT CODIGO_COMPROBANTE,
                       SUM(CASE WHEN MONTO < 0 THEN ABS(MONTO) ELSE 0 END) AS TOTAL_DEBE,
                       SUM(CASE WHEN MONTO > 0 THEN MONTO ELSE 0 END) AS TOTAL_HABER
                  FROM CNT.CNT_DETALLE_COMPROBANTE
                 WHERE CODIGO_EMPRESA = p_CODIGO_EMPRESA
                 GROUP BY CODIGO_COMPROBANTE
          ) d ON d.CODIGO_COMPROBANTE = c.CODIGO_COMPROBANTE
         WHERE c.CODIGO_COMPROBANTE = p_CODIGO_COMPROBANTE
           AND c.CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_CMP_NUM_GEN (
    p_CODIGO_PERIODO      IN NUMBER,
    p_TIPO_COMPROBANTE_ID IN NUMBER,
    p_FECHA_COMPROBANTE   IN DATE,
    p_CODIGO_EMPRESA      IN NUMBER,
    p_NUMERO_OUT          OUT VARCHAR2,
    p_Message             OUT VARCHAR2
) AS
    v_numero      NUMBER;
    v_tipo_codigo VARCHAR2(10);
    v_dummy       NUMBER;
BEGIN
    SELECT 1
      INTO v_dummy
      FROM CNT.CNT_PERIODOS
     WHERE CODIGO_PERIODO = p_CODIGO_PERIODO
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA
       AND p_FECHA_COMPROBANTE BETWEEN FECHA_DESDE AND FECHA_HASTA
       AND FECHA_CIERRE IS NULL;

    SELECT CODIGO
      INTO v_tipo_codigo
      FROM CNT.CNT_DESCRIPTIVAS
     WHERE DESCRIPCION_ID = p_TIPO_COMPROBANTE_ID;

    SELECT NVL(MAX(TO_NUMBER(SUBSTR(NUMERO_COMPROBANTE, 10))), 0) + 1
      INTO v_numero
      FROM CNT.CNT_COMPROBANTES
     WHERE CODIGO_PERIODO = p_CODIGO_PERIODO
       AND TO_CHAR(FECHA_COMPROBANTE, 'MM') = TO_CHAR(p_FECHA_COMPROBANTE, 'MM')
       AND TIPO_COMPROBANTE_ID = p_TIPO_COMPROBANTE_ID
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    IF p_TIPO_COMPROBANTE_ID = 37 AND v_numero = 1 THEN
        v_numero := v_numero + 900;
    END IF;

    p_NUMERO_OUT := TO_CHAR(p_FECHA_COMPROBANTE, 'RR-MM') || '-' || v_tipo_codigo || '-' || LTRIM(TO_CHAR(v_numero, '000'));
    p_Message := 'Success';
EXCEPTION
    WHEN NO_DATA_FOUND THEN
        p_Message := 'No existe un periodo abierto para la fecha indicada o el tipo de comprobante no es valido.';
    WHEN OTHERS THEN
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_CMP_INS (
    p_CODIGO_PERIODO      IN NUMBER,
    p_TIPO_COMPROBANTE_ID IN NUMBER,
    p_FECHA_COMPROBANTE   IN DATE,
    p_ORIGEN_ID           IN NUMBER,
    p_OBSERVACION         IN VARCHAR2,
    p_USUARIO_ID          IN NUMBER,
    p_CODIGO_EMPRESA      IN NUMBER,
    p_CODIGO_OUT          OUT NUMBER,
    p_Message             OUT VARCHAR2,
    p_PERMITIR_AUTOMATICO IN NUMBER DEFAULT 0
) AS
    v_numero      NUMBER;
    v_tipo_codigo VARCHAR2(10);
    v_comprobante VARCHAR2(20);
    v_dummy       NUMBER;
BEGIN
    SELECT 1
      INTO v_dummy
      FROM CNT.CNT_PERIODOS
     WHERE CODIGO_PERIODO = p_CODIGO_PERIODO
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA
       AND p_FECHA_COMPROBANTE BETWEEN FECHA_DESDE AND FECHA_HASTA
       AND FECHA_CIERRE IS NULL;

    LOCK TABLE CNT.CNT_COMPROBANTES IN EXCLUSIVE MODE;

    SELECT CODIGO
      INTO v_tipo_codigo
      FROM CNT.CNT_DESCRIPTIVAS
     WHERE DESCRIPCION_ID = p_TIPO_COMPROBANTE_ID;

    SELECT NVL(MAX(TO_NUMBER(SUBSTR(NUMERO_COMPROBANTE, 10))), 0) + 1
      INTO v_numero
      FROM CNT.CNT_COMPROBANTES
     WHERE CODIGO_PERIODO = p_CODIGO_PERIODO
       AND TO_CHAR(FECHA_COMPROBANTE, 'MM') = TO_CHAR(p_FECHA_COMPROBANTE, 'MM')
       AND TIPO_COMPROBANTE_ID = p_TIPO_COMPROBANTE_ID
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    IF p_TIPO_COMPROBANTE_ID = 37 AND v_numero = 1 THEN
        v_numero := v_numero + 900;
    END IF;

    v_comprobante := TO_CHAR(p_FECHA_COMPROBANTE, 'RR-MM') || '-' || v_tipo_codigo || '-' || LTRIM(TO_CHAR(v_numero, '000'));

    SELECT CNT.CNT_S_CODIGO_COMPROBANTE.NEXTVAL INTO p_CODIGO_OUT FROM DUAL;

    INSERT INTO CNT.CNT_COMPROBANTES (
        CODIGO_COMPROBANTE, CODIGO_PERIODO, TIPO_COMPROBANTE_ID, NUMERO_COMPROBANTE,
        FECHA_COMPROBANTE, ORIGEN_ID, OBSERVACION, USUARIO_INS, FECHA_INS, CODIGO_EMPRESA
    ) VALUES (
        p_CODIGO_OUT, p_CODIGO_PERIODO, p_TIPO_COMPROBANTE_ID, v_comprobante,
        p_FECHA_COMPROBANTE, p_ORIGEN_ID, p_OBSERVACION, p_USUARIO_ID, SYSDATE, p_CODIGO_EMPRESA
    );

    p_Message := 'Success';
EXCEPTION
    WHEN NO_DATA_FOUND THEN
        p_Message := 'No existe un periodo abierto para la fecha indicada o el tipo de comprobante no es valido.';
    WHEN OTHERS THEN
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_CMP_NUM_ORD (
    p_CODIGO_PERIODO      IN NUMBER,
    p_TIPO_COMPROBANTE_ID IN NUMBER,
    p_USUARIO_ID          IN NUMBER,
    p_CODIGO_EMPRESA      IN NUMBER,
    p_CANTIDAD            OUT NUMBER,
    p_Message             OUT VARCHAR2
) AS
    v_dummy         NUMBER;
    v_tipo_codigo   VARCHAR2(10);
    v_numero        NUMBER := 0;
    v_mes_actual    VARCHAR2(2) := NULL;
    v_nuevo_numero  VARCHAR2(20);
BEGIN
    p_CANTIDAD := 0;

    IF p_CODIGO_PERIODO IS NULL OR p_TIPO_COMPROBANTE_ID IS NULL THEN
        p_Message := 'El periodo y el tipo de comprobante son requeridos.';
        RETURN;
    END IF;

    SELECT 1
      INTO v_dummy
      FROM CNT.CNT_PERIODOS
     WHERE CODIGO_PERIODO = p_CODIGO_PERIODO
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA
       AND FECHA_CIERRE IS NULL;

    SELECT CODIGO
      INTO v_tipo_codigo
      FROM CNT.CNT_DESCRIPTIVAS
     WHERE DESCRIPCION_ID = p_TIPO_COMPROBANTE_ID
       AND TITULO_ID = 5
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    LOCK TABLE CNT.CNT_COMPROBANTES IN EXCLUSIVE MODE;

    UPDATE CNT.CNT_COMPROBANTES
       SET NUMERO_COMPROBANTE = SUBSTR('TMP-' || TO_CHAR(CODIGO_COMPROBANTE), 1, 20),
           USUARIO_UPD = p_USUARIO_ID,
           FECHA_UPD = SYSDATE
     WHERE CODIGO_PERIODO = p_CODIGO_PERIODO
       AND TIPO_COMPROBANTE_ID = p_TIPO_COMPROBANTE_ID
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    FOR item IN (
        SELECT CODIGO_COMPROBANTE,
               FECHA_COMPROBANTE,
               TO_CHAR(FECHA_COMPROBANTE, 'MM') MES
          FROM CNT.CNT_COMPROBANTES
         WHERE CODIGO_PERIODO = p_CODIGO_PERIODO
           AND TIPO_COMPROBANTE_ID = p_TIPO_COMPROBANTE_ID
           AND CODIGO_EMPRESA = p_CODIGO_EMPRESA
         ORDER BY FECHA_COMPROBANTE, CODIGO_COMPROBANTE
    ) LOOP
        IF v_mes_actual IS NULL OR v_mes_actual <> item.MES THEN
            v_mes_actual := item.MES;
            v_numero := 1;

            IF p_TIPO_COMPROBANTE_ID = 37 THEN
                v_numero := 901;
            END IF;
        END IF;

        v_nuevo_numero := TO_CHAR(item.FECHA_COMPROBANTE, 'RR-MM') || '-' || v_tipo_codigo || '-' || LTRIM(TO_CHAR(v_numero, '000'));

        UPDATE CNT.CNT_COMPROBANTES
           SET NUMERO_COMPROBANTE = v_nuevo_numero,
               USUARIO_UPD = p_USUARIO_ID,
               FECHA_UPD = SYSDATE
         WHERE CODIGO_COMPROBANTE = item.CODIGO_COMPROBANTE
           AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

        p_CANTIDAD := p_CANTIDAD + 1;
        v_numero := v_numero + 1;
    END LOOP;

    p_Message := 'Success';
EXCEPTION
    WHEN NO_DATA_FOUND THEN
        p_CANTIDAD := 0;
        p_Message := 'No existe un periodo abierto o el tipo de comprobante no es valido.';
    WHEN OTHERS THEN
        p_CANTIDAD := 0;
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_CMP_UPD (
    p_CODIGO_COMPROBANTE  IN NUMBER,
    p_CODIGO_PERIODO      IN NUMBER,
    p_TIPO_COMPROBANTE_ID IN NUMBER,
    p_FECHA_COMPROBANTE   IN DATE,
    p_ORIGEN_ID           IN NUMBER,
    p_OBSERVACION         IN VARCHAR2,
    p_USUARIO_ID          IN NUMBER,
    p_CODIGO_EMPRESA      IN NUMBER,
    p_CODIGO_OUT          OUT NUMBER,
    p_Message             OUT VARCHAR2
) AS
    v_dummy NUMBER;
BEGIN
    SELECT 1
      INTO v_dummy
      FROM CNT.CNT_COMPROBANTES
     WHERE CODIGO_COMPROBANTE = p_CODIGO_COMPROBANTE
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA
       AND (NVL(EXTRA1, ' ') <> 'AUTOMATICO' OR NVL(p_PERMITIR_AUTOMATICO, 0) = 1);

    SELECT 1
      INTO v_dummy
      FROM CNT.CNT_PERIODOS
     WHERE CODIGO_PERIODO = p_CODIGO_PERIODO
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA
       AND p_FECHA_COMPROBANTE BETWEEN FECHA_DESDE AND FECHA_HASTA
       AND FECHA_CIERRE IS NULL;

    UPDATE CNT.CNT_COMPROBANTES
       SET CODIGO_PERIODO = p_CODIGO_PERIODO,
           TIPO_COMPROBANTE_ID = p_TIPO_COMPROBANTE_ID,
           FECHA_COMPROBANTE = p_FECHA_COMPROBANTE,
           ORIGEN_ID = p_ORIGEN_ID,
           OBSERVACION = p_OBSERVACION,
           USUARIO_UPD = p_USUARIO_ID,
           FECHA_UPD = SYSDATE
     WHERE CODIGO_COMPROBANTE = p_CODIGO_COMPROBANTE
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    p_CODIGO_OUT := p_CODIGO_COMPROBANTE;
    p_Message := CASE WHEN SQL%ROWCOUNT = 0 THEN 'Comprobante no encontrado.' ELSE 'Success' END;
EXCEPTION
    WHEN NO_DATA_FOUND THEN
        p_Message := 'Comprobante no encontrado, automatico sin permiso o periodo no valido.';
    WHEN OTHERS THEN
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_CMP_DEL (
    p_CODIGO_COMPROBANTE IN NUMBER,
    p_USUARIO_ID         IN NUMBER,
    p_CODIGO_EMPRESA     IN NUMBER,
    p_Message            OUT VARCHAR2,
    p_PERMITIR_AUTOMATICO IN NUMBER DEFAULT 0
) AS
    v_dummy NUMBER;
BEGIN
    SELECT 1
      INTO v_dummy
      FROM CNT.CNT_COMPROBANTES
     WHERE CODIGO_COMPROBANTE = p_CODIGO_COMPROBANTE
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA
       AND (NVL(EXTRA1, ' ') <> 'AUTOMATICO' OR NVL(p_PERMITIR_AUTOMATICO, 0) = 1);

    DELETE FROM CNT.CNT_DETALLE_COMPROBANTE
     WHERE CODIGO_COMPROBANTE = p_CODIGO_COMPROBANTE
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    DELETE FROM CNT.CNT_COMPROBANTES
     WHERE CODIGO_COMPROBANTE = p_CODIGO_COMPROBANTE
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    p_Message := CASE WHEN SQL%ROWCOUNT = 0 THEN 'Comprobante no encontrado.' ELSE 'Success' END;
EXCEPTION
    WHEN NO_DATA_FOUND THEN
        p_Message := 'Comprobante no encontrado o automatico sin permiso.';
    WHEN OTHERS THEN
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_DET_GET_CMP (
    p_CODIGO_COMPROBANTE IN NUMBER,
    p_CODIGO_EMPRESA     IN NUMBER,
    p_ResultSet          OUT SYS_REFCURSOR,
    p_Message            OUT VARCHAR2
) AS
BEGIN
    OPEN p_ResultSet FOR
        SELECT d.CODIGO_DETALLE_COMPROBANTE,
               d.CODIGO_COMPROBANTE,
               d.CODIGO_MAYOR,
               m.NUMERO_MAYOR || ' - ' || m.DENOMINACION AS MAYOR,
               d.CODIGO_AUXILIAR,
               a.SEGMENTO1 || ' ' || a.SEGMENTO2 || ' - ' || a.DENOMINACION AS AUXILIAR,
               d.REFERENCIA1,
               d.REFERENCIA2,
               d.REFERENCIA3,
               d.DESCRIPCION,
               d.MONTO,
               d.CODIGO_EMPRESA
          FROM CNT.CNT_DETALLE_COMPROBANTE d
          JOIN CNT.CNT_MAYORES m ON m.CODIGO_MAYOR = d.CODIGO_MAYOR
          JOIN CNT.CNT_AUXILIARES a ON a.CODIGO_AUXILIAR = d.CODIGO_AUXILIAR
         WHERE d.CODIGO_COMPROBANTE = p_CODIGO_COMPROBANTE
           AND d.CODIGO_EMPRESA = p_CODIGO_EMPRESA
         ORDER BY d.CODIGO_DETALLE_COMPROBANTE;

    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_DET_INS (
    p_CODIGO_COMPROBANTE IN NUMBER,
    p_CODIGO_MAYOR       IN NUMBER,
    p_CODIGO_AUXILIAR    IN NUMBER,
    p_REFERENCIA1        IN VARCHAR2,
    p_REFERENCIA2        IN VARCHAR2,
    p_REFERENCIA3        IN VARCHAR2,
    p_DESCRIPCION        IN VARCHAR2,
    p_MONTO              IN NUMBER,
    p_USUARIO_ID         IN NUMBER,
    p_CODIGO_EMPRESA     IN NUMBER,
    p_CODIGO_OUT         OUT NUMBER,
    p_Message            OUT VARCHAR2,
    p_PERMITIR_AUTOMATICO IN NUMBER DEFAULT 0
) AS
    v_dummy NUMBER;
BEGIN
    SELECT 1
      INTO v_dummy
      FROM CNT.CNT_COMPROBANTES
     WHERE CODIGO_COMPROBANTE = p_CODIGO_COMPROBANTE
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA
       AND (NVL(EXTRA1, ' ') <> 'AUTOMATICO' OR NVL(p_PERMITIR_AUTOMATICO, 0) = 1);

    SELECT CNT.CNT_S_CODIGO_DETALLE_COMPROBAN.NEXTVAL INTO p_CODIGO_OUT FROM DUAL;

    INSERT INTO CNT.CNT_DETALLE_COMPROBANTE (
        CODIGO_DETALLE_COMPROBANTE, CODIGO_COMPROBANTE, CODIGO_MAYOR, CODIGO_AUXILIAR,
        REFERENCIA1, REFERENCIA2, REFERENCIA3, DESCRIPCION, MONTO,
        USUARIO_INS, FECHA_INS, CODIGO_EMPRESA
    ) VALUES (
        p_CODIGO_OUT, p_CODIGO_COMPROBANTE, p_CODIGO_MAYOR, p_CODIGO_AUXILIAR,
        p_REFERENCIA1, p_REFERENCIA2, p_REFERENCIA3, p_DESCRIPCION, p_MONTO,
        p_USUARIO_ID, SYSDATE, p_CODIGO_EMPRESA
    );

    p_Message := 'Success';
EXCEPTION
    WHEN NO_DATA_FOUND THEN
        p_Message := 'No se puede modificar el detalle de un comprobante automatico sin permiso.';
    WHEN OTHERS THEN
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_DET_DEL_CMP (
    p_CODIGO_COMPROBANTE IN NUMBER,
    p_CODIGO_EMPRESA     IN NUMBER,
    p_USUARIO_ID         IN NUMBER,
    p_Message            OUT VARCHAR2,
    p_PERMITIR_AUTOMATICO IN NUMBER DEFAULT 0
) AS
    v_dummy NUMBER;
BEGIN
    SELECT 1
      INTO v_dummy
      FROM CNT.CNT_COMPROBANTES
     WHERE CODIGO_COMPROBANTE = p_CODIGO_COMPROBANTE
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA
       AND (NVL(EXTRA1, ' ') <> 'AUTOMATICO' OR NVL(p_PERMITIR_AUTOMATICO, 0) = 1);

    DELETE FROM CNT.CNT_DETALLE_COMPROBANTE
     WHERE CODIGO_COMPROBANTE = p_CODIGO_COMPROBANTE
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    p_Message := 'Success';
EXCEPTION
    WHEN NO_DATA_FOUND THEN
        p_Message := 'No se puede modificar el detalle de un comprobante automatico sin permiso.';
    WHEN OTHERS THEN
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_PER_GET_OPEN (
    p_SOLO_ABIERTOS IN NUMBER,
    p_FECHA         IN DATE,
    p_CODIGO_EMPRESA IN NUMBER,
    p_ResultSet     OUT SYS_REFCURSOR,
    p_Message       OUT VARCHAR2
) AS
BEGIN
    OPEN p_ResultSet FOR
        SELECT CODIGO_PERIODO,
               NOMBRE_PERIODO,
               FECHA_DESDE,
               FECHA_HASTA,
               ANO_PERIODO,
               NUMERO_PERIODO,
               CASE WHEN FECHA_CIERRE IS NULL THEN 0 ELSE 1 END AS CERRADO
          FROM CNT.CNT_PERIODOS
         WHERE CODIGO_EMPRESA = p_CODIGO_EMPRESA
           AND (NVL(p_SOLO_ABIERTOS, 0) = 0 OR FECHA_CIERRE IS NULL)
           AND (p_FECHA IS NULL OR p_FECHA BETWEEN FECHA_DESDE AND FECHA_HASTA)
         ORDER BY ANO_PERIODO DESC, NUMERO_PERIODO DESC;

    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_MAY_SEARCH (
    p_SEARCH_TEXT    IN VARCHAR2,
    p_PAGE_SIZE      IN NUMBER,
    p_CODIGO_EMPRESA IN NUMBER,
    p_ResultSet      OUT SYS_REFCURSOR,
    p_Message        OUT VARCHAR2
) AS
BEGIN
    OPEN p_ResultSet FOR
        SELECT *
          FROM (
                SELECT CODIGO_MAYOR,
                       NUMERO_MAYOR,
                       DENOMINACION,
                       DESCRIPCION,
                       COLUMNA_BALANCE
                  FROM CNT.CNT_MAYORES
                 WHERE CODIGO_EMPRESA = p_CODIGO_EMPRESA
                   AND (p_SEARCH_TEXT IS NULL
                        OR UPPER(NUMERO_MAYOR) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%'
                        OR UPPER(DENOMINACION) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%')
                 ORDER BY NUMERO_MAYOR
          )
         WHERE ROWNUM <= NVL(NULLIF(p_PAGE_SIZE, 0), 20);

    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_AUX_SEARCH (
    p_SEARCH_TEXT    IN VARCHAR2,
    p_CODIGO_MAYOR   IN NUMBER,
    p_PAGE_SIZE      IN NUMBER,
    p_CODIGO_EMPRESA IN NUMBER,
    p_ResultSet      OUT SYS_REFCURSOR,
    p_Message        OUT VARCHAR2
) AS
BEGIN
    OPEN p_ResultSet FOR
        SELECT *
          FROM (
                SELECT CODIGO_AUXILIAR,
                       CODIGO_MAYOR,
                       SEGMENTO1,
                       SEGMENTO2,
                       DENOMINACION,
                       DESCRIPCION
                  FROM CNT.CNT_AUXILIARES
                 WHERE CODIGO_EMPRESA = p_CODIGO_EMPRESA
                   AND (p_CODIGO_MAYOR IS NULL OR CODIGO_MAYOR = p_CODIGO_MAYOR)
                   AND (p_SEARCH_TEXT IS NULL
                        OR UPPER(SEGMENTO1) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%'
                        OR UPPER(SEGMENTO2) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%'
                        OR UPPER(DENOMINACION) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%')
                 ORDER BY SEGMENTO1, SEGMENTO2, DENOMINACION
          )
         WHERE ROWNUM <= NVL(NULLIF(p_PAGE_SIZE, 0), 20);

    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_DET_UPD (
    p_CODIGO_DETALLE  IN NUMBER,
    p_CODIGO_MAYOR    IN NUMBER,
    p_CODIGO_AUXILIAR IN NUMBER,
    p_REFERENCIA1     IN VARCHAR2,
    p_REFERENCIA2     IN VARCHAR2,
    p_REFERENCIA3     IN VARCHAR2,
    p_DESCRIPCION     IN VARCHAR2,
    p_MONTO           IN NUMBER,
    p_USUARIO_ID      IN NUMBER,
    p_CODIGO_EMPRESA  IN NUMBER,
    p_CODIGO_OUT      OUT NUMBER,
    p_Message         OUT VARCHAR2,
    p_PERMITIR_AUTOMATICO IN NUMBER DEFAULT 0
) AS
    v_dummy NUMBER;
BEGIN
    SELECT 1
      INTO v_dummy
      FROM CNT.CNT_DETALLE_COMPROBANTE d
      JOIN CNT.CNT_COMPROBANTES c ON c.CODIGO_COMPROBANTE = d.CODIGO_COMPROBANTE
     WHERE d.CODIGO_DETALLE_COMPROBANTE = p_CODIGO_DETALLE
       AND d.CODIGO_EMPRESA = p_CODIGO_EMPRESA
       AND c.CODIGO_EMPRESA = p_CODIGO_EMPRESA
       AND (NVL(c.EXTRA1, ' ') <> 'AUTOMATICO' OR NVL(p_PERMITIR_AUTOMATICO, 0) = 1);

    UPDATE CNT.CNT_DETALLE_COMPROBANTE
       SET CODIGO_MAYOR = p_CODIGO_MAYOR,
           CODIGO_AUXILIAR = p_CODIGO_AUXILIAR,
           REFERENCIA1 = p_REFERENCIA1,
           REFERENCIA2 = p_REFERENCIA2,
           REFERENCIA3 = p_REFERENCIA3,
           DESCRIPCION = p_DESCRIPCION,
           MONTO = p_MONTO,
           USUARIO_UPD = p_USUARIO_ID,
           FECHA_UPD = SYSDATE
     WHERE CODIGO_DETALLE_COMPROBANTE = p_CODIGO_DETALLE
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    p_CODIGO_OUT := p_CODIGO_DETALLE;
    p_Message := CASE WHEN SQL%ROWCOUNT = 0 THEN 'Detalle no encontrado.' ELSE 'Success' END;
EXCEPTION
    WHEN NO_DATA_FOUND THEN
        p_Message := 'Detalle no encontrado o pertenece a un comprobante automatico sin permiso.';
    WHEN OTHERS THEN
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_DET_DEL (
    p_CODIGO_DETALLE IN NUMBER,
    p_USUARIO_ID     IN NUMBER,
    p_CODIGO_EMPRESA IN NUMBER,
    p_Message        OUT VARCHAR2,
    p_PERMITIR_AUTOMATICO IN NUMBER DEFAULT 0
) AS
    v_dummy NUMBER;
BEGIN
    SELECT 1
      INTO v_dummy
      FROM CNT.CNT_DETALLE_COMPROBANTE d
      JOIN CNT.CNT_COMPROBANTES c ON c.CODIGO_COMPROBANTE = d.CODIGO_COMPROBANTE
     WHERE d.CODIGO_DETALLE_COMPROBANTE = p_CODIGO_DETALLE
       AND d.CODIGO_EMPRESA = p_CODIGO_EMPRESA
       AND c.CODIGO_EMPRESA = p_CODIGO_EMPRESA
       AND (NVL(c.EXTRA1, ' ') <> 'AUTOMATICO' OR NVL(p_PERMITIR_AUTOMATICO, 0) = 1);

    DELETE FROM CNT.CNT_DETALLE_COMPROBANTE
     WHERE CODIGO_DETALLE_COMPROBANTE = p_CODIGO_DETALLE
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    p_Message := CASE WHEN SQL%ROWCOUNT = 0 THEN 'Detalle no encontrado.' ELSE 'Success' END;
EXCEPTION
    WHEN NO_DATA_FOUND THEN
        p_Message := 'Detalle no encontrado o pertenece a un comprobante automatico sin permiso.';
    WHEN OTHERS THEN
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_RPT_MAY_ANA (
    p_PAGE_SIZE      IN NUMBER,
    p_PAGE_NUMBER    IN NUMBER,
    p_SEARCH_TEXT    IN VARCHAR2,
    p_CODIGO_PERIODO IN NUMBER,
    p_CODIGO_MAYOR   IN NUMBER,
    p_CODIGO_AUXILIAR IN NUMBER,
    p_FECHA_DESDE    IN DATE,
    p_FECHA_HASTA    IN DATE,
    p_CODIGO_EMPRESA IN NUMBER,
    p_ResultSet      OUT SYS_REFCURSOR,
    p_Message        OUT VARCHAR2,
    p_TotalRecords   OUT NUMBER,
    p_TotalPages     OUT NUMBER
) AS
    v_page_size NUMBER := NVL(NULLIF(p_PAGE_SIZE, 0), 10);
    v_page_number NUMBER := NVL(NULLIF(p_PAGE_NUMBER, 0), 1);
BEGIN
    SELECT COUNT(*)
      INTO p_TotalRecords
      FROM CNT.CNT_V_MAYOR_ANALITICO r
     WHERE r.CODIGO_EMPRESA = p_CODIGO_EMPRESA
       AND (p_CODIGO_PERIODO IS NULL
            OR EXISTS (
                SELECT 1
                  FROM CNT.CNT_COMPROBANTES c
                 WHERE c.CODIGO_COMPROBANTE = r.CODIGO_COMPROBANTE
                   AND c.CODIGO_EMPRESA = r.CODIGO_EMPRESA
                   AND c.CODIGO_PERIODO = p_CODIGO_PERIODO
            ))
       AND (p_CODIGO_MAYOR IS NULL OR r.CODIGO_MAYOR = p_CODIGO_MAYOR)
       AND (p_CODIGO_AUXILIAR IS NULL OR r.CODIGO_AUXILIAR = p_CODIGO_AUXILIAR)
       AND (p_FECHA_DESDE IS NULL OR r.FECHA_COMPROBANTE IS NULL OR r.FECHA_COMPROBANTE >= p_FECHA_DESDE)
       AND (p_FECHA_HASTA IS NULL OR r.FECHA_COMPROBANTE IS NULL OR r.FECHA_COMPROBANTE < p_FECHA_HASTA + 1)
       AND (p_SEARCH_TEXT IS NULL
            OR UPPER(r.CODIGO_CUENTA) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%'
            OR UPPER(r.DENOMINACION_CUENTA) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%'
            OR UPPER(r.NUMERO_COMPROBANTE) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%'
            OR UPPER(r.DESCRIPCION) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%'
            OR UPPER(r.REFERENCIA1) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%'
            OR UPPER(r.REFERENCIA2) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%');

    p_TotalPages := CEIL(p_TotalRecords / v_page_size);

    OPEN p_ResultSet FOR
        SELECT CODIGO_COMPROBANTE,
               CODIGO_MAYOR,
               CODIGO_AUXILIAR,
               CODIGO_CUENTA,
               DENOMINACION_CUENTA,
               NUMERO_COMPROBANTE,
               FECHA_COMPROBANTE,
               DESCRIPCION,
               REFERENCIA1,
               REFERENCIA2,
               MONTO,
               CODIGO_EMPRESA
          FROM (
                SELECT ordered_rows.*, ROWNUM RN
                  FROM (
                        SELECT r.CODIGO_COMPROBANTE,
                               r.CODIGO_MAYOR,
                               r.CODIGO_AUXILIAR,
                               r.CODIGO_CUENTA,
                               r.DENOMINACION_CUENTA,
                               r.NUMERO_COMPROBANTE,
                               r.FECHA_COMPROBANTE,
                               r.DESCRIPCION,
                               r.REFERENCIA1,
                               r.REFERENCIA2,
                               r.MONTO,
                               r.CODIGO_EMPRESA
                         FROM CNT.CNT_V_MAYOR_ANALITICO r
                         WHERE r.CODIGO_EMPRESA = p_CODIGO_EMPRESA
                           AND (p_CODIGO_PERIODO IS NULL
                                OR EXISTS (
                                    SELECT 1
                                      FROM CNT.CNT_COMPROBANTES c
                                     WHERE c.CODIGO_COMPROBANTE = r.CODIGO_COMPROBANTE
                                       AND c.CODIGO_EMPRESA = r.CODIGO_EMPRESA
                                       AND c.CODIGO_PERIODO = p_CODIGO_PERIODO
                                ))
                           AND (p_CODIGO_MAYOR IS NULL OR r.CODIGO_MAYOR = p_CODIGO_MAYOR)
                           AND (p_CODIGO_AUXILIAR IS NULL OR r.CODIGO_AUXILIAR = p_CODIGO_AUXILIAR)
                           AND (p_FECHA_DESDE IS NULL OR r.FECHA_COMPROBANTE IS NULL OR r.FECHA_COMPROBANTE >= p_FECHA_DESDE)
                           AND (p_FECHA_HASTA IS NULL OR r.FECHA_COMPROBANTE IS NULL OR r.FECHA_COMPROBANTE < p_FECHA_HASTA + 1)
                           AND (p_SEARCH_TEXT IS NULL
                                OR UPPER(r.CODIGO_CUENTA) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%'
                                OR UPPER(r.DENOMINACION_CUENTA) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%'
                                OR UPPER(r.NUMERO_COMPROBANTE) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%'
                                OR UPPER(r.DESCRIPCION) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%'
                                OR UPPER(r.REFERENCIA1) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%'
                                OR UPPER(r.REFERENCIA2) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%')
                         ORDER BY r.CODIGO_CUENTA, r.FECHA_COMPROBANTE, r.CODIGO_COMPROBANTE
                  ) ordered_rows
                 WHERE ROWNUM <= v_page_number * v_page_size
          )
         WHERE RN > (v_page_number - 1) * v_page_size;

    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        p_TotalRecords := 0;
        p_TotalPages := 0;
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_RPT_MOV_AUX (
    p_PAGE_SIZE      IN NUMBER,
    p_PAGE_NUMBER    IN NUMBER,
    p_SEARCH_TEXT    IN VARCHAR2,
    p_CODIGO_PERIODO IN NUMBER,
    p_CODIGO_AUXILIAR IN NUMBER,
    p_FECHA_DESDE    IN DATE,
    p_FECHA_HASTA    IN DATE,
    p_CODIGO_EMPRESA IN NUMBER,
    p_ResultSet      OUT SYS_REFCURSOR,
    p_Message        OUT VARCHAR2,
    p_TotalRecords   OUT NUMBER,
    p_TotalPages     OUT NUMBER
) AS
    v_page_size NUMBER := NVL(NULLIF(p_PAGE_SIZE, 0), 10);
    v_page_number NUMBER := NVL(NULLIF(p_PAGE_NUMBER, 0), 1);
BEGIN
    SELECT COUNT(*)
      INTO p_TotalRecords
     FROM CNT.CNT_V_MOV_AUXILIAR r
     WHERE r.CODIGO_EMPRESA = p_CODIGO_EMPRESA
       AND (p_CODIGO_PERIODO IS NULL
            OR EXISTS (
                SELECT 1
                  FROM CNT.CNT_COMPROBANTES c
                 WHERE c.CODIGO_COMPROBANTE = r.CODIGO_COMPROBANTE
                   AND c.CODIGO_EMPRESA = r.CODIGO_EMPRESA
                   AND c.CODIGO_PERIODO = p_CODIGO_PERIODO
            ))
       AND (p_CODIGO_AUXILIAR IS NULL OR r.CODIGO_AUXILIAR = p_CODIGO_AUXILIAR)
       AND (p_FECHA_DESDE IS NULL OR r.FECHA_COMPROBANTE IS NULL OR r.FECHA_COMPROBANTE >= p_FECHA_DESDE)
       AND (p_FECHA_HASTA IS NULL OR r.FECHA_COMPROBANTE IS NULL OR r.FECHA_COMPROBANTE < p_FECHA_HASTA + 1)
       AND (p_SEARCH_TEXT IS NULL
            OR UPPER(r.NUMERO_CONTABLE) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%'
            OR UPPER(r.NOMBRE_AUXILIAR) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%'
            OR UPPER(r.NUMERO_COMPROBANTE) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%'
            OR UPPER(r.DESCRIPCION) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%'
            OR UPPER(r.REFERENCIA1) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%'
            OR UPPER(r.REFERENCIA2) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%');

    p_TotalPages := CEIL(p_TotalRecords / v_page_size);

    OPEN p_ResultSet FOR
        SELECT CODIGO_COMPROBANTE,
               CODIGO_AUXILIAR,
               NUMERO_CONTABLE,
               NOMBRE_AUXILIAR,
               NUMERO_COMPROBANTE,
               FECHA_COMPROBANTE,
               DESCRIPCION,
               REFERENCIA1,
               REFERENCIA2,
               MONTO,
               CODIGO_EMPRESA
          FROM (
                SELECT ordered_rows.*, ROWNUM RN
                  FROM (
                        SELECT r.CODIGO_COMPROBANTE,
                               r.CODIGO_AUXILIAR,
                               r.NUMERO_CONTABLE,
                               r.NOMBRE_AUXILIAR,
                               r.NUMERO_COMPROBANTE,
                               r.FECHA_COMPROBANTE,
                               r.DESCRIPCION,
                               r.REFERENCIA1,
                               r.REFERENCIA2,
                               r.MONTO,
                               r.CODIGO_EMPRESA
                          FROM CNT.CNT_V_MOV_AUXILIAR r
                         WHERE r.CODIGO_EMPRESA = p_CODIGO_EMPRESA
                           AND (p_CODIGO_PERIODO IS NULL
                                OR EXISTS (
                                    SELECT 1
                                      FROM CNT.CNT_COMPROBANTES c
                                     WHERE c.CODIGO_COMPROBANTE = r.CODIGO_COMPROBANTE
                                       AND c.CODIGO_EMPRESA = r.CODIGO_EMPRESA
                                       AND c.CODIGO_PERIODO = p_CODIGO_PERIODO
                                ))
                           AND (p_CODIGO_AUXILIAR IS NULL OR r.CODIGO_AUXILIAR = p_CODIGO_AUXILIAR)
                           AND (p_FECHA_DESDE IS NULL OR r.FECHA_COMPROBANTE IS NULL OR r.FECHA_COMPROBANTE >= p_FECHA_DESDE)
                           AND (p_FECHA_HASTA IS NULL OR r.FECHA_COMPROBANTE IS NULL OR r.FECHA_COMPROBANTE < p_FECHA_HASTA + 1)
                           AND (p_SEARCH_TEXT IS NULL
                                OR UPPER(r.NUMERO_CONTABLE) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%'
                                OR UPPER(r.NOMBRE_AUXILIAR) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%'
                                OR UPPER(r.NUMERO_COMPROBANTE) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%'
                                OR UPPER(r.DESCRIPCION) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%'
                                OR UPPER(r.REFERENCIA1) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%'
                                OR UPPER(r.REFERENCIA2) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%')
                         ORDER BY r.NUMERO_CONTABLE, r.FECHA_COMPROBANTE, r.CODIGO_COMPROBANTE
                  ) ordered_rows
                 WHERE ROWNUM <= v_page_number * v_page_size
          )
         WHERE RN > (v_page_number - 1) * v_page_size;

    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        p_TotalRecords := 0;
        p_TotalPages := 0;
        p_Message := SQLERRM;
END;
/
