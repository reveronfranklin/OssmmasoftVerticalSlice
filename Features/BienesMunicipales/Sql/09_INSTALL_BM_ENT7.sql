CREATE TABLE BM.BM_PROC_MASIVO (
    CODIGO_PROC_MASIVO NUMBER(10,0) NOT NULL,
    CODIGO_EMPRESA NUMBER(10,0) NOT NULL,
    FECHA_PROC DATE DEFAULT SYSDATE NOT NULL,
    TIPO_PROC VARCHAR2(30),
    CODIGO_DIR_ORIGEN NUMBER(10,0),
    CODIGO_ICP NUMBER(10,0),
    CODIGO_ARTICULO NUMBER(10,0),
    CODIGO_DIR_DESTINO NUMBER(10,0),
    CONCEPTO_MOV_ID NUMBER(10,0),
    FECHA_MOVIMIENTO DATE,
    USUARIO_ID NUMBER(10,0),
    OBSERVACION VARCHAR2(500),
    TOTAL_PROCESADOS NUMBER(10,0),
    TOTAL_EXITOSOS NUMBER(10,0),
    TOTAL_RECHAZADOS NUMBER(10,0),
    FECHA_INS DATE DEFAULT SYSDATE,
    CONSTRAINT PK_BM_PROC_MASIVO PRIMARY KEY (CODIGO_PROC_MASIVO)
);

CREATE TABLE BM.BM_PROC_MAS_DET (
    CODIGO_PROC_MAS_DET NUMBER(10,0) NOT NULL,
    CODIGO_PROC_MASIVO NUMBER(10,0) NOT NULL,
    CODIGO_EMPRESA NUMBER(10,0) NOT NULL,
    CODIGO_BIEN NUMBER(10,0),
    NUMERO_PLACA VARCHAR2(50),
    CODIGO_MOV_BIEN NUMBER(10,0),
    ESTADO VARCHAR2(20),
    MENSAJE VARCHAR2(500),
    FECHA_INS DATE DEFAULT SYSDATE,
    CONSTRAINT PK_BM_PROC_MAS_DET PRIMARY KEY (CODIGO_PROC_MAS_DET),
    CONSTRAINT FK_BM_PROC_MAS_DET FOREIGN KEY (CODIGO_PROC_MASIVO)
        REFERENCES BM.BM_PROC_MASIVO (CODIGO_PROC_MASIVO)
);

