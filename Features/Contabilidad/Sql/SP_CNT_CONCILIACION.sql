CREATE OR REPLACE PROCEDURE CNT.SP_CNT_BANCOS_GET (
    p_SEARCH_TEXT    IN  VARCHAR2,
    p_CODIGO_EMPRESA IN  NUMBER,
    p_ResultSet      OUT SYS_REFCURSOR,
    p_Message        OUT VARCHAR2
) AS
BEGIN
    OPEN p_ResultSet FOR
        SELECT
            b.CODIGO_BANCO,
            NVL(b.NOMBRE, '') AS NOMBRE,
            NVL(b.CODIGO_INTERBANCARIO, '') AS CODIGO_INTERBANCARIO,
            b.CODIGO_EMPRESA
        FROM SIS.SIS_BANCOS b
        WHERE b.CODIGO_EMPRESA = p_CODIGO_EMPRESA
          AND (
                p_SEARCH_TEXT IS NULL
             OR TRIM(p_SEARCH_TEXT) IS NULL
             OR UPPER(b.NOMBRE) LIKE '%' || UPPER(TRIM(p_SEARCH_TEXT)) || '%'
             OR UPPER(NVL(b.CODIGO_INTERBANCARIO, '')) LIKE '%' || UPPER(TRIM(p_SEARCH_TEXT)) || '%'
          )
        ORDER BY b.NOMBRE;

    p_Message := 'OK';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_EDO_CTA_GET (
    p_CODIGO_BANCO        IN  NUMBER,
    p_CODIGO_CUENTA_BANCO IN  NUMBER,
    p_FECHA_DESDE         IN  DATE,
    p_FECHA_HASTA         IN  DATE,
    p_SEARCH_TEXT         IN  VARCHAR2,
    p_CODIGO_EMPRESA      IN  NUMBER,
    p_ResultSet           OUT SYS_REFCURSOR,
    p_Message             OUT VARCHAR2
) AS
BEGIN
    OPEN p_ResultSet FOR
        SELECT
            e.CODIGO_ESTADO_CUENTA,
            e.CODIGO_CUENTA_BANCO,
            NVL(cb.NO_CUENTA, '') AS NO_CUENTA,
            NVL(b.CODIGO_BANCO, 0) AS CODIGO_BANCO,
            NVL(b.NOMBRE, '') AS BANCO,
            NVL(e.NUMERO_ESTADO_CUENTA, '') AS NUMERO_ESTADO_CUENTA,
            e.FECHA_DESDE,
            e.FECHA_HASTA,
            NVL(e.SALDO_INICIAL, 0) AS SALDO_INICIAL,
            NVL(e.SALDO_FINAL, 0) AS SALDO_FINAL,
            COUNT(d.CODIGO_DETALLE_EDO_CTA) AS CANTIDAD_MOVIMIENTOS,
            NVL(SUM(d.MONTO), 0) AS MONTO_MOVIMIENTOS,
            e.CODIGO_EMPRESA
        FROM CNT.CNT_ESTADO_CUENTAS e
        INNER JOIN SIS.SIS_CUENTAS_BANCOS cb
                ON cb.CODIGO_CUENTA_BANCO = e.CODIGO_CUENTA_BANCO
        INNER JOIN SIS.SIS_BANCOS b
                ON b.CODIGO_BANCO = cb.CODIGO_BANCO
        LEFT JOIN CNT.CNT_DETALLE_EDO_CTA d
               ON d.CODIGO_ESTADO_CUENTA = e.CODIGO_ESTADO_CUENTA
              AND d.CODIGO_EMPRESA = e.CODIGO_EMPRESA
        WHERE e.CODIGO_EMPRESA = p_CODIGO_EMPRESA
          AND (p_CODIGO_BANCO IS NULL OR b.CODIGO_BANCO = p_CODIGO_BANCO)
          AND (p_CODIGO_CUENTA_BANCO IS NULL OR e.CODIGO_CUENTA_BANCO = p_CODIGO_CUENTA_BANCO)
          AND (p_FECHA_DESDE IS NULL OR e.FECHA_HASTA >= TRUNC(p_FECHA_DESDE))
          AND (p_FECHA_HASTA IS NULL OR e.FECHA_DESDE <= TRUNC(p_FECHA_HASTA))
          AND (
                p_SEARCH_TEXT IS NULL
             OR TRIM(p_SEARCH_TEXT) IS NULL
             OR UPPER(NVL(e.NUMERO_ESTADO_CUENTA, '')) LIKE '%' || UPPER(TRIM(p_SEARCH_TEXT)) || '%'
             OR UPPER(NVL(cb.NO_CUENTA, '')) LIKE '%' || UPPER(TRIM(p_SEARCH_TEXT)) || '%'
             OR UPPER(NVL(b.NOMBRE, '')) LIKE '%' || UPPER(TRIM(p_SEARCH_TEXT)) || '%'
          )
        GROUP BY
            e.CODIGO_ESTADO_CUENTA,
            e.CODIGO_CUENTA_BANCO,
            cb.NO_CUENTA,
            b.CODIGO_BANCO,
            b.NOMBRE,
            e.NUMERO_ESTADO_CUENTA,
            e.FECHA_DESDE,
            e.FECHA_HASTA,
            e.SALDO_INICIAL,
            e.SALDO_FINAL,
            e.CODIGO_EMPRESA
        ORDER BY e.FECHA_HASTA DESC, b.NOMBRE, cb.NO_CUENTA, e.NUMERO_ESTADO_CUENTA DESC;

    p_Message := 'OK';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_EDO_CTA_DET_GET (
    p_CODIGO_ESTADO_CUENTA IN  NUMBER,
    p_STATUS               IN  VARCHAR2,
    p_SEARCH_TEXT          IN  VARCHAR2,
    p_CODIGO_EMPRESA       IN  NUMBER,
    p_ResultSet            OUT SYS_REFCURSOR,
    p_Message              OUT VARCHAR2
) AS
BEGIN
    OPEN p_ResultSet FOR
        SELECT
            d.CODIGO_DETALLE_EDO_CTA,
            d.CODIGO_ESTADO_CUENTA,
            d.TIPO_TRANSACCION_ID,
            NVL(td.DESCRIPCION, '') AS TIPO_TRANSACCION,
            NVL(d.NUMERO_TRANSACCION, '') AS NUMERO_TRANSACCION,
            d.FECHA_TRANSACCION,
            NVL(d.DESCRIPCION, '') AS DESCRIPCION,
            NVL(d.MONTO, 0) AS MONTO,
            NVL(d.STATUS, '') AS STATUS,
            d.CODIGO_EMPRESA
        FROM CNT.CNT_DETALLE_EDO_CTA d
        LEFT JOIN CNT.CNT_DESCRIPTIVAS td
               ON td.DESCRIPCION_ID = d.TIPO_TRANSACCION_ID
        WHERE d.CODIGO_EMPRESA = p_CODIGO_EMPRESA
          AND d.CODIGO_ESTADO_CUENTA = p_CODIGO_ESTADO_CUENTA
          AND (
                p_STATUS IS NULL
             OR TRIM(p_STATUS) IS NULL
             OR d.STATUS = UPPER(TRIM(p_STATUS))
          )
          AND (
                p_SEARCH_TEXT IS NULL
             OR TRIM(p_SEARCH_TEXT) IS NULL
             OR UPPER(NVL(d.NUMERO_TRANSACCION, '')) LIKE '%' || UPPER(TRIM(p_SEARCH_TEXT)) || '%'
             OR UPPER(NVL(d.DESCRIPCION, '')) LIKE '%' || UPPER(TRIM(p_SEARCH_TEXT)) || '%'
             OR UPPER(NVL(td.DESCRIPCION, '')) LIKE '%' || UPPER(TRIM(p_SEARCH_TEXT)) || '%'
          )
        ORDER BY d.FECHA_TRANSACCION, d.NUMERO_TRANSACCION, d.CODIGO_DETALLE_EDO_CTA;

    p_Message := 'OK';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_LIB_GET (
    p_CODIGO_BANCO        IN  NUMBER,
    p_CODIGO_CUENTA_BANCO IN  NUMBER,
    p_FECHA_DESDE         IN  DATE,
    p_FECHA_HASTA         IN  DATE,
    p_STATUS              IN  VARCHAR2,
    p_SEARCH_TEXT         IN  VARCHAR2,
    p_CODIGO_EMPRESA      IN  NUMBER,
    p_ResultSet           OUT SYS_REFCURSOR,
    p_Message             OUT VARCHAR2
) AS
BEGIN
    OPEN p_ResultSet FOR
        SELECT
            l.CODIGO_LIBRO,
            l.CODIGO_CUENTA_BANCO,
            NVL(cb.NO_CUENTA, '') AS NO_CUENTA,
            NVL(b.CODIGO_BANCO, 0) AS CODIGO_BANCO,
            NVL(b.NOMBRE, '') AS BANCO,
            l.FECHA_LIBRO,
            NVL(l.STATUS, '') AS STATUS,
            COUNT(d.CODIGO_DETALLE_LIBRO) AS CANTIDAD_MOVIMIENTOS,
            NVL(SUM(d.MONTO), 0) AS MONTO_MOVIMIENTOS,
            l.CODIGO_EMPRESA
        FROM CNT.CNT_LIBROS l
        INNER JOIN SIS.SIS_CUENTAS_BANCOS cb
                ON cb.CODIGO_CUENTA_BANCO = l.CODIGO_CUENTA_BANCO
        INNER JOIN SIS.SIS_BANCOS b
                ON b.CODIGO_BANCO = cb.CODIGO_BANCO
        LEFT JOIN CNT.CNT_DETALLE_LIBRO d
               ON d.CODIGO_LIBRO = l.CODIGO_LIBRO
              AND d.CODIGO_EMPRESA = l.CODIGO_EMPRESA
        WHERE l.CODIGO_EMPRESA = p_CODIGO_EMPRESA
          AND (p_CODIGO_BANCO IS NULL OR b.CODIGO_BANCO = p_CODIGO_BANCO)
          AND (p_CODIGO_CUENTA_BANCO IS NULL OR l.CODIGO_CUENTA_BANCO = p_CODIGO_CUENTA_BANCO)
          AND (p_FECHA_DESDE IS NULL OR l.FECHA_LIBRO >= TRUNC(p_FECHA_DESDE))
          AND (p_FECHA_HASTA IS NULL OR l.FECHA_LIBRO <= TRUNC(p_FECHA_HASTA))
          AND (
                p_STATUS IS NULL
             OR TRIM(p_STATUS) IS NULL
             OR l.STATUS = UPPER(TRIM(p_STATUS))
          )
          AND (
                p_SEARCH_TEXT IS NULL
             OR TRIM(p_SEARCH_TEXT) IS NULL
             OR UPPER(NVL(cb.NO_CUENTA, '')) LIKE '%' || UPPER(TRIM(p_SEARCH_TEXT)) || '%'
             OR UPPER(NVL(b.NOMBRE, '')) LIKE '%' || UPPER(TRIM(p_SEARCH_TEXT)) || '%'
          )
        GROUP BY
            l.CODIGO_LIBRO,
            l.CODIGO_CUENTA_BANCO,
            cb.NO_CUENTA,
            b.CODIGO_BANCO,
            b.NOMBRE,
            l.FECHA_LIBRO,
            l.STATUS,
            l.CODIGO_EMPRESA
        ORDER BY l.FECHA_LIBRO DESC, b.NOMBRE, cb.NO_CUENTA;

    p_Message := 'OK';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_LIB_DET_GET (
    p_CODIGO_LIBRO   IN  NUMBER,
    p_STATUS         IN  VARCHAR2,
    p_SEARCH_TEXT    IN  VARCHAR2,
    p_CODIGO_EMPRESA IN  NUMBER,
    p_ResultSet      OUT SYS_REFCURSOR,
    p_Message        OUT VARCHAR2
) AS
BEGIN
    OPEN p_ResultSet FOR
        SELECT
            d.CODIGO_DETALLE_LIBRO,
            d.CODIGO_LIBRO,
            d.TIPO_DOCUMENTO_ID,
            NVL(td.DESCRIPCION, '') AS TIPO_DOCUMENTO,
            d.CODIGO_CHEQUE,
            d.CODIGO_IDENTIFICADOR,
            d.ORIGEN_ID,
            NVL(d.NUMERO_DOCUMENTO, '') AS NUMERO_DOCUMENTO,
            NVL(d.DESCRIPCION, '') AS DESCRIPCION,
            NVL(d.MONTO, 0) AS MONTO,
            NVL(d.STATUS, '') AS STATUS,
            d.CODIGO_EMPRESA
        FROM CNT.CNT_DETALLE_LIBRO d
        LEFT JOIN CNT.CNT_DESCRIPTIVAS td
               ON td.DESCRIPCION_ID = d.TIPO_DOCUMENTO_ID
        WHERE d.CODIGO_EMPRESA = p_CODIGO_EMPRESA
          AND d.CODIGO_LIBRO = p_CODIGO_LIBRO
          AND (
                p_STATUS IS NULL
             OR TRIM(p_STATUS) IS NULL
             OR d.STATUS = UPPER(TRIM(p_STATUS))
          )
          AND (
                p_SEARCH_TEXT IS NULL
             OR TRIM(p_SEARCH_TEXT) IS NULL
             OR UPPER(NVL(d.NUMERO_DOCUMENTO, '')) LIKE '%' || UPPER(TRIM(p_SEARCH_TEXT)) || '%'
             OR UPPER(NVL(d.DESCRIPCION, '')) LIKE '%' || UPPER(TRIM(p_SEARCH_TEXT)) || '%'
             OR UPPER(NVL(td.DESCRIPCION, '')) LIKE '%' || UPPER(TRIM(p_SEARCH_TEXT)) || '%'
          )
        ORDER BY d.NUMERO_DOCUMENTO, d.CODIGO_DETALLE_LIBRO;

    p_Message := 'OK';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_LIB_GEN (
    p_CODIGO_CUENTA_BANCO IN  NUMBER,
    p_FECHA_DESDE         IN  DATE,
    p_FECHA_HASTA         IN  DATE,
    p_USUARIO_ID          IN  NUMBER,
    p_CODIGO_EMPRESA      IN  NUMBER,
    p_CANTIDAD_LIBROS_OUT IN OUT NUMBER,
    p_CANTIDAD_DET_OUT    IN OUT NUMBER,
    p_Message             OUT VARCHAR2
) AS
    v_CODIGO_MAYOR       SIS.SIS_CUENTAS_BANCOS.CODIGO_MAYOR%TYPE;
    v_CODIGO_AUXILIAR    SIS.SIS_CUENTAS_BANCOS.CODIGO_AUXILIAR%TYPE;
    v_CODIGO_LIBRO       CNT.CNT_LIBROS.CODIGO_LIBRO%TYPE;
    v_STATUS_LIBRO       CNT.CNT_LIBROS.STATUS%TYPE;
    v_LIBROS_CREADOS     NUMBER := 0;
    v_DETALLES_CREADOS   NUMBER := 0;
