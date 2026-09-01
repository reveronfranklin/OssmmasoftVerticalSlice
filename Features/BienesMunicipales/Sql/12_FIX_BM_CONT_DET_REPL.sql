-- Corrige el nombre de la columna de replica usado por los procedimientos
-- de detalle de conteo. La columna existente es REPLICAR_COMENTARIO.

CREATE OR REPLACE PROCEDURE BMC.SP_BM_CONTEO_CERRAR (
    p_CodigoEmpresa IN NUMBER,
    p_CodigoBmConteo IN NUMBER,
    p_Comentario IN VARCHAR2,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
BEGIN
    INSERT INTO BMC.BM_CONTEO_HISTORICO (
        CODIGO_BM_CONTEO,
        TITULO,
        CODIGO_PERSONA_RESPONSABLE,
        CANTIDAD_CONTEOS_ID,
        FECHA,
        USUARIO_INS,
        FECHA_INS,
        USUARIO_UPD,
        FECHA_UPD,
        CODIGO_EMPRESA,
        COMENTARIO,
        USUARIO_CIERRE,
        FECHA_CIERRE,
        TOTAL_CANTIDAD,
        TOTAL_CANTIDAD_CONTADA,
        TOTAL_DIFERENCIA
    )
    SELECT C.CODIGO_BM_CONTEO,
           C.TITULO,
           C.CODIGO_PERSONA_RESPONSABLE,
           C.CANTIDAD_CONTEOS_ID,
           C.FECHA,
           C.USUARIO_INS,
           C.FECHA_INS,
           C.USUARIO_UPD,
           C.FECHA_UPD,
           C.CODIGO_EMPRESA,
           NVL(p_Comentario, C.COMENTARIO),
           -1,
           SYSDATE,
           NVL(SUM(D.CANTIDAD), 0),
           NVL(SUM(D.CANTIDAD_CONTADA), 0),
           NVL(SUM(D.DIFERENCIA), 0)
      FROM BMC.BM_CONTEO C,
           BMC.BM_CONTEO_DETALLE D
     WHERE C.CODIGO_EMPRESA = p_CodigoEmpresa
       AND C.CODIGO_BM_CONTEO = p_CodigoBmConteo
       AND D.CODIGO_BM_CONTEO(+) = C.CODIGO_BM_CONTEO
     GROUP BY C.CODIGO_BM_CONTEO,
              C.TITULO,
              C.CODIGO_PERSONA_RESPONSABLE,
              C.CANTIDAD_CONTEOS_ID,
              C.FECHA,
              C.USUARIO_INS,
              C.FECHA_INS,
              C.USUARIO_UPD,
              C.FECHA_UPD,
              C.CODIGO_EMPRESA,
              C.COMENTARIO;

    INSERT INTO BMC.BM_CONTEO_DETALLE_HISTORICO (
        CODIGO_BM_CONTEO,
        CONTEO,
        CODIGO_ICP,
        UNIDAD_TRABAJO,
        CODIGO_GRUPO,
        CODIGO_NIVEL1,
        CODIGO_NIVEL2,
        NUMERO_LOTE,
        CANTIDAD,
        NUMERO_PLACA,
        VALOR_ACTUAL,
        ARTICULO,
        ESPECIFICACION,
        SERVICIO,
        RESPONSABLE_BIEN,
        FECHA_MOVIMIENTO,
        CODIGO_BIEN,
        CODIGO_MOV_BIEN,
        CANTIDAD_CONTADA,
        DIFERENCIA,
        CODIGO_EMPRESA,
        USUARIO_INS,
        FECHA_INS,
        USUARIO_UPD,
        FECHA_UPD,
        COMENTARIO,
        REPLICAR_COMENTARIO,
        CODIGO_BM_CONTEO_MOTIVO
    )
    SELECT CODIGO_BM_CONTEO,
           CONTEO,
           CODIGO_ICP,
           UNIDAD_TRABAJO,
           CODIGO_GRUPO,
           CODIGO_NIVEL1,
           CODIGO_NIVEL2,
           NUMERO_LOTE,
           CANTIDAD,
           NUMERO_PLACA,
           VALOR_ACTUAL,
           ARTICULO,
           ESPECIFICACION,
           SERVICIO,
           RESPONSABLE_BIEN,
           FECHA_MOVIMIENTO,
           CODIGO_BIEN,
           CODIGO_MOV_BIEN,
           CANTIDAD_CONTADA,
           DIFERENCIA,
           CODIGO_EMPRESA,
           USUARIO_INS,
           FECHA_INS,
           USUARIO_UPD,
           FECHA_UPD,
           COMENTARIO,
           REPLICAR_COMENTARIO,
           CODIGO_BM_CONTEO_MOTIVO
      FROM BMC.BM_CONTEO_DETALLE
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND CODIGO_BM_CONTEO = p_CodigoBmConteo;

    DELETE FROM BMC.BM_CONTEO_DETALLE
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND CODIGO_BM_CONTEO = p_CodigoBmConteo;

    DELETE FROM BMC.BM_CONTEO
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND CODIGO_BM_CONTEO = p_CodigoBmConteo;

    BMC.SP_BM_CONTEO_GET_ALL(p_CodigoEmpresa, p_ResultSet, p_Message, p_TotalRecords);
EXCEPTION
    WHEN DUP_VAL_ON_INDEX THEN
        p_TotalRecords := 0;
        p_Message := 'El conteo ya fue cerrado.';
        OPEN p_ResultSet FOR SELECT * FROM BMC.BM_CONTEO WHERE 1 = 0;
    WHEN OTHERS THEN
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT * FROM BMC.BM_CONTEO WHERE 1 = 0;
END SP_BM_CONTEO_CERRAR;
/

CREATE OR REPLACE PROCEDURE BMC.SP_BM_CONT_DET_GET (
    p_CodigoEmpresa IN NUMBER,
    p_CodigoBmConteo IN NUMBER,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
BEGIN
    SELECT COUNT(*)
      INTO p_TotalRecords
      FROM BMC.BM_CONTEO_DETALLE D
     WHERE D.CODIGO_EMPRESA = p_CodigoEmpresa
       AND D.CODIGO_BM_CONTEO = p_CodigoBmConteo;

    OPEN p_ResultSet FOR
        SELECT D.CODIGO_BM_CONTEO_DETALLE,
               D.CODIGO_BM_CONTEO,
               D.CONTEO,
               D.CODIGO_ICP,
               D.UNIDAD_TRABAJO,
               D.COMENTARIO,
               D.NUMERO_PLACA CODIGO_PLACA,
               D.CANTIDAD,
               D.CANTIDAD_CONTADA,
               0 CANTIDAD_CONTADA_OTRO,
               D.DIFERENCIA,
               D.CODIGO_GRUPO,
               D.CODIGO_NIVEL1,
               D.CODIGO_NIVEL2,
               D.NUMERO_LOTE,
               D.NUMERO_PLACA,
               D.VALOR_ACTUAL,
               D.ARTICULO,
               D.ESPECIFICACION,
               D.SERVICIO,
               D.RESPONSABLE_BIEN,
               D.FECHA_MOVIMIENTO,
               D.CODIGO_BIEN,
               D.CODIGO_MOV_BIEN,
               C.FECHA,
               NVL(D.REPLICAR_COMENTARIO, 0) REPLICAR_COMENTARIO
          FROM BMC.BM_CONTEO_DETALLE D,
               BMC.BM_CONTEO C
         WHERE D.CODIGO_EMPRESA = p_CodigoEmpresa
           AND D.CODIGO_BM_CONTEO = p_CodigoBmConteo
           AND C.CODIGO_BM_CONTEO(+) = D.CODIGO_BM_CONTEO
         ORDER BY D.UNIDAD_TRABAJO, D.ARTICULO, D.NUMERO_PLACA, D.CONTEO;

    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT * FROM BMC.BM_CONTEO_DETALLE WHERE 1 = 0;
END SP_BM_CONT_DET_GET;
/

CREATE OR REPLACE PROCEDURE BMC.SP_BM_CONT_DET_CMP (
    p_CodigoEmpresa IN NUMBER,
    p_CodigoBmConteo IN NUMBER,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
BEGIN
    BMC.SP_BM_CONT_DET_GET(p_CodigoEmpresa, p_CodigoBmConteo, p_ResultSet, p_Message, p_TotalRecords);
END SP_BM_CONT_DET_CMP;
/

CREATE OR REPLACE PROCEDURE BMC.SP_BM_CONT_DET_UPD (
    p_CodigoEmpresa IN NUMBER,
    p_CodigoDetalle IN NUMBER,
    p_CantidadContada IN NUMBER,
    p_Comentario IN VARCHAR2,
    p_ReplicarComentario IN NUMBER,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
    v_Conteo NUMBER;
BEGIN
    SELECT CODIGO_BM_CONTEO
      INTO v_Conteo
      FROM BMC.BM_CONTEO_DETALLE
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND CODIGO_BM_CONTEO_DETALLE = p_CodigoDetalle;

    UPDATE BMC.BM_CONTEO_DETALLE
       SET CANTIDAD_CONTADA = p_CantidadContada,
           DIFERENCIA = NVL(CANTIDAD, 0) - NVL(p_CantidadContada, 0),
           COMENTARIO = p_Comentario,
           REPLICAR_COMENTARIO = p_ReplicarComentario,
           FECHA_UPD = SYSDATE
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND CODIGO_BM_CONTEO_DETALLE = p_CodigoDetalle;

    IF p_ReplicarComentario = 1 THEN
        UPDATE BMC.BM_CONTEO_DETALLE
           SET COMENTARIO = p_Comentario,
               REPLICAR_COMENTARIO = p_ReplicarComentario,
               FECHA_UPD = SYSDATE
         WHERE CODIGO_EMPRESA = p_CodigoEmpresa
           AND CODIGO_BM_CONTEO = v_Conteo;
    END IF;

    BMC.SP_BM_CONT_DET_GET(p_CodigoEmpresa, v_Conteo, p_ResultSet, p_Message, p_TotalRecords);
EXCEPTION
    WHEN OTHERS THEN
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT * FROM BMC.BM_CONTEO_DETALLE WHERE 1 = 0;
END SP_BM_CONT_DET_UPD;
/
