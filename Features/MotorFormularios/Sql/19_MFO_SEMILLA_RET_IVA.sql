-- =============================================================================
-- Motor de Formularios (MFO) - Formulario de parametros de la Relacion de
-- Retenciones de IVA por periodos.
-- Requerimiento 22, integrado al requerimiento 16.
--
-- El reporte legado ADM_RELACION_RETENCION_IVA_OP2.rdf se pedia desde el
-- "Runtime Parameter Form" de Oracle Reports. Su sustituto no es una pantalla
-- codificada a mano en el frontend: son estas filas. Es exactamente el caso de
-- uso del modo PARAMETROS, y significa que el rango de fechas o el filtro de
-- estatus se pueden ajustar despues sin desplegar nada.
--
-- Parametros del reporte legado y que se hace con cada uno:
--
--   P_FECHA_DESDE / P_FECHA_HASTA - se migran como los dos campos obligatorios
--       del formulario. En el .rdf traian 01/01/2017 y 31/01/2017 como valor por
--       defecto; no se replican porque un valor de 2017 en un reporte que se
--       corre por mes vigente es una trampa: el usuario acepta y obtiene un
--       reporte vacio que parece decir que no hubo retenciones.
--   P_ESTATUS - se migra como lista opcional AP/PE/AN. Vacio = todos, igual que
--       el nvl(:P_ESTATUS, aop.status) del query legado.
--   CODIGO_EMPRESA - parametro de origen SISTEMA. El cliente no lo puede
--       enviar: se resuelve desde settings:EmpresaConfig. Aparece en PARAMS_CLB
--       para que quede auditable con que empresa corrio cada ejecucion.
--   Usuario - parametro de origen SISTEMA. Alimenta el pie de auditoria del
--       requerimiento 17, que este reporte imprime como todos los de
--       retenciones.
--
-- Lo que NO se incluye, y por que:
--
--   P_CODIGO_PROVEEDOR / P_CODIGO_RETENCION - el .rdf los declara y su
--       AfterPForm arma con ellos los parametros lexicos LP_CODIGO_PROVEEDOR y
--       LP_CODIGO_RETENCION, pero **el query principal no referencia ninguno de
--       los dos**: son codigo muerto heredado de otro reporte. Exponerlos como
--       campos daria dos controles que no filtran nada. Agregar el de proveedor
--       si se pide es un campo mas aqui, un parametro mas en MFO_REP_PARAM y un
--       predicado mas en SP_REP_RET_IVA_PER_GET.
--   CODIGO_PRESUPUESTO - el predicado que lo usaba esta comentado en el .rdf.
--
-- Requiere: INSTALL_MFO.sql, INSTALL_MFO_DEF.sql e INSTALL_MFO_REP.sql, y que
-- ADM.SP_REP_RET_IVA_PER_GET este creado en el schema ADM.
-- Se ejecuta conectado como MFO.
-- =============================================================================

SET SERVEROUTPUT ON

DECLARE
    v_formulario_id  MFO_FORMULARIO.FORMULARIO_ID%TYPE;
    v_version_id     MFO_VERSION.VERSION_ID%TYPE;
    v_seccion_id     MFO_SECCION.SECCION_ID%TYPE;
    v_reporte_id     MFO_REPORTE.REPORTE_ID%TYPE;
    v_c_fecha_ini    MFO_CAMPO.CAMPO_ID%TYPE;
    v_c_fecha_fin    MFO_CAMPO.CAMPO_ID%TYPE;
    v_c_estatus      MFO_CAMPO.CAMPO_ID%TYPE;
    v_t_fecha        MFO_TIPO_CAMPO.TIPO_CAMPO_ID%TYPE;
    v_t_select       MFO_TIPO_CAMPO.TIPO_CAMPO_ID%TYPE;
    v_existe         NUMBER;

    c_empresa CONSTANT NUMBER := 13;
