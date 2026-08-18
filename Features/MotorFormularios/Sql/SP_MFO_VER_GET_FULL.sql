-- =============================================================================
-- MFO - Definicion completa de una version, en una sola llamada.
--
-- Seis ref cursors: version, secciones, campos, opciones, reglas y condiciones.
-- Es la llamada que alimenta la cache de definiciones del backend y, a traves de
-- ella, el renderizador. Se hace en una sola ida a la base a proposito: el
-- frontend arma el formulario entero de golpe, y seis viajes por formulario
-- serian seis oportunidades de que la definicion llegue incoherente.
--
-- Admite resolver por VERSION_ID o por el ALIAS del formulario. Con alias se
-- devuelve la version PUBLICADA vigente, que es lo que necesita el renderizador;
-- el diseñador pide por VERSION_ID porque tambien trabaja sobre borradores.
-- =============================================================================
CREATE OR REPLACE PROCEDURE MFO.SP_MFO_VER_GET_FULL (
    p_VersionId   IN  NUMBER,
    p_Alias       IN  VARCHAR2,
    p_Version     OUT SYS_REFCURSOR,
    p_Secciones   OUT SYS_REFCURSOR,
    p_Campos      OUT SYS_REFCURSOR,
    p_Opciones    OUT SYS_REFCURSOR,
    p_Reglas      OUT SYS_REFCURSOR,
    p_Condiciones OUT SYS_REFCURSOR,
    p_Message     OUT VARCHAR2
) AS
    v_version_id NUMBER;

    PROCEDURE cursores_vacios IS
    BEGIN
        OPEN p_Version     FOR SELECT NULL VERSION_ID   FROM DUAL WHERE 1 = 0;
        OPEN p_Secciones   FOR SELECT NULL SECCION_ID   FROM DUAL WHERE 1 = 0;
        OPEN p_Campos      FOR SELECT NULL CAMPO_ID     FROM DUAL WHERE 1 = 0;
        OPEN p_Opciones    FOR SELECT NULL OPCION_ID    FROM DUAL WHERE 1 = 0;
        OPEN p_Reglas      FOR SELECT NULL REGLA_ID     FROM DUAL WHERE 1 = 0;
        OPEN p_Condiciones FOR SELECT NULL CONDICION_ID FROM DUAL WHERE 1 = 0;
    END cursores_vacios;
