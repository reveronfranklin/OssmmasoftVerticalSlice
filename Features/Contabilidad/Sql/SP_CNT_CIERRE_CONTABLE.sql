CREATE OR REPLACE PROCEDURE CNT.SP_CNT_CIE_PER_GET (
    p_ANO_PERIODO    IN NUMBER,
    p_SOLO_PEND      IN NUMBER,
    p_SEARCH_TEXT    IN VARCHAR2,
    p_CODIGO_EMPRESA IN NUMBER,
    p_ResultSet      OUT SYS_REFCURSOR,
    p_Message        OUT VARCHAR2
) AS
BEGIN
    OPEN p_ResultSet FOR
        SELECT p.CODIGO_PERIODO,
               p.NOMBRE_PERIODO,
               p.FECHA_DESDE,
               p.FECHA_HASTA,
               p.ANO_PERIODO,
               p.NUMERO_PERIODO,
               p.FECHA_PRECIERRE,
               p.USUARIO_PRECIERRE,
               p.FECHA_CIERRE,
               p.USUARIO_CIERRE,
               CASE
                   WHEN p.FECHA_CIERRE IS NOT NULL THEN 'CERRADO'
                   WHEN p.FECHA_PRECIERRE IS NOT NULL
                        AND NVL(m.CANT_MODIF, 0) > 0 THEN 'MODIFICADO'
                   WHEN p.FECHA_PRECIERRE IS NOT NULL THEN 'PRECIERRE'
                   ELSE 'ABIERTO'
               END AS ESTADO,
               NVL(ts.CANT_TMP_SALDOS, 0) AS CANT_TMP_SALDOS,
               NVL(ta.CANT_TMP_ANALITICO, 0) AS CANT_TMP_ANALITICO,
               NVL(s.CANT_SALDOS, 0) AS CANT_SALDOS,
               NVL(ha.CANT_HIST_ANALITICO, 0) AS CANT_HIST_ANALITICO,
               NVL(m.CANT_MODIF, 0) AS CANT_MODIFICACIONES,
               p.CODIGO_EMPRESA
          FROM CNT.CNT_PERIODOS p
          LEFT JOIN (
                SELECT CODIGO_PERIODO,
                       CODIGO_EMPRESA,
                       COUNT(1) CANT_TMP_SALDOS
                  FROM CNT.CNT_TMP_SALDOS
                 GROUP BY CODIGO_PERIODO, CODIGO_EMPRESA
          ) ts ON ts.CODIGO_PERIODO = p.CODIGO_PERIODO
              AND ts.CODIGO_EMPRESA = p.CODIGO_EMPRESA
          LEFT JOIN (
                SELECT x.CODIGO_PERIODO,
                       x.CODIGO_EMPRESA,
                       COUNT(1) CANT_TMP_ANALITICO
                  FROM CNT.CNT_TMP_ANALITICO a
                  INNER JOIN CNT.CNT_TMP_SALDOS x
                     ON x.CODIGO_TMP_SALDO = a.CODIGO_TMP_SALDO
                    AND x.CODIGO_EMPRESA = a.CODIGO_EMPRESA
                 GROUP BY x.CODIGO_PERIODO, x.CODIGO_EMPRESA
          ) ta ON ta.CODIGO_PERIODO = p.CODIGO_PERIODO
              AND ta.CODIGO_EMPRESA = p.CODIGO_EMPRESA
          LEFT JOIN (
                SELECT CODIGO_PERIODO,
                       CODIGO_EMPRESA,
                       COUNT(1) CANT_SALDOS
                  FROM CNT.CNT_SALDOS
                 GROUP BY CODIGO_PERIODO, CODIGO_EMPRESA
          ) s ON s.CODIGO_PERIODO = p.CODIGO_PERIODO
             AND s.CODIGO_EMPRESA = p.CODIGO_EMPRESA
          LEFT JOIN (
                SELECT s.CODIGO_PERIODO,
                       s.CODIGO_EMPRESA,
                       COUNT(1) CANT_HIST_ANALITICO
                  FROM CNT.CNT_HIST_ANALITICO h
                  INNER JOIN CNT.CNT_SALDOS s
                     ON s.CODIGO_SALDO = h.CODIGO_SALDO
                    AND s.CODIGO_EMPRESA = h.CODIGO_EMPRESA
                 GROUP BY s.CODIGO_PERIODO, s.CODIGO_EMPRESA
          ) ha ON ha.CODIGO_PERIODO = p.CODIGO_PERIODO
              AND ha.CODIGO_EMPRESA = p.CODIGO_EMPRESA
          LEFT JOIN (
                SELECT x.CODIGO_PERIODO,
                       x.CODIGO_EMPRESA,
                       COUNT(1) CANT_MODIF
                  FROM CNT.CNT_PERIODOS x,
                       (
                           SELECT c.CODIGO_PERIODO,
                                  c.CODIGO_EMPRESA,
                                  MAX(GREATEST(
                                      NVL(c.FECHA_UPD, c.FECHA_INS),
                                      NVL(d.FECHA_UPD, d.FECHA_INS)
                                  )) FECHA_MODIF
                             FROM CNT.CNT_COMPROBANTES c
                             INNER JOIN CNT.CNT_DETALLE_COMPROBANTE d
                                ON d.CODIGO_COMPROBANTE = c.CODIGO_COMPROBANTE
                            GROUP BY c.CODIGO_PERIODO, c.CODIGO_EMPRESA
                       ) y
                 WHERE y.CODIGO_PERIODO = x.CODIGO_PERIODO
                   AND y.CODIGO_EMPRESA = x.CODIGO_EMPRESA
                   AND x.FECHA_PRECIERRE IS NOT NULL
                   AND y.FECHA_MODIF > x.FECHA_PRECIERRE
                 GROUP BY x.CODIGO_PERIODO, x.CODIGO_EMPRESA
          ) m ON m.CODIGO_PERIODO = p.CODIGO_PERIODO
             AND m.CODIGO_EMPRESA = p.CODIGO_EMPRESA
         WHERE p.CODIGO_EMPRESA = p_CODIGO_EMPRESA
           AND (p_ANO_PERIODO IS NULL OR p.ANO_PERIODO = p_ANO_PERIODO)
           AND (
                p_SEARCH_TEXT IS NULL
                OR UPPER(p.NOMBRE_PERIODO) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%'
                OR TO_CHAR(p.ANO_PERIODO) LIKE '%' || p_SEARCH_TEXT || '%'
                OR TO_CHAR(p.NUMERO_PERIODO) LIKE '%' || p_SEARCH_TEXT || '%'
           )
           AND (
                NVL(p_SOLO_PEND, 0) = 0
                OR p.FECHA_CIERRE IS NULL
                OR NVL(m.CANT_MODIF, 0) > 0
           )
         ORDER BY p.ANO_PERIODO DESC, p.NUMERO_PERIODO DESC;

    p_Message := 'OK';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_CIE_MOD_GET (
    p_CODIGO_PERIODO IN NUMBER,
    p_CODIGO_EMPRESA IN NUMBER,
    p_CANTIDAD_OUT   OUT NUMBER,
    p_Message        OUT VARCHAR2
) AS
BEGIN
    SELECT COUNT(1)
      INTO p_CANTIDAD_OUT
      FROM CNT.CNT_PERIODOS p,
           (
               SELECT c.CODIGO_PERIODO,
                      c.CODIGO_EMPRESA,
                      MAX(GREATEST(
                          NVL(c.FECHA_UPD, c.FECHA_INS),
                          NVL(d.FECHA_UPD, d.FECHA_INS)
                      )) FECHA_MODIF
                 FROM CNT.CNT_COMPROBANTES c
                 INNER JOIN CNT.CNT_DETALLE_COMPROBANTE d
                    ON d.CODIGO_COMPROBANTE = c.CODIGO_COMPROBANTE
                WHERE c.CODIGO_PERIODO = p_CODIGO_PERIODO
                  AND c.CODIGO_EMPRESA = p_CODIGO_EMPRESA
                GROUP BY c.CODIGO_PERIODO, c.CODIGO_EMPRESA
           ) x
     WHERE p.CODIGO_PERIODO = p_CODIGO_PERIODO
       AND p.CODIGO_EMPRESA = p_CODIGO_EMPRESA
       AND x.CODIGO_PERIODO = p.CODIGO_PERIODO
       AND x.CODIGO_EMPRESA = p.CODIGO_EMPRESA
       AND p.FECHA_PRECIERRE IS NOT NULL
       AND x.FECHA_MODIF > p.FECHA_PRECIERRE;

    p_Message := 'OK';
