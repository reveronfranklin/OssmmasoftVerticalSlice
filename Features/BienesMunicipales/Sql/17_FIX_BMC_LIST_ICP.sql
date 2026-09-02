-- Catalogo de ICP para crear conteos.
-- Ejecutar conectado como BMC en el servidor de conteo.
-- La pantalla y BM_P_CONTEO deben consultar la misma replica BMC.
CREATE OR REPLACE PROCEDURE BMC.SP_BM1_GET_LIST_ICP (
    p_CodigoEmpresa IN NUMBER,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
BEGIN
    SELECT COUNT(*)
      INTO p_TotalRecords
      FROM (
            SELECT V.CODIGO_ICP
              FROM BMC.BM_V_BM1 V
             WHERE V.CODIGO_EMPRESA = p_CodigoEmpresa
             GROUP BY V.CODIGO_ICP
          );

    OPEN p_ResultSet FOR
        SELECT V.CODIGO_ICP,
               V.UNIDAD_TRABAJO
          FROM BMC.BM_V_BM1 V
         WHERE V.CODIGO_EMPRESA = p_CodigoEmpresa
         GROUP BY V.CODIGO_ICP, V.UNIDAD_TRABAJO
         ORDER BY V.UNIDAD_TRABAJO;

    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR
            SELECT CAST(NULL AS NUMBER) CODIGO_ICP,
                   CAST(NULL AS VARCHAR2(200)) UNIDAD_TRABAJO
              FROM DUAL
             WHERE 1 = 0;
END SP_BM1_GET_LIST_ICP;
/

SHOW ERRORS PROCEDURE BMC.SP_BM1_GET_LIST_ICP;
