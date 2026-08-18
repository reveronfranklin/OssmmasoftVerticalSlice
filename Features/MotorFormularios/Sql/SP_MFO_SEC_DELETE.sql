-- =============================================================================
-- MFO - Eliminar una seccion con todo lo que cuelga de ella.
--
-- Borrado explicito de abajo hacia arriba: opciones y reglas de sus campos,
-- condiciones que la tengan como destino o cuyo origen sea uno de sus campos,
-- luego los campos y por ultimo la seccion. El arbol de definicion no tiene
-- ON DELETE CASCADE porque con cascada los triggers de inmutabilidad fallarian
-- con ORA-04091 (ver 02_MFO_CONSTRAINTS.sql).
--
-- Las condiciones que dependian de esta seccion se borran, no se dejan
-- huerfanas. Es una perdida real de configuracion, asi que se informa cuantas
-- en p_CondicionesBorradas en vez de hacerlo callado.
-- =============================================================================
CREATE OR REPLACE PROCEDURE MFO.SP_MFO_SEC_DELETE (
    p_SeccionId           IN  NUMBER,
    p_Usuario             IN  VARCHAR2,
    p_CondicionesBorradas OUT NUMBER,
    p_Message             OUT VARCHAR2
) AS
    v_version_id NUMBER;
BEGIN
    p_CondicionesBorradas := 0;

    BEGIN
        SELECT VERSION_ID INTO v_version_id
          FROM MFO_SECCION WHERE SECCION_ID = p_SeccionId;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            p_Message := 'La seccion indicada no existe.';
            RETURN;
    END;

    SELECT COUNT(1) INTO p_CondicionesBorradas
      FROM MFO_CONDICION D
     WHERE D.VERSION_ID = v_version_id
       AND ((D.DESTINO_TIPO = 'SECCION' AND D.DESTINO_ID = p_SeccionId)
         OR D.CAMPO_ORIGEN_ID IN (SELECT CAMPO_ID FROM MFO_CAMPO WHERE SECCION_ID = p_SeccionId)
         OR (D.DESTINO_TIPO = 'CAMPO'
             AND D.DESTINO_ID IN (SELECT CAMPO_ID FROM MFO_CAMPO WHERE SECCION_ID = p_SeccionId)));

    DELETE FROM MFO_CONDICION D
     WHERE D.VERSION_ID = v_version_id
       AND ((D.DESTINO_TIPO = 'SECCION' AND D.DESTINO_ID = p_SeccionId)
         OR D.CAMPO_ORIGEN_ID IN (SELECT CAMPO_ID FROM MFO_CAMPO WHERE SECCION_ID = p_SeccionId)
         OR (D.DESTINO_TIPO = 'CAMPO'
             AND D.DESTINO_ID IN (SELECT CAMPO_ID FROM MFO_CAMPO WHERE SECCION_ID = p_SeccionId)));

    DELETE FROM MFO_OPCION
     WHERE CAMPO_ID IN (SELECT CAMPO_ID FROM MFO_CAMPO WHERE SECCION_ID = p_SeccionId);

    DELETE FROM MFO_REGLA
     WHERE CAMPO_ID IN (SELECT CAMPO_ID FROM MFO_CAMPO WHERE SECCION_ID = p_SeccionId);

    DELETE FROM MFO_CAMPO   WHERE SECCION_ID = p_SeccionId;
    DELETE FROM MFO_SECCION WHERE SECCION_ID = p_SeccionId;

    COMMIT;
    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        p_CondicionesBorradas := 0;
        IF SQLCODE IN (-20501, -20502, -20503, -20504, -20505) THEN
            p_Message := 'No se puede modificar una version publicada. Cree una version nueva.';
        ELSE
            p_Message := 'Error tecnico: ' || SQLERRM;
        END IF;
END;
/
