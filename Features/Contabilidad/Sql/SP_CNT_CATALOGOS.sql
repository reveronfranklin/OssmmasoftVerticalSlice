-- Catalogos CNT - Titulos y Descriptivas
-- Oracle 10 compatible. Nombres de SP <= 30 caracteres.

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_TIT_GET_ALL (
    p_SEARCH_TEXT    IN VARCHAR2,
    p_CODIGO_EMPRESA IN NUMBER,
    p_ResultSet      OUT SYS_REFCURSOR,
    p_Message        OUT VARCHAR2
) AS
BEGIN
    OPEN p_ResultSet FOR
        SELECT TITULO_ID,
               TITULO_FK_ID,
               TITULO,
               CODIGO,
               EXTRA1,
               EXTRA2,
               EXTRA3,
               CODIGO_EMPRESA
          FROM CNT.CNT_TITULOS
         WHERE (CODIGO_EMPRESA IS NULL OR CODIGO_EMPRESA = p_CODIGO_EMPRESA)
           AND (p_SEARCH_TEXT IS NULL
                OR UPPER(TITULO) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%'
                OR UPPER(CODIGO) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%')
         ORDER BY TITULO;

    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_TIT_GET_ID (
    p_TITULO_ID      IN NUMBER,
    p_CODIGO_EMPRESA IN NUMBER,
    p_ResultSet      OUT SYS_REFCURSOR,
    p_Message        OUT VARCHAR2
) AS
BEGIN
    OPEN p_ResultSet FOR
        SELECT TITULO_ID,
               TITULO_FK_ID,
               TITULO,
               CODIGO,
               EXTRA1,
               EXTRA2,
               EXTRA3,
               CODIGO_EMPRESA
          FROM CNT.CNT_TITULOS
         WHERE TITULO_ID = p_TITULO_ID
           AND (CODIGO_EMPRESA IS NULL OR CODIGO_EMPRESA = p_CODIGO_EMPRESA);

    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_TIT_INS (
    p_TITULO_FK_ID   IN NUMBER,
    p_TITULO         IN VARCHAR2,
    p_CODIGO         IN VARCHAR2,
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
    IF p_TITULO IS NULL THEN
        p_Message := 'El titulo es requerido.';
        RETURN;
    END IF;

    IF p_CODIGO IS NOT NULL THEN
        SELECT COUNT(*)
          INTO v_count
          FROM CNT.CNT_TITULOS
         WHERE UPPER(CODIGO) = UPPER(p_CODIGO)
           AND NVL(CODIGO_EMPRESA, p_CODIGO_EMPRESA) = p_CODIGO_EMPRESA;

        IF v_count > 0 THEN
            p_Message := 'Ya existe un titulo CNT con el mismo codigo.';
            RETURN;
        END IF;
    END IF;

    SELECT CNT.CNT_S_TITULO_ID.NEXTVAL INTO p_CODIGO_OUT FROM DUAL;

    INSERT INTO CNT.CNT_TITULOS (
        TITULO_ID, TITULO_FK_ID, TITULO, CODIGO, EXTRA1, EXTRA2, EXTRA3,
        USUARIO_INS, FECHA_INS, CODIGO_EMPRESA
    ) VALUES (
        p_CODIGO_OUT, p_TITULO_FK_ID, p_TITULO, p_CODIGO, p_EXTRA1, p_EXTRA2, p_EXTRA3,
        p_USUARIO_ID, SYSDATE, p_CODIGO_EMPRESA
    );

    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        p_CODIGO_OUT := 0;
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_TIT_UPD (
    p_TITULO_ID      IN NUMBER,
    p_TITULO_FK_ID   IN NUMBER,
    p_TITULO         IN VARCHAR2,
    p_CODIGO         IN VARCHAR2,
    p_EXTRA1         IN VARCHAR2,
    p_EXTRA2         IN VARCHAR2,
    p_EXTRA3         IN VARCHAR2,
    p_USUARIO_ID     IN NUMBER,
    p_CODIGO_EMPRESA IN NUMBER,
    p_Message        OUT VARCHAR2
) AS
    v_count NUMBER;
BEGIN
    IF p_TITULO IS NULL THEN
        p_Message := 'El titulo es requerido.';
        RETURN;
    END IF;

    IF p_CODIGO IS NOT NULL THEN
        SELECT COUNT(*)
          INTO v_count
          FROM CNT.CNT_TITULOS
         WHERE UPPER(CODIGO) = UPPER(p_CODIGO)
           AND NVL(CODIGO_EMPRESA, p_CODIGO_EMPRESA) = p_CODIGO_EMPRESA
           AND TITULO_ID <> p_TITULO_ID;

        IF v_count > 0 THEN
            p_Message := 'Ya existe un titulo CNT con el mismo codigo.';
            RETURN;
        END IF;
    END IF;

    UPDATE CNT.CNT_TITULOS
       SET TITULO_FK_ID = p_TITULO_FK_ID,
           TITULO = p_TITULO,
           CODIGO = p_CODIGO,
           EXTRA1 = p_EXTRA1,
           EXTRA2 = p_EXTRA2,
           EXTRA3 = p_EXTRA3,
           USUARIO_UPD = p_USUARIO_ID,
           FECHA_UPD = SYSDATE
     WHERE TITULO_ID = p_TITULO_ID
       AND (CODIGO_EMPRESA IS NULL OR CODIGO_EMPRESA = p_CODIGO_EMPRESA);

    p_Message := CASE WHEN SQL%ROWCOUNT > 0 THEN 'Success' ELSE 'Titulo CNT no encontrado.' END;
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_TIT_DEL (
    p_TITULO_ID      IN NUMBER,
    p_CODIGO_EMPRESA IN NUMBER,
    p_Message        OUT VARCHAR2
) AS
    v_count NUMBER;
BEGIN
    SELECT COUNT(*)
      INTO v_count
      FROM CNT.CNT_DESCRIPTIVAS
     WHERE TITULO_ID = p_TITULO_ID
       AND (CODIGO_EMPRESA IS NULL OR CODIGO_EMPRESA = p_CODIGO_EMPRESA);

    IF v_count > 0 THEN
        p_Message := 'No se puede eliminar el titulo porque tiene descriptivas asociadas.';
        RETURN;
    END IF;

    SELECT COUNT(*)
      INTO v_count
      FROM CNT.CNT_TITULOS
     WHERE TITULO_FK_ID = p_TITULO_ID
       AND (CODIGO_EMPRESA IS NULL OR CODIGO_EMPRESA = p_CODIGO_EMPRESA);

    IF v_count > 0 THEN
        p_Message := 'No se puede eliminar el titulo porque tiene titulos hijos.';
        RETURN;
    END IF;

    DELETE FROM CNT.CNT_TITULOS
     WHERE TITULO_ID = p_TITULO_ID
       AND (CODIGO_EMPRESA IS NULL OR CODIGO_EMPRESA = p_CODIGO_EMPRESA);

    p_Message := CASE WHEN SQL%ROWCOUNT > 0 THEN 'Success' ELSE 'Titulo CNT no encontrado.' END;
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_DES_GET_ALL (
    p_TITULO_ID      IN NUMBER,
    p_SEARCH_TEXT    IN VARCHAR2,
    p_CODIGO_EMPRESA IN NUMBER,
    p_ResultSet      OUT SYS_REFCURSOR,
    p_Message        OUT VARCHAR2
) AS
BEGIN
    OPEN p_ResultSet FOR
        SELECT d.DESCRIPCION_ID,
               d.DESCRIPCION_FK_ID,
               d.TITULO_ID,
               t.TITULO,
               d.DESCRIPCION,
               d.CODIGO,
               d.EXTRA1,
               d.EXTRA2,
               d.EXTRA3,
               d.CODIGO_EMPRESA
          FROM CNT.CNT_DESCRIPTIVAS d
          LEFT JOIN CNT.CNT_TITULOS t ON t.TITULO_ID = d.TITULO_ID
         WHERE (p_TITULO_ID IS NULL OR d.TITULO_ID = p_TITULO_ID)
           AND (d.CODIGO_EMPRESA IS NULL OR d.CODIGO_EMPRESA = p_CODIGO_EMPRESA)
           AND (p_SEARCH_TEXT IS NULL
                OR UPPER(d.DESCRIPCION) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%'
                OR UPPER(d.CODIGO) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%')
         ORDER BY t.TITULO, d.CODIGO, d.DESCRIPCION;

    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_DES_GET_ID (
    p_DESCRIPCION_ID IN NUMBER,
    p_CODIGO_EMPRESA IN NUMBER,
    p_ResultSet      OUT SYS_REFCURSOR,
    p_Message        OUT VARCHAR2
) AS
BEGIN
    OPEN p_ResultSet FOR
        SELECT d.DESCRIPCION_ID,
               d.DESCRIPCION_FK_ID,
               d.TITULO_ID,
               t.TITULO,
               d.DESCRIPCION,
               d.CODIGO,
               d.EXTRA1,
               d.EXTRA2,
               d.EXTRA3,
               d.CODIGO_EMPRESA
          FROM CNT.CNT_DESCRIPTIVAS d
          LEFT JOIN CNT.CNT_TITULOS t ON t.TITULO_ID = d.TITULO_ID
         WHERE d.DESCRIPCION_ID = p_DESCRIPCION_ID
           AND (d.CODIGO_EMPRESA IS NULL OR d.CODIGO_EMPRESA = p_CODIGO_EMPRESA);

    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_DES_INS (
    p_DESCRIPCION_FK_ID IN NUMBER,
    p_TITULO_ID         IN NUMBER,
    p_DESCRIPCION       IN VARCHAR2,
    p_CODIGO            IN VARCHAR2,
    p_EXTRA1            IN VARCHAR2,
    p_EXTRA2            IN VARCHAR2,
    p_EXTRA3            IN VARCHAR2,
    p_USUARIO_ID        IN NUMBER,
    p_CODIGO_EMPRESA    IN NUMBER,
    p_CODIGO_OUT        OUT NUMBER,
    p_Message           OUT VARCHAR2
) AS
    v_count NUMBER;
BEGIN
    IF p_TITULO_ID IS NULL OR p_DESCRIPCION IS NULL THEN
        p_Message := 'El titulo y la descripcion son requeridos.';
        RETURN;
    END IF;

    SELECT COUNT(*)
      INTO v_count
      FROM CNT.CNT_TITULOS
     WHERE TITULO_ID = p_TITULO_ID
       AND (CODIGO_EMPRESA IS NULL OR CODIGO_EMPRESA = p_CODIGO_EMPRESA);

    IF v_count = 0 THEN
        p_Message := 'Titulo CNT no valido.';
        RETURN;
    END IF;

    IF p_CODIGO IS NOT NULL THEN
        SELECT COUNT(*)
          INTO v_count
          FROM CNT.CNT_DESCRIPTIVAS
         WHERE TITULO_ID = p_TITULO_ID
           AND UPPER(CODIGO) = UPPER(p_CODIGO)
           AND NVL(CODIGO_EMPRESA, p_CODIGO_EMPRESA) = p_CODIGO_EMPRESA;

        IF v_count > 0 THEN
            p_Message := 'Ya existe una descriptiva CNT con el mismo codigo para el titulo.';
            RETURN;
        END IF;
    END IF;

    SELECT CNT.CNT_S_DESCRIPCION_ID.NEXTVAL INTO p_CODIGO_OUT FROM DUAL;

    INSERT INTO CNT.CNT_DESCRIPTIVAS (
        DESCRIPCION_ID, DESCRIPCION_FK_ID, TITULO_ID, DESCRIPCION, CODIGO,
        EXTRA1, EXTRA2, EXTRA3, USUARIO_INS, FECHA_INS, CODIGO_EMPRESA
    ) VALUES (
        p_CODIGO_OUT, p_DESCRIPCION_FK_ID, p_TITULO_ID, p_DESCRIPCION, p_CODIGO,
        p_EXTRA1, p_EXTRA2, p_EXTRA3, p_USUARIO_ID, SYSDATE, p_CODIGO_EMPRESA
    );

    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        p_CODIGO_OUT := 0;
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_DES_UPD (
    p_DESCRIPCION_ID    IN NUMBER,
    p_DESCRIPCION_FK_ID IN NUMBER,
    p_TITULO_ID         IN NUMBER,
    p_DESCRIPCION       IN VARCHAR2,
    p_CODIGO            IN VARCHAR2,
    p_EXTRA1            IN VARCHAR2,
    p_EXTRA2            IN VARCHAR2,
    p_EXTRA3            IN VARCHAR2,
    p_USUARIO_ID        IN NUMBER,
    p_CODIGO_EMPRESA    IN NUMBER,
    p_Message           OUT VARCHAR2
) AS
    v_count NUMBER;
BEGIN
    IF p_TITULO_ID IS NULL OR p_DESCRIPCION IS NULL THEN
        p_Message := 'El titulo y la descripcion son requeridos.';
        RETURN;
    END IF;

    IF p_CODIGO IS NOT NULL THEN
        SELECT COUNT(*)
          INTO v_count
          FROM CNT.CNT_DESCRIPTIVAS
         WHERE TITULO_ID = p_TITULO_ID
           AND UPPER(CODIGO) = UPPER(p_CODIGO)
           AND NVL(CODIGO_EMPRESA, p_CODIGO_EMPRESA) = p_CODIGO_EMPRESA
           AND DESCRIPCION_ID <> p_DESCRIPCION_ID;

        IF v_count > 0 THEN
            p_Message := 'Ya existe una descriptiva CNT con el mismo codigo para el titulo.';
            RETURN;
        END IF;
    END IF;

    UPDATE CNT.CNT_DESCRIPTIVAS
       SET DESCRIPCION_FK_ID = p_DESCRIPCION_FK_ID,
           TITULO_ID = p_TITULO_ID,
           DESCRIPCION = p_DESCRIPCION,
           CODIGO = p_CODIGO,
           EXTRA1 = p_EXTRA1,
           EXTRA2 = p_EXTRA2,
           EXTRA3 = p_EXTRA3,
           USUARIO_UPD = p_USUARIO_ID,
           FECHA_UPD = SYSDATE
     WHERE DESCRIPCION_ID = p_DESCRIPCION_ID
       AND (CODIGO_EMPRESA IS NULL OR CODIGO_EMPRESA = p_CODIGO_EMPRESA);

    p_Message := CASE WHEN SQL%ROWCOUNT > 0 THEN 'Success' ELSE 'Descriptiva CNT no encontrada.' END;
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_DES_USED (
    p_DESCRIPCION_ID IN NUMBER,
    p_CANTIDAD       OUT NUMBER,
    p_Message        OUT VARCHAR2
) AS
BEGIN
    SELECT SUM(cantidad)
      INTO p_CANTIDAD
      FROM (
            SELECT COUNT(*) cantidad FROM CNT.CNT_COMPROBANTES WHERE TIPO_COMPROBANTE_ID = p_DESCRIPCION_ID OR ORIGEN_ID = p_DESCRIPCION_ID
            UNION ALL SELECT COUNT(*) FROM CNT.CNT_RELACION_DOCUMENTOS WHERE TIPO_DOCUMENTO_ID = p_DESCRIPCION_ID OR TIPO_TRANSACCION_ID = p_DESCRIPCION_ID
            UNION ALL SELECT COUNT(*) FROM CNT.CNT_DETALLE_LIBRO WHERE TIPO_DOCUMENTO_ID = p_DESCRIPCION_ID
            UNION ALL SELECT COUNT(*) FROM CNT.CNT_DETALLE_EDO_CTA WHERE TIPO_TRANSACCION_ID = p_DESCRIPCION_ID
            UNION ALL SELECT COUNT(*) FROM CNT.CNT_DESCRIPTIVAS WHERE DESCRIPCION_FK_ID = p_DESCRIPCION_ID
      );

    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        p_CANTIDAD := 0;
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_DES_DEL (
    p_DESCRIPCION_ID IN NUMBER,
    p_CODIGO_EMPRESA IN NUMBER,
    p_Message        OUT VARCHAR2
) AS
    v_count NUMBER;
BEGIN
    CNT.SP_CNT_DES_USED(p_DESCRIPCION_ID, v_count, p_Message);

    IF p_Message <> 'Success' THEN
        RETURN;
    END IF;

    IF v_count > 0 THEN
        p_Message := 'No se puede eliminar la descriptiva porque esta siendo usada.';
        RETURN;
    END IF;

    DELETE FROM CNT.CNT_DESCRIPTIVAS
     WHERE DESCRIPCION_ID = p_DESCRIPCION_ID
       AND (CODIGO_EMPRESA IS NULL OR CODIGO_EMPRESA = p_CODIGO_EMPRESA);

    p_Message := CASE WHEN SQL%ROWCOUNT > 0 THEN 'Success' ELSE 'Descriptiva CNT no encontrada.' END;
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SQLERRM;
END;
/
