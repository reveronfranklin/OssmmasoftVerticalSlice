CREATE OR REPLACE PROCEDURE SIS.SP_OSS_USR_ROL_INS (
    p_USUARIO                 IN VARCHAR2,
    p_CODIGO_USUARIO          IN NUMBER,
    p_DESCRIPCION             IN VARCHAR2,
    p_JSON_MENU               IN CLOB,
    p_CODIGO_USUARIO_ROL_OUT  OUT NUMBER,
    p_Message                 OUT VARCHAR2
) AS
    v_exists NUMBER;
BEGIN
    p_CODIGO_USUARIO_ROL_OUT := NULL;

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
     WHERE UPPER(TRIM(USUARIO)) = UPPER(TRIM(p_USUARIO));

    IF v_exists > 0 THEN
        p_Message := 'USUARIO ya existe en SIS.OSS_USUARIO_ROL';
        RETURN;
    END IF;

    LOCK TABLE SIS.OSS_USUARIO_ROL IN EXCLUSIVE MODE;

    SELECT NVL(MAX(CODIGO_USUARIO_ROL), 0) + 1
      INTO p_CODIGO_USUARIO_ROL_OUT
      FROM SIS.OSS_USUARIO_ROL;

    INSERT INTO SIS.OSS_USUARIO_ROL (
        CODIGO_USUARIO_ROL,
        USUARIO,
        CODIGO_USUARIO,
        DESCRIPCION,
        JSON_MENU
    ) VALUES (
        p_CODIGO_USUARIO_ROL_OUT,
        TRIM(p_USUARIO),
        p_CODIGO_USUARIO,
        p_DESCRIPCION,
        p_JSON_MENU
    );

    p_Message := 'suscces';
EXCEPTION
    WHEN OTHERS THEN
        p_CODIGO_USUARIO_ROL_OUT := NULL;
        p_Message := SUBSTR(SQLERRM, 1, 4000);
END SP_OSS_USR_ROL_INS;
/
