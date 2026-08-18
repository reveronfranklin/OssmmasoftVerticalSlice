-- =============================================================================
-- MFO - Abrir una respuesta en BORRADOR contra la version publicada.
--
-- Se resuelve por ALIAS y no por VERSION_ID a proposito: quien llena un
-- formulario no elige version, siempre le toca la vigente. Amarrar la respuesta
-- a la version concreta ocurre aqui, una sola vez, y a partir de ese momento esa
-- respuesta se renderiza siempre con esa definicion aunque despues se publique
-- otra.
--
-- Idempotencia por CLAVE_IDEM: el cliente genera un GUID y lo repite si tiene
-- que reintentar. Si ya existe una respuesta con esa clave, se devuelve la que
-- ya hay en vez de crear una segunda. Es lo que evita respuestas duplicadas por
-- un doble clic o por un reintento de red.
-- =============================================================================
CREATE OR REPLACE PROCEDURE MFO.SP_MFO_RESP_CREATE (
    p_Alias        IN  VARCHAR2,
    p_ClaveIdem    IN  VARCHAR2,
    p_EntidadRef   IN  VARCHAR2,
    p_ClaveRef     IN  VARCHAR2,
    p_Usuario      IN  VARCHAR2,
    p_IpOrigen     IN  VARCHAR2,
    p_RespuestaId  OUT NUMBER,
    p_VersionId    OUT NUMBER,
    p_Message      OUT VARCHAR2
) AS
    v_formulario_id NUMBER;
    v_version_id    NUMBER;
    v_estado_form   VARCHAR2(12);
    v_empresa       NUMBER;
    v_max_resp      NUMBER;
    v_usadas        NUMBER;
BEGIN
    p_RespuestaId := 0;
    p_VersionId   := 0;

    BEGIN
        SELECT FORMULARIO_ID, VERSION_PUBL_ID, ESTADO, CODIGO_EMPRESA, MAX_RESP_USUARIO
          INTO v_formulario_id, v_version_id, v_estado_form, v_empresa, v_max_resp
          FROM MFO_FORMULARIO
         WHERE ALIAS = UPPER(TRIM(p_Alias));
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            p_Message := 'El formulario ' || p_Alias || ' no existe.';
            RETURN;
    END;

    IF v_estado_form <> 'ACTIVO' THEN
        p_Message := 'El formulario ' || p_Alias || ' esta inactivo.';
        RETURN;
    END IF;

    IF v_version_id IS NULL THEN
        p_Message := 'El formulario ' || p_Alias || ' no tiene una version publicada.';
        RETURN;
    END IF;

    -- Idempotencia. Se comprueba antes que el cupo: un reintento no debe
    -- consumir una respuesta del limite ni fallar por haberlo alcanzado.
    IF p_ClaveIdem IS NOT NULL THEN
        BEGIN
            SELECT RESPUESTA_ID, VERSION_ID
              INTO p_RespuestaId, p_VersionId
              FROM MFO_RESPUESTA
             WHERE CLAVE_IDEM = p_ClaveIdem;

            p_Message := 'success';
            RETURN;
        EXCEPTION
            WHEN NO_DATA_FOUND THEN NULL;
        END;
    END IF;

    -- Cupo por usuario. Solo cuentan las que llegaron a enviarse: un borrador
    -- abandonado no debe bloquear al usuario para siempre.
    IF v_max_resp IS NOT NULL AND p_Usuario IS NOT NULL THEN
        SELECT COUNT(1) INTO v_usadas
          FROM MFO_RESPUESTA
         WHERE FORMULARIO_ID = v_formulario_id
           AND USUARIO_LLENA = p_Usuario
           AND ESTADO = 'ENVIADA';

        IF v_usadas >= v_max_resp THEN
            p_Message := 'Alcanzo el maximo de ' || v_max_resp || ' respuesta(s) para este formulario.';
            RETURN;
        END IF;
    END IF;

    SELECT SEQ_MFO_RESPUESTA.NEXTVAL INTO p_RespuestaId FROM DUAL;
    p_VersionId := v_version_id;

    INSERT INTO MFO_RESPUESTA (
        RESPUESTA_ID, VERSION_ID, FORMULARIO_ID, CODIGO_EMPRESA, ESTADO, CLAVE_IDEM,
        ENTIDAD_REF, CLAVE_REF, USUARIO_LLENA, FECHA_INICIO, IP_ORIGEN,
        USUARIO_INS, FECHA_INS
    ) VALUES (
        p_RespuestaId, v_version_id, v_formulario_id, v_empresa, 'BORRADOR', p_ClaveIdem,
        p_EntidadRef, p_ClaveRef, p_Usuario, SYSDATE, p_IpOrigen,
        p_Usuario, SYSDATE
    );

    COMMIT;
    p_Message := 'success';
EXCEPTION
    WHEN DUP_VAL_ON_INDEX THEN
        -- Dos peticiones con la misma CLAVE_IDEM en paralelo: la que pierde la
        -- carrera recupera la fila que gano, en vez de devolver un error.
        ROLLBACK;
        BEGIN
            SELECT RESPUESTA_ID, VERSION_ID
              INTO p_RespuestaId, p_VersionId
              FROM MFO_RESPUESTA WHERE CLAVE_IDEM = p_ClaveIdem;
            p_Message := 'success';
        EXCEPTION
            WHEN OTHERS THEN
                p_RespuestaId := 0;
                p_VersionId := 0;
                p_Message := 'Error tecnico: ' || SQLERRM;
        END;
    WHEN OTHERS THEN
        ROLLBACK;
        p_RespuestaId := 0;
        p_VersionId := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
END;
/
