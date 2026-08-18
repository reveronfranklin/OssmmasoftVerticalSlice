-- =============================================================================
-- MFO - Enviar una respuesta.
--
-- Aqui vive la unica regla de validacion que NO puede evaluar el motor C#: UNICO.
-- Todas las demas se resuelven en memoria con la definicion y el payload; UNICO
-- exige preguntarle a la base si otra respuesta ya uso ese valor, y hacerlo en
-- el mismo momento en que se pasa a ENVIADA es lo unico que cierra la ventana
-- entre "valide" y "guarde".
--
-- Ambito de UNICO (PARAM_1):
--   FORMULARIO - unico entre todas las respuestas del formulario, de cualquier
--                version. Es el ambito util cuando la clave es de negocio (una
--                cedula, un numero de expediente) y debe seguir siendo unica
--                aunque el formulario evolucione.
--   VERSION    - unico solo dentro de la version. Util cuando el significado del
--                campo cambio entre versiones.
--
-- Las respuestas ANULADAS no cuentan para la unicidad: anular existe justamente
-- para poder volver a capturar.
-- =============================================================================
CREATE OR REPLACE PROCEDURE MFO.SP_MFO_RESP_SUBMIT (
    p_RespuestaId IN  NUMBER,
    p_Snapshot    IN  CLOB,
    p_Usuario     IN  VARCHAR2,
    p_IpOrigen    IN  VARCHAR2,
    p_CampoError  OUT VARCHAR2,
    p_Message     OUT VARCHAR2
) AS
    v_version_id    NUMBER;
    v_formulario_id NUMBER;
    v_estado        VARCHAR2(12);
    v_valores       NUMBER;
    v_repetidos     NUMBER;
BEGIN
    p_CampoError := NULL;

    BEGIN
        SELECT VERSION_ID, FORMULARIO_ID, ESTADO
          INTO v_version_id, v_formulario_id, v_estado
          FROM MFO_RESPUESTA WHERE RESPUESTA_ID = p_RespuestaId;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            p_Message := 'La respuesta indicada no existe.';
            RETURN;
    END;

    IF v_estado <> 'BORRADOR' THEN
        p_Message := 'La respuesta ya esta ' || v_estado || '.';
        RETURN;
    END IF;

    SELECT COUNT(1) INTO v_valores FROM MFO_VALOR WHERE RESPUESTA_ID = p_RespuestaId;
    IF v_valores = 0 THEN
        p_Message := 'La respuesta no tiene ningun valor capturado.';
        RETURN;
    END IF;

    -- ------------------------------------------------------------------------
    -- Reglas UNICO
    -- ------------------------------------------------------------------------
    FOR r IN (SELECT C.CLAVE, C.ETIQUETA, G.PARAM_1 AMBITO, G.MENSAJE, T.COLUMNA_VALOR
                FROM MFO_REGLA G
                JOIN MFO_CAMPO C      ON C.CAMPO_ID = G.CAMPO_ID
                JOIN MFO_TIPO_CAMPO T ON T.TIPO_CAMPO_ID = C.TIPO_CAMPO_ID
               WHERE C.VERSION_ID = v_version_id
                 AND G.TIPO_REGLA = 'UNICO'
                 AND G.ACTIVO = 'S') LOOP

        SELECT COUNT(1) INTO v_repetidos
          FROM MFO_VALOR V
          JOIN MFO_RESPUESTA R ON R.RESPUESTA_ID = V.RESPUESTA_ID
         WHERE V.CLAVE_CAMPO = r.CLAVE
           AND V.RESPUESTA_ID <> p_RespuestaId
           AND R.ESTADO = 'ENVIADA'
           AND ((NVL(r.AMBITO, 'FORMULARIO') = 'FORMULARIO'
                 AND R.FORMULARIO_ID = v_formulario_id)
             OR (NVL(r.AMBITO, 'FORMULARIO') = 'VERSION'
                 AND R.VERSION_ID = v_version_id))
           AND EXISTS (SELECT 1
                         FROM MFO_VALOR X
                        WHERE X.RESPUESTA_ID = p_RespuestaId
                          AND X.CLAVE_CAMPO = r.CLAVE
                          AND ((r.COLUMNA_VALOR = 'TXT' AND X.VALOR_TXT = V.VALOR_TXT)
                            OR (r.COLUMNA_VALOR = 'NUM' AND X.VALOR_NUM = V.VALOR_NUM)));

        IF v_repetidos > 0 THEN
            p_CampoError := r.CLAVE;
            p_Message := NVL(r.MENSAJE, 'El valor de ' || r.ETIQUETA || ' ya fue registrado.');
            RETURN;
        END IF;
    END LOOP;

    -- ------------------------------------------------------------------------
    -- Envio
    -- ------------------------------------------------------------------------
    UPDATE MFO_RESPUESTA
       SET ESTADO       = 'ENVIADA',
           FECHA_ENVIO  = SYSDATE,
           SNAPSHOT_CLB = p_Snapshot,
           IP_ORIGEN    = NVL(p_IpOrigen, IP_ORIGEN),
           USUARIO_UPD  = p_Usuario,
           FECHA_UPD    = SYSDATE
     WHERE RESPUESTA_ID = p_RespuestaId;

    INSERT INTO MFO_AUDITORIA (AUDITORIA_ID, ENTIDAD, ENTIDAD_ID, ACCION, USUARIO, FECHA, DETALLE_CLB)
    VALUES (SEQ_MFO_AUDITORIA.NEXTVAL, 'RESPUESTA', p_RespuestaId, 'ENVIAR', p_Usuario, SYSDATE,
            'Version ' || v_version_id || ', ' || v_valores || ' valores.');

    COMMIT;
    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        p_CampoError := NULL;
        p_Message := 'Error tecnico: ' || SQLERRM;
END;
/
