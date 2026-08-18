-- =============================================================================
-- MFO - Eliminar una condicion.
-- =============================================================================
CREATE OR REPLACE PROCEDURE MFO.SP_MFO_COND_DELETE (
    p_CondicionId IN  NUMBER,
    p_Usuario     IN  VARCHAR2,
    p_Message     OUT VARCHAR2
) AS
    v_existe NUMBER;
BEGIN
    SELECT COUNT(1) INTO v_existe FROM MFO_CONDICION WHERE CONDICION_ID = p_CondicionId;
    IF v_existe = 0 THEN
        p_Message := 'La condicion indicada no existe.';
        RETURN;
    END IF;

    DELETE FROM MFO_CONDICION WHERE CONDICION_ID = p_CondicionId;

    COMMIT;
    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        IF SQLCODE = -20505 THEN
            p_Message := 'No se puede modificar una version publicada. Cree una version nueva.';
        ELSE
            p_Message := 'Error tecnico: ' || SQLERRM;
        END IF;
END;
/
