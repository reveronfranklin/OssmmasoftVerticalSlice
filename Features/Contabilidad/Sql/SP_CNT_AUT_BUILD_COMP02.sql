-- R58: documented COMP/02 phase only. No rule is inferred for other types.
CREATE OR REPLACE PROCEDURE CNT.SP_CNT_AUT_BUILD_COMP02 (
    p_RUN_ID             IN VARCHAR2,
    p_FECHA_DESDE        IN DATE,
    p_FECHA_HASTA        IN DATE,
    p_CODIGO_PRESUPUESTO IN NUMBER,
    p_CODIGO_EMPRESA     IN NUMBER,
    p_Message           OUT VARCHAR2
) AS
    v_cuentas NUMBER;
    v_numeros NUMBER;
    v_secuencia NUMBER := 0;
    v_debe NUMBER := 0;
    v_haber NUMBER := 0;
    v_monto NUMBER;
    v_compromiso NUMBER;
    v_referencia VARCHAR2(20);
BEGIN
    DELETE FROM CNT.CNT_AUT_LINE_WRK WHERE RUN_ID = p_RUN_ID;

    SELECT COUNT(*), COUNT(DISTINCT TRIM(NUMERO_MAYOR))
      INTO v_cuentas, v_numeros
      FROM CNT.CNT_MAYORES
     WHERE CODIGO_EMPRESA = p_CODIGO_EMPRESA
       AND TRIM(NUMERO_MAYOR) IN ('300', '103');
    IF v_cuentas <> 2 OR v_numeros <> 2 THEN
        p_Message := 'COMP/02 requiere un mayor 300 y uno 103 en la empresa configurada, sin duplicados.';
        RETURN;
    END IF;

    -- A mismatched child must not disappear through the company/budget predicate.
    SELECT COUNT(*) INTO v_cuentas
      FROM PRE.PRE_COMPROMISOS AC
      JOIN PRE.PRE_DETALLE_COMPROMISOS PDC
        ON PDC.CODIGO_COMPROMISO = AC.CODIGO_COMPROMISO
      JOIN PRE.PRE_PUC_COMPROMISOS APC
        ON APC.CODIGO_DETALLE_COMPROMISO = PDC.CODIGO_DETALLE_COMPROMISO
     WHERE AC.CODIGO_EMPRESA = p_CODIGO_EMPRESA
       AND AC.CODIGO_PRESUPUESTO = p_CODIGO_PRESUPUESTO
       AND AC.STATUS <> 'AN'
       AND AC.CODIGO_PROVEEDOR NOT IN (8127, 8128)
       AND AC.FECHA_COMPROMISO >= TRUNC(p_FECHA_DESDE)
       AND AC.FECHA_COMPROMISO < TRUNC(p_FECHA_HASTA) + 1
       AND NVL(APC.MONTO, 0) <> 0
       AND (NVL(APC.CODIGO_EMPRESA, -1) <> AC.CODIGO_EMPRESA
            OR NVL(APC.CODIGO_PRESUPUESTO, -1) <> AC.CODIGO_PRESUPUESTO);
    IF v_cuentas > 0 THEN
        p_Message := 'COMP/02: existen partidas con empresa o presupuesto distintos de su compromiso; revise las fuentes.';
        RETURN;
    END IF;

    FOR r IN (
        WITH fuentes AS (
            SELECT AC.CODIGO_COMPROMISO, AC.NUMERO_COMPROMISO,
                   AC.FECHA_COMPROMISO, RTRIM(AC.MOTIVO) MOTIVO,
                   AC.CODIGO_PROVEEDOR, APC.CODIGO_PUC_COMPROMISO,
                   APC.CODIGO_PUC, APC.MONTO
              FROM PRE.PRE_COMPROMISOS AC
              JOIN PRE.PRE_DETALLE_COMPROMISOS PDC
                ON PDC.CODIGO_COMPROMISO = AC.CODIGO_COMPROMISO
              JOIN PRE.PRE_PUC_COMPROMISOS APC
                ON APC.CODIGO_DETALLE_COMPROMISO = PDC.CODIGO_DETALLE_COMPROMISO
             WHERE AC.CODIGO_EMPRESA = p_CODIGO_EMPRESA
               AND AC.CODIGO_PRESUPUESTO = p_CODIGO_PRESUPUESTO
               AND APC.CODIGO_EMPRESA = AC.CODIGO_EMPRESA
               AND APC.CODIGO_PRESUPUESTO = AC.CODIGO_PRESUPUESTO
               AND AC.STATUS <> 'AN'
               AND AC.CODIGO_PROVEEDOR NOT IN (8127, 8128)
               AND AC.FECHA_COMPROMISO >= TRUNC(p_FECHA_DESDE)
               AND AC.FECHA_COMPROMISO < TRUNC(p_FECHA_HASTA) + 1
               AND NVL(APC.MONTO, 0) <> 0
        ), cuentas AS (
            SELECT CODIGO_MAYOR, TRIM(NUMERO_MAYOR) NUMERO_MAYOR
              FROM CNT.CNT_MAYORES
             WHERE CODIGO_EMPRESA = p_CODIGO_EMPRESA
               AND TRIM(NUMERO_MAYOR) IN ('300', '103')
        ), pares AS (
            SELECT F.*, M.CODIGO_MAYOR, M.NUMERO_MAYOR
              FROM fuentes F CROSS JOIN cuentas M
        ), candidatos AS (
            SELECT P.CODIGO_PUC_COMPROMISO, P.CODIGO_MAYOR,
                   CA.CODIGO_AUXILIAR, CA.SEGMENTO1, CA.SEGMENTO2,
                   CA.DENOMINACION
              FROM pares P
              JOIN CNT.CNT_AUXILIARES CA
                ON CA.CODIGO_MAYOR = P.CODIGO_MAYOR
               AND CA.CODIGO_EMPRESA = p_CODIGO_EMPRESA
             WHERE EXISTS (
                    SELECT 1 FROM CNT.CNT_AUXILIARES_PUC N
                     WHERE N.CODIGO_EMPRESA = p_CODIGO_EMPRESA
                       AND N.CODIGO_PUC = P.CODIGO_PUC
                       AND LTRIM(TRIM(N.TIPO_DOCUMENTO_ID), '0') = '2'
                       AND N.CODIGO_AUXILIAR = CA.CODIGO_AUXILIAR
                   )
                OR (CA.CODIGO_PROVEEDOR = P.CODIGO_PROVEEDOR
                    AND NOT EXISTS (
                        SELECT 1 FROM CNT.CNT_AUXILIARES_PUC N
                        JOIN CNT.CNT_AUXILIARES A
                          ON A.CODIGO_AUXILIAR = N.CODIGO_AUXILIAR
                         AND A.CODIGO_EMPRESA = p_CODIGO_EMPRESA
                         WHERE N.CODIGO_EMPRESA = p_CODIGO_EMPRESA
                           AND A.CODIGO_MAYOR = P.CODIGO_MAYOR
                           AND N.CODIGO_PUC = P.CODIGO_PUC
                           AND LTRIM(TRIM(N.TIPO_DOCUMENTO_ID), '0') = '2'
                    ))
        ), evaluados AS (
            SELECT P.*, C.CODIGO_AUXILIAR, C.SEGMENTO1, C.SEGMENTO2,
                   C.DENOMINACION,
                   COUNT(C.CODIGO_AUXILIAR) OVER (
                       PARTITION BY P.CODIGO_PUC_COMPROMISO, P.CODIGO_MAYOR
                   ) CANT_CANDIDATOS
              FROM pares P LEFT JOIN candidatos C
                ON C.CODIGO_PUC_COMPROMISO = P.CODIGO_PUC_COMPROMISO
               AND C.CODIGO_MAYOR = P.CODIGO_MAYOR
        )
        SELECT CODIGO_COMPROMISO, NUMERO_COMPROMISO, FECHA_COMPROMISO,
               MOTIVO, CODIGO_MAYOR, NUMERO_MAYOR, CODIGO_AUXILIAR,
               SEGMENTO1, SEGMENTO2, DENOMINACION, SUM(MONTO) MONTO,
               MIN(CANT_CANDIDATOS) MIN_CAND, MAX(CANT_CANDIDATOS) MAX_CAND
          FROM evaluados
         GROUP BY CODIGO_COMPROMISO, NUMERO_COMPROMISO, FECHA_COMPROMISO,
                  MOTIVO, CODIGO_MAYOR, NUMERO_MAYOR, CODIGO_AUXILIAR,
                  SEGMENTO1, SEGMENTO2, DENOMINACION
         ORDER BY CODIGO_COMPROMISO, NUMERO_MAYOR DESC, CODIGO_AUXILIAR
    ) LOOP
        IF v_compromiso IS NOT NULL AND v_compromiso <> r.CODIGO_COMPROMISO THEN
            IF v_debe <> v_haber THEN
                DELETE FROM CNT.CNT_AUT_LINE_WRK WHERE RUN_ID = p_RUN_ID;
                p_Message := 'COMP/02: distribucion por auxiliares descuadrada en el compromiso ' || v_referencia || '.';
                RETURN;
            END IF;
            v_debe := 0;
            v_haber := 0;
        END IF;
        v_compromiso := r.CODIGO_COMPROMISO;
        v_referencia := r.NUMERO_COMPROMISO;
        IF r.MIN_CAND <> 1 OR r.MAX_CAND <> 1 THEN
            DELETE FROM CNT.CNT_AUT_LINE_WRK WHERE RUN_ID = p_RUN_ID;
            p_Message := 'COMP/02: falta auxiliar o hay configuracion ambigua en el compromiso '
                         || r.NUMERO_COMPROMISO || ', mayor ' || r.NUMERO_MAYOR || '.';
            RETURN;
        END IF;
        IF ROUND(NVL(r.MONTO, 0), 2) <> 0 THEN
            v_secuencia := v_secuencia + 1;
            v_monto := CASE WHEN r.NUMERO_MAYOR = '300' THEN -ABS(ROUND(r.MONTO, 2)) ELSE ABS(ROUND(r.MONTO, 2)) END;
            INSERT INTO CNT.CNT_AUT_LINE_WRK (
                RUN_ID, SECUENCIA, CODIGO_MAYOR, MAYOR, CODIGO_AUXILIAR,
                AUXILIAR, REFERENCIA1, REFERENCIA2, REFERENCIA3, DESCRIPCION, MONTO
            ) VALUES (
                p_RUN_ID, v_secuencia, r.CODIGO_MAYOR, r.NUMERO_MAYOR,
                r.CODIGO_AUXILIAR,
                r.SEGMENTO1 || '-' || r.SEGMENTO2 || ' ' || r.DENOMINACION,
                SUBSTR(r.NUMERO_COMPROMISO, 1, 20),
                TO_CHAR(r.FECHA_COMPROMISO, 'DD/MM/RRRR'), NULL,
                SUBSTRB('(CP) ' || r.MOTIVO, 1, 200), v_monto
            );
            IF v_monto < 0 THEN v_debe := v_debe - v_monto;
            ELSE v_haber := v_haber + v_monto;
            END IF;
        END IF;
    END LOOP;
    IF v_debe <> v_haber THEN
        DELETE FROM CNT.CNT_AUT_LINE_WRK WHERE RUN_ID = p_RUN_ID;
        p_Message := 'COMP/02: distribucion por auxiliares descuadrada en el compromiso ' || v_referencia || '.';
        RETURN;
    END IF;
    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        DELETE FROM CNT.CNT_AUT_LINE_WRK WHERE RUN_ID = p_RUN_ID;
        p_Message := SQLERRM;
END;
/
