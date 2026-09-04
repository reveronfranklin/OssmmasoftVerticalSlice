PROMPT Registrando el modulo de Facturacion Electronica en el menu (SIS)

-- =============================================================================
-- Facturacion Electronica (FED) - Requerimiento 32 - T1.11
--
-- ORACLE, no PostgreSQL. Por eso este script vive en SqlOracle/ y no en Sql/:
-- la carpeta Sql/ de esta feature esta declarada en CLAUDE.md como PostgreSQL, y
-- check-oracle-identifiers.sh no se corre sobre ella. Este archivo SI debe pasar
-- ese chequeo.
--
-- POR QUE EXISTE. El menu lateral del ERP no sale de src/navigation/vertical:
-- en src/layouts/UserLayout.tsx ese import esta comentado y el layout usa
-- ServerSideVerticalNavItems, que hace POST /SisUsuarios/GetMenuByUsuario. Ese
-- endpoint devuelve OSS_USUARIO_ROL.JSON_MENU, y ese JSON lo genera
-- POST /api/SisSeguridad/regenerarCache a partir del modelo normalizado
-- OSS_MOD / OSS_MENU / OSS_ROL_MENU. Agregar la opcion en el archivo de
-- TypeScript no tenia ningun efecto: era codigo muerto, y por eso se elimino.
--
-- QUE HACE. Registra el modulo y sus cuatro opciones en el modelo normalizado, con
-- MERGE, de forma reejecutable. Sigue el patron de
-- Features/SisSeguridad/Sql/08_SEED_APP_ROUTES.sql, del que se copiaron los
-- procedimientos locales.
--
-- QUE NO HACE. No decide quien ve el modulo. Mientras no se otorgue a un rol, las
-- filas existen y nadie las ve: OSS_MENU sin OSS_ROL_MENU no llega a ningun
-- JSON_MENU. Para otorgarlo hay que poner la CLAVE del rol en v_rol_clave, abajo.
--
-- COMO SE APLICA
--   1. Conectado como el propietario del esquema SIS, ejecutar este script.
--   2. POST /api/SisSeguridad/regenerarCache con el codigoUsuario correspondiente.
--      Ese paso escribe JSON_MENU. Sin el, el menu no cambia.
--   3. Recargar el ERP: el menu se pide al montar el layout.
--
-- COMO SE REVIERTE
--   DELETE FROM OSS_ROL_MENU WHERE CODIGO_MENU IN (9000, 9010, 9020, 9030, 9040);
--   DELETE FROM OSS_MENU     WHERE CODIGO_MENU IN (9000, 9010, 9020, 9030, 9040);
--   DELETE FROM OSS_MOD      WHERE CODIGO_MOD = 9;
--   DELETE FROM OSS_USUARIO_ROL WHERE UPPER(TRIM(DESCRIPCION)) = 'FED';
--   y volver a regenerar el cache.
--
-- CODIGOS ELEGIDOS. Los modulos 1 a 8 estan tomados (SIS, SOP, CNT, RH, PRE, ADM,
-- BM, APP), asi que FED es el 9. Los CODIGO_MENU siguen la convencion del seed
-- existente -el primer digito es el modulo-, de 9000 en adelante: rango libre.
--
-- Los textos van SIN ACENTOS, como el resto de los seeds de menu del ERP
-- (Administracion, Tesoreria, Bienes Muebles). No es un descuido: es la
-- convencion del dato.
-- =============================================================================

DECLARE
  -- CLAVE del rol que debe ver el modulo. Vacio = no se otorga a nadie.
  -- Se deja vacio a proposito: quien ve un modulo fiscal es una decision de
  -- negocio, no del script que lo instala.
  v_rol_clave CONSTANT VARCHAR2(60) := '';

  v_otorgados NUMBER := 0;

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

  PROCEDURE add_rm(p_rol NUMBER, p_menu NUMBER) IS
  BEGIN
    MERGE INTO OSS_ROL_MENU t
    USING (SELECT p_rol rol_id, p_menu menu_id FROM dual) s
       ON (t.CODIGO_ROL = s.rol_id AND t.CODIGO_MENU = s.menu_id)
     WHEN NOT MATCHED THEN INSERT (CODIGO_ROL, CODIGO_MENU, FECHA_INS) VALUES (s.rol_id, s.menu_id, SYSDATE);
  END;

BEGIN
  -- Modulo. El 9 esta libre: del 1 al 8 estan tomados por el seed existente.
  up_mod(9, 'FED', 'Facturacion Electronica', 'mdi:receipt-text-outline', 90);

  -- Nodo padre: sin PATH, como Tesoreria (6100) en el seed existente. Un padre
  -- con path se vuelve navegable y no debe serlo.
  up_menu(9000, 9, NULL, 'Facturacion Electronica', NULL, 'mdi:receipt-text-outline', 10);

  -- Opciones. Cada path tiene que coincidir con una pagina real del frontend,
  -- bajo src/pages/apps/fed/.
  up_menu(9010, 9, 9000, 'Emisores', '/apps/fed', 'mdi:account-box-outline', 20);
  up_menu(9020, 9, 9000, 'Numeros de Control', '/apps/fed/numeros-control', 'mdi:numeric', 30);
  up_menu(9030, 9, 9000, 'Reporte Mensual', '/apps/fed/reporte-mensual', 'mdi:calendar-check-outline', 40);
  up_menu(9040, 9, 9000, 'Documentos Fiscales', '/apps/fed/facturas', 'mdi:file-document-outline', 50);

  -- Otorgamiento al rol, solo si se indico una clave arriba.
  IF v_rol_clave IS NOT NULL AND LENGTH(TRIM(v_rol_clave)) > 0 THEN
    FOR r IN (SELECT CODIGO_ROL FROM OSS_ROL WHERE UPPER(TRIM(CLAVE)) = UPPER(TRIM(v_rol_clave))) LOOP
      add_rm(r.CODIGO_ROL, 9000);
      add_rm(r.CODIGO_ROL, 9010);
      add_rm(r.CODIGO_ROL, 9020);
      add_rm(r.CODIGO_ROL, 9030);
      add_rm(r.CODIGO_ROL, 9040);
      v_otorgados := v_otorgados + 1;
    END LOOP;

    IF v_otorgados = 0 THEN
      -- No se lanza: el registro del modulo ya quedo hecho y es lo valioso del
      -- script. Se avisa para que no se crea otorgado lo que no lo esta.
      DBMS_OUTPUT.PUT_LINE('AVISO: no existe un rol con la clave ' || v_rol_clave || '. El modulo quedo registrado pero sin otorgar.');
    END IF;
  ELSE
    DBMS_OUTPUT.PUT_LINE('AVISO: v_rol_clave vacio. El modulo quedo registrado y NO se otorgo a ningun rol.');
  END IF;
END;
/

COMMIT;

PROMPT Modulo FED registrado. Falta POST /api/SisSeguridad/regenerarCache para que aparezca en el menu.
