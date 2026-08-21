-- =============================================================================
-- PRE - Ejecucion Presupuestaria y Financiera del Presupuesto de Gastos.
--
-- Migracion del reporte Oracle Reports PRE_EJECUCION_POR_FECHA_FP.M4
-- (requerimiento 26). Devuelve el detalle por imputacion presupuestaria (ICP) y
-- por los cinco niveles del Plan Unico de Cuentas, con una columna NIVEL que dice
-- a que nivel corresponde cada fila. El agrupamiento por ICP, la indentacion, los
-- subtotales por grupo y el total general los arma el generador de PDF en C#.
--
-- ---------------------------------------------------------------------------
-- ESTRUCTURA: cinco ramas, un solo calculo de saldos
-- ---------------------------------------------------------------------------
-- El reporte legado es un UNION de cinco ramas -una por nivel del PUC: Partida,
-- Generica, Especifica, Subespecifica y Nivel5- y **cada rama repite tres
-- subconsultas identicas** (AA, BB, CC) sobre PRE_V_SALDOS y PRE_SALDOS_DIARIOS.
-- Son 15 subconsultas de las que 12 son copias.
--
-- Aqui las tres subconsultas se calculan **una vez** en la clausula WITH
-- (SALDO / MOV / ACUM) y las cinco ramas solo se diferencian en lo que
-- realmente las diferencia: a que nivel se une PRE_PLAN_UNICO_CUENTAS para
-- traer la denominacion, y hasta donde llega el GROUP BY.
--
-- **Esto no cambia ningun numero, y la razon es concreta:** en las tres
-- subconsultas del reporte legado CODIGO_SALDO forma parte del GROUP BY, y
-- CODIGO_SALDO es la granularidad mas fina de PRE_V_SALDOS. Es decir, las 15
-- subconsultas agregan al mismo nivel -por saldo y fuente de financiamiento- y
-- solo se distinguen por la denominacion del PUC que arrastran, que es
-- funcionalmente dependiente del saldo. Calcularlas una vez da exactamente las
-- mismas filas.
--
-- ---------------------------------------------------------------------------
-- LO QUE NO SE PUEDE PERDER DE VISTA: los subtotales no suman todas las filas
-- ---------------------------------------------------------------------------
-- Un mismo importe aparece impreso en la fila de Partida, en la de su Generica,
-- en la de su Especifica y asi hasta el Nivel5. Sumar todas las filas de un grupo
-- **multiplica el dinero por el numero de niveles**.
--
-- El reporte legado lo resuelve con las columnas de formula CF_*, que devuelven
-- el valor **solo cuando la fila es de nivel Partida** y 0 en cualquier otro
-- caso; son esas formulas -no las columnas crudas- las que alimentan los
-- subtotales CS_* y el total general CS_T_*.
--
-- Verificado contra el PDF de muestra, grupo 01-02-01-00-51: sus filas de nivel
-- Partida son 4.908,00 + 5.140.000,00 + 1.800.000,00 = 6.944.908,00, que es
-- exactamente el "TOTAL 01-02-01-00-51" impreso. Sumar todas las filas del grupo
-- daria 20.834.724,00. Por eso este SP devuelve la columna NIVEL: el generador
-- suma solo NIVEL = 1.
--
-- ---------------------------------------------------------------------------
-- DIVERGENCIAS DELIBERADAS respecto del binario, con su razon
-- ---------------------------------------------------------------------------
--   1. **El filtro de fuente de financiamiento se unifica en las cinco ramas.**
--      El legado usa dos formas distintas:
--        * ramas 1 a 4:  A.FINANCIADO_ID = NVL(:P_FINANCIADO_ID, A.FINANCIADO_ID)
--        * rama 5:       A.FINANCIADO_ID = DECODE(:P_FINANCIADO_ID, 719,
--                                                 :P_FINANCIADO_ID, A.FINANCIADO_ID)
--      No son equivalentes: con la segunda, **cualquier valor distinto de 719 no
--      filtra nada**. Pedir la fuente 100 daria los niveles 1 a 4 filtrados y el
--      nivel 5 con todas las fuentes: los cinco niveles de la misma jerarquia
--      dejarian de cuadrar entre si, con dinero de mas en las hojas.
--
--      Ademas las dos formas se contradicen con las propias sumas para el valor
--      92: las columnas hacen
--      `CASE WHEN :P_FINANCIADO_ID = 92 THEN DECODE(A.FINANCIADO_ID,719,0,...)`,
--      esto es "todas las fuentes menos la 719", lo que solo tiene sentido si el
--      WHERE **no** filtro por 92. La forma de la rama 5 es coherente con eso; la
--      de las ramas 1 a 4 no.
--
--      Aqui las cinco ramas usan el mismo predicado, el unico coherente con las
--      sumas: nulo = todas las fuentes, 92 = modo consolidado sin filtrar, y
--      cualquier otro valor = esa fuente. **Es la divergencia mas importante de
--      esta migracion y la primera que hay que confirmar contra datos reales**;
--      esta tambien en el PLAN.md del requerimiento 26.
--
--      Con p_FinanciadoId nulo -el caso del PDF de muestra- las tres formas se
--      comportan igual, asi que el unico caso verificable no cambia.
--
--   2. **Las expresiones de suma se reproducen literalmente**, incluida su propia
--      incoherencia: el MODIFICADO de SALDO excluye la fuente 719 solo cuando se
--      pide 92, mientras que los de MOV y ACUM lo excluyen tambien cuando no se
--      pide ninguna fuente. No se toca: son las expresiones que producen los
--      numeros del PDF de muestra.
--
--   3. **El predicado de exclusion de filas se simplifica.** El legado escribe
--      `( AA.ASIGNACION >= 0 OR ( AA.ASIGNACION = 0 AND BB.MODIFICADO <> 0 )
--         OR ( AA.ASIGNACION > 0 AND BB.MODIFICADO <> 0 ) )`.
--      Las dos ultimas alternativas estan contenidas en la primera -si ASIGNACION
--      es 0 o mayor que 0, ya cumple >= 0-, asi que todo el parentesis equivale a
--      `ASIGNACION >= 0`. Se escribe asi.
--
--   4. **Se filtra por CODIGO_EMPRESA.** El query legado solo filtraba por
--      CODIGO_PRESUPUESTO.
--
--   5. **El rango de fechas se compara con TRUNC.** El legado usa
--      `C.FECHA_SALDO BETWEEN :P_FECHA_DESDE AND :P_FECHA_HASTA`, que pierde el
--      ultimo dia si FECHA_SALDO lleva hora.
--
-- LO QUE NO SE MIGRA, y es deliberado:
--
--   * **El lexico &P_WHERE** (rango de imputacion + PUC, parametros
--     P_ICP_PUC_DESDE / P_ICP_PUC_HASTA). El PDF de muestra no lo ejercita y su
--     logica en AfterPForm tiene un bloque alternativo comentado, asi que no hay
--     forma de saber cual es el vigente. Queda fuera del contrato inicial, como
--     propuso el analisis. Agregarlo despues son dos parametros y un predicado
--     sobre CODIGO_ICP_CONCAT / CODIGO_PUC_CONCAT.
--   * **P_MES_ANO, P_VIGENTE y CODIGO_USUARIO**: declarados en el binario, sin
--     ninguna referencia en el query ni en las formulas.
--   * **El ajuste de P_FECHA_HASTA al 31/12** que hacia AfterPForm cuando la
--     fecha hasta caia en un ano posterior al de la fecha desde. Se hace en el
--     handler, que es donde se puede avisar al usuario.
-- =============================================================================
CREATE OR REPLACE PROCEDURE PRE.SP_REP_EJEC_PRESUP_GET (
    p_CodigoEmpresa     IN  NUMBER,
    p_CodigoPresupuesto IN  NUMBER,
    p_FechaDesde        IN  DATE,
    p_FechaHasta        IN  DATE,
    p_FinanciadoId      IN  NUMBER DEFAULT NULL,
    p_ResultSet         OUT SYS_REFCURSOR,
    p_Message           OUT VARCHAR2,
    p_TotalRecords      OUT NUMBER
) AS
    v_desde DATE := TRUNC(p_FechaDesde);
    v_hasta DATE := TRUNC(p_FechaHasta);
