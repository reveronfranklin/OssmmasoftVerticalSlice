-- =============================================================================
-- Motor de Formularios (MFO) - Formulario de parametros del BM-1 Especial
-- Requerimiento 27, integrado al requerimiento 16.
--
-- El requerimiento 27 preveia una pantalla de parametros codificada a mano
-- (su Fase 4). En su lugar se define aqui como formulario del motor: es
-- exactamente el caso de uso para el que existe el modo PARAMETROS, y significa
-- que los filtros se pueden cambiar despues sin tocar codigo ni desplegar.
--
-- Todos los filtros son opcionales, como en el reporte legado BM_BM1_ESP1.rdf:
-- sin ninguno, imprime el inventario completo de la empresa.
--
-- Lo que NO se incluye, siguiendo lo propuesto en el analisis del
-- requerimiento 27:
--   * Serial (P_SERIAL_BA_INI/FIN): su logica fija TIPO_ESPECIFICACION_ID=803
--     en el codigo y el analisis no pudo confirmar que siga vigente.
--   * P_ESPECIFICACION / P_ESPECIFICACION_ID: sin punto de uso confirmado en el
--     query principal.
--   * P_CODIGO_BIEN: parametro declarado sin uso detectado.
-- Agregarlos despues es una fila mas en este formulario y un filtro mas en el
-- SP; omitirlos ahora evita exponer controles que no filtran nada.
--
-- Requiere: INSTALL_MFO.sql, INSTALL_MFO_DEF.sql e INSTALL_MFO_REP.sql.
-- Se ejecuta conectado como MFO.
-- =============================================================================

SET SERVEROUTPUT ON

DECLARE
    v_formulario_id  MFO_FORMULARIO.FORMULARIO_ID%TYPE;
    v_version_id     MFO_VERSION.VERSION_ID%TYPE;
    v_seccion_id     MFO_SECCION.SECCION_ID%TYPE;
    v_reporte_id     MFO_REPORTE.REPORTE_ID%TYPE;
    v_c_unidad       MFO_CAMPO.CAMPO_ID%TYPE;
    v_c_placa_ini    MFO_CAMPO.CAMPO_ID%TYPE;
    v_c_placa_fin    MFO_CAMPO.CAMPO_ID%TYPE;
    v_c_articulo     MFO_CAMPO.CAMPO_ID%TYPE;
    v_c_fecha_ini    MFO_CAMPO.CAMPO_ID%TYPE;
    v_c_fecha_fin    MFO_CAMPO.CAMPO_ID%TYPE;
    v_c_responsable  MFO_CAMPO.CAMPO_ID%TYPE;
    v_t_catalogo     MFO_TIPO_CAMPO.TIPO_CAMPO_ID%TYPE;
    v_t_texto        MFO_TIPO_CAMPO.TIPO_CAMPO_ID%TYPE;
    v_t_numero       MFO_TIPO_CAMPO.TIPO_CAMPO_ID%TYPE;
    v_t_fecha        MFO_TIPO_CAMPO.TIPO_CAMPO_ID%TYPE;
    v_existe         NUMBER;

    c_empresa CONSTANT NUMBER := 13;
