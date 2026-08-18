-- =============================================================================
-- MFO - Guardar los valores de una respuesta.
--
-- Este es el procedimiento que decide, valor por valor, en cual de las cuatro
-- columnas tipadas de MFO_VALOR se escribe. Esa decision la dicta
-- MFO_TIPO_CAMPO.COLUMNA_VALOR y no el cliente: si el cliente pudiera elegir la
-- columna, el EAV tipado dejaria de estar tipado.
--
-- Idempotente por MERGE sobre UK_MFO_VALOR_UBIC (RESPUESTA_ID, CAMPO_ID, FILA,
-- ORDEN_VAL). Guardar dos veces el mismo autoguardado no duplica nada, que es
-- justo lo que hace falta cuando el renderizador reintenta.
--
-- Los valores llegan en arreglos asociativos (ver 14_MFO_PKG_ARRAYS.sql). Los
-- textos de tipo CLB mas largos que 4000 caracteres no caben en un elemento del
-- arreglo y viajan por los cuatro parametros p_Clob*, uno por llamada.
--
-- La validacion de negocio (reglas, condiciones, obligatoriedad) NO esta aqui:
-- vive en el motor C# de la Fase 5, que es su unica autoridad. Aqui solo se
-- comprueba lo estructural, que es lo que protege la integridad de la tabla y no
-- puede delegarse: que la clave exista en la version, que el campo capture
-- datos, que no sea de solo lectura, y que FILA y ORDEN_VAL solo se usen donde
-- el modelo los admite.
-- =============================================================================
CREATE OR REPLACE PROCEDURE MFO.SP_MFO_RESP_VAL_SAVE (
    p_RespuestaId IN  NUMBER,
    p_Claves      IN  PKG_MFO_ARRAYS.t_cla,
    p_Filas       IN  PKG_MFO_ARRAYS.t_num,
    p_Ordenes     IN  PKG_MFO_ARRAYS.t_num,
    p_ValoresTxt  IN  PKG_MFO_ARRAYS.t_txt,
    p_ValoresNum  IN  PKG_MFO_ARRAYS.t_num,
    p_ValoresFec  IN  PKG_MFO_ARRAYS.t_fec,
    p_Etiquetas   IN  PKG_MFO_ARRAYS.t_eti,
    p_ClobClave   IN  VARCHAR2,
    p_ClobFila    IN  NUMBER,
    p_ClobOrden   IN  NUMBER,
    p_ClobValor   IN  CLOB,
    p_Usuario     IN  VARCHAR2,
    p_Guardados   OUT NUMBER,
    p_Message     OUT VARCHAR2
) AS
    v_version_id NUMBER;
    v_estado     VARCHAR2(12);
    i            BINARY_INTEGER;

    -- Datos del campo resueltos por clave
    v_campo_id   NUMBER;
    v_columna    VARCHAR2(3);
    v_presenta   CHAR(1);
    v_multiple   CHAR(1);
    v_solo_lec   CHAR(1);
    v_repetible  CHAR(1);
    v_min_filas  NUMBER;
    v_max_filas  NUMBER;

    v_fila       NUMBER;
    v_orden      NUMBER;
    v_valor_id   NUMBER;
    v_error      VARCHAR2(500);

    -- Copias del elemento en curso, para no volver a indexar los arreglos.
    v_txt        VARCHAR2(4000);
    v_num        NUMBER;
    v_fec        DATE;
    v_eti        VARCHAR2(200);

    -- Resuelve un campo por clave dentro de la version de la respuesta y
    -- comprueba lo estructural. Devuelve el mensaje de rechazo, o NULL si todo
    -- esta bien.
    FUNCTION resolver(p_Clave IN VARCHAR2, p_Fila IN NUMBER, p_Orden IN NUMBER)
        RETURN VARCHAR2 IS
    BEGIN
        BEGIN
            SELECT C.CAMPO_ID, T.COLUMNA_VALOR, T.ES_PRESENTACION, T.ADMITE_MULTIPLE,
                   C.SOLO_LECTURA, S.REPETIBLE, S.MIN_FILAS, S.MAX_FILAS
              INTO v_campo_id, v_columna, v_presenta, v_multiple,
                   v_solo_lec, v_repetible, v_min_filas, v_max_filas
              FROM MFO_CAMPO C
              JOIN MFO_TIPO_CAMPO T ON T.TIPO_CAMPO_ID = C.TIPO_CAMPO_ID
              JOIN MFO_SECCION S    ON S.SECCION_ID = C.SECCION_ID
             WHERE C.VERSION_ID = v_version_id
               AND C.CLAVE = UPPER(TRIM(p_Clave));
        EXCEPTION
            WHEN NO_DATA_FOUND THEN
                RETURN 'El campo ' || p_Clave || ' no existe en la version de esta respuesta.';
        END;

        IF v_presenta = 'S' THEN
            RETURN 'El elemento ' || p_Clave || ' es de presentacion y no captura valor.';
        END IF;

        IF v_solo_lec = 'S' THEN
            RETURN 'El campo ' || p_Clave || ' es de solo lectura y no acepta valor del cliente.';
        END IF;

        IF NVL(p_Orden, 0) > 0 AND v_multiple <> 'S' THEN
            RETURN 'El campo ' || p_Clave || ' no admite multiples valores.';
        END IF;

        IF NVL(p_Fila, 0) > 0 AND v_repetible <> 'S' THEN
            RETURN 'El campo ' || p_Clave || ' no pertenece a una seccion repetible.';
        END IF;

        IF v_repetible = 'S' AND v_max_filas IS NOT NULL AND NVL(p_Fila, 0) > v_max_filas THEN
            RETURN 'La seccion del campo ' || p_Clave || ' admite como maximo ' || v_max_filas || ' filas.';
        END IF;

        RETURN NULL;
    END resolver;

    -- MERGE manual: UPDATE y si no toco nada, INSERT. Se hace asi y no con la
    -- sentencia MERGE porque hay que escribir en una columna distinta segun el
    -- tipo, y un MERGE con cuatro columnas condicionadas queda ilegible.
    PROCEDURE grabar(p_Clave IN VARCHAR2, p_Fila IN NUMBER, p_Orden IN NUMBER,
                     p_Txt IN VARCHAR2, p_Num IN NUMBER, p_Fec IN DATE,
                     p_Clb IN CLOB, p_Eti IN VARCHAR2) IS
    BEGIN
        UPDATE MFO_VALOR
           SET VALOR_TXT    = CASE WHEN v_columna = 'TXT' THEN p_Txt ELSE NULL END,
               VALOR_NUM    = CASE WHEN v_columna = 'NUM' THEN p_Num ELSE NULL END,
               VALOR_FEC    = CASE WHEN v_columna = 'FEC' THEN p_Fec ELSE NULL END,
               VALOR_CLB    = CASE WHEN v_columna = 'CLB' THEN NVL(p_Clb, TO_CLOB(p_Txt)) ELSE NULL END,
               ETIQUETA_VAL = p_Eti
         WHERE RESPUESTA_ID = p_RespuestaId
           AND CAMPO_ID     = v_campo_id
           AND FILA         = p_Fila
           AND ORDEN_VAL    = p_Orden;

        IF SQL%ROWCOUNT = 0 THEN
            SELECT SEQ_MFO_VALOR.NEXTVAL INTO v_valor_id FROM DUAL;
            INSERT INTO MFO_VALOR (
                VALOR_ID, RESPUESTA_ID, CAMPO_ID, CLAVE_CAMPO, FILA, ORDEN_VAL,
                VALOR_TXT, VALOR_NUM, VALOR_FEC, VALOR_CLB, ETIQUETA_VAL
            ) VALUES (
                v_valor_id, p_RespuestaId, v_campo_id, UPPER(TRIM(p_Clave)), p_Fila, p_Orden,
                CASE WHEN v_columna = 'TXT' THEN p_Txt ELSE NULL END,
                CASE WHEN v_columna = 'NUM' THEN p_Num ELSE NULL END,
                CASE WHEN v_columna = 'FEC' THEN p_Fec ELSE NULL END,
                CASE WHEN v_columna = 'CLB' THEN NVL(p_Clb, TO_CLOB(p_Txt)) ELSE NULL END,
                p_Eti
            );
        END IF;

        p_Guardados := p_Guardados + 1;
    END grabar;
