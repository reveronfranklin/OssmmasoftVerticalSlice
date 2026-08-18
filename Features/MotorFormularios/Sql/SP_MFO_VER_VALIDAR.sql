-- =============================================================================
-- MFO - Validar una version sin publicarla.
--
-- Devuelve una lista de hallazgos. SEVERIDAD='ERROR' impide publicar;
-- SEVERIDAD='AVISO' no, pero señala algo que casi siempre es un descuido.
--
-- La lista se arma en una coleccion y se entrega por TABLE(): no toca disco, no
-- deja estado de sesion y dos validaciones concurrentes no se pisan.
--
-- Es prerequisito de SP_MFO_VER_PUBLICAR, que la invoca. Se expone aparte para
-- que el diseñador pueda mostrar los hallazgos ANTES de que el usuario pulse
-- publicar, que es cuando sirven.
-- =============================================================================
CREATE OR REPLACE PROCEDURE MFO.SP_MFO_VER_VALIDAR (
    p_VersionId    IN  NUMBER,
    p_ResultSet    OUT SYS_REFCURSOR,
    p_Message      OUT VARCHAR2,
    p_TotalRecords OUT NUMBER,
    p_Errores      OUT NUMBER
) AS
    v_h       MFO_HALLAZGO_TAB := MFO_HALLAZGO_TAB();
    v_estado  MFO_VERSION.ESTADO%TYPE;
    v_conteo  NUMBER;

    PROCEDURE agregar(
        p_Severidad IN VARCHAR2, p_Codigo IN VARCHAR2, p_Entidad IN VARCHAR2,
        p_EntidadId IN NUMBER,   p_Clave  IN VARCHAR2, p_Mensaje IN VARCHAR2
    ) IS
    BEGIN
        v_h.EXTEND;
        v_h(v_h.COUNT) := MFO_HALLAZGO_OBJ(p_Severidad, p_Codigo, p_Entidad,
                                           p_EntidadId, p_Clave, p_Mensaje);
    END agregar;
