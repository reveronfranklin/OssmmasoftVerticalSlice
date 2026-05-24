CREATE OR REPLACE PROCEDURE SIS.SP_OSS_USR_ROL_UPD (
    p_CODIGO_USUARIO_ROL IN NUMBER,
    p_USUARIO            IN VARCHAR2,
    p_CODIGO_USUARIO     IN NUMBER,
    p_DESCRIPCION        IN VARCHAR2,
    p_JSON_MENU          IN CLOB,
    p_Message            OUT VARCHAR2
) AS
    v_exists NUMBER;
BEGIN
    SELECT COUNT(1)
      INTO v_exists
      FROM SIS.OSS_USUARIO_ROL
     WHERE CODIGO_USUARIO_ROL = p_CODIGO_USUARIO_ROL;

    IF v_exists = 0 THEN
        p_Message := 'CODIGO_USUARIO_ROL no existe en SIS.OSS_USUARIO_ROL';
        RETURN;
    END IF;

    IF p_USUARIO IS NULL OR TRIM(p_USUARIO) IS NULL THEN
        p_Message := 'USUARIO es requerido';
        RETURN;
    END IF;

    IF p_CODIGO_USUARIO IS NULL OR p_CODIGO_USUARIO <= 0 THEN
        p_Message := 'CODIGO_USUARIO debe ser mayor a cero';
        RETURN;
    END IF;

    IF p_JSON_MENU IS NULL THEN
        p_Message := 'JSON_MENU es requerido';
        RETURN;
    END IF;

    SELECT COUNT(1)
      INTO v_exists
      FROM SIS.OSS_USUARIO_ROL
     WHERE UPPER(TRIM(USUARIO)) = UPPER(TRIM(p_USUARIO))
       AND CODIGO_USUARIO_ROL <> p_CODIGO_USUARIO_ROL;

    IF v_exists > 0 THEN
        p_Message := 'USUARIO ya existe en SIS.OSS_USUARIO_ROL';
        RETURN;
    END IF;

    UPDATE SIS.OSS_USUARIO_ROL
       SET USUARIO = TRIM(p_USUARIO),
           CODIGO_USUARIO = p_CODIGO_USUARIO,
           DESCRIPCION = p_DESCRIPCION,
           JSON_MENU = p_JSON_MENU
     WHERE CODIGO_USUARIO_ROL = p_CODIGO_USUARIO_ROL;

    p_Message := 'suscces';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SUBSTR(SQLERRM, 1, 4000);
END SP_OSS_USR_ROL_UPD;
/
