CREATE OR REPLACE PROCEDURE RH.SP_REP_GRAL_NOM_DET_GET_ALL (
    p_from_table1    IN VARCHAR2,
    p_from_table2    IN VARCHAR2,
    p_tipo_nomina    IN NUMBER,
    p_codigo_empresa IN NUMBER,
    p_fecha_pago     IN DATE,
    p_where          IN VARCHAR2,
    p_cedula         IN VARCHAR2,
    p_ResultSet      OUT SYS_REFCURSOR,
    p_Message        OUT VARCHAR2,
    p_TotalRecords   OUT NUMBER
) AS
    v_sql            VARCHAR2(32767);
    v_count_sql      VARCHAR2(32767);
    v_query          VARCHAR2(32767);
    v_from           VARCHAR2(32767);
    v_where_extra    VARCHAR2(32767);
    v_cedula_extra   VARCHAR2(32767);
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

    FUNCTION is_invalid_condition_fragment(p_value IN VARCHAR2) RETURN BOOLEAN IS
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
                CAST(NULL AS DATE) FECHA_PERIODO_NOMINA,
                CAST(NULL AS DATE) FECHA_EMISION_NOMINA,
                CAST(NULL AS NUMBER) CODIGO_PERIODO,
                CAST(NULL AS NUMBER) CODIGO_TIPO_NOMINA,
                CAST(NULL AS VARCHAR2(100)) CODIGO_OFICINA,
                CAST(NULL AS NUMBER) CODIGO_ICP,
                CAST(NULL AS VARCHAR2(4000)) DENOMINACION,
                CAST(NULL AS VARCHAR2(4000)) DENOMINACION_CARGO,
                CAST(NULL AS VARCHAR2(100)) CEDULA,
                CAST(NULL AS VARCHAR2(4000)) NOMBRE,
                CAST(NULL AS VARCHAR2(100)) NO_CUENTA,
                CAST(NULL AS VARCHAR2(100)) NUMERO_CONCEPTO,
                CAST(NULL AS VARCHAR2(100)) TIPO_MOV_CONCEPTO,
                CAST(NULL AS VARCHAR2(4000)) DENOMINACION_CONCEPTO,
                CAST(NULL AS VARCHAR2(4000)) COMPLEMENTO_CONCEPTO,
                CAST(NULL AS NUMBER) PORCENTAJE,
                CAST(NULL AS VARCHAR2(100)) TIPO_CONCEPTO,
                CAST(NULL AS NUMBER) MONTO,
                CAST(NULL AS NUMBER) ASIGNACION,
                CAST(NULL AS NUMBER) DEDUCCION,
                CAST(NULL AS VARCHAR2(100)) STATUS,
                CAST(NULL AS VARCHAR2(4000)) DESCRIPCION_STATUS,
                CAST(NULL AS NUMBER) ACTIVOS,
                CAST(NULL AS NUMBER) PERMISOS,
                CAST(NULL AS NUMBER) VACACIONES,
                CAST(NULL AS NUMBER) REPOSOS,
                CAST(NULL AS NUMBER) CODIGO_PERSONA,
                CAST(NULL AS DATE) FECHA_INGRESO,
                CAST(NULL AS VARCHAR2(100)) CARGO_CODIGO,
                CAST(NULL AS VARCHAR2(4000)) BANCO,
                CAST(NULL AS NUMBER) CODIGO_CONCEPTO,
                CAST(NULL AS VARCHAR2(4000)) MODULO,
                CAST(NULL AS VARCHAR2(4000)) CODIGO_IDENTIFICADOR
            FROM DUAL
            WHERE 1 = 0;
        RETURN;
    END IF;

    IF is_invalid_condition_fragment(p_where) OR is_invalid_condition_fragment(p_cedula) THEN
        p_Message := 'Invalid filter parameter';
        OPEN p_ResultSet FOR
            SELECT
                CAST(NULL AS DATE) FECHA_PERIODO_NOMINA,
                CAST(NULL AS DATE) FECHA_EMISION_NOMINA,
                CAST(NULL AS NUMBER) CODIGO_PERIODO,
                CAST(NULL AS NUMBER) CODIGO_TIPO_NOMINA,
                CAST(NULL AS VARCHAR2(100)) CODIGO_OFICINA,
                CAST(NULL AS NUMBER) CODIGO_ICP,
                CAST(NULL AS VARCHAR2(4000)) DENOMINACION,
                CAST(NULL AS VARCHAR2(4000)) DENOMINACION_CARGO,
                CAST(NULL AS VARCHAR2(100)) CEDULA,
                CAST(NULL AS VARCHAR2(4000)) NOMBRE,
                CAST(NULL AS VARCHAR2(100)) NO_CUENTA,
                CAST(NULL AS VARCHAR2(100)) NUMERO_CONCEPTO,
                CAST(NULL AS VARCHAR2(100)) TIPO_MOV_CONCEPTO,
                CAST(NULL AS VARCHAR2(4000)) DENOMINACION_CONCEPTO,
                CAST(NULL AS VARCHAR2(4000)) COMPLEMENTO_CONCEPTO,
                CAST(NULL AS NUMBER) PORCENTAJE,
                CAST(NULL AS VARCHAR2(100)) TIPO_CONCEPTO,
                CAST(NULL AS NUMBER) MONTO,
                CAST(NULL AS NUMBER) ASIGNACION,
                CAST(NULL AS NUMBER) DEDUCCION,
                CAST(NULL AS VARCHAR2(100)) STATUS,
                CAST(NULL AS VARCHAR2(4000)) DESCRIPCION_STATUS,
                CAST(NULL AS NUMBER) ACTIVOS,
                CAST(NULL AS NUMBER) PERMISOS,
                CAST(NULL AS NUMBER) VACACIONES,
                CAST(NULL AS NUMBER) REPOSOS,
                CAST(NULL AS NUMBER) CODIGO_PERSONA,
                CAST(NULL AS DATE) FECHA_INGRESO,
                CAST(NULL AS VARCHAR2(100)) CARGO_CODIGO,
                CAST(NULL AS VARCHAR2(4000)) BANCO,
                CAST(NULL AS NUMBER) CODIGO_CONCEPTO,
                CAST(NULL AS VARCHAR2(4000)) MODULO,
                CAST(NULL AS VARCHAR2(4000)) CODIGO_IDENTIFICADOR
            FROM DUAL
            WHERE 1 = 0;
        RETURN;
    END IF;

    v_from_table1_up := TRIM(p_from_table1);
    v_from_table2_up := TRIM(p_from_table2);
    v_where_extra := '';
    v_cedula_extra := '';

    IF p_where IS NOT NULL AND TRIM(p_where) IS NOT NULL THEN
        v_where_extra := ' AND (' || p_where || ') ';
    END IF;

    IF p_cedula IS NOT NULL AND TRIM(p_cedula) IS NOT NULL THEN
        v_cedula_extra := ' AND (' || p_cedula || ') ';
    END IF;

    v_from := ' FROM ' ||
              v_from_table1_up || ', ' ||
              v_from_table2_up || ', ' ||
              'RH.RH_CONCEPTOS RC, ' ||
              'PRE.PRE_INDICE_CAT_PRG PVIO ';

    v_query :=
        'SELECT UNIQUE
             RTN.FECHA_NOMINA FECHA_PERIODO_NOMINA,
             :b_fecha_emision FECHA_EMISION_NOMINA,
             RTN.CODIGO_PERIODO,
             RTN.CODIGO_TIPO_NOMINA,
             PVIO.CODIGO_SECTOR || ''-'' ||
             PVIO.CODIGO_PROGRAMA || ''-'' ||
             PVIO.CODIGO_SUBPROGRAMA || ''-'' ||
             PVIO.CODIGO_PROYECTO || ''-'' ||
             PVIO.CODIGO_ACTIVIDAD ||
             DECODE(PVIO.CODIGO_OFICINA, ''00'', NULL, ''-'' || PVIO.CODIGO_OFICINA) CODIGO_OFICINA,
             PVIO.CODIGO_ICP,
             PVIO.DENOMINACION,
             RVPC.DESCRIPCION_CARGO DENOMINACION_CARGO,
             RVPC.CEDULA,
             RVPC.APELLIDO || '' '' || RVPC.NOMBRE NOMBRE,
             RVPC.NO_CUENTA,
             RC.CODIGO NUMERO_CONCEPTO,
             DECODE(RTN.TIPO, ''F'', ''FIJO'', ''V'', ''VARIABLE'', ''E'', ''ESPECIAL'', ''P'', ''PROGRAMADO'') TIPO_MOV_CONCEPTO,
             RC.DENOMINACION DENOMINACION_CONCEPTO,
             RTN.COMPLEMENTO_CONCEPTO,
             NULL PORCENTAJE,
             RC.TIPO_CONCEPTO,
             SIS_RECONVERTIR_OLD(''DUMMY'', RTN.FECHA_NOMINA, RTN.MONTO) MONTO,
             SIS_RECONVERTIR_OLD(''DUMMY'', RTN.FECHA_NOMINA, DECODE(SIGN(RTN.MONTO), 1, RTN.MONTO, 0)) ASIGNACION,
             SIS_RECONVERTIR_OLD(''DUMMY'', RTN.FECHA_NOMINA, DECODE(SIGN(RTN.MONTO), -1, -(RTN.MONTO), 0)) DEDUCCION,
             RVPC.STATUS,
             RVPC.DESCRIPCION_STATUS,
             DECODE(RVPC.STATUS, ''A'', 1, 0) ACTIVOS,
             DECODE(RVPC.STATUS, ''P'', 1, 0) PERMISOS,
             DECODE(RVPC.STATUS, ''V'', 1, 0) VACACIONES,
             DECODE(RVPC.STATUS, ''R'', 1, 0) REPOSOS,
             RVPC.CODIGO_PERSONA,
             RVPC.FECHA_INGRESO,
             RVPC.CARGO_CODIGO,
             REPLACE(RVPC.DESCRIPCION_BANCO, ''BANCO '', '''') BANCO,
             RTN.CODIGO_CONCEPTO,
             RTN.EXTRA1 MODULO,
             RTN.EXTRA2 CODIGO_IDENTIFICADOR ' ||
        v_from ||
        'WHERE RVPC.CODIGO_TIPO_NOMINA = :b_tipo_nomina
             AND RVPC.CODIGO_TIPO_NOMINA = RTN.CODIGO_TIPO_NOMINA
             AND RVPC.CODIGO_EMPRESA = :b_codigo_empresa
             AND RTN.CODIGO_PERSONA = RVPC.CODIGO_PERSONA
             AND RTN.FECHA_NOMINA = :b_fecha_pago
             AND RC.CODIGO_CONCEPTO = RTN.CODIGO_CONCEPTO
             AND PVIO.CODIGO_ICP = RVPC.CODIGO_ICP ' ||
             v_where_extra ||
             v_cedula_extra;

    v_count_sql := 'SELECT COUNT(*) FROM (' || v_query || ')';

    EXECUTE IMMEDIATE v_count_sql
        INTO p_TotalRecords
        USING p_fecha_pago,
              p_tipo_nomina,
              p_codigo_empresa,
              p_fecha_pago;

    v_sql := v_query || ' ORDER BY 5, 9, 17, 18 DESC';

    OPEN p_ResultSet FOR v_sql
        USING p_fecha_pago,
              p_tipo_nomina,
              p_codigo_empresa,
              p_fecha_pago;

    p_Message := 'Success';

EXCEPTION
    WHEN OTHERS THEN
        p_Message := SUBSTR(SQLERRM, 1, 4000);
        p_TotalRecords := 0;
        OPEN p_ResultSet FOR
            SELECT
                CAST(NULL AS DATE) FECHA_PERIODO_NOMINA,
                CAST(NULL AS DATE) FECHA_EMISION_NOMINA,
                CAST(NULL AS NUMBER) CODIGO_PERIODO,
                CAST(NULL AS NUMBER) CODIGO_TIPO_NOMINA,
                CAST(NULL AS VARCHAR2(100)) CODIGO_OFICINA,
                CAST(NULL AS NUMBER) CODIGO_ICP,
                CAST(NULL AS VARCHAR2(4000)) DENOMINACION,
                CAST(NULL AS VARCHAR2(4000)) DENOMINACION_CARGO,
                CAST(NULL AS VARCHAR2(100)) CEDULA,
                CAST(NULL AS VARCHAR2(4000)) NOMBRE,
                CAST(NULL AS VARCHAR2(100)) NO_CUENTA,
                CAST(NULL AS VARCHAR2(100)) NUMERO_CONCEPTO,
                CAST(NULL AS VARCHAR2(100)) TIPO_MOV_CONCEPTO,
                CAST(NULL AS VARCHAR2(4000)) DENOMINACION_CONCEPTO,
                CAST(NULL AS VARCHAR2(4000)) COMPLEMENTO_CONCEPTO,
                CAST(NULL AS NUMBER) PORCENTAJE,
                CAST(NULL AS VARCHAR2(100)) TIPO_CONCEPTO,
                CAST(NULL AS NUMBER) MONTO,
                CAST(NULL AS NUMBER) ASIGNACION,
                CAST(NULL AS NUMBER) DEDUCCION,
                CAST(NULL AS VARCHAR2(100)) STATUS,
                CAST(NULL AS VARCHAR2(4000)) DESCRIPCION_STATUS,
                CAST(NULL AS NUMBER) ACTIVOS,
                CAST(NULL AS NUMBER) PERMISOS,
                CAST(NULL AS NUMBER) VACACIONES,
                CAST(NULL AS NUMBER) REPOSOS,
                CAST(NULL AS NUMBER) CODIGO_PERSONA,
                CAST(NULL AS DATE) FECHA_INGRESO,
                CAST(NULL AS VARCHAR2(100)) CARGO_CODIGO,
                CAST(NULL AS VARCHAR2(4000)) BANCO,
                CAST(NULL AS NUMBER) CODIGO_CONCEPTO,
                CAST(NULL AS VARCHAR2(4000)) MODULO,
                CAST(NULL AS VARCHAR2(4000)) CODIGO_IDENTIFICADOR
            FROM DUAL
            WHERE 1 = 0;
END SP_REP_GRAL_NOM_DET_GET_ALL;
/
