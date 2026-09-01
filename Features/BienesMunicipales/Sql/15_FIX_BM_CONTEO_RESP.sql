-- Ejecutar con un usuario que pueda otorgar SELECT sobre RH.RH_PERSONAS y
-- crear procedimientos en BMC.
GRANT SELECT ON RH.RH_PERSONAS TO BMC;

CREATE OR REPLACE PROCEDURE BMC.SP_BM_CONTEO_GET_ALL (
    p_CodigoEmpresa IN NUMBER,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
BEGIN
    SELECT COUNT(*)
      INTO p_TotalRecords
      FROM BMC.BM_CONTEO C
     WHERE C.CODIGO_EMPRESA = p_CodigoEmpresa;

    OPEN p_ResultSet FOR
        SELECT C.CODIGO_BM_CONTEO,
               C.TITULO,
               C.COMENTARIO,
               C.CODIGO_PERSONA_RESPONSABLE,
               TRIM(NVL(P.NOMBRE, '') || ' ' || NVL(P.APELLIDO, '')) NOMBRE_PERSONA_RESPONSABLE,
               C.CANTIDAD_CONTEOS_ID CONTEO_ID,
               C.FECHA,
               C.CANTIDAD_CONTEOS_ID CONTEO,
               NVL(S.TOTAL_CANTIDAD, 0) TOTAL_CANTIDAD,
               NVL(S.TOTAL_CANTIDAD_CONTADA, 0) TOTAL_CANTIDAD_CONTADA,
               NVL(S.TOTAL_DIFERENCIA, 0) TOTAL_DIFERENCIA
          FROM BMC.BM_CONTEO C,
               RH.RH_PERSONAS P,
               (
                SELECT CODIGO_BM_CONTEO,
                       SUM(NVL(CANTIDAD, 0)) TOTAL_CANTIDAD,
                       SUM(NVL(CANTIDAD_CONTADA, 0)) TOTAL_CANTIDAD_CONTADA,
                       SUM(NVL(DIFERENCIA, 0)) TOTAL_DIFERENCIA
                  FROM BMC.BM_CONTEO_DETALLE
                 WHERE CODIGO_EMPRESA = p_CodigoEmpresa
                 GROUP BY CODIGO_BM_CONTEO
               ) S
         WHERE C.CODIGO_EMPRESA = p_CodigoEmpresa
           AND P.CODIGO_PERSONA(+) = C.CODIGO_PERSONA_RESPONSABLE
           AND P.CODIGO_EMPRESA(+) = C.CODIGO_EMPRESA
           AND S.CODIGO_BM_CONTEO(+) = C.CODIGO_BM_CONTEO
         ORDER BY C.FECHA DESC, C.CODIGO_BM_CONTEO DESC;

    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR
            SELECT CAST(NULL AS NUMBER) CODIGO_BM_CONTEO,
                   CAST(NULL AS VARCHAR2(100)) TITULO,
                   CAST(NULL AS VARCHAR2(4000)) COMENTARIO,
                   CAST(NULL AS NUMBER) CODIGO_PERSONA_RESPONSABLE,
                   CAST(NULL AS VARCHAR2(200)) NOMBRE_PERSONA_RESPONSABLE,
                   CAST(NULL AS NUMBER) CONTEO_ID,
                   CAST(NULL AS DATE) FECHA,
                   CAST(NULL AS NUMBER) CONTEO,
                   CAST(NULL AS NUMBER) TOTAL_CANTIDAD,
                   CAST(NULL AS NUMBER) TOTAL_CANTIDAD_CONTADA,
                   CAST(NULL AS NUMBER) TOTAL_DIFERENCIA
              FROM DUAL
             WHERE 1 = 0;
END SP_BM_CONTEO_GET_ALL;
/

SHOW ERRORS PROCEDURE BMC.SP_BM_CONTEO_GET_ALL;

CREATE OR REPLACE PROCEDURE BMC.SP_BM_CONT_HIST_GET (
    p_CodigoEmpresa IN NUMBER,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
BEGIN
    SELECT COUNT(*)
      INTO p_TotalRecords
      FROM BMC.BM_CONTEO_HISTORICO H
     WHERE H.CODIGO_EMPRESA = p_CodigoEmpresa;

    OPEN p_ResultSet FOR
        SELECT H.CODIGO_BM_CONTEO,
               H.TITULO,
               H.COMENTARIO,
               H.CODIGO_PERSONA_RESPONSABLE,
               TRIM(NVL(P.NOMBRE, '') || ' ' || NVL(P.APELLIDO, '')) NOMBRE_PERSONA_RESPONSABLE,
               H.CANTIDAD_CONTEOS_ID CONTEO_ID,
               H.FECHA,
               H.CANTIDAD_CONTEOS_ID CONTEO,
               H.TOTAL_CANTIDAD,
               H.TOTAL_CANTIDAD_CONTADA,
               H.TOTAL_DIFERENCIA
          FROM BMC.BM_CONTEO_HISTORICO H,
               RH.RH_PERSONAS P
         WHERE H.CODIGO_EMPRESA = p_CodigoEmpresa
           AND P.CODIGO_PERSONA(+) = H.CODIGO_PERSONA_RESPONSABLE
           AND P.CODIGO_EMPRESA(+) = H.CODIGO_EMPRESA
         ORDER BY H.FECHA_CIERRE DESC;

    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT * FROM BMC.BM_CONTEO_HISTORICO WHERE 1 = 0;
END SP_BM_CONT_HIST_GET;
/

SHOW ERRORS PROCEDURE BMC.SP_BM_CONT_HIST_GET;
