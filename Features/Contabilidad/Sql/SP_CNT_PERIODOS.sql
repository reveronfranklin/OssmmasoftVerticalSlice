CREATE OR REPLACE PROCEDURE CNT.SP_CNT_PER_ADM_GET (
    p_ANO_PERIODO    IN NUMBER,
    p_SOLO_ABIERTOS  IN NUMBER,
    p_SEARCH_TEXT    IN VARCHAR2,
    p_CODIGO_EMPRESA IN NUMBER,
    p_ResultSet      OUT SYS_REFCURSOR,
    p_Message        OUT VARCHAR2
) AS
BEGIN
    OPEN p_ResultSet FOR
        SELECT CODIGO_PERIODO,
               NOMBRE_PERIODO,
               FECHA_DESDE,
               FECHA_HASTA,
               ANO_PERIODO,
               NUMERO_PERIODO,
               FECHA_CIERRE,
               CASE WHEN FECHA_CIERRE IS NULL THEN 0 ELSE 1 END AS CERRADO,
               EXTRA1,
               EXTRA2,
               EXTRA3,
               CODIGO_EMPRESA
          FROM CNT.CNT_PERIODOS
         WHERE CODIGO_EMPRESA = p_CODIGO_EMPRESA
           AND (p_ANO_PERIODO IS NULL OR ANO_PERIODO = p_ANO_PERIODO)
           AND (NVL(p_SOLO_ABIERTOS, 0) = 0 OR FECHA_CIERRE IS NULL)
           AND (
                p_SEARCH_TEXT IS NULL
                OR UPPER(NOMBRE_PERIODO) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%'
                OR TO_CHAR(ANO_PERIODO) LIKE '%' || p_SEARCH_TEXT || '%'
                OR TO_CHAR(NUMERO_PERIODO) LIKE '%' || p_SEARCH_TEXT || '%'
           )
         ORDER BY ANO_PERIODO DESC, NUMERO_PERIODO DESC;

    p_Message := 'OK';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_PER_INS (
    p_NOMBRE_PERIODO IN VARCHAR2,
    p_FECHA_DESDE    IN DATE,
    p_FECHA_HASTA    IN DATE,
    p_ANO_PERIODO    IN NUMBER,
    p_NUM_PERIODO    IN NUMBER,
    p_CERRADO        IN NUMBER,
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
    IF p_NOMBRE_PERIODO IS NULL OR p_FECHA_DESDE IS NULL OR p_FECHA_HASTA IS NULL OR p_ANO_PERIODO IS NULL OR p_NUM_PERIODO IS NULL THEN
        p_Message := 'Nombre, fechas, ano y numero de periodo son requeridos.';
        RETURN;
    END IF;

    IF p_FECHA_DESDE > p_FECHA_HASTA THEN
        p_Message := 'La fecha desde no puede ser mayor que la fecha hasta.';
        RETURN;
    END IF;

    SELECT COUNT(1) INTO v_count
      FROM CNT.CNT_PERIODOS
     WHERE CODIGO_EMPRESA = p_CODIGO_EMPRESA
       AND ANO_PERIODO = p_ANO_PERIODO
       AND NUMERO_PERIODO = p_NUM_PERIODO;

    IF v_count > 0 THEN
        p_Message := 'Ya existe un periodo con el mismo ano y numero.';
        RETURN;
    END IF;

    SELECT COUNT(1) INTO v_count
      FROM CNT.CNT_PERIODOS
     WHERE CODIGO_EMPRESA = p_CODIGO_EMPRESA
       AND p_FECHA_DESDE <= FECHA_HASTA
       AND p_FECHA_HASTA >= FECHA_DESDE;

    IF v_count > 0 THEN
        p_Message := 'El rango de fechas se solapa con otro periodo.';
        RETURN;
    END IF;

    SELECT CNT.CNT_S_CODIGO_PERIODO.NEXTVAL INTO p_CODIGO_OUT FROM DUAL;

    INSERT INTO CNT.CNT_PERIODOS (
        CODIGO_PERIODO, NOMBRE_PERIODO, FECHA_DESDE, FECHA_HASTA, ANO_PERIODO, NUMERO_PERIODO,
        USUARIO_CIERRE, FECHA_CIERRE, EXTRA1, EXTRA2, EXTRA3, USUARIO_INS, FECHA_INS, CODIGO_EMPRESA
    ) VALUES (
        p_CODIGO_OUT, SUBSTR(p_NOMBRE_PERIODO, 1, 100), p_FECHA_DESDE, p_FECHA_HASTA, p_ANO_PERIODO, p_NUM_PERIODO,
        CASE WHEN NVL(p_CERRADO, 0) = 1 THEN p_USUARIO_ID ELSE NULL END,
        CASE WHEN NVL(p_CERRADO, 0) = 1 THEN SYSDATE ELSE NULL END,
        p_EXTRA1, p_EXTRA2, p_EXTRA3, p_USUARIO_ID, SYSDATE, p_CODIGO_EMPRESA
    );

    p_Message := 'OK';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_PER_UPD (
    p_CODIGO_PERIODO IN NUMBER,
    p_NOMBRE_PERIODO IN VARCHAR2,
    p_FECHA_DESDE    IN DATE,
    p_FECHA_HASTA    IN DATE,
    p_ANO_PERIODO    IN NUMBER,
    p_NUM_PERIODO    IN NUMBER,
    p_CERRADO        IN NUMBER,
    p_EXTRA1         IN VARCHAR2,
    p_EXTRA2         IN VARCHAR2,
    p_EXTRA3         IN VARCHAR2,
    p_USUARIO_ID     IN NUMBER,
    p_CODIGO_EMPRESA IN NUMBER,
    p_Message        OUT VARCHAR2
) AS
    v_count NUMBER;
BEGIN
    IF p_CODIGO_PERIODO IS NULL OR p_NOMBRE_PERIODO IS NULL OR p_FECHA_DESDE IS NULL OR p_FECHA_HASTA IS NULL OR p_ANO_PERIODO IS NULL OR p_NUM_PERIODO IS NULL THEN
        p_Message := 'Periodo, nombre, fechas, ano y numero son requeridos.';
        RETURN;
    END IF;

    IF p_FECHA_DESDE > p_FECHA_HASTA THEN
        p_Message := 'La fecha desde no puede ser mayor que la fecha hasta.';
        RETURN;
    END IF;

    SELECT COUNT(1) INTO v_count
      FROM CNT.CNT_PERIODOS
     WHERE CODIGO_PERIODO = p_CODIGO_PERIODO
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    IF v_count = 0 THEN
        p_Message := 'El periodo indicado no existe.';
        RETURN;
    END IF;

    SELECT COUNT(1) INTO v_count
      FROM CNT.CNT_PERIODOS
     WHERE CODIGO_PERIODO <> p_CODIGO_PERIODO
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA
       AND ANO_PERIODO = p_ANO_PERIODO
       AND NUMERO_PERIODO = p_NUM_PERIODO;

    IF v_count > 0 THEN
        p_Message := 'Ya existe un periodo con el mismo ano y numero.';
        RETURN;
    END IF;

    SELECT COUNT(1) INTO v_count
      FROM CNT.CNT_PERIODOS
     WHERE CODIGO_PERIODO <> p_CODIGO_PERIODO
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA
       AND p_FECHA_DESDE <= FECHA_HASTA
       AND p_FECHA_HASTA >= FECHA_DESDE;

    IF v_count > 0 THEN
        p_Message := 'El rango de fechas se solapa con otro periodo.';
        RETURN;
    END IF;

    UPDATE CNT.CNT_PERIODOS
       SET NOMBRE_PERIODO = SUBSTR(p_NOMBRE_PERIODO, 1, 100),
           FECHA_DESDE = p_FECHA_DESDE,
           FECHA_HASTA = p_FECHA_HASTA,
           ANO_PERIODO = p_ANO_PERIODO,
           NUMERO_PERIODO = p_NUM_PERIODO,
           FECHA_CIERRE = CASE WHEN NVL(p_CERRADO, 0) = 1 THEN NVL(FECHA_CIERRE, SYSDATE) ELSE NULL END,
           USUARIO_CIERRE = CASE WHEN NVL(p_CERRADO, 0) = 1 THEN NVL(USUARIO_CIERRE, p_USUARIO_ID) ELSE NULL END,
           EXTRA1 = p_EXTRA1,
           EXTRA2 = p_EXTRA2,
           EXTRA3 = p_EXTRA3,
           USUARIO_UPD = p_USUARIO_ID,
           FECHA_UPD = SYSDATE
     WHERE CODIGO_PERIODO = p_CODIGO_PERIODO
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    p_Message := 'OK';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_PER_DEL (
    p_CODIGO_PERIODO IN NUMBER,
    p_CODIGO_EMPRESA IN NUMBER,
    p_Message        OUT VARCHAR2
) AS
    v_count NUMBER;
BEGIN
    SELECT COUNT(1) INTO v_count
      FROM CNT.CNT_COMPROBANTES
     WHERE CODIGO_PERIODO = p_CODIGO_PERIODO
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    IF v_count > 0 THEN
        p_Message := 'No se puede eliminar el periodo porque tiene comprobantes.';
        RETURN;
    END IF;

    SELECT COUNT(1) INTO v_count
      FROM CNT.CNT_SALDOS
     WHERE CODIGO_PERIODO = p_CODIGO_PERIODO
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    IF v_count > 0 THEN
        p_Message := 'No se puede eliminar el periodo porque tiene saldos.';
        RETURN;
    END IF;

    SELECT COUNT(1) INTO v_count
      FROM CNT.CNT_CONCILIACIONES
     WHERE CODIGO_PERIODO = p_CODIGO_PERIODO
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    IF v_count > 0 THEN
        p_Message := 'No se puede eliminar el periodo porque tiene conciliaciones.';
        RETURN;
    END IF;

    DELETE FROM CNT.CNT_PERIODOS
     WHERE CODIGO_PERIODO = p_CODIGO_PERIODO
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    IF SQL%ROWCOUNT = 0 THEN
        p_Message := 'El periodo indicado no existe.';
        RETURN;
    END IF;

    p_Message := 'OK';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_PER_GEN_YEAR (
    p_ANO_PERIODO    IN NUMBER,
    p_USUARIO_ID     IN NUMBER,
    p_CODIGO_EMPRESA IN NUMBER,
    p_CANTIDAD_OUT   OUT NUMBER,
    p_Message        OUT VARCHAR2
) AS
    v_count NUMBER;
    v_inicio DATE;
    v_fin DATE;
    v_fecha_desde DATE;
    v_fecha_hasta DATE;
    v_codigo NUMBER;
    v_nombre VARCHAR2(100);
BEGIN
    p_CANTIDAD_OUT := 0;

    IF p_ANO_PERIODO IS NULL OR p_ANO_PERIODO < 1900 OR p_ANO_PERIODO > 9999 THEN
        p_Message := 'El ano del periodo no es valido.';
        RETURN;
    END IF;

    v_inicio := TO_DATE(TO_CHAR(p_ANO_PERIODO) || '0101', 'YYYYMMDD');
    v_fin := TO_DATE(TO_CHAR(p_ANO_PERIODO) || '1231', 'YYYYMMDD');

    SELECT COUNT(1) INTO v_count
      FROM CNT.CNT_PERIODOS
     WHERE CODIGO_EMPRESA = p_CODIGO_EMPRESA
       AND ANO_PERIODO = p_ANO_PERIODO;

    IF v_count > 0 THEN
        p_Message := 'Ya existen periodos para el ano indicado.';
        RETURN;
    END IF;

    SELECT COUNT(1) INTO v_count
      FROM CNT.CNT_PERIODOS
     WHERE CODIGO_EMPRESA = p_CODIGO_EMPRESA
       AND v_inicio <= FECHA_HASTA
       AND v_fin >= FECHA_DESDE;

    IF v_count > 0 THEN
        p_Message := 'El ano indicado se solapa con periodos existentes.';
        RETURN;
    END IF;

    FOR i IN 1..12 LOOP
        v_fecha_desde := ADD_MONTHS(v_inicio, i - 1);
        v_fecha_hasta := LAST_DAY(v_fecha_desde);
        v_nombre :=
            CASE i
                WHEN 1 THEN 'Enero'
                WHEN 2 THEN 'Febrero'
                WHEN 3 THEN 'Marzo'
                WHEN 4 THEN 'Abril'
                WHEN 5 THEN 'Mayo'
                WHEN 6 THEN 'Junio'
                WHEN 7 THEN 'Julio'
                WHEN 8 THEN 'Agosto'
                WHEN 9 THEN 'Septiembre'
                WHEN 10 THEN 'Octubre'
                WHEN 11 THEN 'Noviembre'
                ELSE 'Diciembre'
            END || ' ' || TO_CHAR(p_ANO_PERIODO);

        SELECT CNT.CNT_S_CODIGO_PERIODO.NEXTVAL INTO v_codigo FROM DUAL;

        INSERT INTO CNT.CNT_PERIODOS (
            CODIGO_PERIODO, NOMBRE_PERIODO, FECHA_DESDE, FECHA_HASTA, ANO_PERIODO, NUMERO_PERIODO,
            USUARIO_CIERRE, FECHA_CIERRE, EXTRA1, EXTRA2, EXTRA3, USUARIO_INS, FECHA_INS, CODIGO_EMPRESA
        ) VALUES (
            v_codigo, v_nombre, v_fecha_desde, v_fecha_hasta, p_ANO_PERIODO, i,
            NULL, NULL, NULL, NULL, NULL, p_USUARIO_ID, SYSDATE, p_CODIGO_EMPRESA
        );

        p_CANTIDAD_OUT := p_CANTIDAD_OUT + 1;
    END LOOP;

    p_Message := 'OK';
EXCEPTION
    WHEN OTHERS THEN
        p_CANTIDAD_OUT := 0;
        p_Message := SQLERRM;
END;
/
