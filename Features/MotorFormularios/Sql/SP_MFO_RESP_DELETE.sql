-- =============================================================================
-- MFO - Eliminar un borrador de respuesta.
--
-- Solo BORRADOR. Una respuesta ENVIADA no se borra nunca -se anula- porque es un
-- hecho registrado: alguien la lleno y la envio, y eso no deja de haber
-- ocurrido porque despues se quiera deshacer.
--
-- Aqui si se aprovecha el ON DELETE CASCADE: el arbol de datos
-- (respuesta -> valor -> adjunto) si lo lleva, porque no tiene triggers de
-- inmutabilidad que consulten una tabla en mutacion. Aun asi se borran los
-- adjuntos de forma explicita antes, para poder devolver cuantos archivos habria
-- que limpiar despues del filesystem: la fila se va, el archivo en disco no.
-- =============================================================================
CREATE OR REPLACE PROCEDURE MFO.SP_MFO_RESP_DELETE (
    p_RespuestaId     IN  NUMBER,
    p_Usuario         IN  VARCHAR2,
    p_AdjuntosHuerfanos OUT NUMBER,
    p_Message         OUT VARCHAR2
) AS
    v_estado VARCHAR2(12);
BEGIN
    p_AdjuntosHuerfanos := 0;

    BEGIN
        SELECT ESTADO INTO v_estado FROM MFO_RESPUESTA WHERE RESPUESTA_ID = p_RespuestaId;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            p_Message := 'La respuesta indicada no existe.';
            RETURN;
    END;

    IF v_estado <> 'BORRADOR' THEN
        p_Message := 'Solo se puede eliminar un borrador. Esta respuesta esta ' || v_estado ||
                     '. Use la anulacion.';
        RETURN;
    END IF;

    SELECT COUNT(1) INTO p_AdjuntosHuerfanos
      FROM MFO_ADJUNTO A
     WHERE A.RUTA IS NOT NULL
       AND A.VALOR_ID IN (SELECT VALOR_ID FROM MFO_VALOR WHERE RESPUESTA_ID = p_RespuestaId);

    DELETE FROM MFO_ADJUNTO
     WHERE VALOR_ID IN (SELECT VALOR_ID FROM MFO_VALOR WHERE RESPUESTA_ID = p_RespuestaId);

    DELETE FROM MFO_VALOR     WHERE RESPUESTA_ID = p_RespuestaId;
    DELETE FROM MFO_RESPUESTA WHERE RESPUESTA_ID = p_RespuestaId;

    INSERT INTO MFO_AUDITORIA (AUDITORIA_ID, ENTIDAD, ENTIDAD_ID, ACCION, USUARIO, FECHA, DETALLE_CLB)
    VALUES (SEQ_MFO_AUDITORIA.NEXTVAL, 'RESPUESTA', p_RespuestaId, 'ELIMINAR', p_Usuario, SYSDATE,
            'Borrador eliminado. Adjuntos a limpiar del filesystem: ' || p_AdjuntosHuerfanos);

    COMMIT;
    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        p_AdjuntosHuerfanos := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
END;
/
