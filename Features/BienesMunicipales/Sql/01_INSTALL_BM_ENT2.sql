CREATE OR REPLACE PROCEDURE BM.SP_BM_BIEN_GET_ALL (
    p_CodigoEmpresa IN NUMBER,
    p_SearchText IN VARCHAR2,
    p_Page IN NUMBER,
    p_PageSize IN NUMBER,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
    v_Page NUMBER := NVL(p_Page, 1);
    v_PageSize NUMBER := NVL(p_PageSize, 25);
    v_FromRow NUMBER;
    v_ToRow NUMBER;
BEGIN
    v_FromRow := ((v_Page - 1) * v_PageSize) + 1;
    v_ToRow := v_Page * v_PageSize;

    SELECT COUNT(1)
      INTO p_TotalRecords
      FROM BM.BM_BIENES B,
           BM.BM_ARTICULOS A
     WHERE B.CODIGO_ARTICULO = A.CODIGO_ARTICULO
       AND B.CODIGO_EMPRESA = p_CodigoEmpresa
       AND (
            p_SearchText IS NULL
            OR TO_CHAR(B.CODIGO_BIEN) LIKE '%' || TRIM(p_SearchText) || '%'
            OR UPPER(B.NUMERO_PLACA) LIKE '%' || UPPER(p_SearchText) || '%'
            OR UPPER(A.DENOMINACION) LIKE '%' || UPPER(p_SearchText) || '%'
            OR UPPER(NVL(B.NUMERO_LOTE, '')) LIKE '%' || UPPER(p_SearchText) || '%'
       );

    OPEN p_ResultSet FOR
        SELECT *
          FROM (
                SELECT X.*, ROWNUM RN
                  FROM (
                        SELECT B.CODIGO_BIEN,
                               B.CODIGO_ARTICULO,
                               A.DENOMINACION ARTICULO,
                               B.NUMERO_PLACA,
                               B.NUMERO_LOTE,
                               NVL(B.VALOR_INICIAL, 0) VALOR_INICIAL,
                               NVL(B.VALOR_ACTUAL, B.VALOR_INICIAL) VALOR_ACTUAL,
                               B.FECHA_COMPRA,
                               B.FECHA_FACTURA,
                               B.NUMERO_FACTURA,
                               B.NUMERO_ORDEN_COMPRA,
                               NVL(B.CODIGO_PROVEEDOR, 0) CODIGO_PROVEEDOR,
                               NULL PROVEEDOR,
                               NVL(B.ORIGEN_ID, 0) ORIGEN_ID,
                               O.DESCRIPCION ORIGEN,
                               NVL(B.TIPO_IMPUESTO_ID, 0) TIPO_IMPUESTO_ID,
                               TI.DESCRIPCION TIPO_IMPUESTO,
                               NVL(V.ESPECIFICACION, BM.BM_ESPECIFICACIONES(B.CODIGO_BIEN)) ESPECIFICACION,
                               V.SERVICIO,
                               V.RESPONSABLE_BIEN,
                               V.UNIDAD_TRABAJO,
                               UPPER(B.CODIGO_BIEN || ' ' || B.NUMERO_PLACA || ' ' || A.DENOMINACION || ' ' || NVL(B.NUMERO_LOTE, '')) SEARCH_TEXT
                          FROM BM.BM_BIENES B,
                               BM.BM_ARTICULOS A,
                               BM.BM_DESCRIPTIVAS O,
                               BM.BM_DESCRIPTIVAS TI,
                               BM.BM_V_BM1 V
                         WHERE B.CODIGO_ARTICULO = A.CODIGO_ARTICULO
                           AND O.DESCRIPCION_ID(+) = B.ORIGEN_ID
                           AND TI.DESCRIPCION_ID(+) = B.TIPO_IMPUESTO_ID
                           AND V.CODIGO_BIEN(+) = B.CODIGO_BIEN
                           AND B.CODIGO_EMPRESA = p_CodigoEmpresa
                           AND (
                                p_SearchText IS NULL
                                OR TO_CHAR(B.CODIGO_BIEN) LIKE '%' || TRIM(p_SearchText) || '%'
                                OR UPPER(B.NUMERO_PLACA) LIKE '%' || UPPER(p_SearchText) || '%'
                                OR UPPER(A.DENOMINACION) LIKE '%' || UPPER(p_SearchText) || '%'
                                OR UPPER(NVL(B.NUMERO_LOTE, '')) LIKE '%' || UPPER(p_SearchText) || '%'
                           )
                         ORDER BY B.NUMERO_PLACA
                       ) X
                 WHERE ROWNUM <= v_ToRow
               )
         WHERE RN >= v_FromRow;

    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR
            SELECT NULL CODIGO_BIEN,
                   NULL CODIGO_ARTICULO,
                   NULL ARTICULO,
                   NULL NUMERO_PLACA,
                   NULL NUMERO_LOTE,
                   NULL VALOR_INICIAL,
                   NULL VALOR_ACTUAL,
                   NULL FECHA_COMPRA,
                   NULL FECHA_FACTURA,
                   NULL NUMERO_FACTURA,
                   NULL NUMERO_ORDEN_COMPRA,
                   NULL CODIGO_PROVEEDOR,
                   NULL PROVEEDOR,
                   NULL ORIGEN_ID,
                   NULL ORIGEN,
                   NULL TIPO_IMPUESTO_ID,
                   NULL TIPO_IMPUESTO,
                   NULL ESPECIFICACION,
                   NULL SERVICIO,
                   NULL RESPONSABLE_BIEN,
                   NULL UNIDAD_TRABAJO,
                   NULL SEARCH_TEXT
              FROM DUAL
             WHERE 1 = 0;
END;
/

CREATE OR REPLACE PROCEDURE BM.SP_BM_BIEN_GET_ID (
    p_CodigoEmpresa IN NUMBER,
    p_CodigoBien IN NUMBER,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
BEGIN
    OPEN p_ResultSet FOR
        SELECT B.CODIGO_BIEN,
               B.CODIGO_ARTICULO,
               A.DENOMINACION ARTICULO,
               B.NUMERO_PLACA,
               B.NUMERO_LOTE,
               NVL(B.VALOR_INICIAL, 0) VALOR_INICIAL,
               NVL(B.VALOR_ACTUAL, B.VALOR_INICIAL) VALOR_ACTUAL,
               B.FECHA_COMPRA,
               B.FECHA_FACTURA,
               B.NUMERO_FACTURA,
               B.NUMERO_ORDEN_COMPRA,
               NVL(B.CODIGO_PROVEEDOR, 0) CODIGO_PROVEEDOR,
               NULL PROVEEDOR,
               NVL(B.ORIGEN_ID, 0) ORIGEN_ID,
               O.DESCRIPCION ORIGEN,
               NVL(B.TIPO_IMPUESTO_ID, 0) TIPO_IMPUESTO_ID,
               TI.DESCRIPCION TIPO_IMPUESTO,
               NVL(V.ESPECIFICACION, BM.BM_ESPECIFICACIONES(B.CODIGO_BIEN)) ESPECIFICACION,
               V.SERVICIO,
               V.RESPONSABLE_BIEN,
               V.UNIDAD_TRABAJO,
               UPPER(B.NUMERO_PLACA || ' ' || A.DENOMINACION || ' ' || NVL(B.NUMERO_LOTE, '')) SEARCH_TEXT
          FROM BM.BM_BIENES B,
               BM.BM_ARTICULOS A,
               BM.BM_DESCRIPTIVAS O,
               BM.BM_DESCRIPTIVAS TI,
               BM.BM_V_BM1 V
         WHERE B.CODIGO_ARTICULO = A.CODIGO_ARTICULO
           AND O.DESCRIPCION_ID(+) = B.ORIGEN_ID
           AND TI.DESCRIPCION_ID(+) = B.TIPO_IMPUESTO_ID
           AND V.CODIGO_BIEN(+) = B.CODIGO_BIEN
           AND B.CODIGO_EMPRESA = p_CodigoEmpresa
           AND B.CODIGO_BIEN = p_CodigoBien;

    p_TotalRecords := 1;
    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT NULL CODIGO_BIEN FROM DUAL WHERE 1 = 0;
END;
/

CREATE OR REPLACE PROCEDURE BM.SP_BM_BIEN_GET_PLACA (
    p_CodigoEmpresa IN NUMBER,
    p_NumeroPlaca IN VARCHAR2,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
BEGIN
    OPEN p_ResultSet FOR
        SELECT B.CODIGO_BIEN,
               B.CODIGO_ARTICULO,
               A.DENOMINACION ARTICULO,
               B.NUMERO_PLACA,
               B.NUMERO_LOTE,
               NVL(B.VALOR_INICIAL, 0) VALOR_INICIAL,
               NVL(B.VALOR_ACTUAL, B.VALOR_INICIAL) VALOR_ACTUAL,
               B.FECHA_COMPRA,
               B.FECHA_FACTURA,
               B.NUMERO_FACTURA,
               B.NUMERO_ORDEN_COMPRA,
               NVL(B.CODIGO_PROVEEDOR, 0) CODIGO_PROVEEDOR,
               NULL PROVEEDOR,
               NVL(B.ORIGEN_ID, 0) ORIGEN_ID,
               O.DESCRIPCION ORIGEN,
               NVL(B.TIPO_IMPUESTO_ID, 0) TIPO_IMPUESTO_ID,
               TI.DESCRIPCION TIPO_IMPUESTO,
               NVL(V.ESPECIFICACION, BM.BM_ESPECIFICACIONES(B.CODIGO_BIEN)) ESPECIFICACION,
               V.SERVICIO,
               V.RESPONSABLE_BIEN,
               V.UNIDAD_TRABAJO,
               UPPER(B.NUMERO_PLACA || ' ' || A.DENOMINACION || ' ' || NVL(B.NUMERO_LOTE, '')) SEARCH_TEXT
          FROM BM.BM_BIENES B,
               BM.BM_ARTICULOS A,
               BM.BM_DESCRIPTIVAS O,
               BM.BM_DESCRIPTIVAS TI,
               BM.BM_V_BM1 V
         WHERE B.CODIGO_ARTICULO = A.CODIGO_ARTICULO
           AND O.DESCRIPCION_ID(+) = B.ORIGEN_ID
           AND TI.DESCRIPCION_ID(+) = B.TIPO_IMPUESTO_ID
           AND V.CODIGO_BIEN(+) = B.CODIGO_BIEN
           AND B.CODIGO_EMPRESA = p_CodigoEmpresa
           AND B.NUMERO_PLACA = p_NumeroPlaca;

    p_TotalRecords := 1;
    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT NULL CODIGO_BIEN FROM DUAL WHERE 1 = 0;
END;
/

CREATE OR REPLACE PROCEDURE BM.SP_BM_DET_BIEN_GET (
    p_CodigoEmpresa IN NUMBER,
    p_CodigoBien IN NUMBER,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
BEGIN
    SELECT COUNT(1)
      INTO p_TotalRecords
      FROM BM.BM_DETALLE_BIENES
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND CODIGO_BIEN = p_CodigoBien;

    OPEN p_ResultSet FOR
        SELECT D.CODIGO_DETALLE_BIEN,
               D.CODIGO_BIEN,
               NVL(D.TIPO_ESPECIFICACION_ID, 0) TIPO_ESPECIFICACION_ID,
               T.DESCRIPCION TIPO_ESPECIFICACION,
               NVL(D.ESPECIFICACION_ID, 0) ESPECIFICACION_ID,
               E.DESCRIPCION ESPECIFICACION_ID_DESC,
               D.ESPECIFICACION
          FROM BM.BM_DETALLE_BIENES D,
               BM.BM_DESCRIPTIVAS T,
               BM.BM_DESCRIPTIVAS E
         WHERE T.DESCRIPCION_ID(+) = D.TIPO_ESPECIFICACION_ID
           AND E.DESCRIPCION_ID(+) = D.ESPECIFICACION_ID
           AND D.CODIGO_EMPRESA = p_CodigoEmpresa
           AND D.CODIGO_BIEN = p_CodigoBien
         ORDER BY D.CODIGO_DETALLE_BIEN;

    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT NULL CODIGO_DETALLE_BIEN FROM DUAL WHERE 1 = 0;
END;
/

CREATE OR REPLACE PROCEDURE BM.SP_BM_DET_BIEN_INS (
    p_CodigoEmpresa IN NUMBER,
    p_CodigoDetalleBien IN NUMBER,
    p_CodigoBien IN NUMBER,
    p_TipoEspecificacionId IN NUMBER,
    p_EspecificacionId IN NUMBER,
    p_Especificacion IN VARCHAR2,
    p_UsuarioId IN NUMBER,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
    v_Codigo NUMBER;
    v_Existe NUMBER;
BEGIN
    IF NVL(p_CodigoBien, 0) = 0 THEN
        p_TotalRecords := 0;
        p_Message := 'Debe indicar el bien.';
        OPEN p_ResultSet FOR SELECT NULL CODIGO_DETALLE_BIEN FROM DUAL WHERE 1 = 0;
        RETURN;
    END IF;

    IF NVL(p_TipoEspecificacionId, 0) = 0 THEN
        p_TotalRecords := 0;
        p_Message := 'Debe indicar el tipo de especificacion.';
        OPEN p_ResultSet FOR SELECT NULL CODIGO_DETALLE_BIEN FROM DUAL WHERE 1 = 0;
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
        OPEN p_ResultSet FOR SELECT NULL CODIGO_DETALLE_BIEN FROM DUAL WHERE 1 = 0;
        RETURN;
    END IF;

    SELECT COUNT(1)
      INTO v_Existe
      FROM BM.BM_DETALLE_BIENES
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND CODIGO_BIEN = p_CodigoBien
       AND TIPO_ESPECIFICACION_ID = p_TipoEspecificacionId;

    IF v_Existe > 0 THEN
        p_TotalRecords := 0;
        p_Message := 'Ya existe un detalle para este tipo de especificacion.';
        OPEN p_ResultSet FOR SELECT NULL CODIGO_DETALLE_BIEN FROM DUAL WHERE 1 = 0;
        RETURN;
    END IF;

    SELECT NVL(MAX(CODIGO_DETALLE_BIEN), 0) + 1
      INTO v_Codigo
      FROM BM.BM_DETALLE_BIENES;

    INSERT INTO BM.BM_DETALLE_BIENES (
        CODIGO_DETALLE_BIEN,
        CODIGO_BIEN,
        TIPO_ESPECIFICACION_ID,
        ESPECIFICACION_ID,
        ESPECIFICACION,
        USUARIO_INS,
        FECHA_INS,
        CODIGO_EMPRESA
    ) VALUES (
        v_Codigo,
        p_CodigoBien,
        p_TipoEspecificacionId,
        p_EspecificacionId,
        p_Especificacion,
        p_UsuarioId,
        SYSDATE,
        p_CodigoEmpresa
    );

    COMMIT;

    BM.SP_BM_DET_BIEN_GET(p_CodigoEmpresa, p_CodigoBien, p_ResultSet, p_Message, p_TotalRecords);
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT NULL CODIGO_DETALLE_BIEN FROM DUAL WHERE 1 = 0;
END;
/

CREATE OR REPLACE PROCEDURE BM.SP_BM_DET_BIEN_UPD (
    p_CodigoEmpresa IN NUMBER,
    p_CodigoDetalleBien IN NUMBER,
    p_CodigoBien IN NUMBER,
    p_TipoEspecificacionId IN NUMBER,
    p_EspecificacionId IN NUMBER,
    p_Especificacion IN VARCHAR2,
    p_UsuarioId IN NUMBER,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
    v_Existe NUMBER;
BEGIN
    IF NVL(p_CodigoDetalleBien, 0) = 0 THEN
        p_TotalRecords := 0;
        p_Message := 'Debe indicar el detalle del bien.';
        OPEN p_ResultSet FOR SELECT NULL CODIGO_DETALLE_BIEN FROM DUAL WHERE 1 = 0;
        RETURN;
    END IF;

    IF NVL(p_CodigoBien, 0) = 0 THEN
        p_TotalRecords := 0;
        p_Message := 'Debe indicar el bien.';
        OPEN p_ResultSet FOR SELECT NULL CODIGO_DETALLE_BIEN FROM DUAL WHERE 1 = 0;
        RETURN;
    END IF;

    IF NVL(p_TipoEspecificacionId, 0) = 0 THEN
        p_TotalRecords := 0;
        p_Message := 'Debe indicar el tipo de especificacion.';
        OPEN p_ResultSet FOR SELECT NULL CODIGO_DETALLE_BIEN FROM DUAL WHERE 1 = 0;
        RETURN;
    END IF;

    SELECT COUNT(1)
      INTO v_Existe
      FROM BM.BM_DETALLE_BIENES
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND CODIGO_DETALLE_BIEN = p_CodigoDetalleBien
       AND CODIGO_BIEN = p_CodigoBien;

    IF v_Existe = 0 THEN
        p_TotalRecords := 0;
        p_Message := 'El detalle del bien no existe.';
        OPEN p_ResultSet FOR SELECT NULL CODIGO_DETALLE_BIEN FROM DUAL WHERE 1 = 0;
        RETURN;
    END IF;

    SELECT COUNT(1)
      INTO v_Existe
      FROM BM.BM_DETALLE_BIENES
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND CODIGO_BIEN = p_CodigoBien
       AND TIPO_ESPECIFICACION_ID = p_TipoEspecificacionId
       AND CODIGO_DETALLE_BIEN <> p_CodigoDetalleBien;

    IF v_Existe > 0 THEN
        p_TotalRecords := 0;
        p_Message := 'Ya existe un detalle para este tipo de especificacion.';
        OPEN p_ResultSet FOR SELECT NULL CODIGO_DETALLE_BIEN FROM DUAL WHERE 1 = 0;
        RETURN;
    END IF;

    UPDATE BM.BM_DETALLE_BIENES
       SET TIPO_ESPECIFICACION_ID = p_TipoEspecificacionId,
           ESPECIFICACION_ID = p_EspecificacionId,
           ESPECIFICACION = p_Especificacion,
           USUARIO_UPD = p_UsuarioId,
           FECHA_UPD = SYSDATE
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND CODIGO_DETALLE_BIEN = p_CodigoDetalleBien
       AND CODIGO_BIEN = p_CodigoBien;

    COMMIT;

    BM.SP_BM_DET_BIEN_GET(p_CodigoEmpresa, p_CodigoBien, p_ResultSet, p_Message, p_TotalRecords);
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT NULL CODIGO_DETALLE_BIEN FROM DUAL WHERE 1 = 0;
END;
/

CREATE OR REPLACE FUNCTION BM.NUMBER_PLACA (
    P_ART_CODE IN NUMBER,
    P_CODIGO_EMPRESA IN NUMBER
) RETURN VARCHAR2
IS
    V_PLACA VARCHAR2(20);
    V_NUMBER_NEXT VARCHAR2(10);
BEGIN
    SELECT LPAD(COUNT(*) + 1, 5, '0')
      INTO V_NUMBER_NEXT
      FROM BM.BM_BIENES;

    BEGIN
        SELECT A.CODIGO_GRUPO || '-' || A.CODIGO_NIVEL1 || '-' ||
               A.CODIGO_NIVEL2 || '-' || V_NUMBER_NEXT
          INTO V_PLACA
          FROM BM.BM_CLASIFICACION_BIENES A,
               BM.BM_ARTICULOS B
         WHERE A.CODIGO_CLASIFICACION_BIEN = B.CODIGO_CLASIFICACION_BIEN
           AND B.CODIGO_ARTICULO = P_ART_CODE
           AND A.CODIGO_EMPRESA = P_CODIGO_EMPRESA;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            RETURN NULL;
    END;

    RETURN V_PLACA;
END;
/

CREATE OR REPLACE PROCEDURE BM.SP_BM_BIEN_INS (
    p_CodigoEmpresa IN NUMBER,
    p_CodigoBien IN NUMBER,
    p_CodigoArticulo IN NUMBER,
    p_CodigoProveedor IN NUMBER,
    p_CodigoOrdenCompra IN NUMBER,
    p_OrigenId IN NUMBER,
    p_FechaFabricacion IN DATE,
    p_NumeroOrdenCompra IN VARCHAR2,
    p_FechaCompra IN DATE,
    p_NumeroPlaca IN VARCHAR2,
    p_NumeroLote IN VARCHAR2,
    p_ValorInicial IN NUMBER,
    p_ValorActual IN NUMBER,
    p_NumeroFactura IN VARCHAR2,
    p_FechaFactura IN DATE,
    p_TipoImpuestoId IN NUMBER,
    p_Cantidad IN NUMBER,
    p_CodigoDirBien IN NUMBER,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
    v_CodigoBien NUMBER;
    v_Existe NUMBER;
    v_Cantidad NUMBER;
    v_Count NUMBER := 0;
    v_NumeroLote BM.BM_BIENES.NUMERO_LOTE%TYPE;
    v_NumeroPlaca BM.BM_BIENES.NUMERO_PLACA%TYPE;
    v_ConceptoMovId NUMBER;
    v_CodigoMovBien NUMBER;
    v_SessionId VARCHAR2(24);
BEGIN
    v_Cantidad := NVL(p_Cantidad, 0);
    IF v_Cantidad <= 0 THEN
        v_Cantidad := 1;
    END IF;

    IF NVL(p_CodigoDirBien, 0) = 0 THEN
        p_TotalRecords := 0;
        p_Message := 'Debe asignar una ubicacion inicial.';
        OPEN p_ResultSet FOR SELECT NULL CODIGO_BIEN FROM DUAL WHERE 1 = 0;
        RETURN;
    END IF;

    SELECT COUNT(1)
      INTO v_Existe
      FROM BM.BM_DIR_BIEN
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND CODIGO_DIR_BIEN = p_CodigoDirBien;

    IF v_Existe = 0 THEN
        p_TotalRecords := 0;
        p_Message := 'La ubicacion inicial no existe.';
        OPEN p_ResultSet FOR SELECT NULL CODIGO_BIEN FROM DUAL WHERE 1 = 0;
        RETURN;
    END IF;

    BEGIN
        SELECT DESCRIPCION_ID
          INTO v_ConceptoMovId
          FROM BM.BM_DESCRIPTIVAS
         WHERE CODIGO_EMPRESA = p_CodigoEmpresa
           AND CODIGO = '03'
           AND ROWNUM = 1;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            p_TotalRecords := 0;
            p_Message := 'No existe el concepto de insercion de bienes.';
            OPEN p_ResultSet FOR SELECT NULL CODIGO_BIEN FROM DUAL WHERE 1 = 0;
            RETURN;
    END;

    IF p_NumeroLote IS NULL THEN
        SELECT TO_CHAR(NVL(MAX(TO_NUMBER(NUMERO_LOTE)), 0) + 1)
          INTO v_NumeroLote
          FROM BM.BM_BIENES
         WHERE CODIGO_EMPRESA = p_CodigoEmpresa;
    ELSE
        v_NumeroLote := p_NumeroLote;
    END IF;

    v_SessionId := SUBSTR(RAWTOHEX(SYS_GUID()), 1, 24);

    LOOP
        EXIT WHEN v_Count = v_Cantidad;

        IF v_Cantidad = 1 AND p_NumeroPlaca IS NOT NULL THEN
            v_NumeroPlaca := p_NumeroPlaca;
        ELSE
            v_NumeroPlaca := BM.NUMBER_PLACA(p_CodigoArticulo, p_CodigoEmpresa);
        END IF;

        IF v_NumeroPlaca IS NULL THEN
            ROLLBACK;
            p_TotalRecords := 0;
            p_Message := 'No se pudo generar el numero de placa.';
            OPEN p_ResultSet FOR SELECT NULL CODIGO_BIEN FROM DUAL WHERE 1 = 0;
            RETURN;
        END IF;

        SELECT COUNT(1)
          INTO v_Existe
          FROM BM.BM_BIENES
         WHERE CODIGO_EMPRESA = p_CodigoEmpresa
           AND NUMERO_PLACA = v_NumeroPlaca;

        IF v_Existe > 0 THEN
            ROLLBACK;
            p_TotalRecords := 0;
            p_Message := 'La placa ya existe para la empresa.';
            OPEN p_ResultSet FOR SELECT NULL CODIGO_BIEN FROM DUAL WHERE 1 = 0;
            RETURN;
        END IF;

        SELECT COUNT(1)
          INTO v_Existe
          FROM BM.BM_PLACAS_CUARENTENA
         WHERE CODIGO_EMPRESA = p_CodigoEmpresa
           AND NUMERO_PLACA = v_NumeroPlaca;

        IF v_Existe > 0 THEN
            ROLLBACK;
            p_TotalRecords := 0;
            p_Message := 'La placa se encuentra en cuarentena.';
            OPEN p_ResultSet FOR SELECT NULL CODIGO_BIEN FROM DUAL WHERE 1 = 0;
            RETURN;
        END IF;

        IF NVL(p_CodigoBien, 0) > 0 AND v_Cantidad = 1 THEN
            v_CodigoBien := p_CodigoBien;
        ELSE
            SELECT NVL(MAX(CODIGO_BIEN), 0) + 1
              INTO v_CodigoBien
              FROM BM.BM_BIENES;
        END IF;

        SELECT NVL(MAX(CODIGO_MOV_BIEN), 0) + 1
          INTO v_CodigoMovBien
          FROM BM.BM_MOV_BIENES;

        INSERT INTO BM.BM_BIENES (
            CODIGO_BIEN,
            CODIGO_ARTICULO,
            CODIGO_PROVEEDOR,
            CODIGO_ORDEN_COMPRA,
            ORIGEN_ID,
            FECHA_FABRICACION,
            NUMERO_ORDEN_COMPRA,
            FECHA_COMPRA,
            NUMERO_PLACA,
            NUMERO_LOTE,
            VALOR_INICIAL,
            VALOR_ACTUAL,
            FECHA_INS,
            CODIGO_EMPRESA,
            NUMERO_FACTURA,
            FECHA_FACTURA,
            TIPO_IMPUESTO_ID
        ) VALUES (
            v_CodigoBien,
            p_CodigoArticulo,
            p_CodigoProveedor,
            p_CodigoOrdenCompra,
            p_OrigenId,
            p_FechaFabricacion,
            p_NumeroOrdenCompra,
            p_FechaCompra,
            v_NumeroPlaca,
            v_NumeroLote,
            p_ValorInicial,
            p_ValorActual,
            SYSDATE,
            p_CodigoEmpresa,
            p_NumeroFactura,
            p_FechaFactura,
            p_TipoImpuestoId
        );

        INSERT INTO BM.BM_MOV_BIENES (
            CODIGO_MOV_BIEN,
            CODIGO_BIEN,
            TIPO_MOVIMIENTO,
            FECHA_MOVIMIENTO,
            CODIGO_DIR_BIEN,
            USUARIO_INS,
            FECHA_INS,
            CODIGO_EMPRESA,
            CONCEPTO_MOV_ID
        ) VALUES (
            v_CodigoMovBien,
            v_CodigoBien,
            'I',
            TRUNC(SYSDATE),
            p_CodigoDirBien,
            NULL,
            SYSDATE,
            p_CodigoEmpresa,
            v_ConceptoMovId
        );

        INSERT INTO BM.BM_TMP_INSERT_BIENES (
            CODIGO_BIEN,
            SESSION_ID
        ) VALUES (
            v_CodigoBien,
            v_SessionId
        );

        v_Count := v_Count + 1;
    END LOOP;

    COMMIT;

    SELECT COUNT(1)
      INTO p_TotalRecords
      FROM BM.BM_TMP_INSERT_BIENES
     WHERE SESSION_ID = v_SessionId;

    OPEN p_ResultSet FOR
        SELECT B.CODIGO_BIEN,
               B.CODIGO_ARTICULO,
               A.DENOMINACION ARTICULO,
               B.NUMERO_PLACA,
               B.NUMERO_LOTE,
               NVL(B.VALOR_INICIAL, 0) VALOR_INICIAL,
               NVL(B.VALOR_ACTUAL, B.VALOR_INICIAL) VALOR_ACTUAL,
               B.FECHA_COMPRA,
               B.FECHA_FACTURA,
               B.NUMERO_FACTURA,
               B.NUMERO_ORDEN_COMPRA,
               NVL(B.CODIGO_PROVEEDOR, 0) CODIGO_PROVEEDOR,
               NULL PROVEEDOR,
               NVL(B.ORIGEN_ID, 0) ORIGEN_ID,
               O.DESCRIPCION ORIGEN,
               NVL(B.TIPO_IMPUESTO_ID, 0) TIPO_IMPUESTO_ID,
               TI.DESCRIPCION TIPO_IMPUESTO,
               NVL(V.ESPECIFICACION, BM.BM_ESPECIFICACIONES(B.CODIGO_BIEN)) ESPECIFICACION,
               V.SERVICIO,
               V.RESPONSABLE_BIEN,
               V.UNIDAD_TRABAJO,
               UPPER(B.NUMERO_PLACA || ' ' || A.DENOMINACION || ' ' || NVL(B.NUMERO_LOTE, '')) SEARCH_TEXT
          FROM BM.BM_TMP_INSERT_BIENES T,
               BM.BM_BIENES B,
               BM.BM_ARTICULOS A,
               BM.BM_DESCRIPTIVAS O,
               BM.BM_DESCRIPTIVAS TI,
               BM.BM_V_BM1 V
         WHERE T.CODIGO_BIEN = B.CODIGO_BIEN
           AND B.CODIGO_ARTICULO = A.CODIGO_ARTICULO
           AND O.DESCRIPCION_ID(+) = B.ORIGEN_ID
           AND TI.DESCRIPCION_ID(+) = B.TIPO_IMPUESTO_ID
           AND V.CODIGO_BIEN(+) = B.CODIGO_BIEN
           AND T.SESSION_ID = v_SessionId
         ORDER BY B.NUMERO_PLACA;

    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT NULL CODIGO_BIEN FROM DUAL WHERE 1 = 0;
END;
/

CREATE OR REPLACE PROCEDURE BM.SP_BM_BIEN_UPD (
    p_CodigoEmpresa IN NUMBER,
    p_CodigoBien IN NUMBER,
    p_CodigoArticulo IN NUMBER,
    p_CodigoProveedor IN NUMBER,
    p_CodigoOrdenCompra IN NUMBER,
    p_OrigenId IN NUMBER,
    p_FechaFabricacion IN DATE,
    p_NumeroOrdenCompra IN VARCHAR2,
    p_FechaCompra IN DATE,
    p_NumeroPlaca IN VARCHAR2,
    p_NumeroLote IN VARCHAR2,
    p_ValorInicial IN NUMBER,
    p_ValorActual IN NUMBER,
    p_NumeroFactura IN VARCHAR2,
    p_FechaFactura IN DATE,
    p_TipoImpuestoId IN NUMBER,
    p_UsuarioUpd IN NUMBER,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
    v_Existe NUMBER;
BEGIN
    SELECT COUNT(1)
      INTO v_Existe
      FROM BM.BM_BIENES
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND CODIGO_BIEN = p_CodigoBien;

    IF v_Existe = 0 THEN
        p_TotalRecords := 0;
        p_Message := 'El bien no existe.';
        OPEN p_ResultSet FOR SELECT NULL CODIGO_BIEN FROM DUAL WHERE 1 = 0;
        RETURN;
    END IF;

    UPDATE BM.BM_BIENES
       SET CODIGO_ARTICULO = p_CodigoArticulo,
           CODIGO_PROVEEDOR = p_CodigoProveedor,
           CODIGO_ORDEN_COMPRA = p_CodigoOrdenCompra,
           ORIGEN_ID = p_OrigenId,
           FECHA_FABRICACION = p_FechaFabricacion,
           NUMERO_ORDEN_COMPRA = p_NumeroOrdenCompra,
           FECHA_COMPRA = p_FechaCompra,
           VALOR_INICIAL = p_ValorInicial,
           VALOR_ACTUAL = p_ValorActual,
           USUARIO_UPD = p_UsuarioUpd,
           FECHA_UPD = SYSDATE,
           NUMERO_FACTURA = p_NumeroFactura,
           FECHA_FACTURA = p_FechaFactura,
           TIPO_IMPUESTO_ID = p_TipoImpuestoId
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND CODIGO_BIEN = p_CodigoBien;

    COMMIT;

    BM.SP_BM_BIEN_GET_ID(
        p_CodigoEmpresa,
        p_CodigoBien,
        p_ResultSet,
        p_Message,
        p_TotalRecords
    );
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT NULL CODIGO_BIEN FROM DUAL WHERE 1 = 0;
END;
/

CREATE OR REPLACE PROCEDURE BM.SP_BM_FOTO_GET_PLACA (
    p_CodigoEmpresa IN NUMBER,
    p_NumeroPlaca IN VARCHAR2,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
BEGIN
    SELECT COUNT(1)
      INTO p_TotalRecords
      FROM BM.BM_BIENES_FOTO
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND NUMERO_PLACA = p_NumeroPlaca;

    OPEN p_ResultSet FOR
        SELECT CODIGO_BIEN_FOTO,
               CODIGO_BIEN,
               NUMERO_PLACA,
               FOTO,
               TITULO,
               FOTO PATCH
          FROM BM.BM_BIENES_FOTO
         WHERE CODIGO_EMPRESA = p_CodigoEmpresa
           AND NUMERO_PLACA = p_NumeroPlaca
         ORDER BY CODIGO_BIEN_FOTO DESC;

    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT NULL CODIGO_BIEN_FOTO FROM DUAL WHERE 1 = 0;
END;
/

CREATE OR REPLACE PROCEDURE BM.SP_BM_FOTO_INS (
    p_CodigoEmpresa IN NUMBER,
    p_CodigoBien IN NUMBER,
    p_NumeroPlaca IN VARCHAR2,
    p_Foto IN VARCHAR2,
    p_Titulo IN VARCHAR2,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
    v_Codigo NUMBER;
BEGIN
    SELECT NVL(MAX(CODIGO_BIEN_FOTO), 0) + 1
      INTO v_Codigo
      FROM BM.BM_BIENES_FOTO;

    INSERT INTO BM.BM_BIENES_FOTO (
        CODIGO_BIEN_FOTO,
        CODIGO_BIEN,
        NUMERO_PLACA,
        FOTO,
        TITULO,
        FECHA_INS,
        CODIGO_EMPRESA
    ) VALUES (
        v_Codigo,
        p_CodigoBien,
        p_NumeroPlaca,
        p_Foto,
        p_Titulo,
        SYSDATE,
        p_CodigoEmpresa
    );

    COMMIT;

    BM.SP_BM_FOTO_GET_PLACA(
        p_CodigoEmpresa,
        p_NumeroPlaca,
        p_ResultSet,
        p_Message,
        p_TotalRecords
    );
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT NULL CODIGO_BIEN_FOTO FROM DUAL WHERE 1 = 0;
END;
/

CREATE OR REPLACE PROCEDURE BM.SP_BM_FOTO_DEL (
    p_CodigoEmpresa IN NUMBER,
    p_CodigoBienFoto IN NUMBER,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
    v_NumeroPlaca BM.BM_BIENES_FOTO.NUMERO_PLACA%TYPE;
BEGIN
    SELECT NUMERO_PLACA
      INTO v_NumeroPlaca
      FROM BM.BM_BIENES_FOTO
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND CODIGO_BIEN_FOTO = p_CodigoBienFoto;

    DELETE FROM BM.BM_BIENES_FOTO
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND CODIGO_BIEN_FOTO = p_CodigoBienFoto;

    COMMIT;

    BM.SP_BM_FOTO_GET_PLACA(
        p_CodigoEmpresa,
        v_NumeroPlaca,
        p_ResultSet,
        p_Message,
        p_TotalRecords
    );
EXCEPTION
    WHEN NO_DATA_FOUND THEN
        p_TotalRecords := 0;
        p_Message := 'success';
        OPEN p_ResultSet FOR SELECT NULL CODIGO_BIEN_FOTO FROM DUAL WHERE 1 = 0;
    WHEN OTHERS THEN
        ROLLBACK;
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT NULL CODIGO_BIEN_FOTO FROM DUAL WHERE 1 = 0;
END;
/