BEGIN
    SAVEPOINT SP_CNT_LIB_GEN_START;

    p_CANTIDAD_LIBROS_OUT := 0;
    p_CANTIDAD_DET_OUT := 0;

    IF p_CODIGO_CUENTA_BANCO IS NULL OR p_CODIGO_CUENTA_BANCO <= 0 THEN
        p_Message := 'Debe indicar la cuenta bancaria.';
        RETURN;
    END IF;

    IF p_FECHA_DESDE IS NULL OR p_FECHA_HASTA IS NULL THEN
        p_Message := 'Debe indicar el rango de fechas.';
        RETURN;
    END IF;

    IF TRUNC(p_FECHA_DESDE) > TRUNC(p_FECHA_HASTA) THEN
        p_Message := 'La fecha desde no puede ser mayor a la fecha hasta.';
        RETURN;
    END IF;

    BEGIN
        SELECT cb.CODIGO_MAYOR,
               cb.CODIGO_AUXILIAR
          INTO v_CODIGO_MAYOR,
               v_CODIGO_AUXILIAR
          FROM SIS.SIS_CUENTAS_BANCOS cb
         WHERE cb.CODIGO_CUENTA_BANCO = p_CODIGO_CUENTA_BANCO
           AND cb.CODIGO_EMPRESA = p_CODIGO_EMPRESA;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            p_Message := 'Cuenta bancaria no encontrada para la empresa.';
            RETURN;
    END;

    IF v_CODIGO_MAYOR IS NULL OR v_CODIGO_AUXILIAR IS NULL THEN
        p_Message := 'La cuenta bancaria no tiene mayor/auxiliar contable configurado.';
        RETURN;
    END IF;

    FOR dia IN (
        SELECT DISTINCT TRUNC(c.FECHA_COMPROBANTE) AS FECHA_LIBRO
          FROM CNT.CNT_COMPROBANTES c
          INNER JOIN CNT.CNT_DETALLE_COMPROBANTE d
                  ON d.CODIGO_COMPROBANTE = c.CODIGO_COMPROBANTE
         WHERE c.CODIGO_EMPRESA = p_CODIGO_EMPRESA
           AND d.CODIGO_EMPRESA = p_CODIGO_EMPRESA
           AND d.CODIGO_MAYOR = v_CODIGO_MAYOR
           AND d.CODIGO_AUXILIAR = v_CODIGO_AUXILIAR
           AND TRUNC(c.FECHA_COMPROBANTE) BETWEEN TRUNC(p_FECHA_DESDE) AND TRUNC(p_FECHA_HASTA)
         ORDER BY TRUNC(c.FECHA_COMPROBANTE)
    ) LOOP
        BEGIN
            SELECT l.CODIGO_LIBRO,
                   NVL(l.STATUS, 'A')
              INTO v_CODIGO_LIBRO,
                   v_STATUS_LIBRO
              FROM CNT.CNT_LIBROS l
             WHERE l.CODIGO_CUENTA_BANCO = p_CODIGO_CUENTA_BANCO
               AND l.CODIGO_EMPRESA = p_CODIGO_EMPRESA
               AND TRUNC(l.FECHA_LIBRO) = dia.FECHA_LIBRO
               AND ROWNUM = 1;
        EXCEPTION
            WHEN NO_DATA_FOUND THEN
                v_CODIGO_LIBRO := CNT.CNT_S_CODIGO_LIBRO.NEXTVAL;
                v_STATUS_LIBRO := 'A';

                INSERT INTO CNT.CNT_LIBROS (
                    CODIGO_LIBRO,
                    CODIGO_CUENTA_BANCO,
                    FECHA_LIBRO,
                    STATUS,
                    USUARIO_INS,
                    FECHA_INS,
                    CODIGO_EMPRESA
                ) VALUES (
                    v_CODIGO_LIBRO,
                    p_CODIGO_CUENTA_BANCO,
                    dia.FECHA_LIBRO,
                    'A',
                    p_USUARIO_ID,
                    SYSDATE,
                    p_CODIGO_EMPRESA
                );

                v_LIBROS_CREADOS := v_LIBROS_CREADOS + 1;
        END;

        IF v_STATUS_LIBRO = 'A' THEN
            FOR mov IN (
                SELECT
                    c.CODIGO_COMPROBANTE,
                    c.TIPO_COMPROBANTE_ID,
                    c.ORIGEN_ID,
                    SUBSTR(NVL(NULLIF(TRIM(d.REFERENCIA1), ''), c.NUMERO_COMPROBANTE), 1, 20) AS NUMERO_DOCUMENTO,
                    SUBSTR(NVL(NULLIF(TRIM(d.DESCRIPCION), ''), NVL(c.OBSERVACION, 'Movimiento contable bancario')), 1, 1000) AS DESCRIPCION,
                    SUM(NVL(d.MONTO, 0)) AS MONTO
                  FROM CNT.CNT_COMPROBANTES c
                  INNER JOIN CNT.CNT_DETALLE_COMPROBANTE d
                          ON d.CODIGO_COMPROBANTE = c.CODIGO_COMPROBANTE
                 WHERE c.CODIGO_EMPRESA = p_CODIGO_EMPRESA
                   AND d.CODIGO_EMPRESA = p_CODIGO_EMPRESA
                   AND d.CODIGO_MAYOR = v_CODIGO_MAYOR
                   AND d.CODIGO_AUXILIAR = v_CODIGO_AUXILIAR
                   AND TRUNC(c.FECHA_COMPROBANTE) = dia.FECHA_LIBRO
                 GROUP BY
                    c.CODIGO_COMPROBANTE,
                    c.TIPO_COMPROBANTE_ID,
                    c.ORIGEN_ID,
                    SUBSTR(NVL(NULLIF(TRIM(d.REFERENCIA1), ''), c.NUMERO_COMPROBANTE), 1, 20),
                    SUBSTR(NVL(NULLIF(TRIM(d.DESCRIPCION), ''), NVL(c.OBSERVACION, 'Movimiento contable bancario')), 1, 1000)
            ) LOOP
                BEGIN
                    INSERT INTO CNT.CNT_DETALLE_LIBRO (
                        CODIGO_DETALLE_LIBRO,
                        CODIGO_LIBRO,
                        TIPO_DOCUMENTO_ID,
                        CODIGO_IDENTIFICADOR,
                        ORIGEN_ID,
                        NUMERO_DOCUMENTO,
                        DESCRIPCION,
                        MONTO,
                        STATUS,
                        USUARIO_INS,
                        FECHA_INS,
                        CODIGO_EMPRESA
                    )
                    SELECT
                        CNT.CNT_S_CODIGO_DETALLE_LIBRO.NEXTVAL,
                        v_CODIGO_LIBRO,
                        mov.TIPO_COMPROBANTE_ID,
                        mov.CODIGO_COMPROBANTE,
                        mov.ORIGEN_ID,
                        mov.NUMERO_DOCUMENTO,
                        mov.DESCRIPCION,
                        mov.MONTO,
                        'A',
                        p_USUARIO_ID,
                        SYSDATE,
                        p_CODIGO_EMPRESA
                      FROM DUAL
                     WHERE NOT EXISTS (
                        SELECT 1
                          FROM CNT.CNT_DETALLE_LIBRO dl
                         WHERE dl.CODIGO_LIBRO = v_CODIGO_LIBRO
                           AND dl.TIPO_DOCUMENTO_ID = mov.TIPO_COMPROBANTE_ID
                           AND dl.NUMERO_DOCUMENTO = mov.NUMERO_DOCUMENTO
                     );

                    v_DETALLES_CREADOS := v_DETALLES_CREADOS + SQL%ROWCOUNT;
                EXCEPTION
                    WHEN DUP_VAL_ON_INDEX THEN
                        NULL;
                END;
            END LOOP;
        END IF;
    END LOOP;

    p_CANTIDAD_LIBROS_OUT := v_LIBROS_CREADOS;
    p_CANTIDAD_DET_OUT := v_DETALLES_CREADOS;
    p_Message := 'OK';
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK TO SP_CNT_LIB_GEN_START;
        p_CANTIDAD_LIBROS_OUT := 0;
        p_CANTIDAD_DET_OUT := 0;
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_CONC_BCO_GET (
    p_CODIGO_CONCILIACION IN  NUMBER,
    p_SOLO_PENDIENTES     IN  NUMBER,
    p_SEARCH_TEXT         IN  VARCHAR2,
    p_CODIGO_EMPRESA      IN  NUMBER,
    p_ResultSet           OUT SYS_REFCURSOR,
    p_Message             OUT VARCHAR2
) AS
BEGIN
    OPEN p_ResultSet FOR
        SELECT
            d.CODIGO_DETALLE_EDO_CTA,
            e.CODIGO_ESTADO_CUENTA,
            NVL(e.NUMERO_ESTADO_CUENTA, '') AS NUMERO_ESTADO_CUENTA,
            d.TIPO_TRANSACCION_ID,
            NVL(t.DESCRIPCION, '') AS TIPO_TRANSACCION,
            NVL(d.NUMERO_TRANSACCION, '') AS NUMERO_TRANSACCION,
            d.FECHA_TRANSACCION,
            NVL(d.DESCRIPCION, '') AS DESCRIPCION,
            NVL(d.MONTO, 0) AS MONTO,
            NVL(d.STATUS, '') AS STATUS,
            tmp.CODIGO_TMP_CONCILIACION,
            CASE WHEN tmp.CODIGO_TMP_CONCILIACION IS NULL THEN 0 ELSE 1 END AS EN_TEMPORAL,
            d.CODIGO_EMPRESA
        FROM CNT.CNT_CONCILIACIONES c
        INNER JOIN CNT.CNT_PERIODOS p
            ON p.CODIGO_PERIODO = c.CODIGO_PERIODO
           AND p.CODIGO_EMPRESA = c.CODIGO_EMPRESA
        INNER JOIN CNT.CNT_ESTADO_CUENTAS e
            ON e.CODIGO_CUENTA_BANCO = c.CODIGO_CUENTA_BANCO
           AND e.CODIGO_EMPRESA = c.CODIGO_EMPRESA
        INNER JOIN CNT.CNT_DETALLE_EDO_CTA d
            ON d.CODIGO_ESTADO_CUENTA = e.CODIGO_ESTADO_CUENTA
           AND d.CODIGO_EMPRESA = c.CODIGO_EMPRESA
        LEFT JOIN CNT.CNT_DESCRIPTIVAS t
            ON t.DESCRIPCION_ID = d.TIPO_TRANSACCION_ID
           AND t.CODIGO_EMPRESA = c.CODIGO_EMPRESA
        LEFT JOIN CNT.CNT_TMP_CONCILIACION tmp
            ON tmp.CODIGO_CONCILIACION = c.CODIGO_CONCILIACION
           AND tmp.CODIGO_DETALLE_EDO_CTA = d.CODIGO_DETALLE_EDO_CTA
           AND tmp.CODIGO_EMPRESA = c.CODIGO_EMPRESA
        WHERE c.CODIGO_CONCILIACION = p_CODIGO_CONCILIACION
          AND c.CODIGO_EMPRESA = p_CODIGO_EMPRESA
          AND d.FECHA_TRANSACCION BETWEEN p.FECHA_DESDE AND p.FECHA_HASTA
          AND (NVL(p_SOLO_PENDIENTES, 0) = 0 OR tmp.CODIGO_TMP_CONCILIACION IS NULL)
          AND (
                p_SEARCH_TEXT IS NULL
             OR TRIM(p_SEARCH_TEXT) IS NULL
             OR UPPER(NVL(d.NUMERO_TRANSACCION, '')) LIKE '%' || UPPER(TRIM(p_SEARCH_TEXT)) || '%'
             OR UPPER(NVL(d.DESCRIPCION, '')) LIKE '%' || UPPER(TRIM(p_SEARCH_TEXT)) || '%'
             OR UPPER(NVL(t.DESCRIPCION, '')) LIKE '%' || UPPER(TRIM(p_SEARCH_TEXT)) || '%'
          )
        ORDER BY d.FECHA_TRANSACCION, d.NUMERO_TRANSACCION, d.CODIGO_DETALLE_EDO_CTA;

    p_Message := 'OK';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_CONC_LIB_GET (
    p_CODIGO_CONCILIACION IN  NUMBER,
    p_SOLO_PENDIENTES     IN  NUMBER,
    p_SEARCH_TEXT         IN  VARCHAR2,
    p_CODIGO_EMPRESA      IN  NUMBER,
    p_ResultSet           OUT SYS_REFCURSOR,
    p_Message             OUT VARCHAR2
) AS
BEGIN
    OPEN p_ResultSet FOR
        SELECT
            d.CODIGO_DETALLE_LIBRO,
            l.CODIGO_LIBRO,
            l.FECHA_LIBRO,
            d.TIPO_DOCUMENTO_ID,
            NVL(t.DESCRIPCION, '') AS TIPO_DOCUMENTO,
            d.CODIGO_CHEQUE,
            d.CODIGO_IDENTIFICADOR,
            d.ORIGEN_ID,
            NVL(d.NUMERO_DOCUMENTO, '') AS NUMERO_DOCUMENTO,
            NVL(d.DESCRIPCION, '') AS DESCRIPCION,
            NVL(d.MONTO, 0) AS MONTO,
            NVL(d.STATUS, '') AS STATUS,
            tmp.CODIGO_TMP_CONCILIACION,
            CASE WHEN tmp.CODIGO_TMP_CONCILIACION IS NULL THEN 0 ELSE 1 END AS EN_TEMPORAL,
            d.CODIGO_EMPRESA
        FROM CNT.CNT_CONCILIACIONES c
        INNER JOIN CNT.CNT_PERIODOS p
            ON p.CODIGO_PERIODO = c.CODIGO_PERIODO
           AND p.CODIGO_EMPRESA = c.CODIGO_EMPRESA
        INNER JOIN CNT.CNT_LIBROS l
            ON l.CODIGO_CUENTA_BANCO = c.CODIGO_CUENTA_BANCO
           AND l.CODIGO_EMPRESA = c.CODIGO_EMPRESA
        INNER JOIN CNT.CNT_DETALLE_LIBRO d
            ON d.CODIGO_LIBRO = l.CODIGO_LIBRO
           AND d.CODIGO_EMPRESA = c.CODIGO_EMPRESA
        LEFT JOIN CNT.CNT_DESCRIPTIVAS t
            ON t.DESCRIPCION_ID = d.TIPO_DOCUMENTO_ID
           AND t.CODIGO_EMPRESA = c.CODIGO_EMPRESA
        LEFT JOIN CNT.CNT_TMP_CONCILIACION tmp
            ON tmp.CODIGO_CONCILIACION = c.CODIGO_CONCILIACION
           AND tmp.CODIGO_DETALLE_LIBRO = d.CODIGO_DETALLE_LIBRO
           AND tmp.CODIGO_EMPRESA = c.CODIGO_EMPRESA
        WHERE c.CODIGO_CONCILIACION = p_CODIGO_CONCILIACION
          AND c.CODIGO_EMPRESA = p_CODIGO_EMPRESA
          AND l.FECHA_LIBRO BETWEEN p.FECHA_DESDE AND p.FECHA_HASTA
          AND (NVL(p_SOLO_PENDIENTES, 0) = 0 OR tmp.CODIGO_TMP_CONCILIACION IS NULL)
          AND (
                p_SEARCH_TEXT IS NULL
             OR TRIM(p_SEARCH_TEXT) IS NULL
             OR UPPER(NVL(d.NUMERO_DOCUMENTO, '')) LIKE '%' || UPPER(TRIM(p_SEARCH_TEXT)) || '%'
             OR UPPER(NVL(d.DESCRIPCION, '')) LIKE '%' || UPPER(TRIM(p_SEARCH_TEXT)) || '%'
             OR UPPER(NVL(t.DESCRIPCION, '')) LIKE '%' || UPPER(TRIM(p_SEARCH_TEXT)) || '%'
          )
        ORDER BY l.FECHA_LIBRO, d.NUMERO_DOCUMENTO, d.CODIGO_DETALLE_LIBRO;

    p_Message := 'OK';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_CONC_TMP_GET (
    p_CODIGO_CONCILIACION IN  NUMBER,
    p_SEARCH_TEXT         IN  VARCHAR2,
    p_CODIGO_EMPRESA      IN  NUMBER,
    p_ResultSet           OUT SYS_REFCURSOR,
    p_Message             OUT VARCHAR2
) AS
BEGIN
    OPEN p_ResultSet FOR
        SELECT
            tmp.CODIGO_TMP_CONCILIACION,
            tmp.CODIGO_CONCILIACION,
            tmp.CODIGO_PERIODO,
            tmp.CODIGO_CUENTA_BANCO,
            tmp.CODIGO_DETALLE_LIBRO,
            tmp.CODIGO_DETALLE_EDO_CTA,
            tmp.FECHA,
            NVL(tmp.NUMERO, '') AS NUMERO,
            NVL(tmp.MONTO, 0) AS MONTO,
            CASE
                WHEN tmp.CODIGO_DETALLE_EDO_CTA IS NOT NULL AND tmp.CODIGO_DETALLE_LIBRO IS NOT NULL THEN 'MATCH'
                WHEN tmp.CODIGO_DETALLE_EDO_CTA IS NOT NULL THEN 'BANCO'
                WHEN tmp.CODIGO_DETALLE_LIBRO IS NOT NULL THEN 'LIBRO'
                ELSE 'SIN_ORIGEN'
            END AS TIPO,
            NVL(bco.DESCRIPCION, '') AS BANCO_DESCRIPCION,
            NVL(lib.DESCRIPCION, '') AS LIBRO_DESCRIPCION,
            tmp.CODIGO_EMPRESA
        FROM CNT.CNT_TMP_CONCILIACION tmp
        LEFT JOIN CNT.CNT_DETALLE_EDO_CTA bco
            ON bco.CODIGO_DETALLE_EDO_CTA = tmp.CODIGO_DETALLE_EDO_CTA
           AND bco.CODIGO_EMPRESA = tmp.CODIGO_EMPRESA
        LEFT JOIN CNT.CNT_DETALLE_LIBRO lib
            ON lib.CODIGO_DETALLE_LIBRO = tmp.CODIGO_DETALLE_LIBRO
           AND lib.CODIGO_EMPRESA = tmp.CODIGO_EMPRESA
        WHERE tmp.CODIGO_CONCILIACION = p_CODIGO_CONCILIACION
          AND tmp.CODIGO_EMPRESA = p_CODIGO_EMPRESA
          AND (
                p_SEARCH_TEXT IS NULL
             OR TRIM(p_SEARCH_TEXT) IS NULL
             OR UPPER(NVL(tmp.NUMERO, '')) LIKE '%' || UPPER(TRIM(p_SEARCH_TEXT)) || '%'
             OR UPPER(NVL(bco.DESCRIPCION, '')) LIKE '%' || UPPER(TRIM(p_SEARCH_TEXT)) || '%'
             OR UPPER(NVL(lib.DESCRIPCION, '')) LIKE '%' || UPPER(TRIM(p_SEARCH_TEXT)) || '%'
          )
        ORDER BY tmp.FECHA, tmp.NUMERO, tmp.CODIGO_TMP_CONCILIACION;

    p_Message := 'OK';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_CONC_SUG_GET (
    p_CODIGO_CONCILIACION IN  NUMBER,
    p_TOLERANCIA_DIAS     IN  NUMBER,
    p_TOLERANCIA_MONTO    IN  NUMBER,
    p_MAX_ROWS            IN  NUMBER,
    p_CODIGO_EMPRESA      IN  NUMBER,
    p_ResultSet           OUT SYS_REFCURSOR,
    p_Message             OUT VARCHAR2
) AS
BEGIN
    OPEN p_ResultSet FOR
        WITH banco AS (
            SELECT
                d.CODIGO_DETALLE_EDO_CTA,
                NVL(d.NUMERO_TRANSACCION, '') AS NUMERO_TRANSACCION,
                d.FECHA_TRANSACCION,
                NVL(d.DESCRIPCION, '') AS DESCRIPCION,
                NVL(d.MONTO, 0) AS MONTO
            FROM CNT.CNT_CONCILIACIONES c
            INNER JOIN CNT.CNT_PERIODOS p
                ON p.CODIGO_PERIODO = c.CODIGO_PERIODO
               AND p.CODIGO_EMPRESA = c.CODIGO_EMPRESA
            INNER JOIN CNT.CNT_ESTADO_CUENTAS e
                ON e.CODIGO_CUENTA_BANCO = c.CODIGO_CUENTA_BANCO
               AND e.CODIGO_EMPRESA = c.CODIGO_EMPRESA
            INNER JOIN CNT.CNT_DETALLE_EDO_CTA d
                ON d.CODIGO_ESTADO_CUENTA = e.CODIGO_ESTADO_CUENTA
               AND d.CODIGO_EMPRESA = c.CODIGO_EMPRESA
            LEFT JOIN CNT.CNT_TMP_CONCILIACION tmp
                ON tmp.CODIGO_CONCILIACION = c.CODIGO_CONCILIACION
               AND tmp.CODIGO_DETALLE_EDO_CTA = d.CODIGO_DETALLE_EDO_CTA
               AND tmp.CODIGO_EMPRESA = c.CODIGO_EMPRESA
            WHERE c.CODIGO_CONCILIACION = p_CODIGO_CONCILIACION
              AND c.CODIGO_EMPRESA = p_CODIGO_EMPRESA
              AND d.FECHA_TRANSACCION BETWEEN p.FECHA_DESDE AND p.FECHA_HASTA
              AND tmp.CODIGO_TMP_CONCILIACION IS NULL
        ),
        libro AS (
            SELECT
                d.CODIGO_DETALLE_LIBRO,
                NVL(d.NUMERO_DOCUMENTO, '') AS NUMERO_DOCUMENTO,
                l.FECHA_LIBRO,
                NVL(d.DESCRIPCION, '') AS DESCRIPCION,
                NVL(d.MONTO, 0) AS MONTO
            FROM CNT.CNT_CONCILIACIONES c
            INNER JOIN CNT.CNT_PERIODOS p
                ON p.CODIGO_PERIODO = c.CODIGO_PERIODO
               AND p.CODIGO_EMPRESA = c.CODIGO_EMPRESA
            INNER JOIN CNT.CNT_LIBROS l
                ON l.CODIGO_CUENTA_BANCO = c.CODIGO_CUENTA_BANCO
               AND l.CODIGO_EMPRESA = c.CODIGO_EMPRESA
            INNER JOIN CNT.CNT_DETALLE_LIBRO d
                ON d.CODIGO_LIBRO = l.CODIGO_LIBRO
               AND d.CODIGO_EMPRESA = c.CODIGO_EMPRESA
            LEFT JOIN CNT.CNT_TMP_CONCILIACION tmp
                ON tmp.CODIGO_CONCILIACION = c.CODIGO_CONCILIACION
               AND tmp.CODIGO_DETALLE_LIBRO = d.CODIGO_DETALLE_LIBRO
               AND tmp.CODIGO_EMPRESA = c.CODIGO_EMPRESA
            WHERE c.CODIGO_CONCILIACION = p_CODIGO_CONCILIACION
              AND c.CODIGO_EMPRESA = p_CODIGO_EMPRESA
              AND l.FECHA_LIBRO BETWEEN p.FECHA_DESDE AND p.FECHA_HASTA
              AND tmp.CODIGO_TMP_CONCILIACION IS NULL
        ),
        candidatos AS (
            SELECT
                b.CODIGO_DETALLE_EDO_CTA,
                l.CODIGO_DETALLE_LIBRO,
                b.FECHA_TRANSACCION AS BANCO_FECHA,
                l.FECHA_LIBRO,
                b.NUMERO_TRANSACCION,
                l.NUMERO_DOCUMENTO,
                b.DESCRIPCION AS BANCO_DESCRIPCION,
                l.DESCRIPCION AS LIBRO_DESCRIPCION,
                b.MONTO AS BANCO_MONTO,
                l.MONTO AS LIBRO_MONTO,
                ABS(NVL(b.MONTO, 0) - NVL(l.MONTO, 0)) AS DIFERENCIA_MONTO,
                ABS(TRUNC(b.FECHA_TRANSACCION) - TRUNC(l.FECHA_LIBRO)) AS DIFERENCIA_DIAS,
                CASE WHEN ABS(NVL(b.MONTO, 0) - NVL(l.MONTO, 0)) <= NVL(p_TOLERANCIA_MONTO, 0) THEN 1 ELSE 0 END AS MATCH_MONTO,
                CASE
                    WHEN TRIM(NVL(b.NUMERO_TRANSACCION, '')) IS NOT NULL
                     AND UPPER(TRIM(NVL(b.NUMERO_TRANSACCION, ''))) = UPPER(TRIM(NVL(l.NUMERO_DOCUMENTO, '')))
                    THEN 1 ELSE 0
                END AS MATCH_NUMERO,
                CASE WHEN ABS(TRUNC(b.FECHA_TRANSACCION) - TRUNC(l.FECHA_LIBRO)) <= NVL(p_TOLERANCIA_DIAS, 0) THEN 1 ELSE 0 END AS MATCH_FECHA
            FROM banco b
            INNER JOIN libro l
                ON ABS(NVL(b.MONTO, 0) - NVL(l.MONTO, 0)) <= NVL(p_TOLERANCIA_MONTO, 0)
                OR (
                    TRIM(NVL(b.NUMERO_TRANSACCION, '')) IS NOT NULL
                    AND UPPER(TRIM(NVL(b.NUMERO_TRANSACCION, ''))) = UPPER(TRIM(NVL(l.NUMERO_DOCUMENTO, '')))
                )
                OR ABS(TRUNC(b.FECHA_TRANSACCION) - TRUNC(l.FECHA_LIBRO)) <= NVL(p_TOLERANCIA_DIAS, 0)
        )
        SELECT *
        FROM (
            SELECT
                CODIGO_DETALLE_EDO_CTA,
                CODIGO_DETALLE_LIBRO,
                BANCO_FECHA,
                FECHA_LIBRO,
                NUMERO_TRANSACCION,
                NUMERO_DOCUMENTO,
                BANCO_DESCRIPCION,
                LIBRO_DESCRIPCION,
                BANCO_MONTO,
                LIBRO_MONTO,
                DIFERENCIA_MONTO,
                DIFERENCIA_DIAS,
                MATCH_MONTO,
                MATCH_NUMERO,
                MATCH_FECHA,
                (MATCH_MONTO * 45) + (MATCH_NUMERO * 35) + (MATCH_FECHA * 20) AS SCORE,
                TRIM(BOTH ',' FROM
                    CASE WHEN MATCH_MONTO = 1 THEN 'MONTO,' ELSE '' END ||
                    CASE WHEN MATCH_NUMERO = 1 THEN 'NUMERO,' ELSE '' END ||
                    CASE WHEN MATCH_FECHA = 1 THEN 'FECHA,' ELSE '' END
                ) AS MOTIVOS,
                p_CODIGO_EMPRESA AS CODIGO_EMPRESA
            FROM candidatos
            ORDER BY SCORE DESC, DIFERENCIA_MONTO, DIFERENCIA_DIAS, BANCO_FECHA
        )
        WHERE ROWNUM <= NVL(p_MAX_ROWS, 100);

    p_Message := 'OK';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_CONC_MATCH (
    p_CODIGO_CONCILIACION    IN  NUMBER,
    p_CODIGO_DETALLE_EDO_CTA IN  NUMBER,
    p_CODIGO_DETALLE_LIBRO   IN  NUMBER,
    p_USUARIO_ID             IN  NUMBER,
    p_CODIGO_EMPRESA         IN  NUMBER,
    p_CODIGO_TMP             OUT NUMBER,
    p_Message                OUT VARCHAR2
) AS
    v_CODIGO_PERIODO      CNT.CNT_CONCILIACIONES.CODIGO_PERIODO%TYPE;
    v_CODIGO_CUENTA_BANCO CNT.CNT_CONCILIACIONES.CODIGO_CUENTA_BANCO%TYPE;
    v_FECHA_CIERRE        CNT.CNT_CONCILIACIONES.FECHA_CIERRE%TYPE;
    v_BANCO_FECHA         CNT.CNT_DETALLE_EDO_CTA.FECHA_TRANSACCION%TYPE;
    v_BANCO_NUMERO        CNT.CNT_DETALLE_EDO_CTA.NUMERO_TRANSACCION%TYPE;
    v_BANCO_MONTO         CNT.CNT_DETALLE_EDO_CTA.MONTO%TYPE;
    v_LIBRO_FECHA         CNT.CNT_LIBROS.FECHA_LIBRO%TYPE;
    v_LIBRO_NUMERO        CNT.CNT_DETALLE_LIBRO.NUMERO_DOCUMENTO%TYPE;
    v_LIBRO_MONTO         CNT.CNT_DETALLE_LIBRO.MONTO%TYPE;
    v_FECHA               DATE;
    v_NUMERO              VARCHAR2(20);
    v_MONTO               NUMBER;
    v_COUNT               NUMBER;
