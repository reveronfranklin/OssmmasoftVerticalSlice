CREATE OR REPLACE PROCEDURE CNT.SP_CNT_REL_DOC_GET (
    p_TIPO_DOC_ID    IN NUMBER,
    p_TIPO_TRANS_ID  IN NUMBER,
    p_SEARCH_TEXT    IN VARCHAR2,
    p_CODIGO_EMPRESA IN NUMBER,
    p_ResultSet      OUT SYS_REFCURSOR,
    p_Message        OUT VARCHAR2
) AS
BEGIN
    OPEN p_ResultSet FOR
        SELECT r.CODIGO_RELACION_DOCUMENTO,
               r.TIPO_DOCUMENTO_ID,
               td.CODIGO AS TIPO_DOCUMENTO_CODIGO,
               td.DESCRIPCION AS TIPO_DOCUMENTO,
               td.TITULO_ID AS TIPO_DOCUMENTO_TITULO_ID,
               r.TIPO_TRANSACCION_ID,
               tt.CODIGO AS TIPO_TRANSACCION_CODIGO,
               tt.DESCRIPCION AS TIPO_TRANSACCION,
               tt.TITULO_ID AS TIPO_TRANSACCION_TITULO_ID,
               r.EXTRA1,
               r.EXTRA2,
               r.EXTRA3,
               r.CODIGO_EMPRESA
          FROM CNT.CNT_RELACION_DOCUMENTOS r,
               CNT.CNT_DESCRIPTIVAS td,
               CNT.CNT_DESCRIPTIVAS tt
         WHERE td.DESCRIPCION_ID = r.TIPO_DOCUMENTO_ID
           AND tt.DESCRIPCION_ID = r.TIPO_TRANSACCION_ID
           AND (r.CODIGO_EMPRESA IS NULL OR r.CODIGO_EMPRESA = p_CODIGO_EMPRESA)
           AND (td.CODIGO_EMPRESA IS NULL OR td.CODIGO_EMPRESA = p_CODIGO_EMPRESA)
           AND (tt.CODIGO_EMPRESA IS NULL OR tt.CODIGO_EMPRESA = p_CODIGO_EMPRESA)
           AND (p_TIPO_DOC_ID IS NULL OR r.TIPO_DOCUMENTO_ID = p_TIPO_DOC_ID)
           AND (p_TIPO_TRANS_ID IS NULL OR r.TIPO_TRANSACCION_ID = p_TIPO_TRANS_ID)
           AND (
                p_SEARCH_TEXT IS NULL
                OR UPPER(td.CODIGO) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%'
                OR UPPER(td.DESCRIPCION) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%'
                OR UPPER(tt.CODIGO) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%'
                OR UPPER(tt.DESCRIPCION) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%'
           )
         ORDER BY td.DESCRIPCION, tt.DESCRIPCION;

    p_Message := 'OK';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_REL_DOC_INS (
    p_TIPO_DOC_ID    IN NUMBER,
    p_TIPO_TRANS_ID  IN NUMBER,
    p_EXTRA1         IN VARCHAR2,
    p_EXTRA2         IN VARCHAR2,
    p_EXTRA3         IN VARCHAR2,
    p_USUARIO_ID     IN NUMBER,
    p_CODIGO_EMPRESA IN NUMBER,
    p_CODIGO_OUT     OUT NUMBER,
    p_Message        OUT VARCHAR2
) AS
    v_count NUMBER;
