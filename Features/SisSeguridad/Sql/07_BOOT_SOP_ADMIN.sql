PROMPT Bootstrap administrador inicial de Soporte
PROMPT Este script asigna SOPORTE_ADMIN y crea cache JSON_MENU legacy.

ACCEPT CODIGO_USUARIO_ADMIN NUMBER PROMPT 'Codigo usuario administrador Soporte: '
ACCEPT CODIGO_EMPRESA NUMBER PROMPT 'Codigo empresa: '
ACCEPT USUARIO_ACCION NUMBER PROMPT 'Codigo usuario accion/auditoria: '

DECLARE
  v_usuario VARCHAR2(100);
  v_count NUMBER;
  v_json CLOB;
BEGIN
  SELECT COUNT(1)
    INTO v_count
    FROM SIS_USUARIOS
   WHERE CODIGO_USUARIO = &CODIGO_USUARIO_ADMIN
     AND CODIGO_EMPRESA = &CODIGO_EMPRESA;

  IF v_count = 0 THEN
    RAISE_APPLICATION_ERROR(-20001, 'Usuario admin no existe para la empresa indicada.');
  END IF;

  SELECT COUNT(1)
    INTO v_count
    FROM OSS_ROL
   WHERE CLAVE = 'SOPORTE_ADMIN'
     AND ACTIVO = 1;

  IF v_count = 0 THEN
    RAISE_APPLICATION_ERROR(-20002, 'Rol SOPORTE_ADMIN no existe. Ejecute 00_INSTALL_SIS_SEGURIDAD.sql.');
  END IF;

  SELECT NVL(LOGIN, USUARIO)
    INTO v_usuario
    FROM SIS_USUARIOS
   WHERE CODIGO_USUARIO = &CODIGO_USUARIO_ADMIN
     AND CODIGO_EMPRESA = &CODIGO_EMPRESA;

  MERGE INTO OSS_USR_ROL t
  USING (
        SELECT &CODIGO_USUARIO_ADMIN CODIGO_USUARIO,
               r.CODIGO_ROL,
               &CODIGO_EMPRESA CODIGO_EMPRESA
          FROM OSS_ROL r
         WHERE r.CLAVE = 'SOPORTE_ADMIN'
       ) s
     ON (t.CODIGO_USUARIO = s.CODIGO_USUARIO
         AND t.CODIGO_ROL = s.CODIGO_ROL
         AND t.CODIGO_EMPRESA = s.CODIGO_EMPRESA)
   WHEN MATCHED THEN UPDATE SET
        ACTIVO = 1,
        USUARIO_UPD = &USUARIO_ACCION,
        FECHA_UPD = SYSDATE
   WHEN NOT MATCHED THEN INSERT (
        CODIGO_USR_ROL,
        CODIGO_USUARIO,
        CODIGO_ROL,
        CODIGO_EMPRESA,
        ACTIVO,
        USUARIO_INS,
        FECHA_INS
   ) VALUES (
        (SELECT NVL(MAX(CODIGO_USR_ROL), 0) + 1 FROM OSS_USR_ROL),
        s.CODIGO_USUARIO,
        s.CODIGO_ROL,
        s.CODIGO_EMPRESA,
        1,
        &USUARIO_ACCION,
        SYSDATE
   );

  v_json := '[
  {
    "title": "Sistema",
    "icon": "mdi:shield-account-outline",
    "children": [
      {
        "title": "Soporte",
        "children": [
          {
            "title": "Tickets",
            "path": "/apps/soporte/tickets",
            "permissions": [
              "soporte.tickets.crear",
              "soporte.tickets.ver_todos",
              "soporte.tickets.asignar",
              "soporte.tickets.cambiar_estado",
              "soporte.tickets.cerrar",
              "soporte.comentarios.crear",
              "soporte.comentarios.internos"
            ]
          },
          {
            "title": "Dashboard",
            "path": "/apps/soporte/dashboard",
            "permissions": [
              "soporte.dashboard.ver"
            ]
          },
          {
            "title": "Notificaciones",
            "path": "/apps/soporte/notificaciones"
          },
          {
            "title": "Configuracion",
            "path": "/apps/soporte/configuracion",
            "permissions": [
              "soporte.catalogos.admin",
              "soporte.sla.admin",
              "soporte.usuarios.configurar"
            ]
          },
          {
            "title": "Usuarios SIS",
            "path": "/apps/sis/usuarios",
            "permissions": [
              "soporte.usuarios.configurar"
            ]
          },
          {
            "title": "Usuarios-Rol",
            "path": "/apps/sis/usuario-rol",
            "permissions": [
              "soporte.usuarios.configurar"
            ]
          },
          {
            "title": "Seguridad",
            "path": "/apps/sis/seguridad",
            "permissions": [
              "soporte.usuarios.configurar"
            ]
          }
        ]
      }
    ]
  }
]';

  MERGE INTO OSS_USUARIO_ROL t
  USING (
        SELECT &CODIGO_USUARIO_ADMIN CODIGO_USUARIO,
               v_usuario USUARIO,
               'SIS' DESCRIPCION,
               v_json JSON_MENU
          FROM dual
       ) s
     ON (t.CODIGO_USUARIO = s.CODIGO_USUARIO
         AND UPPER(TRIM(t.DESCRIPCION)) = UPPER(TRIM(s.DESCRIPCION)))
   WHEN MATCHED THEN UPDATE SET
        USUARIO = s.USUARIO,
        JSON_MENU = s.JSON_MENU
   WHEN NOT MATCHED THEN INSERT (
        CODIGO_USUARIO_ROL,
        USUARIO,
        CODIGO_USUARIO,
        DESCRIPCION,
        JSON_MENU
   ) VALUES (
        (SELECT NVL(MAX(CODIGO_USUARIO_ROL), 0) + 1 FROM OSS_USUARIO_ROL),
        s.USUARIO,
        s.CODIGO_USUARIO,
        s.DESCRIPCION,
        s.JSON_MENU
   );

  INSERT INTO OSS_SEG_AUD (
      CODIGO_AUD,
      CODIGO_USUARIO,
      CODIGO_EMPRESA,
      ACCION,
      DETALLE,
      USUARIO_ACCION,
      FECHA_ACCION
  ) VALUES (
      (SELECT NVL(MAX(CODIGO_AUD), 0) + 1 FROM OSS_SEG_AUD),
      &CODIGO_USUARIO_ADMIN,
      &CODIGO_EMPRESA,
      'BOOT_SOP_ADMIN',
      'Asignacion inicial SOPORTE_ADMIN y cache JSON_MENU SIS',
      &USUARIO_ACCION,
      SYSDATE
  );
END;
/

COMMIT;

PROMPT Bootstrap administrador Soporte finalizado.
