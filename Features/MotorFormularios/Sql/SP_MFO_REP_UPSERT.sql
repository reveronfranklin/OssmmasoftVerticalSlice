-- =============================================================================
-- MFO - Alta o modificacion de un reporte enlazado a un formulario.
--
-- La identidad del reporte es (FORMULARIO_ID, CLAVE), no REPORTE_ID: la CLAVE es
-- lo que identifica el reporte en el selector y lo que un dia habra que
-- interpretar al leer una fila vieja de MFO_REP_EJEC.
--
-- CLAVE_REGISTRO se guarda tal como llega y **no se valida aqui contra la lista
-- blanca**: la lista vive en C# y esta capa no la conoce. La validacion ocurre en
-- el momento de ejecutar, que es el unico momento en que importa. Guardar una
-- clave no registrada deja un reporte que no se puede ejecutar -y eso es
-- exactamente lo que debe pasar-, no un agujero.
-- =============================================================================
CREATE OR REPLACE PROCEDURE MFO.SP_MFO_REP_UPSERT (
    p_ReporteId      IN  NUMBER,
    p_FormularioId   IN  NUMBER,
    p_Clave          IN  VARCHAR2,
    p_Nombre         IN  VARCHAR2,
    p_Descripcion    IN  VARCHAR2,
    p_TipoEjec       IN  VARCHAR2,
    p_ClaveRegistro  IN  VARCHAR2,
    p_TituloReporte  IN  VARCHAR2,
    p_Orientacion    IN  VARCHAR2,
    p_MaxFilas       IN  NUMBER,
    p_TimeoutSeg     IN  NUMBER,
    p_Orden          IN  NUMBER,
    p_Activo         IN  CHAR,
    p_Usuario        IN  VARCHAR2,
    p_OutId          OUT NUMBER,
    p_Message        OUT VARCHAR2
) AS
    v_clave    VARCHAR2(40)  := UPPER(TRIM(p_Clave));
    v_registro VARCHAR2(60)  := UPPER(TRIM(p_ClaveRegistro));
    v_tipo     VARCHAR2(12)  := UPPER(TRIM(p_TipoEjec));
    v_orient   VARCHAR2(10)  := UPPER(TRIM(NVL(p_Orientacion, 'VERTICAL')));
    v_existe   NUMBER;
    v_id       NUMBER;
    v_aud      NUMBER;
    v_msg      VARCHAR2(4000);
BEGIN
    p_OutId := 0;

    IF v_clave IS NULL OR p_Nombre IS NULL OR v_registro IS NULL THEN
        p_Message := 'La clave, el nombre y la clave de registro son obligatorios.';
        RETURN;
    END IF;

    IF v_tipo NOT IN ('ENDPOINT', 'SP_CURSOR') THEN
        p_Message := 'El tipo de ejecucion debe ser ENDPOINT o SP_CURSOR.';
        RETURN;
    END IF;

    IF v_orient NOT IN ('VERTICAL', 'HORIZONTAL') THEN
        p_Message := 'La orientacion debe ser VERTICAL u HORIZONTAL.';
        RETURN;
    END IF;

    SELECT COUNT(1) INTO v_existe
      FROM MFO_FORMULARIO
     WHERE FORMULARIO_ID = p_FormularioId;

    IF v_existe = 0 THEN
        p_Message := 'El formulario indicado no existe.';
        RETURN;
    END IF;

    BEGIN
        SELECT REPORTE_ID INTO v_id
          FROM MFO_REPORTE
         WHERE FORMULARIO_ID = p_FormularioId
           AND (REPORTE_ID = p_ReporteId OR CLAVE = v_clave);
    EXCEPTION
        WHEN NO_DATA_FOUND THEN v_id := NULL;
        WHEN TOO_MANY_ROWS THEN
            p_Message := 'La clave ' || v_clave || ' ya la usa otro reporte de este formulario.';
            RETURN;
    END;

    IF v_id IS NULL THEN
        SELECT SEQ_MFO_REPORTE.NEXTVAL INTO v_id FROM DUAL;

        INSERT INTO MFO_REPORTE (
            REPORTE_ID, FORMULARIO_ID, CLAVE, NOMBRE, DESCRIPCION, TIPO_EJEC,
            CLAVE_REGISTRO, TITULO_REPORTE, ORIENTACION, MAX_FILAS, TIMEOUT_SEG,
            ORDEN, ACTIVO, USUARIO_INS, FECHA_INS
        ) VALUES (
            v_id, p_FormularioId, v_clave, p_Nombre, p_Descripcion, v_tipo,
            v_registro, p_TituloReporte, v_orient, p_MaxFilas, p_TimeoutSeg,
            NVL(p_Orden, 10), NVL(p_Activo, 'S'), p_Usuario, SYSDATE
        );
    ELSE
        UPDATE MFO_REPORTE
           SET CLAVE          = v_clave,
               NOMBRE         = p_Nombre,
               DESCRIPCION    = p_Descripcion,
               TIPO_EJEC      = v_tipo,
               CLAVE_REGISTRO = v_registro,
               TITULO_REPORTE = p_TituloReporte,
               ORIENTACION    = v_orient,
               MAX_FILAS      = p_MaxFilas,
               TIMEOUT_SEG    = p_TimeoutSeg,
               ORDEN          = NVL(p_Orden, ORDEN),
               ACTIVO         = NVL(p_Activo, ACTIVO),
               USUARIO_UPD    = p_Usuario,
               FECHA_UPD      = SYSDATE
         WHERE REPORTE_ID = v_id;
    END IF;

    COMMIT;

    SP_MFO_AUD_INS('REPORTE', v_id, CASE WHEN p_ReporteId IS NULL THEN 'CREAR' ELSE 'MODIFICAR' END,
                   p_Usuario, 'Reporte ' || v_clave || ' -> ' || v_registro, v_aud, v_msg);

    p_OutId := v_id;
    p_Message := 'success';
EXCEPTION
    WHEN DUP_VAL_ON_INDEX THEN
        ROLLBACK;
        p_OutId := 0;
        p_Message := 'La clave ' || v_clave || ' ya la usa otro reporte de este formulario.';
    WHEN OTHERS THEN
        ROLLBACK;
        p_OutId := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
END;
/
