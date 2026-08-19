-- =============================================================================
-- MFO - Reportes concretos que una persona puede ejecutar.
--
-- Reemplaza el conjunto completo, igual que las acciones. **Una lista vacia no
-- revoca: devuelve al usuario a heredar todos los reportes del formulario.**
-- Es la convencion del modelo -sin filas significa "todos"- y por eso quitar el
-- acceso a los reportes se hace revocando EXPORTAR sobre el formulario, no
-- vaciando esta lista.
--
-- Los ids se validan contra el formulario indicado: sin eso, se podria conceder
-- un reporte de otro formulario al que el usuario no tiene acceso.
-- =============================================================================
CREATE OR REPLACE PROCEDURE MFO.SP_MFO_PERM_REP_SET (
    p_FormularioId IN  NUMBER,
    p_Usuario      IN  VARCHAR2,
    p_ReportesCsv  IN  VARCHAR2,
    p_UsuarioIns   IN  VARCHAR2,
    p_Asignados    OUT NUMBER,
    p_Message      OUT VARCHAR2
) AS
    v_usuario VARCHAR2(60)   := UPPER(TRIM(p_Usuario));
    v_resto   VARCHAR2(4000) := TRIM(p_ReportesCsv);
    v_texto   VARCHAR2(60);
    v_id      NUMBER;
    v_corte   NUMBER;
    v_existe  NUMBER;
    v_aud     NUMBER;
    v_msg     VARCHAR2(4000);
BEGIN
    p_Asignados := 0;

    IF v_usuario IS NULL THEN
        p_Message := 'El usuario es obligatorio.';
        RETURN;
    END IF;

    -- Se borran solo los reportes de ESTE formulario: un usuario puede tener
    -- acotaciones en varios formularios a la vez.
    DELETE FROM MFO_PERMISO_REP
     WHERE USUARIO = v_usuario
       AND REPORTE_ID IN (SELECT REPORTE_ID FROM MFO_REPORTE WHERE FORMULARIO_ID = p_FormularioId);

    WHILE v_resto IS NOT NULL LOOP
        v_corte := INSTR(v_resto, ',');

        IF v_corte = 0 THEN
            v_texto := TRIM(v_resto);
            v_resto := NULL;
        ELSE
            v_texto := TRIM(SUBSTR(v_resto, 1, v_corte - 1));
            v_resto := SUBSTR(v_resto, v_corte + 1);
        END IF;

        IF v_texto IS NOT NULL THEN
            BEGIN
                v_id := TO_NUMBER(v_texto);
            EXCEPTION
                WHEN OTHERS THEN
                    ROLLBACK;
                    p_Asignados := 0;
                    p_Message := 'Identificador de reporte no valido: ' || v_texto;
                    RETURN;
            END;

            SELECT COUNT(1) INTO v_existe
              FROM MFO_REPORTE
             WHERE REPORTE_ID = v_id AND FORMULARIO_ID = p_FormularioId;

            IF v_existe = 0 THEN
                ROLLBACK;
                p_Asignados := 0;
                p_Message := 'El reporte ' || v_id || ' no pertenece a este formulario.';
                RETURN;
            END IF;

            INSERT INTO MFO_PERMISO_REP (
                PERM_REP_ID, REPORTE_ID, USUARIO, USUARIO_INS, FECHA_INS
            ) VALUES (
                SEQ_MFO_PERM_REP.NEXTVAL, v_id, v_usuario, p_UsuarioIns, SYSDATE
            );

            p_Asignados := p_Asignados + 1;
        END IF;
    END LOOP;

    COMMIT;

    SP_MFO_AUD_INS('PERMISO_REP', p_FormularioId, 'ASIGNAR', p_UsuarioIns,
                   'Usuario ' || v_usuario || ' -> reportes ' || NVL(p_ReportesCsv, '(todos)'),
                   v_aud, v_msg);

    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        p_Asignados := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
END;
/
