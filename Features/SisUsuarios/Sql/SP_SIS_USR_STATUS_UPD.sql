-- Ejecutar como SIS. SP_SIS_USR_STATUS_UPD: 21 caracteres (Oracle 10g).
CREATE OR REPLACE PROCEDURE SP_SIS_USR_STATUS_UPD (
  p_CODIGO_USUARIO IN NUMBER,
  p_STATUS IN VARCHAR2,
  p_USUARIO_UPD IN NUMBER,
  p_CODIGO_EMPRESA IN NUMBER,
  p_Message OUT VARCHAR2
) AS
BEGIN
  IF p_STATUS IS NULL OR p_STATUS NOT IN ('0', '1', '2') THEN
    p_Message := 'Estatus invalido: 0 Inactivo, 1 Activo, 2 Suspendido.';
    RETURN;
  END IF;

  UPDATE SIS_USUARIOS
     SET STATUS = p_STATUS,
         USUARIO_UPD = p_USUARIO_UPD,
         FECHA_UPD = SYSDATE
   WHERE CODIGO_USUARIO = p_CODIGO_USUARIO
     AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

  IF SQL%ROWCOUNT = 0 THEN
    p_Message := 'No se encontro el usuario en la empresa configurada.';
  ELSE
    p_Message := 'Success';
  END IF;
EXCEPTION
  WHEN OTHERS THEN
    p_Message := SQLERRM;
END;
/
