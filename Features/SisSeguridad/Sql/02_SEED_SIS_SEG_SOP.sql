PROMPT Cargando seed SIS Seguridad Soporte

DECLARE
  PROCEDURE up_mod(p_id NUMBER, p_cod VARCHAR2, p_nom VARCHAR2, p_ico VARCHAR2, p_ord NUMBER) IS
  BEGIN
    MERGE INTO OSS_MOD t
    USING (SELECT p_id id, p_cod cod, p_nom nom, p_ico ico, p_ord ord FROM dual) s
       ON (t.CODIGO_MOD = s.id)
     WHEN MATCHED THEN UPDATE SET CODIGO = s.cod, NOMBRE = s.nom, ICONO = s.ico, ORDEN = s.ord, ACTIVO = 1, FECHA_UPD = SYSDATE
     WHEN NOT MATCHED THEN INSERT (CODIGO_MOD, CODIGO, NOMBRE, ICONO, ORDEN, ACTIVO, FECHA_INS)
       VALUES (s.id, s.cod, s.nom, s.ico, s.ord, 1, SYSDATE);
  END;

  PROCEDURE up_menu(p_id NUMBER, p_mod NUMBER, p_pad NUMBER, p_tit VARCHAR2, p_path VARCHAR2, p_ico VARCHAR2, p_ord NUMBER) IS
  BEGIN
    MERGE INTO OSS_MENU t
    USING (SELECT p_id id, p_mod mod_id, p_pad pad, p_tit tit, p_path path, p_ico ico, p_ord ord FROM dual) s
       ON (t.CODIGO_MENU = s.id)
     WHEN MATCHED THEN UPDATE SET CODIGO_MOD = s.mod_id, CODIGO_PADRE = s.pad, TITULO = s.tit, PATH = s.path, ICONO = s.ico, ORDEN = s.ord, ACTIVO = 1, FECHA_UPD = SYSDATE
     WHEN NOT MATCHED THEN INSERT (CODIGO_MENU, CODIGO_MOD, CODIGO_PADRE, TITULO, PATH, ICONO, ORDEN, ACTIVO, FECHA_INS)
       VALUES (s.id, s.mod_id, s.pad, s.tit, s.path, s.ico, s.ord, 1, SYSDATE);
  END;

  PROCEDURE up_perm(p_id NUMBER, p_mod NUMBER, p_clv VARCHAR2, p_nom VARCHAR2, p_des VARCHAR2) IS
  BEGIN
    MERGE INTO OSS_PERM t
    USING (SELECT p_id id, p_mod mod_id, p_clv clv, p_nom nom, p_des des FROM dual) s
       ON (t.CODIGO_PERM = s.id)
     WHEN MATCHED THEN UPDATE SET CODIGO_MOD = s.mod_id, CLAVE = s.clv, NOMBRE = s.nom, DESCRIPCION = s.des, ACTIVO = 1, FECHA_UPD = SYSDATE
     WHEN NOT MATCHED THEN INSERT (CODIGO_PERM, CODIGO_MOD, CLAVE, NOMBRE, DESCRIPCION, ACTIVO, FECHA_INS)
       VALUES (s.id, s.mod_id, s.clv, s.nom, s.des, 1, SYSDATE);
  END;

  PROCEDURE up_rol(p_id NUMBER, p_mod NUMBER, p_clv VARCHAR2, p_nom VARCHAR2, p_des VARCHAR2) IS
  BEGIN
    MERGE INTO OSS_ROL t
    USING (SELECT p_id id, p_mod mod_id, p_clv clv, p_nom nom, p_des des FROM dual) s
       ON (t.CODIGO_ROL = s.id)
     WHEN MATCHED THEN UPDATE SET CODIGO_MOD = s.mod_id, CLAVE = s.clv, NOMBRE = s.nom, DESCRIPCION = s.des, ACTIVO = 1, FECHA_UPD = SYSDATE
     WHEN NOT MATCHED THEN INSERT (CODIGO_ROL, CODIGO_MOD, CLAVE, NOMBRE, DESCRIPCION, ACTIVO, FECHA_INS)
       VALUES (s.id, s.mod_id, s.clv, s.nom, s.des, 1, SYSDATE);
  END;

  PROCEDURE add_menu_perm(p_menu NUMBER, p_perm NUMBER) IS
  BEGIN
    MERGE INTO OSS_MENU_PERM t
    USING (SELECT p_menu menu_id, p_perm perm_id FROM dual) s
       ON (t.CODIGO_MENU = s.menu_id AND t.CODIGO_PERM = s.perm_id)
     WHEN NOT MATCHED THEN INSERT (CODIGO_MENU, CODIGO_PERM, FECHA_INS) VALUES (s.menu_id, s.perm_id, SYSDATE);
  END;

  PROCEDURE add_rol_menu(p_rol NUMBER, p_menu NUMBER) IS
  BEGIN
    MERGE INTO OSS_ROL_MENU t
    USING (SELECT p_rol rol_id, p_menu menu_id FROM dual) s
       ON (t.CODIGO_ROL = s.rol_id AND t.CODIGO_MENU = s.menu_id)
     WHEN NOT MATCHED THEN INSERT (CODIGO_ROL, CODIGO_MENU, FECHA_INS) VALUES (s.rol_id, s.menu_id, SYSDATE);
  END;

  PROCEDURE add_rol_perm(p_rol NUMBER, p_perm NUMBER) IS
  BEGIN
    MERGE INTO OSS_ROL_PERM t
    USING (SELECT p_rol rol_id, p_perm perm_id FROM dual) s
       ON (t.CODIGO_ROL = s.rol_id AND t.CODIGO_PERM = s.perm_id)
     WHEN NOT MATCHED THEN INSERT (CODIGO_ROL, CODIGO_PERM, FECHA_INS) VALUES (s.rol_id, s.perm_id, SYSDATE);
  END;