EXCEPTION
    WHEN OTHERS THEN
        p_CANTIDAD_OUT := 0;
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_CIE_PRE (
    p_CODIGO_PERIODO IN NUMBER,
    p_USUARIO_ID     IN NUMBER,
    p_CODIGO_EMPRESA IN NUMBER,
    p_TMP_SALDOS_OUT OUT NUMBER,
    p_TMP_ANA_OUT    OUT NUMBER,
    p_Message        OUT VARCHAR2
) AS
    v_count NUMBER;
BEGIN
    SELECT COUNT(1)
      INTO v_count
      FROM CNT.CNT_PERIODOS
     WHERE CODIGO_PERIODO = p_CODIGO_PERIODO
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA
       AND FECHA_CIERRE IS NULL;

    IF v_count = 0 THEN
        p_Message := 'No existe el periodo a precerrar o esta cerrado.';
        RETURN;
    END IF;

    DELETE FROM CNT.CNT_TMP_ANALITICO a
     WHERE EXISTS (
           SELECT 1
             FROM CNT.CNT_TMP_SALDOS s
            WHERE s.CODIGO_TMP_SALDO = a.CODIGO_TMP_SALDO
              AND s.CODIGO_PERIODO = p_CODIGO_PERIODO
              AND s.CODIGO_EMPRESA = p_CODIGO_EMPRESA
     );

    DELETE FROM CNT.CNT_TMP_SALDOS
     WHERE CODIGO_PERIODO = p_CODIGO_PERIODO
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    p_TMP_SALDOS_OUT := 0;

    FOR r_saldo IN (
            SELECT x.CODIGO_MAYOR,
                   x.CODIGO_AUXILIAR,
                   CNT.CNT_SALDO_INICIAL(
                       p_CODIGO_PERIODO,
                       x.CODIGO_MAYOR,
                       x.CODIGO_AUXILIAR
                   ) SALDO_INICIAL,
                   SUM(x.DEBITOS) DEBITOS,
                   SUM(x.CREDITOS) CREDITOS
              FROM (
            SELECT d.CODIGO_MAYOR,
                   d.CODIGO_AUXILIAR,
                   NVL(d.MONTO, 0) DEBITOS,
                   0 CREDITOS
              FROM CNT.CNT_COMPROBANTES c
              INNER JOIN CNT.CNT_DETALLE_COMPROBANTE d
                 ON d.CODIGO_COMPROBANTE = c.CODIGO_COMPROBANTE
             WHERE c.CODIGO_PERIODO = p_CODIGO_PERIODO
               AND c.CODIGO_EMPRESA = p_CODIGO_EMPRESA
               AND NVL(d.MONTO, 0) > 0
            UNION ALL
            SELECT d.CODIGO_MAYOR,
                   d.CODIGO_AUXILIAR,
                   0 DEBITOS,
                   NVL(d.MONTO, 0) CREDITOS
              FROM CNT.CNT_COMPROBANTES c
              INNER JOIN CNT.CNT_DETALLE_COMPROBANTE d
                 ON d.CODIGO_COMPROBANTE = c.CODIGO_COMPROBANTE
             WHERE c.CODIGO_PERIODO = p_CODIGO_PERIODO
               AND c.CODIGO_EMPRESA = p_CODIGO_EMPRESA
               AND NVL(d.MONTO, 0) < 0
            UNION ALL
            SELECT s.CODIGO_MAYOR,
                   s.CODIGO_AUXILIAR,
                   0 DEBITOS,
                   0 CREDITOS
              FROM CNT.CNT_SALDOS s
             WHERE s.CODIGO_PERIODO = (
                   SELECT MAX(sp.CODIGO_PERIODO)
                     FROM CNT.CNT_SALDOS sp
                    WHERE sp.CODIGO_PERIODO < DECODE(p_CODIGO_PERIODO, 0, 1, p_CODIGO_PERIODO)
                      AND sp.CODIGO_MAYOR = s.CODIGO_MAYOR
                      AND sp.CODIGO_AUXILIAR = s.CODIGO_AUXILIAR
                      AND sp.CODIGO_EMPRESA = p_CODIGO_EMPRESA
             )
               AND s.CODIGO_EMPRESA = p_CODIGO_EMPRESA
               AND s.MONTO <> 0
               AND NOT EXISTS (
                   SELECT 1
                     FROM CNT.CNT_COMPROBANTES c
                     INNER JOIN CNT.CNT_DETALLE_COMPROBANTE d
                        ON d.CODIGO_COMPROBANTE = c.CODIGO_COMPROBANTE
                    WHERE c.CODIGO_PERIODO = p_CODIGO_PERIODO
                      AND c.CODIGO_EMPRESA = p_CODIGO_EMPRESA
                      AND d.CODIGO_MAYOR = s.CODIGO_MAYOR
                      AND d.CODIGO_AUXILIAR = s.CODIGO_AUXILIAR
	                      AND NVL(d.MONTO, 0) <> 0
	               )
              ) x
             GROUP BY x.CODIGO_MAYOR, x.CODIGO_AUXILIAR
    ) LOOP
        INSERT INTO CNT.CNT_TMP_SALDOS (
            CODIGO_TMP_SALDO,
            CODIGO_PERIODO,
            CODIGO_MAYOR,
            CODIGO_AUXILIAR,
            SALDO_INICIAL,
            DEBITOS,
            CREDITOS,
            EXTRA1,
            EXTRA2,
            EXTRA3,
            USUARIO_INS,
            FECHA_INS,
            USUARIO_UPD,
            FECHA_UPD,
            CODIGO_EMPRESA
        ) VALUES (
            CNT.CNT_S_CODIGO_TMP_SALDO.NEXTVAL,
            p_CODIGO_PERIODO,
            r_saldo.CODIGO_MAYOR,
            r_saldo.CODIGO_AUXILIAR,
            r_saldo.SALDO_INICIAL,
            r_saldo.DEBITOS,
            r_saldo.CREDITOS,
            NULL,
            NULL,
            NULL,
            p_USUARIO_ID,
            SYSDATE,
            NULL,
            NULL,
            p_CODIGO_EMPRESA
        );

        p_TMP_SALDOS_OUT := p_TMP_SALDOS_OUT + 1;
    END LOOP;

    INSERT INTO CNT.CNT_TMP_ANALITICO (
        CODIGO_TMP_ANALITICO,
        CODIGO_TMP_SALDO,
        CODIGO_DETALLE_COMPROBANTE,
        EXTRA1,
        EXTRA2,
        EXTRA3,
        USUARIO_INS,
        FECHA_INS,
        USUARIO_UPD,
        FECHA_UPD,
        CODIGO_EMPRESA
    )
    SELECT CNT.CNT_S_CODIGO_TMP_ANALITICO.NEXTVAL,
           s.CODIGO_TMP_SALDO,
           d.CODIGO_DETALLE_COMPROBANTE,
           NULL,
           NULL,
           NULL,
           p_USUARIO_ID,
           SYSDATE,
           NULL,
           NULL,
           p_CODIGO_EMPRESA
      FROM CNT.CNT_TMP_SALDOS s
      INNER JOIN CNT.CNT_COMPROBANTES c
         ON c.CODIGO_PERIODO = s.CODIGO_PERIODO
        AND c.CODIGO_EMPRESA = s.CODIGO_EMPRESA
      INNER JOIN CNT.CNT_DETALLE_COMPROBANTE d
         ON d.CODIGO_COMPROBANTE = c.CODIGO_COMPROBANTE
        AND d.CODIGO_MAYOR = s.CODIGO_MAYOR
        AND d.CODIGO_AUXILIAR = s.CODIGO_AUXILIAR
     WHERE s.CODIGO_PERIODO = p_CODIGO_PERIODO
       AND s.CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    p_TMP_ANA_OUT := SQL%ROWCOUNT;

    UPDATE CNT.CNT_PERIODOS
       SET FECHA_PRECIERRE = SYSDATE,
           USUARIO_PRECIERRE = p_USUARIO_ID,
           USUARIO_UPD = p_USUARIO_ID,
           FECHA_UPD = SYSDATE
     WHERE CODIGO_PERIODO = p_CODIGO_PERIODO
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    COMMIT;
    p_Message := 'OK';
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        p_TMP_SALDOS_OUT := 0;
        p_TMP_ANA_OUT := 0;
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_CIE_CER (
    p_CODIGO_PERIODO IN NUMBER,
    p_USUARIO_ID     IN NUMBER,
    p_CODIGO_EMPRESA IN NUMBER,
    p_SALDOS_OUT     OUT NUMBER,
    p_HIST_OUT       OUT NUMBER,
    p_Message        OUT VARCHAR2
) AS
    v_count NUMBER;
    v_modif NUMBER;
