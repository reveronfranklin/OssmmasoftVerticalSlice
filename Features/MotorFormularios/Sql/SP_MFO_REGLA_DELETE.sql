-- =============================================================================
-- MFO - Eliminar una regla de validacion.
-- Si se elimina la regla REQUERIDO, se baja tambien el atajo MFO_CAMPO.REQUERIDO
-- para que la ficha del campo no siga diciendo que es obligatorio.
-- =============================================================================
CREATE OR REPLACE PROCEDURE MFO.SP_MFO_REGLA_DELETE (
    p_ReglaId IN  NUMBER,
    p_Usuario IN  VARCHAR2,
    p_Message OUT VARCHAR2
) AS
    v_campo_id NUMBER;
    v_tipo     VARCHAR2(20);
BEGIN
    BEGIN
        SELECT CAMPO_ID, TIPO_REGLA INTO v_campo_id, v_tipo
          FROM MFO_REGLA WHERE REGLA_ID = p_ReglaId;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            p_Message := 'La regla indicada no existe.';
            RETURN;
    END;

    DELETE FROM MFO_REGLA WHERE REGLA_ID = p_ReglaId;

    IF v_tipo = 'REQUERIDO' THEN
        UPDATE MFO_CAMPO SET REQUERIDO = 'N' WHERE CAMPO_ID = v_campo_id;
    END IF;

    COMMIT;
    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        IF SQLCODE IN (-20502, -20504) THEN
            p_Message := 'No se puede modificar una version publicada. Cree una version nueva.';
        ELSE
            p_Message := 'Error tecnico: ' || SQLERRM;
        END IF;
END;
/
