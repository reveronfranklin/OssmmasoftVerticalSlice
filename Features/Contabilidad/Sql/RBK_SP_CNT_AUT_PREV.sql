-- R58: restore captured QA baseline; verify its hash before use.
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
    v_run_id             VARCHAR2(32) := RAWTOHEX(SYS_GUID());

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
                   CAST(NULL AS VARCHAR2(1000)) DESCRIPCION,
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
       AND (CODIGO_EMPRESA IS NULL OR CODIGO_EMPRESA = p_CODIGO_EMPRESA);

    SELECT 1
      INTO v_dummy
      FROM CNT.CNT_DESCRIPTIVAS
     WHERE DESCRIPCION_ID = p_ORIGEN_ID
       AND TITULO_ID = 1
       AND (CODIGO_EMPRESA IS NULL OR CODIGO_EMPRESA = p_CODIGO_EMPRESA);

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
        CNT.CNT_DESCRIPTIVA_ID('ANODPAUT', 'ODC'),
        CNT.CNT_DESCRIPTIVA_ID('COMP', 'ODC'),
        CNT.CNT_DESCRIPTIVA_ID('ANCOMP', 'ODC')
    ) THEN
        IF v_codigo_presupuesto IS NULL THEN
            open_empty_result;
            p_Message := 'No existe presupuesto vigente para el rango indicado.';
            RETURN;
        END IF;

        IF p_ORIGEN_ID IN (
            CNT.CNT_DESCRIPTIVA_ID('ODPAUT', 'ODC'),
            CNT.CNT_DESCRIPTIVA_ID('ANODPAUT', 'ODC')
        ) THEN
            CNT.SP_CNT_AUT_BUILD_OP(
                v_run_id,
                p_ORIGEN_ID,
                p_FECHA_DESDE,
                p_FECHA_HASTA,
                v_codigo_presupuesto,
                p_CODIGO_EMPRESA,
                p_Message
            );
        ELSE
            CNT.SP_CNT_AUT_BUILD_COMP(
                v_run_id,
                p_ORIGEN_ID,
                p_FECHA_DESDE,
                p_FECHA_HASTA,
                v_codigo_presupuesto,
                p_CODIGO_EMPRESA,
                p_Message
            );
        END IF;

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
              FROM CNT.CNT_AUT_LINE_WRK
             WHERE RUN_ID = v_run_id
             ORDER BY SECUENCIA;

        p_Message := 'Success';
        RETURN;
    END IF;

    IF p_ORIGEN_ID IN (
        CNT.CNT_DESCRIPTIVA_ID('CHEAUT', 'ODC'),
        CNT.CNT_DESCRIPTIVA_ID('CHEPROAUT', 'ODC'),
        CNT.CNT_DESCRIPTIVA_ID('CHERETAUT', 'ODC'),
        CNT.CNT_DESCRIPTIVA_ID('CHERETDAUT', 'ODC'),
        CNT.CNT_DESCRIPTIVA_ID('RETENFTDT', 'ODC'),
        CNT.CNT_DESCRIPTIVA_ID('ANCHEAUT', 'ODC')
    ) THEN
        IF v_codigo_presupuesto IS NULL THEN
            open_empty_result;
            p_Message := 'No existe presupuesto vigente para el rango indicado.';
            RETURN;
        END IF;

        CNT.SP_CNT_AUT_BUILD_CH_RET(
            v_run_id,
            p_ORIGEN_ID,
            p_TIPO_COMPROBANTE_ID,
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
              FROM CNT.CNT_AUT_LINE_WRK
             WHERE RUN_ID = v_run_id
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
                           SUBSTRB('(' || x.TIPO_COMPROMISO || ') ' || x.MOTIVO, 1, 200) DESCRIPCION,
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
                           SUBSTRB('(' || x.TIPO_COMPROMISO || ') ' || x.MOTIVO, 1, 200) DESCRIPCION,
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
                                   SUBSTRB(A.OBRA, 1, 200) MOTIVO,
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
                                   SUBSTRB(A.OBRA, 1, 200) MOTIVO,
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
