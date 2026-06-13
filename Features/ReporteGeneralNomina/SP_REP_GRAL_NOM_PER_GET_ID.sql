CREATE OR REPLACE PROCEDURE RH.SP_REP_GRAL_NOM_PER_GET_ID (
    p_codigo_periodo IN NUMBER,
    p_ResultSet      OUT SYS_REFCURSOR,
    p_Message        OUT VARCHAR2,
    p_TotalRecords   OUT NUMBER
) AS
BEGIN
    p_TotalRecords := 0;

    OPEN p_ResultSet FOR
        SELECT
            rp.CODIGO_PERIODO,
            rp.DESCRIPCION,
            rp.CODIGO_TIPO_NOMINA,
            rtn.DESCRIPCION AS DESCRIPCION_TIPO_NOMINA,
            rp.FECHA_NOMINA,
            rp.PERIODO,
            CASE
                WHEN rp.PERIODO = 1 THEN '1ra. Quincena'
                WHEN rp.PERIODO = 2 THEN '2da. Quincena'
                ELSE 'Periodo no definido'
            END AS DESCRIPCION_PERIODO,
            rp.TIPO_NOMINA,
            CASE
                WHEN rp.TIPO_NOMINA = 'E' THEN 'ESPECIAL'
                WHEN rp.TIPO_NOMINA = 'N' THEN 'NORMAL'
                ELSE 'Tipo no definido'
            END AS TIPO_NOMINA_DESCRIPCION
        FROM RH.RH_PERIODOS rp
        INNER JOIN RH.RH_TIPOS_NOMINA rtn
            ON rp.CODIGO_TIPO_NOMINA = rtn.CODIGO_TIPO_NOMINA
        WHERE rp.CODIGO_PERIODO = p_codigo_periodo;

    SELECT COUNT(*)
      INTO p_TotalRecords
      FROM RH.RH_PERIODOS rp
      INNER JOIN RH.RH_TIPOS_NOMINA rtn
          ON rp.CODIGO_TIPO_NOMINA = rtn.CODIGO_TIPO_NOMINA
     WHERE rp.CODIGO_PERIODO = p_codigo_periodo;

    p_Message := 'Success';

EXCEPTION
    WHEN OTHERS THEN
        p_Message := SUBSTR(SQLERRM, 1, 4000);
        p_TotalRecords := 0;
        OPEN p_ResultSet FOR
            SELECT
                CAST(NULL AS NUMBER) CODIGO_PERIODO,
                CAST(NULL AS VARCHAR2(4000)) DESCRIPCION,
                CAST(NULL AS NUMBER) CODIGO_TIPO_NOMINA,
                CAST(NULL AS VARCHAR2(4000)) DESCRIPCION_TIPO_NOMINA,
                CAST(NULL AS DATE) FECHA_NOMINA,
                CAST(NULL AS NUMBER) PERIODO,
                CAST(NULL AS VARCHAR2(100)) DESCRIPCION_PERIODO,
                CAST(NULL AS VARCHAR2(10)) TIPO_NOMINA,
                CAST(NULL AS VARCHAR2(100)) TIPO_NOMINA_DESCRIPCION
            FROM DUAL
            WHERE 1 = 0;
END SP_REP_GRAL_NOM_PER_GET_ID;
/
