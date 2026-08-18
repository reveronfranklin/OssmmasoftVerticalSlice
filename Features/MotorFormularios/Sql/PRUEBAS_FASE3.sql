-- =============================================================================
-- Motor de Formularios (MFO) - Fase 3 - Guion de pruebas manuales
-- Requerimiento 16.
--
-- Cubre el criterio de aceptacion de la Fase 3: sobre un formulario de prueba
-- crea un borrador, guarda valores incluyendo un multivalor y dos filas de una
-- seccion repetible, guarda otra vez los mismos valores (no duplica), envia,
-- consulta por id y recupera exactamente lo guardado, busca por valor de un
-- campo y lo encuentra, y anula. Mas la prueba de idempotencia de CLAVE_IDEM.
--
-- Se usa un formulario propio (TEST_MF3) y no REP_BM1 porque el formulario de
-- referencia no tiene seccion repetible, y sin ella no se puede ejercitar FILA.
--
-- El guion es reejecutable: empieza limpiando lo que quedo de la corrida
-- anterior. El teardown desactiva los triggers de inmutabilidad, que es la unica
-- forma de borrar una version publicada; es una salida de emergencia de pruebas
-- y no debe aparecer en codigo de aplicacion.
-- =============================================================================

SET SERVEROUTPUT ON SIZE 1000000
SET DEFINE OFF

PROMPT === Teardown previo
DECLARE
    v_form_id NUMBER;
BEGIN
    SELECT FORMULARIO_ID INTO v_form_id FROM MFO_FORMULARIO WHERE ALIAS = 'TEST_MF3';

    EXECUTE IMMEDIATE 'ALTER TRIGGER TRG_MFO_VER_LOCK DISABLE';
    EXECUTE IMMEDIATE 'ALTER TRIGGER TRG_MFO_SEC_LOCK DISABLE';
    EXECUTE IMMEDIATE 'ALTER TRIGGER TRG_MFO_CAMPO_LOCK DISABLE';
    EXECUTE IMMEDIATE 'ALTER TRIGGER TRG_MFO_OPCION_LOCK DISABLE';
    EXECUTE IMMEDIATE 'ALTER TRIGGER TRG_MFO_REGLA_LOCK DISABLE';
    EXECUTE IMMEDIATE 'ALTER TRIGGER TRG_MFO_COND_LOCK DISABLE';

    UPDATE MFO_FORMULARIO SET VERSION_PUBL_ID = NULL WHERE FORMULARIO_ID = v_form_id;

    DELETE FROM MFO_ADJUNTO WHERE VALOR_ID IN (
        SELECT VALOR_ID FROM MFO_VALOR WHERE RESPUESTA_ID IN (
            SELECT RESPUESTA_ID FROM MFO_RESPUESTA WHERE FORMULARIO_ID = v_form_id));
    DELETE FROM MFO_VALOR     WHERE RESPUESTA_ID IN (SELECT RESPUESTA_ID FROM MFO_RESPUESTA WHERE FORMULARIO_ID = v_form_id);
    DELETE FROM MFO_RESPUESTA WHERE FORMULARIO_ID = v_form_id;
    DELETE FROM MFO_CONDICION WHERE VERSION_ID IN (SELECT VERSION_ID FROM MFO_VERSION WHERE FORMULARIO_ID = v_form_id);
    DELETE FROM MFO_OPCION    WHERE CAMPO_ID   IN (SELECT CAMPO_ID FROM MFO_CAMPO WHERE VERSION_ID IN (SELECT VERSION_ID FROM MFO_VERSION WHERE FORMULARIO_ID = v_form_id));
    DELETE FROM MFO_REGLA     WHERE CAMPO_ID   IN (SELECT CAMPO_ID FROM MFO_CAMPO WHERE VERSION_ID IN (SELECT VERSION_ID FROM MFO_VERSION WHERE FORMULARIO_ID = v_form_id));
    DELETE FROM MFO_CAMPO     WHERE VERSION_ID IN (SELECT VERSION_ID FROM MFO_VERSION WHERE FORMULARIO_ID = v_form_id);
    DELETE FROM MFO_SECCION   WHERE VERSION_ID IN (SELECT VERSION_ID FROM MFO_VERSION WHERE FORMULARIO_ID = v_form_id);
    DELETE FROM MFO_VERSION   WHERE FORMULARIO_ID = v_form_id;
    DELETE FROM MFO_PERMISO   WHERE FORMULARIO_ID = v_form_id;
    DELETE FROM MFO_FORMULARIO WHERE FORMULARIO_ID = v_form_id;
    COMMIT;

    DBMS_OUTPUT.PUT_LINE('Datos de prueba anteriores eliminados.');