BEGIN
    OPEN p_ResultSet FOR
        WITH SALDO AS (
            -- AA del reporte legado: la foto configurada del saldo.
            SELECT S.CODIGO_SALDO,
                   S.FINANCIADO_ID,
                   S.CODIGO_SECTOR,
                   S.CODIGO_PROGRAMA,
                   S.CODIGO_SUBPROGRAMA,
                   S.CODIGO_PROYECTO,
                   S.CODIGO_ACTIVIDAD,
                   S.DENOMINACION_ICP,
                   S.CODIGO_GRUPO,
                   S.CODIGO_PARTIDA,
                   S.CODIGO_GENERICA,
                   S.CODIGO_ESPECIFICA,
                   S.CODIGO_SUBESPECIFICA,
                   S.CODIGO_NIVEL5,
                   SUM(CASE WHEN S.ASIGNACION >= 0 THEN S.PRESUPUESTADO ELSE 0 END) PRESUPUESTADO,
                   SUM(S.ASIGNACION)   ASIGNACION,
                   SUM(NVL(CASE WHEN p_FinanciadoId = 92
                                THEN DECODE(S.FINANCIADO_ID, 719, 0, S.MODIFICADO)
                                ELSE S.MODIFICADO END, 0)) MODIFICADO,
                   SUM(S.COMPROMETIDO) COMPROMETIDO,
                   SUM(S.BLOQUEADO)    BLOQUEADO
              FROM PRE.PRE_V_SALDOS S
             WHERE S.CODIGO_PRESUPUESTO = p_CodigoPresupuesto
               AND S.CODIGO_EMPRESA = p_CodigoEmpresa
               AND (p_FinanciadoId IS NULL
                    OR p_FinanciadoId = 92
                    OR S.FINANCIADO_ID = p_FinanciadoId)
             GROUP BY S.CODIGO_SALDO, S.FINANCIADO_ID,
                      S.CODIGO_SECTOR, S.CODIGO_PROGRAMA, S.CODIGO_SUBPROGRAMA,
                      S.CODIGO_PROYECTO, S.CODIGO_ACTIVIDAD, S.DENOMINACION_ICP,
                      S.CODIGO_GRUPO, S.CODIGO_PARTIDA, S.CODIGO_GENERICA,
                      S.CODIGO_ESPECIFICA, S.CODIGO_SUBESPECIFICA, S.CODIGO_NIVEL5
        ),
        MOV AS (
            -- BB del reporte legado: los movimientos del periodo.
            SELECT S.CODIGO_SALDO,
                   S.FINANCIADO_ID,
                   SUM(NVL(CASE WHEN (p_FinanciadoId = 92 OR NVL(p_FinanciadoId, 0) = 0)
                                THEN DECODE(S.FINANCIADO_ID, 719, 0, D.MODIFICADO)
                                ELSE (CASE WHEN TRUNC(D.FECHA_SALDO) <= v_hasta
                                           THEN D.MODIFICADO ELSE 0 END)
                           END, 0)) MODIFICADO,
                   SUM(NVL(CASE WHEN (p_FinanciadoId = 92 OR NVL(p_FinanciadoId, 0) = 0)
                                THEN DECODE(S.FINANCIADO_ID, 719, 0, D.COMPROMETIDO)
                                ELSE D.COMPROMETIDO END, 0)) COMPROMETIDO,
                   SUM(NVL(CASE WHEN (p_FinanciadoId = 92 OR NVL(p_FinanciadoId, 0) = 0)
                                THEN DECODE(S.FINANCIADO_ID, 719, 0, D.CAUSADO)
                                ELSE D.CAUSADO END, 0)) CAUSADO,
                   SUM(NVL(CASE WHEN (p_FinanciadoId = 92 OR NVL(p_FinanciadoId, 0) = 0)
                                THEN DECODE(S.FINANCIADO_ID, 719, 0, D.PAGADO)
                                ELSE D.PAGADO END, 0)) PAGADO
              FROM PRE.PRE_V_SALDOS S
              JOIN PRE.PRE_SALDOS_DIARIOS D
                ON D.CODIGO_SALDO = S.CODIGO_SALDO
             WHERE S.CODIGO_PRESUPUESTO = p_CodigoPresupuesto
               AND S.CODIGO_EMPRESA = p_CodigoEmpresa
               AND (p_FinanciadoId IS NULL
                    OR p_FinanciadoId = 92
                    OR S.FINANCIADO_ID = p_FinanciadoId)
               AND TRUNC(D.FECHA_SALDO) BETWEEN v_desde AND v_hasta
             GROUP BY S.CODIGO_SALDO, S.FINANCIADO_ID
        ),
        ACUM AS (
            -- CC del reporte legado: el acumulado hasta la fecha hasta.
            SELECT S.CODIGO_SALDO,
                   S.FINANCIADO_ID,
                   SUM(DECODE(D.EXTRA2, '1', D.ASIGNACION, 0)) ASIGNACION,
                   SUM(NVL(CASE WHEN (p_FinanciadoId = 92 OR NVL(p_FinanciadoId, 0) = 0)
                                THEN DECODE(S.FINANCIADO_ID, 719, 0, D.MODIFICADO)
                                ELSE D.MODIFICADO END, 0)) MODIFICADO,
                   SUM(NVL(CASE WHEN (p_FinanciadoId = 92 OR NVL(p_FinanciadoId, 0) = 0)
                                THEN DECODE(S.FINANCIADO_ID, 719, 0, D.COMPROMETIDO)
                                ELSE D.COMPROMETIDO END, 0)) COMPROMETIDO,
                   SUM(NVL(CASE WHEN (p_FinanciadoId = 92 OR NVL(p_FinanciadoId, 0) = 0)
                                THEN DECODE(S.FINANCIADO_ID, 719, 0, D.CAUSADO)
                                ELSE D.CAUSADO END, 0)) CAUSADO,
                   SUM(NVL(CASE WHEN (p_FinanciadoId = 92 OR NVL(p_FinanciadoId, 0) = 0)
                                THEN DECODE(S.FINANCIADO_ID, 719, 0, D.PAGADO)
                                ELSE D.PAGADO END, 0)) PAGADO
              FROM PRE.PRE_V_SALDOS S
              JOIN PRE.PRE_SALDOS_DIARIOS D
                ON D.CODIGO_SALDO = S.CODIGO_SALDO
             WHERE S.CODIGO_PRESUPUESTO = p_CodigoPresupuesto
               AND S.CODIGO_EMPRESA = p_CodigoEmpresa
               AND (p_FinanciadoId IS NULL
                    OR p_FinanciadoId = 92
                    OR S.FINANCIADO_ID = p_FinanciadoId)
               AND TRUNC(D.FECHA_SALDO) <= v_hasta
             GROUP BY S.CODIGO_SALDO, S.FINANCIADO_ID
        ),
        -- Las cinco ramas. Cada una une el PUC a su nivel y agrupa hasta ahi;
        -- NIVEL dice cual es, y es lo que el generador usa para indentar y para
        -- saber que filas suman en los subtotales (solo NIVEL = 1).
        DETALLE AS (
            SELECT 1 NIVEL,
                   S.CODIGO_SECTOR, S.CODIGO_PROGRAMA, S.CODIGO_SUBPROGRAMA,
                   S.CODIGO_PROYECTO, S.CODIGO_ACTIVIDAD, S.DENOMINACION_ICP,
                   S.CODIGO_GRUPO || '.' || S.CODIGO_PARTIDA CODIGO_PARTIDA,
                   '00' CODIGO_GENERICA, '00' CODIGO_ESPECIFICA,
                   '00' CODIGO_SUBESPECIFICA, '00' CODIGO_NIVEL5,
                   B.DENOMINACION DENOMINACION_PUC,
                   SUM(NVL(S.PRESUPUESTADO, 0)) PRESUPUESTADO,
                   SUM(NVL(C.MODIFICADO, 0))    MODIFICADO,
                   SUM(NVL(M.COMPROMETIDO, 0))  COMPROMETIDO,
                   SUM(NVL(S.BLOQUEADO, 0))     BLOQUEADO,
                   SUM(NVL(M.CAUSADO, 0))       CAUSADO,
                   SUM(NVL(M.PAGADO, 0))        PAGADO,
                   SUM(NVL(M.COMPROMETIDO - M.PAGADO, 0)) DEUDA,
                   SUM(NVL(S.PRESUPUESTADO, 0) + NVL(C.MODIFICADO, 0))
                       - SUM(NVL(C.COMPROMETIDO, 0)) DISPONIBILIDAD,
                   SUM(NVL(S.ASIGNACION, 0))    ASIGNACION,
                   SUM(CASE WHEN NVL(S.ASIGNACION - NVL(M.COMPROMETIDO, 0)
                                     + NVL(C.MODIFICADO, 0), 0) > 0
                            THEN NVL(S.ASIGNACION - NVL(M.COMPROMETIDO, 0)
                                     + NVL(C.MODIFICADO, 0), 0)
                            ELSE 0 END) DISPONIBILIDAD_FINAN
              FROM SALDO S
              JOIN PRE.PRE_PLAN_UNICO_CUENTAS B
                ON B.CODIGO_GRUPO  = S.CODIGO_GRUPO
               AND B.CODIGO_NIVEL1 = S.CODIGO_PARTIDA
               AND B.CODIGO_NIVEL2 = '00'
               AND B.CODIGO_NIVEL3 = '00'
               AND B.CODIGO_NIVEL4 = '00'
               AND B.CODIGO_NIVEL5 = '00'
               AND B.CODIGO_PRESUPUESTO = p_CodigoPresupuesto
              LEFT JOIN MOV M
                ON M.CODIGO_SALDO = S.CODIGO_SALDO
               AND M.FINANCIADO_ID = S.FINANCIADO_ID
              LEFT JOIN ACUM C
                ON C.CODIGO_SALDO = S.CODIGO_SALDO
               AND C.FINANCIADO_ID = S.FINANCIADO_ID
             WHERE S.ASIGNACION >= 0
             GROUP BY S.CODIGO_SECTOR, S.CODIGO_PROGRAMA, S.CODIGO_SUBPROGRAMA,
                      S.CODIGO_PROYECTO, S.CODIGO_ACTIVIDAD, S.DENOMINACION_ICP,
                      S.CODIGO_GRUPO || '.' || S.CODIGO_PARTIDA, B.DENOMINACION
            UNION
            SELECT 2 NIVEL,
                   S.CODIGO_SECTOR, S.CODIGO_PROGRAMA, S.CODIGO_SUBPROGRAMA,
                   S.CODIGO_PROYECTO, S.CODIGO_ACTIVIDAD, S.DENOMINACION_ICP,
                   S.CODIGO_GRUPO || '.' || S.CODIGO_PARTIDA,
                   S.CODIGO_GENERICA, '00', '00', '00',
                   B.DENOMINACION,
                   SUM(NVL(S.PRESUPUESTADO, 0)), SUM(NVL(C.MODIFICADO, 0)),
                   SUM(NVL(M.COMPROMETIDO, 0)),  SUM(NVL(S.BLOQUEADO, 0)),
                   SUM(NVL(M.CAUSADO, 0)),       SUM(NVL(M.PAGADO, 0)),
                   SUM(NVL(M.COMPROMETIDO - M.PAGADO, 0)),
                   SUM(NVL(S.PRESUPUESTADO, 0) + NVL(C.MODIFICADO, 0))
                       - SUM(NVL(C.COMPROMETIDO, 0)),
                   SUM(NVL(S.ASIGNACION, 0)),
                   SUM(CASE WHEN NVL(S.ASIGNACION - NVL(M.COMPROMETIDO, 0)
                                     + NVL(C.MODIFICADO, 0), 0) > 0
                            THEN NVL(S.ASIGNACION - NVL(M.COMPROMETIDO, 0)
                                     + NVL(C.MODIFICADO, 0), 0)
                            ELSE 0 END)
              FROM SALDO S
              JOIN PRE.PRE_PLAN_UNICO_CUENTAS B
                ON B.CODIGO_GRUPO  = S.CODIGO_GRUPO
               AND B.CODIGO_NIVEL1 = S.CODIGO_PARTIDA
               AND B.CODIGO_NIVEL2 = S.CODIGO_GENERICA
               AND B.CODIGO_NIVEL3 = '00'
               AND B.CODIGO_NIVEL4 = '00'
               AND B.CODIGO_NIVEL5 = '00'
               AND B.CODIGO_PRESUPUESTO = p_CodigoPresupuesto
              LEFT JOIN MOV M
                ON M.CODIGO_SALDO = S.CODIGO_SALDO
               AND M.FINANCIADO_ID = S.FINANCIADO_ID
              LEFT JOIN ACUM C
                ON C.CODIGO_SALDO = S.CODIGO_SALDO
               AND C.FINANCIADO_ID = S.FINANCIADO_ID
             WHERE S.ASIGNACION >= 0
             GROUP BY S.CODIGO_SECTOR, S.CODIGO_PROGRAMA, S.CODIGO_SUBPROGRAMA,
                      S.CODIGO_PROYECTO, S.CODIGO_ACTIVIDAD, S.DENOMINACION_ICP,
                      S.CODIGO_GRUPO || '.' || S.CODIGO_PARTIDA,
                      S.CODIGO_GENERICA, B.DENOMINACION
            UNION
            SELECT 3 NIVEL,
                   S.CODIGO_SECTOR, S.CODIGO_PROGRAMA, S.CODIGO_SUBPROGRAMA,
                   S.CODIGO_PROYECTO, S.CODIGO_ACTIVIDAD, S.DENOMINACION_ICP,
                   S.CODIGO_GRUPO || '.' || S.CODIGO_PARTIDA,
                   S.CODIGO_GENERICA, S.CODIGO_ESPECIFICA, '00', '00',
                   B.DENOMINACION,
                   SUM(NVL(S.PRESUPUESTADO, 0)), SUM(NVL(C.MODIFICADO, 0)),
                   SUM(NVL(M.COMPROMETIDO, 0)),  SUM(NVL(S.BLOQUEADO, 0)),
                   SUM(NVL(M.CAUSADO, 0)),       SUM(NVL(M.PAGADO, 0)),
                   SUM(NVL(M.COMPROMETIDO - M.PAGADO, 0)),
                   SUM(NVL(S.PRESUPUESTADO, 0) + NVL(C.MODIFICADO, 0))
                       - SUM(NVL(C.COMPROMETIDO, 0)),
                   SUM(NVL(S.ASIGNACION, 0)),
                   SUM(CASE WHEN NVL(S.ASIGNACION - NVL(M.COMPROMETIDO, 0)
                                     + NVL(C.MODIFICADO, 0), 0) > 0
                            THEN NVL(S.ASIGNACION - NVL(M.COMPROMETIDO, 0)
                                     + NVL(C.MODIFICADO, 0), 0)
                            ELSE 0 END)
              FROM SALDO S
              JOIN PRE.PRE_PLAN_UNICO_CUENTAS B
                ON B.CODIGO_GRUPO  = S.CODIGO_GRUPO
               AND B.CODIGO_NIVEL1 = S.CODIGO_PARTIDA
               AND B.CODIGO_NIVEL2 = S.CODIGO_GENERICA
               AND B.CODIGO_NIVEL3 = S.CODIGO_ESPECIFICA
               AND B.CODIGO_NIVEL4 = '00'
               AND B.CODIGO_NIVEL5 = '00'
               AND B.CODIGO_PRESUPUESTO = p_CodigoPresupuesto
              LEFT JOIN MOV M
                ON M.CODIGO_SALDO = S.CODIGO_SALDO
               AND M.FINANCIADO_ID = S.FINANCIADO_ID
              LEFT JOIN ACUM C
                ON C.CODIGO_SALDO = S.CODIGO_SALDO
               AND C.FINANCIADO_ID = S.FINANCIADO_ID
             WHERE S.ASIGNACION >= 0
             GROUP BY S.CODIGO_SECTOR, S.CODIGO_PROGRAMA, S.CODIGO_SUBPROGRAMA,
                      S.CODIGO_PROYECTO, S.CODIGO_ACTIVIDAD, S.DENOMINACION_ICP,
                      S.CODIGO_GRUPO || '.' || S.CODIGO_PARTIDA,
                      S.CODIGO_GENERICA, S.CODIGO_ESPECIFICA, B.DENOMINACION
            UNION
            SELECT 4 NIVEL,
                   S.CODIGO_SECTOR, S.CODIGO_PROGRAMA, S.CODIGO_SUBPROGRAMA,
                   S.CODIGO_PROYECTO, S.CODIGO_ACTIVIDAD, S.DENOMINACION_ICP,
                   S.CODIGO_GRUPO || '.' || S.CODIGO_PARTIDA,
                   S.CODIGO_GENERICA, S.CODIGO_ESPECIFICA,
                   S.CODIGO_SUBESPECIFICA, '00',
                   B.DENOMINACION,
                   SUM(NVL(S.PRESUPUESTADO, 0)), SUM(NVL(C.MODIFICADO, 0)),
                   SUM(NVL(M.COMPROMETIDO, 0)),  SUM(NVL(S.BLOQUEADO, 0)),
                   SUM(NVL(M.CAUSADO, 0)),       SUM(NVL(M.PAGADO, 0)),
                   SUM(NVL(M.COMPROMETIDO - M.PAGADO, 0)),
                   SUM(NVL(S.PRESUPUESTADO, 0) + NVL(C.MODIFICADO, 0))
                       - SUM(NVL(C.COMPROMETIDO, 0)),
                   SUM(NVL(S.ASIGNACION, 0)),
                   SUM(CASE WHEN NVL(S.ASIGNACION - NVL(M.COMPROMETIDO, 0)
                                     + NVL(C.MODIFICADO, 0), 0) > 0
                            THEN NVL(S.ASIGNACION - NVL(M.COMPROMETIDO, 0)
                                     + NVL(C.MODIFICADO, 0), 0)
                            ELSE 0 END)
              FROM SALDO S
              JOIN PRE.PRE_PLAN_UNICO_CUENTAS B
                ON B.CODIGO_GRUPO  = S.CODIGO_GRUPO
               AND B.CODIGO_NIVEL1 = S.CODIGO_PARTIDA
               AND B.CODIGO_NIVEL2 = S.CODIGO_GENERICA
               AND B.CODIGO_NIVEL3 = S.CODIGO_ESPECIFICA
               AND B.CODIGO_NIVEL4 = S.CODIGO_SUBESPECIFICA
               AND B.CODIGO_NIVEL5 = '00'
               AND B.CODIGO_PRESUPUESTO = p_CodigoPresupuesto
              LEFT JOIN MOV M
                ON M.CODIGO_SALDO = S.CODIGO_SALDO
               AND M.FINANCIADO_ID = S.FINANCIADO_ID
              LEFT JOIN ACUM C
                ON C.CODIGO_SALDO = S.CODIGO_SALDO
               AND C.FINANCIADO_ID = S.FINANCIADO_ID
             WHERE S.ASIGNACION >= 0
             GROUP BY S.CODIGO_SECTOR, S.CODIGO_PROGRAMA, S.CODIGO_SUBPROGRAMA,
                      S.CODIGO_PROYECTO, S.CODIGO_ACTIVIDAD, S.DENOMINACION_ICP,
                      S.CODIGO_GRUPO || '.' || S.CODIGO_PARTIDA,
                      S.CODIGO_GENERICA, S.CODIGO_ESPECIFICA,
                      S.CODIGO_SUBESPECIFICA, B.DENOMINACION
            UNION
            SELECT 5 NIVEL,
                   S.CODIGO_SECTOR, S.CODIGO_PROGRAMA, S.CODIGO_SUBPROGRAMA,
                   S.CODIGO_PROYECTO, S.CODIGO_ACTIVIDAD, S.DENOMINACION_ICP,
                   S.CODIGO_GRUPO || '.' || S.CODIGO_PARTIDA,
                   S.CODIGO_GENERICA, S.CODIGO_ESPECIFICA,
                   S.CODIGO_SUBESPECIFICA, S.CODIGO_NIVEL5,
                   B.DENOMINACION,
                   SUM(NVL(S.PRESUPUESTADO, 0)), SUM(NVL(C.MODIFICADO, 0)),
                   SUM(NVL(M.COMPROMETIDO, 0)),  SUM(NVL(S.BLOQUEADO, 0)),
                   SUM(NVL(M.CAUSADO, 0)),       SUM(NVL(M.PAGADO, 0)),
                   SUM(NVL(M.COMPROMETIDO - M.PAGADO, 0)),
                   SUM(NVL(S.PRESUPUESTADO, 0) + NVL(C.MODIFICADO, 0))
                       - SUM(NVL(C.COMPROMETIDO, 0)),
                   SUM(NVL(S.ASIGNACION, 0)),
                   SUM(CASE WHEN NVL(S.ASIGNACION - NVL(M.COMPROMETIDO, 0)
                                     + NVL(C.MODIFICADO, 0), 0) > 0
                            THEN NVL(S.ASIGNACION - NVL(M.COMPROMETIDO, 0)
                                     + NVL(C.MODIFICADO, 0), 0)
                            ELSE 0 END)
              FROM SALDO S
              JOIN PRE.PRE_PLAN_UNICO_CUENTAS B
                ON B.CODIGO_GRUPO  = S.CODIGO_GRUPO
               AND B.CODIGO_NIVEL1 = S.CODIGO_PARTIDA
               AND B.CODIGO_NIVEL2 = S.CODIGO_GENERICA
               AND B.CODIGO_NIVEL3 = S.CODIGO_ESPECIFICA
               AND B.CODIGO_NIVEL4 = S.CODIGO_SUBESPECIFICA
               AND B.CODIGO_NIVEL5 = S.CODIGO_NIVEL5
               AND B.CODIGO_PRESUPUESTO = p_CodigoPresupuesto
              LEFT JOIN MOV M
                ON M.CODIGO_SALDO = S.CODIGO_SALDO
               AND M.FINANCIADO_ID = S.FINANCIADO_ID
              LEFT JOIN ACUM C
                ON C.CODIGO_SALDO = S.CODIGO_SALDO
               AND C.FINANCIADO_ID = S.FINANCIADO_ID
             WHERE S.ASIGNACION >= 0
             GROUP BY S.CODIGO_SECTOR, S.CODIGO_PROGRAMA, S.CODIGO_SUBPROGRAMA,
                      S.CODIGO_PROYECTO, S.CODIGO_ACTIVIDAD, S.DENOMINACION_ICP,
                      S.CODIGO_GRUPO || '.' || S.CODIGO_PARTIDA,
                      S.CODIGO_GENERICA, S.CODIGO_ESPECIFICA,
                      S.CODIGO_SUBESPECIFICA, S.CODIGO_NIVEL5, B.DENOMINACION
        )
        SELECT Q.NIVEL,
               Q.CODIGO_SECTOR, Q.CODIGO_PROGRAMA, Q.CODIGO_SUBPROGRAMA,
               Q.CODIGO_PROYECTO, Q.CODIGO_ACTIVIDAD, Q.DENOMINACION_ICP,
               Q.CODIGO_PARTIDA, Q.CODIGO_GENERICA, Q.CODIGO_ESPECIFICA,
               Q.CODIGO_SUBESPECIFICA, Q.CODIGO_NIVEL5, Q.DENOMINACION_PUC,
               Q.PRESUPUESTADO,
               Q.MODIFICADO,
               -- CF_VIGENTE del legado: la columna "Presupuesto Real Modificado".
               Q.PRESUPUESTADO + Q.MODIFICADO VIGENTE,
               Q.COMPROMETIDO, Q.BLOQUEADO, Q.CAUSADO, Q.PAGADO, Q.DEUDA,
               Q.DISPONIBILIDAD, Q.ASIGNACION, Q.DISPONIBILIDAD_FINAN
          FROM DETALLE Q
         -- El orden es el que necesita la jerarquia impresa: el grupo ICP
         -- primero y dentro de el los niveles del PUC de arriba hacia abajo.
         ORDER BY Q.CODIGO_SECTOR, Q.CODIGO_PROGRAMA, Q.CODIGO_SUBPROGRAMA,
                  Q.CODIGO_PROYECTO, Q.CODIGO_ACTIVIDAD,
                  Q.CODIGO_PARTIDA, Q.CODIGO_GENERICA, Q.CODIGO_ESPECIFICA,
                  Q.CODIGO_SUBESPECIFICA, Q.CODIGO_NIVEL5, Q.NIVEL;

    -- El conteo obligaria a repetir las cinco ramas: lo informa el handler, que
    -- ya recorre todas las filas.
    p_TotalRecords := 0;
    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SUBSTR(SQLERRM, 1, 4000);
        p_TotalRecords := 0;
        OPEN p_ResultSet FOR
            SELECT CAST(NULL AS NUMBER)         NIVEL,
                   CAST(NULL AS VARCHAR2(10))   CODIGO_SECTOR,
                   CAST(NULL AS VARCHAR2(10))   CODIGO_PROGRAMA,
                   CAST(NULL AS VARCHAR2(10))   CODIGO_SUBPROGRAMA,
                   CAST(NULL AS VARCHAR2(10))   CODIGO_PROYECTO,
                   CAST(NULL AS VARCHAR2(10))   CODIGO_ACTIVIDAD,
                   CAST(NULL AS VARCHAR2(4000)) DENOMINACION_ICP,
                   CAST(NULL AS VARCHAR2(20))   CODIGO_PARTIDA,
                   CAST(NULL AS VARCHAR2(10))   CODIGO_GENERICA,
                   CAST(NULL AS VARCHAR2(10))   CODIGO_ESPECIFICA,
                   CAST(NULL AS VARCHAR2(10))   CODIGO_SUBESPECIFICA,
                   CAST(NULL AS VARCHAR2(10))   CODIGO_NIVEL5,
                   CAST(NULL AS VARCHAR2(4000)) DENOMINACION_PUC,
                   CAST(NULL AS NUMBER)         PRESUPUESTADO,
                   CAST(NULL AS NUMBER)         MODIFICADO,
                   CAST(NULL AS NUMBER)         VIGENTE,
                   CAST(NULL AS NUMBER)         COMPROMETIDO,
                   CAST(NULL AS NUMBER)         BLOQUEADO,
                   CAST(NULL AS NUMBER)         CAUSADO,
                   CAST(NULL AS NUMBER)         PAGADO,
                   CAST(NULL AS NUMBER)         DEUDA,
                   CAST(NULL AS NUMBER)         DISPONIBILIDAD,
                   CAST(NULL AS NUMBER)         ASIGNACION,
                   CAST(NULL AS NUMBER)         DISPONIBILIDAD_FINAN
              FROM DUAL
             WHERE 1 = 0;
END SP_REP_EJEC_PRESUP_GET;
/
