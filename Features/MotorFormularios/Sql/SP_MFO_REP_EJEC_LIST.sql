-- =============================================================================
-- MFO - Bitacora de ejecuciones, paginada.
--
-- No devuelve PARAMS_CLB. Una bitacora se lee de a paginas de 50 y arrastrar un
-- CLOB por fila para mostrar una tabla seria pagar el costo en cada consulta
-- para un dato que casi nunca se mira. Se expone TIENE_PARAMS para que la UI
-- sepa cuando ofrecer el detalle, y SP_MFO_REP_ULTIMOS lo trae cuando de verdad
-- hace falta (recargar parametros).
-- =============================================================================
CREATE OR REPLACE PROCEDURE MFO.SP_MFO_REP_EJEC_LIST (
    p_CodigoEmpresa IN  NUMBER,
    p_FormularioId  IN  NUMBER,
    p_ReporteId     IN  NUMBER,
    p_Usuario       IN  VARCHAR2,
    p_Resultado     IN  VARCHAR2,
    p_FechaDesde    IN  DATE,
    p_FechaHasta    IN  DATE,
    p_Page          IN  NUMBER,
    p_PageSize      IN  NUMBER,
    p_ResultSet     OUT SYS_REFCURSOR,
    p_Message       OUT VARCHAR2,
    p_TotalRecords  OUT NUMBER,
    p_TotalPage     OUT NUMBER
) AS
    v_Page     NUMBER := NVL(p_Page, 1);
    v_PageSize NUMBER := NVL(p_PageSize, 50);
    v_FromRow  NUMBER;
    v_ToRow    NUMBER;
    -- El filtro por fecha se aplica sobre el dia completo: FECHA_INICIO lleva
    -- hora, asi que comparar contra la fecha pelada dejaria fuera todo lo del
    -- ultimo dia del rango.
    v_Desde    DATE := TRUNC(p_FechaDesde);
    v_Hasta    DATE := TRUNC(p_FechaHasta) + 1;
BEGIN
    v_FromRow := ((v_Page - 1) * v_PageSize) + 1;
    v_ToRow   := v_Page * v_PageSize;

    SELECT COUNT(1)
      INTO p_TotalRecords
      FROM MFO_REP_EJEC E
      JOIN MFO_REPORTE R ON R.REPORTE_ID = E.REPORTE_ID
     WHERE E.CODIGO_EMPRESA = p_CodigoEmpresa
       AND (p_FormularioId IS NULL OR R.FORMULARIO_ID = p_FormularioId)
       AND (p_ReporteId    IS NULL OR E.REPORTE_ID    = p_ReporteId)
       AND (p_Usuario      IS NULL OR UPPER(E.USUARIO) = UPPER(p_Usuario))
       AND (p_Resultado    IS NULL OR E.RESULTADO     = UPPER(p_Resultado))
       AND (v_Desde        IS NULL OR E.FECHA_INICIO >= v_Desde)
       AND (v_Hasta        IS NULL OR E.FECHA_INICIO <  v_Hasta);

    p_TotalPage := CEIL(p_TotalRecords / v_PageSize);

    OPEN p_ResultSet FOR
        SELECT *
          FROM (
                SELECT X.*, ROWNUM RN
                  FROM (
                        SELECT E.REP_EJEC_ID,
                               E.REPORTE_ID,
                               R.CLAVE AS CLAVE_REPORTE,
                               R.NOMBRE AS REPORTE,
                               R.FORMULARIO_ID,
                               F.ALIAS,
                               F.NOMBRE AS FORMULARIO,
                               E.RESPUESTA_ID,
                               E.USUARIO,
                               E.FECHA_INICIO,
                               E.MILISEGUNDOS,
                               E.FILAS,
                               E.RESULTADO,
                               E.MENSAJE,
                               E.IP_ORIGEN,
                               CASE WHEN E.PARAMS_CLB IS NULL THEN 'N' ELSE 'S' END AS TIENE_PARAMS
                          FROM MFO_REP_EJEC E
                          JOIN MFO_REPORTE R    ON R.REPORTE_ID    = E.REPORTE_ID
                          JOIN MFO_FORMULARIO F ON F.FORMULARIO_ID = R.FORMULARIO_ID
                         WHERE E.CODIGO_EMPRESA = p_CodigoEmpresa
                           AND (p_FormularioId IS NULL OR R.FORMULARIO_ID = p_FormularioId)
                           AND (p_ReporteId    IS NULL OR E.REPORTE_ID    = p_ReporteId)
                           AND (p_Usuario      IS NULL OR UPPER(E.USUARIO) = UPPER(p_Usuario))
                           AND (p_Resultado    IS NULL OR E.RESULTADO     = UPPER(p_Resultado))
                           AND (v_Desde        IS NULL OR E.FECHA_INICIO >= v_Desde)
                           AND (v_Hasta        IS NULL OR E.FECHA_INICIO <  v_Hasta)
                         ORDER BY E.FECHA_INICIO DESC, E.REP_EJEC_ID DESC
                       ) X
                 WHERE ROWNUM <= v_ToRow
               )
         WHERE RN >= v_FromRow;

    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        p_TotalRecords := 0;
        p_TotalPage := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT NULL REP_EJEC_ID FROM DUAL WHERE 1 = 0;
END;
/
