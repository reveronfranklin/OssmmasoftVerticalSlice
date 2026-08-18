-- =============================================================================
-- MFO - Eliminar una opcion.
--
-- Si la opcion ya fue elegida en alguna respuesta no se borra: se desactiva.
-- Borrarla dejaria valores guardados apuntando a una opcion inexistente, y la
-- etiqueta congelada en MFO_VALOR.ETIQUETA_VAL solo sirve para mostrar, no para
-- reconstruir el conjunto de opciones que tenia el formulario.
-- =============================================================================
CREATE OR REPLACE PROCEDURE MFO.SP_MFO_OPCION_DELETE (
    p_OpcionId IN  NUMBER,
    p_Usuario  IN  VARCHAR2,
    p_Message  OUT VARCHAR2
) AS
    v_campo_id NUMBER;
    v_valor    VARCHAR2(100);
    v_usos     NUMBER;
BEGIN
    BEGIN
        SELECT CAMPO_ID, VALOR INTO v_campo_id, v_valor
          FROM MFO_OPCION WHERE OPCION_ID = p_OpcionId;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            p_Message := 'La opcion indicada no existe.';
            RETURN;
    END;

    SELECT COUNT(1) INTO v_usos
      FROM MFO_VALOR
     WHERE CAMPO_ID = v_campo_id
       AND VALOR_TXT = v_valor;

    IF v_usos > 0 THEN
        UPDATE MFO_OPCION SET ACTIVO = 'N' WHERE OPCION_ID = p_OpcionId;
        COMMIT;
        p_Message := 'success';
        RETURN;
    END IF;

    DELETE FROM MFO_OPCION WHERE OPCION_ID = p_OpcionId;

    COMMIT;
    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        IF SQLCODE = -20503 THEN
            p_Message := 'No se puede modificar una version publicada. Cree una version nueva.';
        ELSE
            p_Message := 'Error tecnico: ' || SQLERRM;
        END IF;
END;
/
