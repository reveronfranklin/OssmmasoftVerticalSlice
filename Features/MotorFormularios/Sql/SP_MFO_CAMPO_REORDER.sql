-- =============================================================================
-- MFO - Reordenar los campos de una seccion.
--
-- Recibe la lista de CAMPO_ID separada por comas, en el orden deseado, y
-- reescribe ORDEN como 10, 20, 30... Los saltos de 10 son deliberados: dejan
-- hueco para insertar un campo entre dos sin reordenar la seccion entera.
--
-- La lista se parsea con INSTR/SUBSTR y cada elemento se convierte con TO_NUMBER
-- antes de usarse. No se construye SQL dinamico con el texto recibido: es una
-- lista de ids que viene del cliente, y concatenarla en una sentencia seria una
-- inyeccion esperando a ocurrir.
--
-- Solo se reordenan campos que pertenezcan a la seccion indicada. Un id de otra
-- seccion se ignora en vez de moverse: asi una lista mal armada no puede
-- arrastrar campos de otra parte del formulario.
-- =============================================================================
CREATE OR REPLACE PROCEDURE MFO.SP_MFO_CAMPO_REORDER (
    p_SeccionId  IN  NUMBER,
    p_CamposCsv  IN  VARCHAR2,
    p_Usuario    IN  VARCHAR2,
    p_Reordenados OUT NUMBER,
    p_Message    OUT VARCHAR2
) AS
    v_resto    VARCHAR2(4000) := TRIM(p_CamposCsv);
    v_coma     PLS_INTEGER;
    v_token    VARCHAR2(100);
    v_campo_id NUMBER;
    v_orden    NUMBER := 10;
    v_existe   NUMBER;
BEGIN
    p_Reordenados := 0;

    SELECT COUNT(1) INTO v_existe FROM MFO_SECCION WHERE SECCION_ID = p_SeccionId;
    IF v_existe = 0 THEN
        p_Message := 'La seccion indicada no existe.';
        RETURN;
    END IF;

    IF v_resto IS NULL THEN
        p_Message := 'No se recibio ningun campo que reordenar.';
        RETURN;
    END IF;

    LOOP
        v_coma := INSTR(v_resto, ',');

        IF v_coma = 0 THEN
            v_token := TRIM(v_resto);
            v_resto := NULL;
        ELSE
            v_token := TRIM(SUBSTR(v_resto, 1, v_coma - 1));
            v_resto := SUBSTR(v_resto, v_coma + 1);
        END IF;

        IF v_token IS NOT NULL THEN
            BEGIN
                v_campo_id := TO_NUMBER(v_token);
            EXCEPTION
                WHEN OTHERS THEN
                    ROLLBACK;
                    p_Reordenados := 0;
                    p_Message := 'La lista de campos contiene un valor que no es un identificador: ' || v_token;
                    RETURN;
            END;

            UPDATE MFO_CAMPO
               SET ORDEN = v_orden
             WHERE CAMPO_ID = v_campo_id
               AND SECCION_ID = p_SeccionId;

            IF SQL%ROWCOUNT > 0 THEN
                p_Reordenados := p_Reordenados + 1;
                v_orden := v_orden + 10;
            END IF;
        END IF;

        EXIT WHEN v_resto IS NULL;
    END LOOP;

    COMMIT;
    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        p_Reordenados := 0;
        IF SQLCODE = -20502 THEN
            p_Message := 'No se puede modificar una version publicada. Cree una version nueva.';
        ELSE
            p_Message := 'Error tecnico: ' || SQLERRM;
        END IF;
END;
/
