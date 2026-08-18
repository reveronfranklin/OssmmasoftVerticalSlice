-- =============================================================================
-- Motor de Formularios (MFO) - Fase 1 - Formulario de referencia
-- Requerimiento 16. Decision 3 de la Fase 0.
--
-- Formulario de referencia: los parametros del reporte de Bienes Municipales,
-- que hoy estan codificados a mano en la pantalla del frontend y cuyo endpoint
-- ya existe (POST api/ReporteBm1/pdf, ReporteBm1GetAllQuery).
--
-- Este script es lo que desbloquea la Fase 6 sin necesitar diseñador: siembra el
-- formulario con INSERT directos y lo deja PUBLICADO.
--
-- Se eligio un caso real y no un formulario sintetico a proposito: el riesgo que
-- el plan señala es congelar el DDL en abstracto, y un caso real es lo unico que
-- lo refuta. La contrapartida honesta es que este formulario **no ejercita
-- secciones repetibles ni condiciones**, porque no las necesita. Esas dos
-- capacidades quedan cubiertas por el diseñador (Fase 7) o por una segunda
-- semilla, y hasta entonces el criterio de aceptacion de la Fase 6 sobre
-- condiciones en vivo no se puede dar por cerrado con este formulario solo.
--
-- La publicacion se hace en dos pasos (insertar en BORRADOR y luego pasar a
-- PUBLICADA) porque TRG_MFO_VER_LOCK solo admite esa transicion; sembrar
-- directamente en PUBLICADA fallaria, y ademas dejaria los hijos sin proteccion
-- durante la carga.
-- =============================================================================

DECLARE
    v_formulario_id  MFO_FORMULARIO.FORMULARIO_ID%TYPE;
    v_version_id     MFO_VERSION.VERSION_ID%TYPE;
    v_seccion_id     MFO_SECCION.SECCION_ID%TYPE;
    v_campo_desde    MFO_CAMPO.CAMPO_ID%TYPE;
    v_campo_hasta    MFO_CAMPO.CAMPO_ID%TYPE;
    v_campo_icp      MFO_CAMPO.CAMPO_ID%TYPE;
    v_tipo_fecha     MFO_TIPO_CAMPO.TIPO_CAMPO_ID%TYPE;
    v_tipo_multi     MFO_TIPO_CAMPO.TIPO_CAMPO_ID%TYPE;
    v_existe         NUMBER;

    -- CODIGO_EMPRESA de settings:EmpresaConfig del vertical slice.
    c_empresa CONSTANT NUMBER := 13;
