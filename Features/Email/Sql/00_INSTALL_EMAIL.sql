PROMPT Instalando modulo transversal de email...

@@01_TABLES_EMAIL.sql
@@SP_EMAIL_QUEUE.sql

PROMPT Configuracion SMTP: ejecutar manualmente 02_SAMPLE_EMAIL_CONFIG.sql despues de reemplazar placeholders.

SELECT object_name, object_type, status
  FROM user_objects
 WHERE object_name LIKE 'SIS_EMAIL%'
    OR object_name LIKE 'SP_EMAIL%'
 ORDER BY object_type, object_name;
