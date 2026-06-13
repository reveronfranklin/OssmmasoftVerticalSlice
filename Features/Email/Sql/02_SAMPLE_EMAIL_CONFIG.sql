/*
  Plantilla de configuracion SMTP para SIS_EMAIL_CONFIG.
  Reemplazar valores antes de ejecutar en QA o produccion.
*/

DECLARE
  v_config_id NUMBER(10);
  v_codigo_empresa NUMBER(10) := 1;
BEGIN
  BEGIN
    SELECT CONFIG_ID
      INTO v_config_id
      FROM SIS_EMAIL_CONFIG
     WHERE CODIGO_EMPRESA = v_codigo_empresa
       AND ROWNUM = 1;
  EXCEPTION
    WHEN NO_DATA_FOUND THEN
      v_config_id := NULL;
  END;

  IF v_config_id IS NULL THEN
    INSERT INTO SIS_EMAIL_CONFIG (
      CONFIG_ID,
      CODIGO_EMPRESA,
      SMTP_HOST,
      SMTP_PORT,
      SMTP_USER,
      SMTP_PASS,
      SMTP_SSL,
      FROM_EMAIL,
      FROM_NAME,
      ACTIVO
    ) VALUES (
      SEQ_EMAIL_CFG.NEXTVAL,
      v_codigo_empresa,
      'smtp.example.com',
      587,
      'usuario_smtp',
      'clave_smtp',
      1,
      'soporte@example.com',
      'Soporte',
      1
    );
  ELSE
    UPDATE SIS_EMAIL_CONFIG
       SET SMTP_HOST = 'smtp.example.com',
           SMTP_PORT = 587,
           SMTP_USER = 'usuario_smtp',
           SMTP_PASS = 'clave_smtp',
           SMTP_SSL = 1,
           FROM_EMAIL = 'soporte@example.com',
           FROM_NAME = 'Soporte',
           ACTIVO = 1,
           UPDATED_AT = SYSDATE
     WHERE CONFIG_ID = v_config_id;
  END IF;

  COMMIT;
END;
/
