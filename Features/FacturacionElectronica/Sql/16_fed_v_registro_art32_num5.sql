-- =============================================================================
-- Facturacion Electronica (FED) - Requerimiento 32 - Fase 5
--
-- COMPLETA EL NUMERAL 5 DEL ART. 32. Es una deuda de la Fase 4, no un cambio de
-- alcance de la Fase 5.
--
-- 06_fed_v_registro_art32.sql:53 dice literalmente:
--
--     -- Numeral 5. La Fase 4 lo reemplaza por la numeracion de FED_DOCUMENTO.
--     CAST(NULL AS VARCHAR)  AS NUMERACION_FORMATO,
--
-- y la decision D-17 lo habia previsto asi: la vista se crea en la Fase 3 sobre
-- FED_NUM_CONTROL y FED_EMISOR, y la Fase 4 la reemplaza con CREATE OR REPLACE
-- VIEW para sumar los datos del documento. LA FASE 4 NO LO HIZO. El numeral
-- quedo en NULL, y ese es el registro que se le remite al SENIAT cada mes por el
-- Art. 29.7.
--
-- Se arregla aca y no reabriendo la Fase 4 por una razon concreta: cada nota de
-- debito o de credito consume su propio numero de control, asi que la Fase 5
-- multiplica las filas del registro. Postergarlo multiplica el problema.
--
-- POR QUE UN SCRIPT NUEVO Y NO EDITAR EL 06. Los scripts se numeran por orden de
-- ejecucion y son reejecutables; volver atras a editar el de una fase cerrada
-- rompe las dos cosas. Es el mismo patron que el modulo ya uso dos veces:
-- 08_fed_emisor_numeracion.sql agrega columna a una tabla creada en el 01, y
-- 09_fed_documento.sql agrega una a otra creada en el 03.
--
-- LO QUE CAMBIA: un LEFT JOIN a FED_DOCUMENTO. LEFT y no INNER, porque asignar
-- un numero de control sin documento es valido (D-16, Arts. 7.5 y 7.15): esas
-- filas siguen apareciendo con el numeral 5 vacio, y esta bien que aparezcan,
-- porque su ESTADO_CONCILIACION ya dice que estan sin documento. Lo que no
-- estaba bien era que apareciera vacio TAMBIEN cuando el documento existe.
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

    -- Numeral 5, ya no NULL. La numeracion propia del documento (Art. 7.2), con
    -- su serie cuando la tiene, que es la forma en que se imprime.
    -- El CAST a VARCHAR no es cosmetico: CREATE OR REPLACE VIEW no puede cambiar
    -- el tipo de una columna existente, y el 06 la habia declarado como
    -- CAST(NULL AS VARCHAR). Sin el cast, el reemplazo falla.
    CAST(
        CASE
            WHEN d.ID IS NULL               THEN NULL
            WHEN COALESCE(d.SERIE, '') = '' THEN d.NUMERACION
            ELSE d.SERIE || '-' || d.NUMERACION
        END AS VARCHAR
    )                                                            AS NUMERACION_FORMATO,

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
JOIN FED.FED_EMISOR e ON e.ID = nc.EMISOR_ID
LEFT JOIN FED.FED_DOCUMENTO d ON d.ID = nc.DOCUMENTO_ID;

COMMENT ON VIEW FED.FED_V_REGISTRO_ART32 IS 'Registro automatizado del Art. 32 de la Providencia SNAT/2024/000102, con los siete numerales completos. Ver D-7, D-17 y T5.7.';

-- =============================================================================
-- SEGUNDO DEFECTO DE LA MISMA FAMILIA, encontrado al verificar el primero.
--
-- ESTADO_CONCILIACION tiene DEFAULT 'sin_documento' y el INSERT del numero de
-- control NUNCA lo seteaba, aunque si pasaba el DOCUMENTO_ID. Resultado: toda
-- asignacion hecha CON documento quedo registrada como si no lo tuviera, y ese
-- es el registro que se le remite al SENIAT por el Art. 29.7.
--
-- El INSERT ya quedo corregido para calcularlo desde DOCUMENTO_ID. Esto arrastra
-- las filas que se escribieron antes. Es idempotente: la segunda corrida no
-- encuentra nada que cambiar.
--
-- Corre como propietario -fed-, no como la aplicacion: fed_app no tiene UPDATE
-- sobre esta columna y no debe tenerlo. Reconciliar es una operacion de
-- mantenimiento, no algo que la aplicacion haga sola.
-- =============================================================================

DO $$
DECLARE
    corregidas INTEGER;
BEGIN
    UPDATE FED.FED_NUM_CONTROL
       SET ESTADO_CONCILIACION = 'conciliado'
     WHERE DOCUMENTO_ID IS NOT NULL
       AND ESTADO_CONCILIACION <> 'conciliado';

    GET DIAGNOSTICS corregidas = ROW_COUNT;

    IF corregidas > 0 THEN
        RAISE NOTICE 'ESTADO_CONCILIACION: % fila(s) decian sin_documento teniendo documento. Corregidas.', corregidas;
    END IF;
END $$;