EXCEPTION
    WHEN NO_DATA_FOUND THEN
        DBMS_OUTPUT.PUT_LINE('No habia datos de prueba anteriores.');
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('Teardown: ' || SQLERRM);
END;
/

BEGIN
    EXECUTE IMMEDIATE 'ALTER TRIGGER TRG_MFO_VER_LOCK ENABLE';
    EXECUTE IMMEDIATE 'ALTER TRIGGER TRG_MFO_SEC_LOCK ENABLE';
    EXECUTE IMMEDIATE 'ALTER TRIGGER TRG_MFO_CAMPO_LOCK ENABLE';
    EXECUTE IMMEDIATE 'ALTER TRIGGER TRG_MFO_OPCION_LOCK ENABLE';
    EXECUTE IMMEDIATE 'ALTER TRIGGER TRG_MFO_REGLA_LOCK ENABLE';
    EXECUTE IMMEDIATE 'ALTER TRIGGER TRG_MFO_COND_LOCK ENABLE';
    DBMS_OUTPUT.PUT_LINE('Triggers de inmutabilidad reactivados.');
END;
/

PROMPT === Ciclo de datos: borrador, valores, envio, consulta, busqueda, anulacion
DECLARE
    v_form_id  NUMBER;
    v_ver      NUMBER;
    v_sec_dat  NUMBER;
    v_sec_lin  NUMBER;
    v_campo    NUMBER;
    v_id       NUMBER;
    v_msg      VARCHAR2(4000);
    v_n        NUMBER;
    v_estado   VARCHAR2(12);
    v_fallos   NUMBER := 0;

    v_resp1    NUMBER;
    v_resp1b   NUMBER;
    v_resp2    NUMBER;
    v_verout   NUMBER;
    v_guard    NUMBER;
    v_campoerr VARCHAR2(30);
    v_huerf    NUMBER;
    v_total    NUMBER;
    v_paginas  NUMBER;
    v_cur      SYS_REFCURSOR;
    v_cur2     SYS_REFCURSOR;

    v_claves   PKG_MFO_ARRAYS.t_cla;
    v_filas    PKG_MFO_ARRAYS.t_num;
    v_ordenes  PKG_MFO_ARRAYS.t_num;
    v_txts     PKG_MFO_ARRAYS.t_txt;
    v_nums     PKG_MFO_ARRAYS.t_num;
    v_fecs     PKG_MFO_ARRAYS.t_fec;
    v_etis     PKG_MFO_ARRAYS.t_eti;

    PROCEDURE chk(p_Cond IN BOOLEAN, p_Texto IN VARCHAR2) IS
    BEGIN
        IF p_Cond THEN
            DBMS_OUTPUT.PUT_LINE('  OK   - ' || p_Texto);
        ELSE
            DBMS_OUTPUT.PUT_LINE('  FALLO- ' || p_Texto);
            v_fallos := v_fallos + 1;
        END IF;
    END chk;

    -- Carga el lote de valores que se usa dos veces, para probar idempotencia.
    PROCEDURE cargar_lote IS
    BEGIN
        v_claves.DELETE; v_filas.DELETE; v_ordenes.DELETE;
        v_txts.DELETE;   v_nums.DELETE;  v_fecs.DELETE; v_etis.DELETE;

        -- Campo simple
        v_claves(1) := 'CEDULA';   v_filas(1) := 0; v_ordenes(1) := 0;
        v_txts(1)   := 'V-12345678'; v_nums(1) := NULL; v_fecs(1) := NULL; v_etis(1) := NULL;

        -- Multivalor: dos valores del mismo campo, distinto ORDEN_VAL
        v_claves(2) := 'INTERESES'; v_filas(2) := 0; v_ordenes(2) := 1;
        v_txts(2)   := 'DEPORTE';   v_nums(2) := NULL; v_fecs(2) := NULL; v_etis(2) := 'Deporte';

        v_claves(3) := 'INTERESES'; v_filas(3) := 0; v_ordenes(3) := 2;
        v_txts(3)   := 'MUSICA';    v_nums(3) := NULL; v_fecs(3) := NULL; v_etis(3) := 'Musica';

        -- Seccion repetible: dos filas
        v_claves(4) := 'DETALLE';   v_filas(4) := 1; v_ordenes(4) := 0;
        v_txts(4)   := 'Primera linea'; v_nums(4) := NULL; v_fecs(4) := NULL; v_etis(4) := NULL;

        v_claves(5) := 'DETALLE';   v_filas(5) := 2; v_ordenes(5) := 0;
        v_txts(5)   := 'Segunda linea'; v_nums(5) := NULL; v_fecs(5) := NULL; v_etis(5) := NULL;
    END cargar_lote;
