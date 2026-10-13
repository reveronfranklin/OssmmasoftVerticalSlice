-- Run as CNT with active writers stopped. Activation is a separate final step.
WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
@@INSTALL_CNT_CMP_PEND.sql
@@SP_CNT_CMP_GUARD.sql
@@SP_CNT_CORE.sql
@@SP_CNT_CMP_VIEWS.sql
@@SP_CNT_CMP_TRIGGERS.sql
@@SP_CNT_CIERRE_CONTABLE.sql
@@SP_CNT_CONCILIACION.sql
@@SP_CNT_PERIODOS.sql
-- Install SP_CNT_CMP_LEGACY.sql separately if the legacy package is deployed,
-- after comparing the delivered body against the production body.
SELECT OBJECT_NAME, OBJECT_TYPE, STATUS FROM ALL_OBJECTS WHERE OWNER = 'CNT'
 AND STATUS <> 'VALID' ORDER BY OBJECT_NAME;
SELECT NAME, TYPE, LINE, POSITION, TEXT FROM ALL_ERRORS WHERE OWNER = 'CNT' ORDER BY NAME, SEQUENCE;
