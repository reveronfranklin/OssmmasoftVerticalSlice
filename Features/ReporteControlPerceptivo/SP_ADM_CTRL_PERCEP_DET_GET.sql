-- Reporte Control Perceptivo (ADM) - lineas de detalle.
-- Reconstruido por ingenieria inversa de ADM_CONTROL_PERCEPTIVO.rdf (Oracle
-- Reports). Ver Requerimientos/15 - Reporte Control Perceptivo/README.md.
-- PENDIENTE DE VALIDACION contra datos reales (Fase 1 del PLAN.md de ese
-- requerimiento): no se ejecuto contra un Oracle real en esta sesion.
--
-- Igual que SP_ADM_CTRL_PERCEP_HDR_GET, prueba las 3 fuentes posibles de
-- p_CodigoCompromiso por UNION ALL: solo una de ellas debe producir filas
-- para un compromiso/contrato dado.
CREATE OR REPLACE PROCEDURE ADM.SP_ADM_CTRL_PERCEP_DET_GET (
    p_CodigoCompromiso IN NUMBER,
    p_ResultSet         OUT SYS_REFCURSOR,
    p_Message           OUT VARCHAR2,
    p_TotalRecords      OUT NUMBER
) AS
BEGIN
    SELECT COUNT(*)
      INTO p_TotalRecords
      FROM (
            SELECT B.CANTIDAD
              FROM ADM.ADM_COMPROMISOS A
              JOIN ADM.ADM_SOL_COMPROMISO D
                ON A.CODIGO_SOLICITUD = D.CODIGO_SOL_COMPROMISO
              JOIN ADM.ADM_PUC_SOL_COMPROMISO C
                ON D.CODIGO_SOL_COMPROMISO = C.CODIGO_SOLICITUD
              LEFT JOIN ADM.ADM_DETALLE_SOL_COMPROMISO B
                ON C.CODIGO_PUC_SOLICITUD = B.CODIGO_PUC_SOLICITUD
             WHERE A.CODIGO_COMPROMISO = p_CodigoCompromiso
            UNION ALL
            SELECT B.CANTIDAD
              FROM PRE.PRE_COMPROMISOS A
              JOIN ADM.ADM_SOLICITUDES D
                ON A.CODIGO_SOLICITUD = D.CODIGO_SOLICITUD
              JOIN ADM.ADM_DETALLE_SOLICITUD B
                ON D.CODIGO_SOLICITUD = B.CODIGO_SOLICITUD
             WHERE A.CODIGO_COMPROMISO = p_CodigoCompromiso
            UNION ALL
            SELECT TO_NUMBER(NULL) CANTIDAD
              FROM ADM.ADM_CONTRATOS A
             WHERE A.CODIGO_CONTRATO = p_CodigoCompromiso
      );

    OPEN p_ResultSet FOR
        -- Rama 1: lineas de la solicitud de compromiso (via ADM_PUC_SOL_COMPROMISO).
        SELECT
            B.CANTIDAD,
            NVL(I.DESCRIPCION, '') UDM,
            NVL(B.DENOMINACION, '') DESCRIPCION_ARTICULO,
            NVL(B.PRECIO_UNITARIO, 0) PRECIO_UNITARIO,
            NVL(B.CANTIDAD, 0) * NVL(B.PRECIO_UNITARIO, 0) PRECIO,
            NVL(TO_NUMBER(J.EXTRA1, '999D99', 'NLS_NUMERIC_CHARACTERS=''.,'''), 0) POR_IMPUESTO,
            0 MONTO_IMPUESTO
          FROM ADM.ADM_COMPROMISOS A
          JOIN ADM.ADM_SOL_COMPROMISO D
            ON A.CODIGO_SOLICITUD = D.CODIGO_SOL_COMPROMISO
          JOIN ADM.ADM_PUC_SOL_COMPROMISO C
            ON D.CODIGO_SOL_COMPROMISO = C.CODIGO_SOLICITUD
          LEFT JOIN ADM.ADM_DETALLE_SOL_COMPROMISO B
            ON C.CODIGO_PUC_SOLICITUD = B.CODIGO_PUC_SOLICITUD
          LEFT JOIN ADM.ADM_DESCRIPTIVAS I
            ON I.DESCRIPCION_ID = B.UDM_ID
          LEFT JOIN ADM.ADM_DESCRIPTIVAS J
            ON J.DESCRIPCION_ID = B.TIPO_IMPUESTO_ID
         WHERE A.CODIGO_COMPROMISO = p_CodigoCompromiso
        UNION ALL
        -- Rama 2: lineas de la solicitud generica (sin PUC), impuesto calculado en linea.
        SELECT
            B.CANTIDAD,
            NVL(I.DESCRIPCION, '') UDM,
            NVL(B.DESCRIPCION, '') DESCRIPCION_ARTICULO,
            NVL(B.PRECIO_UNITARIO, 0) PRECIO_UNITARIO,
            NVL(B.CANTIDAD, 0) * NVL(B.PRECIO_UNITARIO, 0) PRECIO,
            NVL(TO_NUMBER(J.EXTRA1, '999D99', 'NLS_NUMERIC_CHARACTERS=''.,'''), 0) POR_IMPUESTO,
            (NVL(B.CANTIDAD, 0) * NVL(B.PRECIO_UNITARIO, 0))
                * (NVL(TO_NUMBER(J.EXTRA1, '999D99', 'NLS_NUMERIC_CHARACTERS=''.,'''), 0) / 100) MONTO_IMPUESTO
          FROM PRE.PRE_COMPROMISOS A
          JOIN ADM.ADM_SOLICITUDES D
            ON A.CODIGO_SOLICITUD = D.CODIGO_SOLICITUD
          JOIN ADM.ADM_DETALLE_SOLICITUD B
            ON D.CODIGO_SOLICITUD = B.CODIGO_SOLICITUD
          LEFT JOIN ADM.ADM_DESCRIPTIVAS I
            ON I.DESCRIPCION_ID = B.UDM_ID
          LEFT JOIN ADM.ADM_DESCRIPTIVAS J
            ON J.DESCRIPCION_ID = B.TIPO_IMPUESTO_ID
         WHERE A.CODIGO_COMPROMISO = p_CodigoCompromiso
        UNION ALL
        -- Rama 3: contrato, una sola linea sintetica con el monto total.
        SELECT
            TO_NUMBER(NULL) CANTIDAD,
            CAST(NULL AS VARCHAR2(200)) UDM,
            TRIM(NVL(A.OBRA, '') || ' ' || NVL(A.DESCRIPCION, '')) DESCRIPCION_ARTICULO,
            NVL(A.MONTO_CONTRATO, 0) PRECIO_UNITARIO,
            NVL(A.MONTO_CONTRATO, 0) PRECIO,
            CAST(NULL AS NUMBER) POR_IMPUESTO,
            0 MONTO_IMPUESTO
          FROM ADM.ADM_CONTRATOS A
         WHERE A.CODIGO_CONTRATO = p_CodigoCompromiso;

    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SUBSTR(SQLERRM, 1, 4000);
        p_TotalRecords := 0;
        OPEN p_ResultSet FOR
            SELECT
                CAST(NULL AS NUMBER) CANTIDAD,
                CAST(NULL AS VARCHAR2(200)) UDM,
                CAST(NULL AS VARCHAR2(4000)) DESCRIPCION_ARTICULO,
                CAST(NULL AS NUMBER) PRECIO_UNITARIO,
                CAST(NULL AS NUMBER) PRECIO,
                CAST(NULL AS NUMBER) POR_IMPUESTO,
                CAST(NULL AS NUMBER) MONTO_IMPUESTO
              FROM DUAL
             WHERE 1 = 0;
END SP_ADM_CTRL_PERCEP_DET_GET;
/
