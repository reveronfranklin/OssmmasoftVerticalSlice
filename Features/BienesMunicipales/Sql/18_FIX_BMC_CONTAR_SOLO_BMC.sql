-- El proceso de contar debe consultar exclusivamente la replica BMC.
-- Ejecutar conectado como BMC despues de actualizar la replica.

CREATE OR REPLACE PROCEDURE BMC.SP_BM1_GET_PRODUCT_MOB (
    p_CodigoEmpresa IN NUMBER,
    p_CodigoBmConteo IN NUMBER,
    p_CodigoDirBien IN NUMBER,
    p_CodigoIcp IN NUMBER,
    p_CodigoArticulo IN NUMBER,
    p_ResponsableText IN VARCHAR2,
    p_SearchText IN VARCHAR2,
    p_Page IN NUMBER,
    p_PageSize IN NUMBER,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
    v_Page NUMBER := NVL(p_Page, 1);
    v_PageSize NUMBER := NVL(p_PageSize, 25);
    v_FromRow NUMBER;
    v_ToRow NUMBER;
BEGIN
    v_FromRow := ((v_Page - 1) * v_PageSize) + 1;
    v_ToRow := v_Page * v_PageSize;

    SELECT COUNT(*)
      INTO p_TotalRecords
      FROM BMC.BM_V_BM1 V
     WHERE V.CODIGO_EMPRESA = p_CodigoEmpresa
       AND (p_CodigoDirBien = 0 OR V.CODIGO_DIR_BIEN = p_CodigoDirBien)
       AND (p_CodigoIcp = 0 OR V.CODIGO_ICP = p_CodigoIcp)
       AND (p_CodigoArticulo = 0 OR EXISTS (
            SELECT 1
              FROM BMC.BM_BIENES B
             WHERE B.CODIGO_BIEN = V.CODIGO_BIEN
               AND B.CODIGO_EMPRESA = V.CODIGO_EMPRESA
               AND B.CODIGO_ARTICULO = p_CodigoArticulo))
       AND (TRIM(p_ResponsableText) IS NULL OR
            UPPER(NVL(V.RESPONSABLE_BIEN, '')) LIKE '%' || UPPER(TRIM(p_ResponsableText)) || '%')
       AND (TRIM(p_SearchText) IS NULL
            OR TO_CHAR(V.CODIGO_BIEN) LIKE '%' || TRIM(p_SearchText) || '%'
            OR UPPER(V.NUMERO_PLACA) LIKE '%' || UPPER(TRIM(p_SearchText)) || '%'
            OR UPPER(V.NRO_PLACA) LIKE '%' || UPPER(TRIM(p_SearchText)) || '%'
            OR UPPER(V.ARTICULO) LIKE '%' || UPPER(TRIM(p_SearchText)) || '%'
            OR UPPER(NVL(V.RESPONSABLE_BIEN, '')) LIKE '%' || UPPER(TRIM(p_SearchText)) || '%'
            OR UPPER(NVL(V.UNIDAD_TRABAJO, '')) LIKE '%' || UPPER(TRIM(p_SearchText)) || '%');

    OPEN p_ResultSet FOR
        SELECT ID, KEY, ARTICULO, DESCRIPCION, RESPONSABLE, NRO_PLACA,
               CODIGO_DEPARTAMENTO_RESP, DESCRIPCION_DEPARTAMENTO, CODIGO_DIR_BIEN
          FROM (
                SELECT X.*, ROWNUM RN
                  FROM (
                        SELECT V.CODIGO_BIEN ID,
                               TO_CHAR(V.CODIGO_BIEN) || '-' || V.NRO_PLACA KEY,
                               V.ARTICULO,
                               V.ESPECIFICACION DESCRIPCION,
                               V.RESPONSABLE_BIEN RESPONSABLE,
                               V.NRO_PLACA,
                               V.CODIGO_ICP CODIGO_DEPARTAMENTO_RESP,
                               V.UNIDAD_TRABAJO DESCRIPCION_DEPARTAMENTO,
                               V.CODIGO_DIR_BIEN
                          FROM BMC.BM_V_BM1 V
                         WHERE V.CODIGO_EMPRESA = p_CodigoEmpresa
                           AND (p_CodigoDirBien = 0 OR V.CODIGO_DIR_BIEN = p_CodigoDirBien)
                           AND (p_CodigoIcp = 0 OR V.CODIGO_ICP = p_CodigoIcp)
                           AND (p_CodigoArticulo = 0 OR EXISTS (
                                SELECT 1
                                  FROM BMC.BM_BIENES B
                                 WHERE B.CODIGO_BIEN = V.CODIGO_BIEN
                                   AND B.CODIGO_EMPRESA = V.CODIGO_EMPRESA
                                   AND B.CODIGO_ARTICULO = p_CodigoArticulo))
                           AND (TRIM(p_ResponsableText) IS NULL OR
                                UPPER(NVL(V.RESPONSABLE_BIEN, '')) LIKE '%' || UPPER(TRIM(p_ResponsableText)) || '%')
                           AND (TRIM(p_SearchText) IS NULL
                                OR TO_CHAR(V.CODIGO_BIEN) LIKE '%' || TRIM(p_SearchText) || '%'
                                OR UPPER(V.NUMERO_PLACA) LIKE '%' || UPPER(TRIM(p_SearchText)) || '%'
                                OR UPPER(V.NRO_PLACA) LIKE '%' || UPPER(TRIM(p_SearchText)) || '%'
                                OR UPPER(V.ARTICULO) LIKE '%' || UPPER(TRIM(p_SearchText)) || '%'
                                OR UPPER(NVL(V.RESPONSABLE_BIEN, '')) LIKE '%' || UPPER(TRIM(p_SearchText)) || '%'
                                OR UPPER(NVL(V.UNIDAD_TRABAJO, '')) LIKE '%' || UPPER(TRIM(p_SearchText)) || '%')
                         ORDER BY V.UNIDAD_TRABAJO, V.ARTICULO, V.NRO_PLACA
                       ) X
                 WHERE ROWNUM <= v_ToRow)
         WHERE RN >= v_FromRow;

    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR
            SELECT CAST(NULL AS NUMBER) ID, CAST(NULL AS VARCHAR2(100)) KEY,
                   CAST(NULL AS VARCHAR2(200)) ARTICULO,
                   CAST(NULL AS VARCHAR2(4000)) DESCRIPCION,
                   CAST(NULL AS VARCHAR2(4000)) RESPONSABLE,
                   CAST(NULL AS VARCHAR2(20)) NRO_PLACA,
                   CAST(NULL AS NUMBER) CODIGO_DEPARTAMENTO_RESP,
                   CAST(NULL AS VARCHAR2(200)) DESCRIPCION_DEPARTAMENTO,
                   CAST(NULL AS NUMBER) CODIGO_DIR_BIEN
              FROM DUAL WHERE 1 = 0;
END SP_BM1_GET_PRODUCT_MOB;
/

SHOW ERRORS PROCEDURE BMC.SP_BM1_GET_PRODUCT_MOB;

CREATE OR REPLACE PROCEDURE BMC.SP_BM_DESC_GET_TIT (
    p_CodigoEmpresa IN NUMBER,
    p_TituloId IN NUMBER,
    p_DescripcionId IN NUMBER,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
BEGIN
    SELECT COUNT(*) INTO p_TotalRecords
      FROM BMC.BM_DESCRIPTIVAS D
     WHERE D.CODIGO_EMPRESA = p_CodigoEmpresa
       AND D.TITULO_ID = p_TituloId
       AND (p_DescripcionId = 0 OR D.DESCRIPCION_ID = p_DescripcionId);

    OPEN p_ResultSet FOR
        SELECT D.DESCRIPCION_ID ID, D.DESCRIPCION_ID, D.DESCRIPCION,
               D.CODIGO, D.EXTRA1, D.EXTRA2, D.EXTRA3
          FROM BMC.BM_DESCRIPTIVAS D
         WHERE D.CODIGO_EMPRESA = p_CodigoEmpresa
           AND D.TITULO_ID = p_TituloId
           AND (p_DescripcionId = 0 OR D.DESCRIPCION_ID = p_DescripcionId)
         ORDER BY D.DESCRIPCION;

    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR
            SELECT CAST(NULL AS NUMBER) ID, CAST(NULL AS NUMBER) DESCRIPCION_ID,
                   CAST(NULL AS VARCHAR2(500)) DESCRIPCION,
                   CAST(NULL AS VARCHAR2(10)) CODIGO,
                   CAST(NULL AS VARCHAR2(100)) EXTRA1,
                   CAST(NULL AS VARCHAR2(100)) EXTRA2,
                   CAST(NULL AS VARCHAR2(100)) EXTRA3
              FROM DUAL WHERE 1 = 0;
END SP_BM_DESC_GET_TIT;
/

SHOW ERRORS PROCEDURE BMC.SP_BM_DESC_GET_TIT;
