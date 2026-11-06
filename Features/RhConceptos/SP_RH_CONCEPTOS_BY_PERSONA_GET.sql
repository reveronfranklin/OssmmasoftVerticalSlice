CREATE OR REPLACE PROCEDURE RH.SP_RH_CONCEPTOS_BY_PERSONA_GET (
    p_codigo_persona IN NUMBER, p_desde IN DATE, p_hasta IN DATE,
    p_codigos_tipo_nomina IN VARCHAR2,
    p_ResultSet OUT SYS_REFCURSOR, p_Message OUT VARCHAR2, p_TotalRecords OUT NUMBER,
    p_empresa IN NUMBER, p_sin_fecha IN NUMBER
) AS
BEGIN
    p_Message := 'success';
    SELECT COUNT(*) INTO p_TotalRecords FROM RH.RH_CONCEPTOS c
      INNER JOIN RH.RH_TIPOS_NOMINA tn ON tn.CODIGO_TIPO_NOMINA = c.CODIGO_TIPO_NOMINA
       AND tn.CODIGO_EMPRESA = p_empresa
     WHERE c.CODIGO_EMPRESA = p_empresa AND EXISTS (
        SELECT 1 FROM RH.RH_V_HISTORICO_MOVIMIENTOS h
         WHERE h.CODIGO_EMPRESA = p_empresa
               AND (NVL(p_codigo_persona, 0) = 0 OR h.CODIGO_PERSONA = p_codigo_persona)
               AND (NVL(p_sin_fecha, 0) = 1 OR
                    (h.FECHA_NOMINA_MOV >= TRUNC(p_desde) AND h.FECHA_NOMINA_MOV < TRUNC(p_hasta) + 1))
               AND (p_codigos_tipo_nomina IS NULL OR
                    INSTR(',' || p_codigos_tipo_nomina || ',', ',' || h.CODIGO_TIPO_NOMINA || ',') > 0)
           AND h.CODIGO_CONCEPTO = c.CODIGO_CONCEPTO
           AND h.CODIGO_TIPO_NOMINA = c.CODIGO_TIPO_NOMINA);
    OPEN p_ResultSet FOR SELECT
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
      INNER JOIN RH.RH_TIPOS_NOMINA tn ON tn.CODIGO_TIPO_NOMINA = c.CODIGO_TIPO_NOMINA
       AND tn.CODIGO_EMPRESA = p_empresa
     WHERE c.CODIGO_EMPRESA = p_empresa AND EXISTS (
        SELECT 1 FROM RH.RH_V_HISTORICO_MOVIMIENTOS h
         WHERE h.CODIGO_EMPRESA = p_empresa
               AND (NVL(p_codigo_persona, 0) = 0 OR h.CODIGO_PERSONA = p_codigo_persona)
               AND (NVL(p_sin_fecha, 0) = 1 OR
                    (h.FECHA_NOMINA_MOV >= TRUNC(p_desde) AND h.FECHA_NOMINA_MOV < TRUNC(p_hasta) + 1))
               AND (p_codigos_tipo_nomina IS NULL OR
                    INSTR(',' || p_codigos_tipo_nomina || ',', ',' || h.CODIGO_TIPO_NOMINA || ',') > 0)
           AND h.CODIGO_CONCEPTO = c.CODIGO_CONCEPTO
           AND h.CODIGO_TIPO_NOMINA = c.CODIGO_TIPO_NOMINA)
     ORDER BY c.DENOMINACION;
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SQLERRM; p_TotalRecords := 0;
        OPEN p_ResultSet FOR SELECT * FROM RH.RH_CONCEPTOS WHERE 1 = 0;
END;
/
