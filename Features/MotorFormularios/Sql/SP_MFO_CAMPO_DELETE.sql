-- =============================================================================
-- MFO - Eliminar un campo.
--
-- Borra primero sus opciones y reglas, y las condiciones en las que participe
-- -como origen o como destino-. FK_MFO_COND_ORIGEN no tiene cascada justamente
-- para que este borrado sea deliberado: una condicion que pierde su campo origen
-- no tiene forma de evaluarse.
--
-- p_CondicionesBorradas informa cuantas ramas de logica se perdieron, para que
-- el diseñador pueda avisarlo en pantalla.
-- =============================================================================
CREATE OR REPLACE PROCEDURE MFO.SP_MFO_CAMPO_DELETE (
    p_CampoId             IN  NUMBER,
    p_Usuario             IN  VARCHAR2,
    p_CondicionesBorradas OUT NUMBER,
    p_Message             OUT VARCHAR2
) AS
    v_version_id NUMBER;
    v_valores    NUMBER;
BEGIN
    p_CondicionesBorradas := 0;

    BEGIN
        SELECT VERSION_ID INTO v_version_id FROM MFO_CAMPO WHERE CAMPO_ID = p_CampoId;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            p_Message := 'El campo indicado no existe.';
            RETURN;
    END;

    -- Un campo de una version en BORRADOR no deberia tener valores, pero si los
    -- tuviera, borrarlo dejaria respuestas apuntando a un campo inexistente.
    SELECT COUNT(1) INTO v_valores FROM MFO_VALOR WHERE CAMPO_ID = p_CampoId;
    IF v_valores > 0 THEN
        p_Message := 'El campo tiene respuestas asociadas y no se puede eliminar.';
        RETURN;
    END IF;

    SELECT COUNT(1) INTO p_CondicionesBorradas
      FROM MFO_CONDICION
     WHERE VERSION_ID = v_version_id
       AND (CAMPO_ORIGEN_ID = p_CampoId
         OR (DESTINO_TIPO = 'CAMPO' AND DESTINO_ID = p_CampoId));

    DELETE FROM MFO_CONDICION
     WHERE VERSION_ID = v_version_id
       AND (CAMPO_ORIGEN_ID = p_CampoId
         OR (DESTINO_TIPO = 'CAMPO' AND DESTINO_ID = p_CampoId));

    DELETE FROM MFO_OPCION WHERE CAMPO_ID = p_CampoId;
    DELETE FROM MFO_REGLA  WHERE CAMPO_ID = p_CampoId;
    DELETE FROM MFO_CAMPO  WHERE CAMPO_ID = p_CampoId;

    COMMIT;
    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        p_CondicionesBorradas := 0;
        IF SQLCODE IN (-20502, -20503, -20504, -20505) THEN
            p_Message := 'No se puede modificar una version publicada. Cree una version nueva.';
        ELSE
            p_Message := 'Error tecnico: ' || SQLERRM;
        END IF;
END;
/
