-- =============================================================================
-- MFO - Clonar una version a una nueva en BORRADOR.
--
-- Es el procedimiento con mas riesgo de la Fase 2, y el riesgo esta concentrado
-- en un solo punto: MFO_CONDICION guarda DOS referencias a elementos de la
-- version -CAMPO_ORIGEN_ID y DESTINO_ID- y las dos hay que remapearlas a los IDs
-- nuevos. Si una se remapea y la otra no, el formulario clonado queda con
-- condiciones que apuntan a la version vieja: sigue "funcionando" a la vista, no
-- lo detecta ninguna FK -DESTINO_ID es polimorfico y no tiene FK- y produce
-- ramas que no se activan nunca.
--
-- Por eso el clonado se hace en dos pasadas con mapas explicitos en memoria
-- (viejo -> nuevo) y las condiciones se clonan al final, cuando ambos mapas
-- estan completos.
--
-- Las CLAVE se preservan. Es lo que permite que una respuesta llenada con la
-- version 1 se pueda comparar con una de la version 2: los IDs cambian, las
-- claves no.
--
-- p_CondicionesOmitidas informa cuantas condiciones no se pudieron remapear
-- porque su destino u origen ya no existia en la version de partida. Se omiten
-- en vez de clonarse rotas, y se reporta en vez de callarse.
-- =============================================================================
CREATE OR REPLACE PROCEDURE MFO.SP_MFO_VER_CLONE (
    p_VersionOrigenId     IN  NUMBER,
    p_Notas               IN  VARCHAR2,
    p_Usuario             IN  VARCHAR2,
    p_VersionId           OUT NUMBER,
    p_CondicionesOmitidas OUT NUMBER,
    p_Message             OUT VARCHAR2
) AS
    TYPE t_map IS TABLE OF NUMBER INDEX BY PLS_INTEGER;

    v_map_sec    t_map;
    v_map_campo  t_map;

    v_formulario_id NUMBER;
    v_borradores    NUMBER;
    v_numero        NUMBER;
    v_nuevo_id      NUMBER;
    v_destino_id    NUMBER;
    v_origen_id     NUMBER;
