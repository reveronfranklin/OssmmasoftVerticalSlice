CREATE OR REPLACE PROCEDURE RH.SP_REP_GRAL_NOM_FIR_GET_ALL (
    p_ResultSet      OUT SYS_REFCURSOR,
    p_Message        OUT VARCHAR2,
    p_TotalRecords   OUT NUMBER
) AS
BEGIN
    SELECT COUNT(*)
      INTO p_TotalRecords
      FROM (
        SELECT UNIQUE
               SUBSTR(RD.NUMERO_DOCUMENTO, 1, 1) OFICINA,
               RD.NUMERO_DOCUMENTO ORDEN,
               RHPC.CODIGO_PERSONA,
               RHPC.NOMBRE,
               RHPC.APELLIDO,
               RHPC.CEDULA,
               RHPC.DESCRIPCION_CARGO
          FROM RH_V_PERSONAL_CARGO RHPC,
               RH_DOCUMENTOS RD
         WHERE RHPC.CODIGO_PERSONA = RD.CODIGO_PERSONA
           AND RD.TIPO_DOCUMENTO_ID = (
                SELECT RD1.DESCRIPCION_ID
                  FROM RH_DESCRIPTIVAS RD1
                 WHERE RD1.DESCRIPCION_ID = RD.TIPO_DOCUMENTO_ID
                   AND RD1.CODIGO = 'FA'
           )
           AND RD.FECHA_VENCIMIENTO IS NULL
      );

    OPEN p_ResultSet FOR
        SELECT UNIQUE
               SUBSTR(RD.NUMERO_DOCUMENTO, 1, 1) OFICINA,
               RD.NUMERO_DOCUMENTO ORDEN,
               RHPC.CODIGO_PERSONA,
               RHPC.NOMBRE,
               RHPC.APELLIDO,
               RHPC.CEDULA,
               RHPC.DESCRIPCION_CARGO
          FROM RH_V_PERSONAL_CARGO RHPC,
               RH_DOCUMENTOS RD
         WHERE RHPC.CODIGO_PERSONA = RD.CODIGO_PERSONA
           AND RD.TIPO_DOCUMENTO_ID = (
                SELECT RD1.DESCRIPCION_ID
                  FROM RH_DESCRIPTIVAS RD1
                 WHERE RD1.DESCRIPCION_ID = RD.TIPO_DOCUMENTO_ID
                   AND RD1.CODIGO = 'FA'
           )
           AND RD.FECHA_VENCIMIENTO IS NULL
         ORDER BY 2;

    p_Message := 'Success';

EXCEPTION
    WHEN OTHERS THEN
        p_Message := SUBSTR(SQLERRM, 1, 4000);
        p_TotalRecords := 0;
        OPEN p_ResultSet FOR
            SELECT
                CAST(NULL AS VARCHAR2(10)) OFICINA,
                CAST(NULL AS VARCHAR2(100)) ORDEN,
                CAST(NULL AS NUMBER) CODIGO_PERSONA,
                CAST(NULL AS VARCHAR2(4000)) NOMBRE,
                CAST(NULL AS VARCHAR2(4000)) APELLIDO,
                CAST(NULL AS VARCHAR2(100)) CEDULA,
                CAST(NULL AS VARCHAR2(4000)) DESCRIPCION_CARGO
            FROM DUAL
            WHERE 1 = 0;
END SP_REP_GRAL_NOM_FIR_GET_ALL;
/
