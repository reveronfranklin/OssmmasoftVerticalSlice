CREATE OR REPLACE PROCEDURE CNT.SP_CNT_AUT_PRES_GET (
    p_FECHA_DESDE        IN DATE,
    p_FECHA_HASTA        IN DATE,
    p_CODIGO_EMPRESA     IN NUMBER,
    p_CODIGO_PRESUPUESTO OUT NUMBER,
    p_Message           OUT VARCHAR2
) AS
    v_count NUMBER;
    v_desde DATE;
    v_hasta DATE;
BEGIN
    p_CODIGO_PRESUPUESTO := NULL;
    IF p_CODIGO_EMPRESA IS NULL OR p_CODIGO_EMPRESA <= 0 THEN
        p_Message := 'La empresa configurada no es valida.';
        RETURN;
    END IF;
    IF p_FECHA_DESDE IS NULL OR p_FECHA_HASTA IS NULL
       OR TRUNC(p_FECHA_DESDE) > TRUNC(p_FECHA_HASTA) THEN
        p_Message := 'El rango de fechas no es valido.';
        RETURN;
    END IF;

    -- Reject overlaps, including a second budget beginning inside the range.
    -- No ROWNUM/MAX selection: the complete date range must belong to one budget.
    SELECT COUNT(*), MIN(CODIGO_PRESUPUESTO), MIN(FECHA_DESDE), MIN(FECHA_HASTA)
      INTO v_count, p_CODIGO_PRESUPUESTO, v_desde, v_hasta
      FROM PRE.PRE_PRESUPUESTOS
     WHERE CODIGO_EMPRESA = p_CODIGO_EMPRESA
       AND TRUNC(FECHA_DESDE) <= TRUNC(p_FECHA_HASTA)
       AND TRUNC(FECHA_HASTA) >= TRUNC(p_FECHA_DESDE);

    IF v_count = 0 THEN
        p_Message := 'No existe presupuesto de la empresa para el rango indicado.';
    ELSIF v_count > 1 THEN
        p_Message := 'El rango coincide con varios presupuestos de la empresa; divida el rango o revise las vigencias.';
    ELSIF TRUNC(p_FECHA_DESDE) < TRUNC(v_desde)
       OR TRUNC(p_FECHA_HASTA) > TRUNC(v_hasta) THEN
        p_Message := 'El rango completo debe pertenecer a un mismo presupuesto de la empresa.';
    ELSE
        p_Message := 'Success';
        RETURN;
    END IF;
    p_CODIGO_PRESUPUESTO := NULL;
EXCEPTION
    WHEN OTHERS THEN
        p_CODIGO_PRESUPUESTO := NULL;
        p_Message := SQLERRM;
END;
/
