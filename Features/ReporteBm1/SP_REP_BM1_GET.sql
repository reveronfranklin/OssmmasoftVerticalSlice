CREATE OR REPLACE PROCEDURE BM.SP_REP_BM1_GET (
    p_CodigoEmpresa IN NUMBER,
    p_FechaDesde    IN DATE,
    p_FechaHasta    IN DATE,
    p_CodigosIcp    IN VARCHAR2,
    p_ResultSet     OUT SYS_REFCURSOR,
    p_Message       OUT VARCHAR2,
    p_TotalRecords  OUT NUMBER
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
               AND TRUNC(B.FECHA_MOVIMIENTO) >= TRUNC(p_FechaDesde)
               AND TRUNC(B.FECHA_MOVIMIENTO) <= TRUNC(p_FechaHasta)
               AND (
                     p_CodigosIcp IS NULL
                     OR INSTR(',' || p_CodigosIcp || ',', ',' || TO_CHAR(C.CODIGO_ICP) || ',') > 0
                   )
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
           AND TRUNC(B.FECHA_MOVIMIENTO) >= TRUNC(p_FechaDesde)
           AND TRUNC(B.FECHA_MOVIMIENTO) <= TRUNC(p_FechaHasta)
           AND (
                 p_CodigosIcp IS NULL
                 OR INSTR(',' || p_CodigosIcp || ',', ',' || TO_CHAR(C.CODIGO_ICP) || ',') > 0
               )
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
      ORDER BY B.FECHA_MOVIMIENTO;

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
