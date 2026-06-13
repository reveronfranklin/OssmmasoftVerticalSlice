CREATE OR REPLACE PROCEDURE CNT.SP_CNT_BAL_GET_ALL (
    p_CODIGO_RUBRO   IN NUMBER DEFAULT NULL,
    p_SEARCH_TEXT    IN VARCHAR2 DEFAULT NULL,
    p_CODIGO_EMPRESA IN NUMBER,
    p_ResultSet      OUT SYS_REFCURSOR,
    p_Message        OUT VARCHAR2
) AS
BEGIN
    OPEN p_ResultSet FOR
        SELECT b.CODIGO_BALANCE,
               b.NUMERO_BALANCE,
               b.DENOMINACION,
               b.DESCRIPCION,
               b.EXTRA1,
               b.EXTRA2,
               b.EXTRA3,
               b.CODIGO_EMPRESA,
               b.CODIGO_RUBRO,
               r.NUMERO_RUBRO,
               r.DENOMINACION AS RUBRO
          FROM CNT.CNT_BALANCES b
          LEFT JOIN CNT.CNT_RUBROS r
            ON r.CODIGO_RUBRO = b.CODIGO_RUBRO
           AND r.CODIGO_EMPRESA = b.CODIGO_EMPRESA
         WHERE b.CODIGO_EMPRESA = p_CODIGO_EMPRESA
           AND (p_CODIGO_RUBRO IS NULL OR b.CODIGO_RUBRO = p_CODIGO_RUBRO)
           AND (
                p_SEARCH_TEXT IS NULL
                OR UPPER(b.NUMERO_BALANCE) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%'
                OR UPPER(b.DENOMINACION) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%'
                OR UPPER(NVL(b.DESCRIPCION, '')) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%'
                OR UPPER(NVL(r.DENOMINACION, '')) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%'
           )
         ORDER BY r.NUMERO_RUBRO, b.NUMERO_BALANCE;

    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_BAL_INS (
    p_NUMERO_BALANCE IN VARCHAR2,
    p_DENOMINACION   IN VARCHAR2,
    p_DESCRIPCION    IN VARCHAR2 DEFAULT NULL,
    p_EXTRA1         IN VARCHAR2 DEFAULT NULL,
    p_EXTRA2         IN VARCHAR2 DEFAULT NULL,
    p_EXTRA3         IN VARCHAR2 DEFAULT NULL,
    p_CODIGO_RUBRO   IN NUMBER DEFAULT NULL,
    p_USUARIO_ID     IN NUMBER,
    p_CODIGO_EMPRESA IN NUMBER,
    p_CODIGO_OUT     OUT NUMBER,
    p_Message        OUT VARCHAR2
) AS
    v_count NUMBER;
BEGIN
    p_CODIGO_OUT := 0;

    IF p_NUMERO_BALANCE IS NULL OR p_DENOMINACION IS NULL THEN
        p_Message := 'El numero y la denominacion del balance son requeridos.';
        RETURN;
    END IF;

    IF p_CODIGO_RUBRO IS NOT NULL THEN
        SELECT COUNT(1)
          INTO v_count
          FROM CNT.CNT_RUBROS
         WHERE CODIGO_RUBRO = p_CODIGO_RUBRO
           AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

        IF v_count = 0 THEN
            p_Message := 'El rubro seleccionado no existe.';
            RETURN;
        END IF;
    END IF;

    SELECT COUNT(1)
      INTO v_count
      FROM CNT.CNT_BALANCES
     WHERE NUMERO_BALANCE = p_NUMERO_BALANCE
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    IF v_count > 0 THEN
        p_Message := 'Ya existe un balance con el mismo numero.';
        RETURN;
    END IF;

    SELECT CNT.CNT_S_CODIGO_BALANCE.NEXTVAL INTO p_CODIGO_OUT FROM DUAL;

    INSERT INTO CNT.CNT_BALANCES (
        CODIGO_BALANCE,
        NUMERO_BALANCE,
        DENOMINACION,
        DESCRIPCION,
        EXTRA1,
        EXTRA2,
        EXTRA3,
        USUARIO_INS,
        FECHA_INS,
        CODIGO_EMPRESA,
        CODIGO_RUBRO
    ) VALUES (
        p_CODIGO_OUT,
        p_NUMERO_BALANCE,
        p_DENOMINACION,
        p_DESCRIPCION,
        p_EXTRA1,
        p_EXTRA2,
        p_EXTRA3,
        p_USUARIO_ID,
        SYSDATE,
        p_CODIGO_EMPRESA,
        p_CODIGO_RUBRO
    );

    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SQLERRM;
        p_CODIGO_OUT := 0;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_BAL_UPD (
    p_CODIGO_BALANCE IN NUMBER,
    p_NUMERO_BALANCE IN VARCHAR2,
    p_DENOMINACION   IN VARCHAR2,
    p_DESCRIPCION    IN VARCHAR2 DEFAULT NULL,
    p_EXTRA1         IN VARCHAR2 DEFAULT NULL,
    p_EXTRA2         IN VARCHAR2 DEFAULT NULL,
    p_EXTRA3         IN VARCHAR2 DEFAULT NULL,
    p_CODIGO_RUBRO   IN NUMBER DEFAULT NULL,
    p_USUARIO_ID     IN NUMBER,
    p_CODIGO_EMPRESA IN NUMBER,
    p_Message        OUT VARCHAR2
) AS
    v_count NUMBER;
