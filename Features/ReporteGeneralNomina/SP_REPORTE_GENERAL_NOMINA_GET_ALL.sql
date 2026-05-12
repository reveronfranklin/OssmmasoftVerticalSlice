CREATE OR REPLACE PROCEDURE RH.SP_REP_GRAL_NOMINA_GET_ALL (
    p_from_table1    IN VARCHAR2,
    p_from_table2    IN VARCHAR2,
    p_tipo_nomina    IN NUMBER,
    p_fecha_pago     IN DATE,
    p_codigo_empresa IN NUMBER,
    p_where          IN VARCHAR2,
    p_ResultSet      OUT SYS_REFCURSOR,
    p_Message        OUT VARCHAR2,
    p_TotalRecords   OUT NUMBER
) AS
    v_sql            VARCHAR2(32767);
    v_count_sql      VARCHAR2(32767);
    v_query          VARCHAR2(32767);
    v_from           VARCHAR2(32767);
    v_where_extra    VARCHAR2(32767);
    v_from_table1_up VARCHAR2(32767);
    v_from_table2_up VARCHAR2(32767);

    FUNCTION is_invalid_from_fragment(p_value IN VARCHAR2) RETURN BOOLEAN IS
        v_value VARCHAR2(32767);
    BEGIN
        IF p_value IS NULL OR TRIM(p_value) IS NULL THEN
            RETURN TRUE;
        END IF;

        v_value := ' ' || UPPER(p_value) || ' ';

        IF INSTR(v_value, ';') > 0
           OR INSTR(v_value, '--') > 0
           OR INSTR(v_value, '/*') > 0
           OR INSTR(v_value, '*/') > 0
           OR INSTR(v_value, ' DROP ') > 0
           OR INSTR(v_value, ' DELETE ') > 0
           OR INSTR(v_value, ' UPDATE ') > 0
           OR INSTR(v_value, ' INSERT ') > 0
           OR INSTR(v_value, ' MERGE ') > 0
           OR INSTR(v_value, ' ALTER ') > 0
           OR INSTR(v_value, ' CREATE ') > 0
           OR INSTR(v_value, ' EXEC ') > 0
           OR INSTR(v_value, ' EXECUTE ') > 0 THEN
            RETURN TRUE;
        END IF;

        RETURN FALSE;
    END;

    FUNCTION is_invalid_where_fragment(p_value IN VARCHAR2) RETURN BOOLEAN IS
        v_value VARCHAR2(32767);
    BEGIN
        IF p_value IS NULL OR TRIM(p_value) IS NULL THEN
            RETURN FALSE;
        END IF;

        v_value := ' ' || UPPER(p_value) || ' ';

        IF INSTR(v_value, ';') > 0
           OR INSTR(v_value, '--') > 0
           OR INSTR(v_value, '/*') > 0
           OR INSTR(v_value, '*/') > 0
           OR INSTR(v_value, ' DROP ') > 0
           OR INSTR(v_value, ' DELETE ') > 0
           OR INSTR(v_value, ' UPDATE ') > 0
           OR INSTR(v_value, ' INSERT ') > 0
           OR INSTR(v_value, ' MERGE ') > 0
           OR INSTR(v_value, ' ALTER ') > 0
           OR INSTR(v_value, ' CREATE ') > 0
           OR INSTR(v_value, ' EXEC ') > 0
           OR INSTR(v_value, ' EXECUTE ') > 0 THEN
            RETURN TRUE;
        END IF;

        RETURN FALSE;
    END;
