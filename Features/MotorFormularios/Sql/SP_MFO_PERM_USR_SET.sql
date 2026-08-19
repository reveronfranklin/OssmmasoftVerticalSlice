-- =============================================================================
-- MFO - Acciones de una persona sobre un formulario.
--
-- Reemplaza el conjunto completo, no agrega: la pantalla muestra casillas, y lo
-- que el administrador ve al guardar es el estado final, no un delta. Una lista
-- vacia revoca todo, que es como se quita el acceso a alguien.
-- =============================================================================
CREATE OR REPLACE PROCEDURE MFO.SP_MFO_PERM_USR_SET (
    p_FormularioId IN  NUMBER,
    p_Usuario      IN  VARCHAR2,
    p_AccionesCsv  IN  VARCHAR2,
    p_UsuarioIns   IN  VARCHAR2,
    p_Asignados    OUT NUMBER,
    p_Message      OUT VARCHAR2
) AS
    v_usuario VARCHAR2(60)   := UPPER(TRIM(p_Usuario));
    v_resto   VARCHAR2(4000) := UPPER(TRIM(p_AccionesCsv));
    v_accion  VARCHAR2(12);
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

    SELECT COUNT(1) INTO v_existe FROM MFO_FORMULARIO WHERE FORMULARIO_ID = p_FormularioId;
    IF v_existe = 0 THEN
        p_Message := 'El formulario indicado no existe.';
        RETURN;
    END IF;

    DELETE FROM MFO_PERMISO_USR
     WHERE FORMULARIO_ID = p_FormularioId AND USUARIO = v_usuario;

    -- Parseo por INSTR/SUBSTR: son valores de un dominio cerrado y no pueden
    -- contener el separador. No se construye SQL con ellos en ningun momento.
    WHILE v_resto IS NOT NULL LOOP
        v_corte := INSTR(v_resto, ',');

        IF v_corte = 0 THEN
            v_accion := TRIM(v_resto);
            v_resto  := NULL;
        ELSE
            v_accion := TRIM(SUBSTR(v_resto, 1, v_corte - 1));
            v_resto  := SUBSTR(v_resto, v_corte + 1);
        END IF;

        IF v_accion IS NOT NULL THEN
            IF v_accion NOT IN ('DISENAR', 'LLENAR', 'VER', 'EXPORTAR', 'ANULAR') THEN
                ROLLBACK;
                p_Asignados := 0;
                p_Message := 'Accion no valida: ' || v_accion;
                RETURN;
            END IF;

            INSERT INTO MFO_PERMISO_USR (
                PERM_USR_ID, FORMULARIO_ID, USUARIO, ACCION, USUARIO_INS, FECHA_INS
            ) VALUES (
                SEQ_MFO_PERM_USR.NEXTVAL, p_FormularioId, v_usuario, v_accion, p_UsuarioIns, SYSDATE
            );

            p_Asignados := p_Asignados + 1;
        END IF;
    END LOOP;

    COMMIT;

    SP_MFO_AUD_INS('PERMISO_USR', p_FormularioId, 'ASIGNAR', p_UsuarioIns,
                   'Usuario ' || v_usuario || ' -> ' || NVL(p_AccionesCsv, '(sin acciones)'),
                   v_aud, v_msg);

    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        p_Asignados := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
END;
/
