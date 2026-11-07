-- Ejecutar como una sentencia. Requiere R50 instalado. No asigna permisos a roles.
DECLARE
 rev NUMBER; menu_id NUMBER; perm_id NUMBER; mod_id NUMBER; parent_id NUMBER;
BEGIN
 SIS.PKG_OSS_MENU_SIMPLE.LOCK_REV(NULL,rev);
 SELECT MIN(CODIGO_PADRE) INTO parent_id FROM SIS.OSS_MENU WHERE RTRIM(PATH,'/')='/apps/sis/seguridad' AND ACTIVO=1;
 BEGIN
 SELECT CODIGO_MENU INTO menu_id FROM SIS.OSS_MENU WHERE RTRIM(PATH,'/')='/apps/sis/correos' AND ACTIVO=1;
 EXCEPTION WHEN NO_DATA_FOUND THEN
 SIS.PKG_OSS_MENU_SIMPLE.MAP_NODE('R52_EMAIL_MANUAL_V1','SIS',parent_id,'Enviar correo','/apps/sis/correos','mdi:email-outline',90,menu_id);
 END;
 SELECT CODIGO_MOD INTO mod_id FROM SIS.OSS_MENU WHERE CODIGO_MENU=menu_id;
 SIS.PKG_OSS_MENU_SIMPLE.MAP_PERM('sistema.correos.enviar',mod_id,perm_id);
 SIS.PKG_OSS_MENU_SIMPLE.LINK_PERM(menu_id,perm_id);
 SIS.PKG_OSS_MENU_SIMPLE.FINISH(13,NULL,0,'INSTALL_R52','Catalogo de correo manual; sin asignaciones de roles.');
 SIS.PKG_OSS_MENU_SIMPLE.RESET_WRITE;
 COMMIT;
EXCEPTION WHEN OTHERS THEN
 ROLLBACK;
 SIS.PKG_OSS_MENU_SIMPLE.RESET_WRITE;
 RAISE;
END;