BEGIN
    SELECT COUNT(1)
      INTO v_count
      FROM CNT.CNT_PERIODOS
     WHERE CODIGO_PERIODO = p_CODIGO_PERIODO
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA
       AND FECHA_CIERRE IS NULL;

    IF v_count = 0 THEN
        p_Message := 'No existe el periodo a cerrar o ya esta cerrado.';
        RETURN;
    END IF;

    SELECT COUNT(1)
      INTO v_count
      FROM CNT.CNT_PERIODOS
     WHERE CODIGO_PERIODO = p_CODIGO_PERIODO
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA
       AND FECHA_PRECIERRE IS NOT NULL;

    IF v_count = 0 THEN
        p_Message := 'Debe ejecutar el precierre antes del cierre.';
        RETURN;
    END IF;

    CNT.SP_CNT_CIE_MOD_GET(p_CODIGO_PERIODO, p_CODIGO_EMPRESA, v_modif, p_Message);
    IF NVL(v_modif, 0) > 0 THEN
        p_Message := 'Existen modificaciones en los comprobantes despues del ultimo precierre.';
        RETURN;
    END IF;

    DELETE FROM CNT.CNT_HIST_ANALITICO h
     WHERE EXISTS (
           SELECT 1
             FROM CNT.CNT_SALDOS s
            WHERE s.CODIGO_SALDO = h.CODIGO_SALDO
              AND s.CODIGO_PERIODO = p_CODIGO_PERIODO
              AND s.CODIGO_EMPRESA = p_CODIGO_EMPRESA
     );

    DELETE FROM CNT.CNT_SALDOS
     WHERE CODIGO_PERIODO = p_CODIGO_PERIODO
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    INSERT INTO CNT.CNT_SALDOS (
        CODIGO_SALDO,
        CODIGO_PERIODO,
        CODIGO_MAYOR,
        CODIGO_AUXILIAR,
        DEBITOS,
        CREDITOS,
        MONTO,
        EXTRA1,
        EXTRA2,
        EXTRA3,
        USUARIO_INS,
        FECHA_INS,
        USUARIO_UPD,
        FECHA_UPD,
        CODIGO_EMPRESA
    )
    SELECT CNT.CNT_S_CODIGO_SALDO.NEXTVAL,
           CODIGO_PERIODO,
           CODIGO_MAYOR,
           CODIGO_AUXILIAR,
           DEBITOS,
           CREDITOS,
           SALDO_INICIAL + CREDITOS + DEBITOS,
           EXTRA1,
           EXTRA2,
           EXTRA3,
           USUARIO_INS,
           FECHA_INS,
           USUARIO_UPD,
           FECHA_UPD,
           CODIGO_EMPRESA
      FROM CNT.CNT_TMP_SALDOS
     WHERE CODIGO_PERIODO = p_CODIGO_PERIODO
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    p_SALDOS_OUT := SQL%ROWCOUNT;

    INSERT INTO CNT.CNT_HIST_ANALITICO (
        CODIGO_HIST_ANALITICO,
        CODIGO_SALDO,
        CODIGO_DETALLE_COMPROBANTE,
        EXTRA1,
        EXTRA2,
        EXTRA3,
        USUARIO_INS,
        FECHA_INS,
        USUARIO_UPD,
        FECHA_UPD,
        CODIGO_EMPRESA
    )
    SELECT CNT.CNT_S_CODIGO_HIST_ANALITICO.NEXTVAL,
           s.CODIGO_SALDO,
           d.CODIGO_DETALLE_COMPROBANTE,
           NULL,
           NULL,
           NULL,
           p_USUARIO_ID,
           SYSDATE,
           NULL,
           NULL,
           p_CODIGO_EMPRESA
      FROM CNT.CNT_SALDOS s
      INNER JOIN CNT.CNT_COMPROBANTES c
         ON c.CODIGO_PERIODO = s.CODIGO_PERIODO
        AND c.CODIGO_EMPRESA = s.CODIGO_EMPRESA
      INNER JOIN CNT.CNT_DETALLE_COMPROBANTE d
         ON d.CODIGO_COMPROBANTE = c.CODIGO_COMPROBANTE
        AND d.CODIGO_MAYOR = s.CODIGO_MAYOR
        AND d.CODIGO_AUXILIAR = s.CODIGO_AUXILIAR
     WHERE s.CODIGO_PERIODO = p_CODIGO_PERIODO
       AND s.CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    p_HIST_OUT := SQL%ROWCOUNT;

    UPDATE CNT.CNT_PERIODOS
       SET FECHA_CIERRE = SYSDATE,
           USUARIO_CIERRE = p_USUARIO_ID,
           USUARIO_UPD = p_USUARIO_ID,
           FECHA_UPD = SYSDATE
     WHERE CODIGO_PERIODO = p_CODIGO_PERIODO
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    DELETE FROM CNT.CNT_TMP_ANALITICO a
     WHERE EXISTS (
           SELECT 1
             FROM CNT.CNT_TMP_SALDOS s
            WHERE s.CODIGO_TMP_SALDO = a.CODIGO_TMP_SALDO
              AND s.CODIGO_PERIODO = p_CODIGO_PERIODO
              AND s.CODIGO_EMPRESA = p_CODIGO_EMPRESA
     );

    DELETE FROM CNT.CNT_TMP_SALDOS
     WHERE CODIGO_PERIODO = p_CODIGO_PERIODO
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    COMMIT;
    p_Message := 'OK';
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        p_SALDOS_OUT := 0;
        p_HIST_OUT := 0;
        p_Message := SQLERRM;
