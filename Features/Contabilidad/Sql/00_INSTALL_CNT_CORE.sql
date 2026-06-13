PROMPT Instalando core del modulo de Contabilidad CNT...
PROMPT Requiere que las tablas, indices y secuencias base de CNT existan previamente.

@@SP_CNT_CORE.sql
@@SP_CNT_CATALOGOS.sql
@@SP_CNT_RUBROS.sql
@@SP_CNT_BALANCES.sql
@@SP_CNT_MAYORES.sql
@@SP_CNT_AUXILIARES.sql
@@SP_CNT_AUX_PUC.sql
@@SP_CNT_PERIODOS.sql
@@SP_CNT_REL_DOC.sql
@@SP_CNT_SALDOS.sql
@@SP_CNT_CONCILIACION.sql
@@SP_CNT_CIERRE_CONTABLE.sql

PROMPT Validando objetos CNT...

SELECT object_name, object_type, status
  FROM user_objects
 WHERE object_name LIKE 'SP_CNT_%'
 ORDER BY object_type, object_name;
