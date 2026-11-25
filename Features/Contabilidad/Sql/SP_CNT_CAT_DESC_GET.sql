CREATE OR REPLACE PROCEDURE CNT.SP_CNT_CAT_DESC_GET (
    p_TITULO_ID      IN NUMBER,
    p_SEARCH_TEXT    IN VARCHAR2,
    p_CODIGO_EMPRESA IN NUMBER,
    p_ResultSet      OUT SYS_REFCURSOR,
    p_Message        OUT VARCHAR2,
    p_CODIGO_TITULO  IN VARCHAR2 DEFAULT NULL
) AS
BEGIN
    IF p_CODIGO_TITULO IS NOT NULL THEN
        OPEN p_ResultSet FOR
            SELECT A.DESCRIPCION_ID AS ID,
                   A.CODIGO,
                   A.DESCRIPCION,
                   A.EXTRA1,
                   A.EXTRA2,
                   A.EXTRA3
              FROM CNT.CNT_DESCRIPTIVAS A,
                   CNT.CNT_TITULOS B
             WHERE B.CODIGO = p_CODIGO_TITULO
               AND A.TITULO_ID = B.TITULO_ID
               AND (p_SEARCH_TEXT IS NULL
                    OR UPPER(A.CODIGO) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%'
                    OR UPPER(A.DESCRIPCION) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%')
             ORDER BY CASE WHEN p_CODIGO_TITULO = 'ODC'
                           THEN A.DESCRIPCION ELSE A.CODIGO END;
    ELSE
        OPEN p_ResultSet FOR
            SELECT DESCRIPCION_ID AS ID,
                   CODIGO,
                   DESCRIPCION,
                   EXTRA1,
                   EXTRA2,
                   EXTRA3
              FROM CNT.CNT_DESCRIPTIVAS
             WHERE TITULO_ID = p_TITULO_ID
               AND (CODIGO_EMPRESA IS NULL OR CODIGO_EMPRESA = p_CODIGO_EMPRESA)
               AND (p_SEARCH_TEXT IS NULL
                    OR UPPER(CODIGO) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%'
                    OR UPPER(DESCRIPCION) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%')
             ORDER BY CODIGO, DESCRIPCION;

    END IF;

    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SQLERRM;
END;
/
