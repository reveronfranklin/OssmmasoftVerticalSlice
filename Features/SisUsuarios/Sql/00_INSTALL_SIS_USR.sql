PROMPT Instalando soporte para CRUD SIS_USUARIOS...

@@../../../../Requerimientos/Modulo-Soporte/Encripted.sql
@@../../../../Requerimientos/Modulo-Soporte/Descencripted.sql
@@SP_SIS_USR.sql

SELECT object_name, object_type, status
  FROM user_objects
 WHERE object_name IN (
   'SIS_ENCRYPTED', 'SIS_DESENCRYPTED',
   'SP_SIS_USR_GET_ALL', 'SP_SIS_USR_GET_ID', 'SP_SIS_USR_INS',
   'SP_SIS_USR_UPD', 'SP_SIS_USR_EMAIL_UPD', 'SP_SIS_USR_PASS_UPD',
   'SP_SIS_USR_GET_SUPP'
 )
 ORDER BY object_type, object_name;
