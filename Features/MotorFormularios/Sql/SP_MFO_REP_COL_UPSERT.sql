-- =============================================================================
-- MFO - Alta o modificacion de una columna de reporte tabular.
--
-- Solo tiene sentido para TIPO_EJEC='SP_CURSOR': un reporte ENDPOINT trae su
-- propio layout y estas filas no se leerian nunca. Se rechaza explicitamente en
-- vez de aceptarlas y dejarlas muertas, porque una configuracion que no hace
-- nada es peor que un error: parece que funciona.
--
-- La identidad es (REPORTE_ID, NOMBRE_COL), y NOMBRE_COL debe coincidir con el
-- nombre de la columna que devuelve el ref cursor. No se valida contra el cursor
-- -esta capa no lo ejecuta-; una columna que no exista en el resultado sale
-- vacia y el generador tabular la reporta.
-- =============================================================================
CREATE OR REPLACE PROCEDURE MFO.SP_MFO_REP_COL_UPSERT (
    p_RepColumnaId IN  NUMBER,
    p_ReporteId    IN  NUMBER,
    p_NombreCol    IN  VARCHAR2,
    p_Titulo       IN  VARCHAR2,
    p_Orden        IN  NUMBER,
    p_AnchoRel     IN  NUMBER,
    p_Alineacion   IN  VARCHAR2,
    p_Formato      IN  VARCHAR2,
    p_Totalizar    IN  CHAR,
    p_Agrupar      IN  CHAR,
    p_Visible      IN  CHAR,
    p_Usuario      IN  VARCHAR2,
    p_OutId        OUT NUMBER,
    p_Message      OUT VARCHAR2
) AS
    v_nombre VARCHAR2(30) := UPPER(TRIM(p_NombreCol));
    v_alinea VARCHAR2(8)  := UPPER(TRIM(NVL(p_Alineacion, 'IZQ')));
    v_tipo   MFO_REPORTE.TIPO_EJEC%TYPE;
    v_id     NUMBER;
    v_aud    NUMBER;
    v_msg    VARCHAR2(4000);
BEGIN
    p_OutId := 0;

    IF v_nombre IS NULL OR p_Titulo IS NULL THEN
        p_Message := 'El nombre de la columna y el titulo son obligatorios.';
        RETURN;
    END IF;

    IF v_alinea NOT IN ('IZQ', 'CEN', 'DER') THEN
        p_Message := 'La alineacion debe ser IZQ, CEN o DER.';
        RETURN;
    END IF;

    BEGIN
        SELECT TIPO_EJEC INTO v_tipo
          FROM MFO_REPORTE
         WHERE REPORTE_ID = p_ReporteId;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            p_Message := 'El reporte indicado no existe.';
            RETURN;
    END;

    IF v_tipo <> 'SP_CURSOR' THEN
        p_Message := 'Las columnas solo aplican a reportes de tipo SP_CURSOR. ' ||
                     'Un reporte ENDPOINT trae su propio layout.';
        RETURN;
    END IF;

    BEGIN
        SELECT REP_COLUMNA_ID INTO v_id
          FROM MFO_REP_COLUMNA
         WHERE REPORTE_ID = p_ReporteId
           AND (REP_COLUMNA_ID = p_RepColumnaId OR NOMBRE_COL = v_nombre);
    EXCEPTION
        WHEN NO_DATA_FOUND THEN v_id := NULL;
        WHEN TOO_MANY_ROWS THEN
            p_Message := 'La columna ' || v_nombre || ' ya existe en este reporte.';
            RETURN;
    END;

    IF v_id IS NULL THEN
        SELECT SEQ_MFO_REP_COLUMNA.NEXTVAL INTO v_id FROM DUAL;

        INSERT INTO MFO_REP_COLUMNA (
            REP_COLUMNA_ID, REPORTE_ID, NOMBRE_COL, TITULO, ORDEN, ANCHO_REL,
            ALINEACION, FORMATO, TOTALIZAR, AGRUPAR, VISIBLE
        ) VALUES (
            v_id, p_ReporteId, v_nombre, p_Titulo, NVL(p_Orden, 10), NVL(p_AnchoRel, 1),
            v_alinea, p_Formato, NVL(p_Totalizar, 'N'), NVL(p_Agrupar, 'N'), NVL(p_Visible, 'S')
        );
    ELSE
        UPDATE MFO_REP_COLUMNA
           SET NOMBRE_COL = v_nombre,
               TITULO     = p_Titulo,
               ORDEN      = NVL(p_Orden, ORDEN),
               ANCHO_REL  = NVL(p_AnchoRel, ANCHO_REL),
               ALINEACION = v_alinea,
               FORMATO    = p_Formato,
               TOTALIZAR  = NVL(p_Totalizar, TOTALIZAR),
               AGRUPAR    = NVL(p_Agrupar, AGRUPAR),
               VISIBLE    = NVL(p_Visible, VISIBLE)
         WHERE REP_COLUMNA_ID = v_id;
    END IF;

    COMMIT;

    SP_MFO_AUD_INS('REP_COLUMNA', v_id, CASE WHEN p_RepColumnaId IS NULL THEN 'CREAR' ELSE 'MODIFICAR' END,
                   p_Usuario, 'Columna ' || v_nombre, v_aud, v_msg);

    p_OutId := v_id;
    p_Message := 'success';
EXCEPTION
    WHEN DUP_VAL_ON_INDEX THEN
        ROLLBACK;
        p_OutId := 0;
        p_Message := 'La columna ' || v_nombre || ' ya existe en este reporte.';
    WHEN OTHERS THEN
        ROLLBACK;
        p_OutId := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
END;
/
