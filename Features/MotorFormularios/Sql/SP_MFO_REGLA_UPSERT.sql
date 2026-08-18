-- =============================================================================
-- MFO - Alta o modificacion de una regla de validacion.
--
-- La coherencia entre el tipo de regla y el tipo del campo se comprueba aqui y
-- tambien en SP_MFO_VER_VALIDAR. No es duplicacion ociosa: aqui evita guardar
-- una regla sin sentido en el momento en que el usuario la crea, cuando aun sabe
-- lo que estaba haciendo; alla atrapa las que hayan entrado por otra via -por
-- ejemplo un clonado desde una version cuyo campo cambio de tipo- antes de
-- publicar.
--
-- La identidad de la regla es (CAMPO_ID, TIPO_REGLA): un campo no necesita dos
-- reglas del mismo tipo, y permitirlas solo produciria dos mensajes de error
-- para la misma condicion.
-- =============================================================================
CREATE OR REPLACE PROCEDURE MFO.SP_MFO_REGLA_UPSERT (
    p_ReglaId   IN  NUMBER,
    p_CampoId   IN  NUMBER,
    p_TipoRegla IN  VARCHAR2,
    p_Param1    IN  VARCHAR2,
    p_Param2    IN  VARCHAR2,
    p_Mensaje   IN  VARCHAR2,
    p_Orden     IN  NUMBER,
    p_Activo    IN  CHAR,
    p_Usuario   IN  VARCHAR2,
    p_OutId     OUT NUMBER,
    p_Message   OUT VARCHAR2
) AS
    v_columna  VARCHAR2(3);
    v_multiple CHAR(1);
    v_archivo  CHAR(1);
    v_presenta CHAR(1);
    v_tipo     VARCHAR2(20) := UPPER(TRIM(p_TipoRegla));
    v_id       NUMBER;
    v_ok       BOOLEAN := TRUE;
BEGIN
    p_OutId := 0;

    IF p_Mensaje IS NULL THEN
        p_Message := 'El mensaje de la regla es obligatorio: es lo que vera el usuario al fallar.';
        RETURN;
    END IF;

    BEGIN
        SELECT T.COLUMNA_VALOR, T.ADMITE_MULTIPLE, T.ADMITE_ARCHIVO, T.ES_PRESENTACION
          INTO v_columna, v_multiple, v_archivo, v_presenta
          FROM MFO_CAMPO C
          JOIN MFO_TIPO_CAMPO T ON T.TIPO_CAMPO_ID = C.TIPO_CAMPO_ID
         WHERE C.CAMPO_ID = p_CampoId;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            p_Message := 'El campo indicado no existe.';
            RETURN;
    END;

    IF v_presenta = 'S' THEN
        p_Message := 'Un elemento de presentacion no captura valor y no admite reglas.';
        RETURN;
    END IF;

    IF v_tipo IN ('LONG_MIN', 'LONG_MAX', 'PATRON') AND v_columna NOT IN ('TXT', 'CLB') THEN
        v_ok := FALSE;
    ELSIF v_tipo IN ('MIN', 'MAX', 'DECIMALES') AND v_columna <> 'NUM' THEN
        v_ok := FALSE;
    ELSIF v_tipo IN ('FEC_MIN', 'FEC_MAX') AND v_columna <> 'FEC' THEN
        v_ok := FALSE;
    ELSIF v_tipo IN ('SEL_MIN', 'SEL_MAX') AND v_multiple <> 'S' THEN
        v_ok := FALSE;
    ELSIF v_tipo IN ('ARCH_MAX_MB', 'ARCH_EXT') AND v_archivo <> 'S' THEN
        v_ok := FALSE;
    ELSIF v_tipo = 'UNICO' AND v_columna NOT IN ('TXT', 'NUM') THEN
        v_ok := FALSE;
    END IF;

    IF NOT v_ok THEN
        p_Message := 'La regla ' || v_tipo || ' no aplica al tipo de este campo.';
        RETURN;
    END IF;

    -- Las reglas con parametro obligatorio no sirven de nada sin el.
    IF v_tipo IN ('LONG_MIN', 'LONG_MAX', 'PATRON', 'MIN', 'MAX', 'DECIMALES',
                  'FEC_MIN', 'FEC_MAX', 'SEL_MIN', 'SEL_MAX', 'ARCH_MAX_MB', 'ARCH_EXT')
       AND p_Param1 IS NULL THEN
        p_Message := 'La regla ' || v_tipo || ' necesita un valor en el primer parametro.';
        RETURN;
    END IF;

    BEGIN
        SELECT REGLA_ID INTO v_id
          FROM MFO_REGLA
         WHERE CAMPO_ID = p_CampoId
           AND (REGLA_ID = p_ReglaId OR TIPO_REGLA = v_tipo);
    EXCEPTION
        WHEN NO_DATA_FOUND THEN v_id := NULL;
        WHEN TOO_MANY_ROWS THEN
            p_Message := 'El campo ya tiene una regla ' || v_tipo || '.';
            RETURN;
    END;

    IF v_id IS NULL THEN
        SELECT SEQ_MFO_REGLA.NEXTVAL INTO v_id FROM DUAL;
        INSERT INTO MFO_REGLA (REGLA_ID, CAMPO_ID, TIPO_REGLA, PARAM_1, PARAM_2, MENSAJE, ORDEN, ACTIVO)
        VALUES (v_id, p_CampoId, v_tipo, p_Param1, p_Param2, p_Mensaje, NVL(p_Orden, 10), NVL(p_Activo, 'S'));
    ELSE
        UPDATE MFO_REGLA
           SET TIPO_REGLA = v_tipo,
               PARAM_1    = p_Param1,
               PARAM_2    = p_Param2,
               MENSAJE    = p_Mensaje,
               ORDEN      = NVL(p_Orden, ORDEN),
               ACTIVO     = NVL(p_Activo, ACTIVO)
         WHERE REGLA_ID = v_id;
    END IF;

    -- MFO_CAMPO.REQUERIDO es un atajo de la regla REQUERIDO. Se mantienen
    -- sincronizados aqui para que no exista un campo marcado como obligatorio en
    -- la ficha y sin regla que lo exija, o al reves.
    IF v_tipo = 'REQUERIDO' THEN
        UPDATE MFO_CAMPO
           SET REQUERIDO = CASE WHEN NVL(p_Activo, 'S') = 'S' THEN 'S' ELSE 'N' END
         WHERE CAMPO_ID = p_CampoId;
    END IF;

    COMMIT;
    p_OutId := v_id;
    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        p_OutId := 0;
        IF SQLCODE IN (-20502, -20504) THEN
            p_Message := 'No se puede modificar una version publicada. Cree una version nueva.';
        ELSE
            p_Message := 'Error tecnico: ' || SQLERRM;
        END IF;
END;
/
