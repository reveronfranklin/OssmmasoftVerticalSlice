-- =============================================================================
-- MFO - Alta o modificacion de una opcion estatica de un campo.
--
-- La identidad de la opcion es VALOR, no OPCION_ID: VALOR es lo que se persiste
-- en MFO_VALOR y lo que un dia habra que interpretar al leer una respuesta
-- vieja. Por eso el upsert busca por (CAMPO_ID, VALOR) y la ETIQUETA se trata
-- como texto de presentacion, que si puede cambiar.
-- =============================================================================
CREATE OR REPLACE PROCEDURE MFO.SP_MFO_OPCION_UPSERT (
    p_OpcionId  IN  NUMBER,
    p_CampoId   IN  NUMBER,
    p_Valor     IN  VARCHAR2,
    p_Etiqueta  IN  VARCHAR2,
    p_Orden     IN  NUMBER,
    p_Grupo     IN  VARCHAR2,
    p_EsDefecto IN  CHAR,
    p_Activo    IN  CHAR,
    p_Usuario   IN  VARCHAR2,
    p_OutId     OUT NUMBER,
    p_Message   OUT VARCHAR2
) AS
    v_valor    VARCHAR2(100) := TRIM(p_Valor);
    v_admite   CHAR(1);
    v_multiple CHAR(1);
    v_id       NUMBER;
BEGIN
    p_OutId := 0;

    IF v_valor IS NULL OR p_Etiqueta IS NULL THEN
        p_Message := 'El valor y la etiqueta de la opcion son obligatorios.';
        RETURN;
    END IF;

    BEGIN
        SELECT T.ADMITE_OPCIONES, T.ADMITE_MULTIPLE
          INTO v_admite, v_multiple
          FROM MFO_CAMPO C
          JOIN MFO_TIPO_CAMPO T ON T.TIPO_CAMPO_ID = C.TIPO_CAMPO_ID
         WHERE C.CAMPO_ID = p_CampoId;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            p_Message := 'El campo indicado no existe.';
            RETURN;
    END;

    IF v_admite <> 'S' THEN
        p_Message := 'El tipo de este campo no admite opciones.';
        RETURN;
    END IF;

    BEGIN
        SELECT OPCION_ID INTO v_id
          FROM MFO_OPCION
         WHERE CAMPO_ID = p_CampoId
           AND (OPCION_ID = p_OpcionId OR VALOR = v_valor);
    EXCEPTION
        WHEN NO_DATA_FOUND THEN v_id := NULL;
        WHEN TOO_MANY_ROWS THEN
            p_Message := 'El valor ' || v_valor || ' ya lo usa otra opcion de este campo.';
            RETURN;
    END;

    IF v_id IS NULL THEN
        SELECT SEQ_MFO_OPCION.NEXTVAL INTO v_id FROM DUAL;
        INSERT INTO MFO_OPCION (OPCION_ID, CAMPO_ID, VALOR, ETIQUETA, ORDEN, GRUPO, ES_DEFECTO, ACTIVO)
        VALUES (v_id, p_CampoId, v_valor, p_Etiqueta, NVL(p_Orden, 10), p_Grupo,
                NVL(p_EsDefecto, 'N'), NVL(p_Activo, 'S'));
    ELSE
        UPDATE MFO_OPCION
           SET VALOR      = v_valor,
               ETIQUETA   = p_Etiqueta,
               ORDEN      = NVL(p_Orden, ORDEN),
               GRUPO      = p_Grupo,
               ES_DEFECTO = NVL(p_EsDefecto, ES_DEFECTO),
               ACTIVO     = NVL(p_Activo, ACTIVO)
         WHERE OPCION_ID = v_id;
    END IF;

    -- En un campo de seleccion simple solo puede haber una opcion por defecto.
    -- La ultima marcada gana; las demas se desmarcan.
    IF NVL(p_EsDefecto, 'N') = 'S' AND v_multiple = 'N' THEN
        UPDATE MFO_OPCION
           SET ES_DEFECTO = 'N'
         WHERE CAMPO_ID = p_CampoId
           AND OPCION_ID <> v_id
           AND ES_DEFECTO = 'S';
    END IF;

    COMMIT;
    p_OutId := v_id;
    p_Message := 'success';
EXCEPTION
    WHEN DUP_VAL_ON_INDEX THEN
        ROLLBACK;
        p_OutId := 0;
        p_Message := 'El valor ' || v_valor || ' ya lo usa otra opcion de este campo.';
    WHEN OTHERS THEN
        ROLLBACK;
        p_OutId := 0;
        IF SQLCODE = -20503 THEN
            p_Message := 'No se puede modificar una version publicada. Cree una version nueva.';
        ELSE
            p_Message := 'Error tecnico: ' || SQLERRM;
        END IF;
END;
/
