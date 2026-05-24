CREATE OR REPLACE PROCEDURE RH.SP_RH_DOC_GET_ID (
    p_CODIGO_DOCUMENTO IN NUMBER,
    p_CODIGO_EMPRESA   IN NUMBER,
    p_ResultSet        OUT SYS_REFCURSOR,
    p_Message          OUT VARCHAR2
) AS
BEGIN
    OPEN p_ResultSet FOR
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
               TRIM(NVL(p.NOMBRE, '') || ' ' || NVL(p.APELLIDO, '')) PERSONA
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
         WHERE d.CODIGO_DOCUMENTO = p_CODIGO_DOCUMENTO
           AND d.CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    p_Message := 'suscces';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SUBSTR(SQLERRM, 1, 4000);
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
END SP_RH_DOC_GET_ID;
/
