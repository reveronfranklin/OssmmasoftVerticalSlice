CREATE OR REPLACE PROCEDURE BM.SP_BM_REP_LOTE (
    p_CodigoEmpresa IN NUMBER,
    p_NumeroLote IN VARCHAR2,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
BEGIN
    SELECT COUNT(1)
      INTO p_TotalRecords
      FROM BM.BM_BIENES
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND NUMERO_LOTE = p_NumeroLote;

    OPEN p_ResultSet FOR
        SELECT B.CODIGO_BIEN,
               B.NUMERO_PLACA,
               B.NUMERO_LOTE,
               A.DENOMINACION ARTICULO,
               B.FECHA_INS,
               B.FECHA_COMPRA,
               NVL(B.VALOR_INICIAL, 0) VALOR_INICIAL,
               NVL(B.VALOR_ACTUAL, B.VALOR_INICIAL) VALOR_ACTUAL,
               NVL(V.CODIGO_ICP, 0) CODIGO_ICP,
               V.UNIDAD_TRABAJO UNIDAD_EJECUTORA,
               V.RESPONSABLE_BIEN
          FROM BM.BM_BIENES B,
               BM.BM_ARTICULOS A,
               BM.BM_V_BM1 V
         WHERE A.CODIGO_ARTICULO = B.CODIGO_ARTICULO
           AND V.CODIGO_BIEN(+) = B.CODIGO_BIEN
           AND B.CODIGO_EMPRESA = p_CodigoEmpresa
           AND B.NUMERO_LOTE = p_NumeroLote
         ORDER BY B.NUMERO_PLACA;

    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT NULL CODIGO_BIEN FROM DUAL WHERE 1 = 0;
END;
/

CREATE OR REPLACE PROCEDURE BM.SP_BM_REP_FICHA (
    p_CodigoEmpresa IN NUMBER,
    p_NumeroPlaca IN VARCHAR2,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
    v_CodigoBien NUMBER;
BEGIN
    SELECT MAX(CODIGO_BIEN)
      INTO v_CodigoBien
      FROM BM.BM_BIENES
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND NUMERO_PLACA = p_NumeroPlaca;

    OPEN p_ResultSet FOR
        SELECT 'BIEN' SECCION,
               B.NUMERO_PLACA REFERENCIA,
               A.DENOMINACION DESCRIPCION,
               B.FECHA_INS FECHA,
               NVL(V.UNIDAD_TRABAJO, '') UNIDAD,
               NVL(V.RESPONSABLE_BIEN, '') OBSERVACION
          FROM BM.BM_BIENES B,
               BM.BM_ARTICULOS A,
               BM.BM_V_BM1 V
         WHERE A.CODIGO_ARTICULO = B.CODIGO_ARTICULO
           AND V.CODIGO_BIEN(+) = B.CODIGO_BIEN
           AND B.CODIGO_EMPRESA = p_CodigoEmpresa
           AND B.CODIGO_BIEN = v_CodigoBien
        UNION ALL
        SELECT 'FOTO' SECCION,
               F.TITULO REFERENCIA,
               F.FOTO DESCRIPCION,
               F.FECHA_INS FECHA,
               '' UNIDAD,
               F.FOTO OBSERVACION
          FROM BM.BM_BIENES_FOTO F
         WHERE F.CODIGO_EMPRESA = p_CodigoEmpresa
           AND F.NUMERO_PLACA = p_NumeroPlaca
        UNION ALL
        SELECT 'DETALLE' SECCION,
               T.DESCRIPCION REFERENCIA,
               NVL(E.DESCRIPCION, D.ESPECIFICACION) DESCRIPCION,
               D.FECHA_INS FECHA,
               '' UNIDAD,
               D.ESPECIFICACION OBSERVACION
          FROM BM.BM_DETALLE_BIENES D,
               BM.BM_DESCRIPTIVAS T,
               BM.BM_DESCRIPTIVAS E
         WHERE T.DESCRIPCION_ID(+) = D.TIPO_ESPECIFICACION_ID
           AND E.DESCRIPCION_ID(+) = D.ESPECIFICACION_ID
           AND D.CODIGO_EMPRESA = p_CodigoEmpresa
           AND D.CODIGO_BIEN = v_CodigoBien
        UNION ALL
        SELECT 'MOVIMIENTO' SECCION,
               M.TIPO_MOVIMIENTO REFERENCIA,
               NVL(TM.DESCRIPCION, M.TIPO_MOVIMIENTO) DESCRIPCION,
               M.FECHA_MOVIMIENTO FECHA,
               NVL(P.UNIDAD_EJECUTORA, P.DENOMINACION) UNIDAD,
               NVL(CM.DESCRIPCION, '') OBSERVACION
          FROM BM.BM_MOV_BIENES M,
               BM.BM_DIR_BIEN D,
               PRE.PRE_INDICE_CAT_PRG P,
               BM.BM_DESCRIPTIVAS TM,
               BM.BM_DESCRIPTIVAS CM
         WHERE D.CODIGO_DIR_BIEN(+) = M.CODIGO_DIR_BIEN
           AND P.CODIGO_ICP(+) = D.CODIGO_ICP
           AND TM.CODIGO(+) = M.TIPO_MOVIMIENTO
           AND TM.TITULO_ID(+) = 4
           AND CM.DESCRIPCION_ID(+) = M.CONCEPTO_MOV_ID
           AND M.CODIGO_EMPRESA = p_CodigoEmpresa
           AND M.CODIGO_BIEN = v_CodigoBien;

    SELECT COUNT(1)
      INTO p_TotalRecords
      FROM BM.BM_BIENES
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND CODIGO_BIEN = v_CodigoBien;

    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT NULL SECCION FROM DUAL WHERE 1 = 0;
END;
/

CREATE OR REPLACE PROCEDURE BM.SP_BM_REP_SOL_MOV (
    p_CodigoEmpresa IN NUMBER,
    p_Aprobado IN NUMBER,
    p_TipoMovimiento IN VARCHAR2,
    p_FechaDesde IN DATE,
    p_FechaHasta IN DATE,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
BEGIN
    SELECT COUNT(1)
      INTO p_TotalRecords
      FROM BM.BM_SOL_MOV_BIENES
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND (p_Aprobado < 0 OR NVL(APROBADO, 0) = p_Aprobado)
       AND (p_TipoMovimiento IS NULL OR TIPO_MOVIMIENTO = SUBSTR(p_TipoMovimiento, 1, 1))
       AND (p_FechaDesde IS NULL OR TRUNC(FECHA_MOVIMIENTO) >= TRUNC(p_FechaDesde))
       AND (p_FechaHasta IS NULL OR TRUNC(FECHA_MOVIMIENTO) <= TRUNC(p_FechaHasta));

    OPEN p_ResultSet FOR
        SELECT S.CODIGO_SOL_MOV_BIEN,
               S.NUMERO_SOLICITUD,
               B.NUMERO_PLACA,
               A.DENOMINACION ARTICULO,
               S.TIPO_MOVIMIENTO,
               TM.DESCRIPCION TIPO_MOVIMIENTO_DESC,
               S.FECHA_MOVIMIENTO,
               NVL(S.APROBADO, 0) APROBADO,
               NVL(P.UNIDAD_EJECUTORA, P.DENOMINACION) UNIDAD_EJECUTORA,
               CM.DESCRIPCION CONCEPTO_MOVIMIENTO,
               S.NOTA_INCIDENCIA
          FROM BM.BM_SOL_MOV_BIENES S,
               BM.BM_BIENES B,
               BM.BM_ARTICULOS A,
               BM.BM_DIR_BIEN D,
               PRE.PRE_INDICE_CAT_PRG P,
               BM.BM_DESCRIPTIVAS TM,
               BM.BM_DESCRIPTIVAS CM
         WHERE B.CODIGO_BIEN = S.CODIGO_BIEN
           AND A.CODIGO_ARTICULO = B.CODIGO_ARTICULO
           AND D.CODIGO_DIR_BIEN(+) = S.CODIGO_DIR_BIEN
           AND P.CODIGO_ICP(+) = D.CODIGO_ICP
           AND TM.CODIGO(+) = S.TIPO_MOVIMIENTO
           AND TM.TITULO_ID(+) = 4
           AND CM.DESCRIPCION_ID(+) = S.CONCEPTO_MOV_ID
           AND S.CODIGO_EMPRESA = p_CodigoEmpresa
           AND (p_Aprobado < 0 OR NVL(S.APROBADO, 0) = p_Aprobado)
           AND (p_TipoMovimiento IS NULL OR S.TIPO_MOVIMIENTO = SUBSTR(p_TipoMovimiento, 1, 1))
           AND (p_FechaDesde IS NULL OR TRUNC(S.FECHA_MOVIMIENTO) >= TRUNC(p_FechaDesde))
           AND (p_FechaHasta IS NULL OR TRUNC(S.FECHA_MOVIMIENTO) <= TRUNC(p_FechaHasta))
         ORDER BY S.FECHA_MOVIMIENTO DESC, S.CODIGO_SOL_MOV_BIEN DESC;

    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT NULL CODIGO_SOL_MOV_BIEN FROM DUAL WHERE 1 = 0;
END;
/

CREATE OR REPLACE PROCEDURE BM.SP_BM_REP_MOV_FILT (
    p_CodigoEmpresa IN NUMBER,
    p_TipoMovimiento IN VARCHAR2,
    p_FechaDesde IN DATE,
    p_FechaHasta IN DATE,
    p_CodigoIcp IN NUMBER,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
BEGIN
    SELECT COUNT(1)
      INTO p_TotalRecords
      FROM BM.BM_MOV_BIENES M,
           BM.BM_DIR_BIEN D
     WHERE D.CODIGO_DIR_BIEN(+) = M.CODIGO_DIR_BIEN
       AND M.CODIGO_EMPRESA = p_CodigoEmpresa
       AND (p_TipoMovimiento IS NULL OR M.TIPO_MOVIMIENTO = SUBSTR(p_TipoMovimiento, 1, 1))
       AND (p_FechaDesde IS NULL OR TRUNC(M.FECHA_MOVIMIENTO) >= TRUNC(p_FechaDesde))
       AND (p_FechaHasta IS NULL OR TRUNC(M.FECHA_MOVIMIENTO) <= TRUNC(p_FechaHasta))
       AND (NVL(p_CodigoIcp, 0) = 0 OR D.CODIGO_ICP = p_CodigoIcp);

    OPEN p_ResultSet FOR
        SELECT M.CODIGO_MOV_BIEN,
               M.CODIGO_BIEN,
               B.NUMERO_PLACA,
               A.DENOMINACION ARTICULO,
               M.TIPO_MOVIMIENTO,
               TM.DESCRIPCION TIPO_MOVIMIENTO_DESC,
               M.FECHA_MOVIMIENTO,
               M.CODIGO_DIR_BIEN,
               NVL(D.CODIGO_ICP, 0) CODIGO_ICP,
               NVL(P.UNIDAD_EJECUTORA, P.DENOMINACION) UNIDAD_EJECUTORA,
               NVL(M.CONCEPTO_MOV_ID, 0) CONCEPTO_MOV_ID,
               CM.DESCRIPCION CONCEPTO_MOVIMIENTO,
               NVL(M.CODIGO_SOL_MOV_BIEN, 0) CODIGO_SOL_MOV_BIEN,
               DECODE(M.TIPO_MOVIMIENTO, 'D', 1, 'E', 1, 0) ES_MOVIMIENTO_FINAL,
               M.EXTRA1,
               M.EXTRA2,
               M.EXTRA3
          FROM BM.BM_MOV_BIENES M,
               BM.BM_BIENES B,
               BM.BM_ARTICULOS A,
               BM.BM_DIR_BIEN D,
               PRE.PRE_INDICE_CAT_PRG P,
               BM.BM_DESCRIPTIVAS TM,
               BM.BM_DESCRIPTIVAS CM
         WHERE B.CODIGO_BIEN = M.CODIGO_BIEN
           AND A.CODIGO_ARTICULO = B.CODIGO_ARTICULO
           AND D.CODIGO_DIR_BIEN(+) = M.CODIGO_DIR_BIEN
           AND P.CODIGO_ICP(+) = D.CODIGO_ICP
           AND TM.CODIGO(+) = M.TIPO_MOVIMIENTO
           AND TM.TITULO_ID(+) = 4
           AND CM.DESCRIPCION_ID(+) = M.CONCEPTO_MOV_ID
           AND M.CODIGO_EMPRESA = p_CodigoEmpresa
           AND (p_TipoMovimiento IS NULL OR M.TIPO_MOVIMIENTO = SUBSTR(p_TipoMovimiento, 1, 1))
           AND (p_FechaDesde IS NULL OR TRUNC(M.FECHA_MOVIMIENTO) >= TRUNC(p_FechaDesde))
           AND (p_FechaHasta IS NULL OR TRUNC(M.FECHA_MOVIMIENTO) <= TRUNC(p_FechaHasta))
           AND (NVL(p_CodigoIcp, 0) = 0 OR D.CODIGO_ICP = p_CodigoIcp)
         ORDER BY M.FECHA_MOVIMIENTO DESC, M.CODIGO_MOV_BIEN DESC;

    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT NULL CODIGO_MOV_BIEN FROM DUAL WHERE 1 = 0;
END;
/

CREATE OR REPLACE PROCEDURE BM.SP_BM_REP_PROC_MAS (
    p_CodigoEmpresa IN NUMBER,
    p_CodigoProcMasivo IN NUMBER,
    p_FechaDesde IN DATE,
    p_FechaHasta IN DATE,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
BEGIN
    SELECT COUNT(1)
      INTO p_TotalRecords
      FROM BM.BM_PROC_MASIVO H,
           BM.BM_PROC_MAS_DET D
     WHERE D.CODIGO_PROC_MASIVO = H.CODIGO_PROC_MASIVO
       AND H.CODIGO_EMPRESA = p_CodigoEmpresa
       AND (NVL(p_CodigoProcMasivo, 0) = 0 OR H.CODIGO_PROC_MASIVO = p_CodigoProcMasivo)
       AND (p_FechaDesde IS NULL OR TRUNC(H.FECHA_PROC) >= TRUNC(p_FechaDesde))
       AND (p_FechaHasta IS NULL OR TRUNC(H.FECHA_PROC) <= TRUNC(p_FechaHasta));

    OPEN p_ResultSet FOR
        SELECT H.CODIGO_PROC_MASIVO,
               D.CODIGO_PROC_MAS_DET,
               D.CODIGO_BIEN,
               D.NUMERO_PLACA,
               A.DENOMINACION ARTICULO,
               H.CODIGO_DIR_ORIGEN,
               H.CODIGO_ICP CODIGO_ICP_ORIGEN,
               '' UNIDAD_ORIGEN,
               H.CODIGO_DIR_DESTINO,
               NVL(PD.UNIDAD_EJECUTORA, PD.DENOMINACION) UNIDAD_DESTINO,
               D.ESTADO,
               D.MENSAJE,
               NVL(D.CODIGO_MOV_BIEN, 0) CODIGO_MOV_BIEN,
               H.TOTAL_PROCESADOS,
               H.TOTAL_EXITOSOS,
               H.TOTAL_RECHAZADOS
          FROM BM.BM_PROC_MASIVO H,
               BM.BM_PROC_MAS_DET D,
               BM.BM_BIENES B,
               BM.BM_ARTICULOS A,
               BM.BM_DIR_BIEN DD,
               PRE.PRE_INDICE_CAT_PRG PD
         WHERE D.CODIGO_PROC_MASIVO = H.CODIGO_PROC_MASIVO
           AND B.CODIGO_BIEN(+) = D.CODIGO_BIEN
           AND A.CODIGO_ARTICULO(+) = B.CODIGO_ARTICULO
           AND DD.CODIGO_DIR_BIEN(+) = H.CODIGO_DIR_DESTINO
           AND PD.CODIGO_ICP(+) = DD.CODIGO_ICP
           AND H.CODIGO_EMPRESA = p_CodigoEmpresa
           AND (NVL(p_CodigoProcMasivo, 0) = 0 OR H.CODIGO_PROC_MASIVO = p_CodigoProcMasivo)
           AND (p_FechaDesde IS NULL OR TRUNC(H.FECHA_PROC) >= TRUNC(p_FechaDesde))
           AND (p_FechaHasta IS NULL OR TRUNC(H.FECHA_PROC) <= TRUNC(p_FechaHasta))
         ORDER BY H.CODIGO_PROC_MASIVO DESC, D.CODIGO_PROC_MAS_DET;

    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT NULL CODIGO_PROC_MASIVO FROM DUAL WHERE 1 = 0;
END;
/
