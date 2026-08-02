-- Requerimiento 20 - Migrar PrePresupuesto al VerticalSlice
-- Devuelve los financiados de TODOS los presupuestos en una sola consulta.
-- Reemplaza la llamada por fila a GetListFinanciadoPorPresupuesto del backend
-- legacy (Data/Repository/Presupuesto/PRE_V_SALDOSRepository.cs, linea 195).
--
-- El legacy agrupa PRE_V_SALDOS por (FINANCIADO_ID, DESCRIPTIVA_FINANCIADO)
-- para un presupuesto y ordena por FINANCIADO_ID. Aqui se hace lo mismo para
-- todos los presupuestos a la vez, agregando CODIGO_PRESUPUESTO a la clave para
-- que el handler pueda agrupar en memoria.
--
-- La columna origen se llama DESCRIPTIVA_FINANCIADO en PRE_V_SALDOS, pero el
-- contrato del frontend espera descripcionFinanciado. El alias hace la
-- traduccion aqui para que el handler no tenga que conocer las dos formas.
--
-- CODIGO_PRESUPUESTO y FINANCIADO_ID son nulables en PRE_V_SALDOS. Se descartan
-- las filas nulas: sin esas dos claves la fila no se puede asociar a ningun
-- presupuesto ni construir un PreFinanciadoDto valido.

CREATE OR REPLACE PROCEDURE PRE.SP_PRE_PRESUP_FIN_GET (
    p_ResultSet OUT SYS_REFCURSOR,
    p_Message   OUT VARCHAR2
) AS
BEGIN
    OPEN p_ResultSet FOR
        SELECT DISTINCT
               S.CODIGO_PRESUPUESTO,
               S.FINANCIADO_ID,
               S.DESCRIPTIVA_FINANCIADO DESCRIPCION_FINANCIADO
          FROM PRE.PRE_V_SALDOS S
         WHERE S.CODIGO_PRESUPUESTO IS NOT NULL
           AND S.FINANCIADO_ID IS NOT NULL
         ORDER BY S.CODIGO_PRESUPUESTO,
                  S.FINANCIADO_ID;

    p_Message := 'suscces';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SUBSTR(SQLERRM, 1, 4000);
        OPEN p_ResultSet FOR
            SELECT CAST(NULL AS NUMBER)         CODIGO_PRESUPUESTO,
                   CAST(NULL AS NUMBER)         FINANCIADO_ID,
                   CAST(NULL AS VARCHAR2(4000)) DESCRIPCION_FINANCIADO
              FROM DUAL
             WHERE 1 = 0;
END SP_PRE_PRESUP_FIN_GET;
/
