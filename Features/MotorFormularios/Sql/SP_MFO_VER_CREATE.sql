-- =============================================================================
-- MFO - Nueva version vacia, en BORRADOR.
--
-- Un formulario admite como maximo un BORRADOR a la vez. No es una limitacion
-- tecnica sino de producto: dos borradores simultaneos del mismo formulario
-- obligarian a decidir cual se publica y que pasa con el otro, y esa pregunta no
-- tiene respuesta buena. Para empezar de nuevo se descarta el borrador vigente.
--
-- Para arrancar de la definicion anterior en vez de una vacia, usar
-- SP_MFO_VER_CLONE.
-- =============================================================================
CREATE OR REPLACE PROCEDURE MFO.SP_MFO_VER_CREATE (
    p_FormularioId IN  NUMBER,
    p_Notas        IN  VARCHAR2,
    p_Usuario      IN  VARCHAR2,
    p_VersionId    OUT NUMBER,
    p_Message      OUT VARCHAR2
) AS
    v_existe     NUMBER;
    v_borradores NUMBER;
    v_numero     NUMBER;
BEGIN
    p_VersionId := 0;

    SELECT COUNT(1) INTO v_existe FROM MFO_FORMULARIO WHERE FORMULARIO_ID = p_FormularioId;
    IF v_existe = 0 THEN
        p_Message := 'El formulario indicado no existe.';
        RETURN;
    END IF;

    SELECT COUNT(1) INTO v_borradores
      FROM MFO_VERSION
     WHERE FORMULARIO_ID = p_FormularioId AND ESTADO = 'BORRADOR';

    IF v_borradores > 0 THEN
        p_Message := 'El formulario ya tiene una version en BORRADOR. Publiquela o descartela antes de crear otra.';
        RETURN;
    END IF;

    SELECT NVL(MAX(NUMERO), 0) + 1 INTO v_numero
      FROM MFO_VERSION
     WHERE FORMULARIO_ID = p_FormularioId;

    SELECT SEQ_MFO_VERSION.NEXTVAL INTO p_VersionId FROM DUAL;
    INSERT INTO MFO_VERSION (
        VERSION_ID, FORMULARIO_ID, NUMERO, ESTADO, NOTAS, VERSION_ORIGEN_ID,
        USUARIO_INS, FECHA_INS
    ) VALUES (
        p_VersionId, p_FormularioId, v_numero, 'BORRADOR', p_Notas, NULL,
        p_Usuario, SYSDATE
    );

    INSERT INTO MFO_AUDITORIA (AUDITORIA_ID, ENTIDAD, ENTIDAD_ID, ACCION, USUARIO, FECHA, DETALLE_CLB)
    VALUES (SEQ_MFO_AUDITORIA.NEXTVAL, 'VERSION', p_VersionId, 'CREAR', p_Usuario, SYSDATE,
            'Version ' || v_numero || ' del formulario ' || p_FormularioId);

    COMMIT;
    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        p_VersionId := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
END;
/
