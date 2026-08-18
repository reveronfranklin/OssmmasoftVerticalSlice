-- =============================================================================
-- MFO - Publicar una version.
--
-- Todo en una transaccion: valida, calcula la huella, archiva la version
-- publicada anterior, mueve el puntero del formulario y audita. Si algo falla,
-- no queda un formulario a medio publicar.
--
-- El orden importa: primero se archiva la anterior y despues se publica la
-- nueva. IDX_MFO_VER_PUBL_UNQ se evalua por sentencia, asi que invertir el orden
-- haria fallar la publicacion con ORA-00001 aunque el estado final fuera valido.
--
-- Sobre HASH_DEF: se calcula sobre las CLAVE y no sobre los IDs, de modo que dos
-- versiones estructuralmente identicas producen la misma huella aunque sus IDs
-- difieran. Eso es lo que permite responder "esta version cambio algo respecto
-- de la anterior" sin comparar fila por fila.
--
-- No es un hash criptografico: Oracle 10g no ofrece SHA-256 en DBMS_CRYPTO -solo
-- MD5 y SHA-1- y usar DBMS_CRYPTO exigiria un GRANT sobre un paquete de SYS para
-- una salvaguarda secundaria. La garantia real de inmutabilidad son los
-- triggers, no esta huella; aqui basta con detectar cambios, y para eso se
-- combinan dos semillas distintas de DBMS_UTILITY.GET_HASH_VALUE.
-- =============================================================================
CREATE OR REPLACE PROCEDURE MFO.SP_MFO_VER_PUBLICAR (
    p_VersionId IN  NUMBER,
    p_Usuario   IN  VARCHAR2,
    p_Message   OUT VARCHAR2
) AS
    v_formulario_id NUMBER;
    v_estado        MFO_VERSION.ESTADO%TYPE;
    v_numero        NUMBER;

    v_cur           SYS_REFCURSOR;
    v_msg_val       VARCHAR2(4000);
    v_hallazgos     NUMBER;
    v_errores       NUMBER;

    v_h1            NUMBER := 0;
    v_h2            NUMBER := 0;
    v_txt           VARCHAR2(4000);
    v_hash          VARCHAR2(64);

    PROCEDURE mezclar(p_Texto IN VARCHAR2) IS
    BEGIN
        v_h1 := DBMS_UTILITY.GET_HASH_VALUE(TO_CHAR(v_h1) || '|' || p_Texto, 1, 1073741824);
        v_h2 := DBMS_UTILITY.GET_HASH_VALUE(TO_CHAR(v_h2) || '#' || p_Texto, 37, 1073741824);
    END mezclar;
