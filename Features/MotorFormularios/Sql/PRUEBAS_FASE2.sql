-- =============================================================================
-- Motor de Formularios (MFO) - Fase 2 - Guion de pruebas manuales
-- Requerimiento 16.
--
-- Cubre el criterio de aceptacion de la Fase 2: crear un formulario, crear la
-- version 1, agregar 2 secciones y 6 campos con opciones y reglas, validar,
-- publicar, clonar a la version 2, modificar un campo en la 2, publicar la 2, y
-- verificar que la 1 quedo ARCHIVADA y que las condiciones de la 2 apuntan a los
-- campos de la 2 y no a los de la 1.
--
-- Esa ultima comprobacion es la razon de ser de este guion: es el unico error de
-- SP_MFO_VER_CLONE que no produce ningun sintoma visible -el formulario clonado
-- se ve bien, ninguna FK protesta- y que solo se manifiesta como ramas de logica
-- que no se activan nunca.
--
-- El guion es reejecutable: empieza borrando el formulario de prueba si quedo de
-- una corrida anterior.
-- =============================================================================

SET SERVEROUTPUT ON SIZE 1000000
SET DEFINE OFF

-- -----------------------------------------------------------------------------
-- Teardown previo.
--
-- Una version publicada no se puede borrar: es exactamente lo que garantizan los
-- triggers de inmutabilidad y esta bien que sea asi. Para limpiar datos de
-- PRUEBA hay que desactivarlos un momento.
--
-- Esto es una salida de emergencia de un guion de pruebas y no debe aparecer
-- nunca en codigo de aplicacion ni en un script de instalacion.
-- -----------------------------------------------------------------------------
PROMPT === Teardown previo del formulario de prueba
DECLARE
    v_form_id NUMBER;
BEGIN
    SELECT FORMULARIO_ID INTO v_form_id FROM MFO_FORMULARIO WHERE ALIAS = 'TEST_MFO';

    EXECUTE IMMEDIATE 'ALTER TRIGGER TRG_MFO_VER_LOCK DISABLE';
    EXECUTE IMMEDIATE 'ALTER TRIGGER TRG_MFO_SEC_LOCK DISABLE';
    EXECUTE IMMEDIATE 'ALTER TRIGGER TRG_MFO_CAMPO_LOCK DISABLE';
    EXECUTE IMMEDIATE 'ALTER TRIGGER TRG_MFO_OPCION_LOCK DISABLE';
    EXECUTE IMMEDIATE 'ALTER TRIGGER TRG_MFO_REGLA_LOCK DISABLE';
    EXECUTE IMMEDIATE 'ALTER TRIGGER TRG_MFO_COND_LOCK DISABLE';

    UPDATE MFO_FORMULARIO SET VERSION_PUBL_ID = NULL WHERE FORMULARIO_ID = v_form_id;

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

-- -----------------------------------------------------------------------------
-- Prueba completa
-- -----------------------------------------------------------------------------
PROMPT === Ciclo completo: crear, definir, validar, publicar, clonar, republicar
DECLARE
    v_form_id   NUMBER;
    v_ver1      NUMBER;
    v_ver2      NUMBER;
    v_sec_datos NUMBER;
    v_sec_extra NUMBER;
    v_id        NUMBER;
    v_msg       VARCHAR2(4000);
    v_cur       SYS_REFCURSOR;
    v_hallazgos NUMBER;
    v_errores   NUMBER;
    v_omitidas  NUMBER;
    v_n         NUMBER;
    v_estado    VARCHAR2(12);
    v_fallos    NUMBER := 0;

    PROCEDURE chk(p_Cond IN BOOLEAN, p_Texto IN VARCHAR2) IS
    BEGIN
        IF p_Cond THEN
            DBMS_OUTPUT.PUT_LINE('  OK   - ' || p_Texto);
        ELSE
            DBMS_OUTPUT.PUT_LINE('  FALLO- ' || p_Texto);
            v_fallos := v_fallos + 1;
        END IF;
    END chk;

    PROCEDURE campo(p_Sec IN NUMBER, p_Tipo IN VARCHAR2, p_Clave IN VARCHAR2,
                    p_Etiq IN VARCHAR2, p_Orden IN NUMBER, p_Req IN CHAR) IS
        v_out NUMBER; v_m VARCHAR2(4000);
    BEGIN
        SP_MFO_CAMPO_UPSERT(NULL, p_Sec, p_Tipo, p_Clave, p_Etiq, NULL, NULL,
                            p_Orden, 12, p_Req, 'N', NULL, NULL, NULL, NULL, NULL,
                            'PRUEBA', v_out, v_m);
        IF v_m <> 'success' THEN
            DBMS_OUTPUT.PUT_LINE('  FALLO- alta de campo ' || p_Clave || ': ' || v_m);
            v_fallos := v_fallos + 1;
        END IF;
    END campo;
