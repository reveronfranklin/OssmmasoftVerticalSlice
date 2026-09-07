-- Guarda la placa completa para coincidir con la recepcion del conteo.
-- Aplicar en BMC despues de los scripts anteriores. Solo afecta nuevos conteos.
-- No modifica detalles existentes ni elimina duplicados previos.
-- BM_P_CONTEO pertenece a BMC y debe leer/escribir en ese mismo esquema.
-- Lee BMC.BM_V_BM1, construida con las tablas replicadas en BMC. No debe leer
-- BM.BM_V_BM1 porque BM y BMC pueden estar en servidores Oracle diferentes.
CREATE OR REPLACE PROCEDURE BMC.BM_P_CONTEO (
    P_IN_ICP IN VARCHAR2,
    P_CODIGO_EMPRESA IN NUMBER,
    P_USUARIO_INS IN NUMBER,
    P_CODIGO_CONTEO IN NUMBER,
    P_CANTIDAD_CONTEOS IN NUMBER
) IS
    V_CODIGO_CONTEO_DET NUMBER;
    V_TOTAL_INSERTADOS NUMBER := 0;
    TYPE C_CURTYPE1 IS REF CURSOR;
    C_CONTEO C_CURTYPE1;
    V_WHERE VARCHAR2(1000);

    TYPE R1TYPE1 IS RECORD (
        CODIGO_ICP BMC.BM_V_BM1.CODIGO_ICP%TYPE,
        UNIDAD_TRABAJO BMC.BM_V_BM1.UNIDAD_TRABAJO%TYPE,
        CODIGO_GRUPO BMC.BM_V_BM1.CODIGO_GRUPO%TYPE,
        CODIGO_NIVEL1 BMC.BM_V_BM1.CODIGO_NIVEL1%TYPE,
        CODIGO_NIVEL2 BMC.BM_V_BM1.CODIGO_NIVEL2%TYPE,
        NUMERO_LOTE BMC.BM_V_BM1.NUMERO_LOTE%TYPE,
        CANTIDAD BMC.BM_V_BM1.CANTIDAD%TYPE,
        NUMERO_PLACA BMC.BM_V_BM1.NRO_PLACA%TYPE,
        VALOR_ACTUAL BMC.BM_V_BM1.VALOR_ACTUAL%TYPE,
        ARTICULO BMC.BM_V_BM1.ARTICULO%TYPE,
        ESPECIFICACION BMC.BM_V_BM1.ESPECIFICACION%TYPE,
        SERVICIO BMC.BM_V_BM1.SERVICIO%TYPE,
        RESPONSABLE_BIEN BMC.BM_V_BM1.RESPONSABLE_BIEN%TYPE,
        FECHA_MOVIMIENTO BMC.BM_V_BM1.FECHA_MOVIMIENTO%TYPE,
        CODIGO_EMPRESA BMC.BM_V_BM1.CODIGO_EMPRESA%TYPE,
        CODIGO_BIEN BMC.BM_V_BM1.CODIGO_BIEN%TYPE,
        CODIGO_MOV_BIEN BMC.BM_V_BM1.CODIGO_MOV_BIEN%TYPE
    );
    C1R R1TYPE1;
BEGIN
    IF P_IN_ICP = 'TODOS' THEN
        V_WHERE := ' WHERE BV.CODIGO_EMPRESA = :empresa';
    ELSE
        V_WHERE := ' WHERE BV.CODIGO_EMPRESA = :empresa' ||
                   ' AND BV.CODIGO_ICP IN (' || P_IN_ICP || ')';
    END IF;

    FOR I IN 1 .. P_CANTIDAD_CONTEOS LOOP
        OPEN C_CONTEO FOR
            'SELECT BV.CODIGO_ICP, BV.UNIDAD_TRABAJO, BV.CODIGO_GRUPO, ' ||
            'BV.CODIGO_NIVEL1, BV.CODIGO_NIVEL2, BV.NUMERO_LOTE, ' ||
            'BV.CANTIDAD, BV.NRO_PLACA AS NUMERO_PLACA, BV.VALOR_ACTUAL, BV.ARTICULO, ' ||
            'BV.ESPECIFICACION, BV.SERVICIO, BV.RESPONSABLE_BIEN, ' ||
            'BV.FECHA_MOVIMIENTO, BV.CODIGO_EMPRESA, BV.CODIGO_BIEN, ' ||
            'BV.CODIGO_MOV_BIEN FROM BMC.BM_V_BM1 BV ' || V_WHERE
            USING P_CODIGO_EMPRESA;

        LOOP
            FETCH C_CONTEO INTO C1R;
            EXIT WHEN C_CONTEO%NOTFOUND;

            SELECT BMC.BM_S_CODIGO_CONTEO_DET.NEXTVAL
              INTO V_CODIGO_CONTEO_DET
              FROM DUAL;

            INSERT INTO BMC.BM_CONTEO_DETALLE (
                CODIGO_BM_CONTEO_DETALLE,
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
                COMENTARIO
            ) VALUES (
                V_CODIGO_CONTEO_DET,
                P_CODIGO_CONTEO,
                I,
                C1R.CODIGO_ICP,
                C1R.UNIDAD_TRABAJO,
                C1R.CODIGO_GRUPO,
                C1R.CODIGO_NIVEL1,
                C1R.CODIGO_NIVEL2,
                C1R.NUMERO_LOTE,
                C1R.CANTIDAD,
                C1R.NUMERO_PLACA,
                C1R.VALOR_ACTUAL,
                C1R.ARTICULO,
                C1R.ESPECIFICACION,
                C1R.SERVICIO,
                C1R.RESPONSABLE_BIEN,
                C1R.FECHA_MOVIMIENTO,
                C1R.CODIGO_BIEN,
                C1R.CODIGO_MOV_BIEN,
                0,
                C1R.CANTIDAD,
                P_CODIGO_EMPRESA,
                P_USUARIO_INS,
                SYSDATE,
                NULL,
                NULL,
                NULL
            );
            V_TOTAL_INSERTADOS := V_TOTAL_INSERTADOS + 1;
        END LOOP;

        CLOSE C_CONTEO;
    END LOOP;

    IF V_TOTAL_INSERTADOS = 0 THEN
        RAISE_APPLICATION_ERROR(
            -20002,
            'No se encontraron bienes para los ICP seleccionados'
        );
    END IF;
END BM_P_CONTEO;
/

SHOW ERRORS PROCEDURE BMC.BM_P_CONTEO;

