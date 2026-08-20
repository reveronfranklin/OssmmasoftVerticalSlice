-- =============================================================================
-- ADM - Relacion de Compromisos.
--
-- Migracion del reporte Oracle Reports ADM_RELACION_COMPROMISO.rdf
-- (requerimiento 25). Devuelve una fila por compromiso, ya neta de anulaciones,
-- uniendo las tres fuentes de compromiso del sistema. El total de cierre
-- -cantidad y monto- lo suma el generador de PDF en C#.
--
-- LAS TRES RAMAS DEL UNION SON LAS DEL REPORTE LEGADO:
--
--   Rama 1 - Compromisos administrativos: ADM_COMPROMISOS + ADM_PUC_COMPROMISO.
--   Rama 2 - Compromisos de presupuesto: PRE_COMPROMISOS +
--            PRE_DETALLE_COMPROMISOS + PRE_PUC_COMPROMISOS.
--   Rama 3 - Contratos: ADM_CONTRATOS + ADM_PUC_CONTRATO, con el numero
--            compuesto como 'CONTRATO ' || EXTRA1.
--
-- Es UNION y no UNION ALL, igual que el legado. No es cosmetico: si una misma
-- fila saliera de dos ramas, el legado la cuenta una vez y esto tambien.
--
-- Por eso la subconsulta proyecta CODIGO_COMPROMISO aunque la salida no lo use:
-- **el UNION tiene que deduplicar sobre las mismas columnas que el legado.** Sin
-- esa columna, dos compromisos distintos que coincidieran en numero, fecha,
-- proveedor y monto se colapsarian en una sola fila, y el reporte perderia una
-- linea y su importe del total sin avisar.
--
-- El monto de cada rama ya viene neto a nivel de partida presupuestaria
-- (SUM(MONTO - MONTO_ANULADO)) y agrupado por compromiso, asi que **cada
-- compromiso sale en una sola fila**. Se comprobo contra el PDF de muestra: 116
-- filas y 116 en el total impreso, sin numeros repetidos.
--
-- DIVERGENCIAS DELIBERADAS respecto del .rdf, con su razon. Estan explicadas en
-- detalle en Requerimientos/25 - RelaciondeCompromiso/PLAN.md:
--
--   1. **Sin SQL dinamico.** Los lexicos &LP_CODIGO_PROVEEDOR,
--      &LP_FECHA_COMPROMISO y &LP_FECHA_COMPROMISO_CONTRATO que armaba el
--      trigger AfterPForm se reemplazan por predicados con binds. El
--      comportamiento se conserva: fechas opcionales de forma independiente
--      (solo desde, solo hasta, ambas o ninguna) y proveedor opcional.
--   2. **Se filtra por CODIGO_EMPRESA.** El query legado no lo hacia: filtraba
--      solo por CODIGO_PRESUPUESTO. Las cinco tablas de cabecera tienen columna
--      de empresa, y en un schema multiempresa dos presupuestos distintos pueden
--      compartir codigo.
--   3. **El rango de fechas se evalua con TRUNC en las tres ramas.** El legado
--      comparaba A.FECHA_COMPROMISO directo en las ramas 1 y 2 -y perderia el
--      ultimo dia del rango si la columna llevara hora- y TRUNC(A.FECHA_INS)
--      solo en la de contratos. Aqui las tres usan el mismo criterio de dia
--      completo.
--
-- LO QUE NO SE MIGRA, y es deliberado:
--
--   * **La formula de deuda (CF_DEUDAFORMULA).** En el .rdf hace
--     `return (NULL);` con la resta real comentada, y no aparece en ninguna
--     columna del PDF de muestra. Las dos consultas auxiliares que la
--     alimentaban -monto pagado via orden de pago y via cheque- tampoco se
--     migran. Reactivarlo es una decision de negocio, no de migracion.
--   * **SIS_MEMBRETE.** El legado resolvia el membrete de empresa con esa
--     funcion, que arma SQL dinamico sobre SIS_PKG_GENERICS. El nombre de la
--     entidad se lee como ya lo hace ReporteBm1Esp, desde
--     PRE.PRE_IDENTIFICACIONES, y vive en el handler y no aqui.
--
-- OJO CON LA RAMA DE CONTRATOS. Reproduce dos rarezas del legado a proposito:
--
--   a. **No filtra STATUS <> 'AN'**, al contrario de las otras dos ramas. En el
--      .rdf hay un `--DECODE(A.STATUS,'AP','APROBADO','AN','ANULADO') STATUS`
--      comentado en la proyeccion, lo que sugiere que el status se mostraba y se
--      quito sin agregar el filtro. Un contrato anulado suma +1 al conteo pero
--      normalmente 0 al monto (su MONTO_ANULADO iguala su MONTO), asi que
--      **infla la cantidad de compromisos, no el importe**. Si se decide
--      excluirlos, es una linea: AND A.STATUS <> 'AN'.
--   b. **Filtra por TRUNC(A.FECHA_INS) pero muestra A.FECHA_CONTRATO.** Un
--      contrato cargado dentro del rango con fecha de contrato fuera de el
--      aparece con una fecha que no corresponde al periodo del titulo.
--
-- El PDF de muestra no trae ni una fila de contrato, asi que esta rama esta
-- migrada sin un solo caso observado.
-- =============================================================================
CREATE OR REPLACE PROCEDURE ADM.SP_REP_COMPROMISO_GET (
    p_CodigoEmpresa     IN  NUMBER,
    p_CodigoPresupuesto IN  NUMBER,
    p_FechaDesde        IN  DATE     DEFAULT NULL,
    p_FechaHasta        IN  DATE     DEFAULT NULL,
    p_CodigoProveedor   IN  NUMBER   DEFAULT NULL,
    p_ResultSet         OUT SYS_REFCURSOR,
    p_Message           OUT VARCHAR2,
    p_TotalRecords      OUT NUMBER
) AS
    v_desde DATE := TRUNC(p_FechaDesde);
    v_hasta DATE := TRUNC(p_FechaHasta);
