-- Requerimiento 39. Ejecutar como BM antes de desplegar API y frontend.
-- Identificadores nuevos (longitud): ES_PRINCIPAL(12), BM_FOTO_PRINC_UX(16),
-- BM_FOTO_PRINC_CK(16), SP_BM_FOTO_EDIT(15). Todos <= 30.
-- Reejecutable: no cambia FECHA_INS de fotos historicas.
DECLARE
    v_Existe NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_Existe FROM ALL_TAB_COLUMNS
     WHERE OWNER = 'BM' AND TABLE_NAME = 'BM_BIENES_FOTO'
       AND COLUMN_NAME = 'ES_PRINCIPAL';
    IF v_Existe = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE BM.BM_BIENES_FOTO ADD (ES_PRINCIPAL NUMBER(1) DEFAULT 0 NOT NULL)';
    END IF;
    SELECT COUNT(*) INTO v_Existe FROM ALL_CONSTRAINTS
     WHERE OWNER = 'BM' AND CONSTRAINT_NAME = 'BM_FOTO_PRINC_CK';
    IF v_Existe = 0 THEN
        EXECUTE IMMEDIATE 'ALTER TABLE BM.BM_BIENES_FOTO ADD CONSTRAINT BM_FOTO_PRINC_CK CHECK (ES_PRINCIPAL IN (0,1))';
    END IF;
    SELECT COUNT(*) INTO v_Existe FROM ALL_INDEXES
     WHERE OWNER = 'BM' AND INDEX_NAME = 'BM_FOTO_PRINC_UX';
    IF v_Existe = 0 THEN
        EXECUTE IMMEDIATE 'CREATE UNIQUE INDEX BM.BM_FOTO_PRINC_UX ON BM.BM_BIENES_FOTO
          (CASE WHEN ES_PRINCIPAL = 1 THEN CODIGO_EMPRESA END,
           CASE WHEN ES_PRINCIPAL = 1 THEN CODIGO_BIEN END)';
    END IF;
END;
/
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
               FECHA_INS,
               ES_PRINCIPAL,
               FOTO PATCH
          FROM BM.BM_BIENES_FOTO
         WHERE CODIGO_EMPRESA = p_CodigoEmpresa
           AND NUMERO_PLACA = p_NumeroPlaca
         ORDER BY CODIGO_BIEN_FOTO;

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
    -- Serializa tambien clientes anteriores que usan MAX+1.
    LOCK TABLE BM.BM_BIENES_FOTO IN EXCLUSIVE MODE;
    SELECT MIN(CODIGO_BIEN_FOTO) INTO v_Codigo
      FROM BM.BM_BIENES_FOTO
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa AND CODIGO_BIEN = p_CodigoBien
       AND FOTO = p_Foto;
    IF v_Codigo IS NOT NULL THEN
        COMMIT;
        BM.SP_BM_FOTO_GET_PLACA(p_CodigoEmpresa, p_NumeroPlaca,
            p_ResultSet, p_Message, p_TotalRecords);
        RETURN;
    END IF;
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
    v_Bien NUMBER;
    v_Principal NUMBER;
BEGIN
    LOCK TABLE BM.BM_BIENES_FOTO IN EXCLUSIVE MODE;
    SELECT NUMERO_PLACA, CODIGO_BIEN, ES_PRINCIPAL
      INTO v_NumeroPlaca, v_Bien, v_Principal
      FROM BM.BM_BIENES_FOTO
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND CODIGO_BIEN_FOTO = p_CodigoBienFoto;

    DELETE FROM BM.BM_BIENES_FOTO
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND CODIGO_BIEN_FOTO = p_CodigoBienFoto;

    IF v_Principal = 1 THEN
        UPDATE BM.BM_BIENES_FOTO SET ES_PRINCIPAL = 1
         WHERE CODIGO_EMPRESA = p_CodigoEmpresa AND CODIGO_BIEN = v_Bien
           AND CODIGO_BIEN_FOTO = (SELECT MIN(CODIGO_BIEN_FOTO)
             FROM BM.BM_BIENES_FOTO WHERE CODIGO_EMPRESA = p_CodigoEmpresa
               AND CODIGO_BIEN = v_Bien);
    END IF;
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
        ROLLBACK;
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

CREATE OR REPLACE PROCEDURE BM.SP_BM_FOTO_EDIT (
    p_CodigoEmpresa IN NUMBER,
    p_CodigoBienFoto IN NUMBER,
    p_Titulo IN VARCHAR2,
    p_Principal IN NUMBER,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
    v_Placa BM.BM_BIENES_FOTO.NUMERO_PLACA%TYPE;
    v_Bien NUMBER;
BEGIN
    LOCK TABLE BM.BM_BIENES_FOTO IN EXCLUSIVE MODE;
    SELECT NUMERO_PLACA, CODIGO_BIEN INTO v_Placa, v_Bien
      FROM BM.BM_BIENES_FOTO WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND CODIGO_BIEN_FOTO = p_CodigoBienFoto;
    IF p_Principal = 1 THEN
        UPDATE BM.BM_BIENES_FOTO SET ES_PRINCIPAL = 0
         WHERE CODIGO_EMPRESA = p_CodigoEmpresa AND CODIGO_BIEN = v_Bien
           AND ES_PRINCIPAL = 1;
        UPDATE BM.BM_BIENES_FOTO SET ES_PRINCIPAL = 1, FECHA_UPD = SYSDATE
         WHERE CODIGO_EMPRESA = p_CodigoEmpresa AND CODIGO_BIEN_FOTO = p_CodigoBienFoto;
    ELSE
        IF TRIM(p_Titulo) IS NULL OR LENGTHB(TRIM(p_Titulo)) > 500 THEN
            RAISE_APPLICATION_ERROR(-20001, 'Debe indicar un titulo de hasta 500 bytes.');
        END IF;
        UPDATE BM.BM_BIENES_FOTO SET TITULO = TRIM(p_Titulo), FECHA_UPD = SYSDATE
         WHERE CODIGO_EMPRESA = p_CodigoEmpresa AND CODIGO_BIEN_FOTO = p_CodigoBienFoto;
    END IF;
    COMMIT;
    BM.SP_BM_FOTO_GET_PLACA(p_CodigoEmpresa, v_Placa, p_ResultSet, p_Message, p_TotalRecords);
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        p_TotalRecords := 0;
        p_Message := 'No se pudo modificar la foto: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT NULL CODIGO_BIEN_FOTO FROM DUAL WHERE 1 = 0;
END;
/
-- Otorgar EXECUTE sobre BM.SP_BM_FOTO_EDIT al mismo usuario runtime de los
-- otros SP_BM_FOTO. No conceder a PUBLIC. Los CREATE OR REPLACE conservan grants.

-- Fallar explicitamente si un procedimiento quedo invalido tras instalar.
DECLARE
    v_Errores NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_Errores FROM ALL_ERRORS
     WHERE OWNER = 'BM' AND TYPE = 'PROCEDURE' AND ATTRIBUTE = 'ERROR'
       AND NAME IN ('SP_BM_FOTO_GET_PLACA', 'SP_BM_FOTO_INS',
                    'SP_BM_FOTO_DEL', 'SP_BM_FOTO_EDIT');
    IF v_Errores > 0 THEN
        RAISE_APPLICATION_ERROR(-20002, 'Revisar ALL_ERRORS: procedimientos de fotos invalidos.');
    END IF;
END;
/
