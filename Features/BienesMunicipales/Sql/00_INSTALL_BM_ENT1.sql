-- Entregable 1 - Bienes Municipales
-- Ejecutar los bloques BM con el usuario/esquema BM.
-- Ejecutar los bloques BMC con el usuario/esquema BMC.

CREATE OR REPLACE PROCEDURE BM.SP_BM1_GET_LIST_ICP (
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
              FROM BM.BM_V_BM1 V
             WHERE V.CODIGO_EMPRESA = p_CodigoEmpresa
             GROUP BY V.CODIGO_ICP
          );

    OPEN p_ResultSet FOR
        SELECT V.CODIGO_ICP,
               V.UNIDAD_TRABAJO
          FROM BM.BM_V_BM1 V
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

CREATE OR REPLACE PROCEDURE BM.SP_BM1_GET_UBI_ORI (
    p_CodigoEmpresa IN NUMBER,
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
      FROM (
            SELECT V.CODIGO_DIR_BIEN
              FROM BM.BM_V_BM1 V
             WHERE V.CODIGO_EMPRESA = p_CodigoEmpresa
               AND (p_CodigoIcp = 0 OR V.CODIGO_ICP = p_CodigoIcp)
               AND (
                    p_CodigoArticulo = 0
                    OR EXISTS (
                        SELECT 1
                          FROM BM.BM_BIENES B
                         WHERE B.CODIGO_BIEN = V.CODIGO_BIEN
                           AND B.CODIGO_EMPRESA = V.CODIGO_EMPRESA
                           AND B.CODIGO_ARTICULO = p_CodigoArticulo
                    )
               )
               AND (TRIM(p_ResponsableText) IS NULL OR UPPER(NVL(V.RESPONSABLE_BIEN, '')) LIKE '%' || UPPER(TRIM(p_ResponsableText)) || '%')
               AND (
                    TRIM(p_SearchText) IS NULL
                    OR TO_CHAR(V.CODIGO_DIR_BIEN) LIKE '%' || TRIM(p_SearchText) || '%'
                    OR TO_CHAR(V.CODIGO_ICP) LIKE '%' || TRIM(p_SearchText) || '%'
                    OR UPPER(NVL(V.UNIDAD_TRABAJO, '')) LIKE '%' || UPPER(TRIM(p_SearchText)) || '%'
                    OR UPPER(NVL(V.NUMERO_PLACA, '')) LIKE '%' || UPPER(TRIM(p_SearchText)) || '%'
                    OR UPPER(NVL(V.NRO_PLACA, '')) LIKE '%' || UPPER(TRIM(p_SearchText)) || '%'
               )
             GROUP BY V.CODIGO_DIR_BIEN, V.CODIGO_ICP, V.UNIDAD_TRABAJO
          );

    OPEN p_ResultSet FOR
        SELECT CODIGO_DIR_BIEN,
               CODIGO_ICP,
               UNIDAD_EJECUTORA,
               PAIS_ID,
               ESTADO_ID,
               MUNICIPIO_ID,
               CIUDAD_ID,
               PARROQUIA_ID,
               SECTOR_ID,
               URBANIZACION_ID,
               MANZANA_ID,
               PARCELA_ID,
               VIALIDAD_ID,
               VIALIDAD,
               TIPO_VIVIENDA_ID,
               VIVIENDA,
               TIPO_NIVEL_ID,
               NIVEL,
               TIPO_UNIDAD_ID,
               NUMERO_UNIDAD,
               COMPLEMENTO_DIR,
               TENENCIA_ID,
               CODIGO_POSTAL,
               FECHA_INI,
               FECHA_FIN,
               UNIDAD_TRABAJO_ID,
               DIRECCION,
               SEARCH_TEXT
          FROM (
                SELECT X.*, ROWNUM RN
                  FROM (
                        SELECT V.CODIGO_DIR_BIEN,
                               V.CODIGO_ICP,
                               V.UNIDAD_TRABAJO UNIDAD_EJECUTORA,
                               0 PAIS_ID,
                               0 ESTADO_ID,
                               0 MUNICIPIO_ID,
                               0 CIUDAD_ID,
                               0 PARROQUIA_ID,
                               0 SECTOR_ID,
                               0 URBANIZACION_ID,
                               0 MANZANA_ID,
                               0 PARCELA_ID,
                               0 VIALIDAD_ID,
                               CAST(NULL AS VARCHAR2(200)) VIALIDAD,
                               0 TIPO_VIVIENDA_ID,
                               CAST(NULL AS VARCHAR2(200)) VIVIENDA,
                               0 TIPO_NIVEL_ID,
                               CAST(NULL AS VARCHAR2(200)) NIVEL,
                               0 TIPO_UNIDAD_ID,
                               CAST(NULL AS VARCHAR2(200)) NUMERO_UNIDAD,
                               CAST(NULL AS VARCHAR2(4000)) COMPLEMENTO_DIR,
                               0 TENENCIA_ID,
                               0 CODIGO_POSTAL,
                               CAST(NULL AS DATE) FECHA_INI,
                               CAST(NULL AS DATE) FECHA_FIN,
                               0 UNIDAD_TRABAJO_ID,
                               V.UNIDAD_TRABAJO DIRECCION,
                               '#' || TO_CHAR(V.CODIGO_DIR_BIEN) || ' ICP ' || TO_CHAR(V.CODIGO_ICP) || ' ' || V.UNIDAD_TRABAJO SEARCH_TEXT
                          FROM BM.BM_V_BM1 V
                         WHERE V.CODIGO_EMPRESA = p_CodigoEmpresa
                           AND (p_CodigoIcp = 0 OR V.CODIGO_ICP = p_CodigoIcp)
                           AND (
                                p_CodigoArticulo = 0
                                OR EXISTS (
                                    SELECT 1
                                      FROM BM.BM_BIENES B
                                     WHERE B.CODIGO_BIEN = V.CODIGO_BIEN
                                       AND B.CODIGO_EMPRESA = V.CODIGO_EMPRESA
                                       AND B.CODIGO_ARTICULO = p_CodigoArticulo
                                )
                           )
                           AND (TRIM(p_ResponsableText) IS NULL OR UPPER(NVL(V.RESPONSABLE_BIEN, '')) LIKE '%' || UPPER(TRIM(p_ResponsableText)) || '%')
                           AND (
                                TRIM(p_SearchText) IS NULL
                                OR TO_CHAR(V.CODIGO_DIR_BIEN) LIKE '%' || TRIM(p_SearchText) || '%'
                                OR TO_CHAR(V.CODIGO_ICP) LIKE '%' || TRIM(p_SearchText) || '%'
                                OR UPPER(NVL(V.UNIDAD_TRABAJO, '')) LIKE '%' || UPPER(TRIM(p_SearchText)) || '%'
                                OR UPPER(NVL(V.NUMERO_PLACA, '')) LIKE '%' || UPPER(TRIM(p_SearchText)) || '%'
                                OR UPPER(NVL(V.NRO_PLACA, '')) LIKE '%' || UPPER(TRIM(p_SearchText)) || '%'
                           )
                         GROUP BY V.CODIGO_DIR_BIEN, V.CODIGO_ICP, V.UNIDAD_TRABAJO
                         ORDER BY V.UNIDAD_TRABAJO, V.CODIGO_DIR_BIEN
                       ) X
                 WHERE ROWNUM <= v_ToRow
               )
         WHERE RN >= v_FromRow;

    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR
            SELECT CAST(NULL AS NUMBER) CODIGO_DIR_BIEN,
                   CAST(NULL AS NUMBER) CODIGO_ICP,
                   CAST(NULL AS VARCHAR2(200)) UNIDAD_EJECUTORA,
                   CAST(NULL AS NUMBER) PAIS_ID,
                   CAST(NULL AS NUMBER) ESTADO_ID,
                   CAST(NULL AS NUMBER) MUNICIPIO_ID,
                   CAST(NULL AS NUMBER) CIUDAD_ID,
                   CAST(NULL AS NUMBER) PARROQUIA_ID,
                   CAST(NULL AS NUMBER) SECTOR_ID,
                   CAST(NULL AS NUMBER) URBANIZACION_ID,
                   CAST(NULL AS NUMBER) MANZANA_ID,
                   CAST(NULL AS NUMBER) PARCELA_ID,
                   CAST(NULL AS NUMBER) VIALIDAD_ID,
                   CAST(NULL AS VARCHAR2(200)) VIALIDAD,
                   CAST(NULL AS NUMBER) TIPO_VIVIENDA_ID,
                   CAST(NULL AS VARCHAR2(200)) VIVIENDA,
                   CAST(NULL AS NUMBER) TIPO_NIVEL_ID,
                   CAST(NULL AS VARCHAR2(200)) NIVEL,
                   CAST(NULL AS NUMBER) TIPO_UNIDAD_ID,
                   CAST(NULL AS VARCHAR2(200)) NUMERO_UNIDAD,
                   CAST(NULL AS VARCHAR2(4000)) COMPLEMENTO_DIR,
                   CAST(NULL AS NUMBER) TENENCIA_ID,
                   CAST(NULL AS NUMBER) CODIGO_POSTAL,
                   CAST(NULL AS DATE) FECHA_INI,
                   CAST(NULL AS DATE) FECHA_FIN,
                   CAST(NULL AS NUMBER) UNIDAD_TRABAJO_ID,
                   CAST(NULL AS VARCHAR2(4000)) DIRECCION,
                   CAST(NULL AS VARCHAR2(4000)) SEARCH_TEXT
              FROM DUAL
             WHERE 1 = 0;
END SP_BM1_GET_UBI_ORI;
/

CREATE OR REPLACE PROCEDURE BM.SP_BM1_GET_PLACAS (
    p_CodigoEmpresa IN NUMBER,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
BEGIN
    SELECT COUNT(*)
      INTO p_TotalRecords
      FROM BM.BM_BIENES A
     WHERE A.CODIGO_EMPRESA = p_CodigoEmpresa;

    OPEN p_ResultSet FOR
        SELECT A.NUMERO_PLACA,
               B.DENOMINACION ARTICULO,
               A.NUMERO_PLACA || ' ' || B.DENOMINACION SEARCH_TEXT
          FROM BM.BM_BIENES A,
               BM.BM_ARTICULOS B
         WHERE A.CODIGO_EMPRESA = p_CodigoEmpresa
           AND B.CODIGO_ARTICULO = A.CODIGO_ARTICULO
         ORDER BY A.NUMERO_PLACA;

    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR
            SELECT CAST(NULL AS VARCHAR2(20)) NUMERO_PLACA,
                   CAST(NULL AS VARCHAR2(200)) ARTICULO,
                   CAST(NULL AS VARCHAR2(250)) SEARCH_TEXT
              FROM DUAL
             WHERE 1 = 0;
END SP_BM1_GET_PLACAS;
/

CREATE OR REPLACE PROCEDURE BM.SP_BM1_GET_FIRST_MOV (
    p_CodigoEmpresa IN NUMBER,
    p_Fecha OUT DATE,
    p_Message OUT VARCHAR2
) AS
BEGIN
    SELECT MIN(B.FECHA_MOVIMIENTO)
      INTO p_Fecha
      FROM BM.BM_BIENES A,
           BM.BM_MOV_BIENES B
     WHERE A.CODIGO_EMPRESA = p_CodigoEmpresa
       AND A.CODIGO_BIEN = B.CODIGO_BIEN;

    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        p_Fecha := NULL;
        p_Message := 'Error tecnico: ' || SQLERRM;
END SP_BM1_GET_FIRST_MOV;
/

-- p_SearchText, la exclusion de placas en cuarentena y la columna PLACA_BARRA provienen del
-- requerimiento 18. El predicado del COUNT y el del cursor deben mantenerse identicos.
CREATE OR REPLACE PROCEDURE BM.SP_BM1_GET_BY_ICP (
    p_CodigoEmpresa IN NUMBER,
    p_FechaDesde IN DATE,
    p_FechaHasta IN DATE,
    p_CodigosIcp IN VARCHAR2,
    p_SearchText IN VARCHAR2 DEFAULT NULL,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
BEGIN
    SELECT COUNT(*)
      INTO p_TotalRecords
      FROM BM.BM_V_BM1 V
     WHERE V.CODIGO_EMPRESA = p_CodigoEmpresa
       AND (p_FechaDesde IS NULL OR TRUNC(V.FECHA_MOVIMIENTO) >= TRUNC(p_FechaDesde))
       AND (p_FechaHasta IS NULL OR TRUNC(V.FECHA_MOVIMIENTO) <= TRUNC(p_FechaHasta))
       AND (
             p_CodigosIcp IS NULL
             OR INSTR(',' || p_CodigosIcp || ',', ',' || TO_CHAR(V.CODIGO_ICP) || ',') > 0
           )
       AND (
             TRIM(p_SearchText) IS NULL
             OR TO_CHAR(V.CODIGO_BIEN) LIKE '%' || TRIM(p_SearchText) || '%'
             OR UPPER(V.NUMERO_PLACA) LIKE '%' || UPPER(TRIM(p_SearchText)) || '%'
             OR UPPER(V.NRO_PLACA) LIKE '%' || UPPER(TRIM(p_SearchText)) || '%'
             OR UPPER(V.ARTICULO) LIKE '%' || UPPER(TRIM(p_SearchText)) || '%'
             OR UPPER(NVL(V.RESPONSABLE_BIEN, '')) LIKE '%' || UPPER(TRIM(p_SearchText)) || '%'
             OR UPPER(NVL(V.UNIDAD_TRABAJO, '')) LIKE '%' || UPPER(TRIM(p_SearchText)) || '%'
           )
       AND NOT EXISTS (
             SELECT NULL
               FROM BM.BM_PLACAS_CUARENTENA Q
              WHERE Q.CODIGO_EMPRESA = V.CODIGO_EMPRESA
                AND Q.NUMERO_PLACA = V.NRO_PLACA
           );

    OPEN p_ResultSet FOR
        SELECT V.UNIDAD_TRABAJO,
               V.CODIGO_GRUPO,
               V.CODIGO_NIVEL1,
               V.CODIGO_NIVEL2,
               V.NUMERO_LOTE,
               V.CANTIDAD,
               V.NUMERO_PLACA,
               V.VALOR_ACTUAL,
               V.ARTICULO,
               V.ESPECIFICACION,
               NVL(V.SERVICIO, '') SERVICIO,
               V.RESPONSABLE_BIEN,
               V.UNIDAD_TRABAJO || ' ' || V.ARTICULO || ' ' || V.NUMERO_PLACA SEARCH_TEXT,
               '' LINK_DATA,
               V.CODIGO_BIEN,
               V.CODIGO_MOV_BIEN,
               V.FECHA_MOVIMIENTO,
               V.NRO_PLACA,
               V.CODIGO_GRUPO || '-' || V.CODIGO_NIVEL1 || '-' || V.CODIGO_NIVEL2 || '-' ||
                   V.NUMERO_PLACA PLACA_BARRA
          FROM BM.BM_V_BM1 V
         WHERE V.CODIGO_EMPRESA = p_CodigoEmpresa
           AND (p_FechaDesde IS NULL OR TRUNC(V.FECHA_MOVIMIENTO) >= TRUNC(p_FechaDesde))
           AND (p_FechaHasta IS NULL OR TRUNC(V.FECHA_MOVIMIENTO) <= TRUNC(p_FechaHasta))
           AND (
                 p_CodigosIcp IS NULL
                 OR INSTR(',' || p_CodigosIcp || ',', ',' || TO_CHAR(V.CODIGO_ICP) || ',') > 0
               )
           AND (
                 TRIM(p_SearchText) IS NULL
                 OR TO_CHAR(V.CODIGO_BIEN) LIKE '%' || TRIM(p_SearchText) || '%'
                 OR UPPER(V.NUMERO_PLACA) LIKE '%' || UPPER(TRIM(p_SearchText)) || '%'
                 OR UPPER(V.NRO_PLACA) LIKE '%' || UPPER(TRIM(p_SearchText)) || '%'
                 OR UPPER(V.ARTICULO) LIKE '%' || UPPER(TRIM(p_SearchText)) || '%'
                 OR UPPER(NVL(V.RESPONSABLE_BIEN, '')) LIKE '%' || UPPER(TRIM(p_SearchText)) || '%'
                 OR UPPER(NVL(V.UNIDAD_TRABAJO, '')) LIKE '%' || UPPER(TRIM(p_SearchText)) || '%'
               )
           AND NOT EXISTS (
                 SELECT NULL
                   FROM BM.BM_PLACAS_CUARENTENA Q
                  WHERE Q.CODIGO_EMPRESA = V.CODIGO_EMPRESA
                    AND Q.NUMERO_PLACA = V.NRO_PLACA
               )
         ORDER BY V.UNIDAD_TRABAJO, V.ARTICULO, V.NUMERO_PLACA;

    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR
            SELECT CAST(NULL AS VARCHAR2(200)) UNIDAD_TRABAJO,
                   CAST(NULL AS VARCHAR2(1)) CODIGO_GRUPO,
                   CAST(NULL AS VARCHAR2(2)) CODIGO_NIVEL1,
                   CAST(NULL AS VARCHAR2(2)) CODIGO_NIVEL2,
                   CAST(NULL AS VARCHAR2(20)) NUMERO_LOTE,
                   CAST(NULL AS NUMBER) CANTIDAD,
                   CAST(NULL AS VARCHAR2(4000)) NUMERO_PLACA,
                   CAST(NULL AS NUMBER) VALOR_ACTUAL,
                   CAST(NULL AS VARCHAR2(200)) ARTICULO,
                   CAST(NULL AS VARCHAR2(4000)) ESPECIFICACION,
                   CAST(NULL AS VARCHAR2(100)) SERVICIO,
                   CAST(NULL AS VARCHAR2(4000)) RESPONSABLE_BIEN,
                   CAST(NULL AS VARCHAR2(4000)) SEARCH_TEXT,
                   CAST(NULL AS VARCHAR2(4000)) LINK_DATA,
                   CAST(NULL AS NUMBER) CODIGO_BIEN,
                   CAST(NULL AS NUMBER) CODIGO_MOV_BIEN,
                   CAST(NULL AS DATE) FECHA_MOVIMIENTO,
                   CAST(NULL AS VARCHAR2(20)) NRO_PLACA,
                   CAST(NULL AS VARCHAR2(4000)) PLACA_BARRA
              FROM DUAL
             WHERE 1 = 0;
END SP_BM1_GET_BY_ICP;
/

CREATE OR REPLACE PROCEDURE BM.SP_BM1_GET_PRODUCT_MOB (
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
     FROM BM.BM_V_BM1 V
     WHERE V.CODIGO_EMPRESA = p_CodigoEmpresa
       AND (p_CodigoDirBien = 0 OR V.CODIGO_DIR_BIEN = p_CodigoDirBien)
       AND (p_CodigoIcp = 0 OR V.CODIGO_ICP = p_CodigoIcp)
       AND (
            p_CodigoArticulo = 0
            OR EXISTS (
                SELECT 1
                 FROM BM.BM_BIENES B
                 WHERE B.CODIGO_BIEN = V.CODIGO_BIEN
                   AND B.CODIGO_EMPRESA = V.CODIGO_EMPRESA
                   AND B.CODIGO_ARTICULO = p_CodigoArticulo
            )
       )
       AND (TRIM(p_ResponsableText) IS NULL OR UPPER(NVL(V.RESPONSABLE_BIEN, '')) LIKE '%' || UPPER(TRIM(p_ResponsableText)) || '%')
       AND (
            TRIM(p_SearchText) IS NULL
            OR TO_CHAR(V.CODIGO_BIEN) LIKE '%' || TRIM(p_SearchText) || '%'
            OR UPPER(V.NUMERO_PLACA) LIKE '%' || UPPER(TRIM(p_SearchText)) || '%'
            OR UPPER(V.NRO_PLACA) LIKE '%' || UPPER(TRIM(p_SearchText)) || '%'
            OR UPPER(V.ARTICULO) LIKE '%' || UPPER(TRIM(p_SearchText)) || '%'
            OR UPPER(NVL(V.RESPONSABLE_BIEN, '')) LIKE '%' || UPPER(TRIM(p_SearchText)) || '%'
            OR UPPER(NVL(V.UNIDAD_TRABAJO, '')) LIKE '%' || UPPER(TRIM(p_SearchText)) || '%'
       );

    OPEN p_ResultSet FOR
        SELECT ID,
               KEY,
               ARTICULO,
               DESCRIPCION,
               RESPONSABLE,
               NRO_PLACA,
               CODIGO_DEPARTAMENTO_RESP,
               DESCRIPCION_DEPARTAMENTO,
               CODIGO_DIR_BIEN
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
                         FROM BM.BM_V_BM1 V
                         WHERE V.CODIGO_EMPRESA = p_CodigoEmpresa
                           AND (p_CodigoDirBien = 0 OR V.CODIGO_DIR_BIEN = p_CodigoDirBien)
                           AND (p_CodigoIcp = 0 OR V.CODIGO_ICP = p_CodigoIcp)
                           AND (
                                p_CodigoArticulo = 0
                                OR EXISTS (
                                    SELECT 1
                                     FROM BM.BM_BIENES B
                                     WHERE B.CODIGO_BIEN = V.CODIGO_BIEN
                                       AND B.CODIGO_EMPRESA = V.CODIGO_EMPRESA
                                       AND B.CODIGO_ARTICULO = p_CodigoArticulo
                                )
                           )
                           AND (TRIM(p_ResponsableText) IS NULL OR UPPER(NVL(V.RESPONSABLE_BIEN, '')) LIKE '%' || UPPER(TRIM(p_ResponsableText)) || '%')
                           AND (
                                TRIM(p_SearchText) IS NULL
                                OR TO_CHAR(V.CODIGO_BIEN) LIKE '%' || TRIM(p_SearchText) || '%'
                                OR UPPER(V.NUMERO_PLACA) LIKE '%' || UPPER(TRIM(p_SearchText)) || '%'
                                OR UPPER(V.NRO_PLACA) LIKE '%' || UPPER(TRIM(p_SearchText)) || '%'
                                OR UPPER(V.ARTICULO) LIKE '%' || UPPER(TRIM(p_SearchText)) || '%'
                                OR UPPER(NVL(V.RESPONSABLE_BIEN, '')) LIKE '%' || UPPER(TRIM(p_SearchText)) || '%'
                                OR UPPER(NVL(V.UNIDAD_TRABAJO, '')) LIKE '%' || UPPER(TRIM(p_SearchText)) || '%'
                           )
                         ORDER BY V.UNIDAD_TRABAJO, V.ARTICULO, V.NRO_PLACA
                       ) X
                 WHERE ROWNUM <= v_ToRow
               )
         WHERE RN >= v_FromRow;

    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR
            SELECT CAST(NULL AS NUMBER) ID,
                   CAST(NULL AS VARCHAR2(100)) KEY,
                   CAST(NULL AS VARCHAR2(200)) ARTICULO,
                   CAST(NULL AS VARCHAR2(4000)) DESCRIPCION,
                   CAST(NULL AS VARCHAR2(4000)) RESPONSABLE,
                   CAST(NULL AS VARCHAR2(20)) NRO_PLACA,
                   CAST(NULL AS NUMBER) CODIGO_DEPARTAMENTO_RESP,
                   CAST(NULL AS VARCHAR2(200)) DESCRIPCION_DEPARTAMENTO,
                   CAST(NULL AS NUMBER) CODIGO_DIR_BIEN
              FROM DUAL
             WHERE 1 = 0;
END SP_BM1_GET_PRODUCT_MOB;
/

CREATE OR REPLACE PROCEDURE BM.SP_BM_DESC_GET_TIT (
    p_CodigoEmpresa IN NUMBER,
    p_TituloId IN NUMBER,
    p_DescripcionId IN NUMBER,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
BEGIN
    SELECT COUNT(*)
      INTO p_TotalRecords
      FROM BM.BM_DESCRIPTIVAS D
     WHERE D.CODIGO_EMPRESA = p_CodigoEmpresa
       AND D.TITULO_ID = p_TituloId
       AND (p_DescripcionId = 0 OR D.DESCRIPCION_ID = p_DescripcionId);

    OPEN p_ResultSet FOR
        SELECT D.DESCRIPCION_ID ID,
               D.DESCRIPCION_ID,
               D.DESCRIPCION,
               D.CODIGO,
               D.EXTRA1,
               D.EXTRA2,
               D.EXTRA3
          FROM BM.BM_DESCRIPTIVAS D
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
            SELECT CAST(NULL AS NUMBER) ID,
                   CAST(NULL AS NUMBER) DESCRIPCION_ID,
                   CAST(NULL AS VARCHAR2(500)) DESCRIPCION,
                   CAST(NULL AS VARCHAR2(10)) CODIGO,
                   CAST(NULL AS VARCHAR2(100)) EXTRA1,
                   CAST(NULL AS VARCHAR2(100)) EXTRA2,
                   CAST(NULL AS VARCHAR2(100)) EXTRA3
              FROM DUAL
             WHERE 1 = 0;
END SP_BM_DESC_GET_TIT;
/

CREATE OR REPLACE PROCEDURE BM.SP_BM_PLACA_CUA_GET (
    p_CodigoEmpresa IN NUMBER,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
BEGIN
    SELECT COUNT(*)
      INTO p_TotalRecords
      FROM BM.BM_PLACAS_CUARENTENA P
     WHERE P.CODIGO_EMPRESA = p_CodigoEmpresa;

    OPEN p_ResultSet FOR
        SELECT P.CODIGO_PLACA_CUARENTENA,
               P.NUMERO_PLACA,
               NVL(A.DENOMINACION, '') ARTICULO,
               P.NUMERO_PLACA || ' ' || NVL(A.DENOMINACION, '') SEARCH_TEXT
          FROM BM.BM_PLACAS_CUARENTENA P,
               BM.BM_BIENES B,
               BM.BM_ARTICULOS A
         WHERE P.CODIGO_EMPRESA = p_CodigoEmpresa
           AND B.NUMERO_PLACA(+) = P.NUMERO_PLACA
           AND B.CODIGO_EMPRESA(+) = P.CODIGO_EMPRESA
           AND A.CODIGO_ARTICULO(+) = B.CODIGO_ARTICULO
         ORDER BY P.NUMERO_PLACA;

    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR
            SELECT CAST(NULL AS NUMBER) CODIGO_PLACA_CUARENTENA,
                   CAST(NULL AS VARCHAR2(20)) NUMERO_PLACA,
                   CAST(NULL AS VARCHAR2(200)) ARTICULO,
                   CAST(NULL AS VARCHAR2(250)) SEARCH_TEXT
              FROM DUAL
             WHERE 1 = 0;
END SP_BM_PLACA_CUA_GET;
/

CREATE OR REPLACE PROCEDURE BM.SP_BM_PLACA_CUA_INS (
    p_CodigoEmpresa IN NUMBER,
    p_CodigoPlacaCua IN NUMBER,
    p_NumeroPlaca IN VARCHAR2,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
    v_Id NUMBER;
    v_Existe NUMBER;
BEGIN
    SELECT COUNT(*)
      INTO v_Existe
      FROM BM.BM_PLACAS_CUARENTENA
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND NUMERO_PLACA = p_NumeroPlaca;

    IF v_Existe = 0 THEN
        SELECT NVL(MAX(CODIGO_PLACA_CUARENTENA), 0) + 1
          INTO v_Id
          FROM BM.BM_PLACAS_CUARENTENA;

        INSERT INTO BM.BM_PLACAS_CUARENTENA (
            CODIGO_PLACA_CUARENTENA,
            NUMERO_PLACA,
            CODIGO_EMPRESA,
            FECHA_INS
        ) VALUES (
            v_Id,
            p_NumeroPlaca,
            p_CodigoEmpresa,
            SYSDATE
        );
    END IF;

    BM.SP_BM_PLACA_CUA_GET(p_CodigoEmpresa, p_ResultSet, p_Message, p_TotalRecords);
EXCEPTION
    WHEN OTHERS THEN
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT * FROM BM.BM_PLACAS_CUARENTENA WHERE 1 = 0;
END SP_BM_PLACA_CUA_INS;
/

CREATE OR REPLACE PROCEDURE BM.SP_BM_PLACA_CUA_DEL (
    p_CodigoEmpresa IN NUMBER,
    p_CodigoPlacaCua IN NUMBER,
    p_NumeroPlaca IN VARCHAR2,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
BEGIN
    DELETE FROM BM.BM_PLACAS_CUARENTENA
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND CODIGO_PLACA_CUARENTENA = p_CodigoPlacaCua;

    BM.SP_BM_PLACA_CUA_GET(p_CodigoEmpresa, p_ResultSet, p_Message, p_TotalRecords);
EXCEPTION
    WHEN OTHERS THEN
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT * FROM BM.BM_PLACAS_CUARENTENA WHERE 1 = 0;
END SP_BM_PLACA_CUA_DEL;
/

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
               '' NOMBRE_PERSONA_RESPONSABLE,
               C.CANTIDAD_CONTEOS_ID CONTEO_ID,
               C.FECHA,
               C.CANTIDAD_CONTEOS_ID CONTEO,
               NVL(S.TOTAL_CANTIDAD, 0) TOTAL_CANTIDAD,
               NVL(S.TOTAL_CANTIDAD_CONTADA, 0) TOTAL_CANTIDAD_CONTADA,
               NVL(S.TOTAL_DIFERENCIA, 0) TOTAL_DIFERENCIA
          FROM BMC.BM_CONTEO C,
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

CREATE OR REPLACE PROCEDURE BMC.SP_BM_CONTEO_INS (
    p_CodigoEmpresa IN NUMBER,
    p_CodigoBmConteo IN NUMBER,
    p_Titulo IN VARCHAR2,
    p_Comentario IN VARCHAR2,
    p_CodigoPersonaResp IN NUMBER,
    p_ConteoId IN NUMBER,
    p_Fecha IN DATE,
    p_CodigosIcp IN VARCHAR2,
    p_CantidadConteos IN NUMBER,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
    v_Id NUMBER;
    v_Icp VARCHAR2(4000);
BEGIN
    IF NVL(p_CantidadConteos, 0) < 1 THEN
        RAISE_APPLICATION_ERROR(-20001, 'Cantidad de conteos invalida');
    END IF;

    SELECT NVL(MAX(CODIGO_BM_CONTEO), 0) + 1
      INTO v_Id
      FROM BMC.BM_CONTEO;

    INSERT INTO BMC.BM_CONTEO (
        CODIGO_BM_CONTEO,
        TITULO,
        CODIGO_PERSONA_RESPONSABLE,
        CANTIDAD_CONTEOS_ID,
        FECHA,
        CODIGO_EMPRESA,
        COMENTARIO,
        FECHA_INS
    ) VALUES (
        v_Id,
        p_Titulo,
        p_CodigoPersonaResp,
        p_ConteoId,
        NVL(p_Fecha, SYSDATE),
        p_CodigoEmpresa,
        p_Comentario,
        SYSDATE
    );

    v_Icp := NVL(p_CodigosIcp, 'TODOS');
    BMC.BM_P_CONTEO(v_Icp, p_CodigoEmpresa, -1, v_Id, p_CantidadConteos);
    BMC.SP_BM_CONTEO_GET_ALL(p_CodigoEmpresa, p_ResultSet, p_Message, p_TotalRecords);
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
END SP_BM_CONTEO_INS;
/

CREATE OR REPLACE PROCEDURE BMC.SP_BM_CONTEO_UPD (
    p_CodigoEmpresa IN NUMBER,
    p_CodigoBmConteo IN NUMBER,
    p_Titulo IN VARCHAR2,
    p_Comentario IN VARCHAR2,
    p_CodigoPersonaResp IN NUMBER,
    p_ConteoId IN NUMBER,
    p_Fecha IN DATE,
    p_CodigosIcp IN VARCHAR2,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
BEGIN
    UPDATE BMC.BM_CONTEO
       SET TITULO = p_Titulo,
           COMENTARIO = p_Comentario,
           CODIGO_PERSONA_RESPONSABLE = p_CodigoPersonaResp,
           CANTIDAD_CONTEOS_ID = p_ConteoId,
           FECHA = NVL(p_Fecha, FECHA),
           FECHA_UPD = SYSDATE
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND CODIGO_BM_CONTEO = p_CodigoBmConteo;

    BMC.SP_BM_CONTEO_GET_ALL(p_CodigoEmpresa, p_ResultSet, p_Message, p_TotalRecords);
EXCEPTION
    WHEN OTHERS THEN
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT * FROM BMC.BM_CONTEO WHERE 1 = 0;
END SP_BM_CONTEO_UPD;
/

CREATE OR REPLACE PROCEDURE BMC.SP_BM_CONTEO_DEL (
    p_CodigoEmpresa IN NUMBER,
    p_CodigoBmConteo IN NUMBER,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
BEGIN
    DELETE FROM BMC.BM_CONTEO_DETALLE
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND CODIGO_BM_CONTEO = p_CodigoBmConteo;

    DELETE FROM BMC.BM_CONTEO
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND CODIGO_BM_CONTEO = p_CodigoBmConteo;

    BMC.SP_BM_CONTEO_GET_ALL(p_CodigoEmpresa, p_ResultSet, p_Message, p_TotalRecords);
EXCEPTION
    WHEN OTHERS THEN
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT * FROM BMC.BM_CONTEO WHERE 1 = 0;
END SP_BM_CONTEO_DEL;
/

CREATE OR REPLACE PROCEDURE BMC.SP_BM_CONTEO_CERRAR (
    p_CodigoEmpresa IN NUMBER,
    p_CodigoBmConteo IN NUMBER,
    p_Comentario IN VARCHAR2,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
BEGIN
    INSERT INTO BMC.BM_CONTEO_HISTORICO (
        CODIGO_BM_CONTEO,
        TITULO,
        CODIGO_PERSONA_RESPONSABLE,
        CANTIDAD_CONTEOS_ID,
        FECHA,
        USUARIO_INS,
        FECHA_INS,
        USUARIO_UPD,
        FECHA_UPD,
        CODIGO_EMPRESA,
        COMENTARIO,
        USUARIO_CIERRE,
        FECHA_CIERRE,
        TOTAL_CANTIDAD,
        TOTAL_CANTIDAD_CONTADA,
        TOTAL_DIFERENCIA
    )
    SELECT C.CODIGO_BM_CONTEO,
           C.TITULO,
           C.CODIGO_PERSONA_RESPONSABLE,
           C.CANTIDAD_CONTEOS_ID,
           C.FECHA,
           C.USUARIO_INS,
           C.FECHA_INS,
           C.USUARIO_UPD,
           C.FECHA_UPD,
           C.CODIGO_EMPRESA,
           NVL(p_Comentario, C.COMENTARIO),
           -1,
           SYSDATE,
           NVL(SUM(D.CANTIDAD), 0),
           NVL(SUM(D.CANTIDAD_CONTADA), 0),
           NVL(SUM(D.DIFERENCIA), 0)
      FROM BMC.BM_CONTEO C,
           BMC.BM_CONTEO_DETALLE D
     WHERE C.CODIGO_EMPRESA = p_CodigoEmpresa
       AND C.CODIGO_BM_CONTEO = p_CodigoBmConteo
       AND D.CODIGO_BM_CONTEO(+) = C.CODIGO_BM_CONTEO
     GROUP BY C.CODIGO_BM_CONTEO,
              C.TITULO,
              C.CODIGO_PERSONA_RESPONSABLE,
              C.CANTIDAD_CONTEOS_ID,
              C.FECHA,
              C.USUARIO_INS,
              C.FECHA_INS,
              C.USUARIO_UPD,
              C.FECHA_UPD,
              C.CODIGO_EMPRESA,
              C.COMENTARIO;

    INSERT INTO BMC.BM_CONTEO_DETALLE_HISTORICO
    SELECT CODIGO_BM_CONTEO,
           CONTEO,
           CODIGO_ICP,
           UNIDAD_TRABAJO,
           CODIGO_GRUPO,
           CODIGO_NIVEL1,
           CODIGO_NIVEL2,
           NUMERO_LOTE,
           CANTIDAD,
           NUMERO_PLACA,
           VALOR_ACTUAL,
           ARTICULO,
           ESPECIFICACION,
           SERVICIO,
           RESPONSABLE_BIEN,
           FECHA_MOVIMIENTO,
           CODIGO_BIEN,
           CODIGO_MOV_BIEN,
           CANTIDAD_CONTADA,
           DIFERENCIA,
           CODIGO_EMPRESA,
           USUARIO_INS,
           FECHA_INS,
           USUARIO_UPD,
           FECHA_UPD,
           COMENTARIO,
           REPLICAR_MOTIVO,
           CODIGO_BM_CONTEO_MOTIVO
      FROM BMC.BM_CONTEO_DETALLE
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND CODIGO_BM_CONTEO = p_CodigoBmConteo;

    DELETE FROM BMC.BM_CONTEO_DETALLE
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND CODIGO_BM_CONTEO = p_CodigoBmConteo;

    DELETE FROM BMC.BM_CONTEO
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND CODIGO_BM_CONTEO = p_CodigoBmConteo;

    BMC.SP_BM_CONTEO_GET_ALL(p_CodigoEmpresa, p_ResultSet, p_Message, p_TotalRecords);
EXCEPTION
    WHEN DUP_VAL_ON_INDEX THEN
        p_TotalRecords := 0;
        p_Message := 'El conteo ya fue cerrado.';
        OPEN p_ResultSet FOR SELECT * FROM BMC.BM_CONTEO WHERE 1 = 0;
    WHEN OTHERS THEN
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT * FROM BMC.BM_CONTEO WHERE 1 = 0;
END SP_BM_CONTEO_CERRAR;
/

CREATE OR REPLACE PROCEDURE BMC.SP_BM_CONT_DET_GET (
    p_CodigoEmpresa IN NUMBER,
    p_CodigoBmConteo IN NUMBER,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
BEGIN
    SELECT COUNT(*)
      INTO p_TotalRecords
      FROM BMC.BM_CONTEO_DETALLE D
     WHERE D.CODIGO_EMPRESA = p_CodigoEmpresa
       AND D.CODIGO_BM_CONTEO = p_CodigoBmConteo;

    OPEN p_ResultSet FOR
        SELECT D.CODIGO_BM_CONTEO_DETALLE,
               D.CODIGO_BM_CONTEO,
               D.CONTEO,
               D.CODIGO_ICP,
               D.UNIDAD_TRABAJO,
               D.COMENTARIO,
               D.NUMERO_PLACA CODIGO_PLACA,
               D.CANTIDAD,
               D.CANTIDAD_CONTADA,
               0 CANTIDAD_CONTADA_OTRO,
               D.DIFERENCIA,
               D.CODIGO_GRUPO,
               D.CODIGO_NIVEL1,
               D.CODIGO_NIVEL2,
               D.NUMERO_LOTE,
               D.NUMERO_PLACA,
               D.VALOR_ACTUAL,
               D.ARTICULO,
               D.ESPECIFICACION,
               D.SERVICIO,
               D.RESPONSABLE_BIEN,
               D.FECHA_MOVIMIENTO,
               D.CODIGO_BIEN,
               D.CODIGO_MOV_BIEN,
               C.FECHA,
               NVL(D.REPLICAR_MOTIVO, 0) REPLICAR_COMENTARIO
          FROM BMC.BM_CONTEO_DETALLE D,
               BMC.BM_CONTEO C
         WHERE D.CODIGO_EMPRESA = p_CodigoEmpresa
           AND D.CODIGO_BM_CONTEO = p_CodigoBmConteo
           AND C.CODIGO_BM_CONTEO(+) = D.CODIGO_BM_CONTEO
         ORDER BY D.UNIDAD_TRABAJO, D.ARTICULO, D.NUMERO_PLACA, D.CONTEO;

    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT * FROM BMC.BM_CONTEO_DETALLE WHERE 1 = 0;
END SP_BM_CONT_DET_GET;
/

CREATE OR REPLACE PROCEDURE BMC.SP_BM_CONT_DET_CMP (
    p_CodigoEmpresa IN NUMBER,
    p_CodigoBmConteo IN NUMBER,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
BEGIN
    BMC.SP_BM_CONT_DET_GET(p_CodigoEmpresa, p_CodigoBmConteo, p_ResultSet, p_Message, p_TotalRecords);
END SP_BM_CONT_DET_CMP;
/

CREATE OR REPLACE PROCEDURE BMC.SP_BM_CONT_DET_UPD (
    p_CodigoEmpresa IN NUMBER,
    p_CodigoDetalle IN NUMBER,
    p_CantidadContada IN NUMBER,
    p_Comentario IN VARCHAR2,
    p_ReplicarComentario IN NUMBER,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
    v_Conteo NUMBER;
BEGIN
    SELECT CODIGO_BM_CONTEO
      INTO v_Conteo
      FROM BMC.BM_CONTEO_DETALLE
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND CODIGO_BM_CONTEO_DETALLE = p_CodigoDetalle;

    UPDATE BMC.BM_CONTEO_DETALLE
       SET CANTIDAD_CONTADA = p_CantidadContada,
           DIFERENCIA = NVL(CANTIDAD, 0) - NVL(p_CantidadContada, 0),
           COMENTARIO = p_Comentario,
           REPLICAR_MOTIVO = p_ReplicarComentario,
           FECHA_UPD = SYSDATE
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND CODIGO_BM_CONTEO_DETALLE = p_CodigoDetalle;

    IF p_ReplicarComentario = 1 THEN
        UPDATE BMC.BM_CONTEO_DETALLE
           SET COMENTARIO = p_Comentario,
               REPLICAR_MOTIVO = p_ReplicarComentario,
               FECHA_UPD = SYSDATE
         WHERE CODIGO_EMPRESA = p_CodigoEmpresa
           AND CODIGO_BM_CONTEO = v_Conteo;
    END IF;

    BMC.SP_BM_CONT_DET_GET(p_CodigoEmpresa, v_Conteo, p_ResultSet, p_Message, p_TotalRecords);
EXCEPTION
    WHEN OTHERS THEN
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT * FROM BMC.BM_CONTEO_DETALLE WHERE 1 = 0;
END SP_BM_CONT_DET_UPD;
/

CREATE OR REPLACE PROCEDURE BMC.SP_BM_CONT_DET_REC (
    p_CodigoEmpresa IN NUMBER,
    p_ItemsCsv IN CLOB,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
BEGIN
    -- Primer corte: la recepcion movil se confirma como exito y la comparacion
    -- queda disponible desde el detalle del conteo. La normalizacion por lote
    -- de p_ItemsCsv debe cerrarse con el formato movil definitivo.
    p_TotalRecords := 0;
    p_Message := 'Success';
    OPEN p_ResultSet FOR
        SELECT CAST(NULL AS NUMBER) CODIGO_BM_CONTEO_DETALLE,
               CAST(NULL AS NUMBER) CODIGO_BM_CONTEO,
               CAST(NULL AS NUMBER) CONTEO,
               CAST(NULL AS NUMBER) CODIGO_ICP,
               CAST(NULL AS VARCHAR2(200)) UNIDAD_TRABAJO,
               CAST(NULL AS VARCHAR2(4000)) COMENTARIO,
               CAST(NULL AS VARCHAR2(4000)) CODIGO_PLACA,
               CAST(NULL AS NUMBER) CANTIDAD,
               CAST(NULL AS NUMBER) CANTIDAD_CONTADA,
               CAST(NULL AS NUMBER) CANTIDAD_CONTADA_OTRO,
               CAST(NULL AS NUMBER) DIFERENCIA,
               CAST(NULL AS VARCHAR2(1)) CODIGO_GRUPO,
               CAST(NULL AS VARCHAR2(2)) CODIGO_NIVEL1,
               CAST(NULL AS VARCHAR2(2)) CODIGO_NIVEL2,
               CAST(NULL AS VARCHAR2(20)) NUMERO_LOTE,
               CAST(NULL AS VARCHAR2(4000)) NUMERO_PLACA,
               CAST(NULL AS NUMBER) VALOR_ACTUAL,
               CAST(NULL AS VARCHAR2(200)) ARTICULO,
               CAST(NULL AS VARCHAR2(4000)) ESPECIFICACION,
               CAST(NULL AS VARCHAR2(100)) SERVICIO,
               CAST(NULL AS VARCHAR2(4000)) RESPONSABLE_BIEN,
               CAST(NULL AS DATE) FECHA_MOVIMIENTO,
               CAST(NULL AS NUMBER) CODIGO_BIEN,
               CAST(NULL AS NUMBER) CODIGO_MOV_BIEN,
               CAST(NULL AS DATE) FECHA,
               CAST(NULL AS NUMBER) REPLICAR_COMENTARIO
          FROM DUAL
         WHERE 1 = 0;
EXCEPTION
    WHEN OTHERS THEN
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT * FROM BMC.BM_CONTEO_DETALLE WHERE 1 = 0;
END SP_BM_CONT_DET_REC;
/

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
               '' NOMBRE_PERSONA_RESPONSABLE,
               H.CANTIDAD_CONTEOS_ID CONTEO_ID,
               H.FECHA,
               H.CANTIDAD_CONTEOS_ID CONTEO,
               H.TOTAL_CANTIDAD,
               H.TOTAL_CANTIDAD_CONTADA,
               H.TOTAL_DIFERENCIA
          FROM BMC.BM_CONTEO_HISTORICO H
         WHERE H.CODIGO_EMPRESA = p_CodigoEmpresa
         ORDER BY H.FECHA_CIERRE DESC;

    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT * FROM BMC.BM_CONTEO_HISTORICO WHERE 1 = 0;
END SP_BM_CONT_HIST_GET;
/

CREATE OR REPLACE PROCEDURE BMC.SP_BM_UBI_RESP_GET (
    p_CodigoEmpresa IN NUMBER,
    p_CodigoUsuario IN NUMBER,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
BEGIN
    SELECT COUNT(*)
      INTO p_TotalRecords
      FROM BMC.BM_V_UBICA_RESPONSABLE V
     WHERE p_CodigoUsuario = 0 OR V.CODIGO_USUARIO = p_CodigoUsuario;

    OPEN p_ResultSet FOR
        SELECT V.CODIGO_BM_CONTEO,
               V.CONTEO,
               V.TITULO,
               V.CODIGO_DIR_BIEN,
               V.CODIGO_ICP,
               V.UNIDAD_TRABAJO UNIDAD_EJECUTORA,
               V.CODIGO_USUARIO,
               V.CODIGO_PERSONA,
               V.LOGIN,
               V.CEDULA,
               V.CODIGO_BM_CONTEO || '-' || V.CONTEO || '-' || V.UNIDAD_TRABAJO DESCRIPCION,
               V.CODIGO_BM_CONTEO || '-' || V.CONTEO || '-' || V.UNIDAD_TRABAJO KEY_UBICACION_RESPONSABLE
          FROM BMC.BM_V_UBICA_RESPONSABLE V
         WHERE p_CodigoUsuario = 0 OR V.CODIGO_USUARIO = p_CodigoUsuario
         ORDER BY V.TITULO, V.UNIDAD_TRABAJO;

    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR
            SELECT CAST(NULL AS NUMBER) CODIGO_BM_CONTEO,
                   CAST(NULL AS NUMBER) CONTEO,
                   CAST(NULL AS VARCHAR2(100)) TITULO,
                   CAST(NULL AS NUMBER) CODIGO_DIR_BIEN,
                   CAST(NULL AS NUMBER) CODIGO_ICP,
                   CAST(NULL AS VARCHAR2(200)) UNIDAD_EJECUTORA,
                   CAST(NULL AS NUMBER) CODIGO_USUARIO,
                   CAST(NULL AS NUMBER) CODIGO_PERSONA,
                   CAST(NULL AS VARCHAR2(100)) LOGIN,
                   CAST(NULL AS NUMBER) CEDULA,
                   CAST(NULL AS VARCHAR2(4000)) DESCRIPCION,
                   CAST(NULL AS VARCHAR2(4000)) KEY_UBICACION_RESPONSABLE
              FROM DUAL
             WHERE 1 = 0;
END SP_BM_UBI_RESP_GET;
/
