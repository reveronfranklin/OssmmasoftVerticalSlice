-- =============================================================================
-- MFO - Modificacion de los datos de identidad del formulario.
--
-- ALIAS no se puede cambiar y no es un descuido: es la identidad estable del
-- formulario. Lo usan las rutas del frontend, el enlace de reportes y el nombre
-- de la vista de proyeccion MFO_V_<ALIAS>. Cambiarlo romperia enlaces ya
-- repartidos sin que nada avise. Para "renombrar" se cambia NOMBRE, que es lo
-- que ve el usuario.
--
-- Tampoco toca VERSION_PUBL_ID: ese puntero lo mueve solo SP_MFO_VER_PUBLICAR.
-- =============================================================================
CREATE OR REPLACE PROCEDURE MFO.SP_MFO_FORM_UPDATE (
    p_FormularioId    IN  NUMBER,
    p_Nombre          IN  VARCHAR2,
    p_Descripcion     IN  VARCHAR2,
    p_Categoria       IN  VARCHAR2,
    p_EntidadDestino  IN  VARCHAR2,
    p_MaxRespUsuario  IN  NUMBER,
    p_PermiteBorrador IN  CHAR,
    p_ModoUso         IN  VARCHAR2,
    p_RegistraEjec    IN  CHAR,
    p_Usuario         IN  VARCHAR2,
    p_Message         OUT VARCHAR2
) AS
    v_existe NUMBER;
BEGIN
    SELECT COUNT(1) INTO v_existe FROM MFO_FORMULARIO WHERE FORMULARIO_ID = p_FormularioId;
    IF v_existe = 0 THEN
        p_Message := 'El formulario indicado no existe.';
        RETURN;
    END IF;

    IF p_Nombre IS NULL THEN
        p_Message := 'El nombre es obligatorio.';
        RETURN;
    END IF;

    UPDATE MFO_FORMULARIO
       SET NOMBRE           = p_Nombre,
           DESCRIPCION      = p_Descripcion,
           CATEGORIA        = p_Categoria,
           ENTIDAD_DESTINO  = p_EntidadDestino,
           MAX_RESP_USUARIO = p_MaxRespUsuario,
           PERMITE_BORRADOR = NVL(p_PermiteBorrador, PERMITE_BORRADOR),
           MODO_USO         = NVL(p_ModoUso, MODO_USO),
           REGISTRA_EJEC    = NVL(p_RegistraEjec, REGISTRA_EJEC),
           USUARIO_UPD      = p_Usuario,
           FECHA_UPD        = SYSDATE
     WHERE FORMULARIO_ID = p_FormularioId;

    INSERT INTO MFO_AUDITORIA (AUDITORIA_ID, ENTIDAD, ENTIDAD_ID, ACCION, USUARIO, FECHA, DETALLE_CLB)
    VALUES (SEQ_MFO_AUDITORIA.NEXTVAL, 'FORMULARIO', p_FormularioId, 'ACTUALIZAR', p_Usuario, SYSDATE, NULL);

    COMMIT;
    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        p_Message := 'Error tecnico: ' || SQLERRM;
END;
/
