CREATE OR REPLACE PROCEDURE SIS.SP_OSS_USR_ROL_GET_ALL (
    p_PageSize      IN NUMBER,
    p_PageNumber    IN NUMBER,
    p_SearchText    IN VARCHAR2,
    p_ResultSet     OUT SYS_REFCURSOR,
    p_Message       OUT VARCHAR2,
    p_TotalRecords  OUT NUMBER,
    p_TotalPages    OUT NUMBER
) AS
    v_page_size   NUMBER := NVL(NULLIF(p_PageSize, 0), 10);
    v_page_number NUMBER := NVL(NULLIF(p_PageNumber, 0), 1);
    v_start_row   NUMBER;
    v_end_row     NUMBER;
    v_search      VARCHAR2(4000);
BEGIN
    IF v_page_size < 0 THEN
        v_page_size := 10;
    END IF;

    IF v_page_number < 0 THEN
        v_page_number := 1;
    END IF;

    v_start_row := ((v_page_number - 1) * v_page_size) + 1;
    v_end_row := v_page_number * v_page_size;
    v_search := UPPER(TRIM(NVL(p_SearchText, '')));

    SELECT COUNT(1)
      INTO p_TotalRecords
      FROM SIS.OSS_USUARIO_ROL
     WHERE v_search IS NULL
        OR v_search = ''
        OR UPPER(NVL(USUARIO, '')) LIKE '%' || v_search || '%'
        OR UPPER(NVL(DESCRIPCION, '')) LIKE '%' || v_search || '%';

    p_TotalPages := CASE
        WHEN p_TotalRecords = 0 THEN 0
        ELSE CEIL(p_TotalRecords / v_page_size)
    END;

    OPEN p_ResultSet FOR
        SELECT CODIGO_USUARIO_ROL,
               USUARIO,
               CODIGO_USUARIO,
               DESCRIPCION,
               DBMS_LOB.SUBSTR(JSON_MENU, 4000, 1) JSON_MENU
          FROM (
                SELECT CODIGO_USUARIO_ROL,
                       USUARIO,
                       CODIGO_USUARIO,
                       DESCRIPCION,
                       JSON_MENU,
                       ROW_NUMBER() OVER (ORDER BY CODIGO_USUARIO_ROL DESC) RN
                  FROM SIS.OSS_USUARIO_ROL
                 WHERE v_search IS NULL
                    OR v_search = ''
                    OR UPPER(NVL(USUARIO, '')) LIKE '%' || v_search || '%'
                    OR UPPER(NVL(DESCRIPCION, '')) LIKE '%' || v_search || '%'
               )
         WHERE RN BETWEEN v_start_row AND v_end_row
         ORDER BY RN;

    p_Message := 'suscces';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SUBSTR(SQLERRM, 1, 4000);
        p_TotalRecords := 0;
        p_TotalPages := 0;
        OPEN p_ResultSet FOR
            SELECT CAST(NULL AS NUMBER) CODIGO_USUARIO_ROL,
                   CAST(NULL AS VARCHAR2(50)) USUARIO,
                   CAST(NULL AS NUMBER) CODIGO_USUARIO,
                   CAST(NULL AS VARCHAR2(100)) DESCRIPCION,
                   EMPTY_CLOB() JSON_MENU
              FROM DUAL
             WHERE 1 = 0;
END SP_OSS_USR_ROL_GET_ALL;
/
