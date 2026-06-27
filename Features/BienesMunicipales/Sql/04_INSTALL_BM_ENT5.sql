CREATE OR REPLACE PROCEDURE BM.SP_BM_MOV_GET_BIEN (
    p_CodigoEmpresa IN NUMBER,
    p_CodigoBien IN NUMBER,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
BEGIN
    SELECT COUNT(1)
      INTO p_TotalRecords
      FROM BM.BM_MOV_BIENES
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND CODIGO_BIEN = p_CodigoBien;

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
           AND M.CODIGO_BIEN = p_CodigoBien
         ORDER BY M.FECHA_MOVIMIENTO DESC, M.CODIGO_MOV_BIEN DESC;

    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT NULL CODIGO_MOV_BIEN FROM DUAL WHERE 1 = 0;
END;
/

CREATE OR REPLACE PROCEDURE BM.SP_BM_MOV_INS (
    p_CodigoEmpresa IN NUMBER,
    p_CodigoBien IN NUMBER,
    p_TipoMovimiento IN VARCHAR2,
    p_FechaMovimiento IN DATE,
    p_CodigoDirBien IN NUMBER,
    p_ConceptoMovId IN NUMBER,
    p_CodigoSolMovBien IN NUMBER,
    p_Extra1 IN VARCHAR2,
    p_Extra2 IN VARCHAR2,
    p_Extra3 IN VARCHAR2,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
    v_Codigo NUMBER;
    v_Existe NUMBER;
    v_TipoUlt BM.BM_MOV_BIENES.TIPO_MOVIMIENTO%TYPE;
    v_Tipo VARCHAR2(1);
BEGIN
    v_Tipo := UPPER(SUBSTR(p_TipoMovimiento, 1, 1));

    IF v_Tipo NOT IN ('T', 'D', 'R') THEN
        p_TotalRecords := 0;
        p_Message := 'Tipo de movimiento no permitido.';
        OPEN p_ResultSet FOR SELECT NULL CODIGO_MOV_BIEN FROM DUAL WHERE 1 = 0;
        RETURN;
    END IF;

    IF p_FechaMovimiento IS NULL THEN
        p_TotalRecords := 0;
        p_Message := 'Debe indicar la fecha del movimiento.';
        OPEN p_ResultSet FOR SELECT NULL CODIGO_MOV_BIEN FROM DUAL WHERE 1 = 0;
        RETURN;
    END IF;

    IF NVL(p_CodigoDirBien, 0) = 0 THEN
        p_TotalRecords := 0;
        p_Message := 'Debe indicar la ubicacion.';
        OPEN p_ResultSet FOR SELECT NULL CODIGO_MOV_BIEN FROM DUAL WHERE 1 = 0;
        RETURN;
    END IF;

    IF NVL(p_ConceptoMovId, 0) = 0 THEN
        p_TotalRecords := 0;
        p_Message := 'Debe indicar el concepto del movimiento.';
        OPEN p_ResultSet FOR SELECT NULL CODIGO_MOV_BIEN FROM DUAL WHERE 1 = 0;
        RETURN;
    END IF;

    SELECT COUNT(1)
      INTO v_Existe
      FROM BM.BM_BIENES
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND CODIGO_BIEN = p_CodigoBien;

    IF v_Existe = 0 THEN
        p_TotalRecords := 0;
        p_Message := 'El bien no existe.';
        OPEN p_ResultSet FOR SELECT NULL CODIGO_MOV_BIEN FROM DUAL WHERE 1 = 0;
        RETURN;
    END IF;

    SELECT COUNT(1)
      INTO v_Existe
      FROM BM.BM_DIR_BIEN
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND CODIGO_DIR_BIEN = p_CodigoDirBien;

    IF v_Existe = 0 THEN
        p_TotalRecords := 0;
        p_Message := 'La ubicacion no existe.';
        OPEN p_ResultSet FOR SELECT NULL CODIGO_MOV_BIEN FROM DUAL WHERE 1 = 0;
        RETURN;
    END IF;

    BEGIN
        SELECT TIPO_MOVIMIENTO
          INTO v_TipoUlt
          FROM (
                SELECT TIPO_MOVIMIENTO
                  FROM BM.BM_MOV_BIENES
                 WHERE CODIGO_EMPRESA = p_CodigoEmpresa
                   AND CODIGO_BIEN = p_CodigoBien
                 ORDER BY FECHA_MOVIMIENTO DESC, CODIGO_MOV_BIEN DESC
               )
         WHERE ROWNUM = 1;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            v_TipoUlt := NULL;
    END;

    IF v_Tipo IN ('T', 'D') AND v_TipoUlt IN ('D', 'E') THEN
        p_TotalRecords := 0;
        p_Message := 'El bien tiene un movimiento final y no admite nuevos movimientos.';
        OPEN p_ResultSet FOR SELECT NULL CODIGO_MOV_BIEN FROM DUAL WHERE 1 = 0;
        RETURN;
    END IF;

    IF v_Tipo = 'R' AND NVL(v_TipoUlt, ' ') NOT IN ('D', 'E') THEN
        p_TotalRecords := 0;
        p_Message := 'Solo se permite reincorporar bienes con ultimo movimiento final.';
        OPEN p_ResultSet FOR SELECT NULL CODIGO_MOV_BIEN FROM DUAL WHERE 1 = 0;
        RETURN;
    END IF;

    SELECT NVL(MAX(CODIGO_MOV_BIEN), 0) + 1
      INTO v_Codigo
      FROM BM.BM_MOV_BIENES;

    INSERT INTO BM.BM_MOV_BIENES (
        CODIGO_MOV_BIEN,
        CODIGO_BIEN,
        TIPO_MOVIMIENTO,
        FECHA_MOVIMIENTO,
        CODIGO_DIR_BIEN,
        EXTRA1,
        EXTRA2,
        EXTRA3,
        FECHA_INS,
        CODIGO_EMPRESA,
        CONCEPTO_MOV_ID,
        CODIGO_SOL_MOV_BIEN
    ) VALUES (
        v_Codigo,
        p_CodigoBien,
        v_Tipo,
        p_FechaMovimiento,
        p_CodigoDirBien,
        p_Extra1,
        p_Extra2,
        p_Extra3,
        SYSDATE,
        p_CodigoEmpresa,
        p_ConceptoMovId,
        p_CodigoSolMovBien
    );

    COMMIT;

    BM.SP_BM_MOV_GET_BIEN(p_CodigoEmpresa, p_CodigoBien, p_ResultSet, p_Message, p_TotalRecords);
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT NULL CODIGO_MOV_BIEN FROM DUAL WHERE 1 = 0;
END;
/

CREATE OR REPLACE PROCEDURE BM.SP_BM_SOL_MOV_GET (
    p_CodigoEmpresa IN NUMBER,
    p_Aprobado IN NUMBER,
    p_SearchText IN VARCHAR2,
    p_TipoMovimiento IN VARCHAR2,
    p_FechaDesde IN DATE,
    p_FechaHasta IN DATE,
    p_CodigoDirBien IN NUMBER,
    p_Page IN NUMBER,
    p_PageSize IN NUMBER,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
    v_Page NUMBER := NVL(p_Page, 1);
    v_PageSize NUMBER := NVL(p_PageSize, 50);
    v_FromRow NUMBER;
    v_ToRow NUMBER;
BEGIN
    v_FromRow := ((v_Page - 1) * v_PageSize) + 1;
    v_ToRow := v_Page * v_PageSize;

    SELECT COUNT(1)
      INTO p_TotalRecords
      FROM BM.BM_SOL_MOV_BIENES S,
           BM.BM_BIENES B,
           BM.BM_ARTICULOS A
     WHERE B.CODIGO_BIEN = S.CODIGO_BIEN
       AND A.CODIGO_ARTICULO = B.CODIGO_ARTICULO
       AND S.CODIGO_EMPRESA = p_CodigoEmpresa
       AND (p_Aprobado < 0 OR NVL(S.APROBADO, 0) = p_Aprobado)
       AND (p_TipoMovimiento IS NULL OR S.TIPO_MOVIMIENTO = SUBSTR(p_TipoMovimiento, 1, 1))
       AND (p_FechaDesde IS NULL OR TRUNC(S.FECHA_MOVIMIENTO) >= TRUNC(p_FechaDesde))
       AND (p_FechaHasta IS NULL OR TRUNC(S.FECHA_MOVIMIENTO) <= TRUNC(p_FechaHasta))
       AND (NVL(p_CodigoDirBien, 0) = 0 OR S.CODIGO_DIR_BIEN = p_CodigoDirBien)
       AND (
            p_SearchText IS NULL
            OR UPPER(B.NUMERO_PLACA) LIKE '%' || UPPER(p_SearchText) || '%'
            OR UPPER(A.DENOMINACION) LIKE '%' || UPPER(p_SearchText) || '%'
            OR UPPER(NVL(S.NUMERO_SOLICITUD, '')) LIKE '%' || UPPER(p_SearchText) || '%'
       );

    OPEN p_ResultSet FOR
        SELECT *
          FROM (
                SELECT X.*, ROWNUM RN
                  FROM (
                        SELECT S.CODIGO_SOL_MOV_BIEN,
                               S.CODIGO_BIEN,
                               B.NUMERO_PLACA,
                               A.DENOMINACION ARTICULO,
                               S.TIPO_MOVIMIENTO,
                               TM.DESCRIPCION TIPO_MOVIMIENTO_DESC,
                               S.FECHA_MOVIMIENTO,
                               S.CODIGO_DIR_BIEN,
                               NVL(D.CODIGO_ICP, 0) CODIGO_ICP,
                               NVL(P.UNIDAD_EJECUTORA, P.DENOMINACION) UNIDAD_EJECUTORA,
                               NVL(S.CONCEPTO_MOV_ID, 0) CONCEPTO_MOV_ID,
                               CM.DESCRIPCION CONCEPTO_MOVIMIENTO,
                               S.NUMERO_SOLICITUD,
                               NVL(S.APROBADO, 0) APROBADO,
                               NVL(S.USUARIO_SOLICITA, 0) USUARIO_SOLICITA,
                               S.FECHA_SOLICITA,
                               S.FECHA_INCIDENCIA,
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
                           AND (NVL(p_CodigoDirBien, 0) = 0 OR S.CODIGO_DIR_BIEN = p_CodigoDirBien)
                           AND (
                                p_SearchText IS NULL
                                OR UPPER(B.NUMERO_PLACA) LIKE '%' || UPPER(p_SearchText) || '%'
                                OR UPPER(A.DENOMINACION) LIKE '%' || UPPER(p_SearchText) || '%'
                                OR UPPER(NVL(S.NUMERO_SOLICITUD, '')) LIKE '%' || UPPER(p_SearchText) || '%'
                           )
                         ORDER BY S.FECHA_SOLICITA DESC, S.CODIGO_SOL_MOV_BIEN DESC
                       ) X
                 WHERE ROWNUM <= v_ToRow
               )
         WHERE RN >= v_FromRow;

    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT NULL CODIGO_SOL_MOV_BIEN FROM DUAL WHERE 1 = 0;
END;
/

CREATE OR REPLACE PROCEDURE BM.SP_BM_SOL_MOV_INS (
    p_CodigoEmpresa IN NUMBER,
    p_CodigoBien IN NUMBER,
    p_TipoMovimiento IN VARCHAR2,
    p_FechaMovimiento IN DATE,
    p_CodigoDirBien IN NUMBER,
    p_ConceptoMovId IN NUMBER,
    p_NumeroSolicitud IN VARCHAR2,
    p_UsuarioSolicita IN NUMBER,
    p_FechaIncidencia IN DATE,
    p_NotaIncidencia IN VARCHAR2,
    p_Extra1 IN VARCHAR2,
    p_Extra2 IN VARCHAR2,
    p_Extra3 IN VARCHAR2,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
    v_CodigoSol NUMBER;
    v_CodigoMov NUMBER;
    v_Existe NUMBER;
    v_Tipo VARCHAR2(1);
    v_NumeroSolicitud VARCHAR2(50);
BEGIN
    v_Tipo := UPPER(SUBSTR(p_TipoMovimiento, 1, 1));

    IF NVL(p_CodigoBien, 0) = 0 THEN
        p_TotalRecords := 0;
        p_Message := 'Debe indicar el bien.';
        OPEN p_ResultSet FOR SELECT NULL CODIGO_SOL_MOV_BIEN FROM DUAL WHERE 1 = 0;
        RETURN;
    END IF;

    IF v_Tipo NOT IN ('T', 'D', 'R') THEN
        p_TotalRecords := 0;
        p_Message := 'Tipo de movimiento no permitido para solicitud.';
        OPEN p_ResultSet FOR SELECT NULL CODIGO_SOL_MOV_BIEN FROM DUAL WHERE 1 = 0;
        RETURN;
    END IF;

    IF p_FechaMovimiento IS NULL THEN
        p_TotalRecords := 0;
        p_Message := 'Debe indicar la fecha del movimiento.';
        OPEN p_ResultSet FOR SELECT NULL CODIGO_SOL_MOV_BIEN FROM DUAL WHERE 1 = 0;
        RETURN;
    END IF;

    IF NVL(p_CodigoDirBien, 0) = 0 THEN
        p_TotalRecords := 0;
        p_Message := 'Debe indicar la ubicacion.';
        OPEN p_ResultSet FOR SELECT NULL CODIGO_SOL_MOV_BIEN FROM DUAL WHERE 1 = 0;
        RETURN;
    END IF;

    IF NVL(p_ConceptoMovId, 0) = 0 THEN
        p_TotalRecords := 0;
        p_Message := 'Debe indicar el concepto del movimiento.';
        OPEN p_ResultSet FOR SELECT NULL CODIGO_SOL_MOV_BIEN FROM DUAL WHERE 1 = 0;
        RETURN;
    END IF;

    SELECT COUNT(1)
      INTO v_Existe
      FROM BM.BM_BIENES
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND CODIGO_BIEN = p_CodigoBien;

    IF v_Existe = 0 THEN
        p_TotalRecords := 0;
        p_Message := 'El bien no existe.';
        OPEN p_ResultSet FOR SELECT NULL CODIGO_SOL_MOV_BIEN FROM DUAL WHERE 1 = 0;
        RETURN;
    END IF;

    SELECT COUNT(1)
      INTO v_Existe
      FROM BM.BM_DIR_BIEN
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND CODIGO_DIR_BIEN = p_CodigoDirBien;

    IF v_Existe = 0 THEN
        p_TotalRecords := 0;
        p_Message := 'La ubicacion no existe.';
        OPEN p_ResultSet FOR SELECT NULL CODIGO_SOL_MOV_BIEN FROM DUAL WHERE 1 = 0;
        RETURN;
    END IF;

    SELECT COUNT(1)
      INTO v_Existe
      FROM BM.BM_SOL_MOV_BIENES
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND CODIGO_BIEN = p_CodigoBien
       AND TIPO_MOVIMIENTO = v_Tipo
       AND NVL(APROBADO, 0) = 0;

    IF v_Existe > 0 THEN
        p_TotalRecords := 0;
        p_Message := 'Ya existe una solicitud pendiente para este bien y tipo de movimiento.';
        OPEN p_ResultSet FOR SELECT NULL CODIGO_SOL_MOV_BIEN FROM DUAL WHERE 1 = 0;
        RETURN;
    END IF;

    SELECT NVL(MAX(CODIGO_SOL_MOV_BIEN), 0) + 1
      INTO v_CodigoSol
      FROM BM.BM_SOL_MOV_BIENES;

    SELECT NVL(MAX(CODIGO_MOV_BIEN), 0) + 1
      INTO v_CodigoMov
      FROM BM.BM_SOL_MOV_BIENES;

    v_NumeroSolicitud := NVL(TRIM(p_NumeroSolicitud), 'SOL-' || LPAD(v_CodigoSol, 8, '0'));

    INSERT INTO BM.BM_SOL_MOV_BIENES (
        CODIGO_MOV_BIEN,
        CODIGO_BIEN,
        TIPO_MOVIMIENTO,
        FECHA_MOVIMIENTO,
        CODIGO_DIR_BIEN,
        EXTRA1,
        EXTRA2,
        EXTRA3,
        FECHA_INS,
        CODIGO_EMPRESA,
        CONCEPTO_MOV_ID,
        CODIGO_SOL_MOV_BIEN,
        NUMERO_SOLICITUD,
        APROBADO,
        USUARIO_SOLICITA,
        FECHA_SOLICITA,
        FECHA_INCIDENCIA,
        NOTA_INCIDENCIA
    ) VALUES (
        v_CodigoMov,
        p_CodigoBien,
        v_Tipo,
        p_FechaMovimiento,
        p_CodigoDirBien,
        p_Extra1,
        p_Extra2,
        p_Extra3,
        SYSDATE,
        p_CodigoEmpresa,
        p_ConceptoMovId,
        v_CodigoSol,
        v_NumeroSolicitud,
        0,
        p_UsuarioSolicita,
        SYSDATE,
        p_FechaIncidencia,
        p_NotaIncidencia
    );

    COMMIT;

    OPEN p_ResultSet FOR
        SELECT S.CODIGO_SOL_MOV_BIEN,
               S.CODIGO_BIEN,
               B.NUMERO_PLACA,
               A.DENOMINACION ARTICULO,
               S.TIPO_MOVIMIENTO,
               TM.DESCRIPCION TIPO_MOVIMIENTO_DESC,
               S.FECHA_MOVIMIENTO,
               S.CODIGO_DIR_BIEN,
               NVL(D.CODIGO_ICP, 0) CODIGO_ICP,
               NVL(P.UNIDAD_EJECUTORA, P.DENOMINACION) UNIDAD_EJECUTORA,
               NVL(S.CONCEPTO_MOV_ID, 0) CONCEPTO_MOV_ID,
               CM.DESCRIPCION CONCEPTO_MOVIMIENTO,
               S.NUMERO_SOLICITUD,
               NVL(S.APROBADO, 0) APROBADO,
               NVL(S.USUARIO_SOLICITA, 0) USUARIO_SOLICITA,
               S.FECHA_SOLICITA,
               S.FECHA_INCIDENCIA,
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
           AND S.CODIGO_SOL_MOV_BIEN = v_CodigoSol;

    p_TotalRecords := 1;
    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT NULL CODIGO_SOL_MOV_BIEN FROM DUAL WHERE 1 = 0;
END;
/

CREATE OR REPLACE PROCEDURE BM.SP_BM_SOL_MOV_APR (
    p_CodigoEmpresa IN NUMBER,
    p_CodigoSolMovBien IN NUMBER,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
    v_CodigoMov NUMBER;
    v_CodigoBien NUMBER;
    v_Tipo BM.BM_SOL_MOV_BIENES.TIPO_MOVIMIENTO%TYPE;
    v_Fecha DATE;
    v_Dir NUMBER;
    v_Concepto NUMBER;
    v_Extra1 VARCHAR2(100);
    v_Extra2 VARCHAR2(100);
    v_Extra3 VARCHAR2(100);
    v_TipoUlt BM.BM_MOV_BIENES.TIPO_MOVIMIENTO%TYPE;
BEGIN
    SELECT CODIGO_BIEN, TIPO_MOVIMIENTO, FECHA_MOVIMIENTO, CODIGO_DIR_BIEN,
           CONCEPTO_MOV_ID, EXTRA1, EXTRA2, EXTRA3
      INTO v_CodigoBien, v_Tipo, v_Fecha, v_Dir, v_Concepto, v_Extra1, v_Extra2, v_Extra3
      FROM BM.BM_SOL_MOV_BIENES
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND CODIGO_SOL_MOV_BIEN = p_CodigoSolMovBien
       AND NVL(APROBADO, 0) = 0;

    v_Tipo := UPPER(SUBSTR(v_Tipo, 1, 1));

    IF v_Tipo NOT IN ('T', 'D', 'R') THEN
        p_TotalRecords := 0;
        p_Message := 'Tipo de movimiento no permitido para aprobacion.';
        OPEN p_ResultSet FOR SELECT NULL CODIGO_SOL_MOV_BIEN FROM DUAL WHERE 1 = 0;
        RETURN;
    END IF;

    IF v_Fecha IS NULL THEN
        p_TotalRecords := 0;
        p_Message := 'La solicitud no tiene fecha de movimiento.';
        OPEN p_ResultSet FOR SELECT NULL CODIGO_SOL_MOV_BIEN FROM DUAL WHERE 1 = 0;
        RETURN;
    END IF;

    IF NVL(v_Dir, 0) = 0 THEN
        p_TotalRecords := 0;
        p_Message := 'La solicitud no tiene ubicacion.';
        OPEN p_ResultSet FOR SELECT NULL CODIGO_SOL_MOV_BIEN FROM DUAL WHERE 1 = 0;
        RETURN;
    END IF;

    IF NVL(v_Concepto, 0) = 0 THEN
        p_TotalRecords := 0;
        p_Message := 'La solicitud no tiene concepto de movimiento.';
        OPEN p_ResultSet FOR SELECT NULL CODIGO_SOL_MOV_BIEN FROM DUAL WHERE 1 = 0;
        RETURN;
    END IF;

    BEGIN
        SELECT TIPO_MOVIMIENTO
          INTO v_TipoUlt
          FROM (
                SELECT TIPO_MOVIMIENTO
                  FROM BM.BM_MOV_BIENES
                 WHERE CODIGO_EMPRESA = p_CodigoEmpresa
                   AND CODIGO_BIEN = v_CodigoBien
                 ORDER BY FECHA_MOVIMIENTO DESC, CODIGO_MOV_BIEN DESC
               )
         WHERE ROWNUM = 1;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            v_TipoUlt := NULL;
    END;

    IF v_Tipo IN ('T', 'D') AND v_TipoUlt IN ('D', 'E') THEN
        p_TotalRecords := 0;
        p_Message := 'El bien tiene un movimiento final y no admite nuevos movimientos.';
        OPEN p_ResultSet FOR SELECT NULL CODIGO_SOL_MOV_BIEN FROM DUAL WHERE 1 = 0;
        RETURN;
    END IF;

    IF v_Tipo = 'R' AND NVL(v_TipoUlt, ' ') NOT IN ('D', 'E') THEN
        p_TotalRecords := 0;
        p_Message := 'Solo se permite reincorporar bienes con ultimo movimiento final.';
        OPEN p_ResultSet FOR SELECT NULL CODIGO_SOL_MOV_BIEN FROM DUAL WHERE 1 = 0;
        RETURN;
    END IF;

    SELECT NVL(MAX(CODIGO_MOV_BIEN), 0) + 1
      INTO v_CodigoMov
      FROM BM.BM_MOV_BIENES;

    INSERT INTO BM.BM_MOV_BIENES (
        CODIGO_MOV_BIEN, CODIGO_BIEN, TIPO_MOVIMIENTO, FECHA_MOVIMIENTO,
        CODIGO_DIR_BIEN, EXTRA1, EXTRA2, EXTRA3, FECHA_INS, CODIGO_EMPRESA,
        CONCEPTO_MOV_ID, CODIGO_SOL_MOV_BIEN
    ) VALUES (
        v_CodigoMov, v_CodigoBien, v_Tipo, v_Fecha,
        v_Dir, v_Extra1, v_Extra2, v_Extra3, SYSDATE, p_CodigoEmpresa,
        v_Concepto, p_CodigoSolMovBien
    );

    UPDATE BM.BM_SOL_MOV_BIENES
       SET APROBADO = 1,
           FECHA_UPD = SYSDATE,
           CODIGO_MOV_BIEN = v_CodigoMov
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND CODIGO_SOL_MOV_BIEN = p_CodigoSolMovBien;

    COMMIT;

    OPEN p_ResultSet FOR
        SELECT S.CODIGO_SOL_MOV_BIEN,
               S.CODIGO_BIEN,
               B.NUMERO_PLACA,
               A.DENOMINACION ARTICULO,
               S.TIPO_MOVIMIENTO,
               TM.DESCRIPCION TIPO_MOVIMIENTO_DESC,
               S.FECHA_MOVIMIENTO,
               S.CODIGO_DIR_BIEN,
               NVL(D.CODIGO_ICP, 0) CODIGO_ICP,
               NVL(P.UNIDAD_EJECUTORA, P.DENOMINACION) UNIDAD_EJECUTORA,
               NVL(S.CONCEPTO_MOV_ID, 0) CONCEPTO_MOV_ID,
               CM.DESCRIPCION CONCEPTO_MOVIMIENTO,
               S.NUMERO_SOLICITUD,
               NVL(S.APROBADO, 0) APROBADO,
               NVL(S.USUARIO_SOLICITA, 0) USUARIO_SOLICITA,
               S.FECHA_SOLICITA,
               S.FECHA_INCIDENCIA,
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
           AND S.CODIGO_SOL_MOV_BIEN = p_CodigoSolMovBien;

    p_TotalRecords := 1;
    p_Message := 'success';
EXCEPTION
    WHEN NO_DATA_FOUND THEN
        p_TotalRecords := 0;
        p_Message := 'Solicitud no encontrada o ya aprobada.';
        OPEN p_ResultSet FOR SELECT NULL CODIGO_SOL_MOV_BIEN FROM DUAL WHERE 1 = 0;
    WHEN OTHERS THEN
        ROLLBACK;
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT NULL CODIGO_SOL_MOV_BIEN FROM DUAL WHERE 1 = 0;
END;
/