BEGIN
    p_Guardados := 0;

    BEGIN
        SELECT VERSION_ID, ESTADO INTO v_version_id, v_estado
          FROM MFO_RESPUESTA WHERE RESPUESTA_ID = p_RespuestaId;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            p_Message := 'La respuesta indicada no existe.';
            RETURN;
    END;

    -- Una respuesta enviada o anulada ya no se toca. Es el equivalente en el
    -- plano de datos a la inmutabilidad de la definicion: lo que se envio es lo
    -- que quedo.
    IF v_estado <> 'BORRADOR' THEN
        p_Message := 'La respuesta esta ' || v_estado || ' y ya no admite cambios.';
        RETURN;
    END IF;

    -- p_Claves manda: es el arreglo que define cuantos valores vienen. De los
    -- demas se lee solo el indice que exista. Acceder a un indice ausente de un
    -- arreglo asociativo lanza NO_DATA_FOUND, y ese fallo se manifestaria como
    -- un "error tecnico" opaco si un dia el llamador enviara los arreglos con
    -- distinta densidad.
    i := p_Claves.FIRST;
    WHILE i IS NOT NULL LOOP
        v_fila  := 0;
        v_orden := 0;
        v_txt   := NULL;
        v_num   := NULL;
        v_fec   := NULL;
        v_eti   := NULL;

        IF p_Filas.EXISTS(i)      THEN v_fila  := NVL(p_Filas(i), 0);   END IF;
        IF p_Ordenes.EXISTS(i)    THEN v_orden := NVL(p_Ordenes(i), 0); END IF;
        IF p_ValoresTxt.EXISTS(i) THEN v_txt   := p_ValoresTxt(i);      END IF;
        IF p_ValoresNum.EXISTS(i) THEN v_num   := p_ValoresNum(i);      END IF;
        IF p_ValoresFec.EXISTS(i) THEN v_fec   := p_ValoresFec(i);      END IF;
        IF p_Etiquetas.EXISTS(i)  THEN v_eti   := p_Etiquetas(i);       END IF;

        v_error := resolver(p_Claves(i), v_fila, v_orden);
        IF v_error IS NOT NULL THEN
            ROLLBACK;
            p_Guardados := 0;
            p_Message := v_error;
            RETURN;
        END IF;

        -- Un valor de tipo TXT que no cabe en VALOR_TXT no se trunca en
        -- silencio: se rechaza. Truncar seria perder dato del usuario sin que
        -- nadie se entere.
        IF v_columna = 'TXT' AND LENGTH(v_txt) > 4000 THEN
            ROLLBACK;
            p_Guardados := 0;
            p_Message := 'El valor de ' || p_Claves(i) || ' excede los 4000 caracteres que admite su tipo.';
            RETURN;
        END IF;

        grabar(p_Claves(i), v_fila, v_orden, v_txt, v_num, v_fec, NULL, v_eti);

        i := p_Claves.NEXT(i);
    END LOOP;

    -- Valor CLOB largo, si vino.
    IF p_ClobClave IS NOT NULL THEN
        v_fila  := NVL(p_ClobFila, 0);
        v_orden := NVL(p_ClobOrden, 0);

        v_error := resolver(p_ClobClave, v_fila, v_orden);
        IF v_error IS NOT NULL THEN
            ROLLBACK;
            p_Guardados := 0;
            p_Message := v_error;
            RETURN;
        END IF;

        IF v_columna <> 'CLB' THEN
            ROLLBACK;
            p_Guardados := 0;
            p_Message := 'El campo ' || p_ClobClave || ' no es de texto largo.';
            RETURN;
        END IF;

        grabar(p_ClobClave, v_fila, v_orden, NULL, NULL, NULL, p_ClobValor, NULL);
    END IF;

    UPDATE MFO_RESPUESTA
       SET USUARIO_UPD = p_Usuario,
           FECHA_UPD   = SYSDATE
     WHERE RESPUESTA_ID = p_RespuestaId;

    COMMIT;
    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        p_Guardados := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
END;
/