CREATE OR REPLACE PROCEDURE BM.SP_BM_PROC_MAS_PRE (
    p_CodigoEmpresa IN NUMBER,
    p_CodigoIcp IN NUMBER,
    p_CodigoDirOrigen IN NUMBER,
    p_CodigoArticulo IN NUMBER,
    p_PlacasCsv IN VARCHAR2,
    p_ResponsableText IN VARCHAR2,
    p_CodigoDirDestino IN NUMBER,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
BEGIN
    SELECT COUNT(1)
      INTO p_TotalRecords
      FROM BM.BM_BIENES B,
           BM.BM_ARTICULOS A,
           BM.BM_MOV_BIENES M,
           BM.BM_DIR_BIEN D
     WHERE A.CODIGO_ARTICULO = B.CODIGO_ARTICULO
       AND M.CODIGO_MOV_BIEN = (
            SELECT MAX(X.CODIGO_MOV_BIEN)
              FROM BM.BM_MOV_BIENES X
             WHERE X.CODIGO_EMPRESA = B.CODIGO_EMPRESA
               AND X.CODIGO_BIEN = B.CODIGO_BIEN
       )
       AND D.CODIGO_DIR_BIEN = M.CODIGO_DIR_BIEN
       AND B.CODIGO_EMPRESA = p_CodigoEmpresa
       AND NVL(M.TIPO_MOVIMIENTO, 'A') NOT IN ('D', 'E')
       AND (NVL(p_CodigoIcp, 0) = 0 OR D.CODIGO_ICP = p_CodigoIcp)
       AND (NVL(p_CodigoDirOrigen, 0) = 0 OR M.CODIGO_DIR_BIEN = p_CodigoDirOrigen)
       AND (NVL(p_CodigoArticulo, 0) = 0 OR B.CODIGO_ARTICULO = p_CodigoArticulo)
       AND (
            TRIM(p_PlacasCsv) IS NULL
            OR INSTR(',' || UPPER(p_PlacasCsv) || ',', ',' || UPPER(TRIM(B.NUMERO_PLACA)) || ',') > 0
            OR INSTR(',' || REPLACE(REPLACE(UPPER(p_PlacasCsv), '-', ''), ' ', '') || ',',
                     ',' || REPLACE(REPLACE(UPPER(TRIM(B.NUMERO_PLACA)), '-', ''), ' ', '') || ',') > 0
       )
       AND (TRIM(p_ResponsableText) IS NULL OR UPPER(BM.BM_PKG_UTIL.GET_ESPECIFICACION_RESP(B.CODIGO_BIEN)) LIKE '%' || UPPER(p_ResponsableText) || '%');

    OPEN p_ResultSet FOR
        SELECT 0 CODIGO_PROC_MASIVO,
               0 CODIGO_PROC_MAS_DET,
               B.CODIGO_BIEN,
               B.NUMERO_PLACA,
               A.DENOMINACION ARTICULO,
               M.CODIGO_DIR_BIEN CODIGO_DIR_ORIGEN,
               D.CODIGO_ICP CODIGO_ICP_ORIGEN,
               NVL(P.UNIDAD_EJECUTORA, P.DENOMINACION) UNIDAD_ORIGEN,
               p_CodigoDirDestino CODIGO_DIR_DESTINO,
               NVL(PD.UNIDAD_EJECUTORA, PD.DENOMINACION) UNIDAD_DESTINO,
               'PREVIEW' ESTADO,
               'Bien seleccionado para cambio masivo.' MENSAJE,
               0 CODIGO_MOV_BIEN,
               p_TotalRecords TOTAL_PROCESADOS,
               0 TOTAL_EXITOSOS,
               0 TOTAL_RECHAZADOS
          FROM BM.BM_BIENES B,
               BM.BM_ARTICULOS A,
               BM.BM_MOV_BIENES M,
               BM.BM_DIR_BIEN D,
               PRE.PRE_INDICE_CAT_PRG P,
               BM.BM_DIR_BIEN DD,
               PRE.PRE_INDICE_CAT_PRG PD
         WHERE A.CODIGO_ARTICULO = B.CODIGO_ARTICULO
           AND M.CODIGO_MOV_BIEN = (
                SELECT MAX(X.CODIGO_MOV_BIEN)
                  FROM BM.BM_MOV_BIENES X
                 WHERE X.CODIGO_EMPRESA = B.CODIGO_EMPRESA
                   AND X.CODIGO_BIEN = B.CODIGO_BIEN
           )
           AND D.CODIGO_DIR_BIEN = M.CODIGO_DIR_BIEN
           AND P.CODIGO_ICP(+) = D.CODIGO_ICP
           AND DD.CODIGO_DIR_BIEN(+) = p_CodigoDirDestino
           AND PD.CODIGO_ICP(+) = DD.CODIGO_ICP
           AND B.CODIGO_EMPRESA = p_CodigoEmpresa
           AND NVL(M.TIPO_MOVIMIENTO, 'A') NOT IN ('D', 'E')
           AND (NVL(p_CodigoIcp, 0) = 0 OR D.CODIGO_ICP = p_CodigoIcp)
           AND (NVL(p_CodigoDirOrigen, 0) = 0 OR M.CODIGO_DIR_BIEN = p_CodigoDirOrigen)
           AND (NVL(p_CodigoArticulo, 0) = 0 OR B.CODIGO_ARTICULO = p_CodigoArticulo)
           AND (
                TRIM(p_PlacasCsv) IS NULL
                OR INSTR(',' || UPPER(p_PlacasCsv) || ',', ',' || UPPER(TRIM(B.NUMERO_PLACA)) || ',') > 0
                OR INSTR(',' || REPLACE(REPLACE(UPPER(p_PlacasCsv), '-', ''), ' ', '') || ',',
                         ',' || REPLACE(REPLACE(UPPER(TRIM(B.NUMERO_PLACA)), '-', ''), ' ', '') || ',') > 0
           )
           AND (TRIM(p_ResponsableText) IS NULL OR UPPER(BM.BM_PKG_UTIL.GET_ESPECIFICACION_RESP(B.CODIGO_BIEN)) LIKE '%' || UPPER(p_ResponsableText) || '%')
         ORDER BY D.CODIGO_ICP, B.NUMERO_PLACA;

    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR
            SELECT 0 CODIGO_PROC_MASIVO,
                   0 CODIGO_PROC_MAS_DET,
                   0 CODIGO_BIEN,
                   NULL NUMERO_PLACA,
                   NULL ARTICULO,
                   0 CODIGO_DIR_ORIGEN,
                   0 CODIGO_ICP_ORIGEN,
                   NULL UNIDAD_ORIGEN,
                   0 CODIGO_DIR_DESTINO,
                   NULL UNIDAD_DESTINO,
                   NULL ESTADO,
                   NULL MENSAJE,
                   0 CODIGO_MOV_BIEN,
                   0 TOTAL_PROCESADOS,
                   0 TOTAL_EXITOSOS,
                   0 TOTAL_RECHAZADOS
              FROM DUAL
             WHERE 1 = 0;
END;
/

CREATE OR REPLACE PROCEDURE BM.SP_BM_PROC_MAS_EJE (
    p_CodigoEmpresa IN NUMBER,
    p_CodigoIcp IN NUMBER,
    p_CodigoDirOrigen IN NUMBER,
    p_CodigoArticulo IN NUMBER,
    p_PlacasCsv IN VARCHAR2,
    p_ResponsableText IN VARCHAR2,
    p_CodigoDirDestino IN NUMBER,
    p_ConceptoMovId IN NUMBER,
    p_FechaMovimiento IN DATE,
    p_UsuarioId IN NUMBER,
    p_Observacion IN VARCHAR2,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
    v_CodigoProc NUMBER;
    v_CodigoDet NUMBER;
    v_CodigoMov NUMBER;
    v_Existe NUMBER;
    v_Total NUMBER := 0;
    v_Exitos NUMBER := 0;
    v_Rechazos NUMBER := 0;
BEGIN
    IF NVL(p_CodigoDirDestino, 0) = 0 OR NVL(p_ConceptoMovId, 0) = 0 OR p_FechaMovimiento IS NULL THEN
        p_TotalRecords := 0;
        p_Message := 'Debe indicar ubicacion destino, concepto y fecha.';
        OPEN p_ResultSet FOR SELECT NULL CODIGO_PROC_MASIVO FROM DUAL WHERE 1 = 0;
        RETURN;
    END IF;

    SELECT COUNT(1)
      INTO v_Existe
      FROM BM.BM_DIR_BIEN
     WHERE CODIGO_EMPRESA = p_CodigoEmpresa
       AND CODIGO_DIR_BIEN = p_CodigoDirDestino;

    IF v_Existe = 0 THEN
        p_TotalRecords := 0;
        p_Message := 'La ubicacion destino no existe.';
        OPEN p_ResultSet FOR SELECT NULL CODIGO_PROC_MASIVO FROM DUAL WHERE 1 = 0;
        RETURN;
    END IF;

    SELECT NVL(MAX(CODIGO_PROC_MASIVO), 0) + 1
      INTO v_CodigoProc
      FROM BM.BM_PROC_MASIVO;

    INSERT INTO BM.BM_PROC_MASIVO (
        CODIGO_PROC_MASIVO, CODIGO_EMPRESA, FECHA_PROC, TIPO_PROC,
        CODIGO_DIR_ORIGEN, CODIGO_ICP, CODIGO_ARTICULO, CODIGO_DIR_DESTINO,
        CONCEPTO_MOV_ID, FECHA_MOVIMIENTO, USUARIO_ID, OBSERVACION,
        TOTAL_PROCESADOS, TOTAL_EXITOSOS, TOTAL_RECHAZADOS, FECHA_INS
    ) VALUES (
        v_CodigoProc, p_CodigoEmpresa, SYSDATE, 'CAMBIO_DIRECCION',
        p_CodigoDirOrigen, p_CodigoIcp, p_CodigoArticulo, p_CodigoDirDestino,
        p_ConceptoMovId, p_FechaMovimiento, p_UsuarioId, p_Observacion,
        0, 0, 0, SYSDATE
    );

    FOR r IN (
        SELECT B.CODIGO_BIEN,
               B.NUMERO_PLACA,
               A.DENOMINACION ARTICULO,
               M.TIPO_MOVIMIENTO,
               M.CODIGO_DIR_BIEN CODIGO_DIR_ORIGEN,
               D.CODIGO_ICP CODIGO_ICP_ORIGEN,
               NVL(P.UNIDAD_EJECUTORA, P.DENOMINACION) UNIDAD_ORIGEN
          FROM BM.BM_BIENES B,
               BM.BM_ARTICULOS A,
               BM.BM_MOV_BIENES M,
               BM.BM_DIR_BIEN D,
               PRE.PRE_INDICE_CAT_PRG P
         WHERE A.CODIGO_ARTICULO = B.CODIGO_ARTICULO
           AND M.CODIGO_MOV_BIEN = (
                SELECT MAX(X.CODIGO_MOV_BIEN)
                  FROM BM.BM_MOV_BIENES X
                 WHERE X.CODIGO_EMPRESA = B.CODIGO_EMPRESA
                   AND X.CODIGO_BIEN = B.CODIGO_BIEN
           )
           AND D.CODIGO_DIR_BIEN = M.CODIGO_DIR_BIEN
           AND P.CODIGO_ICP(+) = D.CODIGO_ICP
           AND B.CODIGO_EMPRESA = p_CodigoEmpresa
           AND (NVL(p_CodigoIcp, 0) = 0 OR D.CODIGO_ICP = p_CodigoIcp)
           AND (NVL(p_CodigoDirOrigen, 0) = 0 OR M.CODIGO_DIR_BIEN = p_CodigoDirOrigen)
           AND (NVL(p_CodigoArticulo, 0) = 0 OR B.CODIGO_ARTICULO = p_CodigoArticulo)
           AND (
                TRIM(p_PlacasCsv) IS NULL
                OR INSTR(',' || UPPER(p_PlacasCsv) || ',', ',' || UPPER(TRIM(B.NUMERO_PLACA)) || ',') > 0
                OR INSTR(',' || REPLACE(REPLACE(UPPER(p_PlacasCsv), '-', ''), ' ', '') || ',',
                         ',' || REPLACE(REPLACE(UPPER(TRIM(B.NUMERO_PLACA)), '-', ''), ' ', '') || ',') > 0
           )
           AND (TRIM(p_ResponsableText) IS NULL OR UPPER(BM.BM_PKG_UTIL.GET_ESPECIFICACION_RESP(B.CODIGO_BIEN)) LIKE '%' || UPPER(p_ResponsableText) || '%')
         ORDER BY D.CODIGO_ICP, B.NUMERO_PLACA
    ) LOOP
        v_Total := v_Total + 1;
        SELECT NVL(MAX(CODIGO_PROC_MAS_DET), 0) + 1
          INTO v_CodigoDet
          FROM BM.BM_PROC_MAS_DET;

        IF NVL(r.TIPO_MOVIMIENTO, 'A') IN ('D', 'E') THEN
            v_Rechazos := v_Rechazos + 1;
            INSERT INTO BM.BM_PROC_MAS_DET (
                CODIGO_PROC_MAS_DET, CODIGO_PROC_MASIVO, CODIGO_EMPRESA,
                CODIGO_BIEN, NUMERO_PLACA, CODIGO_MOV_BIEN, ESTADO, MENSAJE, FECHA_INS
            ) VALUES (
                v_CodigoDet, v_CodigoProc, p_CodigoEmpresa,
                r.CODIGO_BIEN, r.NUMERO_PLACA, NULL, 'RECHAZADO',
                'El bien tiene movimiento final.', SYSDATE
            );
        ELSIF r.CODIGO_DIR_ORIGEN = p_CodigoDirDestino THEN
            v_Rechazos := v_Rechazos + 1;
            INSERT INTO BM.BM_PROC_MAS_DET (
                CODIGO_PROC_MAS_DET, CODIGO_PROC_MASIVO, CODIGO_EMPRESA,
                CODIGO_BIEN, NUMERO_PLACA, CODIGO_MOV_BIEN, ESTADO, MENSAJE, FECHA_INS
            ) VALUES (
                v_CodigoDet, v_CodigoProc, p_CodigoEmpresa,
                r.CODIGO_BIEN, r.NUMERO_PLACA, NULL, 'RECHAZADO',
                'El bien ya esta en la ubicacion destino.', SYSDATE
            );
        ELSE
            SELECT NVL(MAX(CODIGO_MOV_BIEN), 0) + 1
              INTO v_CodigoMov
              FROM BM.BM_MOV_BIENES;

            INSERT INTO BM.BM_MOV_BIENES (
                CODIGO_MOV_BIEN, CODIGO_BIEN, TIPO_MOVIMIENTO, FECHA_MOVIMIENTO,
                CODIGO_DIR_BIEN, EXTRA1, EXTRA2, EXTRA3, FECHA_INS,
                CODIGO_EMPRESA, CONCEPTO_MOV_ID, CODIGO_SOL_MOV_BIEN
            ) VALUES (
                v_CodigoMov, r.CODIGO_BIEN, 'T', p_FechaMovimiento,
                p_CodigoDirDestino, SUBSTR(p_Observacion, 1, 100),
                'PROC_MASIVO', TO_CHAR(v_CodigoProc), SYSDATE,
                p_CodigoEmpresa, p_ConceptoMovId, NULL
            );

            v_Exitos := v_Exitos + 1;
            INSERT INTO BM.BM_PROC_MAS_DET (
                CODIGO_PROC_MAS_DET, CODIGO_PROC_MASIVO, CODIGO_EMPRESA,
                CODIGO_BIEN, NUMERO_PLACA, CODIGO_MOV_BIEN, ESTADO, MENSAJE, FECHA_INS
            ) VALUES (
                v_CodigoDet, v_CodigoProc, p_CodigoEmpresa,
                r.CODIGO_BIEN, r.NUMERO_PLACA, v_CodigoMov, 'EXITOSO',
                'Movimiento generado.', SYSDATE
            );
        END IF;
    END LOOP;

    UPDATE BM.BM_PROC_MASIVO
       SET TOTAL_PROCESADOS = v_Total,
           TOTAL_EXITOSOS = v_Exitos,
           TOTAL_RECHAZADOS = v_Rechazos
     WHERE CODIGO_PROC_MASIVO = v_CodigoProc;

    COMMIT;

    p_TotalRecords := v_Total;
    p_Message := 'success';

    OPEN p_ResultSet FOR
        SELECT H.CODIGO_PROC_MASIVO,
               D.CODIGO_PROC_MAS_DET,
               D.CODIGO_BIEN,
               D.NUMERO_PLACA,
               A.DENOMINACION ARTICULO,
               NVL(M0.CODIGO_DIR_BIEN, 0) CODIGO_DIR_ORIGEN,
               NVL(D0.CODIGO_ICP, 0) CODIGO_ICP_ORIGEN,
               NVL(P0.UNIDAD_EJECUTORA, P0.DENOMINACION) UNIDAD_ORIGEN,
               H.CODIGO_DIR_DESTINO,
               NVL(PD.UNIDAD_EJECUTORA, PD.DENOMINACION) UNIDAD_DESTINO,
               D.ESTADO,
               D.MENSAJE,
               NVL(D.CODIGO_MOV_BIEN, 0) CODIGO_MOV_BIEN,
               H.TOTAL_PROCESADOS,
               H.TOTAL_EXITOSOS,
               H.TOTAL_RECHAZADOS
          FROM BM.BM_PROC_MASIVO H
          JOIN BM.BM_PROC_MAS_DET D
            ON D.CODIGO_PROC_MASIVO = H.CODIGO_PROC_MASIVO
          LEFT JOIN BM.BM_BIENES B
            ON B.CODIGO_BIEN = D.CODIGO_BIEN
          LEFT JOIN BM.BM_ARTICULOS A
            ON A.CODIGO_ARTICULO = B.CODIGO_ARTICULO
          LEFT JOIN BM.BM_MOV_BIENES M0
            ON M0.CODIGO_EMPRESA = B.CODIGO_EMPRESA
           AND M0.CODIGO_BIEN = B.CODIGO_BIEN
           AND M0.CODIGO_MOV_BIEN <> NVL(D.CODIGO_MOV_BIEN, 0)
          LEFT JOIN BM.BM_MOV_BIENES M1
            ON M1.CODIGO_EMPRESA = B.CODIGO_EMPRESA
           AND M1.CODIGO_BIEN = B.CODIGO_BIEN
           AND M1.CODIGO_MOV_BIEN <> NVL(D.CODIGO_MOV_BIEN, 0)
           AND M1.CODIGO_MOV_BIEN > M0.CODIGO_MOV_BIEN
          LEFT JOIN BM.BM_DIR_BIEN D0
            ON D0.CODIGO_DIR_BIEN = M0.CODIGO_DIR_BIEN
          LEFT JOIN PRE.PRE_INDICE_CAT_PRG P0
            ON P0.CODIGO_ICP = D0.CODIGO_ICP
          LEFT JOIN BM.BM_DIR_BIEN DD
            ON DD.CODIGO_DIR_BIEN = H.CODIGO_DIR_DESTINO
          LEFT JOIN PRE.PRE_INDICE_CAT_PRG PD
            ON PD.CODIGO_ICP = DD.CODIGO_ICP
         WHERE H.CODIGO_PROC_MASIVO = v_CodigoProc
           AND M1.CODIGO_MOV_BIEN IS NULL
         ORDER BY D.CODIGO_PROC_MAS_DET;
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR
            SELECT 0 CODIGO_PROC_MASIVO,
                   0 CODIGO_PROC_MAS_DET,
                   0 CODIGO_BIEN,
                   NULL NUMERO_PLACA,
                   NULL ARTICULO,
                   0 CODIGO_DIR_ORIGEN,
                   0 CODIGO_ICP_ORIGEN,
                   NULL UNIDAD_ORIGEN,
                   0 CODIGO_DIR_DESTINO,
                   NULL UNIDAD_DESTINO,
                   NULL ESTADO,
                   NULL MENSAJE,
                   0 CODIGO_MOV_BIEN,
                   0 TOTAL_PROCESADOS,
                   0 TOTAL_EXITOSOS,
                   0 TOTAL_RECHAZADOS
              FROM DUAL
             WHERE 1 = 0;
END;
/