BEGIN
    p_CODIGO_TMP := NULL;

    IF p_CODIGO_CONCILIACION IS NULL OR p_CODIGO_CONCILIACION <= 0 THEN
        p_Message := 'CodigoConciliacion es requerido.';
        RETURN;
    END IF;

    IF p_CODIGO_DETALLE_EDO_CTA IS NULL AND p_CODIGO_DETALLE_LIBRO IS NULL THEN
        p_Message := 'Debe seleccionar un movimiento de banco, libro o ambos.';
        RETURN;
    END IF;

    BEGIN
        SELECT c.CODIGO_PERIODO, c.CODIGO_CUENTA_BANCO, c.FECHA_CIERRE
          INTO v_CODIGO_PERIODO, v_CODIGO_CUENTA_BANCO, v_FECHA_CIERRE
          FROM CNT.CNT_CONCILIACIONES c
         WHERE c.CODIGO_CONCILIACION = p_CODIGO_CONCILIACION
           AND c.CODIGO_EMPRESA = p_CODIGO_EMPRESA;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            p_Message := 'Conciliacion no encontrada.';
            RETURN;
    END;

    IF v_FECHA_CIERRE IS NOT NULL THEN
        p_Message := 'La conciliacion esta cerrada.';
        RETURN;
    END IF;

    IF p_CODIGO_DETALLE_EDO_CTA IS NOT NULL THEN
        SELECT COUNT(1)
          INTO v_COUNT
          FROM CNT.CNT_TMP_CONCILIACION tmp
         WHERE tmp.CODIGO_CONCILIACION = p_CODIGO_CONCILIACION
           AND tmp.CODIGO_DETALLE_EDO_CTA = p_CODIGO_DETALLE_EDO_CTA
           AND tmp.CODIGO_EMPRESA = p_CODIGO_EMPRESA;

        IF v_COUNT > 0 THEN
            p_Message := 'El movimiento de banco ya esta en conciliacion temporal.';
            RETURN;
        END IF;

        BEGIN
            SELECT d.FECHA_TRANSACCION,
                   NVL(d.NUMERO_TRANSACCION, ''),
                   NVL(d.MONTO, 0)
              INTO v_BANCO_FECHA, v_BANCO_NUMERO, v_BANCO_MONTO
              FROM CNT.CNT_CONCILIACIONES c
              INNER JOIN CNT.CNT_PERIODOS p
                 ON p.CODIGO_PERIODO = c.CODIGO_PERIODO
                AND p.CODIGO_EMPRESA = c.CODIGO_EMPRESA
              INNER JOIN CNT.CNT_ESTADO_CUENTAS e
                 ON e.CODIGO_CUENTA_BANCO = c.CODIGO_CUENTA_BANCO
                AND e.CODIGO_EMPRESA = c.CODIGO_EMPRESA
              INNER JOIN CNT.CNT_DETALLE_EDO_CTA d
                 ON d.CODIGO_ESTADO_CUENTA = e.CODIGO_ESTADO_CUENTA
                AND d.CODIGO_EMPRESA = c.CODIGO_EMPRESA
             WHERE c.CODIGO_CONCILIACION = p_CODIGO_CONCILIACION
               AND c.CODIGO_EMPRESA = p_CODIGO_EMPRESA
               AND d.CODIGO_DETALLE_EDO_CTA = p_CODIGO_DETALLE_EDO_CTA
               AND d.FECHA_TRANSACCION BETWEEN p.FECHA_DESDE AND p.FECHA_HASTA;
        EXCEPTION
            WHEN NO_DATA_FOUND THEN
                p_Message := 'El movimiento de banco no pertenece a la conciliacion.';
                RETURN;
        END;
    END IF;

    IF p_CODIGO_DETALLE_LIBRO IS NOT NULL THEN
        SELECT COUNT(1)
          INTO v_COUNT
          FROM CNT.CNT_TMP_CONCILIACION tmp
         WHERE tmp.CODIGO_CONCILIACION = p_CODIGO_CONCILIACION
           AND tmp.CODIGO_DETALLE_LIBRO = p_CODIGO_DETALLE_LIBRO
           AND tmp.CODIGO_EMPRESA = p_CODIGO_EMPRESA;

        IF v_COUNT > 0 THEN
            p_Message := 'El movimiento de libro ya esta en conciliacion temporal.';
            RETURN;
        END IF;

        BEGIN
            SELECT l.FECHA_LIBRO,
                   NVL(d.NUMERO_DOCUMENTO, ''),
                   NVL(d.MONTO, 0)
              INTO v_LIBRO_FECHA, v_LIBRO_NUMERO, v_LIBRO_MONTO
              FROM CNT.CNT_CONCILIACIONES c
              INNER JOIN CNT.CNT_PERIODOS p
                 ON p.CODIGO_PERIODO = c.CODIGO_PERIODO
                AND p.CODIGO_EMPRESA = c.CODIGO_EMPRESA
              INNER JOIN CNT.CNT_LIBROS l
                 ON l.CODIGO_CUENTA_BANCO = c.CODIGO_CUENTA_BANCO
                AND l.CODIGO_EMPRESA = c.CODIGO_EMPRESA
              INNER JOIN CNT.CNT_DETALLE_LIBRO d
                 ON d.CODIGO_LIBRO = l.CODIGO_LIBRO
                AND d.CODIGO_EMPRESA = c.CODIGO_EMPRESA
             WHERE c.CODIGO_CONCILIACION = p_CODIGO_CONCILIACION
               AND c.CODIGO_EMPRESA = p_CODIGO_EMPRESA
               AND d.CODIGO_DETALLE_LIBRO = p_CODIGO_DETALLE_LIBRO
               AND l.FECHA_LIBRO BETWEEN p.FECHA_DESDE AND p.FECHA_HASTA;
        EXCEPTION
            WHEN NO_DATA_FOUND THEN
                p_Message := 'El movimiento de libro no pertenece a la conciliacion.';
                RETURN;
        END;
    END IF;

    SELECT CNT.CNT_S_CODIGO_TMP_CONCILIACION.NEXTVAL
      INTO p_CODIGO_TMP
      FROM DUAL;

    v_FECHA := NVL(v_BANCO_FECHA, v_LIBRO_FECHA);
    v_NUMERO := SUBSTR(NVL(v_BANCO_NUMERO, v_LIBRO_NUMERO), 1, 20);
    IF v_NUMERO IS NULL THEN
        v_NUMERO := TO_CHAR(p_CODIGO_TMP);
    END IF;
    v_MONTO := NVL(v_BANCO_MONTO, v_LIBRO_MONTO);

    INSERT INTO CNT.CNT_TMP_CONCILIACION (
        CODIGO_TMP_CONCILIACION,
        CODIGO_CONCILIACION,
        CODIGO_PERIODO,
        CODIGO_CUENTA_BANCO,
        CODIGO_DETALLE_LIBRO,
        CODIGO_DETALLE_EDO_CTA,
        FECHA,
        NUMERO,
        MONTO,
        EXTRA1,
        USUARIO_INS,
        FECHA_INS,
        CODIGO_EMPRESA
    ) VALUES (
        p_CODIGO_TMP,
        p_CODIGO_CONCILIACION,
        v_CODIGO_PERIODO,
        v_CODIGO_CUENTA_BANCO,
        p_CODIGO_DETALLE_LIBRO,
        p_CODIGO_DETALLE_EDO_CTA,
        v_FECHA,
        v_NUMERO,
        NVL(v_MONTO, 0),
        'MATCH_MANUAL',
        p_USUARIO_ID,
        SYSDATE,
        p_CODIGO_EMPRESA
    );

    p_Message := 'OK';
