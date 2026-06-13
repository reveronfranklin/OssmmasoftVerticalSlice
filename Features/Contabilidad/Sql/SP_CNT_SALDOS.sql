CREATE OR REPLACE PROCEDURE CNT.SP_CNT_SAL_GET (
    p_CODIGO_PERIODO IN NUMBER,
    p_CODIGO_MAYOR   IN NUMBER,
    p_CODIGO_AUX     IN NUMBER,
    p_SEARCH_TEXT    IN VARCHAR2,
    p_CODIGO_EMPRESA IN NUMBER,
    p_ResultSet      OUT SYS_REFCURSOR,
    p_Message        OUT VARCHAR2
) AS
BEGIN
    OPEN p_ResultSet FOR
        SELECT s.CODIGO_SALDO,
               s.CODIGO_PERIODO,
               p.NOMBRE_PERIODO,
               s.CODIGO_MAYOR,
               m.NUMERO_MAYOR || ' - ' || m.DENOMINACION AS MAYOR,
               s.CODIGO_AUXILIAR,
               a.SEGMENTO1 || ' ' || a.SEGMENTO2 || ' - ' || a.DENOMINACION AS AUXILIAR,
               s.DEBITOS,
               s.CREDITOS,
               s.MONTO,
               s.EXTRA1,
               s.EXTRA2,
               s.EXTRA3,
               s.CODIGO_EMPRESA
          FROM CNT.CNT_SALDOS s,
               CNT.CNT_PERIODOS p,
               CNT.CNT_MAYORES m,
               CNT.CNT_AUXILIARES a
         WHERE p.CODIGO_PERIODO = s.CODIGO_PERIODO
           AND m.CODIGO_MAYOR = s.CODIGO_MAYOR
           AND a.CODIGO_AUXILIAR = s.CODIGO_AUXILIAR
           AND (s.CODIGO_EMPRESA IS NULL OR s.CODIGO_EMPRESA = p_CODIGO_EMPRESA)
           AND (p.CODIGO_EMPRESA IS NULL OR p.CODIGO_EMPRESA = p_CODIGO_EMPRESA)
           AND (m.CODIGO_EMPRESA IS NULL OR m.CODIGO_EMPRESA = p_CODIGO_EMPRESA)
           AND (a.CODIGO_EMPRESA IS NULL OR a.CODIGO_EMPRESA = p_CODIGO_EMPRESA)
           AND (p_CODIGO_PERIODO IS NULL OR s.CODIGO_PERIODO = p_CODIGO_PERIODO)
           AND (p_CODIGO_MAYOR IS NULL OR s.CODIGO_MAYOR = p_CODIGO_MAYOR)
           AND (p_CODIGO_AUX IS NULL OR s.CODIGO_AUXILIAR = p_CODIGO_AUX)
           AND (
                p_SEARCH_TEXT IS NULL
                OR UPPER(p.NOMBRE_PERIODO) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%'
                OR UPPER(m.NUMERO_MAYOR) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%'
                OR UPPER(m.DENOMINACION) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%'
                OR UPPER(a.SEGMENTO1) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%'
                OR UPPER(a.SEGMENTO2) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%'
                OR UPPER(a.DENOMINACION) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%'
           )
         ORDER BY p.ANO_PERIODO DESC, p.NUMERO_PERIODO DESC, m.NUMERO_MAYOR, a.SEGMENTO1, a.SEGMENTO2;

    p_Message := 'OK';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_SAL_INS (
    p_CODIGO_PERIODO IN NUMBER,
    p_CODIGO_MAYOR   IN NUMBER,
    p_CODIGO_AUX     IN NUMBER,
    p_DEBITOS        IN NUMBER,
    p_CREDITOS       IN NUMBER,
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
    IF p_CODIGO_PERIODO IS NULL OR p_CODIGO_MAYOR IS NULL OR p_CODIGO_AUX IS NULL THEN
        p_Message := 'Periodo, mayor y auxiliar son requeridos.';
        RETURN;
    END IF;

    SELECT COUNT(1) INTO v_count
      FROM CNT.CNT_PERIODOS
     WHERE CODIGO_PERIODO = p_CODIGO_PERIODO
       AND (CODIGO_EMPRESA IS NULL OR CODIGO_EMPRESA = p_CODIGO_EMPRESA);

    IF v_count = 0 THEN
        p_Message := 'El periodo indicado no existe.';
        RETURN;
    END IF;

    SELECT COUNT(1) INTO v_count
      FROM CNT.CNT_AUXILIARES
     WHERE CODIGO_AUXILIAR = p_CODIGO_AUX
       AND CODIGO_MAYOR = p_CODIGO_MAYOR
       AND (CODIGO_EMPRESA IS NULL OR CODIGO_EMPRESA = p_CODIGO_EMPRESA);

    IF v_count = 0 THEN
        p_Message := 'El auxiliar no pertenece al mayor indicado.';
        RETURN;
    END IF;

    SELECT COUNT(1) INTO v_count
      FROM CNT.CNT_SALDOS
     WHERE CODIGO_PERIODO = p_CODIGO_PERIODO
       AND CODIGO_MAYOR = p_CODIGO_MAYOR
       AND CODIGO_AUXILIAR = p_CODIGO_AUX;

    IF v_count > 0 THEN
        p_Message := 'Ya existe saldo para el periodo, mayor y auxiliar.';
        RETURN;
    END IF;

    SELECT CNT.CNT_S_CODIGO_SALDO.NEXTVAL INTO p_CODIGO_OUT FROM DUAL;

    INSERT INTO CNT.CNT_SALDOS (
        CODIGO_SALDO, CODIGO_PERIODO, CODIGO_MAYOR, CODIGO_AUXILIAR,
        DEBITOS, CREDITOS, MONTO, EXTRA1, EXTRA2, EXTRA3,
        USUARIO_INS, FECHA_INS, CODIGO_EMPRESA
    ) VALUES (
        p_CODIGO_OUT, p_CODIGO_PERIODO, p_CODIGO_MAYOR, p_CODIGO_AUX,
        NVL(p_DEBITOS, 0), NVL(p_CREDITOS, 0), NVL(p_DEBITOS, 0) - NVL(p_CREDITOS, 0),
        p_EXTRA1, p_EXTRA2, p_EXTRA3, p_USUARIO_ID, SYSDATE, p_CODIGO_EMPRESA
    );

    p_Message := 'OK';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_SAL_UPD (
    p_CODIGO_SALDO   IN NUMBER,
    p_CODIGO_PERIODO IN NUMBER,
    p_CODIGO_MAYOR   IN NUMBER,
    p_CODIGO_AUX     IN NUMBER,
    p_DEBITOS        IN NUMBER,
    p_CREDITOS       IN NUMBER,
    p_EXTRA1         IN VARCHAR2,
    p_EXTRA2         IN VARCHAR2,
    p_EXTRA3         IN VARCHAR2,
    p_USUARIO_ID     IN NUMBER,
    p_CODIGO_EMPRESA IN NUMBER,
    p_Message        OUT VARCHAR2
) AS
    v_count NUMBER;
BEGIN
    IF p_CODIGO_SALDO IS NULL OR p_CODIGO_PERIODO IS NULL OR p_CODIGO_MAYOR IS NULL OR p_CODIGO_AUX IS NULL THEN
        p_Message := 'Saldo, periodo, mayor y auxiliar son requeridos.';
        RETURN;
    END IF;

    SELECT COUNT(1) INTO v_count
      FROM CNT.CNT_SALDOS
     WHERE CODIGO_SALDO = p_CODIGO_SALDO
       AND (CODIGO_EMPRESA IS NULL OR CODIGO_EMPRESA = p_CODIGO_EMPRESA);

    IF v_count = 0 THEN
        p_Message := 'El saldo indicado no existe.';
        RETURN;
    END IF;

    SELECT COUNT(1) INTO v_count
      FROM CNT.CNT_AUXILIARES
     WHERE CODIGO_AUXILIAR = p_CODIGO_AUX
       AND CODIGO_MAYOR = p_CODIGO_MAYOR
       AND (CODIGO_EMPRESA IS NULL OR CODIGO_EMPRESA = p_CODIGO_EMPRESA);

    IF v_count = 0 THEN
        p_Message := 'El auxiliar no pertenece al mayor indicado.';
        RETURN;
    END IF;

    SELECT COUNT(1) INTO v_count
      FROM CNT.CNT_SALDOS
     WHERE CODIGO_SALDO <> p_CODIGO_SALDO
       AND CODIGO_PERIODO = p_CODIGO_PERIODO
       AND CODIGO_MAYOR = p_CODIGO_MAYOR
       AND CODIGO_AUXILIAR = p_CODIGO_AUX;

    IF v_count > 0 THEN
        p_Message := 'Ya existe saldo para el periodo, mayor y auxiliar.';
        RETURN;
    END IF;

    UPDATE CNT.CNT_SALDOS
       SET CODIGO_PERIODO = p_CODIGO_PERIODO,
           CODIGO_MAYOR = p_CODIGO_MAYOR,
           CODIGO_AUXILIAR = p_CODIGO_AUX,
           DEBITOS = NVL(p_DEBITOS, 0),
           CREDITOS = NVL(p_CREDITOS, 0),
           MONTO = NVL(p_DEBITOS, 0) - NVL(p_CREDITOS, 0),
           EXTRA1 = p_EXTRA1,
           EXTRA2 = p_EXTRA2,
           EXTRA3 = p_EXTRA3,
           USUARIO_UPD = p_USUARIO_ID,
           FECHA_UPD = SYSDATE,
           CODIGO_EMPRESA = p_CODIGO_EMPRESA
     WHERE CODIGO_SALDO = p_CODIGO_SALDO;

    p_Message := 'OK';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_SAL_DEL (
    p_CODIGO_SALDO   IN NUMBER,
    p_CODIGO_EMPRESA IN NUMBER,
    p_Message        OUT VARCHAR2
) AS
    v_count NUMBER;
BEGIN
    SELECT COUNT(1) INTO v_count
      FROM CNT.CNT_HIST_ANALITICO
     WHERE CODIGO_SALDO = p_CODIGO_SALDO
       AND (CODIGO_EMPRESA IS NULL OR CODIGO_EMPRESA = p_CODIGO_EMPRESA);

    IF v_count > 0 THEN
        p_Message := 'No se puede eliminar el saldo porque tiene historico analitico.';
        RETURN;
    END IF;

    DELETE FROM CNT.CNT_SALDOS
     WHERE CODIGO_SALDO = p_CODIGO_SALDO
       AND (CODIGO_EMPRESA IS NULL OR CODIGO_EMPRESA = p_CODIGO_EMPRESA);

    IF SQL%ROWCOUNT = 0 THEN
        p_Message := 'El saldo indicado no existe.';
        RETURN;
    END IF;

    p_Message := 'OK';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SQLERRM;
END;
/
