CREATE OR REPLACE PROCEDURE BM.SP_BM_TIT_GET_ALL (
    p_CodigoEmpresa IN NUMBER,
    p_SearchText IN VARCHAR2,
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
      FROM BM.BM_TITULOS
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND (p_SearchText IS NULL OR UPPER(TITULO) LIKE '%' || UPPER(p_SearchText) || '%');

    OPEN p_ResultSet FOR
        SELECT TITULO_ID, NVL(TITULO_FK_ID, 0) TITULO_FK_ID, TITULO, CODIGO, EXTRA1, EXTRA2, EXTRA3
          FROM (
                SELECT T.*, ROWNUM RN
                  FROM (
                        SELECT *
                          FROM BM.BM_TITULOS
                         WHERE CODIGO_EMPRESA = p_CodigoEmpresa
                           AND (p_SearchText IS NULL OR UPPER(TITULO) LIKE '%' || UPPER(p_SearchText) || '%')
                         ORDER BY TITULO
                       ) T
                 WHERE ROWNUM <= v_ToRow
               )
         WHERE RN >= v_FromRow;

    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT NULL TITULO_ID FROM DUAL WHERE 1 = 0;
END;
/

CREATE OR REPLACE PROCEDURE BM.SP_BM_TIT_INS (
    p_CodigoEmpresa IN NUMBER,
    p_TituloId IN NUMBER,
    p_TituloFkId IN NUMBER,
    p_Titulo IN VARCHAR2,
    p_Codigo IN VARCHAR2,
    p_Extra1 IN VARCHAR2,
    p_Extra2 IN VARCHAR2,
    p_Extra3 IN VARCHAR2,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
    v_TituloId NUMBER;
BEGIN
    IF TRIM(p_Titulo) IS NULL THEN
        p_TotalRecords := 0;
        p_Message := 'Debe indicar el titulo.';
        OPEN p_ResultSet FOR SELECT NULL TITULO_ID FROM DUAL WHERE 1 = 0;
        RETURN;
    END IF;

    SELECT NVL(MAX(TITULO_ID), 0) + 1 INTO v_TituloId FROM BM.BM_TITULOS;

    INSERT INTO BM.BM_TITULOS (
        TITULO_ID, TITULO_FK_ID, TITULO, CODIGO, EXTRA1, EXTRA2, EXTRA3, FECHA_INS, CODIGO_EMPRESA
    ) VALUES (
        v_TituloId, p_TituloFkId, p_Titulo, p_Codigo, p_Extra1, p_Extra2, p_Extra3, SYSDATE, p_CodigoEmpresa
    );

    COMMIT;

    OPEN p_ResultSet FOR
        SELECT TITULO_ID, NVL(TITULO_FK_ID, 0) TITULO_FK_ID, TITULO, CODIGO, EXTRA1, EXTRA2, EXTRA3
          FROM BM.BM_TITULOS
         WHERE CODIGO_EMPRESA = p_CodigoEmpresa
           AND TITULO_ID = v_TituloId;

    p_TotalRecords := 1;
    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT NULL TITULO_ID FROM DUAL WHERE 1 = 0;
END;
/

CREATE OR REPLACE PROCEDURE BM.SP_BM_TIT_UPD (
    p_CodigoEmpresa IN NUMBER,
    p_TituloId IN NUMBER,
    p_TituloFkId IN NUMBER,
    p_Titulo IN VARCHAR2,
    p_Codigo IN VARCHAR2,
    p_Extra1 IN VARCHAR2,
    p_Extra2 IN VARCHAR2,
    p_Extra3 IN VARCHAR2,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
BEGIN
    IF TRIM(p_Titulo) IS NULL THEN
        p_TotalRecords := 0;
        p_Message := 'Debe indicar el titulo.';
        OPEN p_ResultSet FOR SELECT NULL TITULO_ID FROM DUAL WHERE 1 = 0;
        RETURN;
    END IF;

    UPDATE BM.BM_TITULOS
       SET TITULO_FK_ID = p_TituloFkId,
           TITULO = p_Titulo,
           CODIGO = p_Codigo,
           EXTRA1 = p_Extra1,
           EXTRA2 = p_Extra2,
           EXTRA3 = p_Extra3,
           FECHA_UPD = SYSDATE
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND TITULO_ID = p_TituloId;

    COMMIT;

    OPEN p_ResultSet FOR
        SELECT TITULO_ID, NVL(TITULO_FK_ID, 0) TITULO_FK_ID, TITULO, CODIGO, EXTRA1, EXTRA2, EXTRA3
          FROM BM.BM_TITULOS
         WHERE CODIGO_EMPRESA = p_CodigoEmpresa
           AND TITULO_ID = p_TituloId;

    p_TotalRecords := SQL%ROWCOUNT;
    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT NULL TITULO_ID FROM DUAL WHERE 1 = 0;
END;
/

CREATE OR REPLACE PROCEDURE BM.SP_BM_DESC_GET_ALL (
    p_CodigoEmpresa IN NUMBER,
    p_TituloId IN NUMBER,
    p_SearchText IN VARCHAR2,
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
      FROM BM.BM_DESCRIPTIVAS
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND (p_TituloId = 0 OR TITULO_ID = p_TituloId)
       AND (p_SearchText IS NULL OR UPPER(DESCRIPCION) LIKE '%' || UPPER(p_SearchText) || '%');

    OPEN p_ResultSet FOR
        SELECT DESCRIPCION_ID ID, NVL(DESCRIPCION_FK_ID, 0) DESCRIPCION_ID, TITULO_ID, DESCRIPCION, CODIGO, EXTRA1, EXTRA2, EXTRA3
          FROM (
                SELECT D.*, ROWNUM RN
                  FROM (
                        SELECT *
                          FROM BM.BM_DESCRIPTIVAS
                         WHERE CODIGO_EMPRESA = p_CodigoEmpresa
                           AND (p_TituloId = 0 OR TITULO_ID = p_TituloId)
                           AND (p_SearchText IS NULL OR UPPER(DESCRIPCION) LIKE '%' || UPPER(p_SearchText) || '%')
                         ORDER BY DESCRIPCION
                       ) D
                 WHERE ROWNUM <= v_ToRow
               )
         WHERE RN >= v_FromRow;

    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT NULL ID FROM DUAL WHERE 1 = 0;
END;
/

CREATE OR REPLACE PROCEDURE BM.SP_BM_DESC_INS (
    p_CodigoEmpresa IN NUMBER,
    p_DescripcionId IN NUMBER,
    p_DescripcionFkId IN NUMBER,
    p_TituloId IN NUMBER,
    p_Descripcion IN VARCHAR2,
    p_Codigo IN VARCHAR2,
    p_Extra1 IN VARCHAR2,
    p_Extra2 IN VARCHAR2,
    p_Extra3 IN VARCHAR2,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
    v_DescripcionId NUMBER;
    v_Existe NUMBER;
BEGIN
    IF NVL(p_TituloId, 0) = 0 OR TRIM(p_Descripcion) IS NULL THEN
        p_TotalRecords := 0;
        p_Message := 'Debe indicar titulo y descripcion.';
        OPEN p_ResultSet FOR SELECT NULL ID FROM DUAL WHERE 1 = 0;
        RETURN;
    END IF;

    SELECT COUNT(1)
      INTO v_Existe
      FROM BM.BM_TITULOS
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND TITULO_ID = p_TituloId;

    IF v_Existe = 0 THEN
        p_TotalRecords := 0;
        p_Message := 'El titulo de la descriptiva no existe.';
        OPEN p_ResultSet FOR SELECT NULL ID FROM DUAL WHERE 1 = 0;
        RETURN;
    END IF;

    SELECT NVL(MAX(DESCRIPCION_ID), 0) + 1 INTO v_DescripcionId FROM BM.BM_DESCRIPTIVAS;

    INSERT INTO BM.BM_DESCRIPTIVAS (
        DESCRIPCION_ID, DESCRIPCION_FK_ID, TITULO_ID, DESCRIPCION, CODIGO, EXTRA1, EXTRA2, EXTRA3, FECHA_INS, CODIGO_EMPRESA
    ) VALUES (
        v_DescripcionId, p_DescripcionFkId, p_TituloId, p_Descripcion, p_Codigo, p_Extra1, p_Extra2, p_Extra3, SYSDATE, p_CodigoEmpresa
    );

    COMMIT;

    OPEN p_ResultSet FOR
        SELECT DESCRIPCION_ID ID, NVL(DESCRIPCION_FK_ID, 0) DESCRIPCION_ID, TITULO_ID, DESCRIPCION, CODIGO, EXTRA1, EXTRA2, EXTRA3
          FROM BM.BM_DESCRIPTIVAS
         WHERE CODIGO_EMPRESA = p_CodigoEmpresa
           AND DESCRIPCION_ID = v_DescripcionId;

    p_TotalRecords := 1;
    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT NULL ID FROM DUAL WHERE 1 = 0;
END;
/

CREATE OR REPLACE PROCEDURE BM.SP_BM_DESC_UPD (
    p_CodigoEmpresa IN NUMBER,
    p_DescripcionId IN NUMBER,
    p_DescripcionFkId IN NUMBER,
    p_TituloId IN NUMBER,
    p_Descripcion IN VARCHAR2,
    p_Codigo IN VARCHAR2,
    p_Extra1 IN VARCHAR2,
    p_Extra2 IN VARCHAR2,
    p_Extra3 IN VARCHAR2,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
    v_Existe NUMBER;
BEGIN
    IF NVL(p_TituloId, 0) = 0 OR TRIM(p_Descripcion) IS NULL THEN
        p_TotalRecords := 0;
        p_Message := 'Debe indicar titulo y descripcion.';
        OPEN p_ResultSet FOR SELECT NULL ID FROM DUAL WHERE 1 = 0;
        RETURN;
    END IF;

    SELECT COUNT(1)
      INTO v_Existe
      FROM BM.BM_TITULOS
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND TITULO_ID = p_TituloId;

    IF v_Existe = 0 THEN
        p_TotalRecords := 0;
        p_Message := 'El titulo de la descriptiva no existe.';
        OPEN p_ResultSet FOR SELECT NULL ID FROM DUAL WHERE 1 = 0;
        RETURN;
    END IF;

    UPDATE BM.BM_DESCRIPTIVAS
       SET DESCRIPCION_FK_ID = p_DescripcionFkId,
           TITULO_ID = p_TituloId,
           DESCRIPCION = p_Descripcion,
           CODIGO = p_Codigo,
           EXTRA1 = p_Extra1,
           EXTRA2 = p_Extra2,
           EXTRA3 = p_Extra3,
           FECHA_UPD = SYSDATE
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND DESCRIPCION_ID = p_DescripcionId;

    COMMIT;

    OPEN p_ResultSet FOR
        SELECT DESCRIPCION_ID ID, NVL(DESCRIPCION_FK_ID, 0) DESCRIPCION_ID, TITULO_ID, DESCRIPCION, CODIGO, EXTRA1, EXTRA2, EXTRA3
          FROM BM.BM_DESCRIPTIVAS
         WHERE CODIGO_EMPRESA = p_CodigoEmpresa
           AND DESCRIPCION_ID = p_DescripcionId;

    p_TotalRecords := SQL%ROWCOUNT;
    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT NULL ID FROM DUAL WHERE 1 = 0;
END;
/

CREATE OR REPLACE PROCEDURE BM.SP_BM_CLASIF_GET_ALL (
    p_CodigoEmpresa IN NUMBER,
    p_SearchText IN VARCHAR2,
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
      FROM BM.BM_CLASIFICACION_BIENES
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND (p_SearchText IS NULL OR UPPER(DENOMINACION) LIKE '%' || UPPER(p_SearchText) || '%');

    OPEN p_ResultSet FOR
        SELECT CODIGO_CLASIFICACION_BIEN, CODIGO_GRUPO, CODIGO_NIVEL1, CODIGO_NIVEL2, CODIGO_NIVEL3,
               DENOMINACION, DESCRIPCION, FECHA_INI, FECHA_FIN
          FROM (
                SELECT C.*, ROWNUM RN
                  FROM (
                        SELECT *
                          FROM BM.BM_CLASIFICACION_BIENES
                         WHERE CODIGO_EMPRESA = p_CodigoEmpresa
                           AND (p_SearchText IS NULL OR UPPER(DENOMINACION) LIKE '%' || UPPER(p_SearchText) || '%')
                         ORDER BY CODIGO_GRUPO, CODIGO_NIVEL1, CODIGO_NIVEL2, CODIGO_NIVEL3
                       ) C
                 WHERE ROWNUM <= v_ToRow
               )
         WHERE RN >= v_FromRow;

    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT NULL CODIGO_CLASIFICACION_BIEN FROM DUAL WHERE 1 = 0;
END;
/

CREATE OR REPLACE PROCEDURE BM.SP_BM_CLASIF_INS (
    p_CodigoEmpresa IN NUMBER,
    p_CodigoClasifBien IN NUMBER,
    p_CodigoGrupo IN VARCHAR2,
    p_CodigoNivel1 IN VARCHAR2,
    p_CodigoNivel2 IN VARCHAR2,
    p_CodigoNivel3 IN VARCHAR2,
    p_Denominacion IN VARCHAR2,
    p_Descripcion IN VARCHAR2,
    p_FechaIni IN DATE,
    p_FechaFin IN DATE,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
    v_Codigo NUMBER;
BEGIN
    IF TRIM(p_CodigoGrupo) IS NULL OR TRIM(p_Denominacion) IS NULL THEN
        p_TotalRecords := 0;
        p_Message := 'Debe indicar grupo y denominacion.';
        OPEN p_ResultSet FOR SELECT NULL CODIGO_CLASIFICACION_BIEN FROM DUAL WHERE 1 = 0;
        RETURN;
    END IF;

    SELECT NVL(MAX(CODIGO_CLASIFICACION_BIEN), 0) + 1 INTO v_Codigo FROM BM.BM_CLASIFICACION_BIENES;

    INSERT INTO BM.BM_CLASIFICACION_BIENES (
        CODIGO_CLASIFICACION_BIEN, CODIGO_GRUPO, CODIGO_NIVEL1, CODIGO_NIVEL2, CODIGO_NIVEL3,
        DENOMINACION, DESCRIPCION, FECHA_INI, FECHA_FIN, FECHA_INS, CODIGO_EMPRESA
    ) VALUES (
        v_Codigo, p_CodigoGrupo, p_CodigoNivel1, p_CodigoNivel2, p_CodigoNivel3,
        p_Denominacion, p_Descripcion, p_FechaIni, p_FechaFin, SYSDATE, p_CodigoEmpresa
    );

    COMMIT;

    OPEN p_ResultSet FOR
        SELECT CODIGO_CLASIFICACION_BIEN, CODIGO_GRUPO, CODIGO_NIVEL1, CODIGO_NIVEL2, CODIGO_NIVEL3,
               DENOMINACION, DESCRIPCION, FECHA_INI, FECHA_FIN
          FROM BM.BM_CLASIFICACION_BIENES
         WHERE CODIGO_EMPRESA = p_CodigoEmpresa
           AND CODIGO_CLASIFICACION_BIEN = v_Codigo;

    p_TotalRecords := 1;
    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT NULL CODIGO_CLASIFICACION_BIEN FROM DUAL WHERE 1 = 0;
END;
/

CREATE OR REPLACE PROCEDURE BM.SP_BM_CLASIF_UPD (
    p_CodigoEmpresa IN NUMBER,
    p_CodigoClasifBien IN NUMBER,
    p_CodigoGrupo IN VARCHAR2,
    p_CodigoNivel1 IN VARCHAR2,
    p_CodigoNivel2 IN VARCHAR2,
    p_CodigoNivel3 IN VARCHAR2,
    p_Denominacion IN VARCHAR2,
    p_Descripcion IN VARCHAR2,
    p_FechaIni IN DATE,
    p_FechaFin IN DATE,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
BEGIN
    IF TRIM(p_CodigoGrupo) IS NULL OR TRIM(p_Denominacion) IS NULL THEN
        p_TotalRecords := 0;
        p_Message := 'Debe indicar grupo y denominacion.';
        OPEN p_ResultSet FOR SELECT NULL CODIGO_CLASIFICACION_BIEN FROM DUAL WHERE 1 = 0;
        RETURN;
    END IF;

    UPDATE BM.BM_CLASIFICACION_BIENES
       SET CODIGO_GRUPO = p_CodigoGrupo,
           CODIGO_NIVEL1 = p_CodigoNivel1,
           CODIGO_NIVEL2 = p_CodigoNivel2,
           CODIGO_NIVEL3 = p_CodigoNivel3,
           DENOMINACION = p_Denominacion,
           DESCRIPCION = p_Descripcion,
           FECHA_INI = p_FechaIni,
           FECHA_FIN = p_FechaFin,
           FECHA_UPD = SYSDATE
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND CODIGO_CLASIFICACION_BIEN = p_CodigoClasifBien;

    COMMIT;

    OPEN p_ResultSet FOR
        SELECT CODIGO_CLASIFICACION_BIEN, CODIGO_GRUPO, CODIGO_NIVEL1, CODIGO_NIVEL2, CODIGO_NIVEL3,
               DENOMINACION, DESCRIPCION, FECHA_INI, FECHA_FIN
          FROM BM.BM_CLASIFICACION_BIENES
         WHERE CODIGO_EMPRESA = p_CodigoEmpresa
           AND CODIGO_CLASIFICACION_BIEN = p_CodigoClasifBien;

    p_TotalRecords := SQL%ROWCOUNT;
    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT NULL CODIGO_CLASIFICACION_BIEN FROM DUAL WHERE 1 = 0;
END;
/

CREATE OR REPLACE PROCEDURE BM.SP_BM_ART_GET_ALL (
    p_CodigoEmpresa IN NUMBER,
    p_SearchText IN VARCHAR2,
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
      FROM BM.BM_ARTICULOS A
     WHERE A.CODIGO_EMPRESA = p_CodigoEmpresa
       AND (p_SearchText IS NULL OR UPPER(A.DENOMINACION) LIKE '%' || UPPER(p_SearchText) || '%'
            OR UPPER(NVL(A.CODIGO, '')) LIKE '%' || UPPER(p_SearchText) || '%');

    OPEN p_ResultSet FOR
        SELECT CODIGO_ARTICULO, CODIGO_CLASIFICACION_BIEN, CODIGO, DENOMINACION, DESCRIPCION,
               CODIGO_GRUPO, CODIGO_NIVEL1, CODIGO_NIVEL2, CODIGO_NIVEL3, CLASIFICACION
          FROM (
                SELECT X.*, ROWNUM RN
                  FROM (
                        SELECT A.CODIGO_ARTICULO,
                               A.CODIGO_CLASIFICACION_BIEN,
                               A.CODIGO,
                               A.DENOMINACION,
                               A.DESCRIPCION,
                               C.CODIGO_GRUPO,
                               C.CODIGO_NIVEL1,
                               C.CODIGO_NIVEL2,
                               C.CODIGO_NIVEL3,
                               C.DENOMINACION CLASIFICACION
                          FROM BM.BM_ARTICULOS A,
                               BM.BM_CLASIFICACION_BIENES C
                         WHERE C.CODIGO_CLASIFICACION_BIEN = A.CODIGO_CLASIFICACION_BIEN
                           AND A.CODIGO_EMPRESA = p_CodigoEmpresa
                           AND (p_SearchText IS NULL OR UPPER(A.DENOMINACION) LIKE '%' || UPPER(p_SearchText) || '%'
                                OR UPPER(NVL(A.CODIGO, '')) LIKE '%' || UPPER(p_SearchText) || '%')
                         ORDER BY A.DENOMINACION
                       ) X
                 WHERE ROWNUM <= v_ToRow
               )
         WHERE RN >= v_FromRow;

    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT NULL CODIGO_ARTICULO FROM DUAL WHERE 1 = 0;
END;
/

CREATE OR REPLACE PROCEDURE BM.SP_BM_ART_INS (
    p_CodigoEmpresa IN NUMBER,
    p_CodigoArticulo IN NUMBER,
    p_CodigoClasifBien IN NUMBER,
    p_Codigo IN VARCHAR2,
    p_Denominacion IN VARCHAR2,
    p_Descripcion IN VARCHAR2,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
    v_Codigo NUMBER;
    v_Existe NUMBER;
BEGIN
    IF NVL(p_CodigoClasifBien, 0) = 0 OR TRIM(p_Denominacion) IS NULL THEN
        p_TotalRecords := 0;
        p_Message := 'Debe indicar clasificacion y denominacion.';
        OPEN p_ResultSet FOR SELECT NULL CODIGO_ARTICULO FROM DUAL WHERE 1 = 0;
        RETURN;
    END IF;

    SELECT COUNT(1)
      INTO v_Existe
      FROM BM.BM_CLASIFICACION_BIENES
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND CODIGO_CLASIFICACION_BIEN = p_CodigoClasifBien;

    IF v_Existe = 0 THEN
        p_TotalRecords := 0;
        p_Message := 'La clasificacion del articulo no existe.';
        OPEN p_ResultSet FOR SELECT NULL CODIGO_ARTICULO FROM DUAL WHERE 1 = 0;
        RETURN;
    END IF;

    SELECT NVL(MAX(CODIGO_ARTICULO), 0) + 1 INTO v_Codigo FROM BM.BM_ARTICULOS;

    INSERT INTO BM.BM_ARTICULOS (
        CODIGO_ARTICULO, CODIGO_CLASIFICACION_BIEN, CODIGO, DENOMINACION, DESCRIPCION, FECHA_INS, CODIGO_EMPRESA
    ) VALUES (
        v_Codigo, p_CodigoClasifBien, p_Codigo, p_Denominacion, p_Descripcion, SYSDATE, p_CodigoEmpresa
    );

    COMMIT;

    OPEN p_ResultSet FOR
        SELECT A.CODIGO_ARTICULO, A.CODIGO_CLASIFICACION_BIEN, A.CODIGO, A.DENOMINACION, A.DESCRIPCION,
               C.CODIGO_GRUPO, C.CODIGO_NIVEL1, C.CODIGO_NIVEL2, C.CODIGO_NIVEL3, C.DENOMINACION CLASIFICACION
          FROM BM.BM_ARTICULOS A,
               BM.BM_CLASIFICACION_BIENES C
         WHERE C.CODIGO_CLASIFICACION_BIEN = A.CODIGO_CLASIFICACION_BIEN
           AND A.CODIGO_EMPRESA = p_CodigoEmpresa
           AND A.CODIGO_ARTICULO = v_Codigo;

    p_TotalRecords := 1;
    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT NULL CODIGO_ARTICULO FROM DUAL WHERE 1 = 0;
END;
/

CREATE OR REPLACE PROCEDURE BM.SP_BM_ART_UPD (
    p_CodigoEmpresa IN NUMBER,
    p_CodigoArticulo IN NUMBER,
    p_CodigoClasifBien IN NUMBER,
    p_Codigo IN VARCHAR2,
    p_Denominacion IN VARCHAR2,
    p_Descripcion IN VARCHAR2,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
    v_Existe NUMBER;
BEGIN
    IF NVL(p_CodigoClasifBien, 0) = 0 OR TRIM(p_Denominacion) IS NULL THEN
        p_TotalRecords := 0;
        p_Message := 'Debe indicar clasificacion y denominacion.';
        OPEN p_ResultSet FOR SELECT NULL CODIGO_ARTICULO FROM DUAL WHERE 1 = 0;
        RETURN;
    END IF;

    SELECT COUNT(1)
      INTO v_Existe
      FROM BM.BM_CLASIFICACION_BIENES
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND CODIGO_CLASIFICACION_BIEN = p_CodigoClasifBien;

    IF v_Existe = 0 THEN
        p_TotalRecords := 0;
        p_Message := 'La clasificacion del articulo no existe.';
        OPEN p_ResultSet FOR SELECT NULL CODIGO_ARTICULO FROM DUAL WHERE 1 = 0;
        RETURN;
    END IF;

    UPDATE BM.BM_ARTICULOS
       SET CODIGO_CLASIFICACION_BIEN = p_CodigoClasifBien,
           CODIGO = p_Codigo,
           DENOMINACION = p_Denominacion,
           DESCRIPCION = p_Descripcion,
           FECHA_UPD = SYSDATE
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND CODIGO_ARTICULO = p_CodigoArticulo;

    COMMIT;

    OPEN p_ResultSet FOR
        SELECT A.CODIGO_ARTICULO, A.CODIGO_CLASIFICACION_BIEN, A.CODIGO, A.DENOMINACION, A.DESCRIPCION,
               C.CODIGO_GRUPO, C.CODIGO_NIVEL1, C.CODIGO_NIVEL2, C.CODIGO_NIVEL3, C.DENOMINACION CLASIFICACION
          FROM BM.BM_ARTICULOS A,
               BM.BM_CLASIFICACION_BIENES C
         WHERE C.CODIGO_CLASIFICACION_BIEN = A.CODIGO_CLASIFICACION_BIEN
           AND A.CODIGO_EMPRESA = p_CodigoEmpresa
           AND A.CODIGO_ARTICULO = p_CodigoArticulo;

    p_TotalRecords := SQL%ROWCOUNT;
    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT NULL CODIGO_ARTICULO FROM DUAL WHERE 1 = 0;
END;
/

CREATE OR REPLACE PROCEDURE BM.SP_BM_DESC_GET_FK (
    p_CodigoEmpresa IN NUMBER,
    p_DescripcionFkId IN NUMBER,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
BEGIN
    SELECT COUNT(1)
      INTO p_TotalRecords
      FROM BM.BM_DESCRIPTIVAS
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND DESCRIPCION_FK_ID = p_DescripcionFkId;

    OPEN p_ResultSet FOR
        SELECT DESCRIPCION_ID ID,
               DESCRIPCION_ID,
               DESCRIPCION,
               CODIGO,
               EXTRA1,
               EXTRA2,
               EXTRA3
          FROM BM.BM_DESCRIPTIVAS
         WHERE CODIGO_EMPRESA = p_CodigoEmpresa
           AND DESCRIPCION_FK_ID = p_DescripcionFkId
         ORDER BY DESCRIPCION;

    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT NULL ID FROM DUAL WHERE 1 = 0;
END;
/

CREATE OR REPLACE PROCEDURE BM.SP_BM_DET_ART_GET (
    p_CodigoEmpresa IN NUMBER,
    p_CodigoArticulo IN NUMBER,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
BEGIN
    SELECT COUNT(1)
      INTO p_TotalRecords
      FROM BM.BM_DETALLE_ARTICULOS
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND CODIGO_ARTICULO = p_CodigoArticulo;

    OPEN p_ResultSet FOR
        SELECT D.CODIGO_DETALLE_ARTICULO,
               D.CODIGO_ARTICULO,
               NVL(D.TIPO_ESPECIFICACION_ID, 0) TIPO_ESPECIFICACION_ID,
               E.DESCRIPCION TIPO_ESPECIFICACION
          FROM BM.BM_DETALLE_ARTICULOS D,
               BM.BM_DESCRIPTIVAS E
         WHERE E.DESCRIPCION_ID(+) = D.TIPO_ESPECIFICACION_ID
           AND D.CODIGO_EMPRESA = p_CodigoEmpresa
           AND D.CODIGO_ARTICULO = p_CodigoArticulo
         ORDER BY D.CODIGO_DETALLE_ARTICULO;

    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT NULL CODIGO_DETALLE_ARTICULO FROM DUAL WHERE 1 = 0;
END;
/

CREATE OR REPLACE PROCEDURE BM.SP_BM_DET_ART_INS (
    p_CodigoEmpresa IN NUMBER,
    p_CodigoDetArticulo IN NUMBER,
    p_CodigoArticulo IN NUMBER,
    p_TipoEspecificacionId IN NUMBER,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
    v_Codigo NUMBER;
    v_Existe NUMBER;
BEGIN
    IF NVL(p_CodigoArticulo, 0) = 0 OR NVL(p_TipoEspecificacionId, 0) = 0 THEN
        p_TotalRecords := 0;
        p_Message := 'Debe indicar articulo y tipo de especificacion.';
        OPEN p_ResultSet FOR SELECT NULL CODIGO_DETALLE_ARTICULO FROM DUAL WHERE 1 = 0;
        RETURN;
    END IF;

    SELECT COUNT(1)
      INTO v_Existe
      FROM BM.BM_ARTICULOS
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND CODIGO_ARTICULO = p_CodigoArticulo;

    IF v_Existe = 0 THEN
        p_TotalRecords := 0;
        p_Message := 'El articulo no existe.';
        OPEN p_ResultSet FOR SELECT NULL CODIGO_DETALLE_ARTICULO FROM DUAL WHERE 1 = 0;
        RETURN;
    END IF;

    SELECT COUNT(1)
      INTO v_Existe
      FROM BM.BM_DETALLE_ARTICULOS
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND CODIGO_ARTICULO = p_CodigoArticulo
       AND TIPO_ESPECIFICACION_ID = p_TipoEspecificacionId;

    IF v_Existe > 0 THEN
        p_TotalRecords := 0;
        p_Message := 'El articulo ya tiene este tipo de especificacion.';
        OPEN p_ResultSet FOR SELECT NULL CODIGO_DETALLE_ARTICULO FROM DUAL WHERE 1 = 0;
        RETURN;
    END IF;

    SELECT NVL(MAX(CODIGO_DETALLE_ARTICULO), 0) + 1 INTO v_Codigo FROM BM.BM_DETALLE_ARTICULOS;

    INSERT INTO BM.BM_DETALLE_ARTICULOS (
        CODIGO_DETALLE_ARTICULO, CODIGO_ARTICULO, TIPO_ESPECIFICACION_ID, FECHA_INS, CODIGO_EMPRESA
    ) VALUES (
        v_Codigo, p_CodigoArticulo, p_TipoEspecificacionId, SYSDATE, p_CodigoEmpresa
    );

    COMMIT;

    BM.SP_BM_DET_ART_GET(p_CodigoEmpresa, p_CodigoArticulo, p_ResultSet, p_Message, p_TotalRecords);
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT NULL CODIGO_DETALLE_ARTICULO FROM DUAL WHERE 1 = 0;
END;
/

CREATE OR REPLACE PROCEDURE BM.SP_BM_DET_ART_UPD (
    p_CodigoEmpresa IN NUMBER,
    p_CodigoDetArticulo IN NUMBER,
    p_CodigoArticulo IN NUMBER,
    p_TipoEspecificacionId IN NUMBER,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
    v_Existe NUMBER;
BEGIN
    IF NVL(p_CodigoArticulo, 0) = 0 OR NVL(p_TipoEspecificacionId, 0) = 0 THEN
        p_TotalRecords := 0;
        p_Message := 'Debe indicar articulo y tipo de especificacion.';
        OPEN p_ResultSet FOR SELECT NULL CODIGO_DETALLE_ARTICULO FROM DUAL WHERE 1 = 0;
        RETURN;
    END IF;

    SELECT COUNT(1)
      INTO v_Existe
      FROM BM.BM_ARTICULOS
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND CODIGO_ARTICULO = p_CodigoArticulo;

    IF v_Existe = 0 THEN
        p_TotalRecords := 0;
        p_Message := 'El articulo no existe.';
        OPEN p_ResultSet FOR SELECT NULL CODIGO_DETALLE_ARTICULO FROM DUAL WHERE 1 = 0;
        RETURN;
    END IF;

    SELECT COUNT(1)
      INTO v_Existe
      FROM BM.BM_DETALLE_ARTICULOS
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND CODIGO_ARTICULO = p_CodigoArticulo
       AND TIPO_ESPECIFICACION_ID = p_TipoEspecificacionId
       AND CODIGO_DETALLE_ARTICULO <> p_CodigoDetArticulo;

    IF v_Existe > 0 THEN
        p_TotalRecords := 0;
        p_Message := 'El articulo ya tiene este tipo de especificacion.';
        OPEN p_ResultSet FOR SELECT NULL CODIGO_DETALLE_ARTICULO FROM DUAL WHERE 1 = 0;
        RETURN;
    END IF;

    UPDATE BM.BM_DETALLE_ARTICULOS
       SET CODIGO_ARTICULO = p_CodigoArticulo,
           TIPO_ESPECIFICACION_ID = p_TipoEspecificacionId,
           FECHA_UPD = SYSDATE
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND CODIGO_DETALLE_ARTICULO = p_CodigoDetArticulo;

    COMMIT;

    BM.SP_BM_DET_ART_GET(p_CodigoEmpresa, p_CodigoArticulo, p_ResultSet, p_Message, p_TotalRecords);
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT NULL CODIGO_DETALLE_ARTICULO FROM DUAL WHERE 1 = 0;
END;
/
