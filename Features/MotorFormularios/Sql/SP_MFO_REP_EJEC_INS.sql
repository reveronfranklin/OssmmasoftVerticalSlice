-- =============================================================================
-- MFO - Registro de una ejecucion de reporte en la bitacora.
--
-- AUTONOMOUS_TRANSACTION, por la misma razon que SP_MFO_AUD_INS: la entrada de
-- bitacora no debe perderse porque la operacion que la origino se deshaga.
-- Registrar el intento fallido es precisamente lo que interesa -un reporte que
-- revienta por timeout es el caso que hay que poder ver despues-.
--
-- Se escribe SIEMPRE, tambien cuando REGISTRA_EJEC='N' y no hay MFO_RESPUESTA
-- que enlazar. PARAMS_CLB es lo que hace auditable esa ejecucion transitoria:
-- sin respuesta guardada, es el unico rastro de con que valores corrio.
--
-- p_Resultado tiene dominio cerrado por CK_MFO_REP_EJEC_RESULT. Un valor fuera
-- de dominio se normaliza a ERROR en vez de reventar: perder la fila de bitacora
-- por una errata del llamador seria el peor de los dos males.
-- =============================================================================
CREATE OR REPLACE PROCEDURE MFO.SP_MFO_REP_EJEC_INS (
    p_ReporteId     IN  NUMBER,
    p_RespuestaId   IN  NUMBER,
    p_CodigoEmpresa IN  NUMBER,
    p_Usuario       IN  VARCHAR2,
    p_Milisegundos  IN  NUMBER,
    p_Filas         IN  NUMBER,
    p_Resultado     IN  VARCHAR2,
    p_Mensaje       IN  VARCHAR2,
    p_Params        IN  CLOB,
    p_IpOrigen      IN  VARCHAR2,
    p_OutId         OUT NUMBER,
    p_Message       OUT VARCHAR2
) AS
    PRAGMA AUTONOMOUS_TRANSACTION;

    v_resultado VARCHAR2(10) := UPPER(TRIM(p_Resultado));
    v_id        NUMBER;
BEGIN
    p_OutId := 0;

    IF v_resultado IS NULL OR v_resultado NOT IN ('OK', 'ERROR', 'VACIO', 'TRUNCADO') THEN
        v_resultado := 'ERROR';
    END IF;

    SELECT SEQ_MFO_REP_EJEC.NEXTVAL INTO v_id FROM DUAL;

    INSERT INTO MFO_REP_EJEC (
        REP_EJEC_ID, REPORTE_ID, RESPUESTA_ID, CODIGO_EMPRESA, USUARIO,
        FECHA_INICIO, MILISEGUNDOS, FILAS, RESULTADO, MENSAJE, PARAMS_CLB, IP_ORIGEN
    ) VALUES (
        v_id, p_ReporteId, p_RespuestaId, p_CodigoEmpresa, p_Usuario,
        SYSDATE, p_Milisegundos, p_Filas, v_resultado, SUBSTR(p_Mensaje, 1, 500),
        p_Params, p_IpOrigen
    );

    COMMIT;

    p_OutId := v_id;
    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        p_OutId := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
END;
/
