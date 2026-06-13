CREATE OR REPLACE PROCEDURE RH.SP_RH_CONCEPTOS_BY_PERSONA_GET (
    p_codigo_persona       IN NUMBER,
    p_desde                IN DATE,
    p_hasta                IN DATE,
    p_codigos_tipo_nomina  IN VARCHAR2,
    p_ResultSet            OUT SYS_REFCURSOR,
    p_Message              OUT VARCHAR2,
    p_TotalRecords         OUT NUMBER
) AS
    v_count NUMBER := 0;
BEGIN
    p_Message := 'Success';

    IF NVL(p_codigo_persona, 0) = 0 THEN
        SELECT COUNT(1)
          INTO p_TotalRecords
          FROM (
              SELECT DISTINCT h.CODIGO_TIPO_NOMINA, h.CODIGO
                FROM RH.RH_V_HISTORICO_MOVIMIENTOS h
               WHERE TRUNC(h.FECHA_NOMINA_MOV) BETWEEN TRUNC(p_desde) AND TRUNC(p_hasta)
                 AND (
                     p_codigos_tipo_nomina IS NULL
                     OR p_codigos_tipo_nomina = ''
                     OR INSTR(',' || p_codigos_tipo_nomina || ',', ',' || h.CODIGO_TIPO_NOMINA || ',') > 0
                 )
          );

        OPEN p_ResultSet FOR
            SELECT DISTINCT
                c.CODIGO_CONCEPTO,
                c.CODIGO,
                c.CODIGO_TIPO_NOMINA,
                tn.DESCRIPCION AS TIPO_NOMINA_DESCRIPCION,
                c.DENOMINACION,
                c.DESCRIPCION,
                c.TIPO_CONCEPTO,
                c.MODULO_ID,
                '' AS MODULO_DESCRIPCION,
                c.CODIGO_PUC,
                '' AS CODIGO_PUC_CONCAT,
                c.STATUS,
                c.FRECUENCIA_ID,
                '' AS FRECUENCIA_DESCRIPCION,
                c.DEDUSIBLE,
                c.AUTOMATICO,
                NVL(c.ID_MODELO_CALCULO, 0) AS ID_MODELO_CALCULO,
                NVL(c.EXTRA1, '') AS EXTRA1
            FROM RH.RH_CONCEPTOS c
            INNER JOIN RH.RH_TIPOS_NOMINA tn
                ON tn.CODIGO_TIPO_NOMINA = c.CODIGO_TIPO_NOMINA
            INNER JOIN (
                SELECT DISTINCT h.CODIGO_TIPO_NOMINA, h.CODIGO
                  FROM RH.RH_V_HISTORICO_MOVIMIENTOS h
                 WHERE TRUNC(h.FECHA_NOMINA_MOV) BETWEEN TRUNC(p_desde) AND TRUNC(p_hasta)
                   AND (
                       p_codigos_tipo_nomina IS NULL
                       OR p_codigos_tipo_nomina = ''
                       OR INSTR(',' || p_codigos_tipo_nomina || ',', ',' || h.CODIGO_TIPO_NOMINA || ',') > 0
                   )
            ) hist
                ON hist.CODIGO_TIPO_NOMINA = c.CODIGO_TIPO_NOMINA
               AND hist.CODIGO = c.CODIGO
            ORDER BY c.DENOMINACION;
    ELSE
        SELECT COUNT(1)
          INTO v_count
         FROM RH.RH_V_HISTORICO_MOVIMIENTOS h
         WHERE h.CODIGO_PERSONA = p_codigo_persona
           AND TRUNC(h.FECHA_NOMINA_MOV) BETWEEN TRUNC(p_desde) AND TRUNC(p_hasta)
           AND (
               p_codigos_tipo_nomina IS NULL
               OR p_codigos_tipo_nomina = ''
               OR INSTR(',' || p_codigos_tipo_nomina || ',', ',' || h.CODIGO_TIPO_NOMINA || ',') > 0
           );

        SELECT COUNT(1)
          INTO p_TotalRecords
          FROM (
              SELECT DISTINCT h.CODIGO_TIPO_NOMINA, h.CODIGO
                FROM RH.RH_V_HISTORICO_MOVIMIENTOS h
               WHERE (
                     h.CODIGO_PERSONA = p_codigo_persona
                     AND TRUNC(h.FECHA_NOMINA_MOV) BETWEEN TRUNC(p_desde) AND TRUNC(p_hasta)
                     AND (
                         p_codigos_tipo_nomina IS NULL
                         OR p_codigos_tipo_nomina = ''
                         OR INSTR(',' || p_codigos_tipo_nomina || ',', ',' || h.CODIGO_TIPO_NOMINA || ',') > 0
                     )
                 )
                  OR (
                     v_count = 0
                     AND h.CODIGO_PERSONA = p_codigo_persona
                     AND (
                         p_codigos_tipo_nomina IS NULL
                         OR p_codigos_tipo_nomina = ''
                         OR INSTR(',' || p_codigos_tipo_nomina || ',', ',' || h.CODIGO_TIPO_NOMINA || ',') > 0
                     )
                 )
          );

        OPEN p_ResultSet FOR
            SELECT DISTINCT
                c.CODIGO_CONCEPTO,
                c.CODIGO,
                c.CODIGO_TIPO_NOMINA,
                tn.DESCRIPCION AS TIPO_NOMINA_DESCRIPCION,
                c.DENOMINACION,
                c.DESCRIPCION,
                c.TIPO_CONCEPTO,
                c.MODULO_ID,
                '' AS MODULO_DESCRIPCION,
                c.CODIGO_PUC,
                '' AS CODIGO_PUC_CONCAT,
                c.STATUS,
                c.FRECUENCIA_ID,
                '' AS FRECUENCIA_DESCRIPCION,
                c.DEDUSIBLE,
                c.AUTOMATICO,
                NVL(c.ID_MODELO_CALCULO, 0) AS ID_MODELO_CALCULO,
                NVL(c.EXTRA1, '') AS EXTRA1
            FROM RH.RH_CONCEPTOS c
            INNER JOIN RH.RH_TIPOS_NOMINA tn
                ON tn.CODIGO_TIPO_NOMINA = c.CODIGO_TIPO_NOMINA
            INNER JOIN (
                SELECT DISTINCT h.CODIGO_TIPO_NOMINA, h.CODIGO
                  FROM RH.RH_V_HISTORICO_MOVIMIENTOS h
                 WHERE (
                       h.CODIGO_PERSONA = p_codigo_persona
                       AND TRUNC(h.FECHA_NOMINA_MOV) BETWEEN TRUNC(p_desde) AND TRUNC(p_hasta)
                       AND (
                           p_codigos_tipo_nomina IS NULL
                           OR p_codigos_tipo_nomina = ''
                           OR INSTR(',' || p_codigos_tipo_nomina || ',', ',' || h.CODIGO_TIPO_NOMINA || ',') > 0
                       )
                   )
                    OR (
                       v_count = 0
                       AND h.CODIGO_PERSONA = p_codigo_persona
                       AND (
                           p_codigos_tipo_nomina IS NULL
                           OR p_codigos_tipo_nomina = ''
                           OR INSTR(',' || p_codigos_tipo_nomina || ',', ',' || h.CODIGO_TIPO_NOMINA || ',') > 0
                       )
                   )
            ) hist
                ON hist.CODIGO_TIPO_NOMINA = c.CODIGO_TIPO_NOMINA
               AND hist.CODIGO = c.CODIGO
            ORDER BY c.DENOMINACION;
    END IF;
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SQLERRM;
        p_TotalRecords := 0;
        OPEN p_ResultSet FOR
            SELECT
                CAST(NULL AS NUMBER) CODIGO_CONCEPTO,
                CAST(NULL AS VARCHAR2(5)) CODIGO,
                CAST(NULL AS NUMBER) CODIGO_TIPO_NOMINA,
                CAST(NULL AS VARCHAR2(100)) TIPO_NOMINA_DESCRIPCION,
                CAST(NULL AS VARCHAR2(100)) DENOMINACION,
                CAST(NULL AS VARCHAR2(1000)) DESCRIPCION,
                CAST(NULL AS VARCHAR2(1)) TIPO_CONCEPTO,
                CAST(NULL AS NUMBER) MODULO_ID,
                CAST(NULL AS VARCHAR2(100)) MODULO_DESCRIPCION,
                CAST(NULL AS NUMBER) CODIGO_PUC,
                CAST(NULL AS VARCHAR2(100)) CODIGO_PUC_CONCAT,
                CAST(NULL AS VARCHAR2(1)) STATUS,
                CAST(NULL AS NUMBER) FRECUENCIA_ID,
                CAST(NULL AS VARCHAR2(100)) FRECUENCIA_DESCRIPCION,
                CAST(NULL AS NUMBER) DEDUSIBLE,
                CAST(NULL AS NUMBER) AUTOMATICO,
                CAST(NULL AS NUMBER) ID_MODELO_CALCULO,
                CAST(NULL AS VARCHAR2(100)) EXTRA1
            FROM DUAL
            WHERE 1 = 0;
END;
/