BEGIN
    IF p_VersionId IS NULL AND p_Alias IS NULL THEN
        p_Message := 'Indique la version por id o el formulario por alias.';
        cursores_vacios;
        RETURN;
    END IF;

    IF p_VersionId IS NOT NULL THEN
        v_version_id := p_VersionId;
    ELSE
        BEGIN
            SELECT VERSION_PUBL_ID INTO v_version_id
              FROM MFO_FORMULARIO WHERE ALIAS = p_Alias;
        EXCEPTION
            WHEN NO_DATA_FOUND THEN
                p_Message := 'El formulario ' || p_Alias || ' no existe.';
                cursores_vacios;
                RETURN;
        END;

        IF v_version_id IS NULL THEN
            p_Message := 'El formulario ' || p_Alias || ' no tiene una version publicada.';
            cursores_vacios;
            RETURN;
        END IF;
    END IF;

    OPEN p_Version FOR
        SELECT V.VERSION_ID, V.FORMULARIO_ID, V.NUMERO, V.ESTADO, V.NOTAS,
               V.HASH_DEF, V.VERSION_ORIGEN_ID, V.FECHA_PUBL, V.USUARIO_PUBL,
               F.ALIAS, F.NOMBRE, F.DESCRIPCION, F.CATEGORIA, F.ESTADO ESTADO_FORM,
               F.PERMITE_BORRADOR, F.MAX_RESP_USUARIO, F.MODO_USO, F.REGISTRA_EJEC,
               F.ENTIDAD_DESTINO
          FROM MFO_VERSION V
          JOIN MFO_FORMULARIO F ON F.FORMULARIO_ID = V.FORMULARIO_ID
         WHERE V.VERSION_ID = v_version_id;

    OPEN p_Secciones FOR
        SELECT SECCION_ID, CLAVE, TITULO, DESCRIPCION, ORDEN, COLUMNAS,
               ES_PASO, REPETIBLE, MIN_FILAS, MAX_FILAS, COLAPSABLE
          FROM MFO_SECCION
         WHERE VERSION_ID = v_version_id
         ORDER BY ORDEN, SECCION_ID;

    -- Se devuelven los atributos del TIPO junto al campo (COMPONENTE,
    -- COLUMNA_VALOR, ADMITE_*) para que el frontend no tenga que cruzar contra
    -- el catalogo, y el backend no tenga que hacerlo en memoria.
    OPEN p_Campos FOR
        SELECT C.CAMPO_ID, C.SECCION_ID, C.CLAVE, C.ETIQUETA, C.AYUDA, C.PLACEHOLDER,
               C.ORDEN, C.ANCHO, C.REQUERIDO, C.SOLO_LECTURA, C.VALOR_DEFECTO,
               C.ORIGEN_OPCIONES, C.CATALOGO_CLAVE, C.MASCARA, C.UNIDAD,
               T.TIPO_CAMPO_ID, T.CODIGO TIPO_CODIGO, T.COMPONENTE, T.COLUMNA_VALOR,
               T.ADMITE_OPCIONES, T.ADMITE_MULTIPLE, T.ES_PRESENTACION, T.ADMITE_ARCHIVO
          FROM MFO_CAMPO C
          JOIN MFO_TIPO_CAMPO T ON T.TIPO_CAMPO_ID = C.TIPO_CAMPO_ID
         WHERE C.VERSION_ID = v_version_id
         ORDER BY C.SECCION_ID, C.ORDEN, C.CAMPO_ID;

    OPEN p_Opciones FOR
        SELECT O.OPCION_ID, O.CAMPO_ID, C.CLAVE CLAVE_CAMPO, O.VALOR, O.ETIQUETA,
               O.ORDEN, O.GRUPO, O.ES_DEFECTO, O.ACTIVO
          FROM MFO_OPCION O
          JOIN MFO_CAMPO C ON C.CAMPO_ID = O.CAMPO_ID
         WHERE C.VERSION_ID = v_version_id
         ORDER BY O.CAMPO_ID, O.ORDEN, O.OPCION_ID;

    OPEN p_Reglas FOR
        SELECT G.REGLA_ID, G.CAMPO_ID, C.CLAVE CLAVE_CAMPO, G.TIPO_REGLA,
               G.PARAM_1, G.PARAM_2, G.MENSAJE, G.ORDEN, G.ACTIVO
          FROM MFO_REGLA G
          JOIN MFO_CAMPO C ON C.CAMPO_ID = G.CAMPO_ID
         WHERE C.VERSION_ID = v_version_id
           AND G.ACTIVO = 'S'
         ORDER BY G.CAMPO_ID, G.ORDEN, G.REGLA_ID;

    -- CLAVE_DESTINO resuelve aqui la referencia polimorfica: el frontend trabaja
    -- con claves, no con IDs, y hacerle resolver DESTINO_ID contra dos
    -- colecciones distintas seria repetir en TypeScript una logica que aqui es
    -- un CASE.
    OPEN p_Condiciones FOR
        SELECT D.CONDICION_ID, D.ACCION, D.DESTINO_TIPO, D.DESTINO_ID,
               CASE WHEN D.DESTINO_TIPO = 'CAMPO'
                    THEN (SELECT X.CLAVE FROM MFO_CAMPO X WHERE X.CAMPO_ID = D.DESTINO_ID)
                    ELSE (SELECT Y.CLAVE FROM MFO_SECCION Y WHERE Y.SECCION_ID = D.DESTINO_ID)
               END CLAVE_DESTINO,
               D.CAMPO_ORIGEN_ID, CO.CLAVE CLAVE_ORIGEN,
               D.OPERADOR, D.VALOR_COMPARA, D.GRUPO, D.CONECTOR, D.ORDEN
          FROM MFO_CONDICION D
          JOIN MFO_CAMPO CO ON CO.CAMPO_ID = D.CAMPO_ORIGEN_ID
         WHERE D.VERSION_ID = v_version_id
         ORDER BY D.GRUPO, D.ORDEN, D.CONDICION_ID;

    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := 'Error tecnico: ' || SQLERRM;
        cursores_vacios;
END;
/