EXCEPTION
    WHEN OTHERS THEN
        p_CODIGO_TMP := NULL;
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_CONC_UNMATCH (
    p_CODIGO_TMP     IN  NUMBER,
    p_USUARIO_ID     IN  NUMBER,
    p_CODIGO_EMPRESA IN  NUMBER,
    p_Message        OUT VARCHAR2
) AS
    v_COUNT NUMBER;
BEGIN
    IF p_CODIGO_TMP IS NULL OR p_CODIGO_TMP <= 0 THEN
        p_Message := 'CodigoTmpConciliacion es requerido.';
        RETURN;
    END IF;

    SELECT COUNT(1)
      INTO v_COUNT
      FROM CNT.CNT_TMP_CONCILIACION tmp
      INNER JOIN CNT.CNT_CONCILIACIONES c
         ON c.CODIGO_CONCILIACION = tmp.CODIGO_CONCILIACION
        AND c.CODIGO_EMPRESA = tmp.CODIGO_EMPRESA
     WHERE tmp.CODIGO_TMP_CONCILIACION = p_CODIGO_TMP
       AND tmp.CODIGO_EMPRESA = p_CODIGO_EMPRESA
       AND c.FECHA_CIERRE IS NULL;

    IF v_COUNT = 0 THEN
        p_Message := 'Temporal no encontrado o conciliacion cerrada.';
        RETURN;
    END IF;

    DELETE FROM CNT.CNT_TMP_CONCILIACION
     WHERE CODIGO_TMP_CONCILIACION = p_CODIGO_TMP
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    p_Message := 'OK';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_CONC_MATCH_MULTI (
    p_CODIGO_CONCILIACION IN  NUMBER,
    p_CODIGOS_EDO_CTA     IN  VARCHAR2,
    p_CODIGOS_LIBRO       IN  VARCHAR2,
    p_USUARIO_ID          IN  NUMBER,
    p_CODIGO_EMPRESA      IN  NUMBER,
    p_CANTIDAD_OUT        OUT NUMBER,
    p_Message             OUT VARCHAR2
) AS
    v_CODIGO_PERIODO      CNT.CNT_CONCILIACIONES.CODIGO_PERIODO%TYPE;
    v_CODIGO_CUENTA_BANCO CNT.CNT_CONCILIACIONES.CODIGO_CUENTA_BANCO%TYPE;
    v_FECHA_CIERRE        CNT.CNT_CONCILIACIONES.FECHA_CIERRE%TYPE;
    v_COUNT               NUMBER;
    v_CANTIDAD            NUMBER := 0;
    v_GROUP_ID            VARCHAR2(100);
    v_CODIGO_TMP          NUMBER;
