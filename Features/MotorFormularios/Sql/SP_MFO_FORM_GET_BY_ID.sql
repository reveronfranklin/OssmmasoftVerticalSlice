-- =============================================================================
-- MFO - Un formulario con su lista de versiones.
--
-- Resuelve por FORMULARIO_ID o por ALIAS: el frontend rutea por alias
-- (/apps/mfo/llenar/[alias]) y el diseñador por id, y obligar a una traduccion
-- previa solo agregaria un viaje a la base.
--
-- Devuelve dos cursores: el sobre del formulario y sus versiones.
-- =============================================================================
CREATE OR REPLACE PROCEDURE MFO.SP_MFO_FORM_GET_BY_ID (
    p_FormularioId IN  NUMBER,
    p_Alias        IN  VARCHAR2,
    p_ResultSet    OUT SYS_REFCURSOR,
    p_Versiones    OUT SYS_REFCURSOR,
    p_Message      OUT VARCHAR2
) AS
    v_formulario_id NUMBER;
BEGIN
    IF p_FormularioId IS NULL AND p_Alias IS NULL THEN
        p_Message := 'Indique el formulario por id o por alias.';
        OPEN p_ResultSet FOR SELECT NULL FORMULARIO_ID FROM DUAL WHERE 1 = 0;
        OPEN p_Versiones FOR SELECT NULL VERSION_ID FROM DUAL WHERE 1 = 0;
        RETURN;
    END IF;

    BEGIN
        SELECT FORMULARIO_ID
          INTO v_formulario_id
          FROM MFO_FORMULARIO
         WHERE (p_FormularioId IS NOT NULL AND FORMULARIO_ID = p_FormularioId)
            OR (p_FormularioId IS NULL     AND ALIAS = p_Alias);
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            p_Message := 'El formulario indicado no existe.';
            OPEN p_ResultSet FOR SELECT NULL FORMULARIO_ID FROM DUAL WHERE 1 = 0;
            OPEN p_Versiones FOR SELECT NULL VERSION_ID FROM DUAL WHERE 1 = 0;
            RETURN;
    END;

    OPEN p_ResultSet FOR
        SELECT F.FORMULARIO_ID,
               F.ALIAS,
               F.NOMBRE,
               F.DESCRIPCION,
               F.CATEGORIA,
               F.CODIGO_EMPRESA,
               F.ESTADO,
               F.VERSION_PUBL_ID,
               F.ENTIDAD_DESTINO,
               F.MAX_RESP_USUARIO,
               F.PERMITE_BORRADOR,
               F.MODO_USO,
               F.REGISTRA_EJEC,
               F.USUARIO_INS,
               F.FECHA_INS,
               F.USUARIO_UPD,
               F.FECHA_UPD
          FROM MFO_FORMULARIO F
         WHERE F.FORMULARIO_ID = v_formulario_id;

    OPEN p_Versiones FOR
        SELECT V.VERSION_ID,
               V.NUMERO,
               V.ESTADO,
               V.NOTAS,
               V.HASH_DEF,
               V.VERSION_ORIGEN_ID,
               V.FECHA_PUBL,
               V.USUARIO_PUBL,
               V.FECHA_ARCH,
               V.FECHA_INS,
               V.USUARIO_INS,
               (SELECT COUNT(1) FROM MFO_CAMPO C WHERE C.VERSION_ID = V.VERSION_ID) AS CAMPOS,
               (SELECT COUNT(1) FROM MFO_RESPUESTA R WHERE R.VERSION_ID = V.VERSION_ID) AS RESPUESTAS
          FROM MFO_VERSION V
         WHERE V.FORMULARIO_ID = v_formulario_id
         ORDER BY V.NUMERO DESC;

    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT NULL FORMULARIO_ID FROM DUAL WHERE 1 = 0;
        OPEN p_Versiones FOR SELECT NULL VERSION_ID FROM DUAL WHERE 1 = 0;
END;
/