BEGIN
    p_TotalRecords := 0;
    p_Errores := 0;

    BEGIN
        SELECT ESTADO INTO v_estado FROM MFO_VERSION WHERE VERSION_ID = p_VersionId;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            p_Message := 'La version indicada no existe.';
            OPEN p_ResultSet FOR SELECT NULL SEVERIDAD FROM DUAL WHERE 1 = 0;
            RETURN;
    END;

    -- ------------------------------------------------------------------------
    -- 1. Al menos un campo que capture datos.
    --    Una version solo con titulos y notas se puede diseñar pero no se puede
    --    responder, y publicarla dejaria un formulario que no hace nada.
    -- ------------------------------------------------------------------------
    SELECT COUNT(1) INTO v_conteo
      FROM MFO_CAMPO C
      JOIN MFO_TIPO_CAMPO T ON T.TIPO_CAMPO_ID = C.TIPO_CAMPO_ID
     WHERE C.VERSION_ID = p_VersionId
       AND T.ES_PRESENTACION = 'N';

    IF v_conteo = 0 THEN
        agregar('ERROR', 'SIN_CAMPOS', 'VERSION', p_VersionId, NULL,
                'La version no tiene ningun campo que capture datos.');
    END IF;

    -- ------------------------------------------------------------------------
    -- 2. Claves unicas por version.
    --    UK_MFO_SEC_CLAVE y UK_MFO_CAMPO_CLAVE ya lo impiden a nivel de dato;
    --    se comprueba igual para que, si alguna vez se relajaran, la validacion
    --    lo siga atrapando antes de publicar y no despues.
    -- ------------------------------------------------------------------------
    FOR r IN (SELECT CLAVE, COUNT(1) N FROM MFO_SECCION
               WHERE VERSION_ID = p_VersionId GROUP BY CLAVE HAVING COUNT(1) > 1) LOOP
        agregar('ERROR', 'CLAVE_SEC_DUP', 'SECCION', NULL, r.CLAVE,
                'La clave de seccion ' || r.CLAVE || ' esta repetida ' || r.N || ' veces.');
    END LOOP;

    FOR r IN (SELECT CLAVE, COUNT(1) N FROM MFO_CAMPO
               WHERE VERSION_ID = p_VersionId GROUP BY CLAVE HAVING COUNT(1) > 1) LOOP
        agregar('ERROR', 'CLAVE_CAMPO_DUP', 'CAMPO', NULL, r.CLAVE,
                'La clave de campo ' || r.CLAVE || ' esta repetida ' || r.N || ' veces.');
    END LOOP;

    -- ------------------------------------------------------------------------
    -- 3. Todo tipo con ADMITE_OPCIONES='S' necesita de donde sacarlas.
    -- ------------------------------------------------------------------------
    FOR r IN (SELECT C.CAMPO_ID, C.CLAVE, C.ETIQUETA, T.CODIGO TIPO
                FROM MFO_CAMPO C
                JOIN MFO_TIPO_CAMPO T ON T.TIPO_CAMPO_ID = C.TIPO_CAMPO_ID
               WHERE C.VERSION_ID = p_VersionId
                 AND T.ADMITE_OPCIONES = 'S'
                 AND NVL(C.ORIGEN_OPCIONES, 'ESTATICA') = 'ESTATICA'
                 AND NOT EXISTS (SELECT 1 FROM MFO_OPCION O
                                  WHERE O.CAMPO_ID = C.CAMPO_ID AND O.ACTIVO = 'S')) LOOP
        agregar('ERROR', 'OPC_FALTA', 'CAMPO', r.CAMPO_ID, r.CLAVE,
                'El campo ' || r.ETIQUETA || ' es de tipo ' || r.TIPO ||
                ' y no tiene opciones activas ni catalogo.');
    END LOOP;

    -- Opciones cargadas en un tipo que no las usa: no rompe nada, pero es basura
    -- que confunde al siguiente que edite el formulario.
    FOR r IN (SELECT C.CAMPO_ID, C.CLAVE, C.ETIQUETA
                FROM MFO_CAMPO C
                JOIN MFO_TIPO_CAMPO T ON T.TIPO_CAMPO_ID = C.TIPO_CAMPO_ID
               WHERE C.VERSION_ID = p_VersionId
                 AND T.ADMITE_OPCIONES = 'N'
                 AND EXISTS (SELECT 1 FROM MFO_OPCION O WHERE O.CAMPO_ID = C.CAMPO_ID)) LOOP
        agregar('AVISO', 'OPC_SOBRA', 'CAMPO', r.CAMPO_ID, r.CLAVE,
                'El campo ' || r.ETIQUETA || ' tiene opciones cargadas pero su tipo no las usa.');
    END LOOP;

    -- ------------------------------------------------------------------------
    -- 4. Toda condicion apunta a un destino y a un origen de ESTA version.
    --    DESTINO_ID es polimorfico y no tiene FK, asi que esta es la unica
    --    barrera contra condiciones huerfanas.
    -- ------------------------------------------------------------------------
    FOR r IN (SELECT D.CONDICION_ID, D.DESTINO_TIPO, D.DESTINO_ID
                FROM MFO_CONDICION D
               WHERE D.VERSION_ID = p_VersionId
                 AND ((D.DESTINO_TIPO = 'CAMPO'
                       AND NOT EXISTS (SELECT 1 FROM MFO_CAMPO C
                                        WHERE C.CAMPO_ID = D.DESTINO_ID
                                          AND C.VERSION_ID = p_VersionId))
                   OR (D.DESTINO_TIPO = 'SECCION'
                       AND NOT EXISTS (SELECT 1 FROM MFO_SECCION S
                                        WHERE S.SECCION_ID = D.DESTINO_ID
                                          AND S.VERSION_ID = p_VersionId)))) LOOP
        agregar('ERROR', 'COND_DESTINO', 'CONDICION', r.CONDICION_ID, NULL,
                'La condicion apunta a un destino (' || r.DESTINO_TIPO || ' ' ||
                r.DESTINO_ID || ') que no existe en esta version.');
    END LOOP;

    FOR r IN (SELECT D.CONDICION_ID, D.CAMPO_ORIGEN_ID
                FROM MFO_CONDICION D
               WHERE D.VERSION_ID = p_VersionId
                 AND NOT EXISTS (SELECT 1 FROM MFO_CAMPO C
                                  WHERE C.CAMPO_ID = D.CAMPO_ORIGEN_ID
                                    AND C.VERSION_ID = p_VersionId)) LOOP
        agregar('ERROR', 'COND_ORIGEN', 'CONDICION', r.CONDICION_ID, NULL,
                'La condicion se evalua sobre un campo que no pertenece a esta version.');
    END LOOP;

    -- ------------------------------------------------------------------------
    -- 5. Ciclos de condiciones (A depende de B que depende de A).
    --    Un ciclo hace que el evaluador no converja: ningun estado de
    --    visibilidad es estable, y el formulario queda imposible de rellenar.
    --
    --    Se resuelve con CONNECT BY NOCYCLE + CONNECT_BY_ISCYCLE, ambos
    --    disponibles desde Oracle 10g. Las aristas van de campo origen a campo
    --    destino; cuando el destino es una seccion, la arista se expande a todos
    --    los campos de esa seccion, que son los que realmente quedan afectados.
    -- ------------------------------------------------------------------------
    SELECT COUNT(1) INTO v_conteo
      FROM (SELECT CONNECT_BY_ISCYCLE CICLO
              FROM (SELECT D.CAMPO_ORIGEN_ID ORIGEN, D.DESTINO_ID DESTINO
                      FROM MFO_CONDICION D
                     WHERE D.VERSION_ID = p_VersionId
                       AND D.DESTINO_TIPO = 'CAMPO'
                    UNION ALL
                    SELECT D.CAMPO_ORIGEN_ID, C.CAMPO_ID
                      FROM MFO_CONDICION D
                      JOIN MFO_CAMPO C ON C.SECCION_ID = D.DESTINO_ID
                     WHERE D.VERSION_ID = p_VersionId
                       AND D.DESTINO_TIPO = 'SECCION') A
            CONNECT BY NOCYCLE PRIOR A.DESTINO = A.ORIGEN)
     WHERE CICLO = 1;

    IF v_conteo > 0 THEN
        agregar('ERROR', 'COND_CICLO', 'VERSION', p_VersionId, NULL,
                'Hay condiciones que se referencian en ciclo. Revise las dependencias entre campos.');
    END IF;

    -- ------------------------------------------------------------------------
    -- 6. Reglas coherentes con el tipo del campo.
    --    Una regla MIN sobre un campo de texto no falla al guardarse, pero el
    --    interprete no sabria que hacer con ella en tiempo de validacion.
    -- ------------------------------------------------------------------------
    FOR r IN (SELECT G.REGLA_ID, G.TIPO_REGLA, C.CLAVE, C.ETIQUETA, T.CODIGO TIPO
                FROM MFO_REGLA G
                JOIN MFO_CAMPO C      ON C.CAMPO_ID = G.CAMPO_ID
                JOIN MFO_TIPO_CAMPO T ON T.TIPO_CAMPO_ID = C.TIPO_CAMPO_ID
               WHERE C.VERSION_ID = p_VersionId
                 AND G.ACTIVO = 'S'
                 AND (
                      (G.TIPO_REGLA IN ('LONG_MIN', 'LONG_MAX', 'PATRON')
                       AND T.COLUMNA_VALOR NOT IN ('TXT', 'CLB'))
                   OR (G.TIPO_REGLA IN ('MIN', 'MAX', 'DECIMALES')
                       AND T.COLUMNA_VALOR <> 'NUM')
                   OR (G.TIPO_REGLA IN ('FEC_MIN', 'FEC_MAX')
                       AND T.COLUMNA_VALOR <> 'FEC')
                   OR (G.TIPO_REGLA IN ('SEL_MIN', 'SEL_MAX')
                       AND T.ADMITE_MULTIPLE <> 'S')
                   OR (G.TIPO_REGLA IN ('ARCH_MAX_MB', 'ARCH_EXT')
                       AND T.ADMITE_ARCHIVO <> 'S')
                   OR (G.TIPO_REGLA = 'UNICO'
                       AND T.COLUMNA_VALOR NOT IN ('TXT', 'NUM'))
                   OR (T.ES_PRESENTACION = 'S')
                 )) LOOP
        agregar('ERROR', 'REGLA_TIPO', 'REGLA', r.REGLA_ID, r.CLAVE,
                'La regla ' || r.TIPO_REGLA || ' no aplica a un campo de tipo ' ||
                r.TIPO || ' (' || r.ETIQUETA || ').');
    END LOOP;

    -- Un campo de presentacion marcado como requerido nunca se podria satisfacer:
    -- no captura valor.
    FOR r IN (SELECT C.CAMPO_ID, C.CLAVE, C.ETIQUETA
                FROM MFO_CAMPO C
                JOIN MFO_TIPO_CAMPO T ON T.TIPO_CAMPO_ID = C.TIPO_CAMPO_ID
               WHERE C.VERSION_ID = p_VersionId
                 AND T.ES_PRESENTACION = 'S'
                 AND C.REQUERIDO = 'S') LOOP
        agregar('ERROR', 'PRES_REQUERIDO', 'CAMPO', r.CAMPO_ID, r.CLAVE,
                'El elemento ' || r.ETIQUETA || ' es de presentacion y no puede ser obligatorio.');
    END LOOP;

    -- ------------------------------------------------------------------------
    -- 7. Secciones repetibles con rango de filas coherente.
    --    CK_MFO_SEC_FILAS ya lo impide; se comprueba aqui tambien para que el
    --    diseñador vea el problema como un hallazgo y no como un ORA-02290.
    -- ------------------------------------------------------------------------
    FOR r IN (SELECT SECCION_ID, CLAVE, TITULO, MIN_FILAS, MAX_FILAS
                FROM MFO_SECCION
               WHERE VERSION_ID = p_VersionId
                 AND REPETIBLE = 'S'
                 AND MIN_FILAS IS NOT NULL
                 AND MAX_FILAS IS NOT NULL
                 AND MIN_FILAS > MAX_FILAS) LOOP
        agregar('ERROR', 'SEC_FILAS', 'SECCION', r.SECCION_ID, r.CLAVE,
                'La seccion ' || r.TITULO || ' tiene minimo de filas mayor que el maximo.');
    END LOOP;

    -- Seccion sin campos: no rompe, pero se dibuja como un bloque vacio.
    FOR r IN (SELECT S.SECCION_ID, S.CLAVE, S.TITULO
                FROM MFO_SECCION S
               WHERE S.VERSION_ID = p_VersionId
                 AND NOT EXISTS (SELECT 1 FROM MFO_CAMPO C WHERE C.SECCION_ID = S.SECCION_ID)) LOOP
        agregar('AVISO', 'SEC_VACIA', 'SECCION', r.SECCION_ID, r.CLAVE,
                'La seccion ' || NVL(r.TITULO, r.CLAVE) || ' no tiene campos.');
    END LOOP;

    -- ------------------------------------------------------------------------
    -- Resultado
    -- ------------------------------------------------------------------------
    p_TotalRecords := v_h.COUNT;

    p_Errores := 0;
    FOR i IN 1 .. v_h.COUNT LOOP
        IF v_h(i).SEVERIDAD = 'ERROR' THEN
            p_Errores := p_Errores + 1;
        END IF;
    END LOOP;

    OPEN p_ResultSet FOR
        SELECT SEVERIDAD, CODIGO, ENTIDAD, ENTIDAD_ID, CLAVE, MENSAJE
          FROM TABLE(v_h);

    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        p_TotalRecords := 0;
        p_Errores := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT NULL SEVERIDAD FROM DUAL WHERE 1 = 0;
END;
/
