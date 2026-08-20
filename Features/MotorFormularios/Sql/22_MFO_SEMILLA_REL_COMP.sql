-- =============================================================================
-- Motor de Formularios (MFO) - Formulario de parametros de la Relacion de
-- Compromisos.
-- Requerimiento 25, integrado al requerimiento 16.
--
-- El PLAN.md del requerimiento 25 preveia una pantalla de filtros codificada a
-- mano en NextOssmasoft (su Fase 4) y dejaba abierta la pregunta de en que
-- modulo vivia el boton: no se identifico ninguno obvio de "compromisos" en
-- src/adm ni en src/presupuesto. Se define aqui como formulario del motor y la
-- pregunta desaparece.
--
-- **Este es el primer formulario de reporte que exige elegir un presupuesto.**
-- Los otros reportes del motor resuelven todo su contexto en el servidor
-- (CODIGO_EMPRESA desde settings:EmpresaConfig), pero para el presupuesto no
-- existe equivalente: no hay ningun settings:PresupuestoConfig ni concepto de
-- "presupuesto activo" en el backend, y el patron del ERP es que el usuario elija
-- uno de la lista. Por eso PRESUPUESTO es un campo obligatorio contra el catalogo
-- PRE_PRESUPUESTO, que reusa PRE.SP_PRE_PRESUP_LIST_GET -el mismo procedimiento
-- que alimenta la pantalla de presupuestos- y marca cual esta en ejecucion.
--
-- Parametros del reporte legado (ADM_RELACION_COMPROMISO.rdf) y que se hace con
-- cada uno:
--
--   CODIGO_PRESUPUESTO - campo obligatorio contra el catalogo PRE_PRESUPUESTO.
--       En el .rdf no tenia definicion de parametro con etiqueta, lo que sugiere
--       que Oracle Reports lo resolvia como variable de contexto en ejecucion.
--   P_FECHA_COMPROMISO_DESDE / P_FECHA_COMPROMISO_HASTA - **opcionales e
--       independientes**, como en el original: su trigger AfterPForm contempla
--       los cuatro casos (ambas, solo desde, solo hasta, ninguna) y arma el
--       subtitulo en consecuencia. Sin ninguna, el reporte lista el presupuesto
--       completo; de ahi el MAX_FILAS.
--   P_CODIGO_PROVEEDOR - numero opcional.
--   CODIGO_EMPRESA - parametro de origen SISTEMA, desde settings:EmpresaConfig.
--       En el legado solo servia para resolver el membrete; aqui filtra tambien
--       las tres ramas del query, que es una correccion respecto del original.
--   Usuario - parametro de origen SISTEMA. El reporte legado imprimia el login
--       en el pie de cada pagina (user$currentdate) y el pie de auditoria
--       compartido del requerimiento 17 hace lo mismo.
--
-- Lo que NO se incluye:
--
--   LP_CODIGO_PROVEEDOR / LP_FECHA_COMPROMISO / LP_FECHA_COMPROMISO_CONTRATO /
--       SUBTITULO - parametros lexicos internos del .rdf, no filtros del
--       usuario. Su logica vive ahora en los predicados del SP y en el generador
--       de PDF.
--   La columna de deuda (CF_DEUDAFORMULA) - deshabilitada en el .rdf, devuelve
--       NULL siempre. Ver el PLAN.md del requerimiento 25.
--
-- Requiere: INSTALL_MFO.sql, INSTALL_MFO_DEF.sql e INSTALL_MFO_REP.sql, el
-- catalogo PRE_PRESUPUESTO registrado en MfoCatalogoRegistro.cs, y que
-- ADM.SP_REP_COMPROMISO_GET este creado en el schema ADM.
-- Se ejecuta conectado como MFO.
-- =============================================================================

SET SERVEROUTPUT ON

