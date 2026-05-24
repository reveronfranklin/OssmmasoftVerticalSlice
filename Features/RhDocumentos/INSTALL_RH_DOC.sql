PROMPT Compilando stored procedures de RH Documentos...

@@SP_RH_DOC_INS.sql
@@SP_RH_DOC_UPD.sql
@@SP_RH_DOC_DEL.sql
@@SP_RH_DOC_GET_ID.sql
@@SP_RH_DOC_GET_ALL.sql
@@SP_RH_DOC_GET_PER.sql

PROMPT Validando estado de stored procedures de RH Documentos...

SELECT object_name, status
  FROM user_objects
 WHERE object_type = 'PROCEDURE'
   AND object_name IN (
       'SP_RH_DOC_INS',
       'SP_RH_DOC_UPD',
       'SP_RH_DOC_DEL',
       'SP_RH_DOC_GET_ID',
       'SP_RH_DOC_GET_ALL',
       'SP_RH_DOC_GET_PER'
   )
 ORDER BY object_name;

SHOW ERRORS PROCEDURE SP_RH_DOC_INS
SHOW ERRORS PROCEDURE SP_RH_DOC_UPD
SHOW ERRORS PROCEDURE SP_RH_DOC_DEL
SHOW ERRORS PROCEDURE SP_RH_DOC_GET_ID
SHOW ERRORS PROCEDURE SP_RH_DOC_GET_ALL
SHOW ERRORS PROCEDURE SP_RH_DOC_GET_PER

PROMPT Firma actual de SP_RH_DOC_GET_ALL:

SELECT argument_name, position, in_out, data_type
  FROM user_arguments
 WHERE object_name = 'SP_RH_DOC_GET_ALL'
 ORDER BY position;
