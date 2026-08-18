-- =============================================================================
-- MFO - Insertar una entrada en la bitacora.
--
-- Los procedimientos del motor escriben en MFO_AUDITORIA directamente porque lo
-- hacen dentro de su propia transaccion. Este procedimiento existe para lo que
-- audita el backend C#: descargas de adjuntos, consultas de datos sensibles,
-- accesos denegados por permisos. Cosas que ocurren en la capa de aplicacion y
-- que ningun procedimiento de negocio ve pasar.
--
-- Hace COMMIT propio a proposito: una entrada de bitacora no debe perderse
-- porque la operacion que la origino se deshaga. Auditar un intento fallido es
-- justamente lo que interesa.
-- =============================================================================
CREATE OR REPLACE PROCEDURE MFO.SP_MFO_AUD_INS (
    p_Entidad    IN  VARCHAR2,
    p_EntidadId  IN  NUMBER,
    p_Accion     IN  VARCHAR2,
    p_Usuario    IN  VARCHAR2,
    p_Detalle    IN  CLOB,
    p_OutId      OUT NUMBER,
    p_Message    OUT VARCHAR2
) AS
    PRAGMA AUTONOMOUS_TRANSACTION;
BEGIN
    p_OutId := 0;

    IF p_Entidad IS NULL OR p_Accion IS NULL THEN
        p_Message := 'La entidad y la accion son obligatorias.';
        RETURN;
    END IF;

    SELECT SEQ_MFO_AUDITORIA.NEXTVAL INTO p_OutId FROM DUAL;

    INSERT INTO MFO_AUDITORIA (AUDITORIA_ID, ENTIDAD, ENTIDAD_ID, ACCION, USUARIO, FECHA, DETALLE_CLB)
    VALUES (p_OutId, UPPER(p_Entidad), p_EntidadId, UPPER(p_Accion), p_Usuario, SYSDATE, p_Detalle);

    COMMIT;
    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        p_OutId := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
END;
/
