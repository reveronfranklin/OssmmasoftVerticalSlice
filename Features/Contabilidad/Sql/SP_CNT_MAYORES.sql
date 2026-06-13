CREATE OR REPLACE PROCEDURE CNT.SP_CNT_MAY_GET_ALL (
    p_CODIGO_BALANCE IN NUMBER DEFAULT NULL,
    p_SEARCH_TEXT    IN VARCHAR2 DEFAULT NULL,
    p_CODIGO_EMPRESA IN NUMBER,
    p_ResultSet      OUT SYS_REFCURSOR,
    p_Message        OUT VARCHAR2
) AS
BEGIN
    OPEN p_ResultSet FOR
        SELECT m.CODIGO_MAYOR,
               m.NUMERO_MAYOR,
               m.DENOMINACION,
               m.DESCRIPCION,
               m.CODIGO_BALANCE,
               b.NUMERO_BALANCE,
               b.DENOMINACION AS BALANCE,
               b.CODIGO_RUBRO,
               r.NUMERO_RUBRO,
               r.DENOMINACION AS RUBRO,
               m.COLUMNA_BALANCE,
               m.EXTRA1,
               m.EXTRA2,
               m.EXTRA3,
               m.CODIGO_EMPRESA
          FROM CNT.CNT_MAYORES m
          LEFT JOIN CNT.CNT_BALANCES b
            ON b.CODIGO_BALANCE = m.CODIGO_BALANCE
           AND b.CODIGO_EMPRESA = m.CODIGO_EMPRESA
          LEFT JOIN CNT.CNT_RUBROS r
            ON r.CODIGO_RUBRO = b.CODIGO_RUBRO
           AND r.CODIGO_EMPRESA = b.CODIGO_EMPRESA
         WHERE m.CODIGO_EMPRESA = p_CODIGO_EMPRESA
           AND (p_CODIGO_BALANCE IS NULL OR m.CODIGO_BALANCE = p_CODIGO_BALANCE)
           AND (
                p_SEARCH_TEXT IS NULL
                OR UPPER(m.NUMERO_MAYOR) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%'
                OR UPPER(m.DENOMINACION) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%'
                OR UPPER(NVL(m.DESCRIPCION, '')) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%'
                OR UPPER(NVL(b.DENOMINACION, '')) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%'
                OR UPPER(NVL(r.DENOMINACION, '')) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%'
           )
         ORDER BY r.NUMERO_RUBRO, b.NUMERO_BALANCE, m.NUMERO_MAYOR;

    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_MAY_INS (
    p_NUMERO_MAYOR   IN VARCHAR2,
    p_DENOMINACION   IN VARCHAR2,
    p_DESCRIPCION    IN VARCHAR2 DEFAULT NULL,
    p_CODIGO_BALANCE IN NUMBER,
    p_COLUMNA_BAL    IN VARCHAR2 DEFAULT NULL,
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

    IF p_NUMERO_MAYOR IS NULL OR p_DENOMINACION IS NULL OR p_CODIGO_BALANCE IS NULL THEN
        p_Message := 'El numero, denominacion y balance del mayor son requeridos.';
        RETURN;
    END IF;

    SELECT COUNT(1)
      INTO v_count
      FROM CNT.CNT_BALANCES
     WHERE CODIGO_BALANCE = p_CODIGO_BALANCE
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    IF v_count = 0 THEN
        p_Message := 'El balance seleccionado no existe.';
        RETURN;
    END IF;

    SELECT COUNT(1)
      INTO v_count
      FROM CNT.CNT_MAYORES
     WHERE NUMERO_MAYOR = p_NUMERO_MAYOR
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    IF v_count > 0 THEN
        p_Message := 'Ya existe un mayor con el mismo numero.';
        RETURN;
    END IF;

    SELECT CNT.CNT_S_CODIGO_MAYOR.NEXTVAL INTO p_CODIGO_OUT FROM DUAL;

    INSERT INTO CNT.CNT_MAYORES (
        CODIGO_MAYOR,
        NUMERO_MAYOR,
        DENOMINACION,
        DESCRIPCION,
        CODIGO_BALANCE,
        COLUMNA_BALANCE,
        EXTRA1,
        EXTRA2,
        EXTRA3,
        USUARIO_INS,
        FECHA_INS,
        CODIGO_EMPRESA
    ) VALUES (
        p_CODIGO_OUT,
        p_NUMERO_MAYOR,
        p_DENOMINACION,
        p_DESCRIPCION,
        p_CODIGO_BALANCE,
        SUBSTR(p_COLUMNA_BAL, 1, 1),
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

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_MAY_UPD (
    p_CODIGO_MAYOR   IN NUMBER,
    p_NUMERO_MAYOR   IN VARCHAR2,
    p_DENOMINACION   IN VARCHAR2,
    p_DESCRIPCION    IN VARCHAR2 DEFAULT NULL,
    p_CODIGO_BALANCE IN NUMBER,
    p_COLUMNA_BAL    IN VARCHAR2 DEFAULT NULL,
    p_EXTRA1         IN VARCHAR2 DEFAULT NULL,
    p_EXTRA2         IN VARCHAR2 DEFAULT NULL,
    p_EXTRA3         IN VARCHAR2 DEFAULT NULL,
    p_USUARIO_ID     IN NUMBER,
    p_CODIGO_EMPRESA IN NUMBER,
    p_Message        OUT VARCHAR2
) AS
    v_count NUMBER;
BEGIN
    IF p_CODIGO_MAYOR IS NULL OR p_NUMERO_MAYOR IS NULL OR p_DENOMINACION IS NULL OR p_CODIGO_BALANCE IS NULL THEN
        p_Message := 'El codigo, numero, denominacion y balance del mayor son requeridos.';
        RETURN;
    END IF;

    SELECT COUNT(1)
      INTO v_count
      FROM CNT.CNT_MAYORES
     WHERE CODIGO_MAYOR = p_CODIGO_MAYOR
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    IF v_count = 0 THEN
        p_Message := 'El mayor no existe.';
        RETURN;
    END IF;

    SELECT COUNT(1)
      INTO v_count
      FROM CNT.CNT_BALANCES
     WHERE CODIGO_BALANCE = p_CODIGO_BALANCE
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    IF v_count = 0 THEN
        p_Message := 'El balance seleccionado no existe.';
        RETURN;
    END IF;

    SELECT COUNT(1)
      INTO v_count
      FROM CNT.CNT_MAYORES
     WHERE NUMERO_MAYOR = p_NUMERO_MAYOR
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA
       AND CODIGO_MAYOR <> p_CODIGO_MAYOR;

    IF v_count > 0 THEN
        p_Message := 'Ya existe otro mayor con el mismo numero.';
        RETURN;
    END IF;

    UPDATE CNT.CNT_MAYORES
       SET NUMERO_MAYOR = p_NUMERO_MAYOR,
           DENOMINACION = p_DENOMINACION,
           DESCRIPCION = p_DESCRIPCION,
           CODIGO_BALANCE = p_CODIGO_BALANCE,
           COLUMNA_BALANCE = SUBSTR(p_COLUMNA_BAL, 1, 1),
           EXTRA1 = p_EXTRA1,
           EXTRA2 = p_EXTRA2,
           EXTRA3 = p_EXTRA3,
           USUARIO_UPD = p_USUARIO_ID,
           FECHA_UPD = SYSDATE
     WHERE CODIGO_MAYOR = p_CODIGO_MAYOR
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_MAY_USED (
    p_CODIGO_MAYOR   IN NUMBER,
    p_CODIGO_EMPRESA IN NUMBER,
    p_CANTIDAD       OUT NUMBER,
    p_Message        OUT VARCHAR2
) AS
BEGIN
    SELECT SUM(cantidad)
      INTO p_CANTIDAD
      FROM (
            SELECT COUNT(1) cantidad
              FROM CNT.CNT_AUXILIARES
             WHERE CODIGO_MAYOR = p_CODIGO_MAYOR
               AND CODIGO_EMPRESA = p_CODIGO_EMPRESA
            UNION ALL
            SELECT COUNT(1)
              FROM CNT.CNT_DETALLE_COMPROBANTE
             WHERE CODIGO_MAYOR = p_CODIGO_MAYOR
               AND CODIGO_EMPRESA = p_CODIGO_EMPRESA
            UNION ALL
            SELECT COUNT(1)
              FROM CNT.CNT_SALDOS
             WHERE CODIGO_MAYOR = p_CODIGO_MAYOR
               AND CODIGO_EMPRESA = p_CODIGO_EMPRESA
      );

    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        p_CANTIDAD := 0;
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_MAY_DEL (
    p_CODIGO_MAYOR   IN NUMBER,
    p_CODIGO_EMPRESA IN NUMBER,
    p_Message        OUT VARCHAR2
) AS
    v_count NUMBER;
BEGIN
    SELECT COUNT(1)
      INTO v_count
      FROM CNT.CNT_AUXILIARES
     WHERE CODIGO_MAYOR = p_CODIGO_MAYOR
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    IF v_count > 0 THEN
        p_Message := 'No se puede eliminar el mayor porque tiene auxiliares asociados.';
        RETURN;
    END IF;

    SELECT COUNT(1)
      INTO v_count
      FROM CNT.CNT_DETALLE_COMPROBANTE
     WHERE CODIGO_MAYOR = p_CODIGO_MAYOR
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    IF v_count > 0 THEN
        p_Message := 'No se puede eliminar el mayor porque tiene movimientos contables.';
        RETURN;
    END IF;

    SELECT COUNT(1)
      INTO v_count
      FROM CNT.CNT_SALDOS
     WHERE CODIGO_MAYOR = p_CODIGO_MAYOR
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    IF v_count > 0 THEN
        p_Message := 'No se puede eliminar el mayor porque tiene saldos asociados.';
        RETURN;
    END IF;

    DELETE FROM CNT.CNT_MAYORES
     WHERE CODIGO_MAYOR = p_CODIGO_MAYOR
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    IF SQL%ROWCOUNT = 0 THEN
        p_Message := 'El mayor no existe.';
        RETURN;
    END IF;

    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SQLERRM;
END;
/
