-- =============================================================================
-- Motor de Formularios (MFO) - Segunda semilla: formulario de CAPTURA
-- Requerimiento 16. Cierra la deuda que dejo la decision 3 de la Fase 0.
--
-- 08_MFO_SEMILLA_DEMO.sql sembro REP_BM1, que es real pero no ejercita secciones
-- repetibles ni condiciones porque no las necesita. Sin un formulario que si las
-- use, el criterio de aceptacion de la Fase 6 -"las condiciones muestran/ocultan
-- en vivo"- no se puede dar por cerrado, y las secciones repetibles llegarian al
-- frontend sin haberse probado nunca contra la base.
--
-- Este formulario -una solicitud de mantenimiento de bienes- se elige porque
-- cubre de una sola vez lo que falta:
--   * seccion REPETIBLE con MIN_FILAS/MAX_FILAS (los bienes a intervenir);
--   * condiciones con destino CAMPO y con destino SECCION;
--   * acciones MOSTRAR y EXIGIR (un campo oculto que ademas es requerido: el
--     caso que sin evaluador de condiciones hace imposible enviar el
--     formulario);
--   * operadores IGUAL y EN_LISTA;
--   * secciones con ES_PASO='S' (wizard);
--   * MULTI_SELECT con opciones estaticas (ORDEN_VAL) y CATALOGO (BM_ICP);
--   * un campo ARCHIVO.
--
-- CONVENCION DE BOOLEANO. El motor guarda todo valor como texto y el validador
-- no interpreta el tipo, asi que 'que texto representa un si' es una decision de
-- contrato, no una del modelo. Aqui se fija: **'S' / 'N'**, igual que los flags
-- de la base. La condicion de abajo compara contra 'S' y el renderizador de la
-- Fase 6 debe emitir exactamente eso.
--
-- Igual que la semilla 08, se inserta en BORRADOR y se publica despues: es la
-- unica transicion que admite TRG_MFO_VER_LOCK, y ademas deja los hijos sin
-- proteccion de inmutabilidad durante la carga, que es lo que se quiere.
-- =============================================================================

DECLARE
    v_formulario_id  MFO_FORMULARIO.FORMULARIO_ID%TYPE;
    v_version_id     MFO_VERSION.VERSION_ID%TYPE;
    v_sec_datos      MFO_SECCION.SECCION_ID%TYPE;
    v_sec_bienes     MFO_SECCION.SECCION_ID%TYPE;
    v_sec_cierre     MFO_SECCION.SECCION_ID%TYPE;
    v_c_unidad       MFO_CAMPO.CAMPO_ID%TYPE;
    v_c_tipo         MFO_CAMPO.CAMPO_ID%TYPE;
    v_c_urgente      MFO_CAMPO.CAMPO_ID%TYPE;
    v_c_justifica    MFO_CAMPO.CAMPO_ID%TYPE;
    v_c_placa        MFO_CAMPO.CAMPO_ID%TYPE;
    v_c_falla        MFO_CAMPO.CAMPO_ID%TYPE;
    v_c_costo        MFO_CAMPO.CAMPO_ID%TYPE;
    v_c_servicios    MFO_CAMPO.CAMPO_ID%TYPE;
    v_c_observ       MFO_CAMPO.CAMPO_ID%TYPE;
    v_c_foto         MFO_CAMPO.CAMPO_ID%TYPE;
    v_t_catalogo     MFO_TIPO_CAMPO.TIPO_CAMPO_ID%TYPE;
    v_t_select       MFO_TIPO_CAMPO.TIPO_CAMPO_ID%TYPE;
    v_t_booleano     MFO_TIPO_CAMPO.TIPO_CAMPO_ID%TYPE;
    v_t_texto        MFO_TIPO_CAMPO.TIPO_CAMPO_ID%TYPE;
    v_t_largo        MFO_TIPO_CAMPO.TIPO_CAMPO_ID%TYPE;
    v_t_moneda       MFO_TIPO_CAMPO.TIPO_CAMPO_ID%TYPE;
    v_t_multi        MFO_TIPO_CAMPO.TIPO_CAMPO_ID%TYPE;
    v_t_archivo      MFO_TIPO_CAMPO.TIPO_CAMPO_ID%TYPE;
    v_existe         NUMBER;

    -- CODIGO_EMPRESA de settings:EmpresaConfig del vertical slice.
    c_empresa CONSTANT NUMBER := 13;
