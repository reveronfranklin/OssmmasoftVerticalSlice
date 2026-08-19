-- =============================================================================
-- BM - Inventario de bienes por unidad de trabajo.
--
-- Alimenta DOS reportes con el mismo query de negocio:
--   * ReporteBm1        - listado tabular (POST api/ReporteBm1/pdf)
--   * BM-1 Especial     - formulario oficial (requerimiento 27)
--
-- Los parametros nuevos del requerimiento 27 son **todos opcionales y con
-- DEFAULT NULL**, y p_OrdenUnidad tiene DEFAULT 'N'. Es lo que permite extender
-- este procedimiento sin tocar a quien ya lo llama: ReporteBm1 sigue enviando
-- los cuatro de siempre y obtiene exactamente el mismo resultado y el mismo
-- orden que antes.
--
-- Se extiende en vez de duplicar -la opcion (a) del requerimiento 27- porque el
-- query de negocio es identico en los dos reportes, y mantener dos copias
-- garantiza que un dia se corrija una sola.
--
-- Las fechas pasan a ser opcionales. No cambia nada para ReporteBm1: su handler
-- las valida antes de llamar (TryValidateDates), asi que nunca llegan nulas.
-- =============================================================================
CREATE OR REPLACE PROCEDURE BM.SP_REP_BM1_GET (
    p_CodigoEmpresa    IN NUMBER,
    p_FechaDesde       IN DATE,
    p_FechaHasta       IN DATE,
    p_CodigosIcp       IN VARCHAR2,
    p_ResultSet        OUT SYS_REFCURSOR,
    p_Message          OUT VARCHAR2,
    p_TotalRecords     OUT NUMBER,
    p_CodigoDirBien    IN NUMBER   DEFAULT NULL,
    p_PlacaDesde       IN VARCHAR2 DEFAULT NULL,
    p_PlacaHasta       IN VARCHAR2 DEFAULT NULL,
    p_CodigoArticulo   IN NUMBER   DEFAULT NULL,
    p_OrdenUnidad      IN CHAR     DEFAULT 'N'
) AS
BEGIN
    SELECT COUNT(*)
      INTO p_TotalRecords
      FROM (
            SELECT 1
              FROM BM.BM_BIENES A,
                   BM.BM_MOV_BIENES B,
                   BM.BM_DIR_BIEN C,
                   PRE.PRE_INDICE_CAT_PRG D,
                   BM.BM_ARTICULOS E,
                   BM.BM_CLASIFICACION_BIENES F
             WHERE A.CODIGO_EMPRESA = p_CodigoEmpresa
               AND A.CODIGO_BIEN = B.CODIGO_BIEN
               AND B.CODIGO_DIR_BIEN = C.CODIGO_DIR_BIEN
               AND C.CODIGO_ICP = D.CODIGO_ICP
               AND E.CODIGO_ARTICULO = A.CODIGO_ARTICULO
               AND F.CODIGO_CLASIFICACION_BIEN = E.CODIGO_CLASIFICACION_BIEN
               AND (p_FechaDesde IS NULL OR TRUNC(B.FECHA_MOVIMIENTO) >= TRUNC(p_FechaDesde))
               AND (p_FechaHasta IS NULL OR TRUNC(B.FECHA_MOVIMIENTO) <= TRUNC(p_FechaHasta))
               AND (
                     p_CodigosIcp IS NULL
                     OR INSTR(',' || p_CodigosIcp || ',', ',' || TO_CHAR(C.CODIGO_ICP) || ',') > 0
                   )
               AND (p_CodigoDirBien  IS NULL OR B.CODIGO_DIR_BIEN  = p_CodigoDirBien)
               AND (p_CodigoArticulo IS NULL OR A.CODIGO_ARTICULO  = p_CodigoArticulo)
               AND (p_PlacaDesde     IS NULL OR A.NUMERO_PLACA    >= p_PlacaDesde)
               AND (p_PlacaHasta     IS NULL OR A.NUMERO_PLACA    <= p_PlacaHasta)
               AND B.CODIGO_MOV_BIEN = (
                    SELECT MAX(X.CODIGO_MOV_BIEN)
                      FROM BM.BM_MOV_BIENES X
                     WHERE X.CODIGO_BIEN = A.CODIGO_BIEN
                   )
               AND NOT EXISTS (
                    SELECT NULL
                      FROM BM.BM_MOV_BIENES X
                     WHERE X.CODIGO_MOV_BIEN = B.CODIGO_MOV_BIEN
                       AND X.TIPO_MOVIMIENTO = 'D'
                   )
          GROUP BY NVL(D.UNIDAD_EJECUTORA,D.DENOMINACION),
                   F.CODIGO_GRUPO,
                   F.CODIGO_NIVEL1,
                   F.CODIGO_NIVEL2,
                   A.NUMERO_LOTE,
                   NVL(A.VALOR_ACTUAL,A.VALOR_INICIAL),
                   E.DENOMINACION,
                   BM.BM_PKG_UTIL.GET_ESPECIFICACION(A.CODIGO_BIEN)||' / '||B.FECHA_MOVIMIENTO,
                   BM.BM_PKG_UTIL.GET_ESPECIFICACION_RESP(A.CODIGO_BIEN),
                   B.FECHA_MOVIMIENTO
      );

    OPEN p_ResultSet FOR
        SELECT NVL(D.UNIDAD_EJECUTORA,D.DENOMINACION) UNIDAD_TRABAJO,
               F.CODIGO_GRUPO,
               F.CODIGO_NIVEL1,
               F.CODIGO_NIVEL2,
               A.NUMERO_LOTE,
               COUNT(*) CANTIDAD,
               DECODE(COUNT(*),
                   1,
                   LPAD(MIN(TO_NUMBER(SUBSTR(A.NUMERO_PLACA,9))),5,0),
                   MAX(TO_NUMBER(SUBSTR(A.NUMERO_PLACA,9)))-MIN(TO_NUMBER(SUBSTR(A.NUMERO_PLACA,9)))+1,
                   LPAD(MIN(TO_NUMBER(SUBSTR(A.NUMERO_PLACA,9))),5,0)||'-'||
                   LPAD(MAX(TO_NUMBER(SUBSTR(A.NUMERO_PLACA,9))),5,0),
                   BM.BM_PKG_UTIL.GET_RANGE_NUMERO_PLACA(MIN(A.NUMERO_PLACA),MAX(A.NUMERO_PLACA),MIN(B.CODIGO_DIR_BIEN))
               ) NUMERO_PLACA,
               NVL(A.VALOR_ACTUAL,A.VALOR_INICIAL) VALOR_ACTUAL,
               E.DENOMINACION ARTICULO,
               BM.BM_PKG_UTIL.GET_ESPECIFICACION(A.CODIGO_BIEN)||' / '||B.FECHA_MOVIMIENTO ESPECIFICACION,
               NULL SERVICIO,
               BM.BM_PKG_UTIL.GET_ESPECIFICACION_RESP(A.CODIGO_BIEN) RESPONSABLE_BIEN,
               B.FECHA_MOVIMIENTO
          FROM BM.BM_BIENES A,
               BM.BM_MOV_BIENES B,
               BM.BM_DIR_BIEN C,
               PRE.PRE_INDICE_CAT_PRG D,
               BM.BM_ARTICULOS E,
               BM.BM_CLASIFICACION_BIENES F
         WHERE A.CODIGO_EMPRESA = p_CodigoEmpresa
           AND A.CODIGO_BIEN = B.CODIGO_BIEN
           AND B.CODIGO_DIR_BIEN = C.CODIGO_DIR_BIEN
           AND C.CODIGO_ICP = D.CODIGO_ICP
           AND E.CODIGO_ARTICULO = A.CODIGO_ARTICULO
           AND F.CODIGO_CLASIFICACION_BIEN = E.CODIGO_CLASIFICACION_BIEN
           AND (p_FechaDesde IS NULL OR TRUNC(B.FECHA_MOVIMIENTO) >= TRUNC(p_FechaDesde))
           AND (p_FechaHasta IS NULL OR TRUNC(B.FECHA_MOVIMIENTO) <= TRUNC(p_FechaHasta))
           AND (
                 p_CodigosIcp IS NULL
                 OR INSTR(',' || p_CodigosIcp || ',', ',' || TO_CHAR(C.CODIGO_ICP) || ',') > 0
               )
           AND (p_CodigoDirBien  IS NULL OR B.CODIGO_DIR_BIEN  = p_CodigoDirBien)
           AND (p_CodigoArticulo IS NULL OR A.CODIGO_ARTICULO  = p_CodigoArticulo)
           AND (p_PlacaDesde     IS NULL OR A.NUMERO_PLACA    >= p_PlacaDesde)
           AND (p_PlacaHasta     IS NULL OR A.NUMERO_PLACA    <= p_PlacaHasta)
           AND B.CODIGO_MOV_BIEN = (
                SELECT MAX(X.CODIGO_MOV_BIEN)
                  FROM BM.BM_MOV_BIENES X
                 WHERE X.CODIGO_BIEN = A.CODIGO_BIEN
               )
           AND NOT EXISTS (
                SELECT NULL
                  FROM BM.BM_MOV_BIENES X
                 WHERE X.CODIGO_MOV_BIEN = B.CODIGO_MOV_BIEN
                   AND X.TIPO_MOVIMIENTO = 'D'
               )
      GROUP BY NVL(D.UNIDAD_EJECUTORA,D.DENOMINACION),
               F.CODIGO_GRUPO,
               F.CODIGO_NIVEL1,
               F.CODIGO_NIVEL2,
               A.NUMERO_LOTE,
               NVL(A.VALOR_ACTUAL,A.VALOR_INICIAL),
               E.DENOMINACION,
               BM.BM_PKG_UTIL.GET_ESPECIFICACION(A.CODIGO_BIEN)||' / '||B.FECHA_MOVIMIENTO,
               BM.BM_PKG_UTIL.GET_ESPECIFICACION_RESP(A.CODIGO_BIEN),
               B.FECHA_MOVIMIENTO
      -- El BM-1 Especial agrupa por unidad con salto de pagina, y para que el
      -- quiebre funcione las filas tienen que llegar ordenadas por unidad. El
      -- listado tabular conserva su orden de siempre: sin el conmutador, cambiar
      -- el ORDER BY alteraria un reporte que ya esta en produccion.
      ORDER BY CASE WHEN p_OrdenUnidad = 'S'
                    THEN NVL(D.UNIDAD_EJECUTORA,D.DENOMINACION)
               END,
               CASE WHEN p_OrdenUnidad = 'S' THEN F.CODIGO_GRUPO   END,
               CASE WHEN p_OrdenUnidad = 'S' THEN F.CODIGO_NIVEL1  END,
               CASE WHEN p_OrdenUnidad = 'S' THEN F.CODIGO_NIVEL2  END,
               B.FECHA_MOVIMIENTO;

    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR
            SELECT CAST(NULL AS VARCHAR2(4000)) UNIDAD_TRABAJO,
                   CAST(NULL AS VARCHAR2(50)) CODIGO_GRUPO,
                   CAST(NULL AS VARCHAR2(50)) CODIGO_NIVEL1,
                   CAST(NULL AS VARCHAR2(50)) CODIGO_NIVEL2,
                   CAST(NULL AS VARCHAR2(50)) NUMERO_LOTE,
                   CAST(NULL AS NUMBER) CANTIDAD,
                   CAST(NULL AS VARCHAR2(200)) NUMERO_PLACA,
                   CAST(NULL AS NUMBER) VALOR_ACTUAL,
                   CAST(NULL AS VARCHAR2(4000)) ARTICULO,
                   CAST(NULL AS VARCHAR2(4000)) ESPECIFICACION,
                   CAST(NULL AS VARCHAR2(4000)) SERVICIO,
                   CAST(NULL AS VARCHAR2(4000)) RESPONSABLE_BIEN,
                   CAST(NULL AS DATE) FECHA_MOVIMIENTO
              FROM DUAL
             WHERE 1 = 0;
END SP_REP_BM1_GET;
/
