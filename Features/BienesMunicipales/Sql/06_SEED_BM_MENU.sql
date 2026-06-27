PROMPT Cargando opciones de menu Bienes Municipales

/*
  Ejecutar conectado al esquema SIS o con sinonimos/permisos sobre:
  OSS_MOD, OSS_PERM, OSS_ROL, OSS_MENU, OSS_MENU_PERM,
  OSS_ROL_PERM y OSS_ROL_MENU.

  El script es idempotente. Actualiza las opciones si ya existen y
  crea las relaciones de permisos/roles faltantes.
*/

DECLARE
  PROCEDURE up_mod(
    p_id  NUMBER,
    p_cod VARCHAR2,
    p_nom VARCHAR2,
    p_ico VARCHAR2,
    p_ord NUMBER
  ) IS
  BEGIN
    MERGE INTO OSS_MOD t
    USING (
      SELECT p_id id, p_cod cod, p_nom nom, p_ico ico, p_ord ord
        FROM dual
    ) s
       ON (t.CODIGO_MOD = s.id)
     WHEN MATCHED THEN
       UPDATE SET CODIGO = s.cod,
                  NOMBRE = s.nom,
                  ICONO = s.ico,
                  ORDEN = s.ord,
                  ACTIVO = 1,
                  FECHA_UPD = SYSDATE
     WHEN NOT MATCHED THEN
       INSERT (CODIGO_MOD, CODIGO, NOMBRE, ICONO, ORDEN, ACTIVO, FECHA_INS)
       VALUES (s.id, s.cod, s.nom, s.ico, s.ord, 1, SYSDATE);
  END;

  PROCEDURE up_perm(
    p_id  NUMBER,
    p_mod NUMBER,
    p_clv VARCHAR2,
    p_nom VARCHAR2,
    p_des VARCHAR2
  ) IS
  BEGIN
    MERGE INTO OSS_PERM t
    USING (
      SELECT p_id id, p_mod mod_id, p_clv clv, p_nom nom, p_des des
        FROM dual
    ) s
       ON (UPPER(TRIM(t.CLAVE)) = UPPER(TRIM(s.clv)))
     WHEN MATCHED THEN
       UPDATE SET CODIGO_MOD = s.mod_id,
                  NOMBRE = s.nom,
                  DESCRIPCION = s.des,
                  ACTIVO = 1,
                  FECHA_UPD = SYSDATE
     WHEN NOT MATCHED THEN
       INSERT (CODIGO_PERM, CODIGO_MOD, CLAVE, NOMBRE, DESCRIPCION, ACTIVO, FECHA_INS)
       VALUES (s.id, s.mod_id, s.clv, s.nom, s.des, 1, SYSDATE);
  END;

  PROCEDURE up_rol(
    p_id  NUMBER,
    p_mod NUMBER,
    p_clv VARCHAR2,
    p_nom VARCHAR2,
    p_des VARCHAR2
  ) IS
  BEGIN
    MERGE INTO OSS_ROL t
    USING (
      SELECT p_id id, p_mod mod_id, p_clv clv, p_nom nom, p_des des
        FROM dual
    ) s
       ON (UPPER(TRIM(t.CLAVE)) = UPPER(TRIM(s.clv)))
     WHEN MATCHED THEN
       UPDATE SET CODIGO_MOD = s.mod_id,
                  NOMBRE = s.nom,
                  DESCRIPCION = s.des,
                  ACTIVO = 1,
                  FECHA_UPD = SYSDATE
     WHEN NOT MATCHED THEN
       INSERT (CODIGO_ROL, CODIGO_MOD, CLAVE, NOMBRE, DESCRIPCION, ACTIVO, FECHA_INS)
       VALUES (s.id, s.mod_id, s.clv, s.nom, s.des, 1, SYSDATE);
  END;

  PROCEDURE up_menu(
    p_id   NUMBER,
    p_mod  NUMBER,
    p_pad  NUMBER,
    p_tit  VARCHAR2,
    p_path VARCHAR2,
    p_ico  VARCHAR2,
    p_ord  NUMBER,
    p_act  NUMBER DEFAULT 1
  ) IS
  BEGIN
    MERGE INTO OSS_MENU t
    USING (
      SELECT p_id id,
             p_mod mod_id,
             p_pad pad,
             p_tit tit,
             p_path path,
             p_ico ico,
             p_ord ord,
             p_act act
        FROM dual
    ) s
       ON (t.CODIGO_MENU = s.id)
     WHEN MATCHED THEN
       UPDATE SET CODIGO_MOD = s.mod_id,
                  CODIGO_PADRE = s.pad,
                  TITULO = s.tit,
                  PATH = s.path,
                  ICONO = s.ico,
                  ORDEN = s.ord,
                  ACTIVO = s.act,
                  FECHA_UPD = SYSDATE
     WHEN NOT MATCHED THEN
       INSERT (CODIGO_MENU, CODIGO_MOD, CODIGO_PADRE, TITULO, PATH, ICONO, ORDEN, ACTIVO, FECHA_INS)
       VALUES (s.id, s.mod_id, s.pad, s.tit, s.path, s.ico, s.ord, s.act, SYSDATE);
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
     WHEN NOT MATCHED THEN
       INSERT (CODIGO_MENU, CODIGO_PERM, FECHA_INS)
       VALUES (s.menu_id, s.perm_id, SYSDATE);
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
     WHEN NOT MATCHED THEN
       INSERT (CODIGO_ROL, CODIGO_PERM, FECHA_INS)
       VALUES (s.rol_id, s.perm_id, SYSDATE);
  END;

  PROCEDURE add_rm_key(p_rol VARCHAR2, p_menu NUMBER) IS
  BEGIN
    MERGE INTO OSS_ROL_MENU t
    USING (
      SELECT r.CODIGO_ROL rol_id, p_menu menu_id
        FROM OSS_ROL r
       WHERE UPPER(TRIM(r.CLAVE)) = UPPER(TRIM(p_rol))
    ) s
       ON (t.CODIGO_ROL = s.rol_id AND t.CODIGO_MENU = s.menu_id)
     WHEN NOT MATCHED THEN
       INSERT (CODIGO_ROL, CODIGO_MENU, FECHA_INS)
       VALUES (s.rol_id, s.menu_id, SYSDATE);
  END;

  PROCEDURE grant_menu_key(p_rol VARCHAR2) IS
  BEGIN
    FOR r IN (
      SELECT CODIGO_MENU
        FROM OSS_MENU
       WHERE CODIGO_MOD = 7
         AND ACTIVO = 1
    ) LOOP
      add_rm_key(p_rol, r.CODIGO_MENU);
    END LOOP;
  END;
