-- =============================================================================
-- MFO - Reportes enlazados a un formulario, con sus parametros y columnas.
--
-- Alimenta dos cosas distintas con una sola llamada:
--   * el selector de reportes de la pantalla de parametros (cursor de reportes);
--   * la resolucion de parametros del backend antes de ejecutar (los otros dos).
--
-- El cursor de parametros devuelve CLAVE_CAMPO ademas de CAMPO_ID, y eso es
-- deliberado: el backend empareja los valores del payload por CLAVE, no por
-- CAMPO_ID. Un CAMPO_ID pertenece a una version concreta, asi que al publicar
-- una version nueva todos los CAMPO_ID cambian; la CLAVE la preserva el clonado.
-- Emparejar por clave es lo que hace que el enlace formulario-reporte sobreviva
-- a un cambio de version sin reconfigurarlo.
--
-- No existe -ni debe existir- un parametro que diga que ejecutar. CLAVE_REGISTRO
-- viaja como dato para que el backend lo BUSQUE en su lista blanca.
-- =============================================================================
CREATE OR REPLACE PROCEDURE MFO.SP_MFO_REP_GET_BY_FORM (
    p_FormularioId IN  NUMBER,
    p_Alias        IN  VARCHAR2,
    p_ReporteId    IN  NUMBER,
    p_SoloActivos  IN  CHAR,
    p_Reportes     OUT SYS_REFCURSOR,
    p_Parametros   OUT SYS_REFCURSOR,
    p_Columnas     OUT SYS_REFCURSOR,
    p_Message      OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
    v_form_id NUMBER := p_FormularioId;
    v_activos CHAR(1) := NVL(p_SoloActivos, 'S');
BEGIN
    p_TotalRecords := 0;

    -- El formulario se resuelve por id o por alias: el renderizador rutea por
    -- alias y el diseñador por id.
    IF v_form_id IS NULL AND p_Alias IS NOT NULL THEN
        BEGIN
            SELECT FORMULARIO_ID INTO v_form_id
              FROM MFO_FORMULARIO
             WHERE ALIAS = UPPER(TRIM(p_Alias));
        EXCEPTION
            WHEN NO_DATA_FOUND THEN
                p_Message := 'El formulario ' || p_Alias || ' no existe.';
                OPEN p_Reportes   FOR SELECT NULL REPORTE_ID    FROM DUAL WHERE 1 = 0;
                OPEN p_Parametros FOR SELECT NULL REP_PARAM_ID  FROM DUAL WHERE 1 = 0;
                OPEN p_Columnas   FOR SELECT NULL REP_COLUMNA_ID FROM DUAL WHERE 1 = 0;
                RETURN;
        END;
    END IF;

    IF v_form_id IS NULL AND p_ReporteId IS NULL THEN
        p_Message := 'Indique el formulario o el reporte.';
        OPEN p_Reportes   FOR SELECT NULL REPORTE_ID     FROM DUAL WHERE 1 = 0;
        OPEN p_Parametros FOR SELECT NULL REP_PARAM_ID   FROM DUAL WHERE 1 = 0;
        OPEN p_Columnas   FOR SELECT NULL REP_COLUMNA_ID FROM DUAL WHERE 1 = 0;
        RETURN;
    END IF;

    SELECT COUNT(1)
      INTO p_TotalRecords
      FROM MFO_REPORTE R
     WHERE (v_form_id   IS NULL OR R.FORMULARIO_ID = v_form_id)
       AND (p_ReporteId IS NULL OR R.REPORTE_ID    = p_ReporteId)
       AND (v_activos <> 'S' OR R.ACTIVO = 'S');

    OPEN p_Reportes FOR
        SELECT R.REPORTE_ID,
               R.FORMULARIO_ID,
               F.ALIAS,
               R.CLAVE,
               R.NOMBRE,
               R.DESCRIPCION,
               R.TIPO_EJEC,
               R.CLAVE_REGISTRO,
               R.TITULO_REPORTE,
               R.ORIENTACION,
               R.MAX_FILAS,
               R.TIMEOUT_SEG,
               R.ORDEN,
               R.ACTIVO,
               F.MODO_USO,
               F.REGISTRA_EJEC,
               (SELECT COUNT(1) FROM MFO_REP_PARAM P
                 WHERE P.REPORTE_ID = R.REPORTE_ID) AS PARAMETROS,
               (SELECT COUNT(1) FROM MFO_REP_COLUMNA C
                 WHERE C.REPORTE_ID = R.REPORTE_ID) AS COLUMNAS
          FROM MFO_REPORTE R
          JOIN MFO_FORMULARIO F ON F.FORMULARIO_ID = R.FORMULARIO_ID
         WHERE (v_form_id   IS NULL OR R.FORMULARIO_ID = v_form_id)
           AND (p_ReporteId IS NULL OR R.REPORTE_ID    = p_ReporteId)
           AND (v_activos <> 'S' OR R.ACTIVO = 'S')
         ORDER BY R.ORDEN, R.NOMBRE;

    OPEN p_Parametros FOR
        SELECT P.REP_PARAM_ID,
               P.REPORTE_ID,
               P.NOMBRE_PARAM,
               P.ORIGEN,
               P.CAMPO_ID,
               C.CLAVE AS CLAVE_CAMPO,
               C.ETIQUETA AS ETIQUETA_CAMPO,
               P.VALOR_FIJO,
               P.CLAVE_SISTEMA,
               P.TIPO_DATO,
               P.FORMATO,
               P.OBLIGATORIO,
               P.VALOR_DEFECTO,
               P.ORDEN
          FROM MFO_REP_PARAM P
          JOIN MFO_REPORTE R ON R.REPORTE_ID = P.REPORTE_ID
          LEFT JOIN MFO_CAMPO C ON C.CAMPO_ID = P.CAMPO_ID
         WHERE (v_form_id   IS NULL OR R.FORMULARIO_ID = v_form_id)
           AND (p_ReporteId IS NULL OR R.REPORTE_ID    = p_ReporteId)
           AND (v_activos <> 'S' OR R.ACTIVO = 'S')
         ORDER BY P.REPORTE_ID, P.ORDEN, P.NOMBRE_PARAM;

    OPEN p_Columnas FOR
        SELECT L.REP_COLUMNA_ID,
               L.REPORTE_ID,
               L.NOMBRE_COL,
               L.TITULO,
               L.ORDEN,
               L.ANCHO_REL,
               L.ALINEACION,
               L.FORMATO,
               L.TOTALIZAR,
               L.AGRUPAR,
               L.VISIBLE
          FROM MFO_REP_COLUMNA L
          JOIN MFO_REPORTE R ON R.REPORTE_ID = L.REPORTE_ID
         WHERE (v_form_id   IS NULL OR R.FORMULARIO_ID = v_form_id)
           AND (p_ReporteId IS NULL OR R.REPORTE_ID    = p_ReporteId)
           AND (v_activos <> 'S' OR R.ACTIVO = 'S')
         ORDER BY L.REPORTE_ID, L.ORDEN, L.NOMBRE_COL;

    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_Reportes   FOR SELECT NULL REPORTE_ID     FROM DUAL WHERE 1 = 0;
        OPEN p_Parametros FOR SELECT NULL REP_PARAM_ID   FROM DUAL WHERE 1 = 0;
        OPEN p_Columnas   FOR SELECT NULL REP_COLUMNA_ID FROM DUAL WHERE 1 = 0;
END;
/