BEGIN
    IF p_CODIGO_BALANCE IS NULL OR p_NUMERO_BALANCE IS NULL OR p_DENOMINACION IS NULL THEN
        p_Message := 'El codigo, numero y denominacion del balance son requeridos.';
        RETURN;
    END IF;

    SELECT COUNT(1)
      INTO v_count
      FROM CNT.CNT_BALANCES
     WHERE CODIGO_BALANCE = p_CODIGO_BALANCE
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    IF v_count = 0 THEN
        p_Message := 'El balance no existe.';
        RETURN;
    END IF;

    IF p_CODIGO_RUBRO IS NOT NULL THEN
        SELECT COUNT(1)
          INTO v_count
          FROM CNT.CNT_RUBROS
         WHERE CODIGO_RUBRO = p_CODIGO_RUBRO
           AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

        IF v_count = 0 THEN
            p_Message := 'El rubro seleccionado no existe.';
            RETURN;
        END IF;
    END IF;

    SELECT COUNT(1)
      INTO v_count
      FROM CNT.CNT_BALANCES
     WHERE NUMERO_BALANCE = p_NUMERO_BALANCE
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA
       AND CODIGO_BALANCE <> p_CODIGO_BALANCE;

    IF v_count > 0 THEN
        p_Message := 'Ya existe otro balance con el mismo numero.';
        RETURN;
    END IF;

    UPDATE CNT.CNT_BALANCES
       SET NUMERO_BALANCE = p_NUMERO_BALANCE,
           DENOMINACION = p_DENOMINACION,
           DESCRIPCION = p_DESCRIPCION,
           EXTRA1 = p_EXTRA1,
           EXTRA2 = p_EXTRA2,
           EXTRA3 = p_EXTRA3,
           CODIGO_RUBRO = p_CODIGO_RUBRO,
           USUARIO_UPD = p_USUARIO_ID,
           FECHA_UPD = SYSDATE
     WHERE CODIGO_BALANCE = p_CODIGO_BALANCE
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_BAL_DEL (
    p_CODIGO_BALANCE IN NUMBER,
    p_CODIGO_EMPRESA IN NUMBER,
    p_Message        OUT VARCHAR2
) AS
    v_count NUMBER;
BEGIN
    SELECT COUNT(1)
      INTO v_count
      FROM CNT.CNT_MAYORES
     WHERE CODIGO_BALANCE = p_CODIGO_BALANCE
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    IF v_count > 0 THEN
        p_Message := 'No se puede eliminar el balance porque tiene mayores asociados.';
        RETURN;
    END IF;

    DELETE FROM CNT.CNT_BALANCES
     WHERE CODIGO_BALANCE = p_CODIGO_BALANCE
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    IF SQL%ROWCOUNT = 0 THEN
        p_Message := 'El balance no existe.';
        RETURN;
    END IF;

    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SQLERRM;
END;
/
