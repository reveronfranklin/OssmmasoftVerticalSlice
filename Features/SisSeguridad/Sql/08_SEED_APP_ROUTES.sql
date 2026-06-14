PROMPT Cargando seed de rutas NextOssmasoft src/pages/apps

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

  PROCEDURE up_menu(p_id NUMBER, p_mod NUMBER, p_pad NUMBER, p_tit VARCHAR2, p_path VARCHAR2, p_ico VARCHAR2, p_ord NUMBER, p_act NUMBER DEFAULT 1) IS
  BEGIN
    MERGE INTO OSS_MENU t
    USING (SELECT p_id id, p_mod mod_id, p_pad pad, p_tit tit, p_path path, p_ico ico, p_ord ord, p_act act FROM dual) s
       ON (t.CODIGO_MENU = s.id)
     WHEN MATCHED THEN UPDATE SET CODIGO_MOD = s.mod_id, CODIGO_PADRE = s.pad, TITULO = s.tit, PATH = s.path, ICONO = s.ico, ORDEN = s.ord, ACTIVO = s.act, FECHA_UPD = SYSDATE
     WHEN NOT MATCHED THEN INSERT (CODIGO_MENU, CODIGO_MOD, CODIGO_PADRE, TITULO, PATH, ICONO, ORDEN, ACTIVO, FECHA_INS)
       VALUES (s.id, s.mod_id, s.pad, s.tit, s.path, s.ico, s.ord, s.act, SYSDATE);
  END;

  PROCEDURE up_perm(p_id NUMBER, p_mod NUMBER, p_clv VARCHAR2, p_nom VARCHAR2, p_des VARCHAR2) IS
  BEGIN
    MERGE INTO OSS_PERM t
    USING (SELECT p_id id, p_mod mod_id, p_clv clv, p_nom nom, p_des des FROM dual) s
       ON (UPPER(TRIM(t.CLAVE)) = UPPER(TRIM(s.clv)))
     WHEN MATCHED THEN UPDATE SET CODIGO_MOD = s.mod_id, NOMBRE = s.nom, DESCRIPCION = s.des, ACTIVO = 1, FECHA_UPD = SYSDATE
     WHEN NOT MATCHED THEN INSERT (CODIGO_PERM, CODIGO_MOD, CLAVE, NOMBRE, DESCRIPCION, ACTIVO, FECHA_INS)
       VALUES (s.id, s.mod_id, s.clv, s.nom, s.des, 1, SYSDATE);
  END;

  PROCEDURE up_rol(p_id NUMBER, p_mod NUMBER, p_clv VARCHAR2, p_nom VARCHAR2, p_des VARCHAR2) IS
  BEGIN
    MERGE INTO OSS_ROL t
    USING (SELECT p_id id, p_mod mod_id, p_clv clv, p_nom nom, p_des des FROM dual) s
       ON (UPPER(TRIM(t.CLAVE)) = UPPER(TRIM(s.clv)))
     WHEN MATCHED THEN UPDATE SET CODIGO_MOD = s.mod_id, NOMBRE = s.nom, DESCRIPCION = s.des, ACTIVO = 1, FECHA_UPD = SYSDATE
     WHEN NOT MATCHED THEN INSERT (CODIGO_ROL, CODIGO_MOD, CLAVE, NOMBRE, DESCRIPCION, ACTIVO, FECHA_INS)
       VALUES (s.id, s.mod_id, s.clv, s.nom, s.des, 1, SYSDATE);
  END;

  PROCEDURE add_mp(p_menu NUMBER, p_perm NUMBER) IS
  BEGIN
    MERGE INTO OSS_MENU_PERM t
    USING (SELECT p_menu menu_id, p_perm perm_id FROM dual) s
       ON (t.CODIGO_MENU = s.menu_id AND t.CODIGO_PERM = s.perm_id)
     WHEN NOT MATCHED THEN INSERT (CODIGO_MENU, CODIGO_PERM, FECHA_INS) VALUES (s.menu_id, s.perm_id, SYSDATE);
  END;

  PROCEDURE add_mp_key(p_menu NUMBER, p_perm VARCHAR2) IS
  BEGIN
    MERGE INTO OSS_MENU_PERM t
    USING (
          SELECT p_menu menu_id, p.CODIGO_PERM perm_id
            FROM OSS_PERM p
           WHERE UPPER(TRIM(p.CLAVE)) = UPPER(TRIM(p_perm))
          ) s
       ON (t.CODIGO_MENU = s.menu_id AND t.CODIGO_PERM = s.perm_id)
     WHEN NOT MATCHED THEN INSERT (CODIGO_MENU, CODIGO_PERM, FECHA_INS) VALUES (s.menu_id, s.perm_id, SYSDATE);
  END;

  PROCEDURE add_rm(p_rol NUMBER, p_menu NUMBER) IS
  BEGIN
    MERGE INTO OSS_ROL_MENU t
    USING (SELECT p_rol rol_id, p_menu menu_id FROM dual) s
       ON (t.CODIGO_ROL = s.rol_id AND t.CODIGO_MENU = s.menu_id)
     WHEN NOT MATCHED THEN INSERT (CODIGO_ROL, CODIGO_MENU, FECHA_INS) VALUES (s.rol_id, s.menu_id, SYSDATE);
  END;

  PROCEDURE add_rp(p_rol NUMBER, p_perm NUMBER) IS
  BEGIN
    MERGE INTO OSS_ROL_PERM t
    USING (SELECT p_rol rol_id, p_perm perm_id FROM dual) s
       ON (t.CODIGO_ROL = s.rol_id AND t.CODIGO_PERM = s.perm_id)
     WHEN NOT MATCHED THEN INSERT (CODIGO_ROL, CODIGO_PERM, FECHA_INS) VALUES (s.rol_id, s.perm_id, SYSDATE);
  END;

  PROCEDURE add_rp_key(p_rol VARCHAR2, p_perm VARCHAR2) IS
  BEGIN
    MERGE INTO OSS_ROL_PERM t
    USING (
          SELECT r.CODIGO_ROL rol_id, p.CODIGO_PERM perm_id
            FROM OSS_ROL r,
                 OSS_PERM p
           WHERE UPPER(TRIM(r.CLAVE)) = UPPER(TRIM(p_rol))
             AND UPPER(TRIM(p.CLAVE)) = UPPER(TRIM(p_perm))
          ) s
       ON (t.CODIGO_ROL = s.rol_id AND t.CODIGO_PERM = s.perm_id)
     WHEN NOT MATCHED THEN INSERT (CODIGO_ROL, CODIGO_PERM, FECHA_INS) VALUES (s.rol_id, s.perm_id, SYSDATE);
  END;

  PROCEDURE grant_active_range(p_rol NUMBER, p_ini NUMBER, p_fin NUMBER) IS
  BEGIN
    FOR r IN (SELECT CODIGO_MENU FROM OSS_MENU WHERE CODIGO_MENU BETWEEN p_ini AND p_fin AND ACTIVO = 1) LOOP
      add_rm(p_rol, r.CODIGO_MENU);
    END LOOP;
  END;

  PROCEDURE grant_active_module(p_rol NUMBER, p_mod NUMBER) IS
  BEGIN
    FOR r IN (SELECT CODIGO_MENU FROM OSS_MENU WHERE CODIGO_MOD = p_mod AND ACTIVO = 1) LOOP
      add_rm(p_rol, r.CODIGO_MENU);
    END LOOP;
  END;

  PROCEDURE grant_active_mod_key(p_rol VARCHAR2, p_mod NUMBER) IS
    v_rol NUMBER;
  BEGIN
    SELECT CODIGO_ROL
      INTO v_rol
      FROM OSS_ROL
     WHERE UPPER(TRIM(CLAVE)) = UPPER(TRIM(p_rol));

    FOR r IN (SELECT CODIGO_MENU FROM OSS_MENU WHERE CODIGO_MOD = p_mod AND ACTIVO = 1) LOOP
      add_rm(v_rol, r.CODIGO_MENU);
    END LOOP;
  END;