BEGIN
    SELECT COUNT(*) INTO v_existe FROM MFO_FORMULARIO WHERE ALIAS = 'REP_RET_IVA_PER';
    IF v_existe > 0 THEN
        DBMS_OUTPUT.PUT_LINE('REP_RET_IVA_PER ya existe. No se siembra de nuevo.');
        RETURN;
    END IF;

    SELECT TIPO_CAMPO_ID INTO v_t_fecha  FROM MFO_TIPO_CAMPO WHERE CODIGO = 'FECHA';
    SELECT TIPO_CAMPO_ID INTO v_t_select FROM MFO_TIPO_CAMPO WHERE CODIGO = 'SELECT';

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
        v_formulario_id, 'REP_RET_IVA_PER',
        'Parametros - Relacion de Retenciones de IVA por periodos',
        'Periodo obligatorio y filtro opcional de estatus de la orden de pago. Sustituye el Parameter Form de ADM_RELACION_RETENCION_IVA_OP2.',
        'Reportes', c_empresa,
        'ACTIVO', NULL, NULL, NULL,
        'N', 'PARAMETROS', 'N', 'SEMILLA', SYSDATE
    );

    SELECT SEQ_MFO_VERSION.NEXTVAL INTO v_version_id FROM DUAL;
    INSERT INTO MFO_VERSION (
        VERSION_ID, FORMULARIO_ID, NUMERO, ESTADO, NOTAS, USUARIO_INS, FECHA_INS
    ) VALUES (
        v_version_id, v_formulario_id, 1, 'BORRADOR',
        'Version inicial sembrada por 19_MFO_SEMILLA_RET_IVA.sql (requerimiento 22).',
        'SEMILLA', SYSDATE
    );

    SELECT SEQ_MFO_SECCION.NEXTVAL INTO v_seccion_id FROM DUAL;
    INSERT INTO MFO_SECCION (
        SECCION_ID, VERSION_ID, CLAVE, TITULO, DESCRIPCION, ORDEN, COLUMNAS,
        ES_PASO, REPETIBLE, MIN_FILAS, MAX_FILAS, COLAPSABLE
    ) VALUES (
        v_seccion_id, v_version_id, 'FILTROS', 'Periodo a relacionar',
        'El periodo se toma sobre la fecha de registro de la orden de pago.', 10, 2,
        'N', 'N', NULL, NULL, 'N'
    );

    -- ------------------------------------------------------------------------
    -- Campos. La CLAVE es lo que ve el disenador; el nombre que espera el
    -- reporte vive en MFO_REP_PARAM, que no asume que coincidan.
    -- ------------------------------------------------------------------------
    SELECT SEQ_MFO_CAMPO.NEXTVAL INTO v_c_fecha_ini FROM DUAL;
    INSERT INTO MFO_CAMPO (
        CAMPO_ID, VERSION_ID, SECCION_ID, TIPO_CAMPO_ID, CLAVE, ETIQUETA, AYUDA,
        PLACEHOLDER, ORDEN, ANCHO, REQUERIDO, SOLO_LECTURA, VALOR_DEFECTO,
        ORIGEN_OPCIONES, CATALOGO_CLAVE, MASCARA, UNIDAD, EXPRESION
    ) VALUES (
        v_c_fecha_ini, v_version_id, v_seccion_id, v_t_fecha, 'FECHA_DESDE',
        'Desde', 'Primer dia del periodo, inclusive.', NULL, 10, 6,
        'S', 'N', NULL, NULL, NULL, NULL, NULL, NULL
    );

    SELECT SEQ_MFO_CAMPO.NEXTVAL INTO v_c_fecha_fin FROM DUAL;
    INSERT INTO MFO_CAMPO (
        CAMPO_ID, VERSION_ID, SECCION_ID, TIPO_CAMPO_ID, CLAVE, ETIQUETA, AYUDA,
        PLACEHOLDER, ORDEN, ANCHO, REQUERIDO, SOLO_LECTURA, VALOR_DEFECTO,
        ORIGEN_OPCIONES, CATALOGO_CLAVE, MASCARA, UNIDAD, EXPRESION
    ) VALUES (
        v_c_fecha_fin, v_version_id, v_seccion_id, v_t_fecha, 'FECHA_HASTA',
        'Hasta', 'Ultimo dia del periodo, inclusive.', NULL, 20, 6,
        'S', 'N', NULL, NULL, NULL, NULL, NULL, NULL
    );

    -- Opcional a proposito: el reporte legado con P_ESTATUS nulo lista los tres
    -- estatus, y ese es el uso normal -la relacion del periodo completo-.
    SELECT SEQ_MFO_CAMPO.NEXTVAL INTO v_c_estatus FROM DUAL;
    INSERT INTO MFO_CAMPO (
        CAMPO_ID, VERSION_ID, SECCION_ID, TIPO_CAMPO_ID, CLAVE, ETIQUETA, AYUDA,
        PLACEHOLDER, ORDEN, ANCHO, REQUERIDO, SOLO_LECTURA, VALOR_DEFECTO,
        ORIGEN_OPCIONES, CATALOGO_CLAVE, MASCARA, UNIDAD, EXPRESION
    ) VALUES (
        v_c_estatus, v_version_id, v_seccion_id, v_t_select, 'ESTATUS',
        'Estatus de la orden de pago', 'Vacio = todos los estatus.', 'Todos', 30, 6,
        'N', 'N', NULL, 'ESTATICA', NULL, NULL, NULL, NULL
    );

    -- Los valores son los codigos reales de ADM_ORDEN_PAGO.STATUS: el reporte
    -- los compara tal cual y el handler rechaza cualquier otro.
    INSERT INTO MFO_OPCION (OPCION_ID, CAMPO_ID, VALOR, ETIQUETA, ORDEN, GRUPO, ES_DEFECTO, ACTIVO)
    VALUES (SEQ_MFO_OPCION.NEXTVAL, v_c_estatus, 'AP', 'Aprobado', 10, NULL, 'N', 'S');

    INSERT INTO MFO_OPCION (OPCION_ID, CAMPO_ID, VALOR, ETIQUETA, ORDEN, GRUPO, ES_DEFECTO, ACTIVO)
    VALUES (SEQ_MFO_OPCION.NEXTVAL, v_c_estatus, 'PE', 'Pendiente', 20, NULL, 'N', 'S');

    INSERT INTO MFO_OPCION (OPCION_ID, CAMPO_ID, VALOR, ETIQUETA, ORDEN, GRUPO, ES_DEFECTO, ACTIVO)
    VALUES (SEQ_MFO_OPCION.NEXTVAL, v_c_estatus, 'AN', 'Anulado', 30, NULL, 'N', 'S');

    -- Coherencia del rango. El backend la valida igual, pero avisar en pantalla
    -- evita un viaje al servidor para decir lo obvio.
    INSERT INTO MFO_REGLA (REGLA_ID, CAMPO_ID, TIPO_REGLA, PARAM_1, PARAM_2, MENSAJE, ORDEN, ACTIVO)
    VALUES (SEQ_MFO_REGLA.NEXTVAL, v_c_fecha_ini, 'FEC_MAX', 'HOY', NULL,
            'La fecha desde no puede ser futura.', 10, 'S');

    INSERT INTO MFO_REGLA (REGLA_ID, CAMPO_ID, TIPO_REGLA, PARAM_1, PARAM_2, MENSAJE, ORDEN, ACTIVO)
    VALUES (SEQ_MFO_REGLA.NEXTVAL, v_c_fecha_fin, 'FEC_MAX', 'HOY', NULL,
            'La fecha hasta no puede ser futura.', 10, 'S');

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
    -- Enlace al reporte.
    --
    -- MAX_FILAS = 4000 documentos. Un mes tipico son decenas; 4000 es el corte
    -- para el caso de pedir un ano completo por error. El ejecutor corta por
    -- comprobante completo, nunca a mitad de uno, para que el TOTAL impreso
    -- cuadre siempre con las filas impresas.
    --
    -- TIMEOUT_SEG = 180. Recordar lo que dice Guia-Enlazar-Reporte.md: corta la
    -- espera de la peticion, NO la consulta en Oracle.
    -- ------------------------------------------------------------------------
    SELECT SEQ_MFO_REPORTE.NEXTVAL INTO v_reporte_id FROM DUAL;
    INSERT INTO MFO_REPORTE (
        REPORTE_ID, FORMULARIO_ID, CLAVE, NOMBRE, DESCRIPCION, TIPO_EJEC,
        CLAVE_REGISTRO, TITULO_REPORTE, ORIENTACION, MAX_FILAS, TIMEOUT_SEG,
        ORDEN, ACTIVO, USUARIO_INS, FECHA_INS
    ) VALUES (
        v_reporte_id, v_formulario_id, 'RET_IVA_PER',
        'Relacion de Retenciones de IVA por periodos (PDF)',
        'Comprobantes de retencion de IVA emitidos en el periodo, con sus documentos y el total retenido.',
        'ENDPOINT', 'REPORTE_RET_IVA_PER_PDF',
        'Relacion de Retenciones por IVA', 'HORIZONTAL',
        4000, 180, 10, 'S', 'SEMILLA', SYSDATE
    );

    -- Mapeo campo -> parametro. Los nombres son los de ReporteRelacionRetIvaQuery.
    INSERT INTO MFO_REP_PARAM (REP_PARAM_ID, REPORTE_ID, NOMBRE_PARAM, ORIGEN, CAMPO_ID, VALOR_FIJO, CLAVE_SISTEMA, TIPO_DATO, FORMATO, OBLIGATORIO, VALOR_DEFECTO, ORDEN)
    VALUES (SEQ_MFO_REP_PARAM.NEXTVAL, v_reporte_id, 'FechaDesde', 'CAMPO', v_c_fecha_ini, NULL, NULL, 'FECHA', 'YYYY-MM-DD', 'S', NULL, 10);

    INSERT INTO MFO_REP_PARAM (REP_PARAM_ID, REPORTE_ID, NOMBRE_PARAM, ORIGEN, CAMPO_ID, VALOR_FIJO, CLAVE_SISTEMA, TIPO_DATO, FORMATO, OBLIGATORIO, VALOR_DEFECTO, ORDEN)
    VALUES (SEQ_MFO_REP_PARAM.NEXTVAL, v_reporte_id, 'FechaHasta', 'CAMPO', v_c_fecha_fin, NULL, NULL, 'FECHA', 'YYYY-MM-DD', 'S', NULL, 20);

    INSERT INTO MFO_REP_PARAM (REP_PARAM_ID, REPORTE_ID, NOMBRE_PARAM, ORIGEN, CAMPO_ID, VALOR_FIJO, CLAVE_SISTEMA, TIPO_DATO, FORMATO, OBLIGATORIO, VALOR_DEFECTO, ORDEN)
    VALUES (SEQ_MFO_REP_PARAM.NEXTVAL, v_reporte_id, 'Estatus', 'CAMPO', v_c_estatus, NULL, NULL, 'TEXTO', NULL, 'N', NULL, 30);

    -- Origen SISTEMA: lo resuelve el servidor y un valor enviado en el payload
    -- se descarta sin mirarlo. CodigoEmpresa no llega por argumento al handler
    -- -lo lee de settings:EmpresaConfig-, pero se mapea igual porque es lo que
    -- hace auditable en PARAMS_CLB con que empresa corrio.
    INSERT INTO MFO_REP_PARAM (REP_PARAM_ID, REPORTE_ID, NOMBRE_PARAM, ORIGEN, CAMPO_ID, VALOR_FIJO, CLAVE_SISTEMA, TIPO_DATO, FORMATO, OBLIGATORIO, VALOR_DEFECTO, ORDEN)
    VALUES (SEQ_MFO_REP_PARAM.NEXTVAL, v_reporte_id, 'CodigoEmpresa', 'SISTEMA', NULL, NULL, 'CODIGO_EMPRESA', 'NUMERO', NULL, 'S', NULL, 40);

    -- Usuario si se usa: es el pie de auditoria del requerimiento 17.
    INSERT INTO MFO_REP_PARAM (REP_PARAM_ID, REPORTE_ID, NOMBRE_PARAM, ORIGEN, CAMPO_ID, VALOR_FIJO, CLAVE_SISTEMA, TIPO_DATO, FORMATO, OBLIGATORIO, VALOR_DEFECTO, ORDEN)
    VALUES (SEQ_MFO_REP_PARAM.NEXTVAL, v_reporte_id, 'Usuario', 'SISTEMA', NULL, NULL, 'USUARIO', 'TEXTO', NULL, 'S', NULL, 50);

    INSERT INTO MFO_AUDITORIA (AUDITORIA_ID, ENTIDAD, ENTIDAD_ID, ACCION, USUARIO, FECHA, DETALLE_CLB)
    VALUES (SEQ_MFO_AUDITORIA.NEXTVAL, 'REPORTE', v_reporte_id, 'CREAR', 'SEMILLA', SYSDATE,
            'REP_RET_IVA_PER -> REPORTE_RET_IVA_PER_PDF sembrado por 19_MFO_SEMILLA_RET_IVA.sql');

    COMMIT;
    DBMS_OUTPUT.PUT_LINE('REP_RET_IVA_PER sembrado y publicado. REPORTE_ID = ' || v_reporte_id);
END;
/

PROMPT === Verificacion
SELECT F.ALIAS, F.MODO_USO, R.CLAVE, R.CLAVE_REGISTRO,
       (SELECT COUNT(*) FROM MFO_REP_PARAM P WHERE P.REPORTE_ID = R.REPORTE_ID) AS PARAMETROS
  FROM MFO_FORMULARIO F
  LEFT JOIN MFO_REPORTE R ON R.FORMULARIO_ID = F.FORMULARIO_ID
 WHERE F.ALIAS = 'REP_RET_IVA_PER';

PROMPT === Deben salir 5 parametros: FechaDesde, FechaHasta, Estatus, CodigoEmpresa, Usuario.