BEGIN
    SELECT COUNT(*) INTO v_existe FROM MFO_FORMULARIO WHERE ALIAS = 'SOL_MANT';
    IF v_existe > 0 THEN
        DBMS_OUTPUT.PUT_LINE('SOL_MANT ya existe. No se siembra de nuevo.');
        RETURN;
    END IF;

    SELECT TIPO_CAMPO_ID INTO v_t_catalogo FROM MFO_TIPO_CAMPO WHERE CODIGO = 'CATALOGO';
    SELECT TIPO_CAMPO_ID INTO v_t_select   FROM MFO_TIPO_CAMPO WHERE CODIGO = 'SELECT';
    SELECT TIPO_CAMPO_ID INTO v_t_booleano FROM MFO_TIPO_CAMPO WHERE CODIGO = 'BOOLEANO';
    SELECT TIPO_CAMPO_ID INTO v_t_texto    FROM MFO_TIPO_CAMPO WHERE CODIGO = 'TEXTO';
    SELECT TIPO_CAMPO_ID INTO v_t_largo    FROM MFO_TIPO_CAMPO WHERE CODIGO = 'TEXTO_LARGO';
    SELECT TIPO_CAMPO_ID INTO v_t_moneda   FROM MFO_TIPO_CAMPO WHERE CODIGO = 'MONEDA';
    SELECT TIPO_CAMPO_ID INTO v_t_multi    FROM MFO_TIPO_CAMPO WHERE CODIGO = 'MULTI_SELECT';
    SELECT TIPO_CAMPO_ID INTO v_t_archivo  FROM MFO_TIPO_CAMPO WHERE CODIGO = 'ARCHIVO';

    -- ------------------------------------------------------------------------
    -- Formulario. PERMITE_BORRADOR='S' a proposito: es el unico de los dos
    -- formularios sembrados que ejercita el autoguardado y la recuperacion del
    -- borrador tras recargar la pagina.
    -- ------------------------------------------------------------------------
    SELECT SEQ_MFO_FORMULARIO.NEXTVAL INTO v_formulario_id FROM DUAL;
    INSERT INTO MFO_FORMULARIO (
        FORMULARIO_ID, ALIAS, NOMBRE, DESCRIPCION, CATEGORIA, CODIGO_EMPRESA,
        ESTADO, VERSION_PUBL_ID, ENTIDAD_DESTINO, MAX_RESP_USUARIO,
        PERMITE_BORRADOR, MODO_USO, REGISTRA_EJEC, USUARIO_INS, FECHA_INS
    ) VALUES (
        v_formulario_id, 'SOL_MANT',
        'Solicitud de mantenimiento de bienes',
        'Solicitud de intervencion sobre uno o mas bienes municipales. Segunda semilla del motor: ejercita secciones repetibles y condiciones.',
        'Bienes Municipales', c_empresa,
        'ACTIVO', NULL, 'BM_BIEN', NULL,
        'S', 'CAPTURA', 'N', 'SEMILLA', SYSDATE
    );

    SELECT SEQ_MFO_VERSION.NEXTVAL INTO v_version_id FROM DUAL;
    INSERT INTO MFO_VERSION (
        VERSION_ID, FORMULARIO_ID, NUMERO, ESTADO, NOTAS, USUARIO_INS, FECHA_INS
    ) VALUES (
        v_version_id, v_formulario_id, 1, 'BORRADOR',
        'Version inicial sembrada por 15_MFO_SEMILLA_CAP.sql (requerimiento 16).',
        'SEMILLA', SYSDATE
    );

    -- ------------------------------------------------------------------------
    -- Seccion 1 - Datos generales (paso 1)
    -- ------------------------------------------------------------------------
    SELECT SEQ_MFO_SECCION.NEXTVAL INTO v_sec_datos FROM DUAL;
    INSERT INTO MFO_SECCION (
        SECCION_ID, VERSION_ID, CLAVE, TITULO, DESCRIPCION, ORDEN, COLUMNAS,
        ES_PASO, REPETIBLE, MIN_FILAS, MAX_FILAS, COLAPSABLE
    ) VALUES (
        v_sec_datos, v_version_id, 'DATOS', 'Datos de la solicitud',
        'Unidad solicitante y tipo de mantenimiento.', 10, 2,
        'S', 'N', NULL, NULL, 'N'
    );

    SELECT SEQ_MFO_CAMPO.NEXTVAL INTO v_c_unidad FROM DUAL;
    INSERT INTO MFO_CAMPO (
        CAMPO_ID, VERSION_ID, SECCION_ID, TIPO_CAMPO_ID, CLAVE, ETIQUETA, AYUDA,
        PLACEHOLDER, ORDEN, ANCHO, REQUERIDO, SOLO_LECTURA, VALOR_DEFECTO,
        ORIGEN_OPCIONES, CATALOGO_CLAVE, MASCARA, UNIDAD, EXPRESION
    ) VALUES (
        v_c_unidad, v_version_id, v_sec_datos, v_t_catalogo, 'UNIDAD',
        'Unidad solicitante', 'Unidad de trabajo que solicita la intervencion.',
        NULL, 10, 6, 'S', 'N', NULL, 'CATALOGO', 'BM_ICP', NULL, NULL, NULL
    );

    SELECT SEQ_MFO_CAMPO.NEXTVAL INTO v_c_tipo FROM DUAL;
    INSERT INTO MFO_CAMPO (
        CAMPO_ID, VERSION_ID, SECCION_ID, TIPO_CAMPO_ID, CLAVE, ETIQUETA, AYUDA,
        PLACEHOLDER, ORDEN, ANCHO, REQUERIDO, SOLO_LECTURA, VALOR_DEFECTO,
        ORIGEN_OPCIONES, CATALOGO_CLAVE, MASCARA, UNIDAD, EXPRESION
    ) VALUES (
        v_c_tipo, v_version_id, v_sec_datos, v_t_select, 'TIPO_SOL',
        'Tipo de mantenimiento', NULL, NULL, 20, 6,
        'S', 'N', 'PREVENTIVO', 'ESTATICA', NULL, NULL, NULL, NULL
    );

    INSERT INTO MFO_OPCION (OPCION_ID, CAMPO_ID, VALOR, ETIQUETA, ORDEN, GRUPO, ES_DEFECTO, ACTIVO)
    VALUES (SEQ_MFO_OPCION.NEXTVAL, v_c_tipo, 'PREVENTIVO', 'Preventivo', 10, NULL, 'S', 'S');
    INSERT INTO MFO_OPCION (OPCION_ID, CAMPO_ID, VALOR, ETIQUETA, ORDEN, GRUPO, ES_DEFECTO, ACTIVO)
    VALUES (SEQ_MFO_OPCION.NEXTVAL, v_c_tipo, 'CORRECTIVO', 'Correctivo', 20, NULL, 'N', 'S');

    SELECT SEQ_MFO_CAMPO.NEXTVAL INTO v_c_urgente FROM DUAL;
    INSERT INTO MFO_CAMPO (
        CAMPO_ID, VERSION_ID, SECCION_ID, TIPO_CAMPO_ID, CLAVE, ETIQUETA, AYUDA,
        PLACEHOLDER, ORDEN, ANCHO, REQUERIDO, SOLO_LECTURA, VALOR_DEFECTO,
        ORIGEN_OPCIONES, CATALOGO_CLAVE, MASCARA, UNIDAD, EXPRESION
    ) VALUES (
        v_c_urgente, v_version_id, v_sec_datos, v_t_booleano, 'URGENTE',
        'Es urgente', 'Marque si la intervencion no puede esperar al proximo ciclo.',
        NULL, 30, 6, 'N', 'N', 'N', NULL, NULL, NULL, NULL, NULL
    );

    -- El campo que la Fase 5 usa como prueba viva: esta OCULTO por defecto y es
    -- REQUERIDO. Sin evaluador de condiciones el formulario seria imposible de
    -- enviar, porque el validador exigiria un campo que el usuario no ve.
    SELECT SEQ_MFO_CAMPO.NEXTVAL INTO v_c_justifica FROM DUAL;
    INSERT INTO MFO_CAMPO (
        CAMPO_ID, VERSION_ID, SECCION_ID, TIPO_CAMPO_ID, CLAVE, ETIQUETA, AYUDA,
        PLACEHOLDER, ORDEN, ANCHO, REQUERIDO, SOLO_LECTURA, VALOR_DEFECTO,
        ORIGEN_OPCIONES, CATALOGO_CLAVE, MASCARA, UNIDAD, EXPRESION
    ) VALUES (
        v_c_justifica, v_version_id, v_sec_datos, v_t_largo, 'JUSTIFICA',
        'Justificacion de la urgencia', NULL, 'Explique por que no puede esperar',
        40, 12, 'S', 'N', NULL, NULL, NULL, NULL, NULL, NULL
    );

    -- ------------------------------------------------------------------------
    -- Seccion 2 - Bienes (paso 2). REPETIBLE: es la razon de existir de esta
    -- semilla. MIN_FILAS=1 obliga a al menos un bien; MAX_FILAS=10 acota.
    -- ------------------------------------------------------------------------
    SELECT SEQ_MFO_SECCION.NEXTVAL INTO v_sec_bienes FROM DUAL;
    INSERT INTO MFO_SECCION (
        SECCION_ID, VERSION_ID, CLAVE, TITULO, DESCRIPCION, ORDEN, COLUMNAS,
        ES_PASO, REPETIBLE, MIN_FILAS, MAX_FILAS, COLAPSABLE
    ) VALUES (
        v_sec_bienes, v_version_id, 'BIENES', 'Bienes a intervenir',
        'Agregue una fila por cada bien. Minimo 1, maximo 10.', 20, 3,
        'S', 'S', 1, 10, 'N'
    );

    SELECT SEQ_MFO_CAMPO.NEXTVAL INTO v_c_placa FROM DUAL;
    INSERT INTO MFO_CAMPO (
        CAMPO_ID, VERSION_ID, SECCION_ID, TIPO_CAMPO_ID, CLAVE, ETIQUETA, AYUDA,
        PLACEHOLDER, ORDEN, ANCHO, REQUERIDO, SOLO_LECTURA, VALOR_DEFECTO,
        ORIGEN_OPCIONES, CATALOGO_CLAVE, MASCARA, UNIDAD, EXPRESION
    ) VALUES (
        v_c_placa, v_version_id, v_sec_bienes, v_t_texto, 'PLACA',
        'Placa', NULL, 'Ej. 04379', 10, 4, 'S', 'N', NULL, NULL, NULL, NULL, NULL, NULL
    );

    SELECT SEQ_MFO_CAMPO.NEXTVAL INTO v_c_falla FROM DUAL;
    INSERT INTO MFO_CAMPO (
        CAMPO_ID, VERSION_ID, SECCION_ID, TIPO_CAMPO_ID, CLAVE, ETIQUETA, AYUDA,
        PLACEHOLDER, ORDEN, ANCHO, REQUERIDO, SOLO_LECTURA, VALOR_DEFECTO,
        ORIGEN_OPCIONES, CATALOGO_CLAVE, MASCARA, UNIDAD, EXPRESION
    ) VALUES (
        v_c_falla, v_version_id, v_sec_bienes, v_t_texto, 'FALLA',
        'Falla reportada', NULL, NULL, 20, 5, 'S', 'N', NULL, NULL, NULL, NULL, NULL, NULL
    );

    SELECT SEQ_MFO_CAMPO.NEXTVAL INTO v_c_costo FROM DUAL;
    INSERT INTO MFO_CAMPO (
        CAMPO_ID, VERSION_ID, SECCION_ID, TIPO_CAMPO_ID, CLAVE, ETIQUETA, AYUDA,
        PLACEHOLDER, ORDEN, ANCHO, REQUERIDO, SOLO_LECTURA, VALOR_DEFECTO,
        ORIGEN_OPCIONES, CATALOGO_CLAVE, MASCARA, UNIDAD, EXPRESION
    ) VALUES (
        v_c_costo, v_version_id, v_sec_bienes, v_t_moneda, 'COSTO_EST',
        'Costo estimado', NULL, NULL, 30, 3, 'N', 'N', NULL, NULL, NULL, NULL, 'Bs', NULL
    );

    -- ------------------------------------------------------------------------
    -- Seccion 3 - Cierre (paso 3). Su visibilidad depende del tipo de
    -- mantenimiento: es la condicion con destino SECCION.
    -- ------------------------------------------------------------------------
    SELECT SEQ_MFO_SECCION.NEXTVAL INTO v_sec_cierre FROM DUAL;
    INSERT INTO MFO_SECCION (
        SECCION_ID, VERSION_ID, CLAVE, TITULO, DESCRIPCION, ORDEN, COLUMNAS,
        ES_PASO, REPETIBLE, MIN_FILAS, MAX_FILAS, COLAPSABLE
    ) VALUES (
        v_sec_cierre, v_version_id, 'CIERRE', 'Detalle correctivo',
        'Solo aplica a mantenimiento correctivo.', 30, 2,
        'S', 'N', NULL, NULL, 'N'
    );

    SELECT SEQ_MFO_CAMPO.NEXTVAL INTO v_c_servicios FROM DUAL;
    INSERT INTO MFO_CAMPO (
        CAMPO_ID, VERSION_ID, SECCION_ID, TIPO_CAMPO_ID, CLAVE, ETIQUETA, AYUDA,
        PLACEHOLDER, ORDEN, ANCHO, REQUERIDO, SOLO_LECTURA, VALOR_DEFECTO,
        ORIGEN_OPCIONES, CATALOGO_CLAVE, MASCARA, UNIDAD, EXPRESION
    ) VALUES (
        v_c_servicios, v_version_id, v_sec_cierre, v_t_multi, 'SERVICIOS',
        'Servicios requeridos', 'Puede seleccionar varios.', NULL, 10, 6,
        'N', 'N', NULL, 'ESTATICA', NULL, NULL, NULL, NULL
    );

    INSERT INTO MFO_OPCION (OPCION_ID, CAMPO_ID, VALOR, ETIQUETA, ORDEN, GRUPO, ES_DEFECTO, ACTIVO)
    VALUES (SEQ_MFO_OPCION.NEXTVAL, v_c_servicios, 'ELECTRICIDAD', 'Electricidad', 10, NULL, 'N', 'S');
    INSERT INTO MFO_OPCION (OPCION_ID, CAMPO_ID, VALOR, ETIQUETA, ORDEN, GRUPO, ES_DEFECTO, ACTIVO)
    VALUES (SEQ_MFO_OPCION.NEXTVAL, v_c_servicios, 'PLOMERIA', 'Plomeria', 20, NULL, 'N', 'S');
    INSERT INTO MFO_OPCION (OPCION_ID, CAMPO_ID, VALOR, ETIQUETA, ORDEN, GRUPO, ES_DEFECTO, ACTIVO)
    VALUES (SEQ_MFO_OPCION.NEXTVAL, v_c_servicios, 'REFRIGERACION', 'Refrigeracion', 30, NULL, 'N', 'S');
    INSERT INTO MFO_OPCION (OPCION_ID, CAMPO_ID, VALOR, ETIQUETA, ORDEN, GRUPO, ES_DEFECTO, ACTIVO)
    VALUES (SEQ_MFO_OPCION.NEXTVAL, v_c_servicios, 'CARPINTERIA', 'Carpinteria', 40, NULL, 'N', 'S');

    SELECT SEQ_MFO_CAMPO.NEXTVAL INTO v_c_observ FROM DUAL;
    INSERT INTO MFO_CAMPO (
        CAMPO_ID, VERSION_ID, SECCION_ID, TIPO_CAMPO_ID, CLAVE, ETIQUETA, AYUDA,
        PLACEHOLDER, ORDEN, ANCHO, REQUERIDO, SOLO_LECTURA, VALOR_DEFECTO,
        ORIGEN_OPCIONES, CATALOGO_CLAVE, MASCARA, UNIDAD, EXPRESION
    ) VALUES (
        v_c_observ, v_version_id, v_sec_cierre, v_t_largo, 'OBSERV',
        'Observaciones', NULL, NULL, 20, 12, 'N', 'N', NULL, NULL, NULL, NULL, NULL, NULL
    );

    SELECT SEQ_MFO_CAMPO.NEXTVAL INTO v_c_foto FROM DUAL;
    INSERT INTO MFO_CAMPO (
        CAMPO_ID, VERSION_ID, SECCION_ID, TIPO_CAMPO_ID, CLAVE, ETIQUETA, AYUDA,
        PLACEHOLDER, ORDEN, ANCHO, REQUERIDO, SOLO_LECTURA, VALOR_DEFECTO,
        ORIGEN_OPCIONES, CATALOGO_CLAVE, MASCARA, UNIDAD, EXPRESION
    ) VALUES (
        v_c_foto, v_version_id, v_sec_cierre, v_t_archivo, 'FOTO',
        'Fotografia de la falla', 'Formatos permitidos: jpg, png, pdf.', NULL,
        30, 6, 'N', 'N', NULL, NULL, NULL, NULL, NULL, NULL
    );

    -- ------------------------------------------------------------------------
    -- Reglas
    -- ------------------------------------------------------------------------
    INSERT INTO MFO_REGLA (REGLA_ID, CAMPO_ID, TIPO_REGLA, PARAM_1, PARAM_2, MENSAJE, ORDEN, ACTIVO)
    VALUES (SEQ_MFO_REGLA.NEXTVAL, v_c_unidad, 'REQUERIDO', NULL, NULL,
            'Indique la unidad solicitante.', 10, 'S');

    INSERT INTO MFO_REGLA (REGLA_ID, CAMPO_ID, TIPO_REGLA, PARAM_1, PARAM_2, MENSAJE, ORDEN, ACTIVO)
    VALUES (SEQ_MFO_REGLA.NEXTVAL, v_c_tipo, 'REQUERIDO', NULL, NULL,
            'Indique el tipo de mantenimiento.', 10, 'S');

    INSERT INTO MFO_REGLA (REGLA_ID, CAMPO_ID, TIPO_REGLA, PARAM_1, PARAM_2, MENSAJE, ORDEN, ACTIVO)
    VALUES (SEQ_MFO_REGLA.NEXTVAL, v_c_justifica, 'REQUERIDO', NULL, NULL,
            'Justifique la urgencia.', 10, 'S');

    INSERT INTO MFO_REGLA (REGLA_ID, CAMPO_ID, TIPO_REGLA, PARAM_1, PARAM_2, MENSAJE, ORDEN, ACTIVO)
    VALUES (SEQ_MFO_REGLA.NEXTVAL, v_c_justifica, 'LONG_MIN', '20', NULL,
            'La justificacion debe tener al menos 20 caracteres.', 20, 'S');

    INSERT INTO MFO_REGLA (REGLA_ID, CAMPO_ID, TIPO_REGLA, PARAM_1, PARAM_2, MENSAJE, ORDEN, ACTIVO)
    VALUES (SEQ_MFO_REGLA.NEXTVAL, v_c_placa, 'REQUERIDO', NULL, NULL,
            'Indique la placa del bien.', 10, 'S');

    INSERT INTO MFO_REGLA (REGLA_ID, CAMPO_ID, TIPO_REGLA, PARAM_1, PARAM_2, MENSAJE, ORDEN, ACTIVO)
    VALUES (SEQ_MFO_REGLA.NEXTVAL, v_c_placa, 'LONG_MAX', '10', NULL,
            'La placa no puede exceder 10 caracteres.', 20, 'S');

    INSERT INTO MFO_REGLA (REGLA_ID, CAMPO_ID, TIPO_REGLA, PARAM_1, PARAM_2, MENSAJE, ORDEN, ACTIVO)
    VALUES (SEQ_MFO_REGLA.NEXTVAL, v_c_falla, 'REQUERIDO', NULL, NULL,
            'Describa la falla reportada.', 10, 'S');

    INSERT INTO MFO_REGLA (REGLA_ID, CAMPO_ID, TIPO_REGLA, PARAM_1, PARAM_2, MENSAJE, ORDEN, ACTIVO)
    VALUES (SEQ_MFO_REGLA.NEXTVAL, v_c_costo, 'MIN', '0', NULL,
            'El costo estimado no puede ser negativo.', 10, 'S');

    INSERT INTO MFO_REGLA (REGLA_ID, CAMPO_ID, TIPO_REGLA, PARAM_1, PARAM_2, MENSAJE, ORDEN, ACTIVO)
    VALUES (SEQ_MFO_REGLA.NEXTVAL, v_c_servicios, 'SEL_MAX', '3', NULL,
            'Seleccione a lo sumo 3 servicios.', 10, 'S');

    INSERT INTO MFO_REGLA (REGLA_ID, CAMPO_ID, TIPO_REGLA, PARAM_1, PARAM_2, MENSAJE, ORDEN, ACTIVO)
    VALUES (SEQ_MFO_REGLA.NEXTVAL, v_c_foto, 'ARCH_EXT', 'jpg,jpeg,png,pdf', NULL,
            'Solo se admiten archivos jpg, jpeg, png o pdf.', 10, 'S');

    INSERT INTO MFO_REGLA (REGLA_ID, CAMPO_ID, TIPO_REGLA, PARAM_1, PARAM_2, MENSAJE, ORDEN, ACTIVO)
    VALUES (SEQ_MFO_REGLA.NEXTVAL, v_c_foto, 'ARCH_MAX_MB', '5', NULL,
            'El archivo no puede superar 5 MB.', 20, 'S');

    -- ------------------------------------------------------------------------
    -- Condiciones. Son la razon principal de esta semilla.
    --
    -- 1 y 2 van juntas y a proposito: JUSTIFICA es REQUERIDO en su definicion,
    -- pero solo se MUESTRA cuando URGENTE='S'. El par MOSTRAR+EXIGIR es lo que
    -- el validador tiene que entender para no bloquear un envio por un campo
    -- que el usuario nunca vio. Es el caso de MfoCondicionesTests.
    --
    -- 3 usa destino SECCION y operador EN_LISTA, para que el evaluador se pruebe
    -- con los dos destinos y con un operador de conjunto, no solo con IGUAL.
    -- ------------------------------------------------------------------------
    INSERT INTO MFO_CONDICION (
        CONDICION_ID, VERSION_ID, ACCION, DESTINO_TIPO, DESTINO_ID,
        CAMPO_ORIGEN_ID, OPERADOR, VALOR_COMPARA, GRUPO, CONECTOR, ORDEN
    ) VALUES (
        SEQ_MFO_CONDICION.NEXTVAL, v_version_id, 'MOSTRAR', 'CAMPO', v_c_justifica,
        v_c_urgente, 'IGUAL', 'S', 1, 'Y', 10
    );

    INSERT INTO MFO_CONDICION (
        CONDICION_ID, VERSION_ID, ACCION, DESTINO_TIPO, DESTINO_ID,
        CAMPO_ORIGEN_ID, OPERADOR, VALOR_COMPARA, GRUPO, CONECTOR, ORDEN
    ) VALUES (
        SEQ_MFO_CONDICION.NEXTVAL, v_version_id, 'EXIGIR', 'CAMPO', v_c_justifica,
        v_c_urgente, 'IGUAL', 'S', 2, 'Y', 20
    );

    INSERT INTO MFO_CONDICION (
        CONDICION_ID, VERSION_ID, ACCION, DESTINO_TIPO, DESTINO_ID,
        CAMPO_ORIGEN_ID, OPERADOR, VALOR_COMPARA, GRUPO, CONECTOR, ORDEN
    ) VALUES (
        SEQ_MFO_CONDICION.NEXTVAL, v_version_id, 'MOSTRAR', 'SECCION', v_sec_cierre,
        v_c_tipo, 'EN_LISTA', 'CORRECTIVO', 3, 'Y', 30
    );

    -- ------------------------------------------------------------------------
    -- Publicacion
    -- ------------------------------------------------------------------------
    UPDATE MFO_VERSION
       SET ESTADO       = 'PUBLICADA',
           FECHA_PUBL   = SYSDATE,
           USUARIO_PUBL = 'SEMILLA'
     WHERE VERSION_ID = v_version_id;

    UPDATE MFO_FORMULARIO
       SET VERSION_PUBL_ID = v_version_id
     WHERE FORMULARIO_ID = v_formulario_id;

    INSERT INTO MFO_AUDITORIA (AUDITORIA_ID, ENTIDAD, ENTIDAD_ID, ACCION, USUARIO, FECHA, DETALLE_CLB)
    VALUES (SEQ_MFO_AUDITORIA.NEXTVAL, 'VERSION', v_version_id, 'PUBLICAR', 'SEMILLA', SYSDATE,
            'Publicada por 15_MFO_SEMILLA_CAP.sql');

    COMMIT;
    DBMS_OUTPUT.PUT_LINE('SOL_MANT sembrado y publicado. VERSION_ID = ' || v_version_id);
END;
/