BEGIN
  up_mod(1, 'SIS', 'Sistema', 'mdi:shield-account-outline', 10);
  up_mod(2, 'SOP', 'Soporte', 'mdi:lifebuoy', 20);
  up_mod(3, 'CNT', 'Contabilidad', 'mdi:calculator-variant-outline', 30);
  up_mod(4, 'RH', 'Nomina', 'mdi:account-group-outline', 40);
  up_mod(5, 'PRE', 'Presupuesto', 'mdi:file-document-outline', 50);
  up_mod(6, 'ADM', 'Administracion', 'mdi:office-building-outline', 60);
  up_mod(7, 'BM', 'Bienes Muebles', 'mdi:archive-outline', 70);
  up_mod(8, 'APP', 'Aplicaciones', 'mdi:apps', 80);

  up_perm(9010, 1, 'sis.menu.ver', 'Ver sistema', 'Consultar menus del modulo sistema.');
  up_perm(9020, 2, 'soporte.menu.ver', 'Ver soporte', 'Consultar menus del modulo soporte.');
  up_perm(9030, 3, 'cnt.menu.ver', 'Ver contabilidad', 'Consultar menus del modulo contabilidad.');
  up_perm(9040, 4, 'rh.menu.ver', 'Ver nomina', 'Consultar menus del modulo nomina.');
  up_perm(9050, 5, 'pre.menu.ver', 'Ver presupuesto', 'Consultar menus del modulo presupuesto.');
  up_perm(9060, 6, 'adm.menu.ver', 'Ver administracion', 'Consultar menus del modulo administracion.');
  up_perm(9070, 7, 'bm.menu.ver', 'Ver bienes muebles', 'Consultar modulo bienes muebles.');
  up_perm(9080, 8, 'apps.menu.ver', 'Ver aplicaciones', 'Consultar rutas generales de aplicaciones.');
  up_rol(9010, 1, 'SIS_MENU', 'Menu Sistema', 'Acceso generico al menu del modulo sistema.');
  up_rol(9020, 2, 'SOP_MENU', 'Menu Soporte', 'Acceso generico al menu del modulo soporte.');
  up_rol(9030, 3, 'CNT_MENU', 'Menu Contabilidad', 'Acceso generico al menu del modulo contabilidad.');
  up_rol(9040, 4, 'RH_MENU', 'Menu Nomina', 'Acceso generico al menu del modulo nomina.');
  up_rol(9050, 5, 'PRE_MENU', 'Menu Presupuesto', 'Acceso generico al menu del modulo presupuesto.');
  up_rol(9060, 6, 'ADM_MENU', 'Menu Administracion', 'Acceso generico al menu del modulo administracion.');
  up_rol(9070, 7, 'BM_MENU', 'Menu Bienes Muebles', 'Acceso generico al menu del modulo bienes muebles.');
  up_rol(9080, 8, 'APP_MENU', 'Menu Aplicaciones', 'Acceso generico al menu de aplicaciones generales.');
  up_rol(700, 7, 'BM_USUARIO', 'Usuario Bienes Muebles', 'Usuario del modulo bienes muebles.');
  up_rol(800, 8, 'APP_USUARIO', 'Usuario Aplicaciones', 'Usuario de rutas generales de aplicaciones.');
  add_rp_key('SIS_MENU', 'sis.menu.ver');
  add_rp_key('SOP_MENU', 'soporte.menu.ver');
  add_rp_key('CNT_MENU', 'cnt.menu.ver');
  add_rp_key('RH_MENU', 'rh.menu.ver');
  add_rp_key('PRE_MENU', 'pre.menu.ver');
  add_rp_key('ADM_MENU', 'adm.menu.ver');
  add_rp_key('BM_MENU', 'bm.menu.ver');
  add_rp_key('APP_MENU', 'apps.menu.ver');
  add_rp_key('BM_USUARIO', 'bm.menu.ver');
  add_rp_key('APP_USUARIO', 'apps.menu.ver');

  -- SIS y Soporte
  up_menu(1000, 1, NULL, 'Sistema', NULL, 'mdi:shield-account-outline', 10);
  up_menu(1150, 1, 1000, 'Usuarios', '/apps/sis/usuarios', NULL, 10);
  up_menu(1160, 1, 1000, 'Roles Usuario', '/apps/sis/usuario-rol', NULL, 20);
  up_menu(1170, 1, 1000, 'Seguridad', '/apps/sis/seguridad', NULL, 30);
  up_menu(1100, 2, 1000, 'Soporte', NULL, NULL, 40);
  up_menu(1180, 2, 1100, 'Inicio', '/apps/soporte', NULL, 5);
  up_menu(1110, 2, 1100, 'Tickets', '/apps/soporte/tickets', NULL, 10);
  up_menu(1111, 2, 1110, 'Nuevo ticket', '/apps/soporte/tickets/nuevo', NULL, 11, 0);
  up_menu(1112, 2, 1110, 'Detalle ticket', '/apps/soporte/tickets/[id]', NULL, 12, 0);
  up_menu(1120, 2, 1100, 'Dashboard', '/apps/soporte/dashboard', NULL, 20);
  up_menu(1130, 2, 1100, 'Notificaciones', '/apps/soporte/notificaciones', NULL, 30);
  up_menu(1140, 2, 1100, 'Configuracion', '/apps/soporte/configuracion', NULL, 40);

  -- CNT
  up_menu(3000, 3, NULL, 'Contabilidad', NULL, 'mdi:calculator-variant-outline', 10);
  up_menu(3010, 3, 3000, 'Comprobantes', '/apps/cnt/comprobantes', NULL, 10);
  up_menu(3011, 3, 3010, 'Nuevo comprobante', '/apps/cnt/comprobantes/nuevo', NULL, 11, 0);
  up_menu(3012, 3, 3010, 'Detalle comprobante', '/apps/cnt/comprobantes/[id]', NULL, 12, 0);
  up_menu(3013, 3, 3010, 'Imprimir comprobante', '/apps/cnt/comprobantes/imprimir/[id]', NULL, 13, 0);
  up_menu(3014, 3, 3010, 'PDF comprobante', '/apps/cnt/comprobantes/pdf/[id]', NULL, 14, 0);
  up_menu(3020, 3, 3000, 'Proceso Automatico', '/apps/cnt/proceso-automatico', NULL, 20);
  up_menu(3030, 3, 3000, 'Procesos', NULL, NULL, 30);
  up_menu(3031, 3, 3030, 'Cierre contable', '/apps/cnt/procesos/cierre-contable', NULL, 10);
  up_menu(3040, 3, 3000, 'Conciliacion', NULL, NULL, 40);
  up_menu(3041, 3, 3040, 'Conciliaciones', '/apps/cnt/conciliacion', NULL, 10);
  up_menu(3047, 3, 3041, 'Detalle conciliacion', '/apps/cnt/conciliacion/[id]', NULL, 11, 0);
  up_menu(3042, 3, 3040, 'Importar estados de cuenta', '/apps/cnt/conciliacion/carga-banco', NULL, 20);
  up_menu(3043, 3, 3040, 'Estados de cuenta', '/apps/cnt/conciliacion/estados-cuenta', NULL, 30);
  up_menu(3044, 3, 3040, 'Libro banco', '/apps/cnt/conciliacion/libro-banco', NULL, 40);
  up_menu(3045, 3, 3040, 'Configuracion conciliacion', '/apps/cnt/conciliacion/configuracion', NULL, 50);
  up_menu(3046, 3, 3040, 'Formatos banco', '/apps/cnt/conciliacion/formatos-banco', NULL, 60);
  up_menu(3050, 3, 3000, 'Reportes', NULL, NULL, 50);
  up_menu(3051, 3, 3050, 'Mayor Analitico', '/apps/cnt/reportes/mayor-analitico', NULL, 10);
  up_menu(3052, 3, 3050, 'Movimiento Auxiliar', '/apps/cnt/reportes/movimiento-auxiliar', NULL, 20);
  up_menu(3060, 3, 3000, 'Catalogos', NULL, NULL, 60);
  up_menu(3061, 3, 3060, 'Plan de cuentas', '/apps/cnt/catalogos/plan-cuentas', NULL, 10);
  up_menu(3062, 3, 3060, 'Descriptivas', '/apps/cnt/catalogos/descriptivas', NULL, 20);
  up_menu(3063, 3, 3060, 'Rubros', '/apps/cnt/catalogos/rubros', NULL, 30);
  up_menu(3064, 3, 3060, 'Balances', '/apps/cnt/catalogos/balances', NULL, 40);
  up_menu(3065, 3, 3060, 'Mayores', '/apps/cnt/catalogos/mayores', NULL, 50);
  up_menu(3066, 3, 3060, 'Auxiliares', '/apps/cnt/catalogos/auxiliares', NULL, 60);
  up_menu(3067, 3, 3060, 'Auxiliares PUC', '/apps/cnt/catalogos/auxiliares-puc', NULL, 70);
  up_menu(3068, 3, 3060, 'Periodos', '/apps/cnt/catalogos/periodos', NULL, 80);
  up_menu(3069, 3, 3060, 'Relacion documentos', '/apps/cnt/catalogos/relacion-documentos', NULL, 90);
  up_menu(3070, 3, 3060, 'Saldos', '/apps/cnt/catalogos/saldos', NULL, 100);
  up_menu(3080, 3, 3000, 'Configuracion', '/apps/cnt/configuracion', NULL, 70);

  -- RH
  up_menu(4000, 4, NULL, 'Nomina', NULL, 'mdi:file-document-outline', 10);
  up_menu(4010, 4, 4000, 'Historico Individual', '/apps/rh/individual', NULL, 10);
  up_menu(4020, 4, 4000, 'Historico Masivo', '/apps/rh/masivo', NULL, 20);
  up_menu(4030, 4, 4000, 'Historico Proceso', '/apps/rh/proceso', NULL, 30);
  up_menu(4074, 4, 4000, 'Procesos Nomina', '/apps/rh/procesos', NULL, 35);
  up_menu(4040, 4, 4000, 'Recibos', '/apps/rh/recibos', NULL, 40);
  up_menu(4140, 4, 4000, 'Pagos', '/apps/rh/pagos', NULL, 45);
  up_menu(4070, 4, 4000, 'Maestros', NULL, 'mdi:account-outline', 70);
  up_menu(4071, 4, 4070, 'Tipo Nomina', '/apps/rh/tipoNomina', NULL, 10);
  up_menu(4072, 4, 4070, 'Conceptos', '/apps/rh/conceptos', NULL, 20);
  up_menu(4073, 4, 4070, 'Periodos Nomina', '/apps/rh/periodos', NULL, 30);
  up_menu(4130, 4, 4070, 'Formulas', '/apps/rh/formulacion/formulas', NULL, 40);
  up_menu(4131, 4, 4070, 'Plantillas', '/apps/rh/formulacion/plantillas', NULL, 50);
  up_menu(4080, 4, 4000, 'Personas', NULL, 'mdi:account-outline', 80);
  up_menu(4081, 4, 4080, 'Lista', '/apps/rh/persona/list', NULL, 10);
  up_menu(4082, 4, 4080, 'Beneficiarios', '/apps/rh/beneficiarios', NULL, 20);
  up_menu(4083, 4, 4080, 'Ficha', '/apps/rh/persona/view/[tab]', NULL, 30, 0);
  up_menu(4090, 4, 4000, 'Ret./Aportes', NULL, 'mdi:account-outline', 90);
  up_menu(4091, 4, 4090, 'CAH', '/apps/rh/retenciones/cah', NULL, 10);
  up_menu(4092, 4, 4090, 'FAOV', '/apps/rh/retenciones/faov', NULL, 20);
  up_menu(4093, 4, 4090, 'FJP', '/apps/rh/retenciones/fjp', NULL, 30);
  up_menu(4094, 4, 4090, 'INCES', '/apps/rh/retenciones/ince', NULL, 40);
  up_menu(4095, 4, 4090, 'SIND', '/apps/rh/retenciones/sind', NULL, 50);
  up_menu(4096, 4, 4090, 'SSO', '/apps/rh/retenciones/sso', NULL, 60);
  up_menu(4120, 4, 4000, 'Variaciones masivas', '/apps/rh/variaciones/variaciones_masivas', NULL, 100);

  -- PRE
  up_menu(5000, 5, NULL, 'Presupuesto', NULL, 'mdi:file-document-outline', 10);
  up_menu(5020, 5, 5000, 'Saldo Presupuesto', '/apps/presupuesto/prevsaldo', NULL, 20);
  up_menu(5030, 5, 5000, 'Creditos Presupuestarios', '/apps/presupuesto/asignaciones', NULL, 30);
  up_menu(5040, 5, 5000, 'Compromiso Presupuestario', '/apps/presupuesto/compromiso', NULL, 40);
  up_menu(5050, 5, 5000, 'Solicitud Modificacion', '/apps/presupuesto/solicitudModificacion', NULL, 50);
  up_menu(5060, 5, 5000, 'Maestro Presupuesto', '/apps/presupuesto/maestro/list', NULL, 60);
  up_menu(5070, 5, 5000, 'Maestro ICP', '/apps/presupuesto/icp/list', NULL, 70);
  up_menu(5080, 5, 5000, 'Maestro PUC', '/apps/presupuesto/puc/list', NULL, 80);
  up_menu(5090, 5, 5000, 'Relacion Cargo', '/apps/presupuesto/relacionCargo', NULL, 90);
  up_menu(5100, 5, 5000, 'Cargos', '/apps/presupuesto/cargo', NULL, 100);
  up_menu(5110, 5, 5000, 'Titulos', '/apps/presupuesto/titulo', NULL, 110);
  up_menu(5120, 5, 5000, 'Descriptivas', '/apps/presupuesto/descriptiva', NULL, 120);

  -- ADM y Tesoreria
  up_menu(6000, 6, NULL, 'Administracion', NULL, 'mdi:file-document-outline', 10);
  up_menu(6010, 6, 6000, 'Solicitud de compromiso', '/apps/adm/solicitudCompromiso', NULL, 10);
  up_menu(6020, 6, 6000, 'Pre-Orden Pago', '/apps/adm/preOrdenPago', NULL, 20);
  up_menu(6030, 6, 6000, 'Ordenes Pago', '/apps/adm/ordenPago', NULL, 30);
  up_menu(6040, 6, 6000, 'Proveedores', NULL, 'mdi:account-outline', 40);
  up_menu(6042, 6, 6040, 'Lista', '/apps/adm/proveedor/list', NULL, 10);
  up_menu(6043, 6, 6040, 'Ficha proveedor', '/apps/adm/proveedor/view/[tab]', NULL, 20, 0);
  up_menu(6100, 6, NULL, 'Tesoreria', NULL, 'mdi:file-document-outline', 20);
  up_menu(6110, 6, 6100, 'Bancos', '/apps/adm/pagos/bancos', NULL, 10);
  up_menu(6120, 6, 6100, 'Cuentas', '/apps/adm/pagos/cuentas', NULL, 20);
  up_menu(6130, 6, 6100, 'Lotes', '/apps/adm/pagos/lotes', NULL, 30);

  -- Bienes Muebles
  up_menu(7000, 7, NULL, 'Bienes Muebles', NULL, 'mdi:archive-outline', 10);
  up_menu(7010, 7, 7000, 'BM1', '/apps/Bm/Bm1', NULL, 10);
  up_menu(7020, 7, 7000, 'Contar', '/apps/Bm/BmContar', NULL, 20);
  up_menu(7030, 7, 7000, 'Conteo', '/apps/Bm/BmConteo', NULL, 30);
  up_menu(7040, 7, 7000, 'Conteo Detalle', '/apps/Bm/BmConteoDetalle', NULL, 40);
  up_menu(7050, 7, 7000, 'Comparar Conteo', '/apps/Bm/BmConteoDetalleCompare', NULL, 50);
  up_menu(7060, 7, 7000, 'Historico Conteo', '/apps/Bm/BmConteoHistorico', NULL, 60);
  up_menu(7070, 7, 7000, 'Placas Cuarentena', '/apps/Bm/BmPlacasCuarentena', NULL, 70);

  -- Aplicaciones generales y rutas de plantilla
  up_menu(8000, 8, NULL, 'Aplicaciones', NULL, 'mdi:apps', 10);
  up_menu(8010, 8, 8000, 'Calendario', '/apps/calendar', NULL, 10);
  up_menu(8020, 8, 8000, 'Chat', '/apps/chat', NULL, 20);
  up_menu(8030, 8, 8000, 'Email', '/apps/email', NULL, 30);
  up_menu(8031, 8, 8030, 'Email folder', '/apps/email/[folder]', NULL, 31, 0);
  up_menu(8032, 8, 8030, 'Email label', '/apps/email/label/[label]', NULL, 32, 0);
  up_menu(8040, 8, 8000, 'Facturas', '/apps/invoice/list', NULL, 40);
  up_menu(8041, 8, 8040, 'Agregar factura', '/apps/invoice/add', NULL, 41, 0);
  up_menu(8042, 8, 8040, 'Editar factura', '/apps/invoice/edit', NULL, 42, 0);
  up_menu(8043, 8, 8040, 'Editar factura id', '/apps/invoice/edit/[id]', NULL, 43, 0);
  up_menu(8044, 8, 8040, 'Vista factura', '/apps/invoice/preview', NULL, 44, 0);
  up_menu(8045, 8, 8040, 'Vista factura id', '/apps/invoice/preview/[id]', NULL, 45, 0);
  up_menu(8046, 8, 8040, 'Imprimir factura', '/apps/invoice/print', NULL, 46, 0);
  up_menu(8047, 8, 8040, 'Imprimir factura id', '/apps/invoice/print/[id]', NULL, 47, 0);
  up_menu(8050, 8, 8000, 'Modelos', '/apps/models', NULL, 50);
  up_menu(8060, 8, 8000, 'Permisos', '/apps/permissions', NULL, 60);
  up_menu(8070, 8, 8000, 'Roles', '/apps/roles', NULL, 70);
  up_menu(8080, 8, 8000, 'Usuarios plantilla', '/apps/user/list', NULL, 80);
  up_menu(8081, 8, 8080, 'Ficha usuario plantilla', '/apps/user/view/[tab]', NULL, 81, 0);

  -- Permisos visibles por modulo. Las rutas tecnicas quedan catalogadas pero inactivas.
  add_mp_key(1000, 'sis.menu.ver'); add_mp_key(1150, 'sis.menu.ver'); add_mp_key(1160, 'sis.menu.ver'); add_mp_key(1170, 'sis.menu.ver');
  add_mp_key(1100, 'soporte.menu.ver'); add_mp_key(1180, 'soporte.menu.ver'); add_mp_key(1110, 'soporte.menu.ver'); add_mp_key(1120, 'soporte.menu.ver'); add_mp_key(1130, 'soporte.menu.ver'); add_mp_key(1140, 'soporte.menu.ver');
  add_mp_key(7000, 'bm.menu.ver'); add_mp_key(7010, 'bm.menu.ver'); add_mp_key(7020, 'bm.menu.ver'); add_mp_key(7030, 'bm.menu.ver'); add_mp_key(7040, 'bm.menu.ver'); add_mp_key(7050, 'bm.menu.ver'); add_mp_key(7060, 'bm.menu.ver'); add_mp_key(7070, 'bm.menu.ver');
  add_mp_key(8000, 'apps.menu.ver'); add_mp_key(8010, 'apps.menu.ver'); add_mp_key(8020, 'apps.menu.ver'); add_mp_key(8030, 'apps.menu.ver'); add_mp_key(8040, 'apps.menu.ver'); add_mp_key(8050, 'apps.menu.ver'); add_mp_key(8060, 'apps.menu.ver'); add_mp_key(8070, 'apps.menu.ver'); add_mp_key(8080, 'apps.menu.ver');

  grant_active_mod_key('SIS_MENU', 1);
  grant_active_mod_key('SOP_MENU', 2);
  grant_active_mod_key('CNT_MENU', 3);
  grant_active_mod_key('RH_MENU', 4);
  grant_active_mod_key('PRE_MENU', 5);
  grant_active_mod_key('ADM_MENU', 6);
  grant_active_mod_key('BM_MENU', 7);
  grant_active_mod_key('APP_MENU', 8);
  grant_active_range(700, 7000, 7099);
  grant_active_range(800, 8000, 8099);
  grant_active_range(102, 1000, 1180);
  grant_active_range(302, 3000, 3080);
  grant_active_range(400, 4000, 4140);
  grant_active_range(500, 5000, 5120);
  grant_active_range(600, 6000, 6130);
END;
/

COMMIT;

PROMPT Seed de rutas NextOssmasoft listo
