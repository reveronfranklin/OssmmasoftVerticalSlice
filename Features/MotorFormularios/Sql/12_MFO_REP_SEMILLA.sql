-- =============================================================================
-- Motor de Formularios (MFO) - Fase 1 - Enlace del reporte de referencia
-- Requerimiento 16. Decision 8 de la Fase 0: el primer caso va por ENDPOINT,
-- contra un reporte ya implementado, para validar el mapeo de parametros de
-- punta a punta antes de construir el generador tabular generico.
--
-- Reporte de referencia: POST api/ReporteBm1/pdf, que recibe
-- ReporteBm1GetAllQuery (FechaDesde, FechaHasta, CodigosIcp) y ya devuelve el
-- PDF armado.
--
-- CLAVE_REGISTRO = 'REPORTE_BM1_PDF'. Este valor NO se ejecuta: es la clave que
-- MfoRegistroReportes.cs busca en su diccionario para obtener el delegado
-- concreto. Si la clave no esta registrada en codigo, la ejecucion falla con
-- IsValid=false y no invoca nada. Cambiar esta fila en base de datos a un valor
-- no registrado no habilita nada: esa es la prueba de seguridad obligatoria del
-- criterio de aceptacion de la Fase 9.
--
-- Este script desbloquea la Fase 9 sin necesitar el diseñador, igual que 08
-- desbloquea la Fase 6.
-- =============================================================================

DECLARE
    v_formulario_id  MFO_FORMULARIO.FORMULARIO_ID%TYPE;
    v_version_id     MFO_VERSION.VERSION_ID%TYPE;
    v_reporte_id     MFO_REPORTE.REPORTE_ID%TYPE;
    v_campo_desde    MFO_CAMPO.CAMPO_ID%TYPE;
    v_campo_hasta    MFO_CAMPO.CAMPO_ID%TYPE;
    v_campo_icp      MFO_CAMPO.CAMPO_ID%TYPE;
    v_existe         NUMBER;