BEGIN
    -- ---------------------------------------------------------------------
    -- Preparacion: formulario con multivalor y seccion repetible
    -- ---------------------------------------------------------------------
    SP_MFO_FORM_CREATE('TEST_MF3', 'Formulario de prueba de la Fase 3', NULL, 'Pruebas',
                       13, 'PRUEBA_ENT', NULL, 'S', 'CAPTURA', 'N', 'PRUEBA', v_form_id, v_msg);
    chk(v_msg = 'success', 'Formulario creado: ' || v_msg);

    SP_MFO_VER_CREATE(v_form_id, 'v1', 'PRUEBA', v_ver, v_msg);
    chk(v_msg = 'success', 'Version 1 creada: ' || v_msg);

    SP_MFO_SEC_UPSERT(NULL, v_ver, 'DATOS', 'Datos', NULL, 10, 1,
                      'N', 'N', NULL, NULL, 'N', 'PRUEBA', v_sec_dat, v_msg);
    SP_MFO_SEC_UPSERT(NULL, v_ver, 'LINEAS', 'Lineas', NULL, 20, 1,
                      'N', 'S', 1, 5, 'N', 'PRUEBA', v_sec_lin, v_msg);
    chk(v_msg = 'success', 'Secciones creadas (una repetible)');

    SP_MFO_CAMPO_UPSERT(NULL, v_sec_dat, 'TEXTO', 'CEDULA', 'Cedula', NULL, NULL,
                        10, 12, 'S', 'N', NULL, NULL, NULL, NULL, NULL, 'PRUEBA', v_campo, v_msg);
    chk(v_msg = 'success', 'Campo CEDULA: ' || v_msg);

    SP_MFO_REGLA_UPSERT(NULL, v_campo, 'UNICO', 'FORMULARIO', NULL,
                        'Esa cedula ya fue registrada.', 10, 'S', 'PRUEBA', v_id, v_msg);
    chk(v_msg = 'success', 'Regla UNICO sobre CEDULA: ' || v_msg);

    SP_MFO_CAMPO_UPSERT(NULL, v_sec_dat, 'MULTI_SELECT', 'INTERESES', 'Intereses', NULL, NULL,
                        20, 12, 'N', 'N', NULL, NULL, NULL, NULL, NULL, 'PRUEBA', v_campo, v_msg);
    SP_MFO_OPCION_UPSERT(NULL, v_campo, 'DEPORTE', 'Deporte', 10, NULL, 'N', 'S', 'PRUEBA', v_id, v_msg);
    SP_MFO_OPCION_UPSERT(NULL, v_campo, 'MUSICA',  'Musica',  20, NULL, 'N', 'S', 'PRUEBA', v_id, v_msg);
    chk(v_msg = 'success', 'Campo INTERESES multivalor con 2 opciones');

    SP_MFO_CAMPO_UPSERT(NULL, v_sec_lin, 'TEXTO', 'DETALLE', 'Detalle', NULL, NULL,
                        10, 12, 'N', 'N', NULL, NULL, NULL, NULL, NULL, 'PRUEBA', v_campo, v_msg);
    chk(v_msg = 'success', 'Campo DETALLE en la seccion repetible');

    SP_MFO_VER_PUBLICAR(v_ver, 'PRUEBA', v_msg);
    chk(v_msg = 'success', 'Version 1 publicada: ' || v_msg);

    -- ---------------------------------------------------------------------
    -- 1. Idempotencia de CLAVE_IDEM
    -- ---------------------------------------------------------------------
    SP_MFO_RESP_CREATE('TEST_MF3', 'IDEM-0001', 'PRUEBA_ENT', 'REF-1',
                       'usuario1', '10.0.0.1', v_resp1, v_verout, v_msg);
    chk(v_msg = 'success' AND v_resp1 > 0, 'Borrador creado: ' || v_msg);
    chk(v_verout = v_ver, 'El borrador quedo amarrado a la version publicada');

    SP_MFO_RESP_CREATE('TEST_MF3', 'IDEM-0001', 'PRUEBA_ENT', 'REF-1',
                       'usuario1', '10.0.0.1', v_resp1b, v_verout, v_msg);
    chk(v_msg = 'success' AND v_resp1b = v_resp1,
        'Dos CREATE con la misma CLAVE_IDEM devuelven la misma respuesta');

    SELECT COUNT(1) INTO v_n FROM MFO_RESPUESTA WHERE FORMULARIO_ID = v_form_id;
    chk(v_n = 1, 'Solo existe una respuesta (hay ' || v_n || ')');

    -- ---------------------------------------------------------------------
    -- 2. Guardado de valores: multivalor y seccion repetible
    -- ---------------------------------------------------------------------
    cargar_lote;
    SP_MFO_RESP_VAL_SAVE(v_resp1, v_claves, v_filas, v_ordenes, v_txts, v_nums, v_fecs, v_etis,
                         NULL, NULL, NULL, NULL, 'usuario1', v_guard, v_msg);
    chk(v_msg = 'success' AND v_guard = 5, 'Se guardaron 5 valores (' || v_guard || '): ' || v_msg);

    SELECT COUNT(1) INTO v_n FROM MFO_VALOR WHERE RESPUESTA_ID = v_resp1;
    chk(v_n = 5, 'MFO_VALOR tiene 5 filas (' || v_n || ')');

    SELECT COUNT(1) INTO v_n FROM MFO_VALOR WHERE RESPUESTA_ID = v_resp1 AND CLAVE_CAMPO = 'INTERESES';
    chk(v_n = 2, 'El multivalor guardo 2 valores (' || v_n || ')');

    SELECT COUNT(DISTINCT FILA) INTO v_n FROM MFO_VALOR WHERE RESPUESTA_ID = v_resp1 AND CLAVE_CAMPO = 'DETALLE';
    chk(v_n = 2, 'La seccion repetible guardo 2 filas (' || v_n || ')');

    -- El valor de texto fue a VALOR_TXT y no a otra columna.
    SELECT COUNT(1) INTO v_n FROM MFO_VALOR
     WHERE RESPUESTA_ID = v_resp1 AND CLAVE_CAMPO = 'CEDULA'
       AND VALOR_TXT = 'V-12345678' AND VALOR_NUM IS NULL AND VALOR_FEC IS NULL;
    chk(v_n = 1, 'El valor de texto se escribio en VALOR_TXT y solo ahi');

    -- ---------------------------------------------------------------------
    -- 3. Idempotencia del guardado
    -- ---------------------------------------------------------------------
    cargar_lote;
    SP_MFO_RESP_VAL_SAVE(v_resp1, v_claves, v_filas, v_ordenes, v_txts, v_nums, v_fecs, v_etis,
                         NULL, NULL, NULL, NULL, 'usuario1', v_guard, v_msg);
    chk(v_msg = 'success', 'Segundo guardado del mismo lote: ' || v_msg);

    SELECT COUNT(1) INTO v_n FROM MFO_VALOR WHERE RESPUESTA_ID = v_resp1;
    chk(v_n = 5, 'Sigue habiendo 5 valores, no 10 (' || v_n || ')');

    -- ---------------------------------------------------------------------
    -- 4. Rechazos estructurales
    -- ---------------------------------------------------------------------
    v_claves.DELETE; v_filas.DELETE; v_ordenes.DELETE;
    v_txts.DELETE; v_nums.DELETE; v_fecs.DELETE; v_etis.DELETE;
    v_claves(1) := 'NO_EXISTE'; v_filas(1) := 0; v_ordenes(1) := 0;
    v_txts(1) := 'x'; v_nums(1) := NULL; v_fecs(1) := NULL; v_etis(1) := NULL;
    SP_MFO_RESP_VAL_SAVE(v_resp1, v_claves, v_filas, v_ordenes, v_txts, v_nums, v_fecs, v_etis,
                         NULL, NULL, NULL, NULL, 'usuario1', v_guard, v_msg);
    chk(v_msg <> 'success', 'Una clave inexistente se rechaza: ' || v_msg);

    v_claves(1) := 'CEDULA'; v_filas(1) := 3; v_ordenes(1) := 0;
    SP_MFO_RESP_VAL_SAVE(v_resp1, v_claves, v_filas, v_ordenes, v_txts, v_nums, v_fecs, v_etis,
                         NULL, NULL, NULL, NULL, 'usuario1', v_guard, v_msg);
    chk(v_msg <> 'success', 'FILA>0 en seccion no repetible se rechaza: ' || v_msg);

    v_claves(1) := 'CEDULA'; v_filas(1) := 0; v_ordenes(1) := 2;
    SP_MFO_RESP_VAL_SAVE(v_resp1, v_claves, v_filas, v_ordenes, v_txts, v_nums, v_fecs, v_etis,
                         NULL, NULL, NULL, NULL, 'usuario1', v_guard, v_msg);
    chk(v_msg <> 'success', 'ORDEN_VAL>0 en campo no multivalor se rechaza: ' || v_msg);

    SELECT COUNT(1) INTO v_n FROM MFO_VALOR WHERE RESPUESTA_ID = v_resp1;
    chk(v_n = 5, 'Los rechazos no dejaron basura (' || v_n || ' valores)');

    -- ---------------------------------------------------------------------
    -- 5. Envio
    -- ---------------------------------------------------------------------
    SP_MFO_RESP_SUBMIT(v_resp1, TO_CLOB('{"payload":"crudo"}'), 'usuario1', '10.0.0.1',
                       v_campoerr, v_msg);
    chk(v_msg = 'success', 'Envio de la respuesta: ' || v_msg);

    SELECT ESTADO INTO v_estado FROM MFO_RESPUESTA WHERE RESPUESTA_ID = v_resp1;
    chk(v_estado = 'ENVIADA', 'La respuesta quedo ENVIADA (esta ' || v_estado || ')');

    cargar_lote;
    SP_MFO_RESP_VAL_SAVE(v_resp1, v_claves, v_filas, v_ordenes, v_txts, v_nums, v_fecs, v_etis,
                         NULL, NULL, NULL, NULL, 'usuario1', v_guard, v_msg);
    chk(v_msg <> 'success', 'Una respuesta enviada ya no admite cambios: ' || v_msg);

    -- ---------------------------------------------------------------------
    -- 6. Consulta por id
    -- ---------------------------------------------------------------------
    SP_MFO_RESP_GET_BY_ID(v_resp1, v_cur, v_cur2, v_msg);
    IF v_cur%ISOPEN  THEN CLOSE v_cur;  END IF;
    IF v_cur2%ISOPEN THEN CLOSE v_cur2; END IF;
    chk(v_msg = 'success', 'Consulta por id: ' || v_msg);

    -- ---------------------------------------------------------------------
    -- 7. Busqueda por valor de campo
    -- ---------------------------------------------------------------------
    SP_MFO_RESP_SEARCH(13, 'TEST_MF3', NULL, NULL, NULL, NULL, NULL, NULL,
                       'CEDULA', 'V-12345678', 1, 25, v_cur, v_msg, v_total, v_paginas);
    IF v_cur%ISOPEN THEN CLOSE v_cur; END IF;
    chk(v_msg = 'success' AND v_total = 1, 'Busqueda por valor de campo encuentra 1 (' || v_total || ')');

    SP_MFO_RESP_SEARCH(13, 'TEST_MF3', NULL, NULL, NULL, NULL, NULL, NULL,
                       'CEDULA', 'V-99999999', 1, 25, v_cur, v_msg, v_total, v_paginas);
    IF v_cur%ISOPEN THEN CLOSE v_cur; END IF;
    chk(v_total = 0, 'Busqueda por un valor que no existe devuelve 0 (' || v_total || ')');

    SP_MFO_RESP_SEARCH(13, 'TEST_MF3', NULL, NULL, NULL, NULL, 'PRUEBA_ENT', 'REF-1',
                       NULL, NULL, 1, 25, v_cur, v_msg, v_total, v_paginas);
    IF v_cur%ISOPEN THEN CLOSE v_cur; END IF;
    chk(v_total = 1, 'Busqueda por entidad de negocio encuentra 1 (' || v_total || ')');

    -- ---------------------------------------------------------------------
    -- 8. Regla UNICO
    -- ---------------------------------------------------------------------
    SP_MFO_RESP_CREATE('TEST_MF3', 'IDEM-0002', NULL, NULL,
                       'usuario2', '10.0.0.2', v_resp2, v_verout, v_msg);
    chk(v_msg = 'success', 'Segundo borrador creado: ' || v_msg);

    v_claves.DELETE; v_filas.DELETE; v_ordenes.DELETE;
    v_txts.DELETE; v_nums.DELETE; v_fecs.DELETE; v_etis.DELETE;
    v_claves(1) := 'CEDULA'; v_filas(1) := 0; v_ordenes(1) := 0;
    v_txts(1) := 'V-12345678'; v_nums(1) := NULL; v_fecs(1) := NULL; v_etis(1) := NULL;
    SP_MFO_RESP_VAL_SAVE(v_resp2, v_claves, v_filas, v_ordenes, v_txts, v_nums, v_fecs, v_etis,
                         NULL, NULL, NULL, NULL, 'usuario2', v_guard, v_msg);
    chk(v_msg = 'success', 'Segundo borrador con la misma cedula se guarda');

    SP_MFO_RESP_SUBMIT(v_resp2, NULL, 'usuario2', '10.0.0.2', v_campoerr, v_msg);
    chk(v_msg <> 'success' AND v_campoerr = 'CEDULA',
        'El envio se rechaza por la regla UNICO, señalando CEDULA: ' || v_msg);

    -- ---------------------------------------------------------------------
    -- 9. Anulacion libera la unicidad
    -- ---------------------------------------------------------------------
    SP_MFO_RESP_ANULAR(v_resp1, NULL, 'PRUEBA', v_msg);
    chk(v_msg <> 'success', 'Anular sin motivo se rechaza: ' || v_msg);

    SP_MFO_RESP_ANULAR(v_resp1, 'Cargada por error', 'PRUEBA', v_msg);
    chk(v_msg = 'success', 'Anulacion con motivo: ' || v_msg);

    SELECT ESTADO INTO v_estado FROM MFO_RESPUESTA WHERE RESPUESTA_ID = v_resp1;
    chk(v_estado = 'ANULADA', 'La respuesta quedo ANULADA (esta ' || v_estado || ')');

    SELECT COUNT(1) INTO v_n FROM MFO_VALOR WHERE RESPUESTA_ID = v_resp1;
    chk(v_n = 5, 'Anular conserva los valores (' || v_n || ')');

    SP_MFO_RESP_SUBMIT(v_resp2, NULL, 'usuario2', '10.0.0.2', v_campoerr, v_msg);
    chk(v_msg = 'success', 'Tras anular la primera, la segunda si se puede enviar: ' || v_msg);

    -- ---------------------------------------------------------------------
    -- 10. Borrado
    -- ---------------------------------------------------------------------
    SP_MFO_RESP_DELETE(v_resp2, 'PRUEBA', v_huerf, v_msg);
    chk(v_msg <> 'success', 'No se puede borrar una respuesta enviada: ' || v_msg);

    SP_MFO_RESP_CREATE('TEST_MF3', 'IDEM-0003', NULL, NULL, 'usuario3', NULL,
                       v_id, v_verout, v_msg);
    SP_MFO_RESP_DELETE(v_id, 'PRUEBA', v_huerf, v_msg);
    chk(v_msg = 'success', 'Un borrador si se puede borrar: ' || v_msg);

    -- ---------------------------------------------------------------------
    -- 11. Exportacion en formato largo
    -- ---------------------------------------------------------------------
    SP_MFO_RESP_EXPORT(13, 'TEST_MF3', NULL, NULL, NULL, v_cur, v_msg, v_total);
    IF v_cur%ISOPEN THEN CLOSE v_cur; END IF;
    chk(v_msg = 'success' AND v_total > 0, 'Exportacion larga devuelve ' || v_total || ' filas');

    -- ---------------------------------------------------------------------
    -- 12. Bitacora
    -- ---------------------------------------------------------------------
    SP_MFO_AUD_INS('RESPUESTA', v_resp1, 'CONSULTAR', 'PRUEBA', TO_CLOB('desde el guion'), v_id, v_msg);
    chk(v_msg = 'success' AND v_id > 0, 'Entrada de bitacora: ' || v_msg);

    DBMS_OUTPUT.PUT_LINE('---------------------------------------------');
    IF v_fallos = 0 THEN
        DBMS_OUTPUT.PUT_LINE('FASE 3: TODAS LAS COMPROBACIONES PASARON.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('FASE 3: ' || v_fallos || ' COMPROBACION(ES) FALLARON.');
    END IF;
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('ABORTADO: ' || SQLERRM);
        DBMS_OUTPUT.PUT_LINE(DBMS_UTILITY.FORMAT_ERROR_BACKTRACE);
END;
/

PROMPT === Fin de las pruebas de la Fase 3.
