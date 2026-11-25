-- Requerimiento 17. Oracle 10g. Identificador SP_REP_COP_BASE_GET: 19 caracteres.
-- Proveedores NULL = todos. Fechas obligatorias, aplicadas al compromiso.
-- Presupuesto tomado de cada compromiso, sin seleccionar el ejercicio mas reciente.
-- Sin PUC de entrada: las ramas legacy A.CODIGO_PUC = NULL no generan asignaciones
-- ni modificaciones. La fuente BASE es identica en los tres procedimientos.
-- Los hijos enlazan tambien al PUC real del compromiso, evitando colisiones de origen.
CREATE OR REPLACE PROCEDURE PRE.SP_REP_COP_BASE_GET (
    p_CodigoEmpresa IN NUMBER,
    p_FechaDesde IN DATE,
    p_FechaHasta IN DATE,
    p_Proveedores IN VARCHAR2,
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message OUT VARCHAR2
) AS
BEGIN
    IF p_CodigoEmpresa IS NULL OR p_CodigoEmpresa <= 0 OR p_FechaDesde IS NULL
       OR p_FechaHasta IS NULL OR TRUNC(p_FechaDesde) > TRUNC(p_FechaHasta) THEN
        RAISE_APPLICATION_ERROR(-20001, 'Empresa y rango de fechas son requeridos.');
    END IF;
    IF p_Proveedores IS NOT NULL AND
       NOT REGEXP_LIKE(p_Proveedores, '^[1-9][0-9]*(,[1-9][0-9]*)*$') THEN
        RAISE_APPLICATION_ERROR(-20002, 'Lista de proveedores invalida.');
    END IF;
    OPEN p_ResultSet FOR
        WITH BASE AS (
        SELECT A.CODIGO_EMPRESA, A.CODIGO_PRESUPUESTO,
               A.CODIGO_COMPROMISO, A.NUMERO_COMPROMISO, A.FECHA_COMPROMISO,
               A.CODIGO_PROVEEDOR, C.NOMBRE_PROVEEDOR,
               C.NACIONALIDAD || '-' || TO_CHAR(C.CEDULA) || ' / ' || C.RIF CED_RIF,
               A.MOTIVO, B.CODIGO_ICP, B.CODIGO_PUC,
               S.PARTIDA, SUM(NVL(B.MONTO, 0) - NVL(B.MONTO_ANULADO, 0)) MONTO_COMPROMISO,
               (SELECT MAX(I.DENOMINACION_ONP) FROM PRE.PRE_IDENTIFICACIONES I
                 WHERE I.CODIGO_EMPRESA = A.CODIGO_EMPRESA
                   AND I.CODIGO_PRESUPUESTO = A.CODIGO_PRESUPUESTO) ENTIDAD
          FROM PRE.PRE_COMPROMISOS A
          JOIN PRE.PRE_DETALLE_COMPROMISOS AA
            ON AA.CODIGO_COMPROMISO = A.CODIGO_COMPROMISO
          JOIN PRE.PRE_PUC_COMPROMISOS B
            ON B.CODIGO_DETALLE_COMPROMISO = AA.CODIGO_DETALLE_COMPROMISO
           AND B.CODIGO_EMPRESA = A.CODIGO_EMPRESA
           AND B.CODIGO_PRESUPUESTO = A.CODIGO_PRESUPUESTO
          JOIN ADM.ADM_PROVEEDORES C ON C.CODIGO_PROVEEDOR = A.CODIGO_PROVEEDOR
           AND C.CODIGO_EMPRESA = A.CODIGO_EMPRESA
          JOIN (
                SELECT V.CODIGO_PRESUPUESTO, V.CODIGO_SALDO, V.CODIGO_ICP, V.CODIGO_PUC,
                       MAX(V.CODIGO_ICP_CONCAT || ' / ' || V.CODIGO_PUC_CONCAT) PARTIDA
                  FROM PRE.PRE_V_SALDOS V
                 WHERE V.CODIGO_SECTOR >= 0
                   AND EXISTS (SELECT 1 FROM PRE.PRE_PLAN_UNICO_CUENTAS P
                                WHERE P.CODIGO_PRESUPUESTO = V.CODIGO_PRESUPUESTO
                                  AND P.CODIGO_GRUPO = V.CODIGO_GRUPO
                                  AND P.CODIGO_PUC = V.CODIGO_PUC)
                 GROUP BY V.CODIGO_PRESUPUESTO, V.CODIGO_SALDO, V.CODIGO_ICP, V.CODIGO_PUC
               ) S
            ON S.CODIGO_PRESUPUESTO = A.CODIGO_PRESUPUESTO
           AND S.CODIGO_SALDO = B.CODIGO_SALDO
           AND S.CODIGO_ICP = B.CODIGO_ICP AND S.CODIGO_PUC = B.CODIGO_PUC
         WHERE A.CODIGO_EMPRESA = p_CodigoEmpresa AND A.STATUS <> 'AN'
           AND A.FECHA_COMPROMISO >= TRUNC(p_FechaDesde)
           AND A.FECHA_COMPROMISO < TRUNC(p_FechaHasta) + 1
           AND (p_Proveedores IS NULL OR
                INSTR(',' || p_Proveedores || ',', ',' || TO_CHAR(A.CODIGO_PROVEEDOR) || ',') > 0)
         GROUP BY A.CODIGO_EMPRESA, A.CODIGO_PRESUPUESTO, A.CODIGO_COMPROMISO,
                  A.NUMERO_COMPROMISO, A.FECHA_COMPROMISO, A.CODIGO_PROVEEDOR,
                  C.NOMBRE_PROVEEDOR, C.NACIONALIDAD, C.CEDULA, C.RIF, A.MOTIVO,
                  B.CODIGO_ICP, B.CODIGO_PUC, S.PARTIDA
        )
        SELECT * FROM BASE ORDER BY FECHA_COMPROMISO, NUMERO_COMPROMISO, CODIGO_PRESUPUESTO, CODIGO_COMPROMISO, CODIGO_ICP, CODIGO_PUC;
    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SUBSTR(SQLERRM, 1, 4000);
        OPEN p_ResultSet FOR
            SELECT CAST(NULL AS NUMBER) CODIGO_EMPRESA,
                   CAST(NULL AS NUMBER) CODIGO_PRESUPUESTO,
                   CAST(NULL AS NUMBER) CODIGO_COMPROMISO,
                   CAST(NULL AS VARCHAR2(100)) NUMERO_COMPROMISO,
                   CAST(NULL AS DATE) FECHA_COMPROMISO,
                   CAST(NULL AS NUMBER) CODIGO_PROVEEDOR,
                   CAST(NULL AS VARCHAR2(1000)) NOMBRE_PROVEEDOR,
                   CAST(NULL AS VARCHAR2(200)) CED_RIF,
                   CAST(NULL AS VARCHAR2(4000)) MOTIVO,
                   CAST(NULL AS NUMBER) CODIGO_ICP,
                   CAST(NULL AS NUMBER) CODIGO_PUC,
                   CAST(NULL AS VARCHAR2(500)) PARTIDA,
                   CAST(NULL AS NUMBER) MONTO_COMPROMISO,
                   CAST(NULL AS VARCHAR2(1000)) ENTIDAD
              FROM DUAL WHERE 1 = 0;
END SP_REP_COP_BASE_GET;
/
