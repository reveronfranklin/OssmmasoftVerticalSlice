CREATE OR REPLACE PROCEDURE RH.SP_RH_TN_BY_PERSONA_GET (
    p_codigo_persona IN NUMBER,
    p_desde          IN DATE,
    p_hasta          IN DATE,
    p_ResultSet      OUT SYS_REFCURSOR,
    p_Message        OUT VARCHAR2,
    p_TotalRecords   OUT NUMBER
) AS
BEGIN
    p_Message := 'Success';

    IF NVL(p_codigo_persona, 0) = 0 THEN
        SELECT COUNT(1)
          INTO p_TotalRecords
          FROM (
              SELECT DISTINCT h.CODIGO_TIPO_NOMINA
                FROM RH.RH_V_HISTORICO_MOVIMIENTOS h
               WHERE TRUNC(h.FECHA_NOMINA_MOV) BETWEEN TRUNC(p_desde) AND TRUNC(p_hasta)
          );

        OPEN p_ResultSet FOR
            SELECT DISTINCT
                tn.CODIGO_TIPO_NOMINA,
                tn.DESCRIPCION,
                tn.SIGLAS_TIPO_NOMINA,
                tn.FRECUENCIA_PAGO_ID,
                '' AS FRECUENCIA_PAGO,
                tn.SUELDO_MINIMO
            FROM RH.RH_TIPOS_NOMINA tn
            INNER JOIN (
                SELECT DISTINCT h.CODIGO_TIPO_NOMINA
                  FROM RH.RH_V_HISTORICO_MOVIMIENTOS h
                 WHERE TRUNC(h.FECHA_NOMINA_MOV) BETWEEN TRUNC(p_desde) AND TRUNC(p_hasta)
            ) hist ON hist.CODIGO_TIPO_NOMINA = tn.CODIGO_TIPO_NOMINA
            ORDER BY tn.DESCRIPCION;
    ELSE
        SELECT COUNT(1)
          INTO p_TotalRecords
          FROM (
              SELECT DISTINCT h.CODIGO_TIPO_NOMINA
                FROM RH.RH_V_HISTORICO_MOVIMIENTOS h
               WHERE h.CODIGO_PERSONA = p_codigo_persona
                 AND TRUNC(h.FECHA_NOMINA_MOV) BETWEEN TRUNC(p_desde) AND TRUNC(p_hasta)
          );

        OPEN p_ResultSet FOR
            SELECT DISTINCT
                tn.CODIGO_TIPO_NOMINA,
                tn.DESCRIPCION,
                tn.SIGLAS_TIPO_NOMINA,
                tn.FRECUENCIA_PAGO_ID,
                '' AS FRECUENCIA_PAGO,
                tn.SUELDO_MINIMO
            FROM RH.RH_TIPOS_NOMINA tn
            INNER JOIN (
                SELECT DISTINCT h.CODIGO_TIPO_NOMINA
                  FROM RH.RH_V_HISTORICO_MOVIMIENTOS h
                 WHERE h.CODIGO_PERSONA = p_codigo_persona
                   AND TRUNC(h.FECHA_NOMINA_MOV) BETWEEN TRUNC(p_desde) AND TRUNC(p_hasta)
            ) hist ON hist.CODIGO_TIPO_NOMINA = tn.CODIGO_TIPO_NOMINA
            ORDER BY tn.DESCRIPCION;
    END IF;
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SQLERRM;
        p_TotalRecords := 0;
        OPEN p_ResultSet FOR
            SELECT
                CAST(NULL AS NUMBER) CODIGO_TIPO_NOMINA,
                CAST(NULL AS VARCHAR2(100)) DESCRIPCION,
                CAST(NULL AS VARCHAR2(3)) SIGLAS_TIPO_NOMINA,
                CAST(NULL AS NUMBER) FRECUENCIA_PAGO_ID,
                CAST(NULL AS VARCHAR2(100)) FRECUENCIA_PAGO,
                CAST(NULL AS NUMBER) SUELDO_MINIMO
            FROM DUAL
            WHERE 1 = 0;
END;
/
