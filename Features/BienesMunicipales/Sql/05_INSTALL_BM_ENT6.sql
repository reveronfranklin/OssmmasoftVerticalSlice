CREATE OR REPLACE PROCEDURE BM.SP_BM_REP_PLACA (
    p_CodigoEmpresa IN NUMBER,
    p_NumeroPlaca IN VARCHAR2,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
BEGIN
    SELECT COUNT(1)
      INTO p_TotalRecords
      FROM BM.BM_BIENES
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND NUMERO_PLACA = p_NumeroPlaca;

    OPEN p_ResultSet FOR
        SELECT B.CODIGO_BIEN,
               B.NUMERO_PLACA,
               A.DENOMINACION ARTICULO,
               BM.BM_ESPECIFICACIONES(B.CODIGO_BIEN) ESPECIFICACION,
               NVL(B.VALOR_INICIAL, 0) VALOR_INICIAL,
               NVL(B.VALOR_ACTUAL, B.VALOR_INICIAL) VALOR_ACTUAL,
               NVL(M.CODIGO_MOV_BIEN, 0) CODIGO_MOV_BIEN,
               M.TIPO_MOVIMIENTO,
               M.FECHA_MOVIMIENTO,
               NVL(M.CODIGO_DIR_BIEN, 0) CODIGO_DIR_BIEN,
               NVL(D.CODIGO_ICP, 0) CODIGO_ICP,
               NVL(P.UNIDAD_EJECUTORA, P.DENOMINACION) UNIDAD_EJECUTORA,
               BM.BM_PKG_UTIL.GET_ESPECIFICACION_RESP(B.CODIGO_BIEN) RESPONSABLE_BIEN,
               DECODE(M.TIPO_MOVIMIENTO, 'D', 'DESINCORPORADO', 'E', 'EXCLUIDO', 'ACTIVO') ESTADO_OPERATIVO
          FROM BM.BM_BIENES B
          JOIN BM.BM_ARTICULOS A
            ON A.CODIGO_ARTICULO = B.CODIGO_ARTICULO
          LEFT JOIN (
                SELECT X.CODIGO_EMPRESA,
                       X.CODIGO_BIEN,
                       MAX(X.CODIGO_MOV_BIEN) CODIGO_MOV_BIEN
                  FROM BM.BM_MOV_BIENES X
                 GROUP BY X.CODIGO_EMPRESA, X.CODIGO_BIEN
          ) LM
            ON LM.CODIGO_EMPRESA = B.CODIGO_EMPRESA
           AND LM.CODIGO_BIEN = B.CODIGO_BIEN
          LEFT JOIN BM.BM_MOV_BIENES M
            ON M.CODIGO_MOV_BIEN = LM.CODIGO_MOV_BIEN
          LEFT JOIN BM.BM_DIR_BIEN D
            ON D.CODIGO_DIR_BIEN = M.CODIGO_DIR_BIEN
          LEFT JOIN PRE.PRE_INDICE_CAT_PRG P
            ON P.CODIGO_ICP = D.CODIGO_ICP
         WHERE B.CODIGO_EMPRESA = p_CodigoEmpresa
           AND B.NUMERO_PLACA = p_NumeroPlaca;

    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT NULL CODIGO_BIEN FROM DUAL WHERE 1 = 0;
END;
/

CREATE OR REPLACE PROCEDURE BM.SP_BM_REP_UBI_ICP (
    p_CodigoEmpresa IN NUMBER,
    p_CodigoIcp IN NUMBER,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
BEGIN
    SELECT COUNT(1)
      INTO p_TotalRecords
      FROM BM.BM_DIR_BIEN
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND (p_CodigoIcp = 0 OR CODIGO_ICP = p_CodigoIcp);

    OPEN p_ResultSet FOR
        SELECT D.CODIGO_ICP,
               NVL(P.UNIDAD_EJECUTORA, P.DENOMINACION) UNIDAD_EJECUTORA,
               D.CODIGO_DIR_BIEN,
               UPPER(TRIM(NVL(D.VIALIDAD, '') || ' ' || NVL(D.VIVIENDA, '') || ' ' || NVL(D.NIVEL, '') || ' ' || NVL(D.NUMERO_UNIDAD, '') || ' ' || NVL(D.COMPLEMENTO_DIR, ''))) DIRECCION,
               COUNT(B.CODIGO_BIEN) TOTAL_BIENES,
               NVL(SUM(NVL(B.VALOR_ACTUAL, B.VALOR_INICIAL)), 0) VALOR_TOTAL
          FROM BM.BM_DIR_BIEN D
          JOIN PRE.PRE_INDICE_CAT_PRG P
            ON P.CODIGO_ICP = D.CODIGO_ICP
          LEFT JOIN (
                SELECT M.CODIGO_EMPRESA,
                       M.CODIGO_BIEN,
                       M.CODIGO_MOV_BIEN,
                       M.CODIGO_DIR_BIEN,
                       M.TIPO_MOVIMIENTO
                  FROM BM.BM_MOV_BIENES M
                  JOIN (
                    SELECT X.CODIGO_EMPRESA,
                           X.CODIGO_BIEN,
                           MAX(X.CODIGO_MOV_BIEN) CODIGO_MOV_BIEN
                      FROM BM.BM_MOV_BIENES X
                     GROUP BY X.CODIGO_EMPRESA, X.CODIGO_BIEN
                  ) LM
                    ON LM.CODIGO_EMPRESA = M.CODIGO_EMPRESA
                   AND LM.CODIGO_BIEN = M.CODIGO_BIEN
                   AND LM.CODIGO_MOV_BIEN = M.CODIGO_MOV_BIEN
          ) MV
            ON MV.CODIGO_DIR_BIEN = D.CODIGO_DIR_BIEN
           AND MV.CODIGO_EMPRESA = D.CODIGO_EMPRESA
           AND NVL(MV.TIPO_MOVIMIENTO, 'A') NOT IN ('D', 'E')
          LEFT JOIN BM.BM_BIENES B
            ON B.CODIGO_BIEN = MV.CODIGO_BIEN
           AND B.CODIGO_EMPRESA = MV.CODIGO_EMPRESA
         WHERE D.CODIGO_EMPRESA = p_CodigoEmpresa
           AND (p_CodigoIcp = 0 OR D.CODIGO_ICP = p_CodigoIcp)
         GROUP BY D.CODIGO_ICP,
                  NVL(P.UNIDAD_EJECUTORA, P.DENOMINACION),
                  D.CODIGO_DIR_BIEN,
                  UPPER(TRIM(NVL(D.VIALIDAD, '') || ' ' || NVL(D.VIVIENDA, '') || ' ' || NVL(D.NIVEL, '') || ' ' || NVL(D.NUMERO_UNIDAD, '') || ' ' || NVL(D.COMPLEMENTO_DIR, '')))
         ORDER BY NVL(P.UNIDAD_EJECUTORA, P.DENOMINACION), D.CODIGO_DIR_BIEN;

    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT NULL CODIGO_ICP FROM DUAL WHERE 1 = 0;
END;
/

CREATE OR REPLACE PROCEDURE BM.SP_BM_REP_MOV_BIEN (
    p_CodigoEmpresa IN NUMBER,
    p_CodigoBien IN NUMBER,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
BEGIN
    BM.SP_BM_MOV_GET_BIEN(p_CodigoEmpresa, p_CodigoBien, p_ResultSet, p_Message, p_TotalRecords);
END;
/

CREATE OR REPLACE PROCEDURE BMC.SP_BM_REP_CONT_DIF (
    p_CodigoEmpresa IN NUMBER,
    p_CodigoBmConteo IN NUMBER,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
BEGIN
    SELECT COUNT(1)
      INTO p_TotalRecords
      FROM BMC.BM_CONTEO_DETALLE
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND CODIGO_BM_CONTEO = p_CodigoBmConteo
       AND NVL(DIFERENCIA, 0) <> 0;

    OPEN p_ResultSet FOR
        SELECT CODIGO_BM_CONTEO,
               CODIGO_BM_CONTEO_DETALLE,
               NVL(CONTEO, 0) CONTEO,
               CODIGO_ICP,
               UNIDAD_TRABAJO,
               NUMERO_PLACA,
               ARTICULO,
               NVL(CANTIDAD, 0) CANTIDAD,
               NVL(CANTIDAD_CONTADA, 0) CANTIDAD_CONTADA,
               NVL(DIFERENCIA, 0) DIFERENCIA,
               COMENTARIO
          FROM BMC.BM_CONTEO_DETALLE
         WHERE CODIGO_EMPRESA = p_CodigoEmpresa
           AND CODIGO_BM_CONTEO = p_CodigoBmConteo
           AND NVL(DIFERENCIA, 0) <> 0
         ORDER BY CODIGO_ICP, NUMERO_PLACA;

    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT NULL CODIGO_BM_CONTEO FROM DUAL WHERE 1 = 0;
END;
/

CREATE OR REPLACE PROCEDURE BMC.SP_BM_REP_CONT_HIST (
    p_CodigoEmpresa IN NUMBER,
    p_FechaDesde IN DATE,
    p_FechaHasta IN DATE,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
BEGIN
    SELECT COUNT(1)
      INTO p_TotalRecords
      FROM BMC.BM_CONTEO_HISTORICO
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND (p_FechaDesde IS NULL OR TRUNC(FECHA_CIERRE) >= TRUNC(p_FechaDesde))
       AND (p_FechaHasta IS NULL OR TRUNC(FECHA_CIERRE) <= TRUNC(p_FechaHasta));

    OPEN p_ResultSet FOR
        SELECT CODIGO_BM_CONTEO,
               TITULO,
               FECHA,
               FECHA_CIERRE,
               NVL(TOTAL_CANTIDAD, 0) TOTAL_CANTIDAD,
               NVL(TOTAL_CANTIDAD_CONTADA, 0) TOTAL_CANTIDAD_CONTADA,
               NVL(TOTAL_DIFERENCIA, 0) TOTAL_DIFERENCIA,
               COMENTARIO
          FROM BMC.BM_CONTEO_HISTORICO
         WHERE CODIGO_EMPRESA = p_CodigoEmpresa
           AND (p_FechaDesde IS NULL OR TRUNC(FECHA_CIERRE) >= TRUNC(p_FechaDesde))
           AND (p_FechaHasta IS NULL OR TRUNC(FECHA_CIERRE) <= TRUNC(p_FechaHasta))
         ORDER BY FECHA_CIERRE DESC, CODIGO_BM_CONTEO DESC;

    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT NULL CODIGO_BM_CONTEO FROM DUAL WHERE 1 = 0;
END;
/
