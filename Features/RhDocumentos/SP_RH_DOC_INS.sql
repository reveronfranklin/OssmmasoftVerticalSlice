CREATE OR REPLACE PROCEDURE RH.SP_RH_DOC_INS (
    p_CODIGO_PERSONA        IN NUMBER,
    p_TIPO_DOCUMENTO_ID     IN NUMBER,
    p_NUMERO_DOCUMENTO      IN VARCHAR2,
    p_FECHA_VENCIMIENTO     IN DATE,
    p_TIPO_GRADO_ID         IN NUMBER,
    p_GRADO_ID              IN NUMBER,
    p_EXTRA1                IN VARCHAR2,
    p_EXTRA2                IN VARCHAR2,
    p_EXTRA3                IN VARCHAR2,
    p_USUARIO_INS           IN NUMBER,
    p_CODIGO_EMPRESA        IN NUMBER,
    p_CODIGO_DOCUMENTO_OUT  OUT NUMBER,
    p_Message               OUT VARCHAR2
) AS
    v_exists NUMBER;
BEGIN
    p_CODIGO_DOCUMENTO_OUT := NULL;

    SELECT COUNT(1)
      INTO v_exists
      FROM RH.RH_PERSONAS
     WHERE CODIGO_PERSONA = p_CODIGO_PERSONA
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    IF v_exists = 0 THEN
        p_Message := 'CODIGO_PERSONA no existe en RH.RH_PERSONAS para la empresa configurada';
        RETURN;
    END IF;

    SELECT COUNT(1)
      INTO v_exists
      FROM RH.RH_DESCRIPTIVAS
     WHERE DESCRIPCION_ID = p_TIPO_DOCUMENTO_ID;

    IF v_exists = 0 THEN
        p_Message := 'TIPO_DOCUMENTO_ID no existe en RH.RH_DESCRIPTIVAS';
        RETURN;
    END IF;

    IF NVL(p_TIPO_GRADO_ID, 0) > 0 THEN
        SELECT COUNT(1)
          INTO v_exists
          FROM RH.RH_DESCRIPTIVAS
         WHERE DESCRIPCION_ID = p_TIPO_GRADO_ID;

        IF v_exists = 0 THEN
            p_Message := 'TIPO_GRADO_ID no existe en RH.RH_DESCRIPTIVAS';
            RETURN;
        END IF;
    END IF;

    IF NVL(p_GRADO_ID, 0) > 0 THEN
        SELECT COUNT(1)
          INTO v_exists
          FROM RH.RH_DESCRIPTIVAS
         WHERE DESCRIPCION_ID = p_GRADO_ID;

        IF v_exists = 0 THEN
            p_Message := 'GRADO_ID no existe en RH.RH_DESCRIPTIVAS';
            RETURN;
        END IF;
    END IF;

    LOCK TABLE RH.RH_DOCUMENTOS IN EXCLUSIVE MODE;

    SELECT NVL(MAX(CODIGO_DOCUMENTO), 0) + 1
      INTO p_CODIGO_DOCUMENTO_OUT
      FROM RH.RH_DOCUMENTOS;

    INSERT INTO RH.RH_DOCUMENTOS (
        CODIGO_PERSONA,
        CODIGO_DOCUMENTO,
        TIPO_DOCUMENTO_ID,
        NUMERO_DOCUMENTO,
        FECHA_VENCIMIENTO,
        TIPO_GRADO_ID,
        GRADO_ID,
        EXTRA1,
        EXTRA2,
        EXTRA3,
        USUARIO_INS,
        FECHA_INS,
        CODIGO_EMPRESA
    ) VALUES (
        p_CODIGO_PERSONA,
        p_CODIGO_DOCUMENTO_OUT,
        p_TIPO_DOCUMENTO_ID,
        p_NUMERO_DOCUMENTO,
        p_FECHA_VENCIMIENTO,
        p_TIPO_GRADO_ID,
        p_GRADO_ID,
        p_EXTRA1,
        p_EXTRA2,
        p_EXTRA3,
        p_USUARIO_INS,
        SYSDATE,
        p_CODIGO_EMPRESA
    );

    p_Message := 'suscces';
EXCEPTION
    WHEN OTHERS THEN
        p_CODIGO_DOCUMENTO_OUT := NULL;
        p_Message := SUBSTR(SQLERRM, 1, 4000);
END SP_RH_DOC_INS;
/
