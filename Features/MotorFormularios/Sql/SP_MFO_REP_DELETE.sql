-- =============================================================================
-- MFO - Retiro de un reporte enlazado.
--
-- Un reporte con ejecuciones registradas NO se borra: se inactiva. Borrarlo
-- obligaria a borrar tambien sus filas de MFO_REP_EJEC, y esa bitacora es
-- justamente lo que el modulo aporta que hoy no existe en ninguna parte del ERP.
-- Perderla para limpiar una fila de configuracion seria un mal negocio.
--
-- p_Eliminado distingue los dos desenlaces: 1 = borrado fisico, 0 = inactivado.
-- El frontend necesita poder decir cual de los dos ocurrio.
-- =============================================================================
CREATE OR REPLACE PROCEDURE MFO.SP_MFO_REP_DELETE (
    p_ReporteId IN  NUMBER,
    p_Usuario   IN  VARCHAR2,
    p_Eliminado OUT NUMBER,
    p_Message   OUT VARCHAR2
) AS
    v_clave  MFO_REPORTE.CLAVE%TYPE;
    v_ejec   NUMBER;
    v_aud    NUMBER;
    v_msg    VARCHAR2(4000);
BEGIN
    p_Eliminado := 0;

    BEGIN
        SELECT CLAVE INTO v_clave
          FROM MFO_REPORTE
         WHERE REPORTE_ID = p_ReporteId;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            p_Message := 'El reporte indicado no existe.';
            RETURN;
    END;

    SELECT COUNT(1) INTO v_ejec
      FROM MFO_REP_EJEC
     WHERE REPORTE_ID = p_ReporteId;

    IF v_ejec > 0 THEN
        UPDATE MFO_REPORTE
           SET ACTIVO      = 'N',
               USUARIO_UPD = p_Usuario,
               FECHA_UPD   = SYSDATE
         WHERE REPORTE_ID = p_ReporteId;

        COMMIT;

        SP_MFO_AUD_INS('REPORTE', p_ReporteId, 'INACTIVAR', p_Usuario,
                       'Reporte ' || v_clave || ' inactivado: tiene ' || v_ejec ||
                       ' ejecucion(es) en bitacora.', v_aud, v_msg);

        p_Eliminado := 0;
        p_Message := 'success';
        RETURN;
    END IF;

    -- Sin ejecuciones: se borra de abajo hacia arriba, igual que el resto del
    -- modulo, porque no hay ON DELETE CASCADE en las FK del schema.
    DELETE FROM MFO_REP_COLUMNA WHERE REPORTE_ID = p_ReporteId;
    DELETE FROM MFO_REP_PARAM   WHERE REPORTE_ID = p_ReporteId;
    DELETE FROM MFO_REPORTE     WHERE REPORTE_ID = p_ReporteId;

    COMMIT;

    SP_MFO_AUD_INS('REPORTE', p_ReporteId, 'ELIMINAR', p_Usuario,
                   'Reporte ' || v_clave || ' eliminado (sin ejecuciones).', v_aud, v_msg);

    p_Eliminado := 1;
    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        p_Eliminado := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
END;
/