BEGIN
    SELECT COUNT(*) INTO v_existe FROM MFO_FORMULARIO WHERE ALIAS = 'REP_BM1';
    IF v_existe > 0 THEN
        DBMS_OUTPUT.PUT_LINE('REP_BM1 ya existe. No se siembra de nuevo.');
        RETURN;
    END IF;

    SELECT TIPO_CAMPO_ID INTO v_tipo_fecha FROM MFO_TIPO_CAMPO WHERE CODIGO = 'FECHA';
    SELECT TIPO_CAMPO_ID INTO v_tipo_multi FROM MFO_TIPO_CAMPO WHERE CODIGO = 'MULTI_SELECT';

    -- ------------------------------------------------------------------------
    -- Formulario
    -- ------------------------------------------------------------------------
    SELECT SEQ_MFO_FORMULARIO.NEXTVAL INTO v_formulario_id FROM DUAL;
    INSERT INTO MFO_FORMULARIO (
        FORMULARIO_ID, ALIAS, NOMBRE, DESCRIPCION, CATEGORIA, CODIGO_EMPRESA,
        ESTADO, VERSION_PUBL_ID, ENTIDAD_DESTINO, MAX_RESP_USUARIO,
        PERMITE_BORRADOR, USUARIO_INS, FECHA_INS
    ) VALUES (
        v_formulario_id, 'REP_BM1',
        'Parametros - Reporte de Bienes Municipales',
        'Filtros del reporte de bienes municipales. Sustituye el dialogo de parametros codificado en la pantalla.',
        'Reportes', c_empresa,
        'ACTIVO', NULL, NULL, NULL,
        'N', 'SEMILLA', SYSDATE
    );

    -- ------------------------------------------------------------------------
    -- Version 1, en BORRADOR mientras se cargan los hijos
    -- ------------------------------------------------------------------------
    SELECT SEQ_MFO_VERSION.NEXTVAL INTO v_version_id FROM DUAL;
    INSERT INTO MFO_VERSION (
        VERSION_ID, FORMULARIO_ID, NUMERO, ESTADO, NOTAS, USUARIO_INS, FECHA_INS
    ) VALUES (
        v_version_id, v_formulario_id, 1, 'BORRADOR',
        'Version inicial sembrada por 08_MFO_SEMILLA_DEMO.sql (requerimiento 16).',
        'SEMILLA', SYSDATE
    );

    -- ------------------------------------------------------------------------
    -- Seccion unica
    -- ------------------------------------------------------------------------
    SELECT SEQ_MFO_SECCION.NEXTVAL INTO v_seccion_id FROM DUAL;
    INSERT INTO MFO_SECCION (
        SECCION_ID, VERSION_ID, CLAVE, TITULO, DESCRIPCION, ORDEN, COLUMNAS,
        ES_PASO, REPETIBLE, MIN_FILAS, MAX_FILAS, COLAPSABLE
    ) VALUES (
        v_seccion_id, v_version_id, 'FILTROS', 'Filtros del reporte',
        'Rango de fechas y unidades de trabajo a incluir.', 10, 2,
        'N', 'N', NULL, NULL, 'N'
    );

    -- ------------------------------------------------------------------------
    -- Campos. Las CLAVE se eligen para mapear 1 a 1 con ReporteBm1GetAllQuery
    -- (FechaDesde, FechaHasta, CodigosIcp); el mapeo explicito vive igualmente
    -- en MFO_REP_PARAM, que no asume que los nombres coincidan.
    -- ------------------------------------------------------------------------
    SELECT SEQ_MFO_CAMPO.NEXTVAL INTO v_campo_desde FROM DUAL;
    INSERT INTO MFO_CAMPO (
        CAMPO_ID, VERSION_ID, SECCION_ID, TIPO_CAMPO_ID, CLAVE, ETIQUETA, AYUDA,
        PLACEHOLDER, ORDEN, ANCHO, REQUERIDO, SOLO_LECTURA, VALOR_DEFECTO,
        ORIGEN_OPCIONES, CATALOGO_CLAVE, MASCARA, UNIDAD, EXPRESION
    ) VALUES (
        v_campo_desde, v_version_id, v_seccion_id, v_tipo_fecha, 'FECHA_DESDE',
        'Desde', 'Fecha inicial del rango de movimientos.', NULL, 10, 6,
        'S', 'N', NULL, NULL, NULL, NULL, NULL, NULL
    );

    SELECT SEQ_MFO_CAMPO.NEXTVAL INTO v_campo_hasta FROM DUAL;
    INSERT INTO MFO_CAMPO (
        CAMPO_ID, VERSION_ID, SECCION_ID, TIPO_CAMPO_ID, CLAVE, ETIQUETA, AYUDA,
        PLACEHOLDER, ORDEN, ANCHO, REQUERIDO, SOLO_LECTURA, VALOR_DEFECTO,
        ORIGEN_OPCIONES, CATALOGO_CLAVE, MASCARA, UNIDAD, EXPRESION
    ) VALUES (
        v_campo_hasta, v_version_id, v_seccion_id, v_tipo_fecha, 'FECHA_HASTA',
        'Hasta', 'Fecha final del rango de movimientos.', NULL, 20, 6,
        'S', 'N', NULL, NULL, NULL, NULL, NULL, NULL
    );

    -- CATALOGO_CLAVE = 'BM_ICP'. El backend la resuelve contra la lista blanca de
    -- MfoCatalogo (Fase 5) hacia GET api/ReporteBm1/GetIcps. La clave nunca se
    -- concatena en SQL.
    SELECT SEQ_MFO_CAMPO.NEXTVAL INTO v_campo_icp FROM DUAL;
    INSERT INTO MFO_CAMPO (
        CAMPO_ID, VERSION_ID, SECCION_ID, TIPO_CAMPO_ID, CLAVE, ETIQUETA, AYUDA,
        PLACEHOLDER, ORDEN, ANCHO, REQUERIDO, SOLO_LECTURA, VALOR_DEFECTO,
        ORIGEN_OPCIONES, CATALOGO_CLAVE, MASCARA, UNIDAD, EXPRESION
    ) VALUES (
        v_campo_icp, v_version_id, v_seccion_id, v_tipo_multi, 'CODIGOS_ICP',
        'ICP', 'Unidades de trabajo a incluir. Vacio = todas.', 'Todas', 30, 12,
        'N', 'N', NULL, 'CATALOGO', 'BM_ICP', NULL, NULL, NULL
    );

    -- ------------------------------------------------------------------------
    -- Reglas. FEC_MAX = HOY porque no existen movimientos con fecha futura; es
    -- la misma restriccion que hoy impone la pantalla codificada.
    -- ------------------------------------------------------------------------
    INSERT INTO MFO_REGLA (REGLA_ID, CAMPO_ID, TIPO_REGLA, PARAM_1, PARAM_2, MENSAJE, ORDEN, ACTIVO)
    VALUES (SEQ_MFO_REGLA.NEXTVAL, v_campo_desde, 'REQUERIDO', NULL, NULL,
            'Indique la fecha desde.', 10, 'S');

    INSERT INTO MFO_REGLA (REGLA_ID, CAMPO_ID, TIPO_REGLA, PARAM_1, PARAM_2, MENSAJE, ORDEN, ACTIVO)
    VALUES (SEQ_MFO_REGLA.NEXTVAL, v_campo_desde, 'FEC_MAX', 'HOY', NULL,
            'La fecha desde no puede ser futura.', 20, 'S');

    INSERT INTO MFO_REGLA (REGLA_ID, CAMPO_ID, TIPO_REGLA, PARAM_1, PARAM_2, MENSAJE, ORDEN, ACTIVO)
    VALUES (SEQ_MFO_REGLA.NEXTVAL, v_campo_hasta, 'REQUERIDO', NULL, NULL,
            'Indique la fecha hasta.', 10, 'S');

    INSERT INTO MFO_REGLA (REGLA_ID, CAMPO_ID, TIPO_REGLA, PARAM_1, PARAM_2, MENSAJE, ORDEN, ACTIVO)
    VALUES (SEQ_MFO_REGLA.NEXTVAL, v_campo_hasta, 'FEC_MAX', 'HOY', NULL,
            'La fecha hasta no puede ser futura.', 20, 'S');

    -- ------------------------------------------------------------------------
    -- Publicacion. Transicion BORRADOR -> PUBLICADA, la unica que admite
    -- TRG_MFO_VER_LOCK, y puntero del formulario a la version vigente.
    -- HASH_DEF queda nulo: lo calcula SP_MFO_VER_PUBLICAR (Fase 2), que todavia
    -- no existe cuando corre esta semilla.
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
            'Publicada por 08_MFO_SEMILLA_DEMO.sql');

    COMMIT;
    DBMS_OUTPUT.PUT_LINE('REP_BM1 sembrado y publicado. VERSION_ID = ' || v_version_id);
END;
/

-- -----------------------------------------------------------------------------
-- PERMISOS - pendiente de dato real.
--
-- Decision 4 de la Fase 0: ROL_CODIGO guarda el codigo de rol de
-- SIS.OSS_USUARIO_ROL. No se siembra ninguna fila porque los codigos de rol
-- reales de esta instalacion no estan en el repositorio, y sembrar un codigo
-- inventado dejaria un permiso que no corresponde a nadie.
--
-- Para habilitar el formulario, ejecutar con el codigo de rol que corresponda:
--
-- INSERT INTO MFO_PERMISO (PERMISO_ID, FORMULARIO_ID, ROL_CODIGO, ACCION)
-- SELECT SEQ_MFO_PERMISO.NEXTVAL, FORMULARIO_ID, '<ROL>', 'LLENAR'
--   FROM MFO_FORMULARIO WHERE ALIAS = 'REP_BM1';
-- COMMIT;
-- -----------------------------------------------------------------------------