BEGIN
    OPEN p_ResultSet FOR
        SELECT Q.CODIGO_COMPROMISO,
               Q.NUMERO_COMPROMISO,
               Q.FECHA_COMPROMISO,
               Q.NOMBRE_PROVEEDOR,
               Q.MONTO_COMPROMISO,
               Q.ORIGEN
          FROM (
                -- ------------------------------------------------------------
                -- Rama 1: compromisos administrativos
                -- ------------------------------------------------------------
                SELECT A.CODIGO_COMPROMISO                    CODIGO_COMPROMISO,
                       A.NUMERO_COMPROMISO                    NUMERO_COMPROMISO,
                       A.FECHA_COMPROMISO                     FECHA_COMPROMISO,
                       C.NOMBRE_PROVEEDOR                     NOMBRE_PROVEEDOR,
                       SUM(B.MONTO - B.MONTO_ANULADO)         MONTO_COMPROMISO,
                       'ADM'                                  ORIGEN
                  FROM ADM.ADM_COMPROMISOS A
                  JOIN ADM.ADM_PUC_COMPROMISO B
                    ON B.CODIGO_COMPROMISO = A.CODIGO_COMPROMISO
                  JOIN ADM.ADM_PROVEEDORES C
                    ON C.CODIGO_PROVEEDOR = A.CODIGO_PROVEEDOR
                 WHERE A.STATUS <> 'AN'
                   AND A.CODIGO_EMPRESA = p_CodigoEmpresa
                   AND A.CODIGO_PRESUPUESTO = p_CodigoPresupuesto
                   AND (p_CodigoProveedor IS NULL OR A.CODIGO_PROVEEDOR = p_CodigoProveedor)
                   AND (v_desde IS NULL OR TRUNC(A.FECHA_COMPROMISO) >= v_desde)
                   AND (v_hasta IS NULL OR TRUNC(A.FECHA_COMPROMISO) <= v_hasta)
                 GROUP BY A.CODIGO_COMPROMISO,
                          A.NUMERO_COMPROMISO,
                          A.FECHA_COMPROMISO,
                          C.NOMBRE_PROVEEDOR
                UNION
                -- ------------------------------------------------------------
                -- Rama 2: compromisos de presupuesto
                -- ------------------------------------------------------------
                SELECT A.CODIGO_COMPROMISO                    CODIGO_COMPROMISO,
                       A.NUMERO_COMPROMISO                    NUMERO_COMPROMISO,
                       A.FECHA_COMPROMISO                     FECHA_COMPROMISO,
                       C.NOMBRE_PROVEEDOR                     NOMBRE_PROVEEDOR,
                       SUM(B.MONTO - B.MONTO_ANULADO)         MONTO_COMPROMISO,
                       'PRE'                                  ORIGEN
                  FROM PRE.PRE_COMPROMISOS A
                  JOIN PRE.PRE_DETALLE_COMPROMISOS X
                    ON X.CODIGO_COMPROMISO = A.CODIGO_COMPROMISO
                  JOIN PRE.PRE_PUC_COMPROMISOS B
                    ON B.CODIGO_DETALLE_COMPROMISO = X.CODIGO_DETALLE_COMPROMISO
                  JOIN ADM.ADM_PROVEEDORES C
                    ON C.CODIGO_PROVEEDOR = A.CODIGO_PROVEEDOR
                 WHERE A.STATUS <> 'AN'
                   AND A.CODIGO_EMPRESA = p_CodigoEmpresa
                   AND A.CODIGO_PRESUPUESTO = p_CodigoPresupuesto
                   AND (p_CodigoProveedor IS NULL OR A.CODIGO_PROVEEDOR = p_CodigoProveedor)
                   AND (v_desde IS NULL OR TRUNC(A.FECHA_COMPROMISO) >= v_desde)
                   AND (v_hasta IS NULL OR TRUNC(A.FECHA_COMPROMISO) <= v_hasta)
                 GROUP BY A.CODIGO_COMPROMISO,
                          A.NUMERO_COMPROMISO,
                          A.FECHA_COMPROMISO,
                          C.NOMBRE_PROVEEDOR
                UNION
                -- ------------------------------------------------------------
                -- Rama 3: contratos. Ver el aviso del encabezado: sin filtro de
                -- status y filtrada por fecha de insercion, como el legado.
                -- ------------------------------------------------------------
                SELECT A.CODIGO_CONTRATO                      CODIGO_COMPROMISO,
                       'CONTRATO ' || A.EXTRA1                NUMERO_COMPROMISO,
                       A.FECHA_CONTRATO                       FECHA_COMPROMISO,
                       C.NOMBRE_PROVEEDOR                     NOMBRE_PROVEEDOR,
                       SUM(B.MONTO - B.MONTO_ANULADO)         MONTO_COMPROMISO,
                       'CONTRATO'                             ORIGEN
                  FROM ADM.ADM_CONTRATOS A
                  JOIN ADM.ADM_PUC_CONTRATO B
                    ON B.CODIGO_CONTRATO = A.CODIGO_CONTRATO
                  JOIN ADM.ADM_PROVEEDORES C
                    ON C.CODIGO_PROVEEDOR = A.CODIGO_PROVEEDOR
                 WHERE A.CODIGO_EMPRESA = p_CodigoEmpresa
                   AND A.CODIGO_PRESUPUESTO = p_CodigoPresupuesto
                   AND (p_CodigoProveedor IS NULL OR A.CODIGO_PROVEEDOR = p_CodigoProveedor)
                   AND (v_desde IS NULL OR TRUNC(A.FECHA_INS) >= v_desde)
                   AND (v_hasta IS NULL OR TRUNC(A.FECHA_INS) <= v_hasta)
                 GROUP BY A.CODIGO_CONTRATO,
                          A.EXTRA1,
                          A.FECHA_CONTRATO,
                          C.NOMBRE_PROVEEDOR
               ) Q
         -- El legado ordena por ORDER BY 3, 2 sobre su propia proyeccion, que es
         -- fecha y luego numero. Se conserva.
         ORDER BY Q.FECHA_COMPROMISO, Q.NUMERO_COMPROMISO;

    -- El conteo no se calcula aparte: obligaria a repetir las tres ramas del
    -- UNION -unas 60 lineas- para un numero que el C# obtiene contando las filas
    -- que ya recorre. Se deja en 0 y el handler informa el suyo.
    p_TotalRecords := 0;
    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SUBSTR(SQLERRM, 1, 4000);
        p_TotalRecords := 0;
        OPEN p_ResultSet FOR
            SELECT CAST(NULL AS NUMBER)         CODIGO_COMPROMISO,
                   CAST(NULL AS VARCHAR2(200))  NUMERO_COMPROMISO,
                   CAST(NULL AS DATE)           FECHA_COMPROMISO,
                   CAST(NULL AS VARCHAR2(4000)) NOMBRE_PROVEEDOR,
                   CAST(NULL AS NUMBER)         MONTO_COMPROMISO,
                   CAST(NULL AS VARCHAR2(10))   ORIGEN
              FROM DUAL
             WHERE 1 = 0;
END SP_REP_COMPROMISO_GET;
/
