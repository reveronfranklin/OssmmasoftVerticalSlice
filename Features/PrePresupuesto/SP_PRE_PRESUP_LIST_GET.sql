-- Requerimiento 20 - Migrar PrePresupuesto al VerticalSlice
-- Lista todos los presupuestos con el indicador de ejecucion resuelto en la
-- base de datos. Reemplaza la llamada por fila a PresupuestoExiste del backend
-- legacy (Services/Presupuesto/PRE_PRESUPUESTOSService.cs, MapPresupuesto).
--
-- Sin parametros de entrada: el endpoint devuelve todos los presupuestos.
-- Orden: FECHA_HASTA descendente, igual que MapListPresupuesto.
--
-- PRESUPUESTO_EN_EJECUCION se resuelve con una subconsulta escalar y ROWNUM = 1
-- en lugar de CASE WHEN EXISTS, porque Oracle no admite EXISTS en la lista de
-- seleccion. ROWNUM = 1 corta en la primera coincidencia.

CREATE OR REPLACE PROCEDURE PRE.SP_PRE_PRESUP_LIST_GET (
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message   OUT VARCHAR2
) AS
BEGIN
    OPEN p_ResultSet FOR
        SELECT P.CODIGO_PRESUPUESTO,
               P.DENOMINACION,
               P.ANO,
               NVL((SELECT 1
                      FROM PRE.PRE_V_SALDOS S
                     WHERE S.CODIGO_PRESUPUESTO = P.CODIGO_PRESUPUESTO
                       AND ROWNUM = 1), 0) PRESUPUESTO_EN_EJECUCION
          FROM PRE.PRE_PRESUPUESTOS P
         ORDER BY P.FECHA_HASTA DESC;

    p_Message := 'suscces';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SUBSTR(SQLERRM, 1, 4000);
        OPEN p_ResultSet FOR
            SELECT CAST(NULL AS NUMBER)         CODIGO_PRESUPUESTO,
                   CAST(NULL AS VARCHAR2(4000)) DENOMINACION,
                   CAST(NULL AS NUMBER)         ANO,
                   CAST(NULL AS NUMBER)         PRESUPUESTO_EN_EJECUCION
              FROM DUAL
             WHERE 1 = 0;
END SP_PRE_PRESUP_LIST_GET;
/
