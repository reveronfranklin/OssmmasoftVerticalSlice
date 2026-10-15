-- Instalar DESPUES de SP_BM_FOTO_GALERIA.sql, como BM.
-- Nuevos identificadores: BM_FOTO_OMIT(12), SP_BM_FOTO_SYNC(15).
-- Resto: CODIGO_EMPRESA(14), CODIGO_BIEN(11), FOTO(4), FECHA_OMIT(10).
DECLARE
    v_Existe NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_Existe FROM ALL_TABLES
     WHERE OWNER = 'BM' AND TABLE_NAME = 'BM_FOTO_OMIT';
    IF v_Existe = 0 THEN
        EXECUTE IMMEDIATE 'CREATE TABLE BM.BM_FOTO_OMIT (
            CODIGO_EMPRESA NUMBER(10) NOT NULL,
            CODIGO_BIEN NUMBER(10) NOT NULL,
            FOTO VARCHAR2(500) NOT NULL,
            FECHA_OMIT DATE DEFAULT SYSDATE NOT NULL)';
    END IF;
END;
/
CREATE OR REPLACE PROCEDURE BM.SP_BM_FOTO_SYNC (
    p_CodigoEmpresa IN NUMBER,
    p_NumeroPlaca IN VARCHAR2,
    p_Foto IN VARCHAR2,
    p_Agregadas OUT NUMBER
) AS
    v_Bien NUMBER;
    v_Count NUMBER;
    v_Codigo NUMBER;
BEGIN
    p_Agregadas := 0;
    -- Solo una placa canonica inequivoca de la empresa configurada.
    SELECT MIN(CODIGO_BIEN), COUNT(*) INTO v_Bien, v_Count
      FROM BM.BM_BIENES WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND NUMERO_PLACA = p_NumeroPlaca;
    IF v_Count <> 1 OR p_Foto IS NULL OR LENGTHB(p_Foto) > 500 THEN RETURN; END IF;

    -- Sin esperar a otro escritor; se volvera a intentar en otra consulta.
    LOCK TABLE BM.BM_BIENES_FOTO IN EXCLUSIVE MODE NOWAIT;
    SELECT COUNT(*) INTO v_Count FROM BM.BM_BIENES_FOTO
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa AND CODIGO_BIEN = v_Bien
       AND UPPER(REGEXP_REPLACE(REGEXP_REPLACE(REPLACE(FOTO, CHR(92), '/'),
           '^.*/', ''), '^min_', '', 1, 1, 'i')) = UPPER(p_Foto);
    IF v_Count > 0 THEN ROLLBACK; RETURN; END IF;
    SELECT COUNT(*) INTO v_Count FROM BM.BM_FOTO_OMIT
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa AND CODIGO_BIEN = v_Bien
       AND UPPER(FOTO) = UPPER(p_Foto);
    IF v_Count > 0 THEN ROLLBACK; RETURN; END IF;

    SELECT NVL(MAX(CODIGO_BIEN_FOTO), 0) + 1 INTO v_Codigo FROM BM.BM_BIENES_FOTO;
    INSERT INTO BM.BM_BIENES_FOTO
        (CODIGO_BIEN_FOTO, CODIGO_BIEN, NUMERO_PLACA, FOTO, TITULO, CODIGO_EMPRESA)
    VALUES (v_Codigo, v_Bien, p_NumeroPlaca, p_Foto, NULL, p_CodigoEmpresa);
    -- No inventar fecha de carga ni reemplazar titulos/principal historicos.
    COMMIT;
    p_Agregadas := 1;
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        RAISE;
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
    v_Foto VARCHAR2(500);
BEGIN
    LOCK TABLE BM.BM_BIENES_FOTO IN EXCLUSIVE MODE;
    SELECT NUMERO_PLACA, CODIGO_BIEN, ES_PRINCIPAL, FOTO
      INTO v_NumeroPlaca, v_Bien, v_Principal, v_Foto
      FROM BM.BM_BIENES_FOTO
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND CODIGO_BIEN_FOTO = p_CodigoBienFoto;

    v_Foto := REGEXP_REPLACE(REGEXP_REPLACE(REPLACE(v_Foto, CHR(92), '/'),
        '^.*/', ''), '^min_', '', 1, 1, 'i');
    INSERT INTO BM.BM_FOTO_OMIT (CODIGO_EMPRESA, CODIGO_BIEN, FOTO)
    SELECT p_CodigoEmpresa, v_Bien, v_Foto FROM DUAL
     WHERE NOT EXISTS (SELECT 1 FROM BM.BM_FOTO_OMIT
       WHERE CODIGO_EMPRESA = p_CodigoEmpresa AND CODIGO_BIEN = v_Bien
         AND UPPER(FOTO) = UPPER(v_Foto));

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

-- Conceder EXECUTE sobre SP_BM_FOTO_SYNC al usuario runtime, nunca a PUBLIC.
DECLARE
    v_Count NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_Count FROM ALL_ERRORS
     WHERE OWNER = 'BM' AND TYPE = 'PROCEDURE' AND ATTRIBUTE = 'ERROR'
       AND NAME IN ('SP_BM_FOTO_SYNC', 'SP_BM_FOTO_DEL');
    IF v_Count > 0 THEN
        RAISE_APPLICATION_ERROR(-20003, 'Revisar ALL_ERRORS de sincronizacion de fotos.');
    END IF;
END;
/
