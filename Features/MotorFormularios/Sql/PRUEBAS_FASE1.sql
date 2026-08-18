-- =============================================================================
-- Motor de Formularios (MFO) - Fase 1 - Guion de pruebas manuales
-- Requerimiento 16.
--
-- Cubre los dos criterios de aceptacion de la Fase 1 que no verifica el script
-- de identificadores:
--   A. La inmutabilidad es real: un UPDATE sobre un campo de la version
--      publicada falla con el mensaje del trigger.
--   B. El invariante de publicacion es real: una segunda version en PUBLICADA
--      para el mismo formulario falla por indice unico.
--
-- Cada prueba deja la base como la encontro.
-- =============================================================================

SET SERVEROUTPUT ON

-- -----------------------------------------------------------------------------
-- A. Inmutabilidad de la definicion publicada.
--    Esperado: ORA-20502 con el texto del trigger.
-- -----------------------------------------------------------------------------
PROMPT === A. UPDATE sobre un campo de la version PUBLICADA (debe fallar)
DECLARE
    v_campo_id NUMBER;
BEGIN
    SELECT c.CAMPO_ID INTO v_campo_id
      FROM MFO_CAMPO c
      JOIN MFO_FORMULARIO f ON f.VERSION_PUBL_ID = c.VERSION_ID
     WHERE f.ALIAS = 'REP_BM1' AND c.CLAVE = 'FECHA_DESDE';

    UPDATE MFO_CAMPO SET ETIQUETA = 'Modificado a mano' WHERE CAMPO_ID = v_campo_id;

    ROLLBACK;
    DBMS_OUTPUT.PUT_LINE('FALLO: el UPDATE paso. La inmutabilidad NO esta activa.');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -20502 THEN
            DBMS_OUTPUT.PUT_LINE('OK: ' || SQLERRM);
        ELSE
            DBMS_OUTPUT.PUT_LINE('FALLO: error inesperado -> ' || SQLERRM);
        END IF;
        ROLLBACK;
END;
/

-- -----------------------------------------------------------------------------
-- B. Un formulario no puede tener dos versiones PUBLICADA.
--    Esperado: ORA-00001 sobre IDX_MFO_VER_PUBL_UNQ.
-- -----------------------------------------------------------------------------
PROMPT === B. Segunda version PUBLICADA para el mismo formulario (debe fallar)
DECLARE
    v_formulario_id NUMBER;
    v_version_id    NUMBER;
BEGIN
    SELECT FORMULARIO_ID INTO v_formulario_id FROM MFO_FORMULARIO WHERE ALIAS = 'REP_BM1';

    SELECT SEQ_MFO_VERSION.NEXTVAL INTO v_version_id FROM DUAL;
    INSERT INTO MFO_VERSION (VERSION_ID, FORMULARIO_ID, NUMERO, ESTADO, USUARIO_INS, FECHA_INS)
    VALUES (v_version_id, v_formulario_id, 99, 'BORRADOR', 'PRUEBA', SYSDATE);

    -- Hasta aqui todo bien: una BORRADOR puede convivir con la PUBLICADA.
    UPDATE MFO_VERSION SET ESTADO = 'PUBLICADA' WHERE VERSION_ID = v_version_id;

    ROLLBACK;
    DBMS_OUTPUT.PUT_LINE('FALLO: hay dos versiones PUBLICADA. El invariante NO esta activo.');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -1 THEN
            DBMS_OUTPUT.PUT_LINE('OK: ' || SQLERRM);
        ELSE
            DBMS_OUTPUT.PUT_LINE('FALLO: error inesperado -> ' || SQLERRM);
        END IF;
        ROLLBACK;
END;
/

-- -----------------------------------------------------------------------------
-- C. Transicion de estado invalida (ARCHIVADA -> PUBLICADA).
--    Esperado: ORA-20507.
-- -----------------------------------------------------------------------------
PROMPT === C. Transicion de estado invalida (debe fallar)
DECLARE
    v_formulario_id NUMBER;
    v_version_id    NUMBER;
BEGIN
    SELECT FORMULARIO_ID INTO v_formulario_id FROM MFO_FORMULARIO WHERE ALIAS = 'REP_BM1';

    SELECT SEQ_MFO_VERSION.NEXTVAL INTO v_version_id FROM DUAL;
    INSERT INTO MFO_VERSION (VERSION_ID, FORMULARIO_ID, NUMERO, ESTADO, USUARIO_INS, FECHA_INS)
    VALUES (v_version_id, v_formulario_id, 98, 'ARCHIVADA', 'PRUEBA', SYSDATE);

    UPDATE MFO_VERSION SET ESTADO = 'PUBLICADA' WHERE VERSION_ID = v_version_id;

    ROLLBACK;
    DBMS_OUTPUT.PUT_LINE('FALLO: se admitio ARCHIVADA -> PUBLICADA.');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -20507 THEN
            DBMS_OUTPUT.PUT_LINE('OK: ' || SQLERRM);
        ELSE
            DBMS_OUTPUT.PUT_LINE('FALLO: error inesperado -> ' || SQLERRM);
        END IF;
        ROLLBACK;
END;
/