BEGIN
    SAVEPOINT SP_CNT_CONC_MATCH_MULTI_START;
    p_CANTIDAD_OUT := 0;

    IF p_CODIGO_CONCILIACION IS NULL OR p_CODIGO_CONCILIACION <= 0 THEN
        p_Message := 'CodigoConciliacion es requerido.';
        RETURN;
    END IF;

    IF (p_CODIGOS_EDO_CTA IS NULL OR TRIM(p_CODIGOS_EDO_CTA) IS NULL)
       AND (p_CODIGOS_LIBRO IS NULL OR TRIM(p_CODIGOS_LIBRO) IS NULL) THEN
        p_Message := 'Debe seleccionar movimientos de banco o libro.';
        RETURN;
    END IF;

    BEGIN
        SELECT c.CODIGO_PERIODO, c.CODIGO_CUENTA_BANCO, c.FECHA_CIERRE
          INTO v_CODIGO_PERIODO, v_CODIGO_CUENTA_BANCO, v_FECHA_CIERRE
          FROM CNT.CNT_CONCILIACIONES c
         WHERE c.CODIGO_CONCILIACION = p_CODIGO_CONCILIACION
           AND c.CODIGO_EMPRESA = p_CODIGO_EMPRESA;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            p_Message := 'Conciliacion no encontrada.';
            RETURN;
    END;

    IF v_FECHA_CIERRE IS NOT NULL THEN
        p_Message := 'La conciliacion esta cerrada.';
        RETURN;
    END IF;

    v_GROUP_ID := 'MATCH_MULTI_' || TO_CHAR(SYSTIMESTAMP, 'YYYYMMDDHH24MISSFF3');

    FOR banco IN (
        SELECT DISTINCT TO_NUMBER(TRIM(REGEXP_SUBSTR(p_CODIGOS_EDO_CTA, '[^,]+', 1, LEVEL))) AS CODIGO_DETALLE_EDO_CTA
          FROM DUAL
        CONNECT BY REGEXP_SUBSTR(p_CODIGOS_EDO_CTA, '[^,]+', 1, LEVEL) IS NOT NULL
    ) LOOP
        SELECT COUNT(1)
          INTO v_COUNT
          FROM CNT.CNT_TMP_CONCILIACION tmp
         WHERE tmp.CODIGO_CONCILIACION = p_CODIGO_CONCILIACION
           AND tmp.CODIGO_DETALLE_EDO_CTA = banco.CODIGO_DETALLE_EDO_CTA
           AND tmp.CODIGO_EMPRESA = p_CODIGO_EMPRESA;

        IF v_COUNT > 0 THEN
            p_Message := 'Un movimiento de banco ya esta en conciliacion temporal.';
            ROLLBACK TO SP_CNT_CONC_MATCH_MULTI_START;
            RETURN;
        END IF;

        SELECT COUNT(1)
          INTO v_COUNT
          FROM CNT.CNT_CONCILIACIONES c
          INNER JOIN CNT.CNT_PERIODOS p
             ON p.CODIGO_PERIODO = c.CODIGO_PERIODO
            AND p.CODIGO_EMPRESA = c.CODIGO_EMPRESA
          INNER JOIN CNT.CNT_ESTADO_CUENTAS e
             ON e.CODIGO_CUENTA_BANCO = c.CODIGO_CUENTA_BANCO
            AND e.CODIGO_EMPRESA = c.CODIGO_EMPRESA
          INNER JOIN CNT.CNT_DETALLE_EDO_CTA d
             ON d.CODIGO_ESTADO_CUENTA = e.CODIGO_ESTADO_CUENTA
            AND d.CODIGO_EMPRESA = c.CODIGO_EMPRESA
         WHERE c.CODIGO_CONCILIACION = p_CODIGO_CONCILIACION
           AND c.CODIGO_EMPRESA = p_CODIGO_EMPRESA
           AND d.CODIGO_DETALLE_EDO_CTA = banco.CODIGO_DETALLE_EDO_CTA
           AND d.FECHA_TRANSACCION BETWEEN p.FECHA_DESDE AND p.FECHA_HASTA;

        IF v_COUNT = 0 THEN
            p_Message := 'Un movimiento de banco no pertenece a la conciliacion.';
            ROLLBACK TO SP_CNT_CONC_MATCH_MULTI_START;
            RETURN;
        END IF;
    END LOOP;

    FOR libro IN (
        SELECT DISTINCT TO_NUMBER(TRIM(REGEXP_SUBSTR(p_CODIGOS_LIBRO, '[^,]+', 1, LEVEL))) AS CODIGO_DETALLE_LIBRO
          FROM DUAL
        CONNECT BY REGEXP_SUBSTR(p_CODIGOS_LIBRO, '[^,]+', 1, LEVEL) IS NOT NULL
    ) LOOP
        SELECT COUNT(1)
          INTO v_COUNT
          FROM CNT.CNT_TMP_CONCILIACION tmp
         WHERE tmp.CODIGO_CONCILIACION = p_CODIGO_CONCILIACION
           AND tmp.CODIGO_DETALLE_LIBRO = libro.CODIGO_DETALLE_LIBRO
           AND tmp.CODIGO_EMPRESA = p_CODIGO_EMPRESA;

        IF v_COUNT > 0 THEN
            p_Message := 'Un movimiento de libro ya esta en conciliacion temporal.';
            ROLLBACK TO SP_CNT_CONC_MATCH_MULTI_START;
            RETURN;
        END IF;

        SELECT COUNT(1)
          INTO v_COUNT
          FROM CNT.CNT_CONCILIACIONES c
          INNER JOIN CNT.CNT_PERIODOS p
             ON p.CODIGO_PERIODO = c.CODIGO_PERIODO
            AND p.CODIGO_EMPRESA = c.CODIGO_EMPRESA
          INNER JOIN CNT.CNT_LIBROS l
             ON l.CODIGO_CUENTA_BANCO = c.CODIGO_CUENTA_BANCO
            AND l.CODIGO_EMPRESA = c.CODIGO_EMPRESA
          INNER JOIN CNT.CNT_DETALLE_LIBRO d
             ON d.CODIGO_LIBRO = l.CODIGO_LIBRO
            AND d.CODIGO_EMPRESA = c.CODIGO_EMPRESA
         WHERE c.CODIGO_CONCILIACION = p_CODIGO_CONCILIACION
           AND c.CODIGO_EMPRESA = p_CODIGO_EMPRESA
           AND d.CODIGO_DETALLE_LIBRO = libro.CODIGO_DETALLE_LIBRO
           AND l.FECHA_LIBRO BETWEEN p.FECHA_DESDE AND p.FECHA_HASTA;

        IF v_COUNT = 0 THEN
            p_Message := 'Un movimiento de libro no pertenece a la conciliacion.';
            ROLLBACK TO SP_CNT_CONC_MATCH_MULTI_START;
            RETURN;
        END IF;
    END LOOP;

    FOR banco IN (
        SELECT DISTINCT TO_NUMBER(TRIM(REGEXP_SUBSTR(p_CODIGOS_EDO_CTA, '[^,]+', 1, LEVEL))) AS CODIGO_DETALLE_EDO_CTA
          FROM DUAL
        CONNECT BY REGEXP_SUBSTR(p_CODIGOS_EDO_CTA, '[^,]+', 1, LEVEL) IS NOT NULL
    ) LOOP
        SELECT CNT.CNT_S_CODIGO_TMP_CONCILIACION.NEXTVAL INTO v_CODIGO_TMP FROM DUAL;

        INSERT INTO CNT.CNT_TMP_CONCILIACION (
            CODIGO_TMP_CONCILIACION, CODIGO_CONCILIACION, CODIGO_PERIODO, CODIGO_CUENTA_BANCO,
            CODIGO_DETALLE_EDO_CTA, FECHA, NUMERO, MONTO, EXTRA1, EXTRA2,
            USUARIO_INS, FECHA_INS, CODIGO_EMPRESA
        )
        SELECT
            v_CODIGO_TMP, p_CODIGO_CONCILIACION, v_CODIGO_PERIODO, v_CODIGO_CUENTA_BANCO,
            d.CODIGO_DETALLE_EDO_CTA, d.FECHA_TRANSACCION, SUBSTR(NVL(d.NUMERO_TRANSACCION, TO_CHAR(v_CODIGO_TMP)), 1, 20),
            NVL(d.MONTO, 0), 'MATCH_MANUAL_MULTI', v_GROUP_ID,
            p_USUARIO_ID, SYSDATE, p_CODIGO_EMPRESA
          FROM CNT.CNT_DETALLE_EDO_CTA d
         WHERE d.CODIGO_DETALLE_EDO_CTA = banco.CODIGO_DETALLE_EDO_CTA
           AND d.CODIGO_EMPRESA = p_CODIGO_EMPRESA;

        v_CANTIDAD := v_CANTIDAD + SQL%ROWCOUNT;
    END LOOP;

    FOR libro IN (
        SELECT DISTINCT TO_NUMBER(TRIM(REGEXP_SUBSTR(p_CODIGOS_LIBRO, '[^,]+', 1, LEVEL))) AS CODIGO_DETALLE_LIBRO
          FROM DUAL
        CONNECT BY REGEXP_SUBSTR(p_CODIGOS_LIBRO, '[^,]+', 1, LEVEL) IS NOT NULL
    ) LOOP
        SELECT CNT.CNT_S_CODIGO_TMP_CONCILIACION.NEXTVAL INTO v_CODIGO_TMP FROM DUAL;

        INSERT INTO CNT.CNT_TMP_CONCILIACION (
            CODIGO_TMP_CONCILIACION, CODIGO_CONCILIACION, CODIGO_PERIODO, CODIGO_CUENTA_BANCO,
            CODIGO_DETALLE_LIBRO, FECHA, NUMERO, MONTO, EXTRA1, EXTRA2,
            USUARIO_INS, FECHA_INS, CODIGO_EMPRESA
        )
        SELECT
            v_CODIGO_TMP, p_CODIGO_CONCILIACION, v_CODIGO_PERIODO, v_CODIGO_CUENTA_BANCO,
            d.CODIGO_DETALLE_LIBRO, l.FECHA_LIBRO, SUBSTR(NVL(d.NUMERO_DOCUMENTO, TO_CHAR(v_CODIGO_TMP)), 1, 20),
            NVL(d.MONTO, 0), 'MATCH_MANUAL_MULTI', v_GROUP_ID,
            p_USUARIO_ID, SYSDATE, p_CODIGO_EMPRESA
          FROM CNT.CNT_DETALLE_LIBRO d
          INNER JOIN CNT.CNT_LIBROS l
             ON l.CODIGO_LIBRO = d.CODIGO_LIBRO
            AND l.CODIGO_EMPRESA = d.CODIGO_EMPRESA
         WHERE d.CODIGO_DETALLE_LIBRO = libro.CODIGO_DETALLE_LIBRO
           AND d.CODIGO_EMPRESA = p_CODIGO_EMPRESA;

        v_CANTIDAD := v_CANTIDAD + SQL%ROWCOUNT;
    END LOOP;

    p_CANTIDAD_OUT := v_CANTIDAD;
    p_Message := 'OK';
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK TO SP_CNT_CONC_MATCH_MULTI_START;
        p_CANTIDAD_OUT := 0;
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_CONC_GET_ID (
    p_CODIGO_CONCILIACION IN  NUMBER,
    p_CODIGO_EMPRESA      IN  NUMBER,
    p_ResultSet           OUT SYS_REFCURSOR,
    p_Message             OUT VARCHAR2
) AS
BEGIN
    OPEN p_ResultSet FOR
        SELECT
            v.CODIGO_CONCILIACION,
            v.CODIGO_PERIODO,
            NVL(v.NOMBRE_PERIODO, '') AS NOMBRE_PERIODO,
            v.ANO_PERIODO,
            v.NUMERO_PERIODO,
            v.CODIGO_CUENTA_BANCO,
            v.CODIGO_BANCO,
            NVL(v.NOMBRE_BANCO, '') AS BANCO,
            NVL(v.NUMERO_CUENTA_BANCO, '') AS NO_CUENTA,
            NVL(v.DENOMINACION_FUNCIONAL, '') AS DENOMINACION_FUNCIONAL,
            NVL(v.SALDO_BANCO, 0) AS SALDO_BANCO,
            NVL(v.SALDO_LIBRO, 0) AS SALDO_LIBRO,
            v.FECHA_PRECIERRE,
            v.FECHA_CIERRE,
            CASE
                WHEN v.FECHA_CIERRE IS NOT NULL THEN 'CERRADA'
                WHEN v.FECHA_PRECIERRE IS NOT NULL THEN 'PRECIERRE'
                ELSE 'ABIERTA'
            END AS ESTADO,
            v.CODIGO_EMPRESA
        FROM CNT.CNT_V_CONCILIACIONES v
        WHERE v.CODIGO_EMPRESA = p_CODIGO_EMPRESA
          AND v.CODIGO_CONCILIACION = p_CODIGO_CONCILIACION;

    p_Message := 'OK';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_CONC_INS (
    p_CODIGO_PERIODO      IN  NUMBER,
    p_CODIGO_CUENTA_BANCO IN  NUMBER,
    p_USUARIO_ID          IN  NUMBER,
    p_CODIGO_EMPRESA      IN  NUMBER,
    p_CODIGO_OUT          OUT NUMBER,
    p_Message             OUT VARCHAR2
) AS
    v_COUNT NUMBER;
BEGIN
    p_CODIGO_OUT := NULL;

    IF p_CODIGO_PERIODO IS NULL OR p_CODIGO_PERIODO <= 0 THEN
        p_Message := 'CodigoPeriodo es requerido.';
        RETURN;
    END IF;

    IF p_CODIGO_CUENTA_BANCO IS NULL OR p_CODIGO_CUENTA_BANCO <= 0 THEN
        p_Message := 'CodigoCuentaBanco es requerido.';
        RETURN;
    END IF;

    SELECT COUNT(1)
      INTO v_COUNT
      FROM CNT.CNT_PERIODOS p
     WHERE p.CODIGO_PERIODO = p_CODIGO_PERIODO
       AND p.CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    IF v_COUNT = 0 THEN
        p_Message := 'Periodo no encontrado.';
        RETURN;
    END IF;

    SELECT COUNT(1)
      INTO v_COUNT
      FROM SIS.SIS_CUENTAS_BANCOS cb
     WHERE cb.CODIGO_CUENTA_BANCO = p_CODIGO_CUENTA_BANCO
       AND cb.CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    IF v_COUNT = 0 THEN
        p_Message := 'Cuenta bancaria no encontrada.';
        RETURN;
    END IF;

    BEGIN
        SELECT c.CODIGO_CONCILIACION
          INTO p_CODIGO_OUT
          FROM CNT.CNT_CONCILIACIONES c
         WHERE c.CODIGO_PERIODO = p_CODIGO_PERIODO
           AND c.CODIGO_CUENTA_BANCO = p_CODIGO_CUENTA_BANCO
           AND c.CODIGO_EMPRESA = p_CODIGO_EMPRESA;

        p_Message := 'OK';
        RETURN;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            NULL;
    END;

    SELECT CNT.CNT_S_CODIGO_CONCILIACION.NEXTVAL
      INTO p_CODIGO_OUT
      FROM DUAL;

    INSERT INTO CNT.CNT_CONCILIACIONES (
        CODIGO_CONCILIACION,
        CODIGO_PERIODO,
        CODIGO_CUENTA_BANCO,
        SALDO_BANCO,
        SALDO_LIBRO,
        USUARIO_INS,
        FECHA_INS,
        CODIGO_EMPRESA
    ) VALUES (
        p_CODIGO_OUT,
        p_CODIGO_PERIODO,
        p_CODIGO_CUENTA_BANCO,
        0,
        0,
        p_USUARIO_ID,
        SYSDATE,
        p_CODIGO_EMPRESA
    );

    p_Message := 'OK';
EXCEPTION
    WHEN DUP_VAL_ON_INDEX THEN
        SELECT c.CODIGO_CONCILIACION
          INTO p_CODIGO_OUT
          FROM CNT.CNT_CONCILIACIONES c
         WHERE c.CODIGO_PERIODO = p_CODIGO_PERIODO
           AND c.CODIGO_CUENTA_BANCO = p_CODIGO_CUENTA_BANCO
           AND c.CODIGO_EMPRESA = p_CODIGO_EMPRESA;
        p_Message := 'OK';
    WHEN OTHERS THEN
        p_CODIGO_OUT := NULL;
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_CONC_PRE (
    p_CODIGO_CONCILIACION IN  NUMBER,
    p_USUARIO_ID          IN  NUMBER,
    p_CODIGO_EMPRESA      IN  NUMBER,
    p_Message             OUT VARCHAR2
) AS
    v_FECHA_CIERRE CNT.CNT_CONCILIACIONES.FECHA_CIERRE%TYPE;
    v_SALDO_BANCO  NUMBER;
    v_SALDO_LIBRO  NUMBER;