BEGIN
    p_TotalRecords := 0;

    IF is_invalid_from_fragment(p_from_table1) OR is_invalid_from_fragment(p_from_table2) THEN
        p_Message := 'Invalid FROM parameter';
        OPEN p_ResultSet FOR
            SELECT
                CAST(NULL AS VARCHAR2(100)) R_TIPO_CONCEPTO,
                CAST(NULL AS VARCHAR2(100)) R_NUMERO_CONCEPTO,
                CAST(NULL AS VARCHAR2(4000)) R_DENOMINACION_CONCEPTO,
                CAST(NULL AS NUMBER) R_ASIGNACION,
                CAST(NULL AS NUMBER) R_DEDUCCION,
                CAST(NULL AS NUMBER) R_MONTO_VISIBLE,
                CAST(NULL AS NUMBER) R_MONTO,
                CAST(NULL AS NUMBER) R_DEDUCIBLE
            FROM DUAL
            WHERE 1 = 0;
        RETURN;
    END IF;

    IF is_invalid_where_fragment(p_where) THEN
        p_Message := 'Invalid WHERE parameter';
        OPEN p_ResultSet FOR
            SELECT
                CAST(NULL AS VARCHAR2(100)) R_TIPO_CONCEPTO,
                CAST(NULL AS VARCHAR2(100)) R_NUMERO_CONCEPTO,
                CAST(NULL AS VARCHAR2(4000)) R_DENOMINACION_CONCEPTO,
                CAST(NULL AS NUMBER) R_ASIGNACION,
                CAST(NULL AS NUMBER) R_DEDUCCION,
                CAST(NULL AS NUMBER) R_MONTO_VISIBLE,
                CAST(NULL AS NUMBER) R_MONTO,
                CAST(NULL AS NUMBER) R_DEDUCIBLE
            FROM DUAL
            WHERE 1 = 0;
        RETURN;
    END IF;

    v_from_table1_up := TRIM(p_from_table1);
    v_from_table2_up := TRIM(p_from_table2);
    v_where_extra := '';

    IF p_where IS NOT NULL AND TRIM(p_where) IS NOT NULL THEN
        v_where_extra := ' AND (' || p_where || ') ';
    END IF;

    v_from := ' FROM ' ||
              v_from_table1_up || ', ' ||
              v_from_table2_up || ', ' ||
              'RH.RH_CONCEPTOS RC, ' ||
              'PRE.PRE_INDICE_CAT_PRG PVIO ';

    v_query :=
        'SELECT
             RC.TIPO_CONCEPTO R_TIPO_CONCEPTO,
             RC.CODIGO R_NUMERO_CONCEPTO,
             RC.DENOMINACION R_DENOMINACION_CONCEPTO,
             DECODE(SIGN(SUM(RTN.MONTO)), 1, SUM(RTN.MONTO), 0) R_ASIGNACION,
             DECODE(SIGN(SUM(RTN.MONTO)), -1, -(SUM(RTN.MONTO)), 0) R_DEDUCCION,
             DECODE(SIGN(SUM(RTN.MONTO)), 1, (SUM(RTN.MONTO)), -(SUM(RTN.MONTO))) R_MONTO_VISIBLE,
             SUM(RTN.MONTO) R_MONTO,
             RC.DEDUSIBLE R_DEDUCIBLE ' ||
        v_from ||
        'WHERE RVPC.CODIGO_TIPO_NOMINA = :b_tipo_nomina1
             AND RTN.FECHA_NOMINA = :b_fecha_pago1
             AND RVPC.CODIGO_TIPO_NOMINA = RTN.CODIGO_TIPO_NOMINA
             AND RVPC.CODIGO_PERSONA = RTN.CODIGO_PERSONA
             AND RVPC.CODIGO_EMPRESA = :b_codigo_empresa1
             AND RC.CODIGO_CONCEPTO = RTN.CODIGO_CONCEPTO
             AND PVIO.CODIGO_ICP = RVPC.CODIGO_ICP ' ||
             v_where_extra ||
        'GROUP BY
             RC.TIPO_CONCEPTO,
             RC.CODIGO,
             RC.DENOMINACION,
             RC.DEDUSIBLE,
             RTN.FECHA_NOMINA
         UNION
         SELECT
             NULL R_TIPO_CONCEPTO,
             NULL R_NUMERO_CONCEPTO,
             RVPC.DESCRIPCION_BANCO R_DENOMINACION_CONCEPTO,
             DECODE(SIGN(SUM(RTN.MONTO)), 1, SUM(RTN.MONTO), 0) R_ASIGNACION,
             DECODE(SIGN(SUM(RTN.MONTO)), -1, -(SUM(RTN.MONTO)), 0) R_DEDUCCION,
             DECODE(SIGN(SUM(RTN.MONTO)), 1, (SUM(RTN.MONTO)), -(SUM(RTN.MONTO))) R_MONTO_VISIBLE,
             SUM(RTN.MONTO) R_MONTO,
             COUNT(DISTINCT RTN.CODIGO_PERSONA) R_DEDUCIBLE ' ||
        v_from ||
        'WHERE RVPC.CODIGO_TIPO_NOMINA = :b_tipo_nomina2
             AND RTN.FECHA_NOMINA = :b_fecha_pago2
             AND RVPC.CODIGO_TIPO_NOMINA = RTN.CODIGO_TIPO_NOMINA
             AND RVPC.CODIGO_PERSONA = RTN.CODIGO_PERSONA
             AND RVPC.CODIGO_EMPRESA = :b_codigo_empresa2
             AND RC.CODIGO_CONCEPTO = RTN.CODIGO_CONCEPTO
             AND PVIO.CODIGO_ICP = RVPC.CODIGO_ICP ' ||
             v_where_extra ||
        'GROUP BY
             NULL,
             NULL,
             RVPC.DESCRIPCION_BANCO,
             RTN.FECHA_NOMINA';

    v_count_sql := 'SELECT COUNT(*) FROM (' || v_query || ')';

    EXECUTE IMMEDIATE v_count_sql
        INTO p_TotalRecords
        USING p_tipo_nomina,
              p_fecha_pago,
              p_codigo_empresa,
              p_tipo_nomina,
              p_fecha_pago,
              p_codigo_empresa;

    v_sql := v_query || ' ORDER BY 1, 2, 4';

    OPEN p_ResultSet FOR v_sql
        USING p_tipo_nomina,
              p_fecha_pago,
              p_codigo_empresa,
              p_tipo_nomina,
              p_fecha_pago,
              p_codigo_empresa;

    p_Message := 'Success';

EXCEPTION
    WHEN OTHERS THEN
        p_Message := SUBSTR(SQLERRM, 1, 4000);
        p_TotalRecords := 0;
        OPEN p_ResultSet FOR
            SELECT
                CAST(NULL AS VARCHAR2(100)) R_TIPO_CONCEPTO,
                CAST(NULL AS VARCHAR2(100)) R_NUMERO_CONCEPTO,
                CAST(NULL AS VARCHAR2(4000)) R_DENOMINACION_CONCEPTO,
                CAST(NULL AS NUMBER) R_ASIGNACION,
                CAST(NULL AS NUMBER) R_DEDUCCION,
                CAST(NULL AS NUMBER) R_MONTO_VISIBLE,
                CAST(NULL AS NUMBER) R_MONTO,
                CAST(NULL AS NUMBER) R_DEDUCIBLE
            FROM DUAL
            WHERE 1 = 0;
END SP_REP_GRAL_NOMINA_GET_ALL;
/
