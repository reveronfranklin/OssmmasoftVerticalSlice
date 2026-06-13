CREATE OR REPLACE PROCEDURE CNT.SP_CNT_RUB_GET_ALL (
    p_SEARCH_TEXT    IN VARCHAR2 DEFAULT NULL,
    p_CODIGO_EMPRESA IN NUMBER,
    p_ResultSet      OUT SYS_REFCURSOR,
    p_Message        OUT VARCHAR2
) AS
BEGIN
    OPEN p_ResultSet FOR
        SELECT CODIGO_RUBRO,
               NUMERO_RUBRO,
               DENOMINACION,
               DESCRIPCION,
               EXTRA1,
               EXTRA2,
               EXTRA3,
               CODIGO_EMPRESA
          FROM CNT.CNT_RUBROS
         WHERE CODIGO_EMPRESA = p_CODIGO_EMPRESA
           AND (
                p_SEARCH_TEXT IS NULL
                OR UPPER(NUMERO_RUBRO) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%'
                OR UPPER(DENOMINACION) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%'
                OR UPPER(NVL(DESCRIPCION, '')) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%'
           )
         ORDER BY NUMERO_RUBRO;

    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_RUB_INS (
    p_NUMERO_RUBRO   IN VARCHAR2,
    p_DENOMINACION   IN VARCHAR2,
    p_DESCRIPCION    IN VARCHAR2 DEFAULT NULL,
    p_EXTRA1         IN VARCHAR2 DEFAULT NULL,
    p_EXTRA2         IN VARCHAR2 DEFAULT NULL,
    p_EXTRA3         IN VARCHAR2 DEFAULT NULL,
    p_USUARIO_ID     IN NUMBER,
    p_CODIGO_EMPRESA IN NUMBER,
    p_CODIGO_OUT     OUT NUMBER,
    p_Message        OUT VARCHAR2
) AS
    v_count NUMBER;
BEGIN
    p_CODIGO_OUT := 0;

    IF p_NUMERO_RUBRO IS NULL OR p_DENOMINACION IS NULL THEN
        p_Message := 'El numero y la denominacion del rubro son requeridos.';
        RETURN;
    END IF;

    SELECT COUNT(1)
      INTO v_count
      FROM CNT.CNT_RUBROS
     WHERE NUMERO_RUBRO = p_NUMERO_RUBRO
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    IF v_count > 0 THEN
        p_Message := 'Ya existe un rubro con el mismo numero.';
        RETURN;
    END IF;

    SELECT CNT.CNT_S_CODIGO_RUBRO.NEXTVAL INTO p_CODIGO_OUT FROM DUAL;

    INSERT INTO CNT.CNT_RUBROS (
        CODIGO_RUBRO,
        NUMERO_RUBRO,
        DENOMINACION,
        DESCRIPCION,
        EXTRA1,
        EXTRA2,
        EXTRA3,
        USUARIO_INS,
        FECHA_INS,
        CODIGO_EMPRESA
    ) VALUES (
        p_CODIGO_OUT,
        p_NUMERO_RUBRO,
        p_DENOMINACION,
        p_DESCRIPCION,
        p_EXTRA1,
        p_EXTRA2,
        p_EXTRA3,
        p_USUARIO_ID,
        SYSDATE,
        p_CODIGO_EMPRESA
    );

    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SQLERRM;
        p_CODIGO_OUT := 0;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_RUB_UPD (
    p_CODIGO_RUBRO   IN NUMBER,
    p_NUMERO_RUBRO   IN VARCHAR2,
    p_DENOMINACION   IN VARCHAR2,
    p_DESCRIPCION    IN VARCHAR2 DEFAULT NULL,
    p_EXTRA1         IN VARCHAR2 DEFAULT NULL,
    p_EXTRA2         IN VARCHAR2 DEFAULT NULL,
    p_EXTRA3         IN VARCHAR2 DEFAULT NULL,
    p_USUARIO_ID     IN NUMBER,
    p_CODIGO_EMPRESA IN NUMBER,
    p_Message        OUT VARCHAR2
) AS
    v_count NUMBER;
BEGIN
    IF p_CODIGO_RUBRO IS NULL OR p_NUMERO_RUBRO IS NULL OR p_DENOMINACION IS NULL THEN
        p_Message := 'El codigo, numero y denominacion del rubro son requeridos.';
        RETURN;
    END IF;

    SELECT COUNT(1)
      INTO v_count
      FROM CNT.CNT_RUBROS
     WHERE CODIGO_RUBRO = p_CODIGO_RUBRO
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    IF v_count = 0 THEN
        p_Message := 'El rubro no existe.';
        RETURN;
    END IF;

    SELECT COUNT(1)
      INTO v_count
      FROM CNT.CNT_RUBROS
     WHERE NUMERO_RUBRO = p_NUMERO_RUBRO
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA
       AND CODIGO_RUBRO <> p_CODIGO_RUBRO;

    IF v_count > 0 THEN
        p_Message := 'Ya existe otro rubro con el mismo numero.';
        RETURN;
    END IF;

    UPDATE CNT.CNT_RUBROS
       SET NUMERO_RUBRO = p_NUMERO_RUBRO,
           DENOMINACION = p_DENOMINACION,
           DESCRIPCION = p_DESCRIPCION,
           EXTRA1 = p_EXTRA1,
           EXTRA2 = p_EXTRA2,
           EXTRA3 = p_EXTRA3,
           USUARIO_UPD = p_USUARIO_ID,
           FECHA_UPD = SYSDATE
     WHERE CODIGO_RUBRO = p_CODIGO_RUBRO
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_RUB_DEL (
    p_CODIGO_RUBRO   IN NUMBER,
    p_CODIGO_EMPRESA IN NUMBER,
    p_Message        OUT VARCHAR2
) AS
    v_count NUMBER;
BEGIN
    SELECT COUNT(1)
      INTO v_count
      FROM CNT.CNT_BALANCES
     WHERE CODIGO_RUBRO = p_CODIGO_RUBRO
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    IF v_count > 0 THEN
        p_Message := 'No se puede eliminar el rubro porque tiene balances asociados.';
        RETURN;
    END IF;

    DELETE FROM CNT.CNT_RUBROS
     WHERE CODIGO_RUBRO = p_CODIGO_RUBRO
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    IF SQL%ROWCOUNT = 0 THEN
        p_Message := 'El rubro no existe.';
        RETURN;
    END IF;

    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SQLERRM;
END;
/
