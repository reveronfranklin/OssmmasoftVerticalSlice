-- R58: restore captured QA baseline; verify its hash before use.
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
    v_run_id             VARCHAR2(32) := RAWTOHEX(SYS_GUID());

    PROCEDURE cleanup_comprobante IS
    BEGIN
        DELETE FROM CNT.CNT_AUT_LINE_WRK
         WHERE RUN_ID = v_run_id;

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
    CNT.CNT_CMP_GUARD.LOCK_ALL;
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

    IF p_FECHA_COMPROBANTE < TRUNC(p_FECHA_DESDE)
       OR p_FECHA_COMPROBANTE >= TRUNC(p_FECHA_HASTA) + 1 THEN
        p_Message := 'La fecha del comprobante debe estar entre fecha desde y fecha hasta.';
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

        CNT.CNT_CMP_GUARD.ALLOW_WRITE(p_CODIGO_COMPROBANTE, p_CODIGO_EMPRESA);
        UPDATE CNT.CNT_COMPROBANTES SET ESTADO_CMP = 'PENDIENTE', EXTRA1 = 'AUTOMATICO'
         WHERE CODIGO_COMPROBANTE = p_CODIGO_COMPROBANTE AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

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
              FROM CNT.CNT_AUT_LINE_WRK
             WHERE RUN_ID = v_run_id
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
                v_det_message,
                1
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

        DELETE FROM CNT.CNT_AUT_LINE_WRK
         WHERE RUN_ID = v_run_id;

        IF p_CANTIDAD_LINEAS = 0 THEN
            cleanup_comprobante;
            p_CODIGO_COMPROBANTE := 0;
            p_NUMERO_COMPROBANTE := NULL;
            p_Message := 'El proceso automatico no genero lineas para el rango indicado.';
            RETURN;
        END IF;

        SELECT NVL(SUM(CASE WHEN MONTO < 0 THEN -MONTO ELSE 0 END), 0),
               NVL(SUM(CASE WHEN MONTO > 0 THEN MONTO ELSE 0 END), 0)
          INTO p_TOTAL_DEBE, p_TOTAL_HABER FROM CNT.CNT_DETALLE_COMPROBANTE
         WHERE CODIGO_COMPROBANTE = p_CODIGO_COMPROBANTE AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

        SELECT HABILITADO INTO v_dummy FROM CNT.CNT_CMP_CTL WHERE CODIGO = 1;
        IF p_TOTAL_DEBE <> p_TOTAL_HABER AND v_dummy <> 1 THEN
            cleanup_comprobante;
            p_Message := 'La generacion pendiente requiere completar la instalacion y validacion CNT 37.';
            RETURN;
        END IF;

        UPDATE CNT.CNT_COMPROBANTES
           SET EXTRA1 = 'AUTOMATICO',
               ESTADO_CMP = CASE WHEN p_TOTAL_DEBE = p_TOTAL_HABER THEN 'FINALIZADO' ELSE 'PENDIENTE' END,
               USUARIO_UPD = p_USUARIO_ID,
               FECHA_UPD = SYSDATE
         WHERE CODIGO_COMPROBANTE = p_CODIGO_COMPROBANTE
           AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

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

        CNT.CNT_CMP_GUARD.ALLOW_WRITE(p_CODIGO_COMPROBANTE, p_CODIGO_EMPRESA);
        UPDATE CNT.CNT_COMPROBANTES SET ESTADO_CMP = 'PENDIENTE', EXTRA1 = 'AUTOMATICO'
         WHERE CODIGO_COMPROBANTE = p_CODIGO_COMPROBANTE AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

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
              FROM CNT.CNT_AUT_LINE_WRK
             WHERE RUN_ID = v_run_id
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
                v_det_message,
                1
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

        DELETE FROM CNT.CNT_AUT_LINE_WRK
         WHERE RUN_ID = v_run_id;

        IF p_CANTIDAD_LINEAS = 0 THEN
            cleanup_comprobante;
            p_CODIGO_COMPROBANTE := 0;
            p_NUMERO_COMPROBANTE := NULL;
            p_Message := 'El proceso automatico no genero lineas para el rango indicado.';
            RETURN;
        END IF;

        SELECT NVL(SUM(CASE WHEN MONTO < 0 THEN -MONTO ELSE 0 END), 0),
               NVL(SUM(CASE WHEN MONTO > 0 THEN MONTO ELSE 0 END), 0)
          INTO p_TOTAL_DEBE, p_TOTAL_HABER FROM CNT.CNT_DETALLE_COMPROBANTE
         WHERE CODIGO_COMPROBANTE = p_CODIGO_COMPROBANTE AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

        SELECT HABILITADO INTO v_dummy FROM CNT.CNT_CMP_CTL WHERE CODIGO = 1;
        IF p_TOTAL_DEBE <> p_TOTAL_HABER AND v_dummy <> 1 THEN
            cleanup_comprobante;
            p_Message := 'La generacion pendiente requiere completar la instalacion y validacion CNT 37.';
            RETURN;
        END IF;

        UPDATE CNT.CNT_COMPROBANTES
           SET EXTRA1 = 'AUTOMATICO',
               ESTADO_CMP = CASE WHEN p_TOTAL_DEBE = p_TOTAL_HABER THEN 'FINALIZADO' ELSE 'PENDIENTE' END,
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

        CNT.CNT_CMP_GUARD.ALLOW_WRITE(p_CODIGO_COMPROBANTE, p_CODIGO_EMPRESA);
        UPDATE CNT.CNT_COMPROBANTES SET ESTADO_CMP = 'PENDIENTE', EXTRA1 = 'AUTOMATICO'
         WHERE CODIGO_COMPROBANTE = p_CODIGO_COMPROBANTE AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

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
                           SUBSTRB('(' || x.TIPO_COMPROMISO || ') ' || x.MOTIVO, 1, 200) DESCRIPCION,
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
                v_det_message,
                1
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

        SELECT NVL(SUM(CASE WHEN MONTO < 0 THEN -MONTO ELSE 0 END), 0),
               NVL(SUM(CASE WHEN MONTO > 0 THEN MONTO ELSE 0 END), 0)
          INTO p_TOTAL_DEBE, p_TOTAL_HABER FROM CNT.CNT_DETALLE_COMPROBANTE
         WHERE CODIGO_COMPROBANTE = p_CODIGO_COMPROBANTE AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

        SELECT HABILITADO INTO v_dummy FROM CNT.CNT_CMP_CTL WHERE CODIGO = 1;
        IF p_TOTAL_DEBE <> p_TOTAL_HABER AND v_dummy <> 1 THEN
            cleanup_comprobante;
            p_Message := 'La generacion pendiente requiere completar la instalacion y validacion CNT 37.';
            RETURN;
        END IF;

        UPDATE CNT.CNT_COMPROBANTES
           SET EXTRA1 = 'AUTOMATICO',
               ESTADO_CMP = CASE WHEN p_TOTAL_DEBE = p_TOTAL_HABER THEN 'FINALIZADO' ELSE 'PENDIENTE' END,
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

        CNT.CNT_CMP_GUARD.ALLOW_WRITE(p_CODIGO_COMPROBANTE, p_CODIGO_EMPRESA);
        UPDATE CNT.CNT_COMPROBANTES SET ESTADO_CMP = 'PENDIENTE', EXTRA1 = 'AUTOMATICO'
         WHERE CODIGO_COMPROBANTE = p_CODIGO_COMPROBANTE AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

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
                           SUBSTRB('(' || x.TIPO_COMPROMISO || ') ' || x.MOTIVO, 1, 200) DESCRIPCION,
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
                v_det_message,
                1
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

        SELECT NVL(SUM(CASE WHEN MONTO < 0 THEN -MONTO ELSE 0 END), 0),
               NVL(SUM(CASE WHEN MONTO > 0 THEN MONTO ELSE 0 END), 0)
          INTO p_TOTAL_DEBE, p_TOTAL_HABER FROM CNT.CNT_DETALLE_COMPROBANTE
         WHERE CODIGO_COMPROBANTE = p_CODIGO_COMPROBANTE AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

        SELECT HABILITADO INTO v_dummy FROM CNT.CNT_CMP_CTL WHERE CODIGO = 1;
        IF p_TOTAL_DEBE <> p_TOTAL_HABER AND v_dummy <> 1 THEN
            cleanup_comprobante;
            p_Message := 'La generacion pendiente requiere completar la instalacion y validacion CNT 37.';
            RETURN;
        END IF;

        UPDATE CNT.CNT_COMPROBANTES
           SET EXTRA1 = 'AUTOMATICO',
               ESTADO_CMP = CASE WHEN p_TOTAL_DEBE = p_TOTAL_HABER THEN 'FINALIZADO' ELSE 'PENDIENTE' END,
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
