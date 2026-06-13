PROMPT Cargando seed SIS Seguridad modulos legacy

DECLARE
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

  PROCEDURE add_mp(p_menu NUMBER, p_perm NUMBER) IS
  BEGIN
    MERGE INTO OSS_MENU_PERM t
    USING (SELECT p_menu menu_id, p_perm perm_id FROM dual) s
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

  PROCEDURE grant_menu_range(p_rol NUMBER, p_ini NUMBER, p_fin NUMBER) IS
  BEGIN
    FOR r IN (SELECT CODIGO_MENU FROM OSS_MENU WHERE CODIGO_MENU BETWEEN p_ini AND p_fin) LOOP
      add_rm(p_rol, r.CODIGO_MENU);
    END LOOP;
  END;

  PROCEDURE grant_perm_range(p_rol NUMBER, p_ini NUMBER, p_fin NUMBER) IS
  BEGIN
    FOR r IN (SELECT CODIGO_PERM FROM OSS_PERM WHERE CODIGO_PERM BETWEEN p_ini AND p_fin) LOOP
      add_rp(p_rol, r.CODIGO_PERM);
    END LOOP;
  END;
BEGIN
  -- CNT
  up_menu(3000, 3, NULL, 'Contabilidad', NULL, 'mdi:calculator-variant-outline', 10);
  up_menu(3010, 3, 3000, 'Comprobantes', '/apps/cnt/comprobantes', NULL, 10);
  up_menu(3020, 3, 3000, 'Proceso Automatico', '/apps/cnt/proceso-automatico', NULL, 20);
  up_menu(3030, 3, 3000, 'Procesos', NULL, NULL, 30);
  up_menu(3031, 3, 3030, 'Cierre contable', '/apps/cnt/procesos/cierre-contable', NULL, 10);
  up_menu(3040, 3, 3000, 'Conciliacion', NULL, NULL, 40);
  up_menu(3041, 3, 3040, 'Conciliaciones', '/apps/cnt/conciliacion', NULL, 10);
  up_menu(3042, 3, 3040, 'Importar estados de cuenta', '/apps/cnt/conciliacion/carga-banco', NULL, 20);
  up_menu(3043, 3, 3040, 'Estados de cuenta', '/apps/cnt/conciliacion/estados-cuenta', NULL, 30);
  up_menu(3044, 3, 3040, 'Libro banco', '/apps/cnt/conciliacion/libro-banco', NULL, 40);
  up_menu(3045, 3, 3040, 'Configuracion', '/apps/cnt/conciliacion/configuracion', NULL, 50);
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

  up_perm(300, 3, 'contabilidad.comprobantes.ver', 'Ver comprobantes', 'Consultar comprobantes contables.');
  up_perm(301, 3, 'contabilidad.comprobantes.crear', 'Crear comprobantes', 'Crear comprobantes contables.');
  up_perm(302, 3, 'contabilidad.comprobantes.editar', 'Editar comprobantes', 'Editar comprobantes contables.');
  up_perm(303, 3, 'contabilidad.comprobantes.editar_automatico', 'Editar automaticos', 'Editar comprobantes automaticos.');
  up_perm(304, 3, 'contabilidad.comprobantes.eliminar', 'Eliminar comprobantes', 'Eliminar comprobantes contables.');
  up_perm(305, 3, 'contabilidad.comprobantes.generar_automatico', 'Generar automatico', 'Generar comprobantes automaticos.');
  up_perm(306, 3, 'contabilidad.comprobantes.reordenar', 'Reordenar comprobantes', 'Reordenar comprobantes.');
  up_perm(307, 3, 'contabilidad.catalogos.ver', 'Ver catalogos', 'Consultar catalogos contables.');
  up_perm(308, 3, 'contabilidad.catalogos.admin', 'Administrar catalogos', 'Administrar catalogos contables.');
  up_perm(309, 3, 'contabilidad.reportes.ver', 'Ver reportes', 'Consultar reportes contables.');
  up_perm(310, 3, 'contabilidad.conciliacion.ver', 'Ver conciliacion', 'Consultar conciliacion bancaria.');
  up_perm(311, 3, 'contabilidad.conciliacion.importar', 'Importar conciliacion', 'Importar estados de cuenta.');
  up_perm(312, 3, 'contabilidad.conciliacion.admin', 'Administrar conciliacion', 'Administrar conciliacion bancaria.');
  up_perm(313, 3, 'contabilidad.conciliacion.cierre_forzado', 'Cierre forzado', 'Ejecutar cierre forzado de conciliacion.');
  up_perm(314, 3, 'contabilidad.conciliacion.editar_precierre', 'Editar precierre', 'Editar conciliacion en precierre.');
  up_perm(315, 3, 'contabilidad.conciliacion.formatos.ver', 'Ver formatos banco', 'Consultar formatos banco.');
  up_perm(316, 3, 'contabilidad.conciliacion.formatos.editar', 'Editar formatos banco', 'Editar formatos banco.');
  up_perm(317, 3, 'contabilidad.cierre.ver', 'Ver cierre', 'Consultar cierre contable.');
  up_perm(318, 3, 'contabilidad.cierre.precierre', 'Precierre', 'Ejecutar precierre contable.');
  up_perm(319, 3, 'contabilidad.cierre.cierre', 'Cierre', 'Ejecutar cierre contable.');
  up_perm(320, 3, 'contabilidad.cierre.reverso', 'Reverso', 'Ejecutar reverso de cierre.');

  add_mp(3010,300); add_mp(3010,301); add_mp(3010,302); add_mp(3010,303); add_mp(3010,304);
  add_mp(3020,305);
  add_mp(3031,317); add_mp(3031,318); add_mp(3031,319); add_mp(3031,320);
  add_mp(3041,310); add_mp(3041,311); add_mp(3041,312); add_mp(3041,313); add_mp(3041,314);
  add_mp(3042,311); add_mp(3043,310); add_mp(3044,310); add_mp(3045,312); add_mp(3046,315); add_mp(3046,316);
  add_mp(3051,309); add_mp(3052,309);
  FOR m IN 3061..3070 LOOP add_mp(m,307); add_mp(m,308); END LOOP;
  add_mp(3080,306); add_mp(3080,308);

  up_rol(300, 3, 'CNT_USUARIO', 'Usuario Contable', 'Usuario basico de contabilidad.');
  up_rol(301, 3, 'CNT_ANALISTA', 'Analista De Contabilidad', 'Analista operativo de contabilidad.');
  up_rol(302, 3, 'CNT_ADMIN', 'Administrador De Contabilidad', 'Administrador completo de contabilidad.');
  grant_menu_range(300, 3000, 3010); add_rp(300,300); add_rp(300,301);
  grant_menu_range(301, 3000, 3070); add_rp(301,300); add_rp(301,301); add_rp(301,302); add_rp(301,305); add_rp(301,307); add_rp(301,309); add_rp(301,310); add_rp(301,311); add_rp(301,317); add_rp(301,318);
  grant_menu_range(302, 3000, 3080); grant_perm_range(302,300,320);

  -- PRE
  up_menu(5000, 5, NULL, 'Presupuesto', NULL, 'mdi:file-document-outline', 10);
  up_menu(5010, 5, 5000, 'Presupuesto', '/dashboards/presupuesto', NULL, 10);
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
  up_perm(500, 5, 'presupuesto.menu.ver', 'Ver presupuesto', 'Consultar modulo presupuesto.');
  up_rol(500, 5, 'PRE_USUARIO', 'Usuario Presupuesto', 'Usuario del modulo presupuesto.');
  FOR m IN 5010..5120 LOOP IF MOD(m,10) = 0 THEN add_mp(m,500); END IF; END LOOP;
  grant_menu_range(500, 5000, 5120); add_rp(500,500);

  -- RH
  up_menu(4000, 4, NULL, 'Nomina', NULL, 'mdi:file-document-outline', 10);
  up_menu(4010, 4, 4000, 'Historico Individual', '/apps/rh/individual', NULL, 10);
  up_menu(4020, 4, 4000, 'Historico Masivo', '/apps/rh/masivo', NULL, 20);
  up_menu(4030, 4, 4000, 'Historico Proceso', '/apps/rh/proceso', NULL, 30);
  up_menu(4040, 4, 4000, 'Recibos', '/apps/rh/recibos', NULL, 40);
  up_menu(4050, 4, 4000, 'Relacion Cargo', '/apps/presupuesto/relacionCargo', NULL, 50);
  up_menu(4060, 4, 4000, 'Cargos', '/apps/presupuesto/cargo', NULL, 60);
  up_menu(4070, 4, 4000, 'Maestros', NULL, 'mdi:account-outline', 70);
  up_menu(4071, 4, 4070, 'Tipo Nomina', '/apps/rh/tipoNomina', NULL, 10);
  up_menu(4072, 4, 4070, 'Conceptos', '/apps/rh/conceptos', NULL, 20);
  up_menu(4073, 4, 4070, 'Periodos Nomina', '/apps/rh/periodos', NULL, 30);
  up_menu(4074, 4, 4070, 'Procesos Nomina', '/apps/rh/procesos', NULL, 40);
  up_menu(4080, 4, 4000, 'Personas', NULL, 'mdi:account-outline', 80);
  up_menu(4081, 4, 4080, 'Lista', '/apps/rh/persona/list', NULL, 10);
  up_menu(4082, 4, 4080, 'Beneficiarios', '/apps/rh/beneficiarios', NULL, 20);
  up_menu(4083, 4, 4080, 'Ficha', NULL, NULL, 30);
  up_menu(4084, 4, 4083, 'Resumen', '/apps/rh/persona/view/resumen', NULL, 10);
  up_menu(4085, 4, 4083, 'Comunicaciones', '/apps/rh/persona/view/security', NULL, 20);
  up_menu(4086, 4, 4083, 'Familiares', '/apps/rh/persona/view/billing-plan', NULL, 30);
  up_menu(4087, 4, 4083, 'Educacion', '/apps/rh/persona/view/notification', NULL, 40);
  up_menu(4088, 4, 4083, 'Variacion', '/apps/rh/persona/view/connection', NULL, 50);
  up_menu(4090, 4, 4000, 'Ret./Aportes', NULL, 'mdi:account-outline', 90);
  up_menu(4091, 4, 4090, 'CAH', '/apps/rh/retenciones/cah', NULL, 10);
  up_menu(4092, 4, 4090, 'FAOV', '/apps/rh/retenciones/faov', NULL, 20);
  up_menu(4093, 4, 4090, 'FJP', '/apps/rh/retenciones/fjp', NULL, 30);
  up_menu(4094, 4, 4090, 'INCES', '/apps/rh/retenciones/ince', NULL, 40);
  up_menu(4095, 4, 4090, 'SIND', '/apps/rh/retenciones/sind', NULL, 50);
  up_menu(4096, 4, 4090, 'SSO', '/apps/rh/retenciones/sso', NULL, 60);
  up_perm(400, 4, 'rh.menu.ver', 'Ver nomina', 'Consultar modulo nomina.');
  up_rol(400, 4, 'RH_USUARIO', 'Usuario Nomina', 'Usuario del modulo nomina.');
  FOR m IN (SELECT CODIGO_MENU FROM OSS_MENU WHERE CODIGO_MENU BETWEEN 4001 AND 4099 AND PATH IS NOT NULL) LOOP add_mp(m.CODIGO_MENU,400); END LOOP;
  grant_menu_range(400, 4000, 4099); add_rp(400,400);

  -- ADM
  up_menu(6000, 6, NULL, 'Administracion', NULL, 'mdi:file-document-outline', 10);
  up_menu(6010, 6, 6000, 'Solicitud de compromiso', '/apps/adm/solicitudCompromiso', NULL, 10);
  up_menu(6020, 6, 6000, 'Pre-Orden Pago', '/apps/adm/preOrdenPago', NULL, 20);
  up_menu(6030, 6, 6000, 'Ordenes Pago', '/apps/adm/ordenPago', NULL, 30);
  up_menu(6040, 6, 6000, 'Proveedores', NULL, 'mdi:account-outline', 40);
  up_menu(6041, 6, 6040, 'Ficha', NULL, NULL, 10);
  up_menu(6042, 6, 6041, 'Resumen', '/apps/adm/proveedor/view/resumen', NULL, 10);
  up_menu(6043, 6, 6041, 'Direcciones', '/apps/adm/proveedor/view/direccion', NULL, 20);
  up_menu(6044, 6, 6041, 'Contactos', '/apps/adm/proveedor/view/contacto', NULL, 30);
  up_menu(6045, 6, 6041, 'Comunicaciones', '/apps/adm/proveedor/view/comunicacion', NULL, 40);
  up_menu(6046, 6, 6041, 'Actividades', '/apps/adm/proveedor/view/actividad', NULL, 50);
  up_menu(6100, 6, NULL, 'Tesoreria', NULL, 'mdi:file-document-outline', 20);
  up_menu(6110, 6, 6100, 'Bancos', '/apps/adm/pagos/bancos', NULL, 10);
  up_menu(6120, 6, 6100, 'Cuentas', '/apps/adm/pagos/cuentas', NULL, 20);
  up_menu(6130, 6, 6100, 'Lotes', '/apps/adm/pagos/lotes', NULL, 30);
  up_perm(600, 6, 'administracion.menu.ver', 'Ver administracion', 'Consultar modulo administracion.');
  up_rol(600, 6, 'ADM_USUARIO', 'Usuario Administracion', 'Usuario del modulo administracion.');
  FOR m IN (SELECT CODIGO_MENU FROM OSS_MENU WHERE CODIGO_MENU BETWEEN 6001 AND 6130 AND PATH IS NOT NULL) LOOP add_mp(m.CODIGO_MENU,600); END LOOP;
  grant_menu_range(600, 6000, 6130); add_rp(600,600);
END;
/

COMMIT;

PROMPT Seed SIS Seguridad modulos legacy listo
