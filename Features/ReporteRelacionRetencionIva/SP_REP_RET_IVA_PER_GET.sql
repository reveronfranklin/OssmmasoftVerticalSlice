-- =============================================================================
-- ADM - Relacion de Retenciones de IVA por periodos de Orden de Pago.
--
-- Migracion del reporte Oracle Reports ADM_RELACION_RETENCION_IVA_OP2.rdf
-- (requerimiento 22). Devuelve una fila por documento (factura / nota) de cada
-- comprobante de retencion emitido en el rango de fechas; el agrupamiento por
-- comprobante y el total de cierre los arma el generador de PDF en C#.
--
-- LAS DOS RAMAS DEL UNION ALL SON LAS DEL REPORTE LEGADO, tal cual:
--
--   Rama 1 - Comprobante "directo" de la orden de pago. Aplica cuando el
--            NUMERO_COMPROBANTE de la OP no tiene ninguna fila en
--            ADM_COMPROBANTES_DOCUMENTOS_OP. La cabecera (comprobante, fecha,
--            proveedor) sale de ADM_ORDEN_PAGO.
--   Rama 2 - Comprobante consolidado. La cabecera sale de
--            ADM_COMPROBANTES_DOCUMENTOS_OP (su propio numero, su propia fecha
--            y su propio proveedor) y los documentos se enlazan por
--            CODIGO_DOCUMENTO_OP.
--
-- No se fusionan en una sola consulta con outer join: la rama 1 excluye por
-- NUMERO_COMPROBANTE y la rama 2 enlaza por CODIGO_DOCUMENTO_OP, asi que un
-- documento sin fila en ACDO cuyo comprobante si esta en ACDO no aparece en
-- ninguna de las dos. Fusionarlas lo haria aparecer, y eso cambiaria los
-- totales impresos respecto del reporte legado.
--
-- DIVERGENCIAS DELIBERADAS respecto del .rdf, todas documentadas en
-- Requerimientos/22 - RelacionporPeriodosdeOPRetenciondelIVA_v2/PLAN.md:
--
--   1. Se filtra por AOP.CODIGO_EMPRESA. El query legado no lo hacia (solo
--      pasaba la empresa a sus funciones auxiliares), lo que en un schema
--      multiempresa devuelve filas de otras empresas.
--   2. ADM_F_TIPO_DOCUMENTO y ADM_F_DESCRIPTIVAS_ID se reemplazan por joins
--      directos a ADM_DESCRIPTIVAS. Las dos funciones hacen exactamente eso
--      -ADM_F_DESCRIPTIVAS_ID es un EXECUTE IMMEDIATE por fila, y
--      ADM_F_TIPO_DOCUMENTO devuelve el valor solo si EXTRA2 coincide con el
--      grupo pedido-, asi que el resultado es identico sin SQL dinamico por
--      fila.
--   3. NUMERO_OPERACION usa ROW_NUMBER() en vez de ROWNUM. En un UNION ALL con
--      ORDER BY, ROWNUM se asigna antes de ordenar y produce una numeracion que
--      no corresponde a lo impreso.
--   4. El rango de fechas se evalua como >= TRUNC(desde) y < TRUNC(hasta)+1,
--      que es equivalente al BETWEEN del legado incluso si FECHA_INS lleva hora,
--      y permite usar el indice sobre la columna.
--
-- Se descarta la consulta de totales del .rdf (Q_IVA_TOTALES / F_TOTAL): suma
-- MONTO_RETENIDO con un predicado heredado de un reporte de cheques
-- (2 >= COUNT de ADM_BENEFICIARIOS_CH ...) que no guarda relacion con lo
-- impreso y haria que el TOTAL no cuadre con las filas del detalle. El total lo
-- suma el generador sobre MONTO_RETENIDO_NETO, que es la columna DECODE que el
-- propio reporte legado calculaba para eso.
-- =============================================================================
CREATE OR REPLACE PROCEDURE ADM.SP_REP_RET_IVA_PER_GET (
    p_CodigoEmpresa IN  NUMBER,
    p_FechaDesde    IN  DATE,
    p_FechaHasta    IN  DATE,
    p_Estatus       IN  VARCHAR2 DEFAULT NULL,
    p_ResultSet     OUT SYS_REFCURSOR,
    p_Message       OUT VARCHAR2,
    p_TotalRecords  OUT NUMBER
) AS
    v_desde DATE := TRUNC(p_FechaDesde);
    v_hasta DATE := TRUNC(p_FechaHasta) + 1;
