-- =============================================================================
-- MFO - Alta o modificacion de una seccion.
--
-- Upsert por (VERSION_ID, CLAVE): el diseñador manda la seccion completa y este
-- procedimiento decide si inserta o actualiza. Asi el frontend no tiene que
-- llevar la cuenta de que ya existe y que no.
--
-- No comprueba que la version este en BORRADOR: de eso se encarga
-- TRG_MFO_SEC_LOCK, que cubre INSERT, UPDATE y DELETE. Comprobarlo tambien aqui
-- seria una segunda copia de la misma regla, con el riesgo de que se separen.
-- Lo que si se hace es traducir el error del trigger a un mensaje de negocio.
-- =============================================================================
CREATE OR REPLACE PROCEDURE MFO.SP_MFO_SEC_UPSERT (
    p_SeccionId   IN  NUMBER,
    p_VersionId   IN  NUMBER,
    p_Clave       IN  VARCHAR2,
    p_Titulo      IN  VARCHAR2,
    p_Descripcion IN  VARCHAR2,
    p_Orden       IN  NUMBER,
    p_Columnas    IN  NUMBER,
    p_EsPaso      IN  CHAR,
    p_Repetible   IN  CHAR,
    p_MinFilas    IN  NUMBER,
    p_MaxFilas    IN  NUMBER,
    p_Colapsable  IN  CHAR,
    p_Usuario     IN  VARCHAR2,
    p_OutId       OUT NUMBER,
    p_Message     OUT VARCHAR2
) AS
    v_clave     VARCHAR2(30) := UPPER(TRIM(p_Clave));
    v_repetible CHAR(1)      := NVL(p_Repetible, 'N');
    v_min       NUMBER       := p_MinFilas;
    v_max       NUMBER       := p_MaxFilas;
    v_id        NUMBER;
BEGIN
    p_OutId := 0;

    IF v_clave IS NULL THEN
        p_Message := 'La clave de la seccion es obligatoria.';
        RETURN;
    END IF;

    -- Una seccion no repetible no puede llevar rango de filas: se normaliza en
    -- vez de rechazar, porque el diseñador puede haber marcado y desmarcado
    -- "repetible" dejando los valores atras.
    IF v_repetible = 'N' THEN
        v_min := NULL;
        v_max := NULL;
    END IF;

    BEGIN
        SELECT SECCION_ID INTO v_id
          FROM MFO_SECCION
         WHERE VERSION_ID = p_VersionId
           AND (SECCION_ID = p_SeccionId OR CLAVE = v_clave);
    EXCEPTION
        WHEN NO_DATA_FOUND THEN v_id := NULL;
        WHEN TOO_MANY_ROWS THEN
            p_Message := 'La clave ' || v_clave || ' ya la usa otra seccion de esta version.';
            RETURN;
    END;

    IF v_id IS NULL THEN
        SELECT SEQ_MFO_SECCION.NEXTVAL INTO v_id FROM DUAL;
        INSERT INTO MFO_SECCION (
            SECCION_ID, VERSION_ID, CLAVE, TITULO, DESCRIPCION, ORDEN, COLUMNAS,
            ES_PASO, REPETIBLE, MIN_FILAS, MAX_FILAS, COLAPSABLE
        ) VALUES (
            v_id, p_VersionId, v_clave, p_Titulo, p_Descripcion, NVL(p_Orden, 10), NVL(p_Columnas, 1),
            NVL(p_EsPaso, 'N'), v_repetible, v_min, v_max, NVL(p_Colapsable, 'N')
        );
    ELSE
        UPDATE MFO_SECCION
           SET CLAVE       = v_clave,
               TITULO      = p_Titulo,
               DESCRIPCION = p_Descripcion,
               ORDEN       = NVL(p_Orden, ORDEN),
               COLUMNAS    = NVL(p_Columnas, COLUMNAS),
               ES_PASO     = NVL(p_EsPaso, ES_PASO),
               REPETIBLE   = v_repetible,
               MIN_FILAS   = v_min,
               MAX_FILAS   = v_max,
               COLAPSABLE  = NVL(p_Colapsable, COLAPSABLE)
         WHERE SECCION_ID = v_id;
    END IF;

    COMMIT;
    p_OutId := v_id;
    p_Message := 'success';
EXCEPTION
    WHEN DUP_VAL_ON_INDEX THEN
        ROLLBACK;
        p_OutId := 0;
        p_Message := 'La clave ' || v_clave || ' ya la usa otra seccion de esta version.';
    WHEN OTHERS THEN
        ROLLBACK;
        p_OutId := 0;
        IF SQLCODE = -20501 THEN
            p_Message := 'No se puede modificar una version publicada. Cree una version nueva.';
        ELSE
            p_Message := 'Error tecnico: ' || SQLERRM;
        END IF;
END;
/
