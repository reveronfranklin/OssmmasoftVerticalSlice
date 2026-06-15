CREATE OR REPLACE PROCEDURE SIS.SP_OSS_USR_ROL_GET_ID (
    p_CODIGO_USUARIO_ROL IN NUMBER,
    p_ResultSet          OUT SYS_REFCURSOR,
    p_Message            OUT VARCHAR2
) AS
BEGIN
    OPEN p_ResultSet FOR
        SELECT CODIGO_USUARIO_ROL,
               USUARIO,
               CODIGO_USUARIO,
               DESCRIPCION,
               DBMS_LOB.SUBSTR(JSON_MENU, 4000, 1) JSON_MENU
          FROM SIS.OSS_USUARIO_ROL
         WHERE CODIGO_USUARIO_ROL = p_CODIGO_USUARIO_ROL;

    p_Message := 'suscces';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SUBSTR(SQLERRM, 1, 4000);
        OPEN p_ResultSet FOR
            SELECT CAST(NULL AS NUMBER) CODIGO_USUARIO_ROL,
                   CAST(NULL AS VARCHAR2(50)) USUARIO,
                   CAST(NULL AS NUMBER) CODIGO_USUARIO,
                   CAST(NULL AS VARCHAR2(100)) DESCRIPCION,
                   EMPTY_CLOB() JSON_MENU
              FROM DUAL
             WHERE 1 = 0;
END SP_OSS_USR_ROL_GET_ID;
/
