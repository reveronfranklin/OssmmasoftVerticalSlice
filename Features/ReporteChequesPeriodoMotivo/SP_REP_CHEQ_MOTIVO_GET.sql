-- =============================================================================
-- ADM - Relacion de Cheques Emitidos Por Periodos (con Motivo).
--
-- Migracion del reporte Oracle Reports ADM_PERIODOS_CHEQUES_MOTIVO1.rdf
-- (requerimiento 23). Devuelve una fila por linea de beneficiario de cheque
-- (ADM_BENEFICIARIOS_CH); el agrupamiento por banco/cuenta, los subtotales de
-- cheques validos y anulados y el total general los arma el generador de PDF en
-- C#, siguiendo el patron "el SP devuelve filas planas, el C# calcula totales"
-- que ya usan ReporteBm1Esp y ReporteControlPerceptivo.
--
-- DIVERGENCIAS DELIBERADAS respecto del .rdf, con su razon. Todas estan
-- explicadas en detalle en
-- Requerimientos/23 - RelaciondeChequesEmitidosPorPeriodosconMotivo/PLAN.md:
--
--   1. **La informacion de orden de pago se correlaciona por linea de
--      beneficiario, no por cheque.** La vista en linea AA del reporte legado
--      agrupa por CODIGO_CHEQUE sin DISTINCT ni GROUP BY, y se une por
--      A.CODIGO_CHEQUE = AA.CODIGO_CHEQUE(+) mientras el detalle ya venia unido
--      por D.CODIGO_CHEQUE = A.CODIGO_CHEQUE. Un cheque con N lineas de
--      beneficiario ligadas a ordenes de pago produce por tanto N x N filas, y
--      **los montos se cuentan N veces en los subtotales**. Aqui AA se
--      correlaciona por CODIGO_BENEFICIARIO_OP -que es la PK de
--      ADM_BENEFICIARIOS_OP-, asi que cada linea de detalle trae su propia orden
--      de pago y aparece exactamente una vez. Para un cheque de una sola linea
--      -el unico caso presente en el PDF de muestra- el resultado es identico.
--   2. **Sin SQL dinamico.** El lexico &P_WHERE_STATUS del reporte legado se
--      reemplaza por predicados (p_X IS NULL OR ...). Ademas de quitar la
--      concatenacion, corrige el comportamiento: AfterPForm usaba un ELSIF, de
--      modo que al informar estatus **y** proveedor solo se aplicaba el estatus y
--      el filtro de proveedor se ignoraba en silencio. Aqui los dos filtros son
--      independientes y se combinan con AND.
--   3. **La fecha de la orden de pago se formatea explicitamente.** El legado
--      concatenaba AOP.FECHA_ORDEN_PAGO sin TO_CHAR, dependiendo del
--      NLS_DATE_FORMAT de la sesion; desde .NET eso produce un formato distinto
--      al del PDF de muestra ('23/07/26'). Se fija con TO_CHAR(...,'DD/MM/RR').
--   4. **El rango de fechas se evalua como >= TRUNC(desde) y < TRUNC(hasta)+1**,
--      que incluye el ultimo dia aunque FECHA_CHEQUE lleve hora -el <= del legado
--      lo perderia- y permite usar el indice sobre la columna.
--   5. **BANCO_CUENTA se devuelve descompuesto** en NOMBRE_BANCO y
--      NUMERO_CUENTA en vez de como el literal concatenado
--      "<banco> CUENTA N <cuenta>". El texto de presentacion lo arma el
--      generador; asi el agrupamiento no depende de una cadena.
--
-- LIMITACION HEREDADA QUE NO SE TOCA: ADM_F_GET_PARTIDAS_CHEQUE declara su
-- acumulador como VARCHAR2(500) y captura WHEN OTHERS devolviendo NULL, asi que
-- un cheque con mas de una docena de partidas imputadas **pierde la lista
-- completa en silencio**. Es un objeto compartido del schema ADM y ampliarlo a
-- VARCHAR2(4000) afectaria a otros consumidores, asi que la decision queda para
-- el usuario; se llama tal cual, como el reporte legado.
-- =============================================================================
CREATE OR REPLACE PROCEDURE ADM.SP_REP_CHEQ_MOTIVO_GET (
    p_CodigoEmpresa    IN  NUMBER,
    p_FechaDesde       IN  DATE,
    p_FechaHasta       IN  DATE,
    p_NombreBanco      IN  VARCHAR2 DEFAULT NULL,
    p_NumeroCuenta     IN  VARCHAR2 DEFAULT NULL,
    p_Status           IN  VARCHAR2 DEFAULT NULL,
    p_CodigoProveedor  IN  NUMBER   DEFAULT NULL,
    p_ResultSet        OUT SYS_REFCURSOR,
    p_Message          OUT VARCHAR2,
    p_TotalRecords     OUT NUMBER
) AS
    v_desde DATE := TRUNC(p_FechaDesde);
    v_hasta DATE := TRUNC(p_FechaHasta) + 1;