BEGIN
    SELECT COUNT(*) INTO v_existe FROM MFO_FORMULARIO WHERE ALIAS = 'REP_BM1_ESP';
    IF v_existe > 0 THEN
        DBMS_OUTPUT.PUT_LINE('REP_BM1_ESP ya existe. No se siembra de nuevo.');
        RETURN;
    END IF;

    SELECT TIPO_CAMPO_ID INTO v_t_catalogo FROM MFO_TIPO_CAMPO WHERE CODIGO = 'CATALOGO';
    SELECT TIPO_CAMPO_ID INTO v_t_texto    FROM MFO_TIPO_CAMPO WHERE CODIGO = 'TEXTO';
    SELECT TIPO_CAMPO_ID INTO v_t_numero   FROM MFO_TIPO_CAMPO WHERE CODIGO = 'NUMERO';
    SELECT TIPO_CAMPO_ID INTO v_t_fecha    FROM MFO_TIPO_CAMPO WHERE CODIGO = 'FECHA';

    -- ------------------------------------------------------------------------
    -- Formulario. REGISTRA_EJEC='N': una consulta de reporte no es una respuesta
    -- de negocio; la trazabilidad la da MFO_REP_EJEC, que se escribe siempre.
    -- ------------------------------------------------------------------------
    SELECT SEQ_MFO_FORMULARIO.NEXTVAL INTO v_formulario_id FROM DUAL;
    INSERT INTO MFO_FORMULARIO (
        FORMULARIO_ID, ALIAS, NOMBRE, DESCRIPCION, CATEGORIA, CODIGO_EMPRESA,
        ESTADO, VERSION_PUBL_ID, ENTIDAD_DESTINO, MAX_RESP_USUARIO,
        PERMITE_BORRADOR, MODO_USO, REGISTRA_EJEC, USUARIO_INS, FECHA_INS
    ) VALUES (
        v_formulario_id, 'REP_BM1_ESP',
        'Parametros - Inventario de Bienes Muebles BM-1',
        'Filtros del formulario oficial BM-1. Todos opcionales: sin ninguno imprime el inventario completo.',
        'Reportes', c_empresa,
        'ACTIVO', NULL, NULL, NULL,
        'N', 'PARAMETROS', 'N', 'SEMILLA', SYSDATE
    );

    SELECT SEQ_MFO_VERSION.NEXTVAL INTO v_version_id FROM DUAL;
    INSERT INTO MFO_VERSION (
        VERSION_ID, FORMULARIO_ID, NUMERO, ESTADO, NOTAS, USUARIO_INS, FECHA_INS
    ) VALUES (
        v_version_id, v_formulario_id, 1, 'BORRADOR',
        'Version inicial sembrada por 18_MFO_SEMILLA_BM1ESP.sql (requerimiento 27).',
        'SEMILLA', SYSDATE
    );

    SELECT SEQ_MFO_SECCION.NEXTVAL INTO v_seccion_id FROM DUAL;
    INSERT INTO MFO_SECCION (
        SECCION_ID, VERSION_ID, CLAVE, TITULO, DESCRIPCION, ORDEN, COLUMNAS,
        ES_PASO, REPETIBLE, MIN_FILAS, MAX_FILAS, COLAPSABLE
    ) VALUES (
        v_seccion_id, v_version_id, 'FILTROS', 'Filtros del formulario BM-1',
        'Deje en blanco los que no quiera aplicar.', 10, 2,
        'N', 'N', NULL, NULL, 'N'
    );

    -- ------------------------------------------------------------------------
    -- Campos. Las CLAVE se eligen para leerse claro en la pantalla; el mapeo al
    -- nombre que espera el reporte vive en MFO_REP_PARAM, que no asume que
    -- coincidan.
    -- ------------------------------------------------------------------------
    SELECT SEQ_MFO_CAMPO.NEXTVAL INTO v_c_unidad FROM DUAL;
    INSERT INTO MFO_CAMPO (
        CAMPO_ID, VERSION_ID, SECCION_ID, TIPO_CAMPO_ID, CLAVE, ETIQUETA, AYUDA,
        PLACEHOLDER, ORDEN, ANCHO, REQUERIDO, SOLO_LECTURA, VALOR_DEFECTO,
        ORIGEN_OPCIONES, CATALOGO_CLAVE, MASCARA, UNIDAD, EXPRESION
    ) VALUES (
        v_c_unidad, v_version_id, v_seccion_id, v_t_catalogo, 'UNIDAD',
        'Unidad o dependencia', 'Vacio = todas las unidades.', 'Todas', 10, 6,
        'N', 'N', NULL, 'CATALOGO', 'BM_DIR_BIEN', NULL, NULL, NULL
    );

    SELECT SEQ_MFO_CAMPO.NEXTVAL INTO v_c_articulo FROM DUAL;
    INSERT INTO MFO_CAMPO (
        CAMPO_ID, VERSION_ID, SECCION_ID, TIPO_CAMPO_ID, CLAVE, ETIQUETA, AYUDA,
        PLACEHOLDER, ORDEN, ANCHO, REQUERIDO, SOLO_LECTURA, VALOR_DEFECTO,
        ORIGEN_OPCIONES, CATALOGO_CLAVE, MASCARA, UNIDAD, EXPRESION
    ) VALUES (
        v_c_articulo, v_version_id, v_seccion_id, v_t_numero, 'ARTICULO',
        'Codigo de articulo', 'Vacio = todos los articulos.', NULL, 20, 6,
        'N', 'N', NULL, NULL, NULL, NULL, NULL, NULL
    );

    SELECT SEQ_MFO_CAMPO.NEXTVAL INTO v_c_placa_ini FROM DUAL;
    INSERT INTO MFO_CAMPO (
        CAMPO_ID, VERSION_ID, SECCION_ID, TIPO_CAMPO_ID, CLAVE, ETIQUETA, AYUDA,
        PLACEHOLDER, ORDEN, ANCHO, REQUERIDO, SOLO_LECTURA, VALOR_DEFECTO,
        ORIGEN_OPCIONES, CATALOGO_CLAVE, MASCARA, UNIDAD, EXPRESION
    ) VALUES (
        v_c_placa_ini, v_version_id, v_seccion_id, v_t_texto, 'PLACA_DESDE',
        'Placa desde', NULL, 'Ej. 04379', 30, 6,
        'N', 'N', NULL, NULL, NULL, NULL, NULL, NULL
    );

    SELECT SEQ_MFO_CAMPO.NEXTVAL INTO v_c_placa_fin FROM DUAL;
    INSERT INTO MFO_CAMPO (
        CAMPO_ID, VERSION_ID, SECCION_ID, TIPO_CAMPO_ID, CLAVE, ETIQUETA, AYUDA,
        PLACEHOLDER, ORDEN, ANCHO, REQUERIDO, SOLO_LECTURA, VALOR_DEFECTO,
        ORIGEN_OPCIONES, CATALOGO_CLAVE, MASCARA, UNIDAD, EXPRESION
    ) VALUES (
        v_c_placa_fin, v_version_id, v_seccion_id, v_t_texto, 'PLACA_HASTA',
        'Placa hasta', NULL, NULL, 40, 6,
        'N', 'N', NULL, NULL, NULL, NULL, NULL, NULL
    );

    SELECT SEQ_MFO_CAMPO.NEXTVAL INTO v_c_fecha_ini FROM DUAL;
    INSERT INTO MFO_CAMPO (
        CAMPO_ID, VERSION_ID, SECCION_ID, TIPO_CAMPO_ID, CLAVE, ETIQUETA, AYUDA,
        PLACEHOLDER, ORDEN, ANCHO, REQUERIDO, SOLO_LECTURA, VALOR_DEFECTO,
        ORIGEN_OPCIONES, CATALOGO_CLAVE, MASCARA, UNIDAD, EXPRESION
    ) VALUES (
        v_c_fecha_ini, v_version_id, v_seccion_id, v_t_fecha, 'FECHA_DESDE',
        'Fecha de registro desde', 'Sobre la fecha de movimiento del bien.', NULL, 50, 6,
        'N', 'N', NULL, NULL, NULL, NULL, NULL, NULL
    );

    SELECT SEQ_MFO_CAMPO.NEXTVAL INTO v_c_fecha_fin FROM DUAL;
    INSERT INTO MFO_CAMPO (
        CAMPO_ID, VERSION_ID, SECCION_ID, TIPO_CAMPO_ID, CLAVE, ETIQUETA, AYUDA,
        PLACEHOLDER, ORDEN, ANCHO, REQUERIDO, SOLO_LECTURA, VALOR_DEFECTO,
        ORIGEN_OPCIONES, CATALOGO_CLAVE, MASCARA, UNIDAD, EXPRESION
    ) VALUES (
        v_c_fecha_fin, v_version_id, v_seccion_id, v_t_fecha, 'FECHA_HASTA',
        'Fecha de registro hasta', NULL, NULL, 60, 6,
        'N', 'N', NULL, NULL, NULL, NULL, NULL, NULL
    );

    -- No es un filtro: es el nombre que se imprime en el bloque de firmas, igual
    -- que el P_USUARIO_RESPONSABLE del reporte original.
    SELECT SEQ_MFO_CAMPO.NEXTVAL INTO v_c_responsable FROM DUAL;
    INSERT INTO MFO_CAMPO (
        CAMPO_ID, VERSION_ID, SECCION_ID, TIPO_CAMPO_ID, CLAVE, ETIQUETA, AYUDA,
        PLACEHOLDER, ORDEN, ANCHO, REQUERIDO, SOLO_LECTURA, VALOR_DEFECTO,
        ORIGEN_OPCIONES, CATALOGO_CLAVE, MASCARA, UNIDAD, EXPRESION
    ) VALUES (
        v_c_responsable, v_version_id, v_seccion_id, v_t_texto, 'RESPONSABLE',
        'Responsable', 'Se imprime en el bloque de firmas. No filtra.', NULL, 70, 12,
        'N', 'N', NULL, NULL, NULL, NULL, NULL, NULL
    );

    -- Coherencia de los dos rangos. El backend la valida igual, pero avisar en
    -- pantalla evita un viaje al servidor para decir lo obvio.
    INSERT INTO MFO_REGLA (REGLA_ID, CAMPO_ID, TIPO_REGLA, PARAM_1, PARAM_2, MENSAJE, ORDEN, ACTIVO)
    VALUES (SEQ_MFO_REGLA.NEXTVAL, v_c_fecha_ini, 'FEC_MAX', 'HOY', NULL,
            'La fecha desde no puede ser futura.', 10, 'S');

    INSERT INTO MFO_REGLA (REGLA_ID, CAMPO_ID, TIPO_REGLA, PARAM_1, PARAM_2, MENSAJE, ORDEN, ACTIVO)
    VALUES (SEQ_MFO_REGLA.NEXTVAL, v_c_fecha_fin, 'FEC_MAX', 'HOY', NULL,
            'La fecha hasta no puede ser futura.', 10, 'S');

    INSERT INTO MFO_REGLA (REGLA_ID, CAMPO_ID, TIPO_REGLA, PARAM_1, PARAM_2, MENSAJE, ORDEN, ACTIVO)
    VALUES (SEQ_MFO_REGLA.NEXTVAL, v_c_placa_ini, 'LONG_MAX', '20', NULL,
            'La placa no puede exceder 20 caracteres.', 10, 'S');

    INSERT INTO MFO_REGLA (REGLA_ID, CAMPO_ID, TIPO_REGLA, PARAM_1, PARAM_2, MENSAJE, ORDEN, ACTIVO)
    VALUES (SEQ_MFO_REGLA.NEXTVAL, v_c_placa_fin, 'LONG_MAX', '20', NULL,
            'La placa no puede exceder 20 caracteres.', 10, 'S');

    -- ------------------------------------------------------------------------
    -- Publicacion. BORRADOR -> PUBLICADA es la unica transicion que admite
    -- TRG_MFO_VER_LOCK.
    -- ------------------------------------------------------------------------
    UPDATE MFO_VERSION
       SET ESTADO = 'PUBLICADA', FECHA_PUBL = SYSDATE, USUARIO_PUBL = 'SEMILLA'
     WHERE VERSION_ID = v_version_id;

    UPDATE MFO_FORMULARIO
       SET VERSION_PUBL_ID = v_version_id
     WHERE FORMULARIO_ID = v_formulario_id;

    -- ------------------------------------------------------------------------
    -- Enlace al reporte. MAX_FILAS = 5000 bienes: sin filtros, este reporte
    -- imprime el inventario completo, y el corte por unidad completa del
    -- ejecutor evita un PDF de cientos de paginas pedido por error.
    -- ------------------------------------------------------------------------
    SELECT SEQ_MFO_REPORTE.NEXTVAL INTO v_reporte_id FROM DUAL;
    INSERT INTO MFO_REPORTE (
        REPORTE_ID, FORMULARIO_ID, CLAVE, NOMBRE, DESCRIPCION, TIPO_EJEC,
        CLAVE_REGISTRO, TITULO_REPORTE, ORIENTACION, MAX_FILAS, TIMEOUT_SEG,
        ORDEN, ACTIVO, USUARIO_INS, FECHA_INS
    ) VALUES (
        v_reporte_id, v_formulario_id, 'BM1_ESP',
        'Inventario de Bienes Muebles BM-1 (PDF)',
        'Formulario oficial BM-1 con encabezado de entidad, agrupamiento por unidad, subtotales y firmas.',
        'ENDPOINT', 'REPORTE_BM1_ESP_PDF',
        'Inventario de Bienes Muebles', 'HORIZONTAL',
        5000, 300, 10, 'S', 'SEMILLA', SYSDATE
    );

    -- Mapeo campo -> parametro. Los nombres son los de ReporteBm1EspQuery.
    INSERT INTO MFO_REP_PARAM (REP_PARAM_ID, REPORTE_ID, NOMBRE_PARAM, ORIGEN, CAMPO_ID, VALOR_FIJO, CLAVE_SISTEMA, TIPO_DATO, FORMATO, OBLIGATORIO, VALOR_DEFECTO, ORDEN)
    VALUES (SEQ_MFO_REP_PARAM.NEXTVAL, v_reporte_id, 'CodigoDirBien', 'CAMPO', v_c_unidad, NULL, NULL, 'NUMERO', NULL, 'N', NULL, 10);

    INSERT INTO MFO_REP_PARAM (REP_PARAM_ID, REPORTE_ID, NOMBRE_PARAM, ORIGEN, CAMPO_ID, VALOR_FIJO, CLAVE_SISTEMA, TIPO_DATO, FORMATO, OBLIGATORIO, VALOR_DEFECTO, ORDEN)
    VALUES (SEQ_MFO_REP_PARAM.NEXTVAL, v_reporte_id, 'CodigoArticulo', 'CAMPO', v_c_articulo, NULL, NULL, 'NUMERO', NULL, 'N', NULL, 20);

    INSERT INTO MFO_REP_PARAM (REP_PARAM_ID, REPORTE_ID, NOMBRE_PARAM, ORIGEN, CAMPO_ID, VALOR_FIJO, CLAVE_SISTEMA, TIPO_DATO, FORMATO, OBLIGATORIO, VALOR_DEFECTO, ORDEN)
    VALUES (SEQ_MFO_REP_PARAM.NEXTVAL, v_reporte_id, 'PlacaDesde', 'CAMPO', v_c_placa_ini, NULL, NULL, 'TEXTO', NULL, 'N', NULL, 30);

    INSERT INTO MFO_REP_PARAM (REP_PARAM_ID, REPORTE_ID, NOMBRE_PARAM, ORIGEN, CAMPO_ID, VALOR_FIJO, CLAVE_SISTEMA, TIPO_DATO, FORMATO, OBLIGATORIO, VALOR_DEFECTO, ORDEN)
    VALUES (SEQ_MFO_REP_PARAM.NEXTVAL, v_reporte_id, 'PlacaHasta', 'CAMPO', v_c_placa_fin, NULL, NULL, 'TEXTO', NULL, 'N', NULL, 40);

    INSERT INTO MFO_REP_PARAM (REP_PARAM_ID, REPORTE_ID, NOMBRE_PARAM, ORIGEN, CAMPO_ID, VALOR_FIJO, CLAVE_SISTEMA, TIPO_DATO, FORMATO, OBLIGATORIO, VALOR_DEFECTO, ORDEN)
    VALUES (SEQ_MFO_REP_PARAM.NEXTVAL, v_reporte_id, 'FechaDesde', 'CAMPO', v_c_fecha_ini, NULL, NULL, 'FECHA', 'YYYY-MM-DD', 'N', NULL, 50);

    INSERT INTO MFO_REP_PARAM (REP_PARAM_ID, REPORTE_ID, NOMBRE_PARAM, ORIGEN, CAMPO_ID, VALOR_FIJO, CLAVE_SISTEMA, TIPO_DATO, FORMATO, OBLIGATORIO, VALOR_DEFECTO, ORDEN)
    VALUES (SEQ_MFO_REP_PARAM.NEXTVAL, v_reporte_id, 'FechaHasta', 'CAMPO', v_c_fecha_fin, NULL, NULL, 'FECHA', 'YYYY-MM-DD', 'N', NULL, 60);

    INSERT INTO MFO_REP_PARAM (REP_PARAM_ID, REPORTE_ID, NOMBRE_PARAM, ORIGEN, CAMPO_ID, VALOR_FIJO, CLAVE_SISTEMA, TIPO_DATO, FORMATO, OBLIGATORIO, VALOR_DEFECTO, ORDEN)
    VALUES (SEQ_MFO_REP_PARAM.NEXTVAL, v_reporte_id, 'Responsable', 'CAMPO', v_c_responsable, NULL, NULL, 'TEXTO', NULL, 'N', NULL, 70);

    INSERT INTO MFO_AUDITORIA (AUDITORIA_ID, ENTIDAD, ENTIDAD_ID, ACCION, USUARIO, FECHA, DETALLE_CLB)
    VALUES (SEQ_MFO_AUDITORIA.NEXTVAL, 'REPORTE', v_reporte_id, 'CREAR', 'SEMILLA', SYSDATE,
            'REP_BM1_ESP -> REPORTE_BM1_ESP_PDF sembrado por 18_MFO_SEMILLA_BM1ESP.sql');

    COMMIT;
    DBMS_OUTPUT.PUT_LINE('REP_BM1_ESP sembrado y publicado. REPORTE_ID = ' || v_reporte_id);
END;
/

PROMPT === Verificacion
SELECT F.ALIAS, F.MODO_USO, R.CLAVE, R.CLAVE_REGISTRO,
       (SELECT COUNT(*) FROM MFO_REP_PARAM P WHERE P.REPORTE_ID = R.REPORTE_ID) AS PARAMETROS
  FROM MFO_FORMULARIO F
  LEFT JOIN MFO_REPORTE R ON R.FORMULARIO_ID = F.FORMULARIO_ID
 WHERE F.ALIAS = 'REP_BM1_ESP';