BEGIN
    -- 1. Formulario ---------------------------------------------------------
    SP_MFO_FORM_CREATE('TEST_MFO', 'Formulario de prueba de la Fase 2', NULL, 'Pruebas',
                       13, NULL, NULL, 'S', 'CAPTURA', 'N', 'PRUEBA', v_form_id, v_msg);
    chk(v_msg = 'success' AND v_form_id > 0, 'Alta del formulario: ' || v_msg);

    -- 2. Version 1 ----------------------------------------------------------
    SP_MFO_VER_CREATE(v_form_id, 'Version inicial', 'PRUEBA', v_ver1, v_msg);
    chk(v_msg = 'success' AND v_ver1 > 0, 'Alta de la version 1: ' || v_msg);

    SP_MFO_VER_CREATE(v_form_id, 'Segundo borrador', 'PRUEBA', v_id, v_msg);
    chk(v_msg <> 'success', 'Un segundo BORRADOR simultaneo se rechaza: ' || v_msg);

    -- 3. Dos secciones ------------------------------------------------------
    SP_MFO_SEC_UPSERT(NULL, v_ver1, 'DATOS', 'Datos generales', NULL, 10, 2,
                      'N', 'N', NULL, NULL, 'N', 'PRUEBA', v_sec_datos, v_msg);
    chk(v_msg = 'success', 'Seccion DATOS: ' || v_msg);

    SP_MFO_SEC_UPSERT(NULL, v_ver1, 'EXTRA', 'Detalle repetible', NULL, 20, 1,
                      'N', 'S', 1, 5, 'N', 'PRUEBA', v_sec_extra, v_msg);
    chk(v_msg = 'success', 'Seccion EXTRA repetible: ' || v_msg);

    -- 4. Seis campos --------------------------------------------------------
    campo(v_sec_datos, 'TEXTO',        'NOMBRE',    'Nombre',        10, 'S');
    campo(v_sec_datos, 'NUMERO',       'CANTIDAD',  'Cantidad',      20, 'N');
    campo(v_sec_datos, 'FECHA',        'FECHA_SOL', 'Fecha',         30, 'N');
    campo(v_sec_datos, 'SELECT',       'TIPO_SOL',  'Tipo',          40, 'S');
    campo(v_sec_datos, 'TEXTO_LARGO',  'MOTIVO',    'Motivo',        50, 'N');
    campo(v_sec_extra, 'TEXTO',        'DETALLE',   'Detalle',       10, 'N');

    SELECT COUNT(1) INTO v_n FROM MFO_CAMPO WHERE VERSION_ID = v_ver1;
    chk(v_n = 6, 'La version 1 tiene 6 campos (tiene ' || v_n || ')');

    -- 5. Opciones del SELECT ------------------------------------------------
    SELECT CAMPO_ID INTO v_id FROM MFO_CAMPO WHERE VERSION_ID = v_ver1 AND CLAVE = 'TIPO_SOL';
    SP_MFO_OPCION_UPSERT(NULL, v_id, 'A', 'Tipo A', 10, NULL, 'S', 'S', 'PRUEBA', v_n, v_msg);
    chk(v_msg = 'success', 'Opcion A: ' || v_msg);
    SP_MFO_OPCION_UPSERT(NULL, v_id, 'B', 'Tipo B', 20, NULL, 'N', 'S', 'PRUEBA', v_n, v_msg);
    chk(v_msg = 'success', 'Opcion B: ' || v_msg);

    -- 6. Reglas -------------------------------------------------------------
    SELECT CAMPO_ID INTO v_id FROM MFO_CAMPO WHERE VERSION_ID = v_ver1 AND CLAVE = 'NOMBRE';
    SP_MFO_REGLA_UPSERT(NULL, v_id, 'LONG_MAX', '100', NULL, 'Maximo 100 caracteres.', 10, 'S',
                        'PRUEBA', v_n, v_msg);
    chk(v_msg = 'success', 'Regla LONG_MAX sobre texto: ' || v_msg);

    SP_MFO_REGLA_UPSERT(NULL, v_id, 'MIN', '1', NULL, 'No aplica.', 20, 'S', 'PRUEBA', v_n, v_msg);
    chk(v_msg <> 'success', 'Regla MIN sobre texto se rechaza: ' || v_msg);

    SELECT CAMPO_ID INTO v_id FROM MFO_CAMPO WHERE VERSION_ID = v_ver1 AND CLAVE = 'CANTIDAD';
    SP_MFO_REGLA_UPSERT(NULL, v_id, 'MIN', '1', NULL, 'Debe ser al menos 1.', 10, 'S',
                        'PRUEBA', v_n, v_msg);
    chk(v_msg = 'success', 'Regla MIN sobre numero: ' || v_msg);

    -- 7. Condicion: si TIPO_SOL = B, mostrar MOTIVO ------------------------
    SP_MFO_COND_UPSERT(NULL, v_ver1, 'MOSTRAR', 'CAMPO', 'MOTIVO', 'TIPO_SOL',
                       'IGUAL', 'B', 1, 'Y', 10, 'PRUEBA', v_id, v_msg);
    chk(v_msg = 'success', 'Condicion TIPO_SOL=B -> mostrar MOTIVO: ' || v_msg);

    SP_MFO_COND_UPSERT(NULL, v_ver1, 'MOSTRAR', 'CAMPO', 'NO_EXISTE', 'TIPO_SOL',
                       'IGUAL', 'B', 1, 'Y', 20, 'PRUEBA', v_id, v_msg);
    chk(v_msg <> 'success', 'Condicion con destino inexistente se rechaza: ' || v_msg);

    SP_MFO_COND_UPSERT(NULL, v_ver1, 'MOSTRAR', 'CAMPO', 'TIPO_SOL', 'TIPO_SOL',
                       'IGUAL', 'B', 1, 'Y', 30, 'PRUEBA', v_id, v_msg);
    chk(v_msg <> 'success', 'Condicion de un campo sobre si mismo se rechaza: ' || v_msg);

    -- 8. Validar ------------------------------------------------------------
    SP_MFO_VER_VALIDAR(v_ver1, v_cur, v_msg, v_hallazgos, v_errores);
    IF v_cur%ISOPEN THEN CLOSE v_cur; END IF;
    chk(v_msg = 'success' AND v_errores = 0,
        'Validacion de la version 1 sin errores (hallazgos=' || v_hallazgos ||
        ', errores=' || v_errores || ')');

    -- 9. Publicar -----------------------------------------------------------
    SP_MFO_VER_PUBLICAR(v_ver1, 'PRUEBA', v_msg);
    chk(v_msg = 'success', 'Publicacion de la version 1: ' || v_msg);

    SELECT ESTADO INTO v_estado FROM MFO_VERSION WHERE VERSION_ID = v_ver1;
    chk(v_estado = 'PUBLICADA', 'La version 1 quedo PUBLICADA');

    SELECT COUNT(1) INTO v_n FROM MFO_VERSION WHERE VERSION_ID = v_ver1 AND HASH_DEF IS NOT NULL;
    chk(v_n = 1, 'La version 1 tiene huella HASH_DEF');

    -- 10. La definicion publicada ya no se toca -----------------------------
    BEGIN
        UPDATE MFO_CAMPO SET ETIQUETA = 'Cambiado' WHERE VERSION_ID = v_ver1 AND CLAVE = 'NOMBRE';
        chk(FALSE, 'UPDATE sobre la version publicada debia fallar');
        ROLLBACK;
    EXCEPTION
        WHEN OTHERS THEN
            chk(SQLCODE = -20502, 'UPDATE sobre la version publicada bloqueado: ' || SQLERRM);
            ROLLBACK;
    END;

    BEGIN
        INSERT INTO MFO_CAMPO (CAMPO_ID, VERSION_ID, SECCION_ID, TIPO_CAMPO_ID, CLAVE,
                               ETIQUETA, ORDEN, ANCHO, REQUERIDO, SOLO_LECTURA)
        SELECT SEQ_MFO_CAMPO.NEXTVAL, v_ver1, v_sec_datos,
               (SELECT TIPO_CAMPO_ID FROM MFO_TIPO_CAMPO WHERE CODIGO = 'TEXTO'),
               'COLADO', 'Colado', 99, 12, 'N', 'N' FROM DUAL;
        chk(FALSE, 'INSERT sobre la version publicada debia fallar');
        ROLLBACK;
    EXCEPTION
        WHEN OTHERS THEN
            chk(SQLCODE = -20502, 'INSERT sobre la version publicada bloqueado: ' || SQLERRM);
            ROLLBACK;
    END;

    -- 11. Clonar ------------------------------------------------------------
    SP_MFO_VER_CLONE(v_ver1, 'Segunda version', 'PRUEBA', v_ver2, v_omitidas, v_msg);
    chk(v_msg = 'success' AND v_ver2 > 0, 'Clonado a la version 2: ' || v_msg);
    chk(v_omitidas = 0, 'Ninguna condicion omitida al clonar (omitidas=' || v_omitidas || ')');

    SELECT COUNT(1) INTO v_n FROM MFO_CAMPO WHERE VERSION_ID = v_ver2;
    chk(v_n = 6, 'La version 2 tiene los 6 campos clonados (tiene ' || v_n || ')');

    SELECT COUNT(1) INTO v_n FROM MFO_SECCION WHERE VERSION_ID = v_ver2;
    chk(v_n = 2, 'La version 2 tiene las 2 secciones clonadas (tiene ' || v_n || ')');

    SELECT COUNT(1) INTO v_n
      FROM MFO_OPCION O JOIN MFO_CAMPO C ON C.CAMPO_ID = O.CAMPO_ID
     WHERE C.VERSION_ID = v_ver2;
    chk(v_n = 2, 'Las 2 opciones se clonaron (hay ' || v_n || ')');

    -- Son 2: LONG_MAX sobre NOMBRE y MIN sobre CANTIDAD. La tercera que se
    -- intento (MIN sobre un campo de texto) fue rechazada a proposito.
    SELECT COUNT(1) INTO v_n
      FROM MFO_REGLA R JOIN MFO_CAMPO C ON C.CAMPO_ID = R.CAMPO_ID
     WHERE C.VERSION_ID = v_ver2;
    chk(v_n = 2, 'Las 2 reglas se clonaron (hay ' || v_n || ')');

    -- 12. LA COMPROBACION CLAVE --------------------------------------------
    --     Las condiciones de la version 2 no pueden referirse a nada de la 1.
    SELECT COUNT(1) INTO v_n FROM MFO_CONDICION WHERE VERSION_ID = v_ver2;
    chk(v_n = 1, 'La version 2 tiene 1 condicion (tiene ' || v_n || ')');

    SELECT COUNT(1) INTO v_n
      FROM MFO_CONDICION D
     WHERE D.VERSION_ID = v_ver2
       AND D.CAMPO_ORIGEN_ID IN (SELECT CAMPO_ID FROM MFO_CAMPO WHERE VERSION_ID = v_ver1);
    chk(v_n = 0, 'Ninguna condicion de la v2 apunta a un campo ORIGEN de la v1 (hay ' || v_n || ')');

    SELECT COUNT(1) INTO v_n
      FROM MFO_CONDICION D
     WHERE D.VERSION_ID = v_ver2
       AND D.DESTINO_TIPO = 'CAMPO'
       AND D.DESTINO_ID IN (SELECT CAMPO_ID FROM MFO_CAMPO WHERE VERSION_ID = v_ver1);
    chk(v_n = 0, 'Ninguna condicion de la v2 apunta a un campo DESTINO de la v1 (hay ' || v_n || ')');

    SELECT COUNT(1) INTO v_n
      FROM MFO_CONDICION D
      JOIN MFO_CAMPO CO ON CO.CAMPO_ID = D.CAMPO_ORIGEN_ID AND CO.VERSION_ID = v_ver2
      JOIN MFO_CAMPO CD ON CD.CAMPO_ID = D.DESTINO_ID      AND CD.VERSION_ID = v_ver2
     WHERE D.VERSION_ID = v_ver2
       AND CO.CLAVE = 'TIPO_SOL'
       AND CD.CLAVE = 'MOTIVO';
    chk(v_n = 1, 'La condicion clonada conserva sus claves TIPO_SOL -> MOTIVO dentro de la v2');

    -- 13. Modificar la v2 y publicarla --------------------------------------
    -- La seccion se resuelve a una variable antes de llamar: PL/SQL no admite una
    -- subconsulta como argumento de un procedimiento.
    SELECT CAMPO_ID, SECCION_ID INTO v_id, v_sec_datos
      FROM MFO_CAMPO WHERE VERSION_ID = v_ver2 AND CLAVE = 'NOMBRE';

    SP_MFO_CAMPO_UPSERT(v_id, v_sec_datos,
                        'TEXTO', 'NOMBRE', 'Nombre completo', NULL, NULL, 10, 12,
                        'S', 'N', NULL, NULL, NULL, NULL, NULL, 'PRUEBA', v_n, v_msg);
    chk(v_msg = 'success', 'Modificacion de un campo del borrador v2: ' || v_msg);

    SP_MFO_VER_PUBLICAR(v_ver2, 'PRUEBA', v_msg);
    chk(v_msg = 'success', 'Publicacion de la version 2: ' || v_msg);

    SELECT ESTADO INTO v_estado FROM MFO_VERSION WHERE VERSION_ID = v_ver1;
    chk(v_estado = 'ARCHIVADA', 'La version 1 quedo ARCHIVADA (esta ' || v_estado || ')');

    SELECT ESTADO INTO v_estado FROM MFO_VERSION WHERE VERSION_ID = v_ver2;
    chk(v_estado = 'PUBLICADA', 'La version 2 quedo PUBLICADA (esta ' || v_estado || ')');

    SELECT COUNT(1) INTO v_n
      FROM MFO_VERSION WHERE FORMULARIO_ID = v_form_id AND ESTADO = 'PUBLICADA';
    chk(v_n = 1, 'Hay exactamente una version PUBLICADA (hay ' || v_n || ')');

    SELECT VERSION_PUBL_ID INTO v_id FROM MFO_FORMULARIO WHERE FORMULARIO_ID = v_form_id;
    chk(v_id = v_ver2, 'El formulario apunta a la version 2');

    -- 14. Las huellas difieren porque la etiqueta cambio --------------------
    SELECT COUNT(1) INTO v_n
      FROM MFO_VERSION A, MFO_VERSION B
     WHERE A.VERSION_ID = v_ver1 AND B.VERSION_ID = v_ver2
       AND A.HASH_DEF <> B.HASH_DEF;
    chk(v_n = 1, 'HASH_DEF cambio entre v1 y v2 al cambiar una etiqueta');

    -- 15. Permisos ----------------------------------------------------------
    SP_MFO_PERMISO_SET(v_form_id, 'ADMIN', 'LLENAR,VER', 'PRUEBA', v_n, v_msg);
    chk(v_msg = 'success' AND v_n = 2, 'Asignacion de 2 permisos: ' || v_msg);

    SP_MFO_PERMISO_SET(v_form_id, 'ADMIN', 'INVENTADA', 'PRUEBA', v_n, v_msg);
    chk(v_msg <> 'success', 'Una accion fuera del dominio se rechaza: ' || v_msg);

    SP_MFO_PERMISO_GET(v_form_id, 'ADMIN', v_cur, v_msg, v_n);
    IF v_cur%ISOPEN THEN CLOSE v_cur; END IF;
    chk(v_msg = 'success' AND v_n = 2, 'Consulta de permisos por rol devuelve 2 (' || v_n || ')');

    SP_MFO_PERMISO_GET(v_form_id, 'ADM', v_cur, v_msg, v_n);
    IF v_cur%ISOPEN THEN CLOSE v_cur; END IF;
    chk(v_n = 0, 'El rol ADM no hace match parcial dentro de ADMIN (' || v_n || ')');

    -- 16. Definicion completa ----------------------------------------------
    DECLARE
        c1 SYS_REFCURSOR; c2 SYS_REFCURSOR; c3 SYS_REFCURSOR;
        c4 SYS_REFCURSOR; c5 SYS_REFCURSOR; c6 SYS_REFCURSOR;
    BEGIN
        SP_MFO_VER_GET_FULL(NULL, 'TEST_MFO', c1, c2, c3, c4, c5, c6, v_msg);
        IF c1%ISOPEN THEN CLOSE c1; END IF;
        IF c2%ISOPEN THEN CLOSE c2; END IF;
        IF c3%ISOPEN THEN CLOSE c3; END IF;
        IF c4%ISOPEN THEN CLOSE c4; END IF;
        IF c5%ISOPEN THEN CLOSE c5; END IF;
        IF c6%ISOPEN THEN CLOSE c6; END IF;
        chk(v_msg = 'success', 'Carga de la definicion completa por alias: ' || v_msg);
    END;

    DBMS_OUTPUT.PUT_LINE('---------------------------------------------');
    IF v_fallos = 0 THEN
        DBMS_OUTPUT.PUT_LINE('FASE 2: TODAS LAS COMPROBACIONES PASARON.');
    ELSE
        DBMS_OUTPUT.PUT_LINE('FASE 2: ' || v_fallos || ' COMPROBACION(ES) FALLARON.');
    END IF;
EXCEPTION
    WHEN OTHERS THEN
        DBMS_OUTPUT.PUT_LINE('ABORTADO: ' || SQLERRM);
        DBMS_OUTPUT.PUT_LINE(DBMS_UTILITY.FORMAT_ERROR_BACKTRACE);
END;
/

PROMPT === Fin de las pruebas de la Fase 2.
