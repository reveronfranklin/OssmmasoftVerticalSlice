-- Requirement 48. SQL*Plus/SQLcl as RH. Coordinate with the API publication.
WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
@@SP_RH_HISTORICO_MOV_MASIVO_GET.sql
@@../RhConceptos/SP_RH_CONCEPTOS_BY_PERSONA_GET.sql
@@../RhTipoNomina/SP_RH_TN_BY_PERSONA_GET.sql
DECLARE
    v_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_count FROM ALL_OBJECTS
     WHERE OWNER = 'RH' AND OBJECT_TYPE = 'PROCEDURE' AND STATUS = 'VALID'
       AND OBJECT_NAME IN ('SP_RH_HISTORICO_MOV_MASIVO_GET',
                           'SP_RH_CONCEPTOS_BY_PERSONA_GET', 'SP_RH_TN_BY_PERSONA_GET');
    IF v_count <> 3 THEN
        RAISE_APPLICATION_ERROR(-20001, 'Revisar ALL_ERRORS: procedimientos de historico invalidos');
    END IF;
END;
/
