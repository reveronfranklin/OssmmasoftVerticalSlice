-- Requerimiento 17. Ejecutar como MFO: @23_MFO_SEMILLA_REL_COP.sql <empresa>
-- Requiere nucleo/definicion/reportes/permisos MFO instalados y backend desplegado.
-- Tres campos: proveedores opcionales (MULTI_SELECT), fecha desde y hasta obligatorias.
-- La empresa es argumento de instalacion, no se copia la constante de otra semilla.
SET SERVEROUTPUT ON
SET DEFINE ON
DEFINE COP_EMPRESA = &1
WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
DECLARE
    v_form NUMBER;
    v_version NUMBER;
    v_seccion NUMBER;
    v_reporte NUMBER;
    v_desde NUMBER;
    v_hasta NUMBER;
    v_proveedores NUMBER;
    v_fecha NUMBER;
    v_multi NUMBER;
    v_existe NUMBER;
    v_empresa NUMBER;
    c_empresa CONSTANT NUMBER := TO_NUMBER('&&COP_EMPRESA');
BEGIN
    SAVEPOINT inicio_cop;
    IF c_empresa IS NULL OR c_empresa <= 0 OR c_empresa <> TRUNC(c_empresa) THEN
        RAISE_APPLICATION_ERROR(-20001, 'Indique una empresa valida.');
    END IF;
    SELECT COUNT(*), MAX(CODIGO_EMPRESA) INTO v_existe, v_empresa
      FROM MFO_FORMULARIO WHERE ALIAS = 'REP_REL_COP';
    IF v_existe > 0 THEN
        IF v_empresa <> c_empresa THEN
            RAISE_APPLICATION_ERROR(-20002, 'El alias REP_REL_COP pertenece a otra empresa.');
        END IF;
        DBMS_OUTPUT.PUT_LINE('REP_REL_COP ya existe; se conservan configuracion y permisos.');
        RETURN;
    END IF;
    SELECT TIPO_CAMPO_ID INTO v_fecha FROM MFO_TIPO_CAMPO WHERE CODIGO = 'FECHA';
    SELECT TIPO_CAMPO_ID INTO v_multi FROM MFO_TIPO_CAMPO WHERE CODIGO = 'MULTI_SELECT';
    SELECT SEQ_MFO_FORMULARIO.NEXTVAL INTO v_form FROM DUAL;
    INSERT INTO MFO_FORMULARIO (
        FORMULARIO_ID, ALIAS, NOMBRE, DESCRIPCION, CATEGORIA, CODIGO_EMPRESA,
        ESTADO, VERSION_PUBL_ID, PERMITE_BORRADOR, MODO_USO, REGISTRA_EJEC, USUARIO_INS, FECHA_INS
    ) VALUES (
        v_form, 'REP_REL_COP', 'Relacion de compromisos con orden de pago y cheque',
        'Consulta por fechas de compromiso y lista opcional de proveedores. Sin seleccion incluye todos.',
        'Reportes', c_empresa, 'ACTIVO', NULL, 'N', 'PARAMETROS', 'N', 'SEMILLA', SYSDATE
    );
    SELECT SEQ_MFO_VERSION.NEXTVAL INTO v_version FROM DUAL;
    INSERT INTO MFO_VERSION (VERSION_ID, FORMULARIO_ID, NUMERO, ESTADO, NOTAS, USUARIO_INS, FECHA_INS)
    VALUES (v_version, v_form, 1, 'BORRADOR', 'Requerimiento 17. Tres filtros de entrada.', 'SEMILLA', SYSDATE);
    SELECT SEQ_MFO_SECCION.NEXTVAL INTO v_seccion FROM DUAL;
    INSERT INTO MFO_SECCION (SECCION_ID, VERSION_ID, CLAVE, TITULO, ORDEN, COLUMNAS,
                            ES_PASO, REPETIBLE, COLAPSABLE)
    VALUES (v_seccion, v_version, 'FILTROS', 'Filtros del reporte', 10, 2, 'N', 'N', 'N');
    SELECT SEQ_MFO_CAMPO.NEXTVAL INTO v_proveedores FROM DUAL;
    INSERT INTO MFO_CAMPO (CAMPO_ID, VERSION_ID, SECCION_ID, TIPO_CAMPO_ID, CLAVE,
                           ETIQUETA, AYUDA, ORDEN, ANCHO, REQUERIDO, SOLO_LECTURA,
                           ORIGEN_OPCIONES, CATALOGO_CLAVE)
    VALUES (v_proveedores, v_version, v_seccion, v_multi, 'PROVEEDORES', 'Proveedores',
            'Opcional. Sin seleccion se consultan todos los proveedores.', 10, 12, 'N', 'N', 'CATALOGO', 'ADM_PROVEEDORES_COP');
    SELECT SEQ_MFO_CAMPO.NEXTVAL INTO v_desde FROM DUAL;
    INSERT INTO MFO_CAMPO (CAMPO_ID, VERSION_ID, SECCION_ID, TIPO_CAMPO_ID, CLAVE,
                           ETIQUETA, AYUDA, ORDEN, ANCHO, REQUERIDO, SOLO_LECTURA,
                           ORIGEN_OPCIONES, CATALOGO_CLAVE)
    VALUES (v_desde, v_version, v_seccion, v_fecha, 'FECHA_DESDE', 'Fecha desde',
            'Fecha del compromiso, inclusive.', 20, 6, 'S', 'N', NULL, NULL);
    SELECT SEQ_MFO_CAMPO.NEXTVAL INTO v_hasta FROM DUAL;
    INSERT INTO MFO_CAMPO (CAMPO_ID, VERSION_ID, SECCION_ID, TIPO_CAMPO_ID, CLAVE,
                           ETIQUETA, AYUDA, ORDEN, ANCHO, REQUERIDO, SOLO_LECTURA,
                           ORIGEN_OPCIONES, CATALOGO_CLAVE)
    VALUES (v_hasta, v_version, v_seccion, v_fecha, 'FECHA_HASTA', 'Fecha hasta',
            'Incluye todo el dia seleccionado.', 30, 6, 'S', 'N', NULL, NULL);
    UPDATE MFO_VERSION SET ESTADO = 'PUBLICADA', FECHA_PUBL = SYSDATE, USUARIO_PUBL = 'SEMILLA'
     WHERE VERSION_ID = v_version;
    UPDATE MFO_FORMULARIO SET VERSION_PUBL_ID = v_version WHERE FORMULARIO_ID = v_form;
    SELECT SEQ_MFO_REPORTE.NEXTVAL INTO v_reporte FROM DUAL;
    INSERT INTO MFO_REPORTE (REPORTE_ID, FORMULARIO_ID, CLAVE, NOMBRE, DESCRIPCION,
        TIPO_EJEC, CLAVE_REGISTRO, TITULO_REPORTE, ORIENTACION, MAX_FILAS, TIMEOUT_SEG,
        ORDEN, ACTIVO, USUARIO_INS, FECHA_INS)
    VALUES (v_reporte, v_form, 'REL_COP', 'Relacion de compromisos, ordenes y cheques (PDF)',
        'Montos netos por compromiso y partida, con sus ordenes y cheques sin duplicar importes.',
        'ENDPOINT', 'REPORTE_REL_COP_PDF', 'Relacion de compromisos con orden de pago y cheque',
        'HORIZONTAL', 10000, 240, 10, 'S', 'SEMILLA', SYSDATE);
    INSERT INTO MFO_REP_PARAM (REP_PARAM_ID, REPORTE_ID, NOMBRE_PARAM, ORIGEN,
                               CAMPO_ID, CLAVE_SISTEMA, TIPO_DATO, OBLIGATORIO, ORDEN)
    VALUES (SEQ_MFO_REP_PARAM.NEXTVAL, v_reporte, 'CodigosProveedores', 'CAMPO',
            v_proveedores, NULL, 'NUMERO', 'N', 10);
    INSERT INTO MFO_REP_PARAM (REP_PARAM_ID, REPORTE_ID, NOMBRE_PARAM, ORIGEN,
                               CAMPO_ID, CLAVE_SISTEMA, TIPO_DATO, OBLIGATORIO, ORDEN)
    VALUES (SEQ_MFO_REP_PARAM.NEXTVAL, v_reporte, 'FechaDesde', 'CAMPO',
            v_desde, NULL, 'FECHA', 'S', 20);
    INSERT INTO MFO_REP_PARAM (REP_PARAM_ID, REPORTE_ID, NOMBRE_PARAM, ORIGEN,
                               CAMPO_ID, CLAVE_SISTEMA, TIPO_DATO, OBLIGATORIO, ORDEN)
    VALUES (SEQ_MFO_REP_PARAM.NEXTVAL, v_reporte, 'FechaHasta', 'CAMPO',
            v_hasta, NULL, 'FECHA', 'S', 30);
    INSERT INTO MFO_REP_PARAM (REP_PARAM_ID, REPORTE_ID, NOMBRE_PARAM, ORIGEN,
                               CAMPO_ID, CLAVE_SISTEMA, TIPO_DATO, OBLIGATORIO, ORDEN)
    VALUES (SEQ_MFO_REP_PARAM.NEXTVAL, v_reporte, 'CodigoEmpresa', 'SISTEMA',
            NULL, 'CODIGO_EMPRESA', 'NUMERO', 'S', 40);
    INSERT INTO MFO_REP_PARAM (REP_PARAM_ID, REPORTE_ID, NOMBRE_PARAM, ORIGEN,
                               CAMPO_ID, CLAVE_SISTEMA, TIPO_DATO, OBLIGATORIO, ORDEN)
    VALUES (SEQ_MFO_REP_PARAM.NEXTVAL, v_reporte, 'Usuario', 'SISTEMA',
            NULL, 'USUARIO', 'TEXTO', 'S', 50);
    INSERT INTO MFO_AUDITORIA (AUDITORIA_ID, ENTIDAD, ENTIDAD_ID, ACCION, USUARIO, FECHA, DETALLE_CLB)
    VALUES (SEQ_MFO_AUDITORIA.NEXTVAL, 'REPORTE', v_reporte, 'CREAR', 'SEMILLA', SYSDATE,
            'REP_REL_COP -> REPORTE_REL_COP_PDF. Requerimiento 17.');
    COMMIT;
    DBMS_OUTPUT.PUT_LINE('REP_REL_COP publicado. Asignar permisos a los usuarios/roles destinatarios.');
EXCEPTION
    WHEN OTHERS THEN
        ROLLBACK TO inicio_cop;
        RAISE;
END;
/
UNDEFINE COP_EMPRESA
SELECT F.ALIAS, F.CODIGO_EMPRESA, F.ESTADO, R.CLAVE_REGISTRO
  FROM MFO_FORMULARIO F JOIN MFO_REPORTE R ON R.FORMULARIO_ID = F.FORMULARIO_ID
 WHERE F.ALIAS = 'REP_REL_COP';
SELECT C.CLAVE, C.REQUERIDO, C.CATALOGO_CLAVE
  FROM MFO_CAMPO C JOIN MFO_FORMULARIO F ON F.VERSION_PUBL_ID = C.VERSION_ID
 WHERE F.ALIAS = 'REP_REL_COP' ORDER BY C.ORDEN;