BEGIN
    BEGIN
        SELECT FORMULARIO_ID, ESTADO, NUMERO
          INTO v_formulario_id, v_estado, v_numero
          FROM MFO_VERSION WHERE VERSION_ID = p_VersionId;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            p_Message := 'La version indicada no existe.';
            RETURN;
    END;

    IF v_estado <> 'BORRADOR' THEN
        p_Message := 'Solo se puede publicar una version en BORRADOR. Esta version esta ' || v_estado || '.';
        RETURN;
    END IF;

    -- ------------------------------------------------------------------------
    -- Validacion. Se reutiliza el mismo procedimiento que consume el diseñador,
    -- para que no existan dos definiciones de "valido" que puedan divergir.
    -- ------------------------------------------------------------------------
    SP_MFO_VER_VALIDAR(p_VersionId, v_cur, v_msg_val, v_hallazgos, v_errores);
    IF v_cur%ISOPEN THEN
        CLOSE v_cur;
    END IF;

    IF v_msg_val <> 'success' THEN
        p_Message := 'No se pudo validar la version: ' || v_msg_val;
        RETURN;
    END IF;

    IF v_errores > 0 THEN
        p_Message := 'La version tiene ' || v_errores ||
                     ' error(es) de validacion. Corrijalos antes de publicar.';
        RETURN;
    END IF;

    -- ------------------------------------------------------------------------
    -- Huella de la definicion. Recorrido en orden estable por CLAVE.
    -- ------------------------------------------------------------------------
    FOR s IN (SELECT CLAVE, TITULO, ORDEN, COLUMNAS, ES_PASO, REPETIBLE,
                     MIN_FILAS, MAX_FILAS, COLAPSABLE
                FROM MFO_SECCION WHERE VERSION_ID = p_VersionId ORDER BY CLAVE) LOOP
        v_txt := 'S:' || s.CLAVE || ':' || s.TITULO || ':' || s.ORDEN || ':' || s.COLUMNAS ||
                 ':' || s.ES_PASO || ':' || s.REPETIBLE || ':' || NVL(TO_CHAR(s.MIN_FILAS), '-') ||
                 ':' || NVL(TO_CHAR(s.MAX_FILAS), '-') || ':' || s.COLAPSABLE;
        mezclar(v_txt);
    END LOOP;

    FOR c IN (SELECT C.CLAVE, C.ETIQUETA, C.ORDEN, C.ANCHO, C.REQUERIDO, C.SOLO_LECTURA,
                     C.VALOR_DEFECTO, C.ORIGEN_OPCIONES, C.CATALOGO_CLAVE, C.MASCARA,
                     C.UNIDAD, T.CODIGO TIPO, S.CLAVE CLAVE_SEC
                FROM MFO_CAMPO C
                JOIN MFO_TIPO_CAMPO T ON T.TIPO_CAMPO_ID = C.TIPO_CAMPO_ID
                JOIN MFO_SECCION S    ON S.SECCION_ID = C.SECCION_ID
               WHERE C.VERSION_ID = p_VersionId ORDER BY C.CLAVE) LOOP
        v_txt := 'C:' || c.CLAVE || ':' || c.CLAVE_SEC || ':' || c.TIPO || ':' || c.ETIQUETA ||
                 ':' || c.ORDEN || ':' || c.ANCHO || ':' || c.REQUERIDO || ':' || c.SOLO_LECTURA ||
                 ':' || NVL(c.VALOR_DEFECTO, '-') || ':' || NVL(c.ORIGEN_OPCIONES, '-') ||
                 ':' || NVL(c.CATALOGO_CLAVE, '-') || ':' || NVL(c.MASCARA, '-') ||
                 ':' || NVL(c.UNIDAD, '-');
        mezclar(v_txt);
    END LOOP;

    FOR o IN (SELECT C.CLAVE CLAVE_CAMPO, O.VALOR, O.ETIQUETA, O.ORDEN, O.ES_DEFECTO, O.ACTIVO
                FROM MFO_OPCION O
                JOIN MFO_CAMPO C ON C.CAMPO_ID = O.CAMPO_ID
               WHERE C.VERSION_ID = p_VersionId ORDER BY C.CLAVE, O.VALOR) LOOP
        v_txt := 'O:' || o.CLAVE_CAMPO || ':' || o.VALOR || ':' || o.ETIQUETA ||
                 ':' || o.ORDEN || ':' || o.ES_DEFECTO || ':' || o.ACTIVO;
        mezclar(v_txt);
    END LOOP;

    FOR g IN (SELECT C.CLAVE CLAVE_CAMPO, G.TIPO_REGLA, G.PARAM_1, G.PARAM_2, G.MENSAJE, G.ACTIVO
                FROM MFO_REGLA G
                JOIN MFO_CAMPO C ON C.CAMPO_ID = G.CAMPO_ID
               WHERE C.VERSION_ID = p_VersionId ORDER BY C.CLAVE, G.TIPO_REGLA, G.ORDEN) LOOP
        v_txt := 'R:' || g.CLAVE_CAMPO || ':' || g.TIPO_REGLA || ':' || NVL(g.PARAM_1, '-') ||
                 ':' || NVL(g.PARAM_2, '-') || ':' || g.MENSAJE || ':' || g.ACTIVO;
        mezclar(v_txt);
    END LOOP;

    -- Las condiciones se identifican por las CLAVE de sus dos extremos, no por
    -- sus IDs: es lo que hace que la huella sobreviva a un clonado.
    FOR d IN (SELECT CO.CLAVE CLAVE_ORIGEN,
                     CASE WHEN D.DESTINO_TIPO = 'CAMPO'
                          THEN (SELECT X.CLAVE FROM MFO_CAMPO X WHERE X.CAMPO_ID = D.DESTINO_ID)
                          ELSE (SELECT Y.CLAVE FROM MFO_SECCION Y WHERE Y.SECCION_ID = D.DESTINO_ID)
                     END CLAVE_DESTINO,
                     D.ACCION, D.DESTINO_TIPO, D.OPERADOR, D.VALOR_COMPARA, D.GRUPO, D.CONECTOR, D.ORDEN
                FROM MFO_CONDICION D
                JOIN MFO_CAMPO CO ON CO.CAMPO_ID = D.CAMPO_ORIGEN_ID
               WHERE D.VERSION_ID = p_VersionId
               ORDER BY D.GRUPO, D.ORDEN, CO.CLAVE) LOOP
        v_txt := 'D:' || d.CLAVE_ORIGEN || ':' || d.DESTINO_TIPO || ':' || NVL(d.CLAVE_DESTINO, '-') ||
                 ':' || d.ACCION || ':' || d.OPERADOR || ':' || NVL(d.VALOR_COMPARA, '-') ||
                 ':' || d.GRUPO || ':' || d.CONECTOR || ':' || d.ORDEN;
        mezclar(v_txt);
    END LOOP;

    v_hash := LPAD(TO_CHAR(v_h1, 'FMXXXXXXXX'), 8, '0') ||
              LPAD(TO_CHAR(v_h2, 'FMXXXXXXXX'), 8, '0');

    -- ------------------------------------------------------------------------
    -- Archivar la publicada anterior ANTES de publicar la nueva.
    -- ------------------------------------------------------------------------
    UPDATE MFO_VERSION
       SET ESTADO     = 'ARCHIVADA',
           FECHA_ARCH = SYSDATE
     WHERE FORMULARIO_ID = v_formulario_id
       AND ESTADO = 'PUBLICADA';

    UPDATE MFO_VERSION
       SET ESTADO       = 'PUBLICADA',
           HASH_DEF     = v_hash,
           FECHA_PUBL   = SYSDATE,
           USUARIO_PUBL = p_Usuario,
           USUARIO_UPD  = p_Usuario,
           FECHA_UPD    = SYSDATE
     WHERE VERSION_ID = p_VersionId;

    UPDATE MFO_FORMULARIO
       SET VERSION_PUBL_ID = p_VersionId,
           USUARIO_UPD     = p_Usuario,
           FECHA_UPD       = SYSDATE
     WHERE FORMULARIO_ID = v_formulario_id;

    INSERT INTO MFO_AUDITORIA (AUDITORIA_ID, ENTIDAD, ENTIDAD_ID, ACCION, USUARIO, FECHA, DETALLE_CLB)
    VALUES (SEQ_MFO_AUDITORIA.NEXTVAL, 'VERSION', p_VersionId, 'PUBLICAR', p_Usuario, SYSDATE,
            'Version ' || v_numero || '. Hallazgos: ' || v_hallazgos || '. Huella: ' || v_hash);

    COMMIT;
    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        IF v_cur%ISOPEN THEN
            CLOSE v_cur;
        END IF;
        p_Message := 'Error tecnico: ' || SQLERRM;
END;
/
