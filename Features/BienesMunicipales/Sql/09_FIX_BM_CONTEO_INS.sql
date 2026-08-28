-- Corrige la creacion de conteos: CANTIDAD_CONTEOS_ID contiene el ID de la
-- descriptiva, no el numero de iteraciones que recibe BM_P_CONTEO.
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
    BMC.SP_BM_CONTEO_GET_ALL(
        p_CodigoEmpresa,
        p_ResultSet,
        p_Message,
        p_TotalRecords
    );
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

SHOW ERRORS PROCEDURE BMC.SP_BM_CONTEO_INS;