BEGIN
    IF p_TIPO_DOC_ID IS NULL OR p_TIPO_TRANS_ID IS NULL THEN
        p_Message := 'El tipo de documento y el tipo de transaccion son requeridos.';
        RETURN;
    END IF;

    SELECT COUNT(1) INTO v_count
      FROM CNT.CNT_DESCRIPTIVAS
     WHERE DESCRIPCION_ID = p_TIPO_DOC_ID
       AND (CODIGO_EMPRESA IS NULL OR CODIGO_EMPRESA = p_CODIGO_EMPRESA);

    IF v_count = 0 THEN
        p_Message := 'El tipo de documento indicado no existe.';
        RETURN;
    END IF;

    SELECT COUNT(1) INTO v_count
      FROM CNT.CNT_DESCRIPTIVAS
     WHERE DESCRIPCION_ID = p_TIPO_TRANS_ID
       AND (CODIGO_EMPRESA IS NULL OR CODIGO_EMPRESA = p_CODIGO_EMPRESA);

    IF v_count = 0 THEN
        p_Message := 'El tipo de transaccion indicado no existe.';
        RETURN;
    END IF;

    SELECT COUNT(1) INTO v_count
      FROM CNT.CNT_RELACION_DOCUMENTOS
     WHERE TIPO_DOCUMENTO_ID = p_TIPO_DOC_ID
       AND TIPO_TRANSACCION_ID = p_TIPO_TRANS_ID
       AND (CODIGO_EMPRESA IS NULL OR CODIGO_EMPRESA = p_CODIGO_EMPRESA);

    IF v_count > 0 THEN
        p_Message := 'Ya existe una relacion para el tipo de documento y transaccion.';
        RETURN;
    END IF;

    SELECT CNT.CNT_S_CODIGO_REL_DOC.NEXTVAL INTO p_CODIGO_OUT FROM DUAL;

    INSERT INTO CNT.CNT_RELACION_DOCUMENTOS (
        CODIGO_RELACION_DOCUMENTO,
        TIPO_DOCUMENTO_ID,
        TIPO_TRANSACCION_ID,
        EXTRA1,
        EXTRA2,
        EXTRA3,
        USUARIO_INS,
        FECHA_INS,
        CODIGO_EMPRESA
    ) VALUES (
        p_CODIGO_OUT,
        p_TIPO_DOC_ID,
        p_TIPO_TRANS_ID,
        p_EXTRA1,
        p_EXTRA2,
        p_EXTRA3,
        p_USUARIO_ID,
        SYSDATE,
        p_CODIGO_EMPRESA
    );

    p_Message := 'OK';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_REL_DOC_UPD (
    p_CODIGO_REL_DOC IN NUMBER,
    p_TIPO_DOC_ID    IN NUMBER,
    p_TIPO_TRANS_ID  IN NUMBER,
    p_EXTRA1         IN VARCHAR2,
    p_EXTRA2         IN VARCHAR2,
    p_EXTRA3         IN VARCHAR2,
    p_USUARIO_ID     IN NUMBER,
    p_CODIGO_EMPRESA IN NUMBER,
    p_Message        OUT VARCHAR2
) AS
    v_count NUMBER;
BEGIN
    IF p_CODIGO_REL_DOC IS NULL OR p_TIPO_DOC_ID IS NULL OR p_TIPO_TRANS_ID IS NULL THEN
        p_Message := 'La relacion, el tipo de documento y el tipo de transaccion son requeridos.';
        RETURN;
    END IF;

    SELECT COUNT(1) INTO v_count
      FROM CNT.CNT_RELACION_DOCUMENTOS
     WHERE CODIGO_RELACION_DOCUMENTO = p_CODIGO_REL_DOC
       AND (CODIGO_EMPRESA IS NULL OR CODIGO_EMPRESA = p_CODIGO_EMPRESA);

    IF v_count = 0 THEN
        p_Message := 'La relacion indicada no existe.';
        RETURN;
    END IF;

    SELECT COUNT(1) INTO v_count
      FROM CNT.CNT_RELACION_DOCUMENTOS
     WHERE CODIGO_RELACION_DOCUMENTO <> p_CODIGO_REL_DOC
       AND TIPO_DOCUMENTO_ID = p_TIPO_DOC_ID
       AND TIPO_TRANSACCION_ID = p_TIPO_TRANS_ID
       AND (CODIGO_EMPRESA IS NULL OR CODIGO_EMPRESA = p_CODIGO_EMPRESA);

    IF v_count > 0 THEN
        p_Message := 'Ya existe una relacion para el tipo de documento y transaccion.';
        RETURN;
    END IF;

    UPDATE CNT.CNT_RELACION_DOCUMENTOS
       SET TIPO_DOCUMENTO_ID = p_TIPO_DOC_ID,
           TIPO_TRANSACCION_ID = p_TIPO_TRANS_ID,
           EXTRA1 = p_EXTRA1,
           EXTRA2 = p_EXTRA2,
           EXTRA3 = p_EXTRA3,
           USUARIO_UPD = p_USUARIO_ID,
           FECHA_UPD = SYSDATE,
           CODIGO_EMPRESA = p_CODIGO_EMPRESA
     WHERE CODIGO_RELACION_DOCUMENTO = p_CODIGO_REL_DOC;

    p_Message := 'OK';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_REL_DOC_DEL (
    p_CODIGO_REL_DOC IN NUMBER,
    p_CODIGO_EMPRESA IN NUMBER,
    p_Message        OUT VARCHAR2
) AS
BEGIN
    DELETE FROM CNT.CNT_RELACION_DOCUMENTOS
     WHERE CODIGO_RELACION_DOCUMENTO = p_CODIGO_REL_DOC
       AND (CODIGO_EMPRESA IS NULL OR CODIGO_EMPRESA = p_CODIGO_EMPRESA);

    IF SQL%ROWCOUNT = 0 THEN
        p_Message := 'La relacion indicada no existe.';
        RETURN;
    END IF;

    p_Message := 'OK';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SQLERRM;
END;
/
