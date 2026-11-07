-- Ejecutar como una sentencia. Requiere R50 instalado. No asigna permisos a roles.
DECLARE
 rev NUMBER; menu_id NUMBER; perm_id NUMBER; mod_id NUMBER; parent_id NUMBER; mod_code VARCHAR2(10);
BEGIN
 SIS.PKG_OSS_MENU_SIMPLE.LOCK_REV(NULL,rev);
 -- Prefer the same Sistema group used by Seguridad; never leave the option at root.
 SELECT MIN(p.CODIGO_MENU) INTO parent_id FROM SIS.OSS_MENU p
 JOIN SIS.OSS_MENU c ON c.CODIGO_PADRE=p.CODIGO_MENU
 WHERE RTRIM(c.PATH,'/')='/apps/sis/seguridad' AND c.ACTIVO=1
 AND p.ACTIVO=1 AND UPPER(TRIM(p.TITULO))='SISTEMA';
 IF parent_id IS NULL THEN
 SELECT MIN(CODIGO_MENU) INTO parent_id FROM SIS.OSS_MENU
 WHERE UPPER(TRIM(TITULO))='SISTEMA' AND ACTIVO=1 AND PATH IS NULL;
 END IF;
 IF parent_id IS NULL THEN
 RAISE_APPLICATION_ERROR(-20008,'No existe el grupo Sistema activo en el catalogo. Revise R50.');
 END IF;
 SELECT m.CODIGO_MOD,mo.CODIGO INTO mod_id,mod_code FROM SIS.OSS_MENU m
 JOIN SIS.OSS_MOD mo ON mo.CODIGO_MOD=m.CODIGO_MOD WHERE m.CODIGO_MENU=parent_id AND mo.ACTIVO=1;
 BEGIN
 SELECT CODIGO_MENU INTO menu_id FROM SIS.OSS_MENU WHERE RTRIM(PATH,'/')='/apps/sis/correos' AND ACTIVO=1;
 EXCEPTION WHEN NO_DATA_FOUND THEN
 SIS.PKG_OSS_MENU_SIMPLE.MAP_NODE('R52_EMAIL_MANUAL_V1',mod_code,parent_id,'Enviar correo','/apps/sis/correos','mdi:email-outline',90,menu_id);
 END;
 -- Also relocate an option installed earlier without the Sistema parent.
 SIS.PKG_OSS_MENU_SIMPLE.SAVE_NODE(menu_id,mod_id,parent_id,'Enviar correo',
 '/apps/sis/correos','mdi:email-outline',90,1,0);
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
