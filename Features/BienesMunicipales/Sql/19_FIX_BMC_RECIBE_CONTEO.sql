-- Implementa la recepcion real de placas del proceso Contar.
-- Formato por linea: id|placa|icp_fisico|codigo_dir_bien|codigo_conteo-conteo-descripcion
CREATE OR REPLACE PROCEDURE BMC.SP_BM_CONT_DET_REC (
    p_CodigoEmpresa IN NUMBER,
    p_ItemsCsv IN CLOB,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
    v_Pos NUMBER := 1;
    v_Next NUMBER;
    v_Length NUMBER;
    v_Line VARCHAR2(32767);
    v_Sep1 NUMBER;
    v_Sep2 NUMBER;
    v_Sep3 NUMBER;
    v_Sep4 NUMBER;
    v_Key VARCHAR2(4000);
    v_KeySep1 NUMBER;
    v_KeySep2 NUMBER;
    v_Placa VARCHAR2(4000);
    v_IcpFisico NUMBER;
    v_CodigoDirBien NUMBER;
    v_CodigoIcp NUMBER;
    v_UnidadTrabajo VARCHAR2(200);
    v_CodigoDetalle NUMBER;
    v_CodigoConteo NUMBER;
    v_Conteo NUMBER;
    v_UltimoConteo NUMBER;
    v_Actualizados NUMBER := 0;
BEGIN
    SAVEPOINT SP_BM_CONT_DET_REC_INI;
    v_Length := DBMS_LOB.GETLENGTH(p_ItemsCsv);

    IF v_Length = 0 THEN
        RAISE_APPLICATION_ERROR(-20001, 'Debe enviar al menos una placa');
    END IF;

    WHILE v_Pos <= v_Length LOOP
        v_Next := DBMS_LOB.INSTR(p_ItemsCsv, CHR(10), v_Pos);
        IF v_Next = 0 THEN
            v_Next := v_Length + 1;
        END IF;

        v_Line := RTRIM(DBMS_LOB.SUBSTR(p_ItemsCsv, v_Next - v_Pos, v_Pos), CHR(13));
        v_Pos := v_Next + 1;

        IF LENGTH(TRIM(v_Line)) > 0 THEN
            v_Sep1 := INSTR(v_Line, '|', 1, 1);
            v_Sep2 := INSTR(v_Line, '|', 1, 2);
            v_Sep3 := INSTR(v_Line, '|', 1, 3);
            v_Sep4 := INSTR(v_Line, '|', 1, 4);

            IF v_Sep1 = 0 OR v_Sep2 = 0 OR v_Sep3 = 0 OR v_Sep4 = 0 THEN
                RAISE_APPLICATION_ERROR(-20002, 'Formato de placa invalido');
            END IF;

            v_Placa := TRIM(SUBSTR(v_Line, v_Sep1 + 1, v_Sep2 - v_Sep1 - 1));
            v_IcpFisico := TO_NUMBER(TRIM(SUBSTR(v_Line, v_Sep2 + 1, v_Sep3 - v_Sep2 - 1)));
            v_CodigoDirBien := TO_NUMBER(TRIM(SUBSTR(v_Line, v_Sep3 + 1, v_Sep4 - v_Sep3 - 1)));
            v_Key := SUBSTR(v_Line, v_Sep4 + 1);
            v_KeySep1 := INSTR(v_Key, '-', 1, 1);
            v_KeySep2 := INSTR(v_Key, '-', 1, 2);

            IF v_KeySep1 = 0 OR v_KeySep2 = 0 THEN
                RAISE_APPLICATION_ERROR(-20003, 'Identificador de conteo invalido');
            END IF;

            v_CodigoConteo := TO_NUMBER(TRIM(SUBSTR(v_Key, 1, v_KeySep1 - 1)));
            v_Conteo := TO_NUMBER(TRIM(SUBSTR(v_Key, v_KeySep1 + 1, v_KeySep2 - v_KeySep1 - 1)));

            SELECT U.CODIGO_ICP, U.UNIDAD_EJECUTORA
              INTO v_CodigoIcp, v_UnidadTrabajo
              FROM BMC.BM_V_UBICACIONES U
             WHERE U.CODIGO_DIR_BIEN = v_CodigoDirBien
               AND ROWNUM = 1;

            UPDATE BMC.BM_CONTEO_DETALLE
               SET CANTIDAD_CONTADA = 1,
                   DIFERENCIA = NVL(CANTIDAD, 0) - 1,
                   CODIGO_ICP = v_CodigoIcp,
                   UNIDAD_TRABAJO = v_UnidadTrabajo,
                   CODIGO_ICP_FISICO = v_IcpFisico,
                   FECHA_UPD = SYSDATE
             WHERE CODIGO_EMPRESA = p_CodigoEmpresa
               AND CODIGO_BM_CONTEO = v_CodigoConteo
               AND CONTEO = v_Conteo
               AND CODIGO_ICP = v_CodigoIcp
               AND TRIM(NUMERO_PLACA) = v_Placa;

            IF SQL%ROWCOUNT = 0 THEN
                SELECT BMC.BM_S_CODIGO_CONTEO_DET.NEXTVAL
                  INTO v_CodigoDetalle
                  FROM DUAL;

                INSERT INTO BMC.BM_CONTEO_DETALLE (
                    CODIGO_BM_CONTEO_DETALLE, CODIGO_BM_CONTEO, CONTEO,
                    CODIGO_ICP, UNIDAD_TRABAJO, CODIGO_GRUPO, CODIGO_NIVEL1,
                    CODIGO_NIVEL2, NUMERO_LOTE, CANTIDAD, NUMERO_PLACA,
                    VALOR_ACTUAL, ARTICULO, ESPECIFICACION, SERVICIO,
                    RESPONSABLE_BIEN, FECHA_MOVIMIENTO, CODIGO_BIEN,
                    CODIGO_MOV_BIEN, CANTIDAD_CONTADA, DIFERENCIA,
                    CODIGO_EMPRESA, FECHA_INS, COMENTARIO,
                    REPLICAR_COMENTARIO, CODIGO_ICP_FISICO
                )
                SELECT v_CodigoDetalle, v_CodigoConteo, v_Conteo,
                       v_CodigoIcp, v_UnidadTrabajo, V.CODIGO_GRUPO,
                       V.CODIGO_NIVEL1, V.CODIGO_NIVEL2, V.NUMERO_LOTE,
                       V.CANTIDAD, V.NRO_PLACA, V.VALOR_ACTUAL, V.ARTICULO,
                       V.ESPECIFICACION, V.SERVICIO, V.RESPONSABLE_BIEN,
                       V.FECHA_MOVIMIENTO, V.CODIGO_BIEN, V.CODIGO_MOV_BIEN,
                       1, NVL(V.CANTIDAD, 0) - 1, p_CodigoEmpresa,
                       SYSDATE, NULL, 0, v_IcpFisico
                  FROM BMC.BM_V_BM1 V
                 WHERE V.CODIGO_EMPRESA = p_CodigoEmpresa
                   AND TRIM(V.NRO_PLACA) = v_Placa
                   AND ROWNUM = 1;

                IF SQL%ROWCOUNT = 0 THEN
                    RAISE_APPLICATION_ERROR(-20004, 'Placa no encontrada en BMC.BM_V_BM1: ' || v_Placa);
                END IF;
            ELSIF SQL%ROWCOUNT > 1 THEN
                RAISE_APPLICATION_ERROR(-20005, 'La placa esta duplicada en el detalle: ' || v_Placa);
            END IF;

            v_Actualizados := v_Actualizados + 1;
            v_UltimoConteo := v_CodigoConteo;
        END IF;
    END LOOP;

    p_TotalRecords := v_Actualizados;
    p_Message := 'Success';
    OPEN p_ResultSet FOR
        SELECT D.CODIGO_BM_CONTEO_DETALLE, D.CODIGO_BM_CONTEO, D.CONTEO,
               D.CODIGO_ICP, D.UNIDAD_TRABAJO, D.COMENTARIO,
               D.NUMERO_PLACA CODIGO_PLACA, D.CANTIDAD, D.CANTIDAD_CONTADA,
               0 CANTIDAD_CONTADA_OTRO, D.DIFERENCIA, D.CODIGO_GRUPO,
               D.CODIGO_NIVEL1, D.CODIGO_NIVEL2, D.NUMERO_LOTE,
               D.NUMERO_PLACA, D.VALOR_ACTUAL, D.ARTICULO, D.ESPECIFICACION,
               D.SERVICIO, D.RESPONSABLE_BIEN, D.FECHA_MOVIMIENTO,
               D.CODIGO_BIEN, D.CODIGO_MOV_BIEN, C.FECHA,
               NVL(D.REPLICAR_COMENTARIO, 0) REPLICAR_COMENTARIO
          FROM BMC.BM_CONTEO_DETALLE D, BMC.BM_CONTEO C
         WHERE D.CODIGO_EMPRESA = p_CodigoEmpresa
           AND D.CODIGO_BM_CONTEO = v_UltimoConteo
           AND C.CODIGO_BM_CONTEO(+) = D.CODIGO_BM_CONTEO
         ORDER BY D.UNIDAD_TRABAJO, D.ARTICULO, D.NUMERO_PLACA, D.CONTEO;
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK TO SP_BM_CONT_DET_REC_INI;
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR
            SELECT D.CODIGO_BM_CONTEO_DETALLE, D.CODIGO_BM_CONTEO, D.CONTEO,
                   D.CODIGO_ICP, D.UNIDAD_TRABAJO, D.COMENTARIO,
                   D.NUMERO_PLACA CODIGO_PLACA, D.CANTIDAD, D.CANTIDAD_CONTADA,
                   0 CANTIDAD_CONTADA_OTRO, D.DIFERENCIA, D.CODIGO_GRUPO,
                   D.CODIGO_NIVEL1, D.CODIGO_NIVEL2, D.NUMERO_LOTE,
                   D.NUMERO_PLACA, D.VALOR_ACTUAL, D.ARTICULO, D.ESPECIFICACION,
                   D.SERVICIO, D.RESPONSABLE_BIEN, D.FECHA_MOVIMIENTO,
                   D.CODIGO_BIEN, D.CODIGO_MOV_BIEN, CAST(NULL AS DATE) FECHA,
                   NVL(D.REPLICAR_COMENTARIO, 0) REPLICAR_COMENTARIO
              FROM BMC.BM_CONTEO_DETALLE D WHERE 1 = 0;
END SP_BM_CONT_DET_REC;
/

SHOW ERRORS PROCEDURE BMC.SP_BM_CONT_DET_REC;