BEGIN
    SELECT COUNT(*)
      INTO p_TotalRecords
      FROM (
            SELECT 1 FILA
              FROM ADM.ADM_ORDEN_PAGO AOP
                  ,ADM.ADM_PROVEEDORES AP
                  ,ADM.ADM_DOCUMENTOS_OP ADO
             WHERE AOP.CODIGO_PROVEEDOR = AP.CODIGO_PROVEEDOR
               AND AOP.CODIGO_ORDEN_PAGO = ADO.CODIGO_ORDEN_PAGO
               AND AOP.CODIGO_EMPRESA = p_CodigoEmpresa
               AND AOP.FECHA_INS >= v_desde
               AND AOP.FECHA_INS <  v_hasta
               AND AOP.NUMERO_COMPROBANTE IS NOT NULL
               AND (p_Estatus IS NULL OR AOP.STATUS = p_Estatus)
               AND NOT EXISTS (SELECT 'x'
                                 FROM ADM.ADM_COMPROBANTES_DOCUMENTOS_OP ACDO
                                WHERE ACDO.NUMERO_COMPROBANTE = AOP.NUMERO_COMPROBANTE)
            UNION ALL
            SELECT 1 FILA
              FROM ADM.ADM_ORDEN_PAGO AOP
                  ,ADM.ADM_PROVEEDORES AP
                  ,ADM.ADM_DOCUMENTOS_OP ADO
                  ,ADM.ADM_COMPROBANTES_DOCUMENTOS_OP ACDO
             WHERE AOP.CODIGO_ORDEN_PAGO = ADO.CODIGO_ORDEN_PAGO
               AND ACDO.CODIGO_PROVEEDOR = AP.CODIGO_PROVEEDOR
               AND ACDO.CODIGO_DOCUMENTO_OP = ADO.CODIGO_DOCUMENTO_OP
               AND AOP.CODIGO_EMPRESA = p_CodigoEmpresa
               AND AOP.FECHA_INS >= v_desde
               AND AOP.FECHA_INS <  v_hasta
               AND AOP.NUMERO_COMPROBANTE IS NOT NULL
               AND (p_Estatus IS NULL OR AOP.STATUS = p_Estatus)
           );

    OPEN p_ResultSet FOR
        SELECT ROW_NUMBER() OVER (ORDER BY Q.NUMERO_COMPROBANTE,
                                           Q.FECHA_DOCUMENTO,
                                           Q.NUMERO_DOCUMENTO) NUMERO_OPERACION,
               Q.NUMERO_COMPROBANTE,
               Q.FECHA_COMPROBANTE,
               Q.NUMERO_ORDEN_PAGO,
               Q.STATUS,
               Q.ESTATUS_DESC,
               Q.NOMBRE_PROVEEDOR,
               Q.RIF_PROVEEDOR,
               Q.FECHA_DOCUMENTO,
               Q.NUMERO_DOCUMENTO,
               Q.NUMERO_FACTURA,
               Q.MONTO_DOCUMENTO,
               Q.MONTO_IMPUESTO_EXENTO,
               Q.BASE_IMPONIBLE,
               Q.ALICUOTA,
               Q.MONTO_IMPUESTO,
               Q.MONTO_RETENIDO,
               Q.MONTO_RETENIDO_NETO
          FROM (
                -- ------------------------------------------------------------
                -- Rama 1: comprobante propio de la orden de pago
                -- ------------------------------------------------------------
                SELECT TO_CHAR(AOP.NUMERO_COMPROBANTE)          NUMERO_COMPROBANTE,
                       AOP.FECHA_INS                            FECHA_COMPROBANTE,
                       AOP.NUMERO_ORDEN_PAGO                    NUMERO_ORDEN_PAGO,
                       NVL(AOP.STATUS, ' ')                     STATUS,
                       DECODE(AOP.STATUS, 'AP', 'APROBADO',
                                          'PE', 'PENDIENTE',
                                          'AN', 'ANULADO',
                                          NVL(AOP.STATUS, ' ')) ESTATUS_DESC,
                       NVL(AP.NOMBRE_PROVEEDOR, ' ')            NOMBRE_PROVEEDOR,
                       RPAD(NVL(REPLACE(AP.RIF, '-'), '0'), 10) RIF_PROVEEDOR,
                       ADO.FECHA_DOCUMENTO                      FECHA_DOCUMENTO,
                       NVL(ADO.NUMERO_DOCUMENTO, ' ')           NUMERO_DOCUMENTO,
                       CASE WHEN TDOC.EXTRA2 = 'FACTURA'
                            THEN NVL(ADO.NUMERO_DOCUMENTO, ' ')
                            ELSE ' '
                       END                                      NUMERO_FACTURA,
                       NVL(ADO.MONTO_DOCUMENTO, 0)              MONTO_DOCUMENTO,
                       NVL(ADO.MONTO_IMPUESTO_EXENTO, 0)        MONTO_IMPUESTO_EXENTO,
                       NVL(ADO.BASE_IMPONIBLE, 0)               BASE_IMPONIBLE,
                       NVL(TIMP.EXTRA1, '0') || '%'             ALICUOTA,
                       NVL(ADO.MONTO_IMPUESTO, 0)               MONTO_IMPUESTO,
                       NVL(ADO.MONTO_RETENIDO, 0)               MONTO_RETENIDO,
                       DECODE(AOP.STATUS,
                              'AN', -1 * NVL(ADO.MONTO_RETENIDO, 0),
                                    NVL(ADO.MONTO_RETENIDO, 0)) MONTO_RETENIDO_NETO
                  FROM ADM.ADM_ORDEN_PAGO AOP
                  JOIN ADM.ADM_PROVEEDORES AP
                    ON AP.CODIGO_PROVEEDOR = AOP.CODIGO_PROVEEDOR
                  JOIN ADM.ADM_DOCUMENTOS_OP ADO
                    ON ADO.CODIGO_ORDEN_PAGO = AOP.CODIGO_ORDEN_PAGO
                  LEFT JOIN ADM.ADM_DESCRIPTIVAS TDOC
                    ON TDOC.DESCRIPCION_ID = ADO.TIPO_DOCUMENTO_ID
                   AND TDOC.CODIGO_EMPRESA = AOP.CODIGO_EMPRESA
                  LEFT JOIN ADM.ADM_DESCRIPTIVAS TIMP
                    ON TIMP.DESCRIPCION_ID = ADO.TIPO_IMPUESTO_ID
                   AND TIMP.CODIGO_EMPRESA = AOP.CODIGO_EMPRESA
                 WHERE AOP.CODIGO_EMPRESA = p_CodigoEmpresa
                   AND AOP.FECHA_INS >= v_desde
                   AND AOP.FECHA_INS <  v_hasta
                   AND AOP.NUMERO_COMPROBANTE IS NOT NULL
                   AND (p_Estatus IS NULL OR AOP.STATUS = p_Estatus)
                   AND NOT EXISTS (SELECT 'x'
                                     FROM ADM.ADM_COMPROBANTES_DOCUMENTOS_OP ACDO
                                    WHERE ACDO.NUMERO_COMPROBANTE = AOP.NUMERO_COMPROBANTE)
                UNION ALL
                -- ------------------------------------------------------------
                -- Rama 2: comprobante consolidado en
                -- ADM_COMPROBANTES_DOCUMENTOS_OP
                -- ------------------------------------------------------------
                SELECT TO_CHAR(ACDO.NUMERO_COMPROBANTE)         NUMERO_COMPROBANTE,
                       ACDO.FECHA_INS                           FECHA_COMPROBANTE,
                       AOP.NUMERO_ORDEN_PAGO                    NUMERO_ORDEN_PAGO,
                       NVL(AOP.STATUS, ' ')                     STATUS,
                       DECODE(AOP.STATUS, 'AP', 'APROBADO',
                                          'PE', 'PENDIENTE',
                                          'AN', 'ANULADO',
                                          NVL(AOP.STATUS, ' ')) ESTATUS_DESC,
                       NVL(AP.NOMBRE_PROVEEDOR, ' ')            NOMBRE_PROVEEDOR,
                       RPAD(NVL(REPLACE(AP.RIF, '-'), '0'), 10) RIF_PROVEEDOR,
                       ADO.FECHA_DOCUMENTO                      FECHA_DOCUMENTO,
                       NVL(ADO.NUMERO_DOCUMENTO, ' ')           NUMERO_DOCUMENTO,
                       CASE WHEN TDOC.EXTRA2 = 'FACTURA'
                            THEN NVL(ADO.NUMERO_DOCUMENTO, ' ')
                            ELSE ' '
                       END                                      NUMERO_FACTURA,
                       NVL(ADO.MONTO_DOCUMENTO, 0)              MONTO_DOCUMENTO,
                       NVL(ADO.MONTO_IMPUESTO_EXENTO, 0)        MONTO_IMPUESTO_EXENTO,
                       NVL(ADO.BASE_IMPONIBLE, 0)               BASE_IMPONIBLE,
                       NVL(TIMP.EXTRA1, '0') || '%'             ALICUOTA,
                       NVL(ADO.MONTO_IMPUESTO, 0)               MONTO_IMPUESTO,
                       NVL(ADO.MONTO_RETENIDO, 0)               MONTO_RETENIDO,
                       DECODE(AOP.STATUS,
                              'AN', -1 * NVL(ADO.MONTO_RETENIDO, 0),
                                    NVL(ADO.MONTO_RETENIDO, 0)) MONTO_RETENIDO_NETO
                  FROM ADM.ADM_ORDEN_PAGO AOP
                  JOIN ADM.ADM_DOCUMENTOS_OP ADO
                    ON ADO.CODIGO_ORDEN_PAGO = AOP.CODIGO_ORDEN_PAGO
                  JOIN ADM.ADM_COMPROBANTES_DOCUMENTOS_OP ACDO
                    ON ACDO.CODIGO_DOCUMENTO_OP = ADO.CODIGO_DOCUMENTO_OP
                  JOIN ADM.ADM_PROVEEDORES AP
                    ON AP.CODIGO_PROVEEDOR = ACDO.CODIGO_PROVEEDOR
                  LEFT JOIN ADM.ADM_DESCRIPTIVAS TDOC
                    ON TDOC.DESCRIPCION_ID = ADO.TIPO_DOCUMENTO_ID
                   AND TDOC.CODIGO_EMPRESA = AOP.CODIGO_EMPRESA
                  LEFT JOIN ADM.ADM_DESCRIPTIVAS TIMP
                    ON TIMP.DESCRIPCION_ID = ADO.TIPO_IMPUESTO_ID
                   AND TIMP.CODIGO_EMPRESA = AOP.CODIGO_EMPRESA
                 WHERE AOP.CODIGO_EMPRESA = p_CodigoEmpresa
                   AND AOP.FECHA_INS >= v_desde
                   AND AOP.FECHA_INS <  v_hasta
                   AND AOP.NUMERO_COMPROBANTE IS NOT NULL
                   AND (p_Estatus IS NULL OR AOP.STATUS = p_Estatus)
               ) Q
         ORDER BY Q.NUMERO_COMPROBANTE, Q.FECHA_DOCUMENTO, Q.NUMERO_DOCUMENTO;

    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SUBSTR(SQLERRM, 1, 4000);
        p_TotalRecords := 0;
        OPEN p_ResultSet FOR
            SELECT CAST(NULL AS NUMBER)          NUMERO_OPERACION,
                   CAST(NULL AS VARCHAR2(100))   NUMERO_COMPROBANTE,
                   CAST(NULL AS DATE)            FECHA_COMPROBANTE,
                   CAST(NULL AS VARCHAR2(100))   NUMERO_ORDEN_PAGO,
                   CAST(NULL AS VARCHAR2(10))    STATUS,
                   CAST(NULL AS VARCHAR2(30))    ESTATUS_DESC,
                   CAST(NULL AS VARCHAR2(4000))  NOMBRE_PROVEEDOR,
                   CAST(NULL AS VARCHAR2(30))    RIF_PROVEEDOR,
                   CAST(NULL AS DATE)            FECHA_DOCUMENTO,
                   CAST(NULL AS VARCHAR2(100))   NUMERO_DOCUMENTO,
                   CAST(NULL AS VARCHAR2(100))   NUMERO_FACTURA,
                   CAST(NULL AS NUMBER)          MONTO_DOCUMENTO,
                   CAST(NULL AS NUMBER)          MONTO_IMPUESTO_EXENTO,
                   CAST(NULL AS NUMBER)          BASE_IMPONIBLE,
                   CAST(NULL AS VARCHAR2(30))    ALICUOTA,
                   CAST(NULL AS NUMBER)          MONTO_IMPUESTO,
                   CAST(NULL AS NUMBER)          MONTO_RETENIDO,
                   CAST(NULL AS NUMBER)          MONTO_RETENIDO_NETO
              FROM DUAL
             WHERE 1 = 0;
END SP_REP_RET_IVA_PER_GET;
/
