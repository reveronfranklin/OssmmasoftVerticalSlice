-- =============================================================================
-- MFO - Alta de formulario.
--
-- No crea la version 1: eso es SP_MFO_VER_CREATE. Un formulario recien creado
-- vive sin definicion hasta que el diseñador le agrega una, y ese estado
-- intermedio es legitimo -es exactamente lo que ve el usuario al pulsar "nuevo"
-- antes de poner el primer campo-.
-- =============================================================================
CREATE OR REPLACE PROCEDURE MFO.SP_MFO_FORM_CREATE (
    p_Alias           IN  VARCHAR2,
    p_Nombre          IN  VARCHAR2,
    p_Descripcion     IN  VARCHAR2,
    p_Categoria       IN  VARCHAR2,
    p_CodigoEmpresa   IN  NUMBER,
    p_EntidadDestino  IN  VARCHAR2,
    p_MaxRespUsuario  IN  NUMBER,
    p_PermiteBorrador IN  CHAR,
    p_ModoUso         IN  VARCHAR2,
    p_RegistraEjec    IN  CHAR,
    p_Usuario         IN  VARCHAR2,
    p_FormularioId    OUT NUMBER,
    p_Message         OUT VARCHAR2
) AS
    v_alias VARCHAR2(24) := UPPER(TRIM(p_Alias));
BEGIN
    p_FormularioId := 0;

    IF v_alias IS NULL OR p_Nombre IS NULL THEN
        p_Message := 'El alias y el nombre son obligatorios.';
        RETURN;
    END IF;

    -- Se valida aqui y no solo con el CHECK para poder devolver un mensaje que
    -- explique la regla, en vez de un ORA-02290 crudo.
    IF NOT REGEXP_LIKE(v_alias, '^[A-Z][A-Z0-9_]*$') THEN
        p_Message := 'El alias debe empezar por letra y contener solo letras mayusculas, numeros y guion bajo.';
        RETURN;
    END IF;

    IF p_CodigoEmpresa IS NULL THEN
        p_Message := 'No se resolvio el codigo de empresa.';
        RETURN;
    END IF;

    SELECT SEQ_MFO_FORMULARIO.NEXTVAL INTO p_FormularioId FROM DUAL;
    INSERT INTO MFO_FORMULARIO (
        FORMULARIO_ID, ALIAS, NOMBRE, DESCRIPCION, CATEGORIA, CODIGO_EMPRESA,
        ESTADO, VERSION_PUBL_ID, ENTIDAD_DESTINO, MAX_RESP_USUARIO,
        PERMITE_BORRADOR, MODO_USO, REGISTRA_EJEC, USUARIO_INS, FECHA_INS
    ) VALUES (
        p_FormularioId, v_alias, p_Nombre, p_Descripcion, p_Categoria, p_CodigoEmpresa,
        'ACTIVO', NULL, p_EntidadDestino, p_MaxRespUsuario,
        NVL(p_PermiteBorrador, 'S'), NVL(p_ModoUso, 'CAPTURA'), NVL(p_RegistraEjec, 'N'),
        p_Usuario, SYSDATE
    );

    INSERT INTO MFO_AUDITORIA (AUDITORIA_ID, ENTIDAD, ENTIDAD_ID, ACCION, USUARIO, FECHA, DETALLE_CLB)
    VALUES (SEQ_MFO_AUDITORIA.NEXTVAL, 'FORMULARIO', p_FormularioId, 'CREAR', p_Usuario, SYSDATE,
            'Alias ' || v_alias);

    COMMIT;
    p_Message := 'success';
EXCEPTION
    WHEN DUP_VAL_ON_INDEX THEN
        ROLLBACK;
        p_FormularioId := 0;
        p_Message := 'Ya existe un formulario con el alias ' || v_alias || '.';
    WHEN OTHERS THEN
        ROLLBACK;
        p_FormularioId := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
END;
/