BEGIN
    IF p_CODIGO_CONCILIACION IS NULL OR p_CODIGO_CONCILIACION <= 0 THEN
        p_Message := 'CodigoConciliacion es requerido.';
        RETURN;
    END IF;

    BEGIN
        SELECT c.FECHA_CIERRE
          INTO v_FECHA_CIERRE
          FROM CNT.CNT_CONCILIACIONES c
         WHERE c.CODIGO_CONCILIACION = p_CODIGO_CONCILIACION
           AND c.CODIGO_EMPRESA = p_CODIGO_EMPRESA;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            p_Message := 'Conciliacion no encontrada.';
            RETURN;
    END;

    IF v_FECHA_CIERRE IS NOT NULL THEN
        p_Message := 'La conciliacion esta cerrada.';
        RETURN;
    END IF;

    SELECT NVL(SUM(CASE WHEN tmp.CODIGO_DETALLE_EDO_CTA IS NOT NULL THEN tmp.MONTO ELSE 0 END), 0),
           NVL(SUM(CASE WHEN tmp.CODIGO_DETALLE_LIBRO IS NOT NULL THEN tmp.MONTO ELSE 0 END), 0)
      INTO v_SALDO_BANCO, v_SALDO_LIBRO
      FROM CNT.CNT_TMP_CONCILIACION tmp
     WHERE tmp.CODIGO_CONCILIACION = p_CODIGO_CONCILIACION
       AND tmp.CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    UPDATE CNT.CNT_CONCILIACIONES
       SET USUARIO_PRECIERRE = p_USUARIO_ID,
           FECHA_PRECIERRE = SYSDATE,
           SALDO_BANCO = v_SALDO_BANCO,
           SALDO_LIBRO = v_SALDO_LIBRO,
           USUARIO_UPD = p_USUARIO_ID,
           FECHA_UPD = SYSDATE
     WHERE CODIGO_CONCILIACION = p_CODIGO_CONCILIACION
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    p_Message := 'OK';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_CONC_CLOSE (
    p_CODIGO_CONCILIACION IN  NUMBER,
    p_USUARIO_ID          IN  NUMBER,
    p_FORZAR_DIFERENCIA   IN  NUMBER,
    p_CODIGO_EMPRESA      IN  NUMBER,
    p_Message             OUT VARCHAR2
) AS
    v_FECHA_CIERRE CNT.CNT_CONCILIACIONES.FECHA_CIERRE%TYPE;
    v_SALDO_BANCO  NUMBER;
    v_SALDO_LIBRO  NUMBER;
BEGIN
    IF p_CODIGO_CONCILIACION IS NULL OR p_CODIGO_CONCILIACION <= 0 THEN
        p_Message := 'CodigoConciliacion es requerido.';
        RETURN;
    END IF;

    BEGIN
        SELECT c.FECHA_CIERRE
          INTO v_FECHA_CIERRE
          FROM CNT.CNT_CONCILIACIONES c
         WHERE c.CODIGO_CONCILIACION = p_CODIGO_CONCILIACION
           AND c.CODIGO_EMPRESA = p_CODIGO_EMPRESA;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            p_Message := 'Conciliacion no encontrada.';
            RETURN;
    END;

    IF v_FECHA_CIERRE IS NOT NULL THEN
        p_Message := 'La conciliacion ya esta cerrada.';
        RETURN;
    END IF;

    SAVEPOINT SP_CNT_CONC_CLOSE_START;

    SELECT NVL(SUM(CASE WHEN tmp.CODIGO_DETALLE_EDO_CTA IS NOT NULL THEN tmp.MONTO ELSE 0 END), 0),
           NVL(SUM(CASE WHEN tmp.CODIGO_DETALLE_LIBRO IS NOT NULL THEN tmp.MONTO ELSE 0 END), 0)
      INTO v_SALDO_BANCO, v_SALDO_LIBRO
      FROM CNT.CNT_TMP_CONCILIACION tmp
    WHERE tmp.CODIGO_CONCILIACION = p_CODIGO_CONCILIACION
       AND tmp.CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    IF ABS(NVL(v_SALDO_BANCO, 0) - NVL(v_SALDO_LIBRO, 0)) > 0.01
       AND NVL(p_FORZAR_DIFERENCIA, 0) = 0 THEN
        p_Message := 'La conciliacion tiene diferencia. Use cierre forzado si tiene permiso.';
        RETURN;
    END IF;

    INSERT INTO CNT.CNT_HIST_CONCILIACION (
        CODIGO_HIST_CONCILIACION,
        CODIGO_CONCILIACION,
        CODIGO_PERIODO,
        CODIGO_CUENTA_BANCO,
        CODIGO_DETALLE_LIBRO,
        CODIGO_DETALLE_EDO_CTA,
        FECHA,
        NUMERO,
        MONTO,
        EXTRA1,
        EXTRA2,
        EXTRA3,
        USUARIO_INS,
        FECHA_INS,
        CODIGO_EMPRESA
    )
    SELECT CNT.CNT_S_CODIGO_HIST_CONCILIACION.NEXTVAL,
           tmp.CODIGO_CONCILIACION,
           tmp.CODIGO_PERIODO,
           tmp.CODIGO_CUENTA_BANCO,
           tmp.CODIGO_DETALLE_LIBRO,
           tmp.CODIGO_DETALLE_EDO_CTA,
           tmp.FECHA,
           tmp.NUMERO,
           tmp.MONTO,
           tmp.EXTRA1,
           tmp.EXTRA2,
           tmp.EXTRA3,
           p_USUARIO_ID,
           SYSDATE,
           tmp.CODIGO_EMPRESA
      FROM CNT.CNT_TMP_CONCILIACION tmp
     WHERE tmp.CODIGO_CONCILIACION = p_CODIGO_CONCILIACION
       AND tmp.CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    UPDATE CNT.CNT_DETALLE_EDO_CTA d
       SET d.STATUS = 'C',
           d.USUARIO_UPD = p_USUARIO_ID,
           d.FECHA_UPD = SYSDATE
     WHERE d.CODIGO_EMPRESA = p_CODIGO_EMPRESA
       AND EXISTS (
             SELECT 1
               FROM CNT.CNT_TMP_CONCILIACION tmp
              WHERE tmp.CODIGO_CONCILIACION = p_CODIGO_CONCILIACION
                AND tmp.CODIGO_DETALLE_EDO_CTA = d.CODIGO_DETALLE_EDO_CTA
                AND tmp.CODIGO_EMPRESA = d.CODIGO_EMPRESA
       );

    UPDATE CNT.CNT_DETALLE_LIBRO d
       SET d.STATUS = 'C',
           d.USUARIO_UPD = p_USUARIO_ID,
           d.FECHA_UPD = SYSDATE
     WHERE d.CODIGO_EMPRESA = p_CODIGO_EMPRESA
       AND EXISTS (
             SELECT 1
               FROM CNT.CNT_TMP_CONCILIACION tmp
              WHERE tmp.CODIGO_CONCILIACION = p_CODIGO_CONCILIACION
                AND tmp.CODIGO_DETALLE_LIBRO = d.CODIGO_DETALLE_LIBRO
                AND tmp.CODIGO_EMPRESA = d.CODIGO_EMPRESA
       );

    UPDATE CNT.CNT_LIBROS l
       SET l.STATUS = 'C',
           l.USUARIO_UPD = p_USUARIO_ID,
           l.FECHA_UPD = SYSDATE
     WHERE l.CODIGO_EMPRESA = p_CODIGO_EMPRESA
       AND EXISTS (
             SELECT 1
               FROM CNT.CNT_TMP_CONCILIACION tmp
               INNER JOIN CNT.CNT_DETALLE_LIBRO d
                  ON d.CODIGO_DETALLE_LIBRO = tmp.CODIGO_DETALLE_LIBRO
                 AND d.CODIGO_EMPRESA = tmp.CODIGO_EMPRESA
              WHERE tmp.CODIGO_CONCILIACION = p_CODIGO_CONCILIACION
                AND d.CODIGO_LIBRO = l.CODIGO_LIBRO
                AND tmp.CODIGO_EMPRESA = l.CODIGO_EMPRESA
       );

    DELETE FROM CNT.CNT_TMP_CONCILIACION
     WHERE CODIGO_CONCILIACION = p_CODIGO_CONCILIACION
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    UPDATE CNT.CNT_CONCILIACIONES
       SET USUARIO_CIERRE = p_USUARIO_ID,
           FECHA_CIERRE = SYSDATE,
           SALDO_BANCO = v_SALDO_BANCO,
           SALDO_LIBRO = v_SALDO_LIBRO,
           USUARIO_UPD = p_USUARIO_ID,
           FECHA_UPD = SYSDATE
     WHERE CODIGO_CONCILIACION = p_CODIGO_CONCILIACION
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    p_Message := 'OK';
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK TO SP_CNT_CONC_CLOSE_START;
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_CONC_REV (
    p_CODIGO_CONCILIACION IN  NUMBER,
    p_USUARIO_ID          IN  NUMBER,
    p_CODIGO_EMPRESA      IN  NUMBER,
    p_Message             OUT VARCHAR2
) AS
    v_FECHA_CIERRE CNT.CNT_CONCILIACIONES.FECHA_CIERRE%TYPE;
    v_COUNT        NUMBER;
    v_SALDO_BANCO  NUMBER;
    v_SALDO_LIBRO  NUMBER;
