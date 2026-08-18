-- =============================================================================
-- MFO - Ultimas ejecuciones de un usuario, con sus parametros.
--
-- Este es el procedimiento que da la capacidad que los dialogos de parametros
-- codificados a mano no tienen: volver a correr el reporte de ayer sin
-- reescribir los filtros. Sale gratis del modelo porque PARAMS_CLB ya se
-- escribe en cada ejecucion.
--
-- Solo devuelve ejecuciones con parametros registrados: una fila sin PARAMS_CLB
-- no se puede recargar, y ofrecerla en la lista seria ofrecer un boton que no
-- hace nada. Las ERROR si entran: repetir la ejecucion que fallo, corrigiendo un
-- filtro, es un caso de uso legitimo.
-- =============================================================================
CREATE OR REPLACE PROCEDURE MFO.SP_MFO_REP_ULTIMOS (
    p_ReporteId    IN  NUMBER,
    p_FormularioId IN  NUMBER,
    p_Usuario      IN  VARCHAR2,
    p_Cantidad     IN  NUMBER,
    p_ResultSet    OUT SYS_REFCURSOR,
    p_Message      OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
    -- Tope duro: es una lista de atajos, no una bitacora. Para la bitacora esta
    -- SP_MFO_REP_EJEC_LIST, que si pagina.
    v_cantidad NUMBER := LEAST(NVL(p_Cantidad, 10), 50);
BEGIN
    p_TotalRecords := 0;

    IF p_Usuario IS NULL THEN
        p_Message := 'Indique el usuario.';
        OPEN p_ResultSet FOR SELECT NULL REP_EJEC_ID FROM DUAL WHERE 1 = 0;
        RETURN;
    END IF;

    IF p_ReporteId IS NULL AND p_FormularioId IS NULL THEN
        p_Message := 'Indique el reporte o el formulario.';
        OPEN p_ResultSet FOR SELECT NULL REP_EJEC_ID FROM DUAL WHERE 1 = 0;
        RETURN;
    END IF;

    SELECT COUNT(1)
      INTO p_TotalRecords
      FROM MFO_REP_EJEC E
      JOIN MFO_REPORTE R ON R.REPORTE_ID = E.REPORTE_ID
     WHERE UPPER(E.USUARIO) = UPPER(p_Usuario)
       AND E.PARAMS_CLB IS NOT NULL
       AND (p_ReporteId    IS NULL OR E.REPORTE_ID    = p_ReporteId)
       AND (p_FormularioId IS NULL OR R.FORMULARIO_ID = p_FormularioId);

    OPEN p_ResultSet FOR
        SELECT *
          FROM (
                SELECT E.REP_EJEC_ID,
                       E.REPORTE_ID,
                       R.CLAVE AS CLAVE_REPORTE,
                       R.NOMBRE AS REPORTE,
                       R.FORMULARIO_ID,
                       F.ALIAS,
                       E.USUARIO,
                       E.FECHA_INICIO,
                       E.MILISEGUNDOS,
                       E.FILAS,
                       E.RESULTADO,
                       E.MENSAJE,
                       E.PARAMS_CLB
                  FROM MFO_REP_EJEC E
                  JOIN MFO_REPORTE R    ON R.REPORTE_ID    = E.REPORTE_ID
                  JOIN MFO_FORMULARIO F ON F.FORMULARIO_ID = R.FORMULARIO_ID
                 WHERE UPPER(E.USUARIO) = UPPER(p_Usuario)
                   AND E.PARAMS_CLB IS NOT NULL
                   AND (p_ReporteId    IS NULL OR E.REPORTE_ID    = p_ReporteId)
                   AND (p_FormularioId IS NULL OR R.FORMULARIO_ID = p_FormularioId)
                 ORDER BY E.FECHA_INICIO DESC, E.REP_EJEC_ID DESC
               )
         WHERE ROWNUM <= v_cantidad;

    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT NULL REP_EJEC_ID FROM DUAL WHERE 1 = 0;
END;
/
