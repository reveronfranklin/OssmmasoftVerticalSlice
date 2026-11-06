CREATE OR REPLACE PROCEDURE RH.SP_RH_HISTORICO_MOV_MASIVO_GET (
    p_desde IN DATE, p_hasta IN DATE, p_codigo_persona IN NUMBER,
    p_codigos_tipo_nomina IN VARCHAR2, p_codigos_concepto IN VARCHAR2,
    p_page IN NUMBER, p_page_size IN NUMBER,
    p_ResultSet OUT SYS_REFCURSOR, p_Message OUT VARCHAR2, p_TotalRecords OUT NUMBER,
    p_empresa IN NUMBER, p_sort_column IN VARCHAR2, p_sort IN VARCHAR2,
    p_search IN VARCHAR2, p_limite_export IN NUMBER
) AS
    v_start NUMBER;
    v_end NUMBER;
BEGIN
    p_Message := 'success';
    OPEN p_ResultSet FOR SELECT * FROM RH.RH_V_HISTORICO_MOVIMIENTOS WHERE 1 = 0;
    IF p_empresa IS NULL OR p_empresa <= 0 THEN
        p_Message := 'Empresa invalida'; p_TotalRecords := 0; RETURN;
    END IF;
    SELECT COUNT(*) INTO p_TotalRecords
      FROM RH.RH_V_HISTORICO_MOVIMIENTOS h
     WHERE h.CODIGO_EMPRESA = p_empresa
           AND h.FECHA_NOMINA_MOV >= TRUNC(p_desde)
           AND h.FECHA_NOMINA_MOV < TRUNC(p_hasta) + 1
           AND (NVL(p_codigo_persona, 0) = 0 OR h.CODIGO_PERSONA = p_codigo_persona)
           AND (p_codigos_tipo_nomina IS NULL OR
                INSTR(',' || p_codigos_tipo_nomina || ',', ',' || h.CODIGO_TIPO_NOMINA || ',') > 0)
           AND (p_codigos_concepto IS NULL OR
                INSTR(',' || p_codigos_concepto || ',', ',' || h.CODIGO_CONCEPTO || ',') > 0)
           AND (TRIM(p_search) IS NULL OR INSTR(LOWER(
                h.NOMBRE || ' ' || h.APELLIDO || ' ' || h.CEDULA || ' ' ||
                h.TIPO_NOMINA || ' ' || h.CODIGO || ' ' || h.DENOMINACION || ' ' ||
                h.COMPLEMENTO), LOWER(TRIM(p_search))) > 0);
    IF NVL(p_page_size, 0) <= 0 THEN
        IF p_TotalRecords > LEAST(NVL(p_limite_export, 20000), 100000) THEN
            p_Message := 'Exportacion supera el limite permitido. Reduzca el rango o los filtros';
            RETURN;
        END IF;
        v_start := 1; v_end := p_TotalRecords;
    ELSE
        v_start := GREATEST(NVL(p_page, 0), 0) * LEAST(p_page_size, 500) + 1;
        v_end := v_start + LEAST(p_page_size, 500) - 1;
    END IF;
    CLOSE p_ResultSet;
    OPEN p_ResultSet FOR
        SELECT * FROM (
            SELECT ROW_NUMBER() OVER (ORDER BY
                    CASE WHEN p_sort_column = 'fechanominamov' AND p_sort = 'asc' THEN h.FECHA_NOMINA_MOV END ASC NULLS LAST,
                    CASE WHEN p_sort_column = 'fechanominamov' AND p_sort = 'desc' THEN h.FECHA_NOMINA_MOV END DESC NULLS LAST,
                    CASE WHEN p_sort_column = 'fechanomina' AND p_sort = 'asc' THEN h.FECHA_NOMINA END ASC NULLS LAST,
                    CASE WHEN p_sort_column = 'fechanomina' AND p_sort = 'desc' THEN h.FECHA_NOMINA END DESC NULLS LAST,
                    CASE WHEN p_sort_column = 'fullname' AND p_sort = 'asc' THEN h.NOMBRE || ' ' || h.APELLIDO END ASC NULLS LAST,
                    CASE WHEN p_sort_column = 'fullname' AND p_sort = 'desc' THEN h.NOMBRE || ' ' || h.APELLIDO END DESC NULLS LAST,
                    CASE WHEN p_sort_column = 'tiponomina' AND p_sort = 'asc' THEN h.TIPO_NOMINA END ASC NULLS LAST,
                    CASE WHEN p_sort_column = 'tiponomina' AND p_sort = 'desc' THEN h.TIPO_NOMINA END DESC NULLS LAST,
                    CASE WHEN p_sort_column = 'sueldo' AND p_sort = 'asc' THEN h.SUELDO END ASC NULLS LAST,
                    CASE WHEN p_sort_column = 'sueldo' AND p_sort = 'desc' THEN h.SUELDO END DESC NULLS LAST,
                    CASE WHEN p_sort_column = 'monto' AND p_sort = 'asc' THEN h.MONTO END ASC NULLS LAST,
                    CASE WHEN p_sort_column = 'monto' AND p_sort = 'desc' THEN h.MONTO END DESC NULLS LAST,
                    CASE WHEN p_sort_column = 'codigo' AND p_sort = 'asc' THEN h.CODIGO END ASC NULLS LAST,
                    CASE WHEN p_sort_column = 'codigo' AND p_sort = 'desc' THEN h.CODIGO END DESC NULLS LAST,
                    CASE WHEN p_sort_column = 'denominacion' AND p_sort = 'asc' THEN h.DENOMINACION END ASC NULLS LAST,
                    CASE WHEN p_sort_column = 'denominacion' AND p_sort = 'desc' THEN h.DENOMINACION END DESC NULLS LAST,
                    CASE WHEN p_sort_column = 'unidadejecutora' AND p_sort = 'asc' THEN h.CODIGO_SECTOR || '-' || h.CODIGO_PROGRAMA END ASC NULLS LAST,
                    CASE WHEN p_sort_column = 'unidadejecutora' AND p_sort = 'desc' THEN h.CODIGO_SECTOR || '-' || h.CODIGO_PROGRAMA END DESC NULLS LAST,
                    CASE WHEN p_sort_column = 'cedula' AND p_sort = 'asc' THEN h.CEDULA END ASC NULLS LAST,
                    CASE WHEN p_sort_column = 'cedula' AND p_sort = 'desc' THEN h.CEDULA END DESC NULLS LAST,
                    h.FECHA_NOMINA_MOV DESC, h.CODIGO_PERSONA, h.CODIGO_TIPO_NOMINA,
                    h.CODIGO_CONCEPTO, h.CODIGO_PERIODO, h.CODIGO_RELACION_CARGO,
                    h.COMPLEMENTO, h.TIPO, h.MONTO) AS CODIGO_HISTORICO_NOMINA,
                    h.CODIGO_PERSONA,
                    h.CEDULA,
                    h.FOTO,
                    h.NOMBRE,
                    h.APELLIDO,
                    h.NOMBRE || ' ' || h.APELLIDO AS FULL_NAME,
                    h.NACIONALIDAD,
                    h.DESCRIPCION_NACIONALIDAD,
                    h.SEXO,
                    h.STATUS,
                    h.DESCRIPCION_STATUS,
                    h.DESCRIPCION_SEXO,
                    h.CODIGO_RELACION_CARGO,
                    h.CODIGO_CARGO,
                    h.CARGO_CODIGO,
                    h.CODIGO_ICP,
                    h.CODIGO_ICP_UBICACION,
                    h.SUELDO,
                    h.DESCRIPCION_CARGO,
                    h.CODIGO_TIPO_NOMINA,
                    h.TIPO_NOMINA,
                    h.FRECUENCIA_PAGO_ID,
                    '' AS DESCRIPCION_FRECUENCIA_PAGO,
                    h.CODIGO_SECTOR,
                    h.CODIGO_PROGRAMA,
                    h.TIPO_CUENTA_ID,
                    h.DESCRIPCION_TIPO_CUENTA,
                    h.BANCO_ID,
                    h.DESCRIPCION_BANCO,
                    h.NO_CUENTA,
                    h.EXTRA1,
                    h.EXTRA2,
                    h.EXTRA3,
                    h.CODIGO_PERIODO,
                    h.FECHA_NOMINA,
                    h.FECHA_INGRESO,
                    h.FECHA_NOMINA_MOV,
                    h.COMPLEMENTO,
                    h.TIPO,
                    h.MONTO,
                    h.ESTATUS_MOV AS STATUS_MOV,
                    h.CODIGO,
                    h.DENOMINACION,
                    h.CODIGO_CONCEPTO,
                    NVL(h.FOTO, '') AS AVATAR,
                    h.CODIGO_SECTOR || '-' || h.CODIGO_PROGRAMA AS UNIDAD_EJECUTORA,
                    '' AS ESTADO_CIVIL,
                    LOWER(
                        h.NOMBRE || ' ' ||
                        h.APELLIDO || ' ' ||
                        h.CEDULA || ' ' ||
                        h.TIPO_NOMINA || ' ' ||
                        h.CODIGO || ' ' ||
                        h.DENOMINACION
                    ) AS SEARCH_TEXT
              FROM RH.RH_V_HISTORICO_MOVIMIENTOS h
             WHERE h.CODIGO_EMPRESA = p_empresa
           AND h.FECHA_NOMINA_MOV >= TRUNC(p_desde)
           AND h.FECHA_NOMINA_MOV < TRUNC(p_hasta) + 1
           AND (NVL(p_codigo_persona, 0) = 0 OR h.CODIGO_PERSONA = p_codigo_persona)
           AND (p_codigos_tipo_nomina IS NULL OR
                INSTR(',' || p_codigos_tipo_nomina || ',', ',' || h.CODIGO_TIPO_NOMINA || ',') > 0)
           AND (p_codigos_concepto IS NULL OR
                INSTR(',' || p_codigos_concepto || ',', ',' || h.CODIGO_CONCEPTO || ',') > 0)
           AND (TRIM(p_search) IS NULL OR INSTR(LOWER(
                h.NOMBRE || ' ' || h.APELLIDO || ' ' || h.CEDULA || ' ' ||
                h.TIPO_NOMINA || ' ' || h.CODIGO || ' ' || h.DENOMINACION || ' ' ||
                h.COMPLEMENTO), LOWER(TRIM(p_search))) > 0)
        )
        WHERE CODIGO_HISTORICO_NOMINA BETWEEN v_start AND v_end
        ORDER BY CODIGO_HISTORICO_NOMINA;
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SQLERRM;
        p_TotalRecords := 0;
        IF p_ResultSet%ISOPEN THEN CLOSE p_ResultSet; END IF;
        OPEN p_ResultSet FOR SELECT * FROM RH.RH_V_HISTORICO_MOVIMIENTOS WHERE 1 = 0;
END;
/