-- -----------------------------------------------------------------------------
-- D. Un BORRADOR si se puede editar y borrar, y su borrado explicito de abajo
--    hacia arriba no choca con los triggers (ORA-04091). Es la prueba de que la
--    decision de no usar ON DELETE CASCADE en el arbol de definicion funciona.
-- -----------------------------------------------------------------------------
PROMPT === D. Edicion y borrado de un BORRADOR (debe pasar)
DECLARE
    v_formulario_id NUMBER;
    v_version_id    NUMBER;
    v_seccion_id    NUMBER;
    v_campo_id      NUMBER;
    v_tipo_texto    NUMBER;
BEGIN
    SELECT FORMULARIO_ID INTO v_formulario_id FROM MFO_FORMULARIO WHERE ALIAS = 'REP_BM1';
    SELECT TIPO_CAMPO_ID INTO v_tipo_texto FROM MFO_TIPO_CAMPO WHERE CODIGO = 'TEXTO';

    SELECT SEQ_MFO_VERSION.NEXTVAL INTO v_version_id FROM DUAL;
    INSERT INTO MFO_VERSION (VERSION_ID, FORMULARIO_ID, NUMERO, ESTADO, USUARIO_INS, FECHA_INS)
    VALUES (v_version_id, v_formulario_id, 97, 'BORRADOR', 'PRUEBA', SYSDATE);

    SELECT SEQ_MFO_SECCION.NEXTVAL INTO v_seccion_id FROM DUAL;
    INSERT INTO MFO_SECCION (SECCION_ID, VERSION_ID, CLAVE, TITULO, ORDEN, COLUMNAS, ES_PASO, REPETIBLE, COLAPSABLE)
    VALUES (v_seccion_id, v_version_id, 'TMP', 'Temporal', 10, 1, 'N', 'N', 'N');

    SELECT SEQ_MFO_CAMPO.NEXTVAL INTO v_campo_id FROM DUAL;
    INSERT INTO MFO_CAMPO (CAMPO_ID, VERSION_ID, SECCION_ID, TIPO_CAMPO_ID, CLAVE, ETIQUETA, ORDEN, ANCHO, REQUERIDO, SOLO_LECTURA)
    VALUES (v_campo_id, v_version_id, v_seccion_id, v_tipo_texto, 'TMP_CAMPO', 'Temporal', 10, 12, 'N', 'N');

    INSERT INTO MFO_REGLA (REGLA_ID, CAMPO_ID, TIPO_REGLA, MENSAJE, ORDEN, ACTIVO)
    VALUES (SEQ_MFO_REGLA.NEXTVAL, v_campo_id, 'LONG_MAX', 'Muy largo.', 10, 'S');

    -- Editar el borrador: debe pasar.
    UPDATE MFO_CAMPO SET ETIQUETA = 'Temporal editado' WHERE CAMPO_ID = v_campo_id;

    -- Borrado explicito de abajo hacia arriba, el orden que usaran los
    -- procedimientos de la Fase 2.
    DELETE FROM MFO_REGLA   WHERE CAMPO_ID   = v_campo_id;
    DELETE FROM MFO_CAMPO   WHERE CAMPO_ID   = v_campo_id;
    DELETE FROM MFO_SECCION WHERE SECCION_ID = v_seccion_id;
    DELETE FROM MFO_VERSION WHERE VERSION_ID = v_version_id;

    ROLLBACK;
    DBMS_OUTPUT.PUT_LINE('OK: el borrador se edito y se borro sin ORA-04091.');
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('FALLO: ' || SQLERRM);
        ROLLBACK;
END;
/

-- -----------------------------------------------------------------------------
-- E. Coherencia de MFO_REP_PARAM: un parametro de origen SISTEMA no puede traer
--    ademas un CAMPO_ID. Esperado: ORA-02290 (CK_MFO_REP_PARAM_COHER).
-- -----------------------------------------------------------------------------
PROMPT === E. Parametro SISTEMA con CAMPO_ID (debe fallar)
DECLARE
    v_reporte_id NUMBER;
    v_campo_id   NUMBER;
BEGIN
    SELECT REPORTE_ID INTO v_reporte_id FROM MFO_REPORTE WHERE CLAVE = 'BM1_PDF';
    SELECT c.CAMPO_ID INTO v_campo_id
      FROM MFO_CAMPO c
      JOIN MFO_FORMULARIO f ON f.VERSION_PUBL_ID = c.VERSION_ID
     WHERE f.ALIAS = 'REP_BM1' AND c.CLAVE = 'FECHA_DESDE';

    INSERT INTO MFO_REP_PARAM (
        REP_PARAM_ID, REPORTE_ID, NOMBRE_PARAM, ORIGEN, CAMPO_ID, CLAVE_SISTEMA,
        TIPO_DATO, OBLIGATORIO, ORDEN
    ) VALUES (
        SEQ_MFO_REP_PARAM.NEXTVAL, v_reporte_id, 'Colado', 'SISTEMA', v_campo_id,
        'CODIGO_EMPRESA', 'NUMERO', 'S', 99
    );

    ROLLBACK;
    DBMS_OUTPUT.PUT_LINE('FALLO: se admitio un parametro SISTEMA con CAMPO_ID.');
EXCEPTION
    WHEN OTHERS THEN
        IF SQLCODE = -2290 THEN
            DBMS_OUTPUT.PUT_LINE('OK: ' || SQLERRM);
        ELSE
            DBMS_OUTPUT.PUT_LINE('FALLO: error inesperado -> ' || SQLERRM);
        END IF;
        ROLLBACK;
END;
/

PROMPT === Fin. Las cinco pruebas deben imprimir OK.