BEGIN
  up_mod(1, 'SIS', 'Sistema', 'mdi:shield-account-outline', 10);
  up_mod(2, 'SOP', 'Soporte', 'mdi:lifebuoy', 20);
  up_mod(3, 'CNT', 'Contabilidad', 'mdi:calculator-variant-outline', 30);
  up_mod(4, 'RH', 'Nomina', 'mdi:account-group-outline', 40);
  up_mod(5, 'PRE', 'Presupuesto', 'mdi:file-document-outline', 50);
  up_mod(6, 'ADM', 'Administracion', 'mdi:office-building-outline', 60);

  up_menu(1000, 1, NULL, 'Sistema', NULL, 'mdi:shield-account-outline', 10);
  up_menu(1100, 2, 1000, 'Soporte', NULL, NULL, 10);
  up_menu(1110, 2, 1100, 'Tickets', '/apps/soporte/tickets', NULL, 10);
  up_menu(1120, 2, 1100, 'Dashboard', '/apps/soporte/dashboard', NULL, 20);
  up_menu(1130, 2, 1100, 'Notificaciones', '/apps/soporte/notificaciones', NULL, 30);
  up_menu(1140, 2, 1100, 'Configuracion', '/apps/soporte/configuracion', NULL, 40);
  up_menu(1150, 1, 1100, 'Usuarios SIS', '/apps/sis/usuarios', NULL, 50);
  up_menu(1160, 1, 1100, 'Usuarios-Rol', '/apps/sis/usuario-rol', NULL, 60);
  up_menu(1170, 1, 1100, 'Seguridad', '/apps/sis/seguridad', NULL, 70);

  up_perm(100, 2, 'soporte.tickets.crear', 'Crear tickets', 'Crear solicitudes de soporte.');
  up_perm(101, 2, 'soporte.tickets.ver_propios', 'Ver propios', 'Consultar tickets propios.');
  up_perm(102, 2, 'soporte.tickets.ver_asignados', 'Ver asignados', 'Consultar tickets asignados.');
  up_perm(103, 2, 'soporte.tickets.ver_sin_asignar', 'Ver sin asignar', 'Consultar tickets sin responsable.');
  up_perm(104, 2, 'soporte.tickets.ver_todos', 'Ver todos', 'Consultar todos los tickets.');
  up_perm(105, 2, 'soporte.tickets.asignar', 'Asignar tickets', 'Asignar responsable a tickets.');
  up_perm(106, 2, 'soporte.tickets.cambiar_estado', 'Cambiar estado', 'Cambiar estado de tickets.');
  up_perm(107, 2, 'soporte.tickets.cerrar', 'Cerrar tickets', 'Cerrar tickets resueltos.');
  up_perm(108, 2, 'soporte.comentarios.crear', 'Crear comentarios', 'Agregar comentarios a tickets.');
  up_perm(109, 2, 'soporte.comentarios.internos', 'Comentarios internos', 'Crear y ver comentarios internos.');
  up_perm(110, 2, 'soporte.catalogos.admin', 'Administrar catalogos', 'Administrar catalogos de soporte.');
  up_perm(111, 2, 'soporte.sla.admin', 'Administrar SLA', 'Administrar reglas SLA.');
  up_perm(112, 2, 'soporte.dashboard.ver', 'Ver dashboard', 'Consultar dashboard de soporte.');
  up_perm(113, 2, 'soporte.usuarios.configurar', 'Configurar usuarios', 'Configurar usuarios y seguridad.');

  add_menu_perm(1110, 100);
  add_menu_perm(1110, 101);
  add_menu_perm(1110, 102);
  add_menu_perm(1110, 103);
  add_menu_perm(1110, 104);
  add_menu_perm(1110, 105);
  add_menu_perm(1110, 106);
  add_menu_perm(1110, 107);
  add_menu_perm(1110, 108);
  add_menu_perm(1110, 109);
  add_menu_perm(1120, 112);
  add_menu_perm(1140, 110);
  add_menu_perm(1140, 111);
  add_menu_perm(1140, 113);
  add_menu_perm(1150, 113);
  add_menu_perm(1160, 113);
  add_menu_perm(1170, 113);

  up_rol(100, 2, 'SOPORTE_USUARIO', 'Usuario de Soporte', 'Usuario que crea y consulta sus tickets.');
  up_rol(101, 2, 'SOPORTE_AGENTE', 'Analista de Soporte', 'Analista que atiende tickets.');
  up_rol(102, 2, 'SOPORTE_ADMIN', 'Administrador de Soporte', 'Administrador completo de soporte.');

  add_rol_menu(100, 1000);
  add_rol_menu(100, 1100);
  add_rol_menu(100, 1110);
  add_rol_menu(100, 1130);
  add_rol_perm(100, 100);
  add_rol_perm(100, 101);
  add_rol_perm(100, 108);

  add_rol_menu(101, 1000);
  add_rol_menu(101, 1100);
  add_rol_menu(101, 1110);
  add_rol_menu(101, 1120);
  add_rol_menu(101, 1130);
  add_rol_perm(101, 100);
  add_rol_perm(101, 101);
  add_rol_perm(101, 102);
  add_rol_perm(101, 103);
  add_rol_perm(101, 105);
  add_rol_perm(101, 106);
  add_rol_perm(101, 108);
  add_rol_perm(101, 109);
  add_rol_perm(101, 112);

  add_rol_menu(102, 1000);
  add_rol_menu(102, 1100);
  add_rol_menu(102, 1110);
  add_rol_menu(102, 1120);
  add_rol_menu(102, 1130);
  add_rol_menu(102, 1140);
  add_rol_menu(102, 1150);
  add_rol_menu(102, 1160);
  add_rol_menu(102, 1170);
  add_rol_perm(102, 100);
  add_rol_perm(102, 104);
  add_rol_perm(102, 105);
  add_rol_perm(102, 106);
  add_rol_perm(102, 107);
  add_rol_perm(102, 108);
  add_rol_perm(102, 109);
  add_rol_perm(102, 110);
  add_rol_perm(102, 111);
  add_rol_perm(102, 112);
  add_rol_perm(102, 113);
END;
/

COMMIT;

PROMPT Seed SIS Seguridad Soporte listo
