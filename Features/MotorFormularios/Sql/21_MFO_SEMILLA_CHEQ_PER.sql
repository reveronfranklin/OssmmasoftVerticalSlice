-- =============================================================================
-- Motor de Formularios (MFO) - Formulario de parametros de la Relacion de
-- Cheques Emitidos Por Periodos (listado simple, sin motivo).
-- Requerimiento 24, integrado al requerimiento 16.
--
-- Es el hermano menor de 20_MFO_SEMILLA_CHEQ_MOT.sql (requerimiento 23). Los dos
-- reportes comparten backend -mismo SP ADM.SP_REP_CHEQ_PERIODO_GET, mismo
-- handler, mismo generador- y se diferencian en la bandera p_IncluirMotivo, que
-- aqui va en 'N'.
--
-- **Por que dos formularios y no uno con un interruptor.** MFO_REPORTE admite N
-- reportes por formulario, y esa es la solucion correcta cuando comparten el
-- juego de parametros -es el caso de REP_BM1, que alimenta el listado BM1 y las
-- placas-. Aqui NO lo comparten: el .rdf del requerimiento 24 no define
-- P_STATUS ni P_CODIGO_PROVEEDOR. Colgar los dos del mismo formulario obligaria a
-- mostrar al usuario del listado simple dos filtros que su reporte legado nunca
-- tuvo. Dos formularios cuesta esta segunda semilla; uno solo costaria confundir
-- a quien lo use.
--
-- Parametros del reporte legado (ADM_PERIODOS_CHEQUES1.RDF) y que se hace con
-- cada uno:
--
--   P_FECHA_INI / P_FECHA_FIN - los dos campos obligatorios. A nivel de SQL el
--       legado los hacia opcionales con NVL(:P_FECHA_INI, A.FECHA_CHEQUE), pero
--       el titulo del reporte imprime siempre un periodo y sin rango recorreria
--       el historico completo de cheques.
--   P_NOMBRE_BANCO - catalogo SIS_BANCO_NOMBRE en vez de texto libre. El reporte
--       filtra por igualdad exacta contra SIS_BANCOS.NOMBRE, asi que escrito a
--       mano un espacio de mas devuelve un reporte vacio que se lee como "no
--       hubo cheques". Como catalogo no se puede escribir mal.
--   P_NUMERO_CUENTA - catalogo SIS_CUENTA_BANCO, por la misma razon.
--   CODIGO_EMPRESA - parametro de origen SISTEMA, desde settings:EmpresaConfig.
--   Usuario - parametro de origen SISTEMA, para el pie de auditoria del
--       requerimiento 17.
--
-- Ese es el juego completo: este .rdf no tiene mas parametros. En particular no
-- tiene P_STATUS, P_CODIGO_PROVEEDOR ni el lexico P_WHERE_STATUS del
-- requerimiento 23. Los dos filtros que el SP compartido si acepta se quedan
-- nulos, que es "todos".
--
-- Requiere: INSTALL_MFO.sql, INSTALL_MFO_DEF.sql e INSTALL_MFO_REP.sql, los dos
-- catalogos registrados en MfoCatalogoRegistro.cs, y que
-- ADM.SP_REP_CHEQ_PERIODO_GET este creado en el schema ADM.
-- Se ejecuta conectado como MFO. Es independiente de 20_MFO_SEMILLA_CHEQ_MOT.sql:
-- se puede instalar uno, el otro o los dos.
-- =============================================================================

SET SERVEROUTPUT ON

DECLARE
    v_formulario_id  MFO_FORMULARIO.FORMULARIO_ID%TYPE;
    v_version_id     MFO_VERSION.VERSION_ID%TYPE;
    v_seccion_id     MFO_SECCION.SECCION_ID%TYPE;
    v_reporte_id     MFO_REPORTE.REPORTE_ID%TYPE;
    v_c_fecha_ini    MFO_CAMPO.CAMPO_ID%TYPE;
    v_c_fecha_fin    MFO_CAMPO.CAMPO_ID%TYPE;
    v_c_banco        MFO_CAMPO.CAMPO_ID%TYPE;
    v_c_cuenta       MFO_CAMPO.CAMPO_ID%TYPE;
    v_t_fecha        MFO_TIPO_CAMPO.TIPO_CAMPO_ID%TYPE;
    v_t_catalogo     MFO_TIPO_CAMPO.TIPO_CAMPO_ID%TYPE;
    v_existe         NUMBER;

    c_empresa CONSTANT NUMBER := 13;