BEGIN
    -- El conteo repite el FROM/WHERE pero NO la lista de columnas: llamar a
    -- ADM_F_GET_PARTIDAS_CHEQUE aqui recorreria PRE_V_SALDOS una vez por fila
    -- solo para contar.
    SELECT COUNT(*)
      INTO p_TotalRecords
      FROM ADM.ADM_CHEQUES A
      JOIN ADM.ADM_PROVEEDORES B
        ON B.CODIGO_PROVEEDOR = A.CODIGO_PROVEEDOR
      JOIN ADM.ADM_BENEFICIARIOS_CH D
        ON D.CODIGO_CHEQUE = A.CODIGO_CHEQUE
      JOIN SIS.SIS_CUENTAS_BANCOS E
        ON E.CODIGO_CUENTA_BANCO = A.CODIGO_CUENTA_BANCO
      JOIN SIS.SIS_BANCOS F
        ON F.CODIGO_BANCO = E.CODIGO_BANCO
     WHERE A.CODIGO_EMPRESA = p_CodigoEmpresa
       AND A.FECHA_CHEQUE >= v_desde
       AND A.FECHA_CHEQUE <  v_hasta
       AND (p_NombreBanco     IS NULL OR F.NOMBRE = p_NombreBanco)
       AND (p_NumeroCuenta    IS NULL OR E.NO_CUENTA = p_NumeroCuenta)
       AND (p_Status          IS NULL OR A.STATUS = p_Status)
       AND (p_CodigoProveedor IS NULL OR A.CODIGO_PROVEEDOR = p_CodigoProveedor);

    OPEN p_ResultSet FOR
        SELECT F.NOMBRE                                     NOMBRE_BANCO,
               E.NO_CUENTA                                  NUMERO_CUENTA,
               A.FECHA_CHEQUE                               FECHA_CHEQUE,
               -- Descriptivo del tipo de cheque + numero: 'PAEL 10025',
               -- 'NDOP 3962026'. ADM_F_DESCRIPTIVAS_ID('A.CODIGO', ...) es un
               -- EXECUTE IMMEDIATE por fila; se sustituye por el join a
               -- ADM_DESCRIPTIVAS, que es lo que la funcion hace por dentro.
               LTRIM(NVL(TCH.CODIGO, ' ') || ' ' || TO_CHAR(A.NUMERO_CHEQUE))
                                                            NUMERO_DOCUMENTO,
               NVL(A.STATUS, ' ')                           STATUS,
               DECODE(A.STATUS, 'AN', 'ANUL',
                                'AP', 'APRO',
                                NVL(A.STATUS, ' '))         ESTATUS_DESC,
               NVL(LTRIM(RTRIM(C.NOMBRE || ' ' || C.APELLIDO)),
                   B.NOMBRE_PROVEEDOR)                      BENEFICIARIO,
               -- Los anulados salen en negativo, como en el reporte legado y en
               -- el PDF de muestra. El generador los vuelve a positivo para la
               -- linea "CHEQUES ANULADOS", igual que hacia CF_1.
               DECODE(A.STATUS, 'AN', -1 * NVL(D.MONTO, 0),
                                      NVL(D.MONTO, 0))      MONTO,
               A.MOTIVO || ' ' || AA.INFO_OP || '    ' || CHR(13) ||
                   ADM.ADM_F_GET_PARTIDAS_CHEQUE(A.CODIGO_CHEQUE)
                                                            MOTIVO
          FROM ADM.ADM_CHEQUES A
          JOIN ADM.ADM_PROVEEDORES B
            ON B.CODIGO_PROVEEDOR = A.CODIGO_PROVEEDOR
          JOIN ADM.ADM_BENEFICIARIOS_CH D
            ON D.CODIGO_CHEQUE = A.CODIGO_CHEQUE
          JOIN SIS.SIS_CUENTAS_BANCOS E
            ON E.CODIGO_CUENTA_BANCO = A.CODIGO_CUENTA_BANCO
          JOIN SIS.SIS_BANCOS F
            ON F.CODIGO_BANCO = E.CODIGO_BANCO
          LEFT JOIN ADM.ADM_CONTACTO_PROVEEDOR C
            ON C.CODIGO_CONTACTO_PROVEEDOR = A.CODIGO_CONTACTO_PROVEEDOR
          LEFT JOIN ADM.ADM_DESCRIPTIVAS TCH
            ON TCH.DESCRIPCION_ID = A.TIPO_CHEQUE_ID
           AND TCH.CODIGO_EMPRESA = A.CODIGO_EMPRESA
          -- Una fila como maximo por linea de beneficiario: ver divergencia 1.
          LEFT JOIN (SELECT ABO.CODIGO_BENEFICIARIO_OP,
                            'N OP: ' || AOP.NUMERO_ORDEN_PAGO ||
                            ' FECHA OP: ' ||
                            TO_CHAR(AOP.FECHA_ORDEN_PAGO, 'DD/MM/RR') INFO_OP
                       FROM ADM.ADM_BENEFICIARIOS_OP ABO
                       JOIN ADM.ADM_ORDEN_PAGO AOP
                         ON AOP.CODIGO_ORDEN_PAGO = ABO.CODIGO_ORDEN_PAGO) AA
            ON AA.CODIGO_BENEFICIARIO_OP = D.CODIGO_BENEFICIARIO_OP
         WHERE A.CODIGO_EMPRESA = p_CodigoEmpresa
           AND A.FECHA_CHEQUE >= v_desde
           AND A.FECHA_CHEQUE <  v_hasta
           AND (p_NombreBanco     IS NULL OR F.NOMBRE = p_NombreBanco)
           AND (p_NumeroCuenta    IS NULL OR E.NO_CUENTA = p_NumeroCuenta)
           AND (p_Status          IS NULL OR A.STATUS = p_Status)
           AND (p_CodigoProveedor IS NULL OR A.CODIGO_PROVEEDOR = p_CodigoProveedor)
         -- El banco y la cuenta van primero: el quiebre de grupo se hace en C#
         -- sobre este orden, y sin ellos delante un mismo banco saldria partido
         -- en varios bloques. Dentro del grupo se conserva el orden del legado.
         ORDER BY F.NOMBRE, E.NO_CUENTA, A.FECHA_CHEQUE, A.NUMERO_CHEQUE, TCH.CODIGO;

    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SUBSTR(SQLERRM, 1, 4000);
        p_TotalRecords := 0;
        OPEN p_ResultSet FOR
            SELECT CAST(NULL AS VARCHAR2(200))  NOMBRE_BANCO,
                   CAST(NULL AS VARCHAR2(50))   NUMERO_CUENTA,
                   CAST(NULL AS DATE)           FECHA_CHEQUE,
                   CAST(NULL AS VARCHAR2(100))  NUMERO_DOCUMENTO,
                   CAST(NULL AS VARCHAR2(10))   STATUS,
                   CAST(NULL AS VARCHAR2(30))   ESTATUS_DESC,
                   CAST(NULL AS VARCHAR2(4000)) BENEFICIARIO,
                   CAST(NULL AS NUMBER)         MONTO,
                   CAST(NULL AS VARCHAR2(4000)) MOTIVO
              FROM DUAL
             WHERE 1 = 0;
END SP_REP_CHEQ_MOTIVO_GET;
/
