CREATE OR REPLACE PROCEDURE RH.SP_RH_HISTORICO_MOV_MASIVO_GET (
    p_desde               IN DATE,
    p_hasta               IN DATE,
    p_codigo_persona      IN NUMBER,
    p_codigos_tipo_nomina IN VARCHAR2,
    p_codigos_concepto    IN VARCHAR2,
    p_page                IN NUMBER,
    p_page_size           IN NUMBER,
    p_ResultSet           OUT SYS_REFCURSOR,
    p_Message             OUT VARCHAR2,
    p_TotalRecords        OUT NUMBER
) AS
    v_start_row NUMBER := 1;
    v_end_row   NUMBER := 100;
    v_page_size NUMBER := 100;
BEGIN
    p_Message := 'Success';
    v_page_size := NVL(p_page_size, 100);

    IF v_page_size <= 0 THEN
        v_start_row := 1;
        v_end_row := 999999999;
    ELSE
        v_start_row := (NVL(p_page, 0) * v_page_size) + 1;
        v_end_row := v_start_row + v_page_size - 1;
    END IF;

    SELECT COUNT(1)
      INTO p_TotalRecords
      FROM RH.RH_V_HISTORICO_MOVIMIENTOS h
     WHERE h.FECHA_NOMINA_MOV >= p_desde
       AND h.FECHA_NOMINA_MOV < p_hasta + 1
       AND (NVL(p_codigo_persona, 0) = 0 OR h.CODIGO_PERSONA = p_codigo_persona)
       AND (
           p_codigos_tipo_nomina IS NULL
           OR p_codigos_tipo_nomina = ''
           OR INSTR(',' || p_codigos_tipo_nomina || ',', ',' || h.CODIGO_TIPO_NOMINA || ',') > 0
       )
       AND (
           p_codigos_concepto IS NULL
           OR p_codigos_concepto = ''
           OR INSTR(',' || p_codigos_concepto || ',', ',' || h.CODIGO_CONCEPTO || ',') > 0
       );

    OPEN p_ResultSet FOR
        SELECT *
        FROM (
            SELECT base_data.*, ROWNUM AS RN
            FROM (
                SELECT
                    ROW_NUMBER() OVER (
                        ORDER BY h.FECHA_NOMINA_MOV DESC, h.CODIGO_PERSONA, h.CODIGO_TIPO_NOMINA, h.CODIGO_CONCEPTO
                    ) AS CODIGO_HISTORICO_NOMINA,
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
                WHERE h.FECHA_NOMINA_MOV >= p_desde
                  AND h.FECHA_NOMINA_MOV < p_hasta + 1
                  AND (NVL(p_codigo_persona, 0) = 0 OR h.CODIGO_PERSONA = p_codigo_persona)
                  AND (
                      p_codigos_tipo_nomina IS NULL
                      OR p_codigos_tipo_nomina = ''
                      OR INSTR(',' || p_codigos_tipo_nomina || ',', ',' || h.CODIGO_TIPO_NOMINA || ',') > 0
                  )
                  AND (
                      p_codigos_concepto IS NULL
                      OR p_codigos_concepto = ''
                      OR INSTR(',' || p_codigos_concepto || ',', ',' || h.CODIGO_CONCEPTO || ',') > 0
                  )
                ORDER BY h.FECHA_NOMINA_MOV DESC, h.CODIGO_PERSONA, h.CODIGO_TIPO_NOMINA, h.CODIGO_CONCEPTO
            ) base_data
            WHERE ROWNUM <= v_end_row
        )
        WHERE RN >= v_start_row;
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SQLERRM;
        p_TotalRecords := 0;
        OPEN p_ResultSet FOR SELECT * FROM RH.RH_V_HISTORICO_MOVIMIENTOS WHERE 1 = 0;
END;
/
