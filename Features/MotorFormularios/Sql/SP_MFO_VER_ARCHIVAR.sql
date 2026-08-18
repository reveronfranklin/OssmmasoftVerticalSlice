-- =============================================================================
-- MFO - Archivar la version publicada, o descartar un borrador.
--
-- Dos operaciones distintas bajo el mismo procedimiento porque para el usuario
-- son la misma intencion -"quitar de en medio esta version"- y el estado de
-- partida decide cual aplica:
--
--   PUBLICADA -> ARCHIVADA. El formulario queda sin version vigente y no se
--   puede llenar hasta que se publique otra. Por eso se avisa y no se hace en
--   silencio.
--
--   BORRADOR -> se elimina. El borrado es explicito y de abajo hacia arriba
--   porque el arbol de definicion no tiene ON DELETE CASCADE: con cascada, los
--   triggers de inmutabilidad fallarian con ORA-04091 al consultar una
--   MFO_VERSION que la propia sentencia esta borrando.
-- =============================================================================
CREATE OR REPLACE PROCEDURE MFO.SP_MFO_VER_ARCHIVAR (
    p_VersionId IN  NUMBER,
    p_Usuario   IN  VARCHAR2,
    p_Message   OUT VARCHAR2
) AS
    v_formulario_id NUMBER;
    v_estado        MFO_VERSION.ESTADO%TYPE;
    v_respuestas    NUMBER;
BEGIN
    BEGIN
        SELECT FORMULARIO_ID, ESTADO INTO v_formulario_id, v_estado
          FROM MFO_VERSION WHERE VERSION_ID = p_VersionId;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            p_Message := 'La version indicada no existe.';
            RETURN;
    END;

    IF v_estado = 'ARCHIVADA' THEN
        p_Message := 'La version ya esta archivada.';
        RETURN;
    END IF;

    IF v_estado = 'PUBLICADA' THEN
        UPDATE MFO_VERSION
           SET ESTADO      = 'ARCHIVADA',
               FECHA_ARCH  = SYSDATE,
               USUARIO_UPD = p_Usuario,
               FECHA_UPD   = SYSDATE
         WHERE VERSION_ID = p_VersionId;

        -- El formulario se queda sin version vigente: no se puede llenar hasta
        -- que se publique otra.
        UPDATE MFO_FORMULARIO
           SET VERSION_PUBL_ID = NULL,
               USUARIO_UPD     = p_Usuario,
               FECHA_UPD       = SYSDATE
         WHERE FORMULARIO_ID = v_formulario_id
           AND VERSION_PUBL_ID = p_VersionId;

        INSERT INTO MFO_AUDITORIA (AUDITORIA_ID, ENTIDAD, ENTIDAD_ID, ACCION, USUARIO, FECHA, DETALLE_CLB)
        VALUES (SEQ_MFO_AUDITORIA.NEXTVAL, 'VERSION', p_VersionId, 'ARCHIVAR', p_Usuario, SYSDATE,
                'El formulario queda sin version publicada.');

        COMMIT;
        p_Message := 'success';
        RETURN;
    END IF;

    -- ------------------------------------------------------------------------
    -- BORRADOR: se descarta por completo.
    -- ------------------------------------------------------------------------
    SELECT COUNT(1) INTO v_respuestas
      FROM MFO_RESPUESTA WHERE VERSION_ID = p_VersionId;

    IF v_respuestas > 0 THEN
        -- No deberia ocurrir -no se responde a un borrador- pero si ocurriera,
        -- borrar la definicion dejaria respuestas sin con que renderizarse.
        p_Message := 'El borrador tiene respuestas asociadas y no se puede eliminar.';
        RETURN;
    END IF;

    DELETE FROM MFO_CONDICION WHERE VERSION_ID = p_VersionId;

    DELETE FROM MFO_OPCION
     WHERE CAMPO_ID IN (SELECT CAMPO_ID FROM MFO_CAMPO WHERE VERSION_ID = p_VersionId);

    DELETE FROM MFO_REGLA
     WHERE CAMPO_ID IN (SELECT CAMPO_ID FROM MFO_CAMPO WHERE VERSION_ID = p_VersionId);

    DELETE FROM MFO_CAMPO   WHERE VERSION_ID = p_VersionId;
    DELETE FROM MFO_SECCION WHERE VERSION_ID = p_VersionId;
    DELETE FROM MFO_VERSION WHERE VERSION_ID = p_VersionId;

    INSERT INTO MFO_AUDITORIA (AUDITORIA_ID, ENTIDAD, ENTIDAD_ID, ACCION, USUARIO, FECHA, DETALLE_CLB)
    VALUES (SEQ_MFO_AUDITORIA.NEXTVAL, 'VERSION', p_VersionId, 'DESCARTAR', p_Usuario, SYSDATE,
            'Borrador descartado del formulario ' || v_formulario_id);

    COMMIT;
    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        p_Message := 'Error tecnico: ' || SQLERRM;
END;
/
