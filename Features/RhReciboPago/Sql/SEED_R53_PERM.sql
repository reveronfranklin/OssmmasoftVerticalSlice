DECLARE rev NUMBER; mod_id NUMBER; id NUMBER;
BEGIN
 SIS.PKG_OSS_MENU_SIMPLE.LOCK_REV(NULL,rev);
 SELECT MIN(CODIGO_MOD) INTO mod_id FROM SIS.OSS_MENU WHERE RTRIM(PATH,'/')='/apps/rh/recibos' AND ACTIVO=1;
 IF mod_id IS NULL THEN RAISE_APPLICATION_ERROR(-20053,'Registre la opcion Recibos en el catalogo antes de instalar R53.'); END IF;
 SIS.PKG_OSS_MENU_SIMPLE.MAP_PERM('nomina.recibos.enviar',mod_id,id);
 SIS.PKG_OSS_MENU_SIMPLE.MAP_PERM('nomina.recibos.reenviar',mod_id,id);
 -- Do not link these action permissions to navigation: viewing PDF remains independent.
 SIS.PKG_OSS_MENU_SIMPLE.FINISH(13,NULL,0,'INSTALL_R53','Acciones RH envio/reenvio; sin concesion de roles.');
 SIS.PKG_OSS_MENU_SIMPLE.RESET_WRITE;
 COMMIT;
EXCEPTION WHEN OTHERS THEN ROLLBACK; SIS.PKG_OSS_MENU_SIMPLE.RESET_WRITE; RAISE;
END;
/