BEGIN
    IF p_CODIGO_CONCILIACION IS NULL OR p_CODIGO_CONCILIACION <= 0 THEN
        p_Message := 'CodigoConciliacion es requerido.';
        RETURN;
    END IF;

    BEGIN
        SELECT c.FECHA_CIERRE
          INTO v_FECHA_CIERRE
          FROM CNT.CNT_CONCILIACIONES c
         WHERE c.CODIGO_CONCILIACION = p_CODIGO_CONCILIACION
           AND c.CODIGO_EMPRESA = p_CODIGO_EMPRESA;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            p_Message := 'Conciliacion no encontrada.';
            RETURN;
    END;

    IF v_FECHA_CIERRE IS NULL THEN
        p_Message := 'La conciliacion no esta cerrada.';
        RETURN;
    END IF;

    SELECT COUNT(1)
      INTO v_COUNT
      FROM CNT.CNT_TMP_CONCILIACION tmp
     WHERE tmp.CODIGO_CONCILIACION = p_CODIGO_CONCILIACION
       AND tmp.CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    IF v_COUNT > 0 THEN
        p_Message := 'La conciliacion tiene temporales activos. No se puede reversar.';
        RETURN;
    END IF;

    SELECT COUNT(1)
      INTO v_COUNT
      FROM CNT.CNT_HIST_CONCILIACION hist
     WHERE hist.CODIGO_CONCILIACION = p_CODIGO_CONCILIACION
       AND hist.CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    IF v_COUNT = 0 THEN
        p_Message := 'La conciliacion no tiene historico para reversar.';
        RETURN;
    END IF;

    SAVEPOINT SP_CNT_CONC_REV_START;

    INSERT INTO CNT.CNT_REVERSO_CONCILIACION (
        CODIGO_HIST_CONCILIACION,
        CODIGO_CONCILIACION,
        CODIGO_PERIODO,
        CODIGO_CUENTA_BANCO,
        CODIGO_DETALLE_LIBRO,
        CODIGO_DETALLE_EDO_CTA,
        FECHA,
        NUMERO,
        MONTO,
        EXTRA1,
        EXTRA2,
        EXTRA3,
        USUARIO_INS,
        FECHA_INS,
        CODIGO_EMPRESA
    )
    SELECT hist.CODIGO_HIST_CONCILIACION,
           hist.CODIGO_CONCILIACION,
           hist.CODIGO_PERIODO,
           hist.CODIGO_CUENTA_BANCO,
           hist.CODIGO_DETALLE_LIBRO,
           hist.CODIGO_DETALLE_EDO_CTA,
           hist.FECHA,
           hist.NUMERO,
           hist.MONTO,
           hist.EXTRA1,
           hist.EXTRA2,
           hist.EXTRA3,
           p_USUARIO_ID,
           SYSDATE,
           hist.CODIGO_EMPRESA
      FROM CNT.CNT_HIST_CONCILIACION hist
     WHERE hist.CODIGO_CONCILIACION = p_CODIGO_CONCILIACION
       AND hist.CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    INSERT INTO CNT.CNT_TMP_CONCILIACION (
        CODIGO_TMP_CONCILIACION,
        CODIGO_CONCILIACION,
        CODIGO_PERIODO,
        CODIGO_CUENTA_BANCO,
        CODIGO_DETALLE_LIBRO,
        CODIGO_DETALLE_EDO_CTA,
        FECHA,
        NUMERO,
        MONTO,
        EXTRA1,
        EXTRA2,
        EXTRA3,
        USUARIO_INS,
        FECHA_INS,
        CODIGO_EMPRESA
    )
    SELECT CNT.CNT_S_CODIGO_TMP_CONCILIACION.NEXTVAL,
           hist.CODIGO_CONCILIACION,
           hist.CODIGO_PERIODO,
           hist.CODIGO_CUENTA_BANCO,
           hist.CODIGO_DETALLE_LIBRO,
           hist.CODIGO_DETALLE_EDO_CTA,
           hist.FECHA,
           hist.NUMERO,
           hist.MONTO,
           'REVERSO_CIERRE',
           hist.EXTRA2,
           hist.EXTRA3,
           p_USUARIO_ID,
           SYSDATE,
           hist.CODIGO_EMPRESA
      FROM CNT.CNT_HIST_CONCILIACION hist
     WHERE hist.CODIGO_CONCILIACION = p_CODIGO_CONCILIACION
       AND hist.CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    UPDATE CNT.CNT_DETALLE_EDO_CTA d
       SET d.STATUS = 'T',
           d.USUARIO_UPD = p_USUARIO_ID,
           d.FECHA_UPD = SYSDATE
     WHERE d.CODIGO_EMPRESA = p_CODIGO_EMPRESA
       AND EXISTS (
             SELECT 1
               FROM CNT.CNT_HIST_CONCILIACION hist
              WHERE hist.CODIGO_CONCILIACION = p_CODIGO_CONCILIACION
                AND hist.CODIGO_DETALLE_EDO_CTA = d.CODIGO_DETALLE_EDO_CTA
                AND hist.CODIGO_EMPRESA = d.CODIGO_EMPRESA
       );

    UPDATE CNT.CNT_DETALLE_LIBRO d
       SET d.STATUS = 'T',
           d.USUARIO_UPD = p_USUARIO_ID,
           d.FECHA_UPD = SYSDATE
     WHERE d.CODIGO_EMPRESA = p_CODIGO_EMPRESA
       AND EXISTS (
             SELECT 1
               FROM CNT.CNT_HIST_CONCILIACION hist
              WHERE hist.CODIGO_CONCILIACION = p_CODIGO_CONCILIACION
                AND hist.CODIGO_DETALLE_LIBRO = d.CODIGO_DETALLE_LIBRO
                AND hist.CODIGO_EMPRESA = d.CODIGO_EMPRESA
       );

    UPDATE CNT.CNT_LIBROS l
       SET l.STATUS = 'T',
           l.USUARIO_UPD = p_USUARIO_ID,
           l.FECHA_UPD = SYSDATE
     WHERE l.CODIGO_EMPRESA = p_CODIGO_EMPRESA
       AND EXISTS (
             SELECT 1
               FROM CNT.CNT_HIST_CONCILIACION hist
               INNER JOIN CNT.CNT_DETALLE_LIBRO d
                  ON d.CODIGO_DETALLE_LIBRO = hist.CODIGO_DETALLE_LIBRO
                 AND d.CODIGO_EMPRESA = hist.CODIGO_EMPRESA
              WHERE hist.CODIGO_CONCILIACION = p_CODIGO_CONCILIACION
                AND d.CODIGO_LIBRO = l.CODIGO_LIBRO
                AND hist.CODIGO_EMPRESA = l.CODIGO_EMPRESA
       );

    SELECT NVL(SUM(CASE WHEN hist.CODIGO_DETALLE_EDO_CTA IS NOT NULL THEN hist.MONTO ELSE 0 END), 0),
           NVL(SUM(CASE WHEN hist.CODIGO_DETALLE_LIBRO IS NOT NULL THEN hist.MONTO ELSE 0 END), 0)
      INTO v_SALDO_BANCO, v_SALDO_LIBRO
      FROM CNT.CNT_HIST_CONCILIACION hist
     WHERE hist.CODIGO_CONCILIACION = p_CODIGO_CONCILIACION
       AND hist.CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    DELETE FROM CNT.CNT_HIST_CONCILIACION
     WHERE CODIGO_CONCILIACION = p_CODIGO_CONCILIACION
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    UPDATE CNT.CNT_CONCILIACIONES
       SET USUARIO_CIERRE = NULL,
           FECHA_CIERRE = NULL,
           SALDO_BANCO = v_SALDO_BANCO,
           SALDO_LIBRO = v_SALDO_LIBRO,
           USUARIO_UPD = p_USUARIO_ID,
           FECHA_UPD = SYSDATE
     WHERE CODIGO_CONCILIACION = p_CODIGO_CONCILIACION
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    p_Message := 'OK';
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK TO SP_CNT_CONC_REV_START;
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_CTAS_BCO_GET (
    p_CODIGO_BANCO       IN  NUMBER,
    p_SOLO_CONFIGURADAS  IN  NUMBER,
    p_SEARCH_TEXT        IN  VARCHAR2,
    p_CODIGO_EMPRESA     IN  NUMBER,
    p_ResultSet          OUT SYS_REFCURSOR,
    p_Message            OUT VARCHAR2
) AS
BEGIN
    OPEN p_ResultSet FOR
        SELECT
            c.CODIGO_CUENTA_BANCO,
            c.CODIGO_BANCO,
            NVL(b.NOMBRE, '') AS BANCO,
            c.TIPO_CUENTA_ID,
            NVL(c.NO_CUENTA, '') AS NO_CUENTA,
            NVL(c.FORMATO_MASCARA, '') AS FORMATO_MASCARA,
            c.DENOMINACION_FUNCIONAL_ID,
            NVL(d.DESCRIPCION, '') AS DENOMINACION_FUNCIONAL,
            NVL(c.CODIGO, '') AS CODIGO,
            NVL(c.PRINCIPAL, 0) AS PRINCIPAL,
            NVL(c.RECAUDADORA, 0) AS RECAUDADORA,
            c.CODIGO_MAYOR,
            NVL(m.DENOMINACION, '') AS MAYOR,
            c.CODIGO_AUXILIAR,
            NVL(a.DENOMINACION, '') AS AUXILIAR,
            NVL(c.SEARCH_TEXT, '') AS SEARCH_TEXT,
            c.CODIGO_EMPRESA
        FROM SIS.SIS_CUENTAS_BANCOS c
        INNER JOIN SIS.SIS_BANCOS b
            ON b.CODIGO_BANCO = c.CODIGO_BANCO
           AND b.CODIGO_EMPRESA = c.CODIGO_EMPRESA
        LEFT JOIN SIS.SIS_DESCRIPTIVAS d
            ON d.DESCRIPCION_ID = c.DENOMINACION_FUNCIONAL_ID
        LEFT JOIN CNT.CNT_MAYORES m
            ON m.CODIGO_MAYOR = c.CODIGO_MAYOR
           AND m.CODIGO_EMPRESA = c.CODIGO_EMPRESA
        LEFT JOIN CNT.CNT_AUXILIARES a
            ON a.CODIGO_AUXILIAR = c.CODIGO_AUXILIAR
           AND a.CODIGO_EMPRESA = c.CODIGO_EMPRESA
        WHERE c.CODIGO_EMPRESA = p_CODIGO_EMPRESA
          AND (p_CODIGO_BANCO IS NULL OR c.CODIGO_BANCO = p_CODIGO_BANCO)
          AND (NVL(p_SOLO_CONFIGURADAS, 0) = 0 OR (c.CODIGO_MAYOR IS NOT NULL AND c.CODIGO_AUXILIAR IS NOT NULL))
          AND (
                p_SEARCH_TEXT IS NULL
             OR TRIM(p_SEARCH_TEXT) IS NULL
             OR UPPER(b.NOMBRE) LIKE '%' || UPPER(TRIM(p_SEARCH_TEXT)) || '%'
             OR UPPER(NVL(c.NO_CUENTA, '')) LIKE '%' || UPPER(TRIM(p_SEARCH_TEXT)) || '%'
             OR UPPER(NVL(c.CODIGO, '')) LIKE '%' || UPPER(TRIM(p_SEARCH_TEXT)) || '%'
             OR UPPER(NVL(c.SEARCH_TEXT, '')) LIKE '%' || UPPER(TRIM(p_SEARCH_TEXT)) || '%'
          )
        ORDER BY b.NOMBRE, c.NO_CUENTA;

    p_Message := 'OK';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_BCO_ARC_GET (
    p_CODIGO_BANCO        IN  NUMBER,
    p_CODIGO_CUENTA_BANCO IN  NUMBER,
    p_SEARCH_TEXT         IN  VARCHAR2,
    p_CODIGO_EMPRESA      IN  NUMBER,
    p_ResultSet           OUT SYS_REFCURSOR,
    p_Message             OUT VARCHAR2
) AS
BEGIN
    OPEN p_ResultSet FOR
        SELECT
            ctl.CODIGO_BANCO_ARCHIVO_CONTROL,
            ctl.CODIGO_BANCO,
            NVL(b.NOMBRE, '') AS BANCO,
            ctl.CODIGO_CUENTA_BANCO,
            NVL(cb.NO_CUENTA, '') AS NO_CUENTA,
            NVL(ctl.NOMBRE_ARCHIVO, '') AS NOMBRE_ARCHIVO,
            ctl.FECHA_DESDE,
            ctl.FECHA_HASTA,
            NVL(ctl.SALDO_INICIAL, 0) AS SALDO_INICIAL,
            NVL(ctl.SALDO_FINAL, 0) AS SALDO_FINAL,
            ctl.CODIGO_ESTADO_CUENTA,
            CASE WHEN ctl.CODIGO_ESTADO_CUENTA IS NULL THEN 0 ELSE 1 END AS CONFIRMADO,
            COUNT(det.CODIGO_BANCO_ARCHIVO) AS CANTIDAD_MOVIMIENTOS,
            NVL(SUM(det.MONTO_TRANSACCION), 0) AS MONTO_MOVIMIENTOS,
            ctl.CODIGO_EMPRESA
        FROM CNT.CNT_BANCO_ARCHIVO_CONTROL ctl
        INNER JOIN SIS.SIS_BANCOS b
            ON b.CODIGO_BANCO = ctl.CODIGO_BANCO
           AND b.CODIGO_EMPRESA = ctl.CODIGO_EMPRESA
        INNER JOIN SIS.SIS_CUENTAS_BANCOS cb
            ON cb.CODIGO_CUENTA_BANCO = ctl.CODIGO_CUENTA_BANCO
           AND cb.CODIGO_EMPRESA = ctl.CODIGO_EMPRESA
        LEFT JOIN CNT.CNT_BANCO_ARCHIVO det
            ON det.CODIGO_BANCO_ARCHIVO_CONTROL = ctl.CODIGO_BANCO_ARCHIVO_CONTROL
           AND det.CODIGO_EMPRESA = ctl.CODIGO_EMPRESA
        WHERE ctl.CODIGO_EMPRESA = p_CODIGO_EMPRESA
          AND (p_CODIGO_BANCO IS NULL OR ctl.CODIGO_BANCO = p_CODIGO_BANCO)
          AND (p_CODIGO_CUENTA_BANCO IS NULL OR ctl.CODIGO_CUENTA_BANCO = p_CODIGO_CUENTA_BANCO)
          AND (
                p_SEARCH_TEXT IS NULL
             OR TRIM(p_SEARCH_TEXT) IS NULL
             OR UPPER(NVL(ctl.NOMBRE_ARCHIVO, '')) LIKE '%' || UPPER(TRIM(p_SEARCH_TEXT)) || '%'
             OR UPPER(NVL(cb.NO_CUENTA, '')) LIKE '%' || UPPER(TRIM(p_SEARCH_TEXT)) || '%'
          )
        GROUP BY
            ctl.CODIGO_BANCO_ARCHIVO_CONTROL,
            ctl.CODIGO_BANCO,
            b.NOMBRE,
            ctl.CODIGO_CUENTA_BANCO,
            cb.NO_CUENTA,
            ctl.NOMBRE_ARCHIVO,
            ctl.FECHA_DESDE,
            ctl.FECHA_HASTA,
            ctl.SALDO_INICIAL,
            ctl.SALDO_FINAL,
            ctl.CODIGO_ESTADO_CUENTA,
            ctl.CODIGO_EMPRESA
        ORDER BY ctl.FECHA_DESDE DESC, ctl.CODIGO_BANCO_ARCHIVO_CONTROL DESC;

    p_Message := 'OK';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_BCO_ARC_CTL_INS (
    p_CODIGO_BANCO        IN  NUMBER,
    p_CODIGO_CUENTA_BANCO IN  NUMBER,
    p_NOMBRE_ARCHIVO      IN  VARCHAR2,
    p_FECHA_DESDE         IN  DATE,
    p_FECHA_HASTA         IN  DATE,
    p_SALDO_INICIAL       IN  NUMBER,
    p_SALDO_FINAL         IN  NUMBER,
    p_USUARIO_ID          IN  NUMBER,
    p_CODIGO_EMPRESA      IN  NUMBER,
    p_CODIGO_OUT          OUT NUMBER,
    p_Message             OUT VARCHAR2
) AS
    v_COUNT NUMBER;
BEGIN
    p_CODIGO_OUT := NULL;

    IF p_CODIGO_BANCO IS NULL OR p_CODIGO_CUENTA_BANCO IS NULL THEN
        p_Message := 'Banco y cuenta bancaria son requeridos.';
        RETURN;
    END IF;

    IF TRIM(p_NOMBRE_ARCHIVO) IS NULL THEN
        p_Message := 'NombreArchivo es requerido.';
        RETURN;
    END IF;

    IF p_FECHA_DESDE IS NULL OR p_FECHA_HASTA IS NULL OR p_FECHA_DESDE > p_FECHA_HASTA THEN
        p_Message := 'Rango de fechas invalido.';
        RETURN;
    END IF;

    SELECT COUNT(1)
      INTO v_COUNT
      FROM SIS.SIS_CUENTAS_BANCOS cb
     WHERE cb.CODIGO_CUENTA_BANCO = p_CODIGO_CUENTA_BANCO
       AND cb.CODIGO_BANCO = p_CODIGO_BANCO
       AND cb.CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    IF v_COUNT = 0 THEN
        p_Message := 'La cuenta bancaria no pertenece al banco seleccionado.';
        RETURN;
    END IF;

    SELECT COUNT(1)
      INTO v_COUNT
      FROM CNT.CNT_BANCO_ARCHIVO_CONTROL ctl
     WHERE UPPER(ctl.NOMBRE_ARCHIVO) = UPPER(TRIM(p_NOMBRE_ARCHIVO))
       AND ctl.CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    IF v_COUNT > 0 THEN
        p_Message := 'El archivo ya fue cargado para esta empresa.';
        RETURN;
    END IF;

    SELECT CNT.CNT_S_CODIGO_BANCO_ARCHIVO_CON.NEXTVAL
      INTO p_CODIGO_OUT
      FROM DUAL;

    INSERT INTO CNT.CNT_BANCO_ARCHIVO_CONTROL (
        CODIGO_BANCO_ARCHIVO_CONTROL,
        CODIGO_BANCO,
        CODIGO_CUENTA_BANCO,
        NOMBRE_ARCHIVO,
        FECHA_DESDE,
        FECHA_HASTA,
        SALDO_INICIAL,
        SALDO_FINAL,
        USUARIO_INS,
        FECHA_INS,
        CODIGO_EMPRESA
    ) VALUES (
        p_CODIGO_OUT,
        p_CODIGO_BANCO,
        p_CODIGO_CUENTA_BANCO,
        SUBSTR(TRIM(p_NOMBRE_ARCHIVO), 1, 255),
        p_FECHA_DESDE,
        p_FECHA_HASTA,
        NVL(p_SALDO_INICIAL, 0),
        NVL(p_SALDO_FINAL, 0),
        p_USUARIO_ID,
        SYSDATE,
        p_CODIGO_EMPRESA
    );

    p_Message := 'OK';
