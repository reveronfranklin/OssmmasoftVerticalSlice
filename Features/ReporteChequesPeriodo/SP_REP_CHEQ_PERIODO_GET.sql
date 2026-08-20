-- =============================================================================
-- ADM - Relacion de Cheques Emitidos Por Periodos.
--
-- **Un solo procedimiento alimenta los DOS reportes de cheques:**
--
--   * Requerimiento 24 - "Relacion de Cheques Emitidos Por Periodos"
--     (ADM_PERIODOS_CHEQUES1.RDF). Listado simple. Se pide con
--     p_IncluirMotivo = 'N'.
--   * Requerimiento 23 - la misma relacion "con Motivo"
--     (ADM_PERIODOS_CHEQUES_MOTIVO1.rdf), que agrega por cada cheque el motivo,
--     la orden de pago que lo origina y las partidas presupuestarias imputadas.
--     Se pide con p_IncluirMotivo = 'S'.
--
-- Se comparte en vez de duplicar por la misma razon que BM.SP_REP_BM1_GET
-- alimenta el listado BM1 y el formulario BM-1 Especial: el query de negocio es
-- el mismo -mismas seis tablas, mismos joins, mismo rango de fechas, mismo
-- DECODE de anulados- y mantener dos copias garantiza que un dia se corrija una
-- sola. El .rdf del requerimiento 24 es literalmente un subconjunto del .rdf del
-- 23.
--
-- Devuelve una fila por linea de beneficiario de cheque (ADM_BENEFICIARIOS_CH).
-- El agrupamiento por banco/cuenta, los subtotales de cheques validos y anulados
-- y el total general los arma el generador de PDF en C#, siguiendo el patron "el
-- SP devuelve filas planas, el C# calcula totales" que ya usan ReporteBm1Esp y
-- ReporteControlPerceptivo.
--
-- DIVERGENCIAS DELIBERADAS respecto de los dos .rdf, con su razon. Estan
-- explicadas en detalle en los PLAN.md de los requerimientos 23 y 24:
--
--   1. **La informacion de orden de pago se correlaciona por linea de
--      beneficiario, no por cheque.** La vista en linea AA del requerimiento 23
--      agrupa por CODIGO_CHEQUE sin DISTINCT ni GROUP BY, y se une por
--      A.CODIGO_CHEQUE = AA.CODIGO_CHEQUE(+) mientras el detalle ya venia unido
--      por D.CODIGO_CHEQUE = A.CODIGO_CHEQUE. Un cheque con N lineas de
--      beneficiario ligadas a ordenes de pago produce por tanto N x N filas, y
--      **los montos se cuentan N veces en los subtotales**. Aqui AA se
--      correlaciona por CODIGO_BENEFICIARIO_OP -la PK de ADM_BENEFICIARIOS_OP-,
--      asi que cada linea de detalle trae su propia orden de pago y aparece
--      exactamente una vez.
--   2. **Sin SQL dinamico.** El lexico &P_WHERE_STATUS del requerimiento 23 se
--      reemplaza por predicados (p_X IS NULL OR ...). Ademas de quitar la
--      concatenacion, corrige el comportamiento: su AfterPForm usaba un ELSIF, de
--      modo que al informar estatus **y** proveedor solo se aplicaba el estatus y
--      el filtro de proveedor se ignoraba en silencio. Aqui los dos filtros son
--      independientes y se combinan con AND. El reporte del requerimiento 24 no
--      expone ninguno de los dos: simplemente los deja nulos.
--   3. **La fecha de la orden de pago se formatea explicitamente.** El legado
--      concatenaba AOP.FECHA_ORDEN_PAGO sin TO_CHAR, dependiendo del
--      NLS_DATE_FORMAT de la sesion; desde .NET eso produce un formato distinto
--      al del PDF de muestra ('23/07/26'). Se fija con TO_CHAR(...,'DD/MM/RR').
--   4. **El rango de fechas se evalua como >= TRUNC(desde) y < TRUNC(hasta)+1**,
--      que incluye el ultimo dia aunque FECHA_CHEQUE lleve hora -el <= del legado
--      lo perderia- y permite usar el indice sobre la columna.
--   5. **El banco y la cuenta se devuelven descompuestos** en NOMBRE_BANCO y
--      NUMERO_CUENTA, no como el literal concatenado "<banco> CUENTA N <cuenta>"
--      (BCO_CTA / BANCO_CUENTA en los .rdf). El texto de presentacion lo arma el
--      generador; asi el agrupamiento no depende de una cadena.
--   6. **SIS_RECONVERTIR_OLD no se llama.** El requerimiento 24 envolvia el monto
--      en SIS.SIS_RECONVERTIR_OLD('DUMMY', FECHA_CHEQUE, ...), cuya logica real
--      -multiplicar por 1000 los montos anteriores a la reconversion monetaria de
--      2008- **esta comentada en el cuerpo de la funcion**: hoy devuelve su
--      argumento sin tocarlo (verificado en
--      'Requerimientos/09 - Migrar BM/SIS.sql'). Llamarla sugeriria una
--      conversion que no ocurre.
--   7. **El orden es determinista.** El requerimiento 24 ordenaba por ORDER BY 1
--      -solo la fecha-, dejando el orden dentro de un mismo dia a merced del plan
--      de ejecucion; en su PDF de muestra los cheques del 08/07/2026 salen
--      desordenados (10015, 3712026, 3752026, 3742026, 3672026...). Aqui se
--      ordena por banco, cuenta, fecha, numero y tipo, que es lo que ya hacia el
--      requerimiento 23. Dos corridas del mismo periodo devuelven lo mismo.
--
-- El banco y la cuenta van primero en el ORDER BY porque el quiebre de grupo se
-- hace en C# sobre este orden: sin ellos delante, un mismo banco saldria partido
-- en varios bloques, cada uno con su subtotal incompleto.
--
-- LIMITACION HEREDADA QUE NO SE TOCA (solo afecta a p_IncluirMotivo = 'S'):
-- ADM_F_GET_PARTIDAS_CHEQUE declara su acumulador como VARCHAR2(500) y captura
-- WHEN OTHERS devolviendo NULL, asi que un cheque con mas de una docena de
-- partidas imputadas **pierde la lista completa en silencio**. Es un objeto
-- compartido del schema ADM y ampliarlo a VARCHAR2(4000) afectaria a otros
-- consumidores, asi que la decision queda para el usuario.
-- =============================================================================
CREATE OR REPLACE PROCEDURE ADM.SP_REP_CHEQ_PERIODO_GET (
    p_CodigoEmpresa    IN  NUMBER,
    p_FechaDesde       IN  DATE,
    p_FechaHasta       IN  DATE,
    p_NombreBanco      IN  VARCHAR2 DEFAULT NULL,
    p_NumeroCuenta     IN  VARCHAR2 DEFAULT NULL,
    p_Status           IN  VARCHAR2 DEFAULT NULL,
    p_CodigoProveedor  IN  NUMBER   DEFAULT NULL,
    p_ResultSet        OUT SYS_REFCURSOR,
    p_Message          OUT VARCHAR2,
    p_TotalRecords     OUT NUMBER,
    -- 'S' agrega el motivo, la orden de pago y las partidas (requerimiento 23).
    -- 'N' devuelve MOTIVO nulo y **no llama a ADM_F_GET_PARTIDAS_CHEQUE**, que es
    -- lo caro del reporte: recorre PRE_V_SALDOS una vez por cheque. Oracle
    -- cortocircuita el CASE, asi que con 'N' la funcion no se evalua.
    p_IncluirMotivo    IN  CHAR     DEFAULT 'S'
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
               -- Numero de cheque crudo: es lo que imprime el reporte del
               -- requerimiento 24 en su columna "Nro. CHEQUE".
               TO_CHAR(A.NUMERO_CHEQUE)                     NUMERO_CHEQUE,
               -- Descriptivo del tipo de cheque + numero ('PAEL 10025',
               -- 'NDOP 3962026'): es la columna "Nro. DOCUMENTO" del
               -- requerimiento 23. ADM_F_DESCRIPTIVAS_ID('A.CODIGO', ...) es un
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
               -- Los anulados salen en negativo, como en los dos reportes
               -- legados y en sus PDF de muestra. El generador los vuelve a
               -- positivo para la linea "CHEQUES ANULADOS", igual que hacia CF_1.
               DECODE(A.STATUS, 'AN', -1 * NVL(D.MONTO, 0),
                                      NVL(D.MONTO, 0))      MONTO,
               CASE WHEN p_IncluirMotivo = 'S'
                    THEN A.MOTIVO || ' ' || AA.INFO_OP || '    ' || CHR(13) ||
                         ADM.ADM_F_GET_PARTIDAS_CHEQUE(A.CODIGO_CHEQUE)
                    ELSE NULL
               END                                          MOTIVO
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
                   CAST(NULL AS VARCHAR2(100))  NUMERO_CHEQUE,
                   CAST(NULL AS VARCHAR2(100))  NUMERO_DOCUMENTO,
                   CAST(NULL AS VARCHAR2(10))   STATUS,
                   CAST(NULL AS VARCHAR2(30))   ESTATUS_DESC,
                   CAST(NULL AS VARCHAR2(4000)) BENEFICIARIO,
                   CAST(NULL AS NUMBER)         MONTO,
                   CAST(NULL AS VARCHAR2(4000)) MOTIVO
              FROM DUAL
             WHERE 1 = 0;
END SP_REP_CHEQ_PERIODO_GET;
/