BEGIN
    p_VersionId := 0;
    p_CondicionesOmitidas := 0;

    BEGIN
        SELECT FORMULARIO_ID INTO v_formulario_id
          FROM MFO_VERSION WHERE VERSION_ID = p_VersionOrigenId;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            p_Message := 'La version de origen no existe.';
            RETURN;
    END;

    SELECT COUNT(1) INTO v_borradores
      FROM MFO_VERSION
     WHERE FORMULARIO_ID = v_formulario_id AND ESTADO = 'BORRADOR';

    IF v_borradores > 0 THEN
        p_Message := 'El formulario ya tiene una version en BORRADOR. Publiquela o descartela antes de clonar.';
        RETURN;
    END IF;

    SELECT NVL(MAX(NUMERO), 0) + 1 INTO v_numero
      FROM MFO_VERSION WHERE FORMULARIO_ID = v_formulario_id;

    SELECT SEQ_MFO_VERSION.NEXTVAL INTO p_VersionId FROM DUAL;
    INSERT INTO MFO_VERSION (
        VERSION_ID, FORMULARIO_ID, NUMERO, ESTADO, NOTAS, VERSION_ORIGEN_ID,
        USUARIO_INS, FECHA_INS
    ) VALUES (
        p_VersionId, v_formulario_id, v_numero, 'BORRADOR', p_Notas, p_VersionOrigenId,
        p_Usuario, SYSDATE
    );

    -- ------------------------------------------------------------------------
    -- Pasada 1: secciones. Se guarda el mapa viejo -> nuevo.
    -- ------------------------------------------------------------------------
    FOR s IN (SELECT * FROM MFO_SECCION WHERE VERSION_ID = p_VersionOrigenId ORDER BY ORDEN) LOOP
        SELECT SEQ_MFO_SECCION.NEXTVAL INTO v_nuevo_id FROM DUAL;
        v_map_sec(s.SECCION_ID) := v_nuevo_id;

        INSERT INTO MFO_SECCION (
            SECCION_ID, VERSION_ID, CLAVE, TITULO, DESCRIPCION, ORDEN, COLUMNAS,
            ES_PASO, REPETIBLE, MIN_FILAS, MAX_FILAS, COLAPSABLE
        ) VALUES (
            v_nuevo_id, p_VersionId, s.CLAVE, s.TITULO, s.DESCRIPCION, s.ORDEN, s.COLUMNAS,
            s.ES_PASO, s.REPETIBLE, s.MIN_FILAS, s.MAX_FILAS, s.COLAPSABLE
        );
    END LOOP;

    -- ------------------------------------------------------------------------
    -- Pasada 2: campos. SECCION_ID se remapea; VERSION_ID es el nuevo.
    -- ------------------------------------------------------------------------
    FOR c IN (SELECT * FROM MFO_CAMPO WHERE VERSION_ID = p_VersionOrigenId ORDER BY SECCION_ID, ORDEN) LOOP
        SELECT SEQ_MFO_CAMPO.NEXTVAL INTO v_nuevo_id FROM DUAL;
        v_map_campo(c.CAMPO_ID) := v_nuevo_id;

        INSERT INTO MFO_CAMPO (
            CAMPO_ID, VERSION_ID, SECCION_ID, TIPO_CAMPO_ID, CLAVE, ETIQUETA,
            AYUDA, PLACEHOLDER, ORDEN, ANCHO, REQUERIDO, SOLO_LECTURA,
            VALOR_DEFECTO, ORIGEN_OPCIONES, CATALOGO_CLAVE, MASCARA, UNIDAD, EXPRESION
        ) VALUES (
            v_nuevo_id, p_VersionId, v_map_sec(c.SECCION_ID), c.TIPO_CAMPO_ID, c.CLAVE, c.ETIQUETA,
            c.AYUDA, c.PLACEHOLDER, c.ORDEN, c.ANCHO, c.REQUERIDO, c.SOLO_LECTURA,
            c.VALOR_DEFECTO, c.ORIGEN_OPCIONES, c.CATALOGO_CLAVE, c.MASCARA, c.UNIDAD, c.EXPRESION
        );
    END LOOP;

    -- ------------------------------------------------------------------------
    -- Opciones y reglas: cuelgan del campo, asi que basta el mapa de campos.
    -- ------------------------------------------------------------------------
    FOR o IN (SELECT O.* FROM MFO_OPCION O
                JOIN MFO_CAMPO C ON C.CAMPO_ID = O.CAMPO_ID
               WHERE C.VERSION_ID = p_VersionOrigenId
               ORDER BY O.CAMPO_ID, O.ORDEN) LOOP
        INSERT INTO MFO_OPCION (
            OPCION_ID, CAMPO_ID, VALOR, ETIQUETA, ORDEN, GRUPO, ES_DEFECTO, ACTIVO
        ) VALUES (
            SEQ_MFO_OPCION.NEXTVAL, v_map_campo(o.CAMPO_ID), o.VALOR, o.ETIQUETA,
            o.ORDEN, o.GRUPO, o.ES_DEFECTO, o.ACTIVO
        );
    END LOOP;

    FOR r IN (SELECT R.* FROM MFO_REGLA R
                JOIN MFO_CAMPO C ON C.CAMPO_ID = R.CAMPO_ID
               WHERE C.VERSION_ID = p_VersionOrigenId
               ORDER BY R.CAMPO_ID, R.ORDEN) LOOP
        INSERT INTO MFO_REGLA (
            REGLA_ID, CAMPO_ID, TIPO_REGLA, PARAM_1, PARAM_2, MENSAJE, ORDEN, ACTIVO
        ) VALUES (
            SEQ_MFO_REGLA.NEXTVAL, v_map_campo(r.CAMPO_ID), r.TIPO_REGLA, r.PARAM_1,
            r.PARAM_2, r.MENSAJE, r.ORDEN, r.ACTIVO
        );
    END LOOP;

    -- ------------------------------------------------------------------------
    -- Condiciones: al final, con los dos mapas completos. Es el unico lugar
    -- donde hay que resolver una referencia polimorfica.
    -- ------------------------------------------------------------------------
    FOR d IN (SELECT * FROM MFO_CONDICION WHERE VERSION_ID = p_VersionOrigenId ORDER BY GRUPO, ORDEN) LOOP
        v_destino_id := NULL;
        v_origen_id  := NULL;

        IF d.DESTINO_TIPO = 'CAMPO' AND v_map_campo.EXISTS(d.DESTINO_ID) THEN
            v_destino_id := v_map_campo(d.DESTINO_ID);
        ELSIF d.DESTINO_TIPO = 'SECCION' AND v_map_sec.EXISTS(d.DESTINO_ID) THEN
            v_destino_id := v_map_sec(d.DESTINO_ID);
        END IF;

        IF v_map_campo.EXISTS(d.CAMPO_ORIGEN_ID) THEN
            v_origen_id := v_map_campo(d.CAMPO_ORIGEN_ID);
        END IF;

        -- Una condicion a la que le falte cualquiera de los dos extremos se
        -- omite. Clonarla apuntando a la version vieja seria peor que no
        -- clonarla: quedaria invisible para la validacion y muerta en ejecucion.
        --
        -- Se usa IF/ELSE y no CONTINUE porque CONTINUE es de Oracle 11g y este
        -- repositorio tiene que correr en 10g.
        IF v_destino_id IS NULL OR v_origen_id IS NULL THEN
            p_CondicionesOmitidas := p_CondicionesOmitidas + 1;
        ELSE
            INSERT INTO MFO_CONDICION (
                CONDICION_ID, VERSION_ID, ACCION, DESTINO_TIPO, DESTINO_ID,
                CAMPO_ORIGEN_ID, OPERADOR, VALOR_COMPARA, GRUPO, CONECTOR, ORDEN
            ) VALUES (
                SEQ_MFO_CONDICION.NEXTVAL, p_VersionId, d.ACCION, d.DESTINO_TIPO, v_destino_id,
                v_origen_id, d.OPERADOR, d.VALOR_COMPARA, d.GRUPO, d.CONECTOR, d.ORDEN
            );
        END IF;
    END LOOP;

    INSERT INTO MFO_AUDITORIA (AUDITORIA_ID, ENTIDAD, ENTIDAD_ID, ACCION, USUARIO, FECHA, DETALLE_CLB)
    VALUES (SEQ_MFO_AUDITORIA.NEXTVAL, 'VERSION', p_VersionId, 'CLONAR', p_Usuario, SYSDATE,
            'Clonada de la version ' || p_VersionOrigenId || '. Condiciones omitidas: ' || p_CondicionesOmitidas);

    COMMIT;
    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK;
        p_VersionId := 0;
        p_CondicionesOmitidas := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
END;
/
