-- Solo lectura. Ejecutar en la misma BD que DefaultConnectionBM.
-- Comparar CODIGO_EMPRESA con settings:EmpresaConfig del backend publicado.
-- Usa el ICP y las fechas de la incidencia; no modifica datos ni procedimientos.
-- Identificadores de salida: CNT_ICP (7), CNT_PERIODO (11),
-- CNT_VISIBLES (12), FECHA_MIN (9), FECHA_MAX (9).
WITH BASE AS (
    SELECT V.CODIGO_EMPRESA,
           V.CODIGO_ICP,
           V.UNIDAD_TRABAJO,
           V.FECHA_MOVIMIENTO,
           CASE WHEN EXISTS (
               SELECT 1
                 FROM BM.BM_PLACAS_CUARENTENA Q
                WHERE Q.CODIGO_EMPRESA = V.CODIGO_EMPRESA
                  AND Q.NUMERO_PLACA = V.NRO_PLACA
           ) THEN 1 ELSE 0 END EN_CUARENTENA
      FROM BM.BM_V_BM1 V
     WHERE V.CODIGO_ICP = 2283
)
SELECT CODIGO_EMPRESA,
       CODIGO_ICP,
       UNIDAD_TRABAJO,
       COUNT(*) CNT_ICP,
       MIN(FECHA_MOVIMIENTO) FECHA_MIN,
       MAX(FECHA_MOVIMIENTO) FECHA_MAX,
       SUM(CASE WHEN TRUNC(FECHA_MOVIMIENTO)
                    BETWEEN DATE '2010-01-01' AND DATE '2026-09-28'
                THEN 1 ELSE 0 END) CNT_PERIODO,
       SUM(CASE WHEN TRUNC(FECHA_MOVIMIENTO)
                    BETWEEN DATE '2010-01-01' AND DATE '2026-09-28'
                     AND EN_CUARENTENA = 0
                THEN 1 ELSE 0 END) CNT_VISIBLES
  FROM BASE
 GROUP BY CODIGO_EMPRESA, CODIGO_ICP, UNIDAD_TRABAJO
 ORDER BY CODIGO_EMPRESA, UNIDAD_TRABAJO;

-- Confirmar la version del procedimiento que realmente esta instalada.
SELECT LINE, TEXT
  FROM ALL_SOURCE
 WHERE OWNER = 'BM'
   AND NAME = 'SP_BM1_GET_BY_ICP'
   AND TYPE = 'PROCEDURE'
 ORDER BY LINE;
