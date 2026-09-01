-- Restaura el contrato legacy del filtro de conteos por usuario responsable.
CREATE OR REPLACE PROCEDURE BMC.SP_BM_UBI_RESP_GET (
    p_CodigoEmpresa IN NUMBER,
    p_UsuarioResponsable IN VARCHAR2,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
BEGIN
    SELECT COUNT(*)
      INTO p_TotalRecords
      FROM BMC.BM_V_UBICA_RESPONSABLE V
     WHERE UPPER(V.LOGIN) = UPPER(TRIM(p_UsuarioResponsable));

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
               'Conteo:' || V.CONTEO || '-' || V.UNIDAD_TRABAJO DESCRIPCION,
               V.CODIGO_BM_CONTEO || '-' || V.CONTEO || '-' || V.CODIGO_DIR_BIEN KEY_UBICACION_RESPONSABLE
          FROM BMC.BM_V_UBICA_RESPONSABLE V
         WHERE UPPER(V.LOGIN) = UPPER(TRIM(p_UsuarioResponsable))
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

SHOW ERRORS PROCEDURE BMC.SP_BM_UBI_RESP_GET;