END;
/

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_CIE_REV (
    p_CODIGO_PERIODO IN NUMBER,
    p_USUARIO_ID     IN NUMBER,
    p_CODIGO_EMPRESA IN NUMBER,
    p_SALDOS_OUT     OUT NUMBER,
    p_HIST_OUT       OUT NUMBER,
    p_Message        OUT VARCHAR2
) AS
    v_count NUMBER;
BEGIN
    SELECT COUNT(1)
      INTO v_count
      FROM CNT.CNT_PERIODOS
     WHERE CODIGO_PERIODO = p_CODIGO_PERIODO
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA
       AND USUARIO_CIERRE IS NOT NULL
       AND FECHA_CIERRE IS NOT NULL;

    IF v_count = 0 THEN
        p_Message := 'El periodo contable ya esta abierto.';
        RETURN;
    END IF;

    SELECT COUNT(1)
      INTO p_HIST_OUT
      FROM CNT.CNT_HIST_ANALITICO h
     WHERE EXISTS (
           SELECT 1
             FROM CNT.CNT_SALDOS s
            WHERE s.CODIGO_SALDO = h.CODIGO_SALDO
              AND s.CODIGO_PERIODO = p_CODIGO_PERIODO
              AND s.CODIGO_EMPRESA = p_CODIGO_EMPRESA
     );

    DELETE FROM CNT.CNT_HIST_ANALITICO h
     WHERE EXISTS (
           SELECT 1
             FROM CNT.CNT_SALDOS s
            WHERE s.CODIGO_SALDO = h.CODIGO_SALDO
              AND s.CODIGO_PERIODO = p_CODIGO_PERIODO
              AND s.CODIGO_EMPRESA = p_CODIGO_EMPRESA
     );

    SELECT COUNT(1)
      INTO p_SALDOS_OUT
      FROM CNT.CNT_SALDOS
     WHERE CODIGO_PERIODO = p_CODIGO_PERIODO
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    DELETE FROM CNT.CNT_SALDOS
     WHERE CODIGO_PERIODO = p_CODIGO_PERIODO
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    UPDATE CNT.CNT_PERIODOS
       SET FECHA_PRECIERRE = NULL,
           USUARIO_PRECIERRE = NULL,
           FECHA_CIERRE = NULL,
           USUARIO_CIERRE = NULL,
           EXTRA1 = SUBSTR('R1|' || TO_CHAR(SYSDATE, 'YYYY-MM-DD HH24:MI:SS'), 1, 100),
           USUARIO_UPD = p_USUARIO_ID,
           FECHA_UPD = SYSDATE
     WHERE CODIGO_PERIODO = p_CODIGO_PERIODO
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    COMMIT;
    p_Message := 'OK';
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        p_SALDOS_OUT := 0;
        p_HIST_OUT := 0;
        p_Message := SQLERRM;
END;
/