BEGIN
    SELECT COUNT(*) INTO v_existe FROM MFO_FORMULARIO WHERE ALIAS = 'REP_CHEQ_PERIODO';
    IF v_existe > 0 THEN
        DBMS_OUTPUT.PUT_LINE('REP_CHEQ_PERIODO ya existe. No se siembra de nuevo.');
        RETURN;
    END IF;

    SELECT TIPO_CAMPO_ID INTO v_t_fecha    FROM MFO_TIPO_CAMPO WHERE CODIGO = 'FECHA';
    SELECT TIPO_CAMPO_ID INTO v_t_catalogo FROM MFO_TIPO_CAMPO WHERE CODIGO = 'CATALOGO';

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
        v_formulario_id, 'REP_CHEQ_PERIODO',
        'Parametros - Relacion de Cheques Emitidos por Periodos',
        'Periodo obligatorio y filtros opcionales de banco y cuenta. Sustituye el Parameter Form de ADM_PERIODOS_CHEQUES. Para la version con motivo, orden de pago y partidas, usar REP_CHEQ_MOTIVO.',
        'Reportes', c_empresa,
        'ACTIVO', NULL, NULL, NULL,
        'N', 'PARAMETROS', 'N', 'SEMILLA', SYSDATE
    );

    SELECT SEQ_MFO_VERSION.NEXTVAL INTO v_version_id FROM DUAL;
    INSERT INTO MFO_VERSION (
        VERSION_ID, FORMULARIO_ID, NUMERO, ESTADO, NOTAS, USUARIO_INS, FECHA_INS
    ) VALUES (
        v_version_id, v_formulario_id, 1, 'BORRADOR',
        'Version inicial sembrada por 21_MFO_SEMILLA_CHEQ_PER.sql (requerimiento 24).',
        'SEMILLA', SYSDATE
    );

    SELECT SEQ_MFO_SECCION.NEXTVAL INTO v_seccion_id FROM DUAL;
    INSERT INTO MFO_SECCION (
        SECCION_ID, VERSION_ID, CLAVE, TITULO, DESCRIPCION, ORDEN, COLUMNAS,
        ES_PASO, REPETIBLE, MIN_FILAS, MAX_FILAS, COLAPSABLE
    ) VALUES (
        v_seccion_id, v_version_id, 'FILTROS', 'Periodo y filtros',
        'El periodo se toma sobre la fecha de emision del cheque. Deje en blanco los filtros que no quiera aplicar.',
        10, 2, 'N', 'N', NULL, NULL, 'N'
    );

    -- ------------------------------------------------------------------------
    -- Campos. Las CLAVE son las mismas que en REP_CHEQ_MOTIVO a proposito: el
    -- mapeo a parametro se hace por clave de campo, asi que usar el mismo
    -- vocabulario en los dos formularios hace que las dos configuraciones se
    -- lean igual.
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

    SELECT SEQ_MFO_CAMPO.NEXTVAL INTO v_c_banco FROM DUAL;
    INSERT INTO MFO_CAMPO (
        CAMPO_ID, VERSION_ID, SECCION_ID, TIPO_CAMPO_ID, CLAVE, ETIQUETA, AYUDA,
        PLACEHOLDER, ORDEN, ANCHO, REQUERIDO, SOLO_LECTURA, VALOR_DEFECTO,
        ORIGEN_OPCIONES, CATALOGO_CLAVE, MASCARA, UNIDAD, EXPRESION
    ) VALUES (
        v_c_banco, v_version_id, v_seccion_id, v_t_catalogo, 'BANCO',
        'Banco', 'Vacio = todos los bancos.', 'Todos', 30, 6,
        'N', 'N', NULL, 'CATALOGO', 'SIS_BANCO_NOMBRE', NULL, NULL, NULL
    );

    SELECT SEQ_MFO_CAMPO.NEXTVAL INTO v_c_cuenta FROM DUAL;
    INSERT INTO MFO_CAMPO (
        CAMPO_ID, VERSION_ID, SECCION_ID, TIPO_CAMPO_ID, CLAVE, ETIQUETA, AYUDA,
        PLACEHOLDER, ORDEN, ANCHO, REQUERIDO, SOLO_LECTURA, VALOR_DEFECTO,
        ORIGEN_OPCIONES, CATALOGO_CLAVE, MASCARA, UNIDAD, EXPRESION
    ) VALUES (
        v_c_cuenta, v_version_id, v_seccion_id, v_t_catalogo, 'CUENTA',
        'Cuenta bancaria', 'Vacio = todas las cuentas. Puede combinarse con el banco.', 'Todas', 40, 6,
        'N', 'N', NULL, 'CATALOGO', 'SIS_CUENTA_BANCO', NULL, NULL, NULL
    );

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
    -- MAX_FILAS = 6000, el doble que el de la variante con motivo: aqui cada
    -- cheque ocupa una sola linea en vez de una linea mas un parrafo, asi que en
    -- el mismo numero de paginas caben muchos mas.
    --
    -- TIMEOUT_SEG = 180, mas bajo que el 300 de la variante con motivo: este
    -- reporte no llama a ADM_F_GET_PARTIDAS_CHEQUE, que es lo que hacia lento al
    -- otro. Recordar que TIMEOUT_SEG corta la espera de la peticion, NO la
    -- consulta en Oracle.
    -- ------------------------------------------------------------------------
    SELECT SEQ_MFO_REPORTE.NEXTVAL INTO v_reporte_id FROM DUAL;
    INSERT INTO MFO_REPORTE (
        REPORTE_ID, FORMULARIO_ID, CLAVE, NOMBRE, DESCRIPCION, TIPO_EJEC,
        CLAVE_REGISTRO, TITULO_REPORTE, ORIENTACION, MAX_FILAS, TIMEOUT_SEG,
        ORDEN, ACTIVO, USUARIO_INS, FECHA_INS
    ) VALUES (
        v_reporte_id, v_formulario_id, 'CHEQ_PERIODO',
        'Relacion de Cheques Emitidos por Periodos (PDF)',
        'Cheques emitidos en el periodo agrupados por banco y cuenta, con subtotales por cuenta y total general.',
        'ENDPOINT', 'REPORTE_CHEQ_PERIODO_PDF',
        'Relacion de Cheques Emitidos', 'HORIZONTAL',
        6000, 180, 10, 'S', 'SEMILLA', SYSDATE
    );

    -- Mapeo campo -> parametro. Los nombres son los de
    -- ReporteChequesPeriodoQuery. Status y CodigoProveedor no se mapean: este
    -- reporte no los expone y el handler los recibe nulos.
    INSERT INTO MFO_REP_PARAM (REP_PARAM_ID, REPORTE_ID, NOMBRE_PARAM, ORIGEN, CAMPO_ID, VALOR_FIJO, CLAVE_SISTEMA, TIPO_DATO, FORMATO, OBLIGATORIO, VALOR_DEFECTO, ORDEN)
    VALUES (SEQ_MFO_REP_PARAM.NEXTVAL, v_reporte_id, 'FechaDesde', 'CAMPO', v_c_fecha_ini, NULL, NULL, 'FECHA', 'YYYY-MM-DD', 'S', NULL, 10);

    INSERT INTO MFO_REP_PARAM (REP_PARAM_ID, REPORTE_ID, NOMBRE_PARAM, ORIGEN, CAMPO_ID, VALOR_FIJO, CLAVE_SISTEMA, TIPO_DATO, FORMATO, OBLIGATORIO, VALOR_DEFECTO, ORDEN)
    VALUES (SEQ_MFO_REP_PARAM.NEXTVAL, v_reporte_id, 'FechaHasta', 'CAMPO', v_c_fecha_fin, NULL, NULL, 'FECHA', 'YYYY-MM-DD', 'S', NULL, 20);

    INSERT INTO MFO_REP_PARAM (REP_PARAM_ID, REPORTE_ID, NOMBRE_PARAM, ORIGEN, CAMPO_ID, VALOR_FIJO, CLAVE_SISTEMA, TIPO_DATO, FORMATO, OBLIGATORIO, VALOR_DEFECTO, ORDEN)
    VALUES (SEQ_MFO_REP_PARAM.NEXTVAL, v_reporte_id, 'NombreBanco', 'CAMPO', v_c_banco, NULL, NULL, 'TEXTO', NULL, 'N', NULL, 30);

    INSERT INTO MFO_REP_PARAM (REP_PARAM_ID, REPORTE_ID, NOMBRE_PARAM, ORIGEN, CAMPO_ID, VALOR_FIJO, CLAVE_SISTEMA, TIPO_DATO, FORMATO, OBLIGATORIO, VALOR_DEFECTO, ORDEN)
    VALUES (SEQ_MFO_REP_PARAM.NEXTVAL, v_reporte_id, 'NumeroCuenta', 'CAMPO', v_c_cuenta, NULL, NULL, 'TEXTO', NULL, 'N', NULL, 40);

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
            'REP_CHEQ_PERIODO -> REPORTE_CHEQ_PERIODO_PDF sembrado por 21_MFO_SEMILLA_CHEQ_PER.sql');

    COMMIT;
    DBMS_OUTPUT.PUT_LINE('REP_CHEQ_PERIODO sembrado y publicado. REPORTE_ID = ' || v_reporte_id);
END;
/

PROMPT === Verificacion
SELECT F.ALIAS, F.MODO_USO, R.CLAVE, R.CLAVE_REGISTRO,
       (SELECT COUNT(*) FROM MFO_REP_PARAM P WHERE P.REPORTE_ID = R.REPORTE_ID) AS PARAMETROS
  FROM MFO_FORMULARIO F
  LEFT JOIN MFO_REPORTE R ON R.FORMULARIO_ID = F.FORMULARIO_ID
 WHERE F.ALIAS = 'REP_CHEQ_PERIODO';

PROMPT === Deben salir 6 parametros. Los dos reportes de cheques, uno por formulario:
SELECT F.ALIAS, R.CLAVE, R.CLAVE_REGISTRO, R.MAX_FILAS, R.TIMEOUT_SEG
  FROM MFO_FORMULARIO F
  JOIN MFO_REPORTE R ON R.FORMULARIO_ID = F.FORMULARIO_ID
 WHERE F.ALIAS IN ('REP_CHEQ_PERIODO', 'REP_CHEQ_MOTIVO')
 ORDER BY F.ALIAS;