EXCEPTION
    WHEN DUP_VAL_ON_INDEX THEN
        p_CODIGO_OUT := NULL;
        p_Message := 'Ya existe una carga bancaria para esa cuenta, archivo o rango.';
    WHEN OTHERS THEN
        p_CODIGO_OUT := NULL;
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_BCO_ARC_DET_INS (
    p_CODIGO_BANCO_ARCHIVO_CONTROL IN  NUMBER,
    p_FECHA_TRANSACCION            IN  DATE,
    p_NUMERO_TRANSACCION           IN  VARCHAR2,
    p_TIPO_TRANSACCION_ID          IN  NUMBER,
    p_TIPO_TRANSACCION             IN  VARCHAR2,
    p_DESCRIPCION_TRANSACCION      IN  VARCHAR2,
    p_MONTO_TRANSACCION            IN  NUMBER,
    p_USUARIO_ID                   IN  NUMBER,
    p_CODIGO_EMPRESA               IN  NUMBER,
    p_CODIGO_OUT                   OUT NUMBER,
    p_Message                      OUT VARCHAR2
) AS
    v_NUMERO_BANCO  VARCHAR2(4);
    v_NUMERO_CUENTA VARCHAR2(20);
    v_FECHA_DESDE   DATE;
    v_FECHA_HASTA   DATE;
    v_CONFIRMADO    NUMBER;
BEGIN
    p_CODIGO_OUT := NULL;

    IF p_CODIGO_BANCO_ARCHIVO_CONTROL IS NULL OR p_CODIGO_BANCO_ARCHIVO_CONTROL <= 0 THEN
        p_Message := 'CodigoBancoArchivoControl es requerido.';
        RETURN;
    END IF;

    IF p_FECHA_TRANSACCION IS NULL OR TRIM(p_NUMERO_TRANSACCION) IS NULL OR p_TIPO_TRANSACCION_ID IS NULL THEN
        p_Message := 'Fecha, numero y tipo de transaccion son requeridos.';
        RETURN;
    END IF;

    BEGIN
        SELECT SUBSTR(NVL(b.CODIGO_INTERBANCARIO, TO_CHAR(ctl.CODIGO_BANCO)), 1, 4),
               SUBSTR(NVL(cb.NO_CUENTA, ''), 1, 20),
               ctl.FECHA_DESDE,
               ctl.FECHA_HASTA,
               CASE WHEN ctl.CODIGO_ESTADO_CUENTA IS NULL THEN 0 ELSE 1 END
          INTO v_NUMERO_BANCO, v_NUMERO_CUENTA, v_FECHA_DESDE, v_FECHA_HASTA, v_CONFIRMADO
          FROM CNT.CNT_BANCO_ARCHIVO_CONTROL ctl
          INNER JOIN SIS.SIS_BANCOS b
             ON b.CODIGO_BANCO = ctl.CODIGO_BANCO
            AND b.CODIGO_EMPRESA = ctl.CODIGO_EMPRESA
          INNER JOIN SIS.SIS_CUENTAS_BANCOS cb
             ON cb.CODIGO_CUENTA_BANCO = ctl.CODIGO_CUENTA_BANCO
            AND cb.CODIGO_EMPRESA = ctl.CODIGO_EMPRESA
         WHERE ctl.CODIGO_BANCO_ARCHIVO_CONTROL = p_CODIGO_BANCO_ARCHIVO_CONTROL
           AND ctl.CODIGO_EMPRESA = p_CODIGO_EMPRESA;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            p_Message := 'Control de archivo no encontrado.';
            RETURN;
    END;

    IF v_CONFIRMADO = 1 THEN
        p_Message := 'El archivo ya fue confirmado.';
        RETURN;
    END IF;

    IF p_FECHA_TRANSACCION < v_FECHA_DESDE OR p_FECHA_TRANSACCION > v_FECHA_HASTA THEN
        p_Message := 'La fecha del movimiento esta fuera del rango del archivo.';
        RETURN;
    END IF;

    SELECT CNT.CNT_S_CODIGO_BANCO_ARCHIVO.NEXTVAL
      INTO p_CODIGO_OUT
      FROM DUAL;

    INSERT INTO CNT.CNT_BANCO_ARCHIVO (
        CODIGO_BANCO_ARCHIVO,
        CODIGO_BANCO_ARCHIVO_CONTROL,
        NUMERO_BANCO,
        NUMERO_CUENTA,
        FECHA_TRANSACCION,
        NUMERO_TRANSACCION,
        TIPO_TRANSACCION_ID,
        TIPO_TRANSACCION,
        DESCRIPCION_TRANSACCION,
        MONTO_TRANSACCION,
        USUARIO_INS,
        FECHA_INS,
        CODIGO_EMPRESA
    ) VALUES (
        p_CODIGO_OUT,
        p_CODIGO_BANCO_ARCHIVO_CONTROL,
        NVL(v_NUMERO_BANCO, '0000'),
        v_NUMERO_CUENTA,
        p_FECHA_TRANSACCION,
        SUBSTR(TRIM(p_NUMERO_TRANSACCION), 1, 20),
        p_TIPO_TRANSACCION_ID,
        SUBSTR(TRIM(p_TIPO_TRANSACCION), 1, 10),
        SUBSTR(TRIM(p_DESCRIPCION_TRANSACCION), 1, 100),
        NVL(p_MONTO_TRANSACCION, 0),
        p_USUARIO_ID,
        SYSDATE,
        p_CODIGO_EMPRESA
    );

    p_Message := 'OK';
EXCEPTION
    WHEN OTHERS THEN
        p_CODIGO_OUT := NULL;
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_BCO_ARC_CONFIRM (
    p_CODIGO_BANCO_ARCHIVO_CONTROL IN  NUMBER,
    p_USUARIO_ID                   IN  NUMBER,
    p_CODIGO_EMPRESA               IN  NUMBER,
    p_CODIGO_ESTADO_CUENTA_OUT     OUT NUMBER,
    p_CANTIDAD_OUT                 OUT NUMBER,
    p_Message                      OUT VARCHAR2
) AS
    v_CONFIRMADO          NUMBER;
    v_CODIGO_CUENTA_BANCO CNT.CNT_BANCO_ARCHIVO_CONTROL.CODIGO_CUENTA_BANCO%TYPE;
    v_FECHA_DESDE         DATE;
    v_FECHA_HASTA         DATE;
    v_SALDO_INICIAL       NUMBER;
    v_SALDO_FINAL         NUMBER;
BEGIN
    p_CODIGO_ESTADO_CUENTA_OUT := NULL;
    p_CANTIDAD_OUT := 0;

    BEGIN
        SELECT ctl.CODIGO_CUENTA_BANCO,
               ctl.FECHA_DESDE,
               ctl.FECHA_HASTA,
               ctl.SALDO_INICIAL,
               ctl.SALDO_FINAL,
               CASE WHEN ctl.CODIGO_ESTADO_CUENTA IS NULL THEN 0 ELSE 1 END
          INTO v_CODIGO_CUENTA_BANCO, v_FECHA_DESDE, v_FECHA_HASTA, v_SALDO_INICIAL, v_SALDO_FINAL, v_CONFIRMADO
          FROM CNT.CNT_BANCO_ARCHIVO_CONTROL ctl
         WHERE ctl.CODIGO_BANCO_ARCHIVO_CONTROL = p_CODIGO_BANCO_ARCHIVO_CONTROL
           AND ctl.CODIGO_EMPRESA = p_CODIGO_EMPRESA;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            p_Message := 'Control de archivo no encontrado.';
            RETURN;
    END;

    IF v_CONFIRMADO = 1 THEN
        p_Message := 'El archivo ya fue confirmado.';
        RETURN;
    END IF;

    SELECT COUNT(1)
      INTO p_CANTIDAD_OUT
      FROM CNT.CNT_BANCO_ARCHIVO det
     WHERE det.CODIGO_BANCO_ARCHIVO_CONTROL = p_CODIGO_BANCO_ARCHIVO_CONTROL
       AND det.CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    IF p_CANTIDAD_OUT = 0 THEN
        p_Message := 'El archivo no tiene movimientos para confirmar.';
        RETURN;
    END IF;

    SAVEPOINT SP_CNT_BCO_ARC_CONFIRM_START;

    SELECT CNT.CNT_S_CODIGO_ESTADO_CUENTA.NEXTVAL
      INTO p_CODIGO_ESTADO_CUENTA_OUT
      FROM DUAL;

    INSERT INTO CNT.CNT_ESTADO_CUENTAS (
        CODIGO_ESTADO_CUENTA,
        CODIGO_CUENTA_BANCO,
        NUMERO_ESTADO_CUENTA,
        FECHA_DESDE,
        FECHA_HASTA,
        SALDO_INICIAL,
        SALDO_FINAL,
        USUARIO_INS,
        FECHA_INS,
        CODIGO_EMPRESA
    ) VALUES (
        p_CODIGO_ESTADO_CUENTA_OUT,
        v_CODIGO_CUENTA_BANCO,
        TO_CHAR(p_CODIGO_BANCO_ARCHIVO_CONTROL),
        v_FECHA_DESDE,
        v_FECHA_HASTA,
        NVL(v_SALDO_INICIAL, 0),
        NVL(v_SALDO_FINAL, 0),
        p_USUARIO_ID,
        SYSDATE,
        p_CODIGO_EMPRESA
    );

    INSERT INTO CNT.CNT_DETALLE_EDO_CTA (
        CODIGO_DETALLE_EDO_CTA,
        CODIGO_ESTADO_CUENTA,
        TIPO_TRANSACCION_ID,
        NUMERO_TRANSACCION,
        FECHA_TRANSACCION,
        DESCRIPCION,
        MONTO,
        STATUS,
        USUARIO_INS,
        FECHA_INS,
        CODIGO_EMPRESA
    )
    SELECT CNT.CNT_S_CODIGO_DETALLE_EDO_CTA.NEXTVAL,
           p_CODIGO_ESTADO_CUENTA_OUT,
           det.TIPO_TRANSACCION_ID,
           det.NUMERO_TRANSACCION,
           det.FECHA_TRANSACCION,
           det.DESCRIPCION_TRANSACCION,
           det.MONTO_TRANSACCION,
           'T',
           p_USUARIO_ID,
           SYSDATE,
           det.CODIGO_EMPRESA
      FROM CNT.CNT_BANCO_ARCHIVO det
     WHERE det.CODIGO_BANCO_ARCHIVO_CONTROL = p_CODIGO_BANCO_ARCHIVO_CONTROL
       AND det.CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    UPDATE CNT.CNT_BANCO_ARCHIVO_CONTROL
       SET CODIGO_ESTADO_CUENTA = p_CODIGO_ESTADO_CUENTA_OUT,
           USUARIO_UPD = p_USUARIO_ID,
           FECHA_UPD = SYSDATE
     WHERE CODIGO_BANCO_ARCHIVO_CONTROL = p_CODIGO_BANCO_ARCHIVO_CONTROL
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    p_Message := 'OK';
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK TO SP_CNT_BCO_ARC_CONFIRM_START;
        p_CODIGO_ESTADO_CUENTA_OUT := NULL;
        p_CANTIDAD_OUT := 0;
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_CONC_GET_ALL (
    p_CODIGO_PERIODO      IN  NUMBER,
    p_CODIGO_BANCO        IN  NUMBER,
    p_CODIGO_CUENTA_BANCO IN  NUMBER,
    p_ESTADO              IN  VARCHAR2,
    p_SEARCH_TEXT         IN  VARCHAR2,
    p_CODIGO_EMPRESA      IN  NUMBER,
    p_ResultSet           OUT SYS_REFCURSOR,
    p_Message             OUT VARCHAR2
) AS
BEGIN
    OPEN p_ResultSet FOR
        SELECT
            v.CODIGO_CONCILIACION,
            v.CODIGO_PERIODO,
            NVL(v.NOMBRE_PERIODO, '') AS NOMBRE_PERIODO,
            v.ANO_PERIODO,
            v.NUMERO_PERIODO,
            v.CODIGO_CUENTA_BANCO,
            v.CODIGO_BANCO,
            NVL(v.NOMBRE_BANCO, '') AS BANCO,
            NVL(v.NUMERO_CUENTA_BANCO, '') AS NO_CUENTA,
            NVL(v.DENOMINACION_FUNCIONAL, '') AS DENOMINACION_FUNCIONAL,
            NVL(v.SALDO_BANCO, 0) AS SALDO_BANCO,
            NVL(v.SALDO_LIBRO, 0) AS SALDO_LIBRO,
            v.FECHA_PRECIERRE,
            v.FECHA_CIERRE,
            CASE
                WHEN v.FECHA_CIERRE IS NOT NULL THEN 'CERRADA'
                WHEN v.FECHA_PRECIERRE IS NOT NULL THEN 'PRECIERRE'
                ELSE 'ABIERTA'
            END AS ESTADO,
            v.CODIGO_EMPRESA
        FROM CNT.CNT_V_CONCILIACIONES v
        WHERE v.CODIGO_EMPRESA = p_CODIGO_EMPRESA
          AND (p_CODIGO_PERIODO IS NULL OR v.CODIGO_PERIODO = p_CODIGO_PERIODO)
          AND (p_CODIGO_BANCO IS NULL OR v.CODIGO_BANCO = p_CODIGO_BANCO)
          AND (p_CODIGO_CUENTA_BANCO IS NULL OR v.CODIGO_CUENTA_BANCO = p_CODIGO_CUENTA_BANCO)
          AND (
                p_ESTADO IS NULL
             OR TRIM(p_ESTADO) IS NULL
             OR UPPER(TRIM(p_ESTADO)) = CASE
                    WHEN v.FECHA_CIERRE IS NOT NULL THEN 'CERRADA'
                    WHEN v.FECHA_PRECIERRE IS NOT NULL THEN 'PRECIERRE'
                    ELSE 'ABIERTA'
                END
          )
          AND (
                p_SEARCH_TEXT IS NULL
             OR TRIM(p_SEARCH_TEXT) IS NULL
             OR UPPER(NVL(v.NOMBRE_PERIODO, '')) LIKE '%' || UPPER(TRIM(p_SEARCH_TEXT)) || '%'
             OR UPPER(NVL(v.NOMBRE_BANCO, '')) LIKE '%' || UPPER(TRIM(p_SEARCH_TEXT)) || '%'
             OR UPPER(NVL(v.NUMERO_CUENTA_BANCO, '')) LIKE '%' || UPPER(TRIM(p_SEARCH_TEXT)) || '%'
             OR UPPER(NVL(v.DENOMINACION_FUNCIONAL, '')) LIKE '%' || UPPER(TRIM(p_SEARCH_TEXT)) || '%'
          )
        ORDER BY v.ANO_PERIODO DESC, v.NUMERO_PERIODO DESC, v.NOMBRE_BANCO, v.NUMERO_CUENTA_BANCO;

    p_Message := 'OK';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SQLERRM;
END;
/