BEGIN
  up_mod(7, 'BM', 'Bienes Municipales', 'mdi:archive-outline', 70);
  up_perm(9070, 7, 'bm.menu.ver', 'Ver bienes municipales', 'Consultar menu del modulo Bienes Municipales.');
  up_rol(9070, 7, 'BM_MENU', 'Menu Bienes Municipales', 'Acceso generico al menu del modulo Bienes Municipales.');
  up_rol(700, 7, 'BM_USUARIO', 'Usuario Bienes Municipales', 'Usuario operativo del modulo Bienes Municipales.');

  up_menu(7000, 7, NULL, 'Bienes Municipales', NULL, 'mdi:archive-outline', 10);
  up_menu(7010, 7, 7000, 'BM1', '/apps/Bm/Bm1', NULL, 10);
  up_menu(7020, 7, 7000, 'Contar', '/apps/Bm/BmContar', NULL, 20);
  up_menu(7030, 7, 7000, 'Conteo', '/apps/Bm/BmConteo', NULL, 30);
  up_menu(7040, 7, 7000, 'Conteo Detalle', '/apps/Bm/BmConteoDetalle', NULL, 40);
  up_menu(7050, 7, 7000, 'Comparar Conteo', '/apps/Bm/BmConteoDetalleCompare', NULL, 50);
  up_menu(7060, 7, 7000, 'Historico Conteo', '/apps/Bm/BmConteoHistorico', NULL, 60);
  up_menu(7070, 7, 7000, 'Placas Cuarentena', '/apps/Bm/BmPlacasCuarentena', NULL, 70);
  up_menu(7080, 7, 7000, 'Reporte BM1', '/apps/Bm/ReporteBM1', NULL, 80);
  up_menu(7090, 7, 7000, 'Ficha de Bienes', '/apps/Bm/BmBienes', NULL, 90);
  up_menu(7100, 7, 7000, 'Catalogos BM', '/apps/Bm/BmCatalogos', NULL, 100);
  up_menu(7110, 7, 7000, 'Ubicaciones BM', '/apps/Bm/BmUbicaciones', NULL, 110);
  up_menu(7120, 7, 7000, 'Movimientos BM', '/apps/Bm/BmMovimientos', NULL, 120);
  up_menu(7130, 7, 7000, 'Reportes BM', '/apps/Bm/BmReportes', NULL, 130);
  up_menu(7140, 7, 7000, 'Procesos Masivos', '/apps/Bm/BmProcesosMasivos', NULL, 140);

  add_rp_key('BM_MENU', 'bm.menu.ver');
  add_rp_key('BM_USUARIO', 'bm.menu.ver');

  add_mp_key(7000, 'bm.menu.ver');
  add_mp_key(7010, 'bm.menu.ver');
  add_mp_key(7020, 'bm.menu.ver');
  add_mp_key(7030, 'bm.menu.ver');
  add_mp_key(7040, 'bm.menu.ver');
  add_mp_key(7050, 'bm.menu.ver');
  add_mp_key(7060, 'bm.menu.ver');
  add_mp_key(7070, 'bm.menu.ver');
  add_mp_key(7080, 'bm.menu.ver');
  add_mp_key(7090, 'bm.menu.ver');
  add_mp_key(7100, 'bm.menu.ver');
  add_mp_key(7110, 'bm.menu.ver');
  add_mp_key(7120, 'bm.menu.ver');
  add_mp_key(7130, 'bm.menu.ver');
  add_mp_key(7140, 'bm.menu.ver');

  grant_menu_key('BM_MENU');
  grant_menu_key('BM_USUARIO');
END;
/

COMMIT;

PROMPT Opciones de menu Bienes Municipales listas
