-- =============================================================================
-- MFO - Baja de un parametro de reporte.
--
-- Se borra sin ceremonia: un parametro es configuracion, no dato historico. Lo
-- que si queda registrado en MFO_REP_EJEC.PARAMS_CLB son los valores con los que
-- se ejecuto, y esas filas no dependen de que la fila de configuracion siga
-- existiendo.
-- =============================================================================
CREATE OR REPLACE PROCEDURE MFO.SP_MFO_REP_PARAM_DEL (
    p_RepParamId IN  NUMBER,
    p_Usuario    IN  VARCHAR2,
    p_Message    OUT VARCHAR2
) AS
    v_nombre MFO_REP_PARAM.NOMBRE_PARAM%TYPE;
    v_aud    NUMBER;
    v_msg    VARCHAR2(4000);
BEGIN
    BEGIN
        SELECT NOMBRE_PARAM INTO v_nombre
          FROM MFO_REP_PARAM
         WHERE REP_PARAM_ID = p_RepParamId;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            p_Message := 'El parametro indicado no existe.';
            RETURN;
    END;

    DELETE FROM MFO_REP_PARAM WHERE REP_PARAM_ID = p_RepParamId;

    COMMIT;

    SP_MFO_AUD_INS('REP_PARAM', p_RepParamId, 'ELIMINAR', p_Usuario,
                   'Parametro ' || v_nombre || ' eliminado.', v_aud, v_msg);

    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        p_Message := 'Error tecnico: ' || SQLERRM;
END;
/
