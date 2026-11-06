CREATE OR REPLACE PROCEDURE RH.SP_RH_TN_BY_PERSONA_GET (
    p_codigo_persona IN NUMBER, p_desde IN DATE, p_hasta IN DATE,
    p_ResultSet OUT SYS_REFCURSOR, p_Message OUT VARCHAR2, p_TotalRecords OUT NUMBER,
    p_empresa IN NUMBER, p_sin_fecha IN NUMBER
) AS
BEGIN
    p_Message := 'success';
    SELECT COUNT(*) INTO p_TotalRecords FROM RH.RH_TIPOS_NOMINA tn
     WHERE tn.CODIGO_EMPRESA = p_empresa AND EXISTS (
        SELECT 1 FROM RH.RH_V_HISTORICO_MOVIMIENTOS h WHERE h.CODIGO_EMPRESA = p_empresa
           AND (NVL(p_codigo_persona, 0) = 0 OR h.CODIGO_PERSONA = p_codigo_persona)
           AND (NVL(p_sin_fecha, 0) = 1 OR
                (h.FECHA_NOMINA_MOV >= TRUNC(p_desde) AND h.FECHA_NOMINA_MOV < TRUNC(p_hasta) + 1))
           AND h.CODIGO_TIPO_NOMINA = tn.CODIGO_TIPO_NOMINA);
    OPEN p_ResultSet FOR SELECT
                tn.CODIGO_TIPO_NOMINA,
                tn.DESCRIPCION,
                tn.SIGLAS_TIPO_NOMINA,
                tn.FRECUENCIA_PAGO_ID,
                '' AS FRECUENCIA_PAGO,
                tn.SUELDO_MINIMO
      FROM RH.RH_TIPOS_NOMINA tn
     WHERE tn.CODIGO_EMPRESA = p_empresa AND EXISTS (
        SELECT 1 FROM RH.RH_V_HISTORICO_MOVIMIENTOS h WHERE h.CODIGO_EMPRESA = p_empresa
           AND (NVL(p_codigo_persona, 0) = 0 OR h.CODIGO_PERSONA = p_codigo_persona)
           AND (NVL(p_sin_fecha, 0) = 1 OR
                (h.FECHA_NOMINA_MOV >= TRUNC(p_desde) AND h.FECHA_NOMINA_MOV < TRUNC(p_hasta) + 1))
           AND h.CODIGO_TIPO_NOMINA = tn.CODIGO_TIPO_NOMINA)
     ORDER BY tn.DESCRIPCION;
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SQLERRM; p_TotalRecords := 0;
        OPEN p_ResultSet FOR SELECT * FROM RH.RH_TIPOS_NOMINA WHERE 1 = 0;
END;
/
