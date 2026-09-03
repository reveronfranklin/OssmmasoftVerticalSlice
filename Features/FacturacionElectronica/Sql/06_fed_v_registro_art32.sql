-- =============================================================================
-- Facturacion Electronica (FED) - Requerimiento 32 - Fase 3
--
-- Vista FED_V_REGISTRO_ART32: el registro automatizado del Articulo 32.
--
-- POR QUE UNA VISTA Y NO UNA TABLA (decision D-7). El Art. 32 obliga a "llevar un
-- registro automatizado", y una vista lo es. Lo que la norma no admite es que ese
-- registro DIFIERA de lo asignado, y esa es exactamente la causal del Art. 34.3.
-- Una tabla aparte obligaria a escribir el mismo hecho dos veces y abriria la
-- posibilidad de que las dos copias se separen. Una vista no puede diferir de su
-- origen: no hay dos copias.
--
-- POR QUE NO USA FED_DOCUMENTO (decision D-17). Esa tabla nace en la Fase 4. El
-- plan dice que ninguna fase depende de una posterior, asi que la vista nace aqui
-- con lo que existe y la Fase 4 la reemplaza con CREATE OR REPLACE VIEW para
-- sumarle la numeracion del documento. Hasta entonces el numeral 5 viaja en NULL,
-- lo cual es honesto: todavia no hay formato que numerar.
--
-- Los siete numerales del Art. 32, en orden, y de donde sale cada uno:
--
--   1. RIF de los emisores               -> FED_EMISOR.RIF, por join
--   2. Fecha de asignacion               -> FED_NUM_CONTROL.FECHA_ASIGNACION
--   3. Tipo de documento                 -> FED_NUM_CONTROL.TIPO_DOCUMENTO
--   4. Numeracion de control asignada    -> IDENTIFICADOR + SECUENCIAL
--   5. Numeracion de cada formato        -> NULL hasta la Fase 4
--   6. Identificacion de la factura      -> las DOS lecturas, ver D-15
--   7. Cualquier otra informacion        -> DATOS_ADICIONALES jsonb
-- =============================================================================

CREATE OR REPLACE VIEW FED.FED_V_REGISTRO_ART32 AS
SELECT
    nc.ID                                                        AS NUM_CONTROL_ID,

    -- Numeral 1
    e.RIF                                                        AS EMISOR_RIF,
    e.RAZON_SOCIAL                                               AS EMISOR_RAZON_SOCIAL,

    -- Numeral 2. El Art. 7.15 lo exige en ocho digitos sobre el documento; aqui
    -- se conserva el instante completo y el formateo queda para quien presente.
    nc.FECHA_ASIGNACION                                          AS FECHA_ASIGNACION,
    TO_CHAR(nc.FECHA_ASIGNACION, 'DDMMYYYY')                     AS FECHA_ASIGNACION_8D,

    -- Numeral 3
    nc.TIPO_DOCUMENTO                                            AS TIPO_DOCUMENTO,

    -- Numeral 4. El formato del Art. 30 se arma una sola vez, aqui, para que el
    -- reporte y la pantalla no puedan discrepar.
    nc.IDENTIFICADOR || '-' || LPAD(nc.SECUENCIAL::text, 8, '0') AS NUMERO_CONTROL,
    nc.IDENTIFICADOR                                             AS IDENTIFICADOR,
    nc.SECUENCIAL                                                AS SECUENCIAL,

    -- Numeral 5. La Fase 4 lo reemplaza por la numeracion de FED_DOCUMENTO.
    CAST(NULL AS VARCHAR)                                        AS NUMERACION_FORMATO,

    -- Numeral 6, las dos lecturas (D-15).
    nc.DOCUMENTO_ID                                              AS DOCUMENTO_ID,
    nc.FACTURA_SERVICIO                                          AS FACTURA_SERVICIO,
    nc.ESTADO_CONCILIACION                                       AS ESTADO_CONCILIACION,

    -- Numeral 7
    nc.DATOS_ADICIONALES                                         AS DATOS_ADICIONALES,

    -- Fuera del articulo, pero necesario para el reporte mensual del Art. 29.7.
    nc.REPORTE_ID                                                AS REPORTE_ID,
    TO_CHAR(nc.FECHA_ASIGNACION, 'YYYYMM')                       AS PERIODO
FROM FED.FED_NUM_CONTROL nc
JOIN FED.FED_EMISOR e ON e.ID = nc.EMISOR_ID;

COMMENT ON VIEW FED.FED_V_REGISTRO_ART32 IS 'Registro automatizado del Art. 32 de la Providencia SNAT/2024/000102. Es una vista a proposito: no puede diferir de lo asignado. Ver D-7 y D-17.';
