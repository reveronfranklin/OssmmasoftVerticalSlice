CREATE OR REPLACE PROCEDURE RH.SP_RH_DOC_GET_ALL (
    p_PageSize      IN NUMBER,
    p_PageNumber    IN NUMBER,
    p_SearchText    IN VARCHAR2,
    p_CODIGO_EMPRESA IN NUMBER,
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
      FROM RH.RH_DOCUMENTOS d
      LEFT JOIN RH.RH_PERSONAS p
        ON p.CODIGO_PERSONA = d.CODIGO_PERSONA
       AND p.CODIGO_EMPRESA = p_CODIGO_EMPRESA
      LEFT JOIN RH.RH_DESCRIPTIVAS td
        ON td.DESCRIPCION_ID = d.TIPO_DOCUMENTO_ID
      LEFT JOIN RH.RH_DESCRIPTIVAS tg
        ON tg.DESCRIPCION_ID = d.TIPO_GRADO_ID
      LEFT JOIN RH.RH_DESCRIPTIVAS g
        ON g.DESCRIPCION_ID = d.GRADO_ID
     WHERE d.CODIGO_EMPRESA = p_CODIGO_EMPRESA
       AND (v_search IS NULL
        OR v_search = ''
        OR UPPER(NVL(d.NUMERO_DOCUMENTO, '')) LIKE '%' || v_search || '%'
        OR UPPER(NVL(d.EXTRA1, '')) LIKE '%' || v_search || '%'
        OR UPPER(NVL(d.EXTRA2, '')) LIKE '%' || v_search || '%'
        OR UPPER(NVL(d.EXTRA3, '')) LIKE '%' || v_search || '%'
        OR UPPER(NVL(td.DESCRIPCION, '')) LIKE '%' || v_search || '%'
        OR UPPER(NVL(tg.DESCRIPCION, '')) LIKE '%' || v_search || '%'
        OR UPPER(NVL(g.DESCRIPCION, '')) LIKE '%' || v_search || '%'
        OR UPPER(NVL(p.NOMBRE, '')) LIKE '%' || v_search || '%'
        OR UPPER(NVL(p.APELLIDO, '')) LIKE '%' || v_search || '%');

    p_TotalPages := CASE
        WHEN p_TotalRecords = 0 THEN 0
        ELSE CEIL(p_TotalRecords / v_page_size)
    END;

    OPEN p_ResultSet FOR
        SELECT CODIGO_PERSONA,
               CODIGO_DOCUMENTO,
               TIPO_DOCUMENTO_ID,
               TIPO_DOCUMENTO,
               NUMERO_DOCUMENTO,
               FECHA_VENCIMIENTO,
               TIPO_GRADO_ID,
               TIPO_GRADO,
               GRADO_ID,
               GRADO,
               EXTRA1,
               EXTRA2,
               EXTRA3,
               USUARIO_INS,
               FECHA_INS,
               USUARIO_UPD,
               FECHA_UPD,
               CODIGO_EMPRESA,
               PERSONA
          FROM (
                SELECT d.CODIGO_PERSONA,
                       d.CODIGO_DOCUMENTO,
                       d.TIPO_DOCUMENTO_ID,
                       NVL(td.DESCRIPCION, '') TIPO_DOCUMENTO,
                       d.NUMERO_DOCUMENTO,
                       d.FECHA_VENCIMIENTO,
                       d.TIPO_GRADO_ID,
                       NVL(tg.DESCRIPCION, '') TIPO_GRADO,
                       d.GRADO_ID,
                       NVL(g.DESCRIPCION, '') GRADO,
                       d.EXTRA1,
                       d.EXTRA2,
                       d.EXTRA3,
                       d.USUARIO_INS,
                       d.FECHA_INS,
                       d.USUARIO_UPD,
                       d.FECHA_UPD,
                       d.CODIGO_EMPRESA,
                       TRIM(NVL(p.NOMBRE, '') || ' ' || NVL(p.APELLIDO, '')) PERSONA,
                       ROW_NUMBER() OVER (ORDER BY d.CODIGO_DOCUMENTO DESC) RN
                  FROM RH.RH_DOCUMENTOS d
                  LEFT JOIN RH.RH_PERSONAS p
                    ON p.CODIGO_PERSONA = d.CODIGO_PERSONA
                   AND p.CODIGO_EMPRESA = p_CODIGO_EMPRESA
                  LEFT JOIN RH.RH_DESCRIPTIVAS td
                    ON td.DESCRIPCION_ID = d.TIPO_DOCUMENTO_ID
                  LEFT JOIN RH.RH_DESCRIPTIVAS tg
                    ON tg.DESCRIPCION_ID = d.TIPO_GRADO_ID
                  LEFT JOIN RH.RH_DESCRIPTIVAS g
                    ON g.DESCRIPCION_ID = d.GRADO_ID
                 WHERE d.CODIGO_EMPRESA = p_CODIGO_EMPRESA
                   AND (v_search IS NULL
                    OR v_search = ''
                    OR UPPER(NVL(d.NUMERO_DOCUMENTO, '')) LIKE '%' || v_search || '%'
                    OR UPPER(NVL(d.EXTRA1, '')) LIKE '%' || v_search || '%'
                    OR UPPER(NVL(d.EXTRA2, '')) LIKE '%' || v_search || '%'
                    OR UPPER(NVL(d.EXTRA3, '')) LIKE '%' || v_search || '%'
                    OR UPPER(NVL(td.DESCRIPCION, '')) LIKE '%' || v_search || '%'
                    OR UPPER(NVL(tg.DESCRIPCION, '')) LIKE '%' || v_search || '%'
                    OR UPPER(NVL(g.DESCRIPCION, '')) LIKE '%' || v_search || '%'
                    OR UPPER(NVL(p.NOMBRE, '')) LIKE '%' || v_search || '%'
                    OR UPPER(NVL(p.APELLIDO, '')) LIKE '%' || v_search || '%')
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
            SELECT CAST(NULL AS NUMBER) CODIGO_PERSONA,
                   CAST(NULL AS NUMBER) CODIGO_DOCUMENTO,
                   CAST(NULL AS NUMBER) TIPO_DOCUMENTO_ID,
                   CAST(NULL AS VARCHAR2(100)) TIPO_DOCUMENTO,
                   CAST(NULL AS VARCHAR2(20)) NUMERO_DOCUMENTO,
                   CAST(NULL AS DATE) FECHA_VENCIMIENTO,
                   CAST(NULL AS NUMBER) TIPO_GRADO_ID,
                   CAST(NULL AS VARCHAR2(100)) TIPO_GRADO,
                   CAST(NULL AS NUMBER) GRADO_ID,
                   CAST(NULL AS VARCHAR2(100)) GRADO,
                   CAST(NULL AS VARCHAR2(100)) EXTRA1,
                   CAST(NULL AS VARCHAR2(100)) EXTRA2,
                   CAST(NULL AS VARCHAR2(100)) EXTRA3,
                   CAST(NULL AS NUMBER) USUARIO_INS,
                   CAST(NULL AS DATE) FECHA_INS,
                   CAST(NULL AS NUMBER) USUARIO_UPD,
                   CAST(NULL AS DATE) FECHA_UPD,
                   CAST(NULL AS NUMBER) CODIGO_EMPRESA,
                   CAST(NULL AS VARCHAR2(101)) PERSONA
              FROM DUAL
             WHERE 1 = 0;
END SP_RH_DOC_GET_ALL;
/
