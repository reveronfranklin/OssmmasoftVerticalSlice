-- =============================================================================
-- Motor de Formularios (MFO) - Segundo reporte del formulario REP_BM1
-- Requerimiento 16.
--
-- Enlaza el PDF de placas de bienes al MISMO formulario de parametros que ya
-- alimenta el reporte BM1. Es el caso para el que MFO_REPORTE admite N reportes
-- por formulario: los dos reportes se filtran por rango de fechas y unidades de
-- trabajo, asi que reusar el juego de parametros evita mantener dos formularios
-- que habria que cambiar a la vez.
--
-- El usuario elige cual generar en el selector de la pantalla, que aparece solo
-- cuando el formulario tiene mas de un reporte.
--
-- CLAVE_REGISTRO = 'REPORTE_BM1_PLACAS_PDF'. Como siempre: **este valor no se
-- ejecuta**. Es la clave que MfoRegistroReportes.cs busca en su lista blanca
-- para obtener el delegado. Si el backend no esta desplegado con ese registro,
-- la pantalla avisa de que el reporte no esta habilitado y ejecutarlo falla sin
-- invocar nada.
--
-- Requiere: 08_MFO_SEMILLA_DEMO.sql y 12_MFO_REP_SEMILLA.sql ya ejecutados.
-- Se ejecuta conectado como MFO.
-- =============================================================================

SET SERVEROUTPUT ON

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
     WHERE FORMULARIO_ID = v_formulario_id AND CLAVE = 'BM1_PLACAS';

    IF v_existe > 0 THEN
        DBMS_OUTPUT.PUT_LINE('El enlace BM1_PLACAS ya existe. No se siembra de nuevo.');
        RETURN;
    END IF;

    SELECT CAMPO_ID INTO v_campo_desde FROM MFO_CAMPO WHERE VERSION_ID = v_version_id AND CLAVE = 'FECHA_DESDE';
    SELECT CAMPO_ID INTO v_campo_hasta FROM MFO_CAMPO WHERE VERSION_ID = v_version_id AND CLAVE = 'FECHA_HASTA';
    SELECT CAMPO_ID INTO v_campo_icp   FROM MFO_CAMPO WHERE VERSION_ID = v_version_id AND CLAVE = 'CODIGOS_ICP';

    -- ------------------------------------------------------------------------
    -- Reporte. ORDEN 20 para que quede despues del BM1 en el selector.
    --
    -- MAX_FILAS = 2000: una placa es una etiqueta por bien, asi que un rango de
    -- fechas amplio puede producir un PDF de miles de paginas que nadie va a
    -- imprimir. El corte avisa al usuario en vez de tumbar el servidor.
    -- ------------------------------------------------------------------------
    SELECT SEQ_MFO_REPORTE.NEXTVAL INTO v_reporte_id FROM DUAL;

    INSERT INTO MFO_REPORTE (
        REPORTE_ID, FORMULARIO_ID, CLAVE, NOMBRE, DESCRIPCION, TIPO_EJEC,
        CLAVE_REGISTRO, TITULO_REPORTE, ORIENTACION, MAX_FILAS, TIMEOUT_SEG,
        ORDEN, ACTIVO, USUARIO_INS, FECHA_INS
    ) VALUES (
        v_reporte_id, v_formulario_id, 'BM1_PLACAS',
        'Placas de bienes (PDF)',
        'Etiquetas de placa de los bienes por rango de fechas y unidad de trabajo. Layout propio ya implementado en el backend.',
        'ENDPOINT', 'REPORTE_BM1_PLACAS_PDF',
        'Placas de Bienes Municipales', 'VERTICAL',
        2000, 180, 20, 'S', 'SEMILLA', SYSDATE
    );

    -- ------------------------------------------------------------------------
    -- Parametros. Mismo mapeo que BM1_PDF: los dos reportes leen los mismos
    -- campos del formulario, cada uno con el nombre que espera su filtro.
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

    INSERT INTO MFO_REP_PARAM (
        REP_PARAM_ID, REPORTE_ID, NOMBRE_PARAM, ORIGEN, CAMPO_ID, VALOR_FIJO,
        CLAVE_SISTEMA, TIPO_DATO, FORMATO, OBLIGATORIO, VALOR_DEFECTO, ORDEN
    ) VALUES (
        SEQ_MFO_REP_PARAM.NEXTVAL, v_reporte_id, 'CodigosIcp', 'CAMPO', v_campo_icp, NULL,
        NULL, 'NUMERO', NULL, 'N', NULL, 30
    );

    -- Origen SISTEMA: lo resuelve el servidor desde settings:EmpresaConfig e
    -- ignora cualquier valor que venga en el payload.
    INSERT INTO MFO_REP_PARAM (
        REP_PARAM_ID, REPORTE_ID, NOMBRE_PARAM, ORIGEN, CAMPO_ID, VALOR_FIJO,
        CLAVE_SISTEMA, TIPO_DATO, FORMATO, OBLIGATORIO, VALOR_DEFECTO, ORDEN
    ) VALUES (
        SEQ_MFO_REP_PARAM.NEXTVAL, v_reporte_id, 'CodigoEmpresa', 'SISTEMA', NULL, NULL,
        'CODIGO_EMPRESA', 'NUMERO', NULL, 'S', NULL, 40
    );

    INSERT INTO MFO_AUDITORIA (AUDITORIA_ID, ENTIDAD, ENTIDAD_ID, ACCION, USUARIO, FECHA, DETALLE_CLB)
    VALUES (SEQ_MFO_AUDITORIA.NEXTVAL, 'REPORTE', v_reporte_id, 'CREAR', 'SEMILLA', SYSDATE,
            'Enlace REP_BM1 -> REPORTE_BM1_PLACAS_PDF sembrado por 16_MFO_REP_PLACAS.sql');

    COMMIT;
    DBMS_OUTPUT.PUT_LINE('Enlace BM1_PLACAS sembrado. REPORTE_ID = ' || v_reporte_id);
END;
/

PROMPT === Verificacion: REP_BM1 debe tener dos reportes
SELECT R.CLAVE, R.NOMBRE, R.CLAVE_REGISTRO, R.ORDEN, R.ACTIVO,
       (SELECT COUNT(*) FROM MFO_REP_PARAM P WHERE P.REPORTE_ID = R.REPORTE_ID) AS PARAMETROS
  FROM MFO_REPORTE R
  JOIN MFO_FORMULARIO F ON F.FORMULARIO_ID = R.FORMULARIO_ID
 WHERE F.ALIAS = 'REP_BM1'
 ORDER BY R.ORDEN;
