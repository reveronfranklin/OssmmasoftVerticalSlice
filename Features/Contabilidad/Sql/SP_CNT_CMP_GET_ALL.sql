CREATE OR REPLACE PROCEDURE CNT.SP_CNT_CMP_GET_ALL (
    p_PageSize            IN NUMBER,
    p_PageNumber          IN NUMBER,
    p_SearchText          IN VARCHAR2,
    p_CODIGO_PERIODO      IN NUMBER,
    p_ORIGEN_ID           IN NUMBER,
    p_FECHA_DESDE         IN DATE,
    p_FECHA_HASTA         IN DATE,
    p_ES_AUTOMATICO       IN NUMBER,
    p_CODIGO_EMPRESA      IN NUMBER,
    p_ResultSet           OUT SYS_REFCURSOR,
    p_Message             OUT VARCHAR2,
    p_TotalRecords        OUT NUMBER,
    p_TotalPages          OUT NUMBER,
    p_ESTADO_CMP          IN VARCHAR2 DEFAULT NULL
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
       AND (p_ESTADO_CMP IS NULL OR c.ESTADO_CMP = p_ESTADO_CMP)
       AND (p_CODIGO_PERIODO IS NULL OR c.CODIGO_PERIODO = p_CODIGO_PERIODO)
       AND (p_ORIGEN_ID IS NULL OR c.ORIGEN_ID = p_ORIGEN_ID)
       AND (p_FECHA_DESDE IS NULL OR c.FECHA_COMPROBANTE >= p_FECHA_DESDE)
       AND (p_FECHA_HASTA IS NULL OR c.FECHA_COMPROBANTE < p_FECHA_HASTA + 1)
       AND (p_ES_AUTOMATICO IS NULL
            OR (p_ES_AUTOMATICO = 1 AND NVL(c.EXTRA1, ' ') = 'AUTOMATICO')
            OR (p_ES_AUTOMATICO = 0 AND NVL(c.EXTRA1, ' ') <> 'AUTOMATICO'))
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
                               c.ESTADO_CMP, c.AJUSTE_MANUAL, c.VERSION_CMP,
                               c.CODIGO_EMPRESA
                          FROM CNT.CNT_COMPROBANTES c
                          LEFT JOIN CNT.CNT_PERIODOS p ON p.CODIGO_PERIODO = c.CODIGO_PERIODO
                          LEFT JOIN CNT.CNT_DESCRIPTIVAS t ON t.DESCRIPCION_ID = c.TIPO_COMPROBANTE_ID
                          LEFT JOIN CNT.CNT_DESCRIPTIVAS o ON o.DESCRIPCION_ID = c.ORIGEN_ID
                          LEFT JOIN (
                                SELECT CODIGO_COMPROBANTE,
                                       SUM(CASE WHEN MONTO > 0 THEN MONTO ELSE 0 END) AS TOTAL_DEBE,
                                       SUM(CASE WHEN MONTO < 0 THEN -MONTO ELSE 0 END) AS TOTAL_HABER
                                  FROM CNT.CNT_DETALLE_COMPROBANTE
                                 WHERE CODIGO_EMPRESA = p_CODIGO_EMPRESA
                                 GROUP BY CODIGO_COMPROBANTE
                          ) d ON d.CODIGO_COMPROBANTE = c.CODIGO_COMPROBANTE
                         WHERE c.CODIGO_EMPRESA = p_CODIGO_EMPRESA
       AND (p_ESTADO_CMP IS NULL OR c.ESTADO_CMP = p_ESTADO_CMP)
                           AND (p_CODIGO_PERIODO IS NULL OR c.CODIGO_PERIODO = p_CODIGO_PERIODO)
                           AND (p_ORIGEN_ID IS NULL OR c.ORIGEN_ID = p_ORIGEN_ID)
                           AND (p_FECHA_DESDE IS NULL OR c.FECHA_COMPROBANTE >= p_FECHA_DESDE)
                           AND (p_FECHA_HASTA IS NULL OR c.FECHA_COMPROBANTE < p_FECHA_HASTA + 1)
                           AND (p_ES_AUTOMATICO IS NULL
                                OR (p_ES_AUTOMATICO = 1 AND NVL(c.EXTRA1, ' ') = 'AUTOMATICO')
                                OR (p_ES_AUTOMATICO = 0 AND NVL(c.EXTRA1, ' ') <> 'AUTOMATICO'))
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
