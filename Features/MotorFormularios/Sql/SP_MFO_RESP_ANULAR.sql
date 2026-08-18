-- =============================================================================
-- MFO - Anular una respuesta enviada.
--
-- Anular no borra: cambia el estado y guarda el motivo. Los valores se
-- conservan, porque la pregunta "que decia esto antes de anularse" es
-- exactamente la que se hace despues.
--
-- Efecto lateral relevante: una respuesta ANULADA deja de contar para las reglas
-- UNICO y para MAX_RESP_USUARIO. Es lo que permite volver a capturar un dato que
-- se registro mal.
-- =============================================================================
CREATE OR REPLACE PROCEDURE MFO.SP_MFO_RESP_ANULAR (
    p_RespuestaId IN  NUMBER,
    p_Motivo      IN  VARCHAR2,
    p_Usuario     IN  VARCHAR2,
    p_Message     OUT VARCHAR2
) AS
    v_estado VARCHAR2(12);
BEGIN
    IF p_Motivo IS NULL OR LENGTH(TRIM(p_Motivo)) = 0 THEN
        p_Message := 'Indique el motivo de la anulacion.';
        RETURN;
    END IF;

    BEGIN
        SELECT ESTADO INTO v_estado FROM MFO_RESPUESTA WHERE RESPUESTA_ID = p_RespuestaId;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            p_Message := 'La respuesta indicada no existe.';
            RETURN;
    END;

    IF v_estado = 'ANULADA' THEN
        p_Message := 'La respuesta ya esta anulada.';
        RETURN;
    END IF;

    IF v_estado = 'BORRADOR' THEN
        p_Message := 'Un borrador no se anula: se elimina.';
        RETURN;
    END IF;

    UPDATE MFO_RESPUESTA
       SET ESTADO       = 'ANULADA',
           MOTIVO_ANULA = p_Motivo,
           USUARIO_UPD  = p_Usuario,
           FECHA_UPD    = SYSDATE
     WHERE RESPUESTA_ID = p_RespuestaId;

    INSERT INTO MFO_AUDITORIA (AUDITORIA_ID, ENTIDAD, ENTIDAD_ID, ACCION, USUARIO, FECHA, DETALLE_CLB)
    VALUES (SEQ_MFO_AUDITORIA.NEXTVAL, 'RESPUESTA', p_RespuestaId, 'ANULAR', p_Usuario, SYSDATE, p_Motivo);

    COMMIT;
    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        p_Message := 'Error tecnico: ' || SQLERRM;
END;
/
