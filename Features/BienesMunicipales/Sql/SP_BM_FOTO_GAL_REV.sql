-- Reverso Req. 39: ejecutar SOLO despues de revertir frontend y API.
-- Conserva ES_PRINCIPAL, indice y datos para permitir volver a desplegar.
-- Restaura las tres operaciones anteriores; el nuevo SP queda sin consumidores.
CREATE OR REPLACE PROCEDURE BM.SP_BM_FOTO_GET_PLACA (
    p_CodigoEmpresa IN NUMBER,
    p_NumeroPlaca IN VARCHAR2,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
BEGIN
    SELECT COUNT(1)
      INTO p_TotalRecords
      FROM BM.BM_BIENES_FOTO
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND NUMERO_PLACA = p_NumeroPlaca;

    OPEN p_ResultSet FOR
        SELECT CODIGO_BIEN_FOTO,
               CODIGO_BIEN,
               NUMERO_PLACA,
               FOTO,
               TITULO,
               FOTO PATCH
          FROM BM.BM_BIENES_FOTO
         WHERE CODIGO_EMPRESA = p_CodigoEmpresa
           AND NUMERO_PLACA = p_NumeroPlaca
         ORDER BY CODIGO_BIEN_FOTO DESC;

    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT NULL CODIGO_BIEN_FOTO FROM DUAL WHERE 1 = 0;
END;
/

CREATE OR REPLACE PROCEDURE BM.SP_BM_FOTO_INS (
    p_CodigoEmpresa IN NUMBER,
    p_CodigoBien IN NUMBER,
    p_NumeroPlaca IN VARCHAR2,
    p_Foto IN VARCHAR2,
    p_Titulo IN VARCHAR2,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
    v_Codigo NUMBER;
BEGIN
    SELECT NVL(MAX(CODIGO_BIEN_FOTO), 0) + 1
      INTO v_Codigo
      FROM BM.BM_BIENES_FOTO;

    INSERT INTO BM.BM_BIENES_FOTO (
        CODIGO_BIEN_FOTO,
        CODIGO_BIEN,
        NUMERO_PLACA,
        FOTO,
        TITULO,
        FECHA_INS,
        CODIGO_EMPRESA
    ) VALUES (
        v_Codigo,
        p_CodigoBien,
        p_NumeroPlaca,
        p_Foto,
        p_Titulo,
        SYSDATE,
        p_CodigoEmpresa
    );

    COMMIT;

    BM.SP_BM_FOTO_GET_PLACA(
        p_CodigoEmpresa,
        p_NumeroPlaca,
        p_ResultSet,
        p_Message,
        p_TotalRecords
    );
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT NULL CODIGO_BIEN_FOTO FROM DUAL WHERE 1 = 0;
END;
/

CREATE OR REPLACE PROCEDURE BM.SP_BM_FOTO_DEL (
    p_CodigoEmpresa IN NUMBER,
    p_CodigoBienFoto IN NUMBER,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
    v_NumeroPlaca BM.BM_BIENES_FOTO.NUMERO_PLACA%TYPE;
BEGIN
    SELECT NUMERO_PLACA
      INTO v_NumeroPlaca
      FROM BM.BM_BIENES_FOTO
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND CODIGO_BIEN_FOTO = p_CodigoBienFoto;

    DELETE FROM BM.BM_BIENES_FOTO
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND CODIGO_BIEN_FOTO = p_CodigoBienFoto;

    COMMIT;

    BM.SP_BM_FOTO_GET_PLACA(
        p_CodigoEmpresa,
        v_NumeroPlaca,
        p_ResultSet,
        p_Message,
        p_TotalRecords
    );
EXCEPTION
    WHEN NO_DATA_FOUND THEN
        p_TotalRecords := 0;
        p_Message := 'success';
        OPEN p_ResultSet FOR SELECT NULL CODIGO_BIEN_FOTO FROM DUAL WHERE 1 = 0;
    WHEN OTHERS THEN
        ROLLBACK;
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT NULL CODIGO_BIEN_FOTO FROM DUAL WHERE 1 = 0;
END;
/
