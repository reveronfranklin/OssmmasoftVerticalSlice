PROMPT Instalando modulo de soporte...
PROMPT Requiere instalar previamente Features/Email/Sql/00_INSTALL_EMAIL.sql.

@@01_TABLES_SUPPORT.sql
@@02_SEED_SUPPORT_CATALOGS.sql
@@SP_SUPPORT_CORE.sql
@@03_FIX_TKT_GET_ALL_CURSOR.sql

PROMPT Validando objetos de soporte...

SELECT object_name, object_type, status
  FROM user_objects
 WHERE object_name LIKE 'SOP_%'
    OR object_name LIKE 'SP_SOP_%'
 ORDER BY object_type, object_name;
