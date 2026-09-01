-- Solo permite eliminar conteos no iniciados y elimina primero su detalle.
CREATE OR REPLACE PROCEDURE BMC.SP_BM_CONTEO_DEL (
    p_CodigoEmpresa IN NUMBER,
    p_CodigoBmConteo IN NUMBER,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
    v_Existe NUMBER;
    v_Iniciados NUMBER;
    v_Restantes NUMBER;
BEGIN
    SAVEPOINT SP_BM_CONTEO_DEL_START;

    SELECT COUNT(*)
      INTO v_Existe
      FROM BMC.BM_CONTEO
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND CODIGO_BM_CONTEO = p_CodigoBmConteo;

    IF v_Existe = 0 THEN
        RAISE_APPLICATION_ERROR(-20003, 'El conteo no existe');
    END IF;

    SELECT COUNT(*)
      INTO v_Iniciados
      FROM BMC.BM_CONTEO_DETALLE
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND CODIGO_BM_CONTEO = p_CodigoBmConteo
       AND NVL(CANTIDAD_CONTADA, 0) > 0;

    IF v_Iniciados > 0 THEN
        RAISE_APPLICATION_ERROR(-20004, 'No puede eliminar un conteo iniciado');
    END IF;

    DELETE FROM BMC.BM_CONTEO_DETALLE
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND CODIGO_BM_CONTEO = p_CodigoBmConteo;

    SELECT COUNT(*)
      INTO v_Restantes
      FROM BMC.BM_CONTEO_DETALLE
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND CODIGO_BM_CONTEO = p_CodigoBmConteo;

    IF v_Restantes > 0 THEN
        RAISE_APPLICATION_ERROR(-20005, 'No se pudo eliminar el detalle del conteo');
    END IF;

    DELETE FROM BMC.BM_CONTEO
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND CODIGO_BM_CONTEO = p_CodigoBmConteo;

    BMC.SP_BM_CONTEO_GET_ALL(
        p_CodigoEmpresa,
        p_ResultSet,
        p_Message,
        p_TotalRecords
    );
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK TO SP_BM_CONTEO_DEL_START;
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT * FROM BMC.BM_CONTEO WHERE 1 = 0;
END SP_BM_CONTEO_DEL;
/

SHOW ERRORS PROCEDURE BMC.SP_BM_CONTEO_DEL;
