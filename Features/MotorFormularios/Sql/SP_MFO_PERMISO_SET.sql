-- =============================================================================
-- MFO - Fijar los permisos de un formulario para un rol.
--
-- Reemplaza el conjunto completo de acciones de ese rol sobre ese formulario:
-- recibe la lista de acciones separada por comas y deja exactamente esas. Es
-- deliberado que sea un reemplazo y no un alta incremental: la pantalla de
-- permisos muestra casillas, y lo que el usuario ve al guardar es el estado
-- final, no un delta.
--
-- ROL_CODIGO es el codigo de rol de SIS.OSS_USUARIO_ROL (decision 4 de la
-- Fase 0). El motor no consulta esa tabla -esta en otro schema y una transaccion
-- no cruza schemas en este repositorio-: guarda el codigo y el backend lo compara
-- con el rol del usuario autenticado.
--
-- La lista se parsea con INSTR/SUBSTR y cada accion se valida contra el dominio
-- cerrado antes de insertarse. No se arma SQL dinamico con el texto recibido.
-- =============================================================================
CREATE OR REPLACE PROCEDURE MFO.SP_MFO_PERMISO_SET (
    p_FormularioId IN  NUMBER,
    p_RolCodigo    IN  VARCHAR2,
    p_AccionesCsv  IN  VARCHAR2,
    p_Usuario      IN  VARCHAR2,
    p_Asignados    OUT NUMBER,
    p_Message      OUT VARCHAR2
) AS
    v_rol    VARCHAR2(30)   := UPPER(TRIM(p_RolCodigo));
    v_resto  VARCHAR2(4000) := UPPER(TRIM(p_AccionesCsv));
    v_coma   PLS_INTEGER;
    v_accion VARCHAR2(30);
    v_existe NUMBER;
BEGIN
    p_Asignados := 0;

    IF v_rol IS NULL THEN
        p_Message := 'El codigo de rol es obligatorio.';
        RETURN;
    END IF;

    SELECT COUNT(1) INTO v_existe FROM MFO_FORMULARIO WHERE FORMULARIO_ID = p_FormularioId;
    IF v_existe = 0 THEN
        p_Message := 'El formulario indicado no existe.';
        RETURN;
    END IF;

    -- Se limpia primero: lo que no venga en la lista queda revocado.
    DELETE FROM MFO_PERMISO
     WHERE FORMULARIO_ID = p_FormularioId AND ROL_CODIGO = v_rol;

    IF v_resto IS NOT NULL THEN
        LOOP
            v_coma := INSTR(v_resto, ',');

            IF v_coma = 0 THEN
                v_accion := TRIM(v_resto);
                v_resto  := NULL;
            ELSE
                v_accion := TRIM(SUBSTR(v_resto, 1, v_coma - 1));
                v_resto  := SUBSTR(v_resto, v_coma + 1);
            END IF;

            IF v_accion IS NOT NULL THEN
                IF v_accion NOT IN ('DISENAR', 'LLENAR', 'VER', 'EXPORTAR', 'ANULAR') THEN
                    ROLLBACK;
                    p_Asignados := 0;
                    p_Message := 'Accion no valida: ' || v_accion ||
                                 '. Use DISENAR, LLENAR, VER, EXPORTAR o ANULAR.';
                    RETURN;
                END IF;

                INSERT INTO MFO_PERMISO (PERMISO_ID, FORMULARIO_ID, ROL_CODIGO, ACCION)
                SELECT SEQ_MFO_PERMISO.NEXTVAL, p_FormularioId, v_rol, v_accion FROM DUAL;

                p_Asignados := p_Asignados + 1;
            END IF;

            EXIT WHEN v_resto IS NULL;
        END LOOP;
    END IF;

    INSERT INTO MFO_AUDITORIA (AUDITORIA_ID, ENTIDAD, ENTIDAD_ID, ACCION, USUARIO, FECHA, DETALLE_CLB)
    VALUES (SEQ_MFO_AUDITORIA.NEXTVAL, 'PERMISO', p_FormularioId, 'ASIGNAR', p_Usuario, SYSDATE,
            'Rol ' || v_rol || ': ' || NVL(p_AccionesCsv, '(ninguna)'));

    COMMIT;
    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        p_Asignados := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
END;
/