DECLARE
    v_formulario_id  MFO_FORMULARIO.FORMULARIO_ID%TYPE;
    v_version_id     MFO_VERSION.VERSION_ID%TYPE;
    v_seccion_id     MFO_SECCION.SECCION_ID%TYPE;
    v_reporte_id     MFO_REPORTE.REPORTE_ID%TYPE;
    v_c_presupuesto  MFO_CAMPO.CAMPO_ID%TYPE;
    v_c_fecha_ini    MFO_CAMPO.CAMPO_ID%TYPE;
    v_c_fecha_fin    MFO_CAMPO.CAMPO_ID%TYPE;
    v_c_proveedor    MFO_CAMPO.CAMPO_ID%TYPE;
    v_t_fecha        MFO_TIPO_CAMPO.TIPO_CAMPO_ID%TYPE;
    v_t_catalogo     MFO_TIPO_CAMPO.TIPO_CAMPO_ID%TYPE;
    v_t_numero       MFO_TIPO_CAMPO.TIPO_CAMPO_ID%TYPE;
    v_existe         NUMBER;

    c_empresa CONSTANT NUMBER := 13;
BEGIN
    SELECT COUNT(*) INTO v_existe FROM MFO_FORMULARIO WHERE ALIAS = 'REP_REL_COMPROMISO';
    IF v_existe > 0 THEN
        DBMS_OUTPUT.PUT_LINE('REP_REL_COMPROMISO ya existe. No se siembra de nuevo.');
        RETURN;
    END IF;

    SELECT TIPO_CAMPO_ID INTO v_t_fecha    FROM MFO_TIPO_CAMPO WHERE CODIGO = 'FECHA';
    SELECT TIPO_CAMPO_ID INTO v_t_catalogo FROM MFO_TIPO_CAMPO WHERE CODIGO = 'CATALOGO';
    SELECT TIPO_CAMPO_ID INTO v_t_numero   FROM MFO_TIPO_CAMPO WHERE CODIGO = 'NUMERO';

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
        v_formulario_id, 'REP_REL_COMPROMISO',
        'Parametros - Relacion de Compromisos',
        'Presupuesto obligatorio, rango de fechas opcional y filtro opcional de proveedor. Sustituye el Parameter Form de ADM_RELACION_COMPROMISO.',
        'Reportes', c_empresa,
        'ACTIVO', NULL, NULL, NULL,
        'N', 'PARAMETROS', 'N', 'SEMILLA', SYSDATE
    );

    SELECT SEQ_MFO_VERSION.NEXTVAL INTO v_version_id FROM DUAL;
    INSERT INTO MFO_VERSION (
        VERSION_ID, FORMULARIO_ID, NUMERO, ESTADO, NOTAS, USUARIO_INS, FECHA_INS
    ) VALUES (
        v_version_id, v_formulario_id, 1, 'BORRADOR',
        'Version inicial sembrada por 22_MFO_SEMILLA_REL_COMP.sql (requerimiento 25).',
        'SEMILLA', SYSDATE
    );

    SELECT SEQ_MFO_SECCION.NEXTVAL INTO v_seccion_id FROM DUAL;
    INSERT INTO MFO_SECCION (
        SECCION_ID, VERSION_ID, CLAVE, TITULO, DESCRIPCION, ORDEN, COLUMNAS,
        ES_PASO, REPETIBLE, MIN_FILAS, MAX_FILAS, COLAPSABLE
    ) VALUES (
        v_seccion_id, v_version_id, 'FILTROS', 'Presupuesto y filtros',
        'El presupuesto es obligatorio. Sin rango de fechas el reporte lista todos los compromisos del presupuesto.',
        10, 2, 'N', 'N', NULL, NULL, 'N'
    );

    -- ------------------------------------------------------------------------
    -- Campos
    -- ------------------------------------------------------------------------
    SELECT SEQ_MFO_CAMPO.NEXTVAL INTO v_c_presupuesto FROM DUAL;
    INSERT INTO MFO_CAMPO (
        CAMPO_ID, VERSION_ID, SECCION_ID, TIPO_CAMPO_ID, CLAVE, ETIQUETA, AYUDA,
        PLACEHOLDER, ORDEN, ANCHO, REQUERIDO, SOLO_LECTURA, VALOR_DEFECTO,
        ORIGEN_OPCIONES, CATALOGO_CLAVE, MASCARA, UNIDAD, EXPRESION
    ) VALUES (
        v_c_presupuesto, v_version_id, v_seccion_id, v_t_catalogo, 'PRESUPUESTO',
        'Presupuesto', 'El que esta en ejecucion aparece marcado en la lista.', NULL, 10, 12,
        'S', 'N', NULL, 'CATALOGO', 'PRE_PRESUPUESTO', NULL, NULL, NULL
    );

    -- Opcionales a proposito: el reporte legado admite las dos, una sola o
    -- ninguna, y arma el subtitulo segun el caso.
    SELECT SEQ_MFO_CAMPO.NEXTVAL INTO v_c_fecha_ini FROM DUAL;
    INSERT INTO MFO_CAMPO (
        CAMPO_ID, VERSION_ID, SECCION_ID, TIPO_CAMPO_ID, CLAVE, ETIQUETA, AYUDA,
        PLACEHOLDER, ORDEN, ANCHO, REQUERIDO, SOLO_LECTURA, VALOR_DEFECTO,
        ORIGEN_OPCIONES, CATALOGO_CLAVE, MASCARA, UNIDAD, EXPRESION
    ) VALUES (
        v_c_fecha_ini, v_version_id, v_seccion_id, v_t_fecha, 'FECHA_DESDE',
        'Fecha de compromiso desde', 'Vacio = sin limite inferior.', NULL, 20, 6,
        'N', 'N', NULL, NULL, NULL, NULL, NULL, NULL
    );

    SELECT SEQ_MFO_CAMPO.NEXTVAL INTO v_c_fecha_fin FROM DUAL;
    INSERT INTO MFO_CAMPO (
        CAMPO_ID, VERSION_ID, SECCION_ID, TIPO_CAMPO_ID, CLAVE, ETIQUETA, AYUDA,
        PLACEHOLDER, ORDEN, ANCHO, REQUERIDO, SOLO_LECTURA, VALOR_DEFECTO,
        ORIGEN_OPCIONES, CATALOGO_CLAVE, MASCARA, UNIDAD, EXPRESION
    ) VALUES (
        v_c_fecha_fin, v_version_id, v_seccion_id, v_t_fecha, 'FECHA_HASTA',
        'Fecha de compromiso hasta', 'Vacio = sin limite superior.', NULL, 30, 6,
        'N', 'N', NULL, NULL, NULL, NULL, NULL, NULL
    );

    SELECT SEQ_MFO_CAMPO.NEXTVAL INTO v_c_proveedor FROM DUAL;
    INSERT INTO MFO_CAMPO (
        CAMPO_ID, VERSION_ID, SECCION_ID, TIPO_CAMPO_ID, CLAVE, ETIQUETA, AYUDA,
        PLACEHOLDER, ORDEN, ANCHO, REQUERIDO, SOLO_LECTURA, VALOR_DEFECTO,
        ORIGEN_OPCIONES, CATALOGO_CLAVE, MASCARA, UNIDAD, EXPRESION
    ) VALUES (
        v_c_proveedor, v_version_id, v_seccion_id, v_t_numero, 'PROVEEDOR',
        'Codigo de proveedor', 'Vacio = todos los proveedores.', NULL, 40, 6,
        'N', 'N', NULL, NULL, NULL, NULL, NULL, NULL
    );

    -- Coherencia del rango. El backend la valida igual, pero avisar en pantalla
    -- evita un viaje al servidor para decir lo obvio.
    INSERT INTO MFO_REGLA (REGLA_ID, CAMPO_ID, TIPO_REGLA, PARAM_1, PARAM_2, MENSAJE, ORDEN, ACTIVO)
    VALUES (SEQ_MFO_REGLA.NEXTVAL, v_c_fecha_ini, 'FEC_MAX', 'HOY', NULL,
            'La fecha desde no puede ser futura.', 10, 'S');

    INSERT INTO MFO_REGLA (REGLA_ID, CAMPO_ID, TIPO_REGLA, PARAM_1, PARAM_2, MENSAJE, ORDEN, ACTIVO)
    VALUES (SEQ_MFO_REGLA.NEXTVAL, v_c_fecha_fin, 'FEC_MAX', 'HOY', NULL,
            'La fecha hasta no puede ser futura.', 10, 'S');

    INSERT INTO MFO_REGLA (REGLA_ID, CAMPO_ID, TIPO_REGLA, PARAM_1, PARAM_2, MENSAJE, ORDEN, ACTIVO)
    VALUES (SEQ_MFO_REGLA.NEXTVAL, v_c_proveedor, 'MIN', '1', NULL,
            'El codigo de proveedor debe ser mayor que cero.', 10, 'S');

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
    -- MAX_FILAS = 5000 compromisos. Este reporte imprime una linea por
    -- compromiso, asi que caben muchos por pagina; el corte es para el caso de
    -- pedirlo sin rango de fechas sobre un presupuesto completo. **Aqui el corte
    -- si es por fila**, al contrario que en los reportes de cheques: no hay
    -- subtotales intermedios que descuadrar, solo el total de cierre, y de que
    -- corresponde a lo incluido avisa el mensaje de TRUNCADO.
    --
    -- TIMEOUT_SEG = 240: el query une tres fuentes con GROUP BY y un UNION que
    -- obliga a ordenar y deduplicar todo el resultado.
    -- ------------------------------------------------------------------------
    SELECT SEQ_MFO_REPORTE.NEXTVAL INTO v_reporte_id FROM DUAL;
    INSERT INTO MFO_REPORTE (
        REPORTE_ID, FORMULARIO_ID, CLAVE, NOMBRE, DESCRIPCION, TIPO_EJEC,
        CLAVE_REGISTRO, TITULO_REPORTE, ORIENTACION, MAX_FILAS, TIMEOUT_SEG,
        ORDEN, ACTIVO, USUARIO_INS, FECHA_INS
    ) VALUES (
        v_reporte_id, v_formulario_id, 'REL_COMPROMISO',
        'Relacion de Compromisos (PDF)',
        'Compromisos del presupuesto en el periodo, netos de anulaciones, uniendo compromisos administrativos, de presupuesto y contratos, con total de cierre.',
        'ENDPOINT', 'REPORTE_REL_COMPROMISO_PDF',
        'Relacion de Compromisos', 'HORIZONTAL',
        5000, 240, 10, 'S', 'SEMILLA', SYSDATE
    );

    -- Mapeo campo -> parametro. Los nombres son los de
    -- ReporteRelacionCompromisoQuery.
    INSERT INTO MFO_REP_PARAM (REP_PARAM_ID, REPORTE_ID, NOMBRE_PARAM, ORIGEN, CAMPO_ID, VALOR_FIJO, CLAVE_SISTEMA, TIPO_DATO, FORMATO, OBLIGATORIO, VALOR_DEFECTO, ORDEN)
    VALUES (SEQ_MFO_REP_PARAM.NEXTVAL, v_reporte_id, 'CodigoPresupuesto', 'CAMPO', v_c_presupuesto, NULL, NULL, 'NUMERO', NULL, 'S', NULL, 10);

    INSERT INTO MFO_REP_PARAM (REP_PARAM_ID, REPORTE_ID, NOMBRE_PARAM, ORIGEN, CAMPO_ID, VALOR_FIJO, CLAVE_SISTEMA, TIPO_DATO, FORMATO, OBLIGATORIO, VALOR_DEFECTO, ORDEN)
    VALUES (SEQ_MFO_REP_PARAM.NEXTVAL, v_reporte_id, 'FechaDesde', 'CAMPO', v_c_fecha_ini, NULL, NULL, 'FECHA', 'YYYY-MM-DD', 'N', NULL, 20);

    INSERT INTO MFO_REP_PARAM (REP_PARAM_ID, REPORTE_ID, NOMBRE_PARAM, ORIGEN, CAMPO_ID, VALOR_FIJO, CLAVE_SISTEMA, TIPO_DATO, FORMATO, OBLIGATORIO, VALOR_DEFECTO, ORDEN)
    VALUES (SEQ_MFO_REP_PARAM.NEXTVAL, v_reporte_id, 'FechaHasta', 'CAMPO', v_c_fecha_fin, NULL, NULL, 'FECHA', 'YYYY-MM-DD', 'N', NULL, 30);

    INSERT INTO MFO_REP_PARAM (REP_PARAM_ID, REPORTE_ID, NOMBRE_PARAM, ORIGEN, CAMPO_ID, VALOR_FIJO, CLAVE_SISTEMA, TIPO_DATO, FORMATO, OBLIGATORIO, VALOR_DEFECTO, ORDEN)
    VALUES (SEQ_MFO_REP_PARAM.NEXTVAL, v_reporte_id, 'CodigoProveedor', 'CAMPO', v_c_proveedor, NULL, NULL, 'NUMERO', NULL, 'N', NULL, 40);

    -- Origen SISTEMA: los resuelve el servidor y un valor enviado en el payload
    -- se descarta sin mirarlo. CodigoEmpresa no llega por argumento al handler
    -- -lo lee de settings:EmpresaConfig-, pero se mapea igual porque es lo que
    -- hace auditable en PARAMS_CLB con que empresa corrio.
    INSERT INTO MFO_REP_PARAM (REP_PARAM_ID, REPORTE_ID, NOMBRE_PARAM, ORIGEN, CAMPO_ID, VALOR_FIJO, CLAVE_SISTEMA, TIPO_DATO, FORMATO, OBLIGATORIO, VALOR_DEFECTO, ORDEN)
    VALUES (SEQ_MFO_REP_PARAM.NEXTVAL, v_reporte_id, 'CodigoEmpresa', 'SISTEMA', NULL, NULL, 'CODIGO_EMPRESA', 'NUMERO', NULL, 'S', NULL, 50);

    INSERT INTO MFO_REP_PARAM (REP_PARAM_ID, REPORTE_ID, NOMBRE_PARAM, ORIGEN, CAMPO_ID, VALOR_FIJO, CLAVE_SISTEMA, TIPO_DATO, FORMATO, OBLIGATORIO, VALOR_DEFECTO, ORDEN)
    VALUES (SEQ_MFO_REP_PARAM.NEXTVAL, v_reporte_id, 'Usuario', 'SISTEMA', NULL, NULL, 'USUARIO', 'TEXTO', NULL, 'S', NULL, 60);

    INSERT INTO MFO_AUDITORIA (AUDITORIA_ID, ENTIDAD, ENTIDAD_ID, ACCION, USUARIO, FECHA, DETALLE_CLB)
    VALUES (SEQ_MFO_AUDITORIA.NEXTVAL, 'REPORTE', v_reporte_id, 'CREAR', 'SEMILLA', SYSDATE,
            'REP_REL_COMPROMISO -> REPORTE_REL_COMPROMISO_PDF sembrado por 22_MFO_SEMILLA_REL_COMP.sql');

    COMMIT;
    DBMS_OUTPUT.PUT_LINE('REP_REL_COMPROMISO sembrado y publicado. REPORTE_ID = ' || v_reporte_id);
END;
/

PROMPT === Verificacion
SELECT F.ALIAS, F.MODO_USO, R.CLAVE, R.CLAVE_REGISTRO,
       (SELECT COUNT(*) FROM MFO_REP_PARAM P WHERE P.REPORTE_ID = R.REPORTE_ID) AS PARAMETROS
  FROM MFO_FORMULARIO F
  LEFT JOIN MFO_REPORTE R ON R.FORMULARIO_ID = F.FORMULARIO_ID
 WHERE F.ALIAS = 'REP_REL_COMPROMISO';

PROMPT === Deben salir 6 parametros y el campo PRESUPUESTO obligatorio con catalogo:
SELECT C.CLAVE, C.REQUERIDO, C.CATALOGO_CLAVE
  FROM MFO_CAMPO C
  JOIN MFO_FORMULARIO F ON F.VERSION_PUBL_ID = C.VERSION_ID
 WHERE F.ALIAS = 'REP_REL_COMPROMISO'
 ORDER BY C.ORDEN;