BEGIN
    SELECT FORMULARIO_ID, VERSION_PUBL_ID
      INTO v_formulario_id, v_version_id
      FROM MFO_FORMULARIO
     WHERE ALIAS = 'REP_BM1';

    SELECT COUNT(*) INTO v_existe
      FROM MFO_REPORTE
     WHERE FORMULARIO_ID = v_formulario_id AND CLAVE = 'BM1_PDF';

    IF v_existe > 0 THEN
        DBMS_OUTPUT.PUT_LINE('El enlace BM1_PDF ya existe. No se siembra de nuevo.');
        RETURN;
    END IF;

    SELECT CAMPO_ID INTO v_campo_desde FROM MFO_CAMPO WHERE VERSION_ID = v_version_id AND CLAVE = 'FECHA_DESDE';
    SELECT CAMPO_ID INTO v_campo_hasta FROM MFO_CAMPO WHERE VERSION_ID = v_version_id AND CLAVE = 'FECHA_HASTA';
    SELECT CAMPO_ID INTO v_campo_icp   FROM MFO_CAMPO WHERE VERSION_ID = v_version_id AND CLAVE = 'CODIGOS_ICP';

    -- ------------------------------------------------------------------------
    -- El formulario pasa a modo PARAMETROS.
    -- REGISTRA_EJEC='N': una consulta de reporte no es una respuesta de negocio
    -- que valga la pena conservar. La trazabilidad la da MFO_REP_EJEC, que se
    -- escribe siempre e incluye PARAMS_CLB.
    -- ------------------------------------------------------------------------
    UPDATE MFO_FORMULARIO
       SET MODO_USO      = 'PARAMETROS',
           REGISTRA_EJEC = 'N'
     WHERE FORMULARIO_ID = v_formulario_id;

    -- ------------------------------------------------------------------------
    -- Reporte
    -- ------------------------------------------------------------------------
    SELECT SEQ_MFO_REPORTE.NEXTVAL INTO v_reporte_id FROM DUAL;
    INSERT INTO MFO_REPORTE (
        REPORTE_ID, FORMULARIO_ID, CLAVE, NOMBRE, DESCRIPCION, TIPO_EJEC,
        CLAVE_REGISTRO, TITULO_REPORTE, ORIENTACION, MAX_FILAS, TIMEOUT_SEG,
        ORDEN, ACTIVO, USUARIO_INS, FECHA_INS
    ) VALUES (
        v_reporte_id, v_formulario_id, 'BM1_PDF',
        'Reporte de Bienes Municipales (PDF)',
        'Listado de bienes por rango de fechas y unidad de trabajo. Layout propio ya implementado en el backend.',
        'ENDPOINT', 'REPORTE_BM1_PDF',
        'Reporte de Bienes Municipales', 'HORIZONTAL',
        NULL, 120, 10, 'S', 'SEMILLA', SYSDATE
    );

    -- ------------------------------------------------------------------------
    -- Parametros. NOMBRE_PARAM usa el nombre que espera ReporteBm1GetAllQuery.
    -- El mapeo es explicito aunque las claves de campo se parezcan: el modelo no
    -- asume que MFO_CAMPO.CLAVE coincida con el nombre del parametro, y esa
    -- indireccion es la que permite reusar un formulario con otro reporte.
    -- ------------------------------------------------------------------------
    INSERT INTO MFO_REP_PARAM (
        REP_PARAM_ID, REPORTE_ID, NOMBRE_PARAM, ORIGEN, CAMPO_ID, VALOR_FIJO,
        CLAVE_SISTEMA, TIPO_DATO, FORMATO, OBLIGATORIO, VALOR_DEFECTO, ORDEN
    ) VALUES (
        SEQ_MFO_REP_PARAM.NEXTVAL, v_reporte_id, 'FechaDesde', 'CAMPO', v_campo_desde, NULL,
        NULL, 'FECHA', 'YYYY-MM-DD', 'S', NULL, 10
    );

    INSERT INTO MFO_REP_PARAM (
        REP_PARAM_ID, REPORTE_ID, NOMBRE_PARAM, ORIGEN, CAMPO_ID, VALOR_FIJO,
        CLAVE_SISTEMA, TIPO_DATO, FORMATO, OBLIGATORIO, VALOR_DEFECTO, ORDEN
    ) VALUES (
        SEQ_MFO_REP_PARAM.NEXTVAL, v_reporte_id, 'FechaHasta', 'CAMPO', v_campo_hasta, NULL,
        NULL, 'FECHA', 'YYYY-MM-DD', 'S', NULL, 20
    );

    -- Multivalor: el campo es MULTI_SELECT, asi que este parametro recibe N
    -- valores. Es a proposito el caso mas exigente del mapeo y por eso entra en
    -- el primer reporte de referencia.
    INSERT INTO MFO_REP_PARAM (
        REP_PARAM_ID, REPORTE_ID, NOMBRE_PARAM, ORIGEN, CAMPO_ID, VALOR_FIJO,
        CLAVE_SISTEMA, TIPO_DATO, FORMATO, OBLIGATORIO, VALOR_DEFECTO, ORDEN
    ) VALUES (
        SEQ_MFO_REP_PARAM.NEXTVAL, v_reporte_id, 'CodigosIcp', 'CAMPO', v_campo_icp, NULL,
        NULL, 'NUMERO', NULL, 'N', NULL, 30
    );

    -- Origen SISTEMA: el backend lo resuelve desde settings:EmpresaConfig e
    -- ignora cualquier valor que venga en el payload. Es la segunda prueba de
    -- seguridad obligatoria de la Fase 9.
    INSERT INTO MFO_REP_PARAM (
        REP_PARAM_ID, REPORTE_ID, NOMBRE_PARAM, ORIGEN, CAMPO_ID, VALOR_FIJO,
        CLAVE_SISTEMA, TIPO_DATO, FORMATO, OBLIGATORIO, VALOR_DEFECTO, ORDEN
    ) VALUES (
        SEQ_MFO_REP_PARAM.NEXTVAL, v_reporte_id, 'CodigoEmpresa', 'SISTEMA', NULL, NULL,
        'CODIGO_EMPRESA', 'NUMERO', NULL, 'S', NULL, 40
    );

    -- MFO_REP_COLUMNA queda vacia: solo aplica a TIPO_EJEC='SP_CURSOR', y este
    -- reporte trae su propio layout.

    INSERT INTO MFO_AUDITORIA (AUDITORIA_ID, ENTIDAD, ENTIDAD_ID, ACCION, USUARIO, FECHA, DETALLE_CLB)
    VALUES (SEQ_MFO_AUDITORIA.NEXTVAL, 'REPORTE', v_reporte_id, 'CREAR', 'SEMILLA', SYSDATE,
            'Enlace REP_BM1 -> REPORTE_BM1_PDF sembrado por 12_MFO_REP_SEMILLA.sql');

    COMMIT;
    DBMS_OUTPUT.PUT_LINE('Enlace BM1_PDF sembrado. REPORTE_ID = ' || v_reporte_id);
END;
/
