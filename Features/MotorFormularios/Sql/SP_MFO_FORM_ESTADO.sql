-- =============================================================================
-- MFO - Activar o inactivar un formulario.
--
-- Es el equivalente a "borrar" desde la UI: un formulario con respuestas no se
-- elimina nunca, se inactiva. INACTIVO significa que no se puede abrir para
-- llenar; las respuestas ya capturadas se siguen consultando.
-- =============================================================================
CREATE OR REPLACE PROCEDURE MFO.SP_MFO_FORM_ESTADO (
    p_FormularioId IN  NUMBER,
    p_Estado       IN  VARCHAR2,
    p_Usuario      IN  VARCHAR2,
    p_Message      OUT VARCHAR2
) AS
    v_existe NUMBER;
BEGIN
    IF p_Estado NOT IN ('ACTIVO', 'INACTIVO') THEN
        p_Message := 'Estado no valido. Use ACTIVO o INACTIVO.';
        RETURN;
    END IF;

    SELECT COUNT(1) INTO v_existe FROM MFO_FORMULARIO WHERE FORMULARIO_ID = p_FormularioId;
    IF v_existe = 0 THEN
        p_Message := 'El formulario indicado no existe.';
        RETURN;
    END IF;

    UPDATE MFO_FORMULARIO
       SET ESTADO      = p_Estado,
           USUARIO_UPD = p_Usuario,
           FECHA_UPD   = SYSDATE
     WHERE FORMULARIO_ID = p_FormularioId;

    INSERT INTO MFO_AUDITORIA (AUDITORIA_ID, ENTIDAD, ENTIDAD_ID, ACCION, USUARIO, FECHA, DETALLE_CLB)
    VALUES (SEQ_MFO_AUDITORIA.NEXTVAL, 'FORMULARIO', p_FormularioId, p_Estado, p_Usuario, SYSDATE, NULL);

    COMMIT;
    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        p_Message := 'Error tecnico: ' || SQLERRM;
END;
/
