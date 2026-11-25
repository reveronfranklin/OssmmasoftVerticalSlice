-- R58 - INSTALADOR UNICO DE PRODUCCION / SQL*Plus / Oracle 10g.
-- Incluye exclusivamente los 10 procedimientos publicados y verificados en QA.
-- Ejecutar conectado como CNT, con API y procesos automaticos detenidos.
-- Publicar API compatible (signos, filtro tipo, OUT totales, ODC/TDC) y frontend.
-- No usar appsettings ni build frontend de QA en produccion.
-- DDL confirma cambios automaticamente; ROLLBACK no restaura procedimientos.
-- Este archivo genera R58_RESTAURAR_<tag>.sql antes del primer CREATE.
-- No ejecuta CONF/PREV, cierres, conversion historica, GRANT ni DML de negocio.
-- Los DML dentro de las definiciones se ejecutan SOLO cuando la app las invoque.
-- No certifica las parejas origen/tipo que siguen pendientes en el req58.
-- Cambiar solo la empresa de prevalidacion si produccion usa otro codigo.
WHENEVER OSERROR EXIT FAILURE ROLLBACK
WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK
SET ECHO OFF
SET VERIFY OFF
SET FEEDBACK OFF
SET HEADING OFF
SET PAGESIZE 0
SET LINESIZE 32767
SET TRIMSPOOL OFF
SET SQLBLANKLINES ON
SET SERVEROUTPUT ON SIZE 1000000 FORMAT WRAPPED
SET DEFINE ON
DEFINE R58_EMPRESA = 13
VARIABLE R58_EMPRESA NUMBER
BEGIN :R58_EMPRESA := &&R58_EMPRESA; END;
/
COLUMN R58_TAG NEW_VALUE R58_TAG NOPRINT
SELECT TO_CHAR(SYSDATE,'YYYYMMDD_HH24MISS')||'_'||SYS_CONTEXT('USERENV','SESSIONID') R58_TAG FROM DUAL;
SPOOL R58_INSTALAR_&&R58_TAG..log
PROMPT R58 - Verificar conexion CNT de PRODUCCION:
SELECT SYS_CONTEXT('USERENV','DB_NAME')||' / '||SYS_CONTEXT('USERENV','SERVER_HOST')||' / '||SYS_CONTEXT('USERENV','SESSION_USER') FROM DUAL;
-- BEGIN R58 TARGET GUARD


DECLARE
BEGIN
    IF SYS_CONTEXT('USERENV','SESSION_USER') <> 'CNT'
       OR SYS_CONTEXT('USERENV','CURRENT_SCHEMA') <> 'CNT' THEN
        RAISE_APPLICATION_ERROR(-20058,'R58: conectese como CNT y use CURRENT_SCHEMA=CNT.');
    END IF;
    IF SYS_CONTEXT('USERENV','DB_NAME') = 'XE'
       AND SYS_CONTEXT('USERENV','SERVER_HOST') = '6d88ba053190' THEN
        RAISE_APPLICATION_ERROR(-20058,'R58: este instalador de produccion rechaza el QA conocido.');
    END IF;
END;
/

-- END R58 TARGET GUARD
-- BEGIN R58 PREFLIGHT

DECLARE
    v_errores NUMBER := 0;
    v_count NUMBER;
    v_bytes NUMBER;
    PROCEDURE fallo(p_texto VARCHAR2) IS
    BEGIN
        v_errores := v_errores + 1;
        DBMS_OUTPUT.PUT_LINE('FALTA: ' || p_texto);
    END;
    PROCEDURE objeto(p_owner VARCHAR2,p_nombre VARCHAR2) IS
        v_tipo VARCHAR2(30);
        v_priv VARCHAR2(30);
        v_n NUMBER;
    BEGIN
        SELECT COUNT(*), MIN(OBJECT_TYPE) INTO v_n,v_tipo
          FROM ALL_OBJECTS
         WHERE OWNER=p_owner AND OBJECT_NAME=p_nombre AND STATUS='VALID'
           AND OBJECT_TYPE IN ('TABLE','VIEW','FUNCTION','PROCEDURE','PACKAGE','SYNONYM');
        IF v_n=0 THEN
            fallo(p_owner||'.'||p_nombre||' no visible o INVALID'); RETURN;
        END IF;
        IF p_owner <> 'CNT' THEN
            v_priv := CASE WHEN v_tipo IN ('FUNCTION','PROCEDURE','PACKAGE')
                           THEN 'EXECUTE' ELSE 'SELECT' END;
            SELECT COUNT(*) INTO v_n FROM ALL_TAB_PRIVS
             WHERE TABLE_SCHEMA=p_owner AND TABLE_NAME=p_nombre
               AND GRANTEE IN ('CNT','PUBLIC') AND PRIVILEGE=v_priv;
            IF v_n=0 THEN
                SELECT COUNT(*) INTO v_n FROM USER_SYS_PRIVS
                 WHERE PRIVILEGE=CASE WHEN v_priv='EXECUTE'
                                      THEN 'EXECUTE ANY PROCEDURE' ELSE 'SELECT ANY TABLE' END;
            END IF;
            IF v_n=0 THEN fallo('privilegio directo '||v_priv||' sobre '||p_owner||'.'||p_nombre); END IF;
        END IF;
    END;
    PROCEDURE columna(p_tabla VARCHAR2,p_columna VARCHAR2) IS
        v_n NUMBER;
    BEGIN
        SELECT COUNT(*) INTO v_n FROM ALL_TAB_COLUMNS
         WHERE OWNER='CNT' AND TABLE_NAME=p_tabla AND COLUMN_NAME=p_columna;
        IF v_n<>1 THEN fallo('CNT.'||p_tabla||'.'||p_columna); END IF;
    END;
BEGIN
    SELECT COUNT(*) INTO v_count FROM SESSION_PRIVS
     WHERE PRIVILEGE IN ('CREATE PROCEDURE','CREATE ANY PROCEDURE');
    IF v_count=0 THEN fallo('permiso CREATE PROCEDURE'); END IF;
    objeto('ADM','ADM_BENEFICIARIOS_CH');
    objeto('ADM','ADM_BENEFICIARIOS_OP');
    objeto('ADM','ADM_CHEQUES');
    objeto('ADM','ADM_COMPROMISO_OP');
    objeto('ADM','ADM_CONTRATOS');
    objeto('ADM','ADM_DESCRIPTIVAS');
    objeto('ADM','ADM_DETALLE_ORDEN_COMPRA');
    objeto('ADM','ADM_DOCUMENTOS_OP');
    objeto('ADM','ADM_F_DESCRIPTIVAS');
    objeto('ADM','ADM_F_DESCRIPTIVAS_ID');
    objeto('ADM','ADM_F_PROVEEDOR');
    objeto('ADM','ADM_F_PROVEEDOR_ID');
    objeto('ADM','ADM_ORDEN_COMPRA');
    objeto('ADM','ADM_ORDEN_PAGO');
    objeto('ADM','ADM_PROVEEDORES');
    objeto('ADM','ADM_PUC_CONTRATO');
    objeto('ADM','ADM_PUC_ORDEN_COMPRA');
    objeto('ADM','ADM_PUC_ORDEN_PAGO');
    objeto('ADM','ADM_RETENCIONES_OP');
    objeto('CNT','CNT_AUT_LINE_WRK');
    objeto('CNT','CNT_AUXILIARES');
    objeto('CNT','CNT_AUXILIARES_PUC');
    objeto('CNT','CNT_CMP_CTL');
    objeto('CNT','CNT_CMP_GUARD');
    objeto('CNT','CNT_COMPROBANTES');
    objeto('CNT','CNT_DESCRIPTIVAS');
    objeto('CNT','CNT_DESCRIPTIVA_ID');
    objeto('CNT','CNT_DETALLE_COMPROBANTE');
    objeto('CNT','CNT_MAYORES');
    objeto('CNT','CNT_PERIODOS');
    objeto('CNT','CNT_TITULOS');
    objeto('CNT','SP_CNT_CMP_INS');
    objeto('CNT','SP_CNT_DET_INS');
    objeto('PRE','PRE_COMPROMISOS');
    objeto('PRE','PRE_DETALLE_COMPROMISOS');
    objeto('PRE','PRE_PRESUPUESTOS');
    objeto('PRE','PRE_PUC_COMPROMISOS');
    objeto('PRE','PRE_V_SALDOS');
    objeto('SIS','SIS_BANCOS');
    objeto('SIS','SIS_CUENTAS_BANCOS');
    columna('CNT_COMPROBANTES','CODIGO_COMPROBANTE');
    columna('CNT_COMPROBANTES','CODIGO_PERIODO');
    columna('CNT_COMPROBANTES','TIPO_COMPROBANTE_ID');
    columna('CNT_COMPROBANTES','ORIGEN_ID');
    columna('CNT_COMPROBANTES','FECHA_COMPROBANTE');
    columna('CNT_COMPROBANTES','NUMERO_COMPROBANTE');
    columna('CNT_COMPROBANTES','CODIGO_EMPRESA');
    columna('CNT_COMPROBANTES','ESTADO_CMP');
    columna('CNT_COMPROBANTES','AJUSTE_MANUAL');
    columna('CNT_COMPROBANTES','VERSION_CMP');
    columna('CNT_COMPROBANTES','EXTRA1');
    columna('CNT_COMPROBANTES','USUARIO_UPD');
    columna('CNT_COMPROBANTES','FECHA_UPD');
    columna('CNT_COMPROBANTES','OBSERVACION');
    columna('CNT_DETALLE_COMPROBANTE','CODIGO_DETALLE_COMPROBANTE');
    columna('CNT_DETALLE_COMPROBANTE','CODIGO_COMPROBANTE');
    columna('CNT_DETALLE_COMPROBANTE','CODIGO_EMPRESA');
    columna('CNT_DETALLE_COMPROBANTE','CODIGO_MAYOR');
    columna('CNT_DETALLE_COMPROBANTE','CODIGO_AUXILIAR');
    columna('CNT_DETALLE_COMPROBANTE','MONTO');
    columna('CNT_CMP_CTL','CODIGO');
    columna('CNT_CMP_CTL','HABILITADO');
    columna('CNT_AUT_LINE_WRK','RUN_ID');
    columna('CNT_AUT_LINE_WRK','SECUENCIA');
    columna('CNT_AUT_LINE_WRK','CODIGO_MAYOR');
    columna('CNT_AUT_LINE_WRK','CODIGO_AUXILIAR');
    columna('CNT_AUT_LINE_WRK','REFERENCIA1');
    columna('CNT_AUT_LINE_WRK','REFERENCIA2');
    columna('CNT_AUT_LINE_WRK','REFERENCIA3');
    columna('CNT_AUT_LINE_WRK','DESCRIPCION');
    columna('CNT_AUT_LINE_WRK','MONTO');
    columna('CNT_AUT_LINE_WRK','FECHA_REGISTRO');
    columna('CNT_AUXILIARES_PUC','CODIGO_AUXILIAR');
    columna('CNT_AUXILIARES_PUC','CODIGO_PUC');
    columna('CNT_AUXILIARES_PUC','TIPO_DOCUMENTO_ID');
    columna('CNT_AUXILIARES_PUC','CODIGO_EMPRESA');
    columna('CNT_DESCRIPTIVAS','DESCRIPCION_ID');
    columna('CNT_DESCRIPTIVAS','TITULO_ID');
    columna('CNT_DESCRIPTIVAS','CODIGO');
    columna('CNT_DESCRIPTIVAS','DESCRIPCION');
    columna('CNT_DESCRIPTIVAS','CODIGO_EMPRESA');
    columna('CNT_DESCRIPTIVAS','EXTRA1');
    columna('CNT_DESCRIPTIVAS','EXTRA2');
    columna('CNT_DESCRIPTIVAS','EXTRA3');
    columna('CNT_TITULOS','TITULO_ID');
    columna('CNT_TITULOS','CODIGO');
    columna('CNT_PERIODOS','CODIGO_PERIODO');
    columna('CNT_PERIODOS','CODIGO_EMPRESA');
    columna('CNT_PERIODOS','FECHA_DESDE');
    columna('CNT_PERIODOS','FECHA_HASTA');
    columna('CNT_PERIODOS','FECHA_CIERRE');
    columna('CNT_PERIODOS','NOMBRE_PERIODO');
    SELECT COUNT(*) INTO v_count FROM ALL_OBJECTS
     WHERE OWNER='CNT' AND OBJECT_NAME='CNT_CMP_GUARD'
       AND OBJECT_TYPE='PACKAGE BODY' AND STATUS='VALID';
    IF v_count<>1 THEN fallo('CNT_CMP_GUARD PACKAGE BODY VALID (prerrequisito req37)'); END IF;
    SELECT COUNT(DISTINCT PROCEDURE_NAME) INTO v_count FROM ALL_PROCEDURES
     WHERE OWNER='CNT' AND OBJECT_NAME='CNT_CMP_GUARD'
       AND PROCEDURE_NAME IN ('LOCK_ALL','ALLOW_WRITE');
    IF v_count<>2 THEN fallo('CNT_CMP_GUARD.LOCK_ALL/ALLOW_WRITE'); END IF;
    SELECT COUNT(*) INTO v_count FROM ALL_ARGUMENTS
     WHERE OWNER='CNT' AND OBJECT_NAME IN ('SP_CNT_CMP_INS','SP_CNT_DET_INS')
       AND PACKAGE_NAME IS NULL AND ARGUMENT_NAME='P_PERMITIR_AUTOMATICO'
       AND IN_OUT='IN';
    IF v_count<>2 THEN fallo('SP_CNT_CMP_INS/DET_INS con p_PERMITIR_AUTOMATICO (req37)'); END IF;
    SELECT COUNT(*) INTO v_count FROM ALL_TRIGGERS T
     WHERE T.OWNER='CNT' AND T.TRIGGER_NAME IN ('CNT_CMP_WRITE_TRG','CNT_DET_WRITE_TRG')
       AND T.STATUS='ENABLED'
       AND EXISTS (SELECT 1 FROM ALL_OBJECTS O WHERE O.OWNER=T.OWNER
                    AND O.OBJECT_NAME=T.TRIGGER_NAME AND O.OBJECT_TYPE='TRIGGER' AND O.STATUS='VALID');
    IF v_count<>2 THEN fallo('triggers CNT_CMP_WRITE_TRG/CNT_DET_WRITE_TRG habilitados y VALID'); END IF;
    SELECT COUNT(*) INTO v_count FROM ALL_SOURCE
     WHERE OWNER='CNT' AND TYPE='PROCEDURE' AND LINE=1 AND NAME IN ('SP_CNT_AUT_PRES_GET','SP_CNT_AUT_BUILD_COMP02','SP_CNT_AUT_BUILD_COMP','SP_CNT_AUT_BUILD_OP','SP_CNT_AUT_BUILD_CH_RET','SP_CNT_AUT_PREV','SP_CNT_AUT_CONF','SP_CNT_CMP_GET_ID','SP_CNT_CMP_GET_ALL','SP_CNT_CAT_DESC_GET');
    DBMS_OUTPUT.PUT_LINE('R58: fuentes anteriores disponibles='||v_count);
    SELECT NVL(SUM(LENGTHB(TEXT)),0) INTO v_bytes FROM ALL_SOURCE
     WHERE OWNER='CNT' AND TYPE='PROCEDURE' AND NAME IN ('SP_CNT_AUT_PRES_GET','SP_CNT_AUT_BUILD_COMP02','SP_CNT_AUT_BUILD_COMP','SP_CNT_AUT_BUILD_OP','SP_CNT_AUT_BUILD_CH_RET','SP_CNT_AUT_PREV','SP_CNT_AUT_CONF','SP_CNT_CMP_GET_ID','SP_CNT_CMP_GET_ALL','SP_CNT_CAT_DESC_GET');
    IF v_bytes>850000 THEN fallo('respaldo supera el buffer; solicite respaldo externo antes de instalar'); END IF;
    IF v_errores>0 THEN
        RAISE_APPLICATION_ERROR(-20058,'R58: prevalidacion incompleta ('||v_errores||'). Sin cambios DDL.');
    END IF;
    EXECUTE IMMEDIATE 'SELECT COUNT(*) FROM CNT.CNT_PERIODOS WHERE CODIGO_EMPRESA=:1'
        INTO v_count USING :R58_EMPRESA;
    IF v_count=0 THEN RAISE_APPLICATION_ERROR(-20058,'R58: no existen periodos para la empresa indicada.'); END IF;
    EXECUTE IMMEDIATE 'SELECT COUNT(*) FROM CNT.CNT_CMP_CTL WHERE CODIGO=1'
        INTO v_count;
    IF v_count<>1 THEN RAISE_APPLICATION_ERROR(-20058,'R58: falta fila CNT_CMP_CTL.CODIGO=1 (req37).'); END IF;
    EXECUTE IMMEDIATE 'SELECT COUNT(DISTINCT CODIGO) FROM CNT.CNT_TITULOS WHERE (CODIGO=''ODC'' AND TITULO_ID=1) OR (CODIGO=''TDC'' AND TITULO_ID=5)'
        INTO v_count;
    IF v_count<>2 THEN RAISE_APPLICATION_ERROR(-20058,'R58: los procesos existentes requieren ODC=1 y TDC=5. Revisar equivalencias antes de instalar.'); END IF;
    DBMS_OUTPUT.PUT_LINE('R58: prevalidacion correcta. Empresa='||:R58_EMPRESA);
    DBMS_OUTPUT.PUT_LINE('No se habilita ni modifica CNT_CMP_CTL.');
END;
/

-- END R58 PREFLIGHT


PROMPT R58 - Prevalidacion aprobada. Generando respaldo de fuentes anteriores.
SPOOL OFF
SPOOL R58_RESTAURAR_&&R58_TAG..sql
-- BEGIN R58 BACKUP WRITER


DECLARE
    v_count NUMBER;
    PROCEDURE guardar(p_nombre VARCHAR2) IS
    BEGIN
        DBMS_OUTPUT.PUT_LINE('-- R58 RESTORE BEGIN '||p_nombre);
        SELECT COUNT(*) INTO v_count FROM ALL_OBJECTS
         WHERE OWNER='CNT' AND OBJECT_NAME=p_nombre AND OBJECT_TYPE='PROCEDURE';
        IF v_count=1 THEN
            SELECT COUNT(*) INTO v_count FROM ALL_SOURCE
             WHERE OWNER='CNT' AND NAME=p_nombre AND TYPE='PROCEDURE';
            IF v_count=0 THEN RAISE_APPLICATION_ERROR(-20058,'R58: no se puede respaldar '||p_nombre); END IF;
            DBMS_OUTPUT.PUT_LINE('CREATE OR REPLACE');
            FOR r IN (SELECT TEXT FROM ALL_SOURCE WHERE OWNER='CNT'
                       AND NAME=p_nombre AND TYPE='PROCEDURE' ORDER BY LINE) LOOP
                IF SUBSTR(r.TEXT,-2)=CHR(13)||CHR(10) THEN
                    DBMS_OUTPUT.PUT_LINE(SUBSTR(r.TEXT,1,LENGTH(r.TEXT)-2));
                ELSIF SUBSTR(r.TEXT,-1) IN (CHR(10),CHR(13)) THEN
                    DBMS_OUTPUT.PUT_LINE(SUBSTR(r.TEXT,1,LENGTH(r.TEXT)-1));
                ELSE
                    DBMS_OUTPUT.PUT_LINE(r.TEXT);
                END IF;
            END LOOP;
            DBMS_OUTPUT.PUT_LINE('/');
        ELSE
            DBMS_OUTPUT.PUT_LINE('-- Objeto ausente antes de instalar: retirar solo al restaurar.');
            DBMS_OUTPUT.PUT_LINE('BEGIN');
            DBMS_OUTPUT.PUT_LINE('  EXECUTE IMMEDIATE ''DROP PROCEDURE CNT.'||p_nombre||''';');
            DBMS_OUTPUT.PUT_LINE('EXCEPTION WHEN OTHERS THEN IF SQLCODE <> -4043 THEN RAISE; END IF;');
            DBMS_OUTPUT.PUT_LINE('END;');
            DBMS_OUTPUT.PUT_LINE('/');
        END IF;
        DBMS_OUTPUT.PUT_LINE('-- R58 RESTORE END '||p_nombre);
        DBMS_OUTPUT.PUT_LINE(' ');
    END;
BEGIN
    DBMS_OUTPUT.PUT_LINE('-- R58: RESPALDO REAL ANTERIOR A ESTA EJECUCION.');
    DBMS_OUTPUT.PUT_LINE('-- Detener API y procesos automaticos antes de restaurar.');
    DBMS_OUTPUT.PUT_LINE('-- Restaurar tambien la version anterior de API/frontend. DDL confirma cambios.');
    DBMS_OUTPUT.PUT_LINE('WHENEVER SQLERROR EXIT SQL.SQLCODE ROLLBACK');
    DBMS_OUTPUT.PUT_LINE('SET DEFINE OFF');
    DBMS_OUTPUT.PUT_LINE('SET SQLBLANKLINES ON');
    DBMS_OUTPUT.PUT_LINE('BEGIN IF SYS_CONTEXT(''USERENV'',''SESSION_USER'') <> ''CNT'' OR SYS_CONTEXT(''USERENV'',''CURRENT_SCHEMA'') <> ''CNT'' THEN RAISE_APPLICATION_ERROR(-20058,''R58: restaurar como CNT.''); END IF; END;');
    DBMS_OUTPUT.PUT_LINE('/');
    guardar('SP_CNT_AUT_PRES_GET');
    guardar('SP_CNT_AUT_BUILD_COMP02');
    guardar('SP_CNT_AUT_BUILD_COMP');
    guardar('SP_CNT_AUT_BUILD_OP');
    guardar('SP_CNT_AUT_BUILD_CH_RET');
    guardar('SP_CNT_AUT_PREV');
    guardar('SP_CNT_AUT_CONF');
    guardar('SP_CNT_CMP_GET_ID');
    guardar('SP_CNT_CMP_GET_ALL');
    guardar('SP_CNT_CAT_DESC_GET');
    DBMS_OUTPUT.PUT_LINE('PROMPT R58: fuentes anteriores restauradas; revisar USER_ERRORS y dependencias.');
END;
/

-- END R58 BACKUP WRITER
SPOOL OFF
SPOOL R58_INSTALAR_&&R58_TAG..log APPEND
PROMPT R58 - Respaldo generado. Comienza DDL de los 10 procedimientos.
SET DEFINE OFF


-- BEGIN R58 OBJECT SP_CNT_AUT_PRES_GET

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_AUT_PRES_GET (
    p_FECHA_DESDE        IN DATE,
    p_FECHA_HASTA        IN DATE,
    p_CODIGO_EMPRESA     IN NUMBER,
    p_CODIGO_PRESUPUESTO OUT NUMBER,
    p_Message           OUT VARCHAR2
) AS
    v_count NUMBER;
    v_desde DATE;
    v_hasta DATE;
BEGIN
    p_CODIGO_PRESUPUESTO := NULL;
    IF p_CODIGO_EMPRESA IS NULL OR p_CODIGO_EMPRESA <= 0 THEN
        p_Message := 'La empresa configurada no es valida.';
        RETURN;
    END IF;
    IF p_FECHA_DESDE IS NULL OR p_FECHA_HASTA IS NULL
       OR TRUNC(p_FECHA_DESDE) > TRUNC(p_FECHA_HASTA) THEN
        p_Message := 'El rango de fechas no es valido.';
        RETURN;
    END IF;

    -- Reject overlaps, including a second budget beginning inside the range.
    -- No ROWNUM/MAX selection: the complete date range must belong to one budget.
    SELECT COUNT(*), MIN(CODIGO_PRESUPUESTO), MIN(FECHA_DESDE), MIN(FECHA_HASTA)
      INTO v_count, p_CODIGO_PRESUPUESTO, v_desde, v_hasta
      FROM PRE.PRE_PRESUPUESTOS
     WHERE CODIGO_EMPRESA = p_CODIGO_EMPRESA
       AND TRUNC(FECHA_DESDE) <= TRUNC(p_FECHA_HASTA)
       AND TRUNC(FECHA_HASTA) >= TRUNC(p_FECHA_DESDE);

    IF v_count = 0 THEN
        p_Message := 'No existe presupuesto de la empresa para el rango indicado.';
    ELSIF v_count > 1 THEN
        p_Message := 'El rango coincide con varios presupuestos de la empresa; divida el rango o revise las vigencias.';
    ELSIF TRUNC(p_FECHA_DESDE) < TRUNC(v_desde)
       OR TRUNC(p_FECHA_HASTA) > TRUNC(v_hasta) THEN
        p_Message := 'El rango completo debe pertenecer a un mismo presupuesto de la empresa.';
    ELSE
        p_Message := 'Success';
        RETURN;
    END IF;
    p_CODIGO_PRESUPUESTO := NULL;
EXCEPTION
    WHEN OTHERS THEN
        p_CODIGO_PRESUPUESTO := NULL;
        p_Message := SQLERRM;
END;
/

-- END R58 OBJECT SP_CNT_AUT_PRES_GET

DECLARE
    v_count NUMBER;
BEGIN
    FOR r IN (SELECT LINE,POSITION,TEXT FROM USER_ERRORS
               WHERE NAME='SP_CNT_AUT_PRES_GET' AND TYPE='PROCEDURE' AND ATTRIBUTE='ERROR'
               ORDER BY SEQUENCE) LOOP
        DBMS_OUTPUT.PUT_LINE('LINEA '||r.LINE||':'||r.POSITION||' '||r.TEXT);
    END LOOP;
    SELECT COUNT(*) INTO v_count FROM USER_OBJECTS
     WHERE OBJECT_NAME='SP_CNT_AUT_PRES_GET' AND OBJECT_TYPE='PROCEDURE' AND STATUS='VALID';
    IF v_count<>1 THEN RAISE_APPLICATION_ERROR(-20058,'R58: SP_CNT_AUT_PRES_GET INVALID. Usar respaldo generado.'); END IF;
END;
/

-- BEGIN R58 OBJECT SP_CNT_AUT_BUILD_COMP02

-- R58: documented COMP/02 phase only. No rule is inferred for other types.
CREATE OR REPLACE PROCEDURE CNT.SP_CNT_AUT_BUILD_COMP02 (
    p_RUN_ID             IN VARCHAR2,
    p_FECHA_DESDE        IN DATE,
    p_FECHA_HASTA        IN DATE,
    p_CODIGO_PRESUPUESTO IN NUMBER,
    p_CODIGO_EMPRESA     IN NUMBER,
    p_Message           OUT VARCHAR2
) AS
    v_cuentas NUMBER;
    v_numeros NUMBER;
    v_secuencia NUMBER := 0;
    v_debe NUMBER := 0;
    v_haber NUMBER := 0;
    v_monto NUMBER;
    v_compromiso NUMBER;
    v_referencia VARCHAR2(20);
BEGIN
    DELETE FROM CNT.CNT_AUT_LINE_WRK WHERE RUN_ID = p_RUN_ID;

    SELECT COUNT(*), COUNT(DISTINCT TRIM(NUMERO_MAYOR))
      INTO v_cuentas, v_numeros
      FROM CNT.CNT_MAYORES
     WHERE CODIGO_EMPRESA = p_CODIGO_EMPRESA
       AND TRIM(NUMERO_MAYOR) IN ('300', '103');
    IF v_cuentas <> 2 OR v_numeros <> 2 THEN
        p_Message := 'COMP/02 requiere un mayor 300 y uno 103 en la empresa configurada, sin duplicados.';
        RETURN;
    END IF;

    -- A mismatched child must not disappear through the company/budget predicate.
    SELECT COUNT(*) INTO v_cuentas
      FROM PRE.PRE_COMPROMISOS AC
      JOIN PRE.PRE_DETALLE_COMPROMISOS PDC
        ON PDC.CODIGO_COMPROMISO = AC.CODIGO_COMPROMISO
      JOIN PRE.PRE_PUC_COMPROMISOS APC
        ON APC.CODIGO_DETALLE_COMPROMISO = PDC.CODIGO_DETALLE_COMPROMISO
     WHERE AC.CODIGO_EMPRESA = p_CODIGO_EMPRESA
       AND AC.CODIGO_PRESUPUESTO = p_CODIGO_PRESUPUESTO
       AND AC.STATUS <> 'AN'
       AND AC.CODIGO_PROVEEDOR NOT IN (8127, 8128)
       AND AC.FECHA_COMPROMISO >= TRUNC(p_FECHA_DESDE)
       AND AC.FECHA_COMPROMISO < TRUNC(p_FECHA_HASTA) + 1
       AND NVL(APC.MONTO, 0) <> 0
       AND (NVL(APC.CODIGO_EMPRESA, -1) <> AC.CODIGO_EMPRESA
            OR NVL(APC.CODIGO_PRESUPUESTO, -1) <> AC.CODIGO_PRESUPUESTO);
    IF v_cuentas > 0 THEN
        p_Message := 'COMP/02: existen partidas con empresa o presupuesto distintos de su compromiso; revise las fuentes.';
        RETURN;
    END IF;

    FOR r IN (
        WITH fuentes AS (
            SELECT AC.CODIGO_COMPROMISO, AC.NUMERO_COMPROMISO,
                   AC.FECHA_COMPROMISO, RTRIM(AC.MOTIVO) MOTIVO,
                   AC.CODIGO_PROVEEDOR, APC.CODIGO_PUC_COMPROMISO,
                   APC.CODIGO_PUC, APC.MONTO
              FROM PRE.PRE_COMPROMISOS AC
              JOIN PRE.PRE_DETALLE_COMPROMISOS PDC
                ON PDC.CODIGO_COMPROMISO = AC.CODIGO_COMPROMISO
              JOIN PRE.PRE_PUC_COMPROMISOS APC
                ON APC.CODIGO_DETALLE_COMPROMISO = PDC.CODIGO_DETALLE_COMPROMISO
             WHERE AC.CODIGO_EMPRESA = p_CODIGO_EMPRESA
               AND AC.CODIGO_PRESUPUESTO = p_CODIGO_PRESUPUESTO
               AND APC.CODIGO_EMPRESA = AC.CODIGO_EMPRESA
               AND APC.CODIGO_PRESUPUESTO = AC.CODIGO_PRESUPUESTO
               AND AC.STATUS <> 'AN'
               AND AC.CODIGO_PROVEEDOR NOT IN (8127, 8128)
               AND AC.FECHA_COMPROMISO >= TRUNC(p_FECHA_DESDE)
               AND AC.FECHA_COMPROMISO < TRUNC(p_FECHA_HASTA) + 1
               AND NVL(APC.MONTO, 0) <> 0
        ), cuentas AS (
            SELECT CODIGO_MAYOR, TRIM(NUMERO_MAYOR) NUMERO_MAYOR
              FROM CNT.CNT_MAYORES
             WHERE CODIGO_EMPRESA = p_CODIGO_EMPRESA
               AND TRIM(NUMERO_MAYOR) IN ('300', '103')
        ), pares AS (
            SELECT F.*, M.CODIGO_MAYOR, M.NUMERO_MAYOR
              FROM fuentes F CROSS JOIN cuentas M
        ), candidatos AS (
            SELECT P.CODIGO_PUC_COMPROMISO, P.CODIGO_MAYOR,
                   CA.CODIGO_AUXILIAR, CA.SEGMENTO1, CA.SEGMENTO2,
                   CA.DENOMINACION
              FROM pares P
              JOIN CNT.CNT_AUXILIARES CA
                ON CA.CODIGO_MAYOR = P.CODIGO_MAYOR
               AND CA.CODIGO_EMPRESA = p_CODIGO_EMPRESA
             WHERE EXISTS (
                    SELECT 1 FROM CNT.CNT_AUXILIARES_PUC N
                     WHERE N.CODIGO_EMPRESA = p_CODIGO_EMPRESA
                       AND N.CODIGO_PUC = P.CODIGO_PUC
                       AND LTRIM(TRIM(N.TIPO_DOCUMENTO_ID), '0') = '2'
                       AND N.CODIGO_AUXILIAR = CA.CODIGO_AUXILIAR
                   )
                OR (CA.CODIGO_PROVEEDOR = P.CODIGO_PROVEEDOR
                    AND NOT EXISTS (
                        SELECT 1 FROM CNT.CNT_AUXILIARES_PUC N
                        JOIN CNT.CNT_AUXILIARES A
                          ON A.CODIGO_AUXILIAR = N.CODIGO_AUXILIAR
                         AND A.CODIGO_EMPRESA = p_CODIGO_EMPRESA
                         WHERE N.CODIGO_EMPRESA = p_CODIGO_EMPRESA
                           AND A.CODIGO_MAYOR = P.CODIGO_MAYOR
                           AND N.CODIGO_PUC = P.CODIGO_PUC
                           AND LTRIM(TRIM(N.TIPO_DOCUMENTO_ID), '0') = '2'
                    ))
        ), evaluados AS (
            SELECT P.*, C.CODIGO_AUXILIAR, C.SEGMENTO1, C.SEGMENTO2,
                   C.DENOMINACION,
                   COUNT(C.CODIGO_AUXILIAR) OVER (
                       PARTITION BY P.CODIGO_PUC_COMPROMISO, P.CODIGO_MAYOR
                   ) CANT_CANDIDATOS
              FROM pares P LEFT JOIN candidatos C
                ON C.CODIGO_PUC_COMPROMISO = P.CODIGO_PUC_COMPROMISO
               AND C.CODIGO_MAYOR = P.CODIGO_MAYOR
        )
        SELECT CODIGO_COMPROMISO, NUMERO_COMPROMISO, FECHA_COMPROMISO,
               MOTIVO, CODIGO_MAYOR, NUMERO_MAYOR, CODIGO_AUXILIAR,
               SEGMENTO1, SEGMENTO2, DENOMINACION, SUM(MONTO) MONTO,
               MIN(CANT_CANDIDATOS) MIN_CAND, MAX(CANT_CANDIDATOS) MAX_CAND
          FROM evaluados
         GROUP BY CODIGO_COMPROMISO, NUMERO_COMPROMISO, FECHA_COMPROMISO,
                  MOTIVO, CODIGO_MAYOR, NUMERO_MAYOR, CODIGO_AUXILIAR,
                  SEGMENTO1, SEGMENTO2, DENOMINACION
         ORDER BY CODIGO_COMPROMISO, NUMERO_MAYOR DESC, CODIGO_AUXILIAR
    ) LOOP
        IF v_compromiso IS NOT NULL AND v_compromiso <> r.CODIGO_COMPROMISO THEN
            IF v_debe <> v_haber THEN
                DELETE FROM CNT.CNT_AUT_LINE_WRK WHERE RUN_ID = p_RUN_ID;
                p_Message := 'COMP/02: distribucion por auxiliares descuadrada en el compromiso ' || v_referencia || '.';
                RETURN;
            END IF;
            v_debe := 0;
            v_haber := 0;
        END IF;
        v_compromiso := r.CODIGO_COMPROMISO;
        v_referencia := r.NUMERO_COMPROMISO;
        IF r.MIN_CAND <> 1 OR r.MAX_CAND <> 1 THEN
            DELETE FROM CNT.CNT_AUT_LINE_WRK WHERE RUN_ID = p_RUN_ID;
            p_Message := 'COMP/02: falta auxiliar o hay configuracion ambigua en el compromiso '
                         || r.NUMERO_COMPROMISO || ', mayor ' || r.NUMERO_MAYOR || '.';
            RETURN;
        END IF;
        IF ROUND(NVL(r.MONTO, 0), 2) <> 0 THEN
            v_secuencia := v_secuencia + 1;
            v_monto := CASE WHEN r.NUMERO_MAYOR = '300' THEN -ABS(ROUND(r.MONTO, 2)) ELSE ABS(ROUND(r.MONTO, 2)) END;
            INSERT INTO CNT.CNT_AUT_LINE_WRK (
                RUN_ID, SECUENCIA, CODIGO_MAYOR, MAYOR, CODIGO_AUXILIAR,
                AUXILIAR, REFERENCIA1, REFERENCIA2, REFERENCIA3, DESCRIPCION, MONTO
            ) VALUES (
                p_RUN_ID, v_secuencia, r.CODIGO_MAYOR, r.NUMERO_MAYOR,
                r.CODIGO_AUXILIAR,
                r.SEGMENTO1 || '-' || r.SEGMENTO2 || ' ' || r.DENOMINACION,
                SUBSTR(r.NUMERO_COMPROMISO, 1, 20),
                TO_CHAR(r.FECHA_COMPROMISO, 'DD/MM/RRRR'), NULL,
                SUBSTRB('(CP) ' || r.MOTIVO, 1, 200), v_monto
            );
            IF v_monto < 0 THEN v_debe := v_debe - v_monto;
            ELSE v_haber := v_haber + v_monto;
            END IF;
        END IF;
    END LOOP;
    IF v_debe <> v_haber THEN
        DELETE FROM CNT.CNT_AUT_LINE_WRK WHERE RUN_ID = p_RUN_ID;
        p_Message := 'COMP/02: distribucion por auxiliares descuadrada en el compromiso ' || v_referencia || '.';
        RETURN;
    END IF;
    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        DELETE FROM CNT.CNT_AUT_LINE_WRK WHERE RUN_ID = p_RUN_ID;
        p_Message := SQLERRM;
END;
/

-- END R58 OBJECT SP_CNT_AUT_BUILD_COMP02

DECLARE
    v_count NUMBER;
BEGIN
    FOR r IN (SELECT LINE,POSITION,TEXT FROM USER_ERRORS
               WHERE NAME='SP_CNT_AUT_BUILD_COMP02' AND TYPE='PROCEDURE' AND ATTRIBUTE='ERROR'
               ORDER BY SEQUENCE) LOOP
        DBMS_OUTPUT.PUT_LINE('LINEA '||r.LINE||':'||r.POSITION||' '||r.TEXT);
    END LOOP;
    SELECT COUNT(*) INTO v_count FROM USER_OBJECTS
     WHERE OBJECT_NAME='SP_CNT_AUT_BUILD_COMP02' AND OBJECT_TYPE='PROCEDURE' AND STATUS='VALID';
    IF v_count<>1 THEN RAISE_APPLICATION_ERROR(-20058,'R58: SP_CNT_AUT_BUILD_COMP02 INVALID. Usar respaldo generado.'); END IF;
END;
/

-- BEGIN R58 OBJECT SP_CNT_AUT_BUILD_COMP

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_AUT_BUILD_COMP (
    p_RUN_ID              IN VARCHAR2,
    p_ORIGEN_ID           IN NUMBER,
    p_FECHA_DESDE         IN DATE,
    p_FECHA_HASTA         IN DATE,
    p_CODIGO_PRESUPUESTO  IN NUMBER,
    p_CODIGO_EMPRESA      IN NUMBER,
    p_Message             OUT VARCHAR2,
    p_TIPO_COMPROBANTE_ID IN NUMBER DEFAULT NULL
) AS
    v_tipo_codigo VARCHAR2(10);
BEGIN
    -- Keep the seven existing positional arguments; new callers pass the type by name.
    IF p_TIPO_COMPROBANTE_ID IS NULL THEN
        p_Message := 'El tipo de comprobante es requerido por el constructor automatico.';
        RETURN;
    END IF;
    BEGIN
        SELECT TRIM(CODIGO) INTO v_tipo_codigo
          FROM CNT.CNT_DESCRIPTIVAS
         WHERE DESCRIPCION_ID = p_TIPO_COMPROBANTE_ID
           AND TITULO_ID = 5
           AND (CODIGO_EMPRESA = p_CODIGO_EMPRESA OR CODIGO_EMPRESA IS NULL);
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            p_Message := 'El tipo de comprobante no existe para la empresa configurada.';
            RETURN;
    END;
    IF v_tipo_codigo IS NULL OR NOT REGEXP_LIKE(v_tipo_codigo, '^[0-9]+$') THEN
        p_Message := 'El tipo de comprobante no tiene un codigo numerico valido.';
        RETURN;
    END IF;
    IF p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('COMP', 'ODC')
       AND LTRIM(v_tipo_codigo, '0') = '2' THEN
        CNT.SP_CNT_AUT_BUILD_COMP02(
            p_RUN_ID, p_FECHA_DESDE, p_FECHA_HASTA,
            p_CODIGO_PRESUPUESTO, p_CODIGO_EMPRESA, p_Message
        );
        RETURN;
    END IF;
    DELETE FROM CNT.CNT_AUT_LINE_WRK
     WHERE RUN_ID = p_RUN_ID
        OR FECHA_REGISTRO < SYSDATE - 1;

    INSERT INTO CNT.CNT_AUT_LINE_WRK (
        RUN_ID,
        SECUENCIA,
        CODIGO_MAYOR,
        MAYOR,
        CODIGO_AUXILIAR,
        AUXILIAR,
        REFERENCIA1,
        REFERENCIA2,
        REFERENCIA3,
        DESCRIPCION,
        MONTO
    )
    SELECT p_RUN_ID,
           ROWNUM,
           q.CODIGO_MAYOR,
           q.NUMERO_MAYOR,
           q.CODIGO_AUXILIAR,
           q.SEGMENTO1 || '-' || q.SEGMENTO2 || ' ' || q.DENOMINACION,
           SUBSTR(q.NUMERO_COMPROMISO, 1, 20),
           TO_CHAR(q.FECHA_COMPROMISO, 'DD/MM/RRRR'),
           NULL,
           SUBSTRB('(' || q.TIPO_COMPROMISO || ') ' || q.MOTIVO, 1, 200),
           CASE
               WHEN p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('COMP', 'ODC')
                    THEN CASE WHEN q.COLUMNA_BALANCE = 'H' THEN -ABS(NVL(q.MONTO_COMPROMISO, 0)) ELSE ABS(NVL(q.MONTO_COMPROMISO, 0)) END
               ELSE CASE WHEN q.COLUMNA_BALANCE = 'D' THEN -ABS(NVL(q.MONTO_COMPROMISO, 0)) ELSE ABS(NVL(q.MONTO_COMPROMISO, 0)) END
           END
      FROM (
            SELECT CM.COLUMNA_BALANCE,
                   AC.CODIGO_COMPROMISO,
                   AC.NUMERO_COMPROMISO,
                   AC.FECHA_COMPROMISO,
                   AP.NOMBRE_PROVEEDOR,
                   SUM(APC.MONTO) MONTO_COMPROMISO,
                   CASE WHEN CM.COLUMNA_BALANCE = 'H' THEN NVL(CA.CODIGO_MAYOR, 9) ELSE NVL(CA.CODIGO_MAYOR, 3) END CODIGO_MAYOR,
                   CA.CODIGO_AUXILIAR,
                   CASE WHEN CM.COLUMNA_BALANCE = 'H' THEN NVL(CM.NUMERO_MAYOR, '300') ELSE NVL(CM.NUMERO_MAYOR, '103') END NUMERO_MAYOR,
                   NVL(CA.SEGMENTO1, '001') SEGMENTO1,
                   NVL(CA.SEGMENTO2, '000') SEGMENTO2,
                   NVL(CA.DENOMINACION, '999') DENOMINACION,
                   RTRIM(AC.MOTIVO) MOTIVO,
                   'CP' TIPO_COMPROMISO
              FROM PRE.PRE_COMPROMISOS AC,
                   PRE.PRE_DETALLE_COMPROMISOS PDC,
                   PRE.PRE_PUC_COMPROMISOS APC,
                   ADM.ADM_PROVEEDORES AP,
                   PRE.PRE_V_SALDOS PVS,
                   CNT.CNT_MAYORES CM,
                   CNT.CNT_AUXILIARES CA
             WHERE AC.CODIGO_PRESUPUESTO = p_CODIGO_PRESUPUESTO
               AND AC.CODIGO_EMPRESA = p_CODIGO_EMPRESA
               AND AC.CODIGO_COMPROMISO = PDC.CODIGO_COMPROMISO
               AND PDC.CODIGO_DETALLE_COMPROMISO = APC.CODIGO_DETALLE_COMPROMISO
               AND AP.CODIGO_PROVEEDOR = AC.CODIGO_PROVEEDOR
               AND PVS.CODIGO_SALDO = APC.CODIGO_SALDO
               AND CM.COLUMNA_BALANCE IN ('D', 'H')
               AND CM.CODIGO_MAYOR = CA.CODIGO_MAYOR
               AND CA.CODIGO_PROVEEDOR = AC.CODIGO_PROVEEDOR
               AND AC.CODIGO_PROVEEDOR NOT IN (8127, 8128)
               AND (
                    (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('COMP', 'ODC')
                     AND AC.STATUS <> 'AN'
                     AND AC.FECHA_COMPROMISO >= p_FECHA_DESDE
                     AND AC.FECHA_COMPROMISO < p_FECHA_HASTA + 1)
                    OR
                    (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ANCOMP', 'ODC')
                     AND AC.STATUS = 'AN'
                     AND TRUNC(AC.FECHA_UPD) >= p_FECHA_DESDE
                     AND TRUNC(AC.FECHA_UPD) <= p_FECHA_HASTA)
               )
             GROUP BY CM.COLUMNA_BALANCE,
                      AC.CODIGO_COMPROMISO,
                      AC.NUMERO_COMPROMISO,
                      AC.FECHA_COMPROMISO,
                      AP.NOMBRE_PROVEEDOR,
                      CASE WHEN CM.COLUMNA_BALANCE = 'H' THEN NVL(CA.CODIGO_MAYOR, 9) ELSE NVL(CA.CODIGO_MAYOR, 3) END,
                      CA.CODIGO_AUXILIAR,
                      CASE WHEN CM.COLUMNA_BALANCE = 'H' THEN NVL(CM.NUMERO_MAYOR, '300') ELSE NVL(CM.NUMERO_MAYOR, '103') END,
                      NVL(CA.SEGMENTO1, '001'),
                      NVL(CA.SEGMENTO2, '000'),
                      NVL(CA.DENOMINACION, '999'),
                      RTRIM(AC.MOTIVO)
        ) q
     WHERE NVL(q.MONTO_COMPROMISO, 0) <> 0
     ORDER BY q.TIPO_COMPROMISO, q.CODIGO_COMPROMISO, q.COLUMNA_BALANCE DESC;

    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        DELETE FROM CNT.CNT_AUT_LINE_WRK
         WHERE RUN_ID = p_RUN_ID;
        p_Message := SQLERRM;
END;
/

-- END R58 OBJECT SP_CNT_AUT_BUILD_COMP

DECLARE
    v_count NUMBER;
BEGIN
    FOR r IN (SELECT LINE,POSITION,TEXT FROM USER_ERRORS
               WHERE NAME='SP_CNT_AUT_BUILD_COMP' AND TYPE='PROCEDURE' AND ATTRIBUTE='ERROR'
               ORDER BY SEQUENCE) LOOP
        DBMS_OUTPUT.PUT_LINE('LINEA '||r.LINE||':'||r.POSITION||' '||r.TEXT);
    END LOOP;
    SELECT COUNT(*) INTO v_count FROM USER_OBJECTS
     WHERE OBJECT_NAME='SP_CNT_AUT_BUILD_COMP' AND OBJECT_TYPE='PROCEDURE' AND STATUS='VALID';
    IF v_count<>1 THEN RAISE_APPLICATION_ERROR(-20058,'R58: SP_CNT_AUT_BUILD_COMP INVALID. Usar respaldo generado.'); END IF;
END;
/

-- BEGIN R58 OBJECT SP_CNT_AUT_BUILD_OP

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_AUT_BUILD_OP (
    p_RUN_ID              IN VARCHAR2,
    p_ORIGEN_ID           IN NUMBER,
    p_FECHA_DESDE         IN DATE,
    p_FECHA_HASTA         IN DATE,
    p_CODIGO_PRESUPUESTO  IN NUMBER,
    p_CODIGO_EMPRESA      IN NUMBER,
    p_Message             OUT VARCHAR2,
    p_TIPO_COMPROBANTE_ID IN NUMBER DEFAULT NULL
) AS
    v_tipo_codigo VARCHAR2(10);
BEGIN
    -- Keep the seven existing positional arguments; new callers pass the type by name.
    IF p_TIPO_COMPROBANTE_ID IS NULL THEN
        p_Message := 'El tipo de comprobante es requerido por el constructor automatico.';
        RETURN;
    END IF;
    BEGIN
        SELECT TRIM(CODIGO) INTO v_tipo_codigo
          FROM CNT.CNT_DESCRIPTIVAS
         WHERE DESCRIPCION_ID = p_TIPO_COMPROBANTE_ID
           AND TITULO_ID = 5
           AND (CODIGO_EMPRESA = p_CODIGO_EMPRESA OR CODIGO_EMPRESA IS NULL);
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            p_Message := 'El tipo de comprobante no existe para la empresa configurada.';
            RETURN;
    END;
    IF v_tipo_codigo IS NULL OR NOT REGEXP_LIKE(v_tipo_codigo, '^[0-9]+$') THEN
        p_Message := 'El tipo de comprobante no tiene un codigo numerico valido.';
        RETURN;
    END IF;
    DELETE FROM CNT.CNT_AUT_LINE_WRK
     WHERE RUN_ID = p_RUN_ID
        OR FECHA_REGISTRO < SYSDATE - 1;

    INSERT INTO CNT.CNT_AUT_LINE_WRK (
        RUN_ID,
        SECUENCIA,
        CODIGO_MAYOR,
        MAYOR,
        CODIGO_AUXILIAR,
        AUXILIAR,
        REFERENCIA1,
        REFERENCIA2,
        REFERENCIA3,
        DESCRIPCION,
        MONTO
    )
    SELECT p_RUN_ID,
           ROWNUM,
           q.CODIGO_MAYOR,
           q.NUMERO_MAYOR,
           q.CODIGO_AUXILIAR,
           q.SEGMENTO1 || '-' || q.SEGMENTO2 || ' ' || q.DENOMINACION,
           SUBSTR(q.NUMERO_ORDEN_PAGO, 1, 20),
           TO_CHAR(q.FECHA_ORDEN_PAGO, 'DD/MM/RRRR'),
           NULL,
           SUBSTRB('(' || q.TIPO_COMPROMISO || ') ' || q.MOTIVO, 1, 200),
           CASE WHEN q.COLUMNA_BALANCE = 'D' THEN -ABS(NVL(q.MONTO, 0)) ELSE ABS(NVL(q.MONTO, 0)) END
      FROM (
            SELECT *
              FROM (
                    SELECT 'D' COLUMNA_BALANCE,
                           AC.CODIGO_ORDEN_PAGO,
                           AC.NUMERO_ORDEN_PAGO,
                           AC.FECHA_ORDEN_PAGO,
                           AP.NOMBRE_PROVEEDOR,
                           SUM(APC.MONTO) MONTO,
                           NVL(CA.CODIGO_MAYOR, 3) CODIGO_MAYOR,
                           NVL(CA.CODIGO_AUXILIAR, 1094) CODIGO_AUXILIAR,
                           NVL(CM.NUMERO_MAYOR, '103') NUMERO_MAYOR,
                           NVL(CA.SEGMENTO1, '001') SEGMENTO1,
                           NVL(CA.SEGMENTO2, '000') SEGMENTO2,
                           NVL(CA.DENOMINACION, '999') DENOMINACION,
                           RTRIM(AC.MOTIVO) MOTIVO,
                           CASE WHEN p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ANODPAUT', 'ODC') THEN 'AN' ELSE 'OP' END TIPO_COMPROMISO
                      FROM ADM.ADM_ORDEN_PAGO AC,
                           ADM.ADM_PUC_ORDEN_PAGO APC,
                           ADM.ADM_PROVEEDORES AP,
                           CNT.CNT_MAYORES CM,
                           CNT.CNT_AUXILIARES CA
                     WHERE AC.CODIGO_PRESUPUESTO = p_CODIGO_PRESUPUESTO
                       AND AC.CODIGO_EMPRESA = p_CODIGO_EMPRESA
                       AND APC.CODIGO_ORDEN_PAGO = AC.CODIGO_ORDEN_PAGO
                       AND AP.CODIGO_PROVEEDOR = AC.CODIGO_PROVEEDOR
                       AND CM.CODIGO_MAYOR = 3
                       AND CM.CODIGO_MAYOR = CA.CODIGO_MAYOR
                       AND CA.CODIGO_PROVEEDOR = AC.CODIGO_PROVEEDOR
                       AND CA.CODIGO_AUXILIAR NOT IN (
                            SELECT DISTINCT NUE.CODIGO_AUXILIAR
                              FROM CNT.CNT_AUXILIARES_PUC NUE,
                                   ADM.ADM_PUC_ORDEN_PAGO APC2
                             WHERE APC2.CODIGO_PUC = NUE.CODIGO_PUC
                               AND APC2.CODIGO_ORDEN_PAGO = AC.CODIGO_ORDEN_PAGO
                       )
                       AND NVL(ADM.ADM_F_DESCRIPTIVAS_ID('CODIGO', AC.TIPO_ORDEN_PAGO_ID, AC.CODIGO_EMPRESA), ' ') <> 'A'
                       AND EXISTS (
                            SELECT 1
                              FROM ADM.ADM_COMPROMISO_OP COMOP
                             WHERE COMOP.CODIGO_ORDEN_PAGO = AC.CODIGO_ORDEN_PAGO
                               AND COMOP.CODIGO_EMPRESA = AC.CODIGO_EMPRESA
                               AND NVL(ADM.ADM_F_DESCRIPTIVAS_ID('CODIGO', COMOP.ORIGEN_COMPROMISO_ID, COMOP.CODIGO_EMPRESA), ' ') <> 'CON'
                       )
                       AND (
                            (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ODPAUT', 'ODC')
                             AND AC.STATUS <> 'AN'
                             AND AC.FECHA_ORDEN_PAGO >= p_FECHA_DESDE
                             AND AC.FECHA_ORDEN_PAGO < p_FECHA_HASTA + 1)
                            OR
                            (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ANODPAUT', 'ODC')
                             AND AC.STATUS = 'AN'
                             AND TRUNC(AC.FECHA_UPD) >= p_FECHA_DESDE
                             AND TRUNC(AC.FECHA_UPD) <= p_FECHA_HASTA)
                       )
                     GROUP BY AC.CODIGO_ORDEN_PAGO,
                              AC.NUMERO_ORDEN_PAGO,
                              AC.FECHA_ORDEN_PAGO,
                              AP.NOMBRE_PROVEEDOR,
                              NVL(CA.CODIGO_MAYOR, 3),
                              NVL(CA.CODIGO_AUXILIAR, 1094),
                              NVL(CM.NUMERO_MAYOR, '103'),
                              NVL(CA.SEGMENTO1, '001'),
                              NVL(CA.SEGMENTO2, '000'),
                              NVL(CA.DENOMINACION, '999'),
                              RTRIM(AC.MOTIVO)
                    UNION ALL
                    SELECT 'H' COLUMNA_BALANCE,
                           AC.CODIGO_ORDEN_PAGO,
                           AC.NUMERO_ORDEN_PAGO,
                           AC.FECHA_ORDEN_PAGO,
                           AP.NOMBRE_PROVEEDOR,
                           SUM(APC.MONTO) MONTO,
                           NVL(CA.CODIGO_MAYOR, 1) CODIGO_MAYOR,
                           NVL(CA.CODIGO_AUXILIAR, 1094) CODIGO_AUXILIAR,
                           NVL(CM.NUMERO_MAYOR, '101') NUMERO_MAYOR,
                           NVL(CA.SEGMENTO1, '001') SEGMENTO1,
                           NVL(CA.SEGMENTO2, '000') SEGMENTO2,
                           NVL(CA.DENOMINACION, '999') DENOMINACION,
                           RTRIM(AC.MOTIVO) MOTIVO,
                           CASE WHEN p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ANODPAUT', 'ODC') THEN 'AN' ELSE 'OP' END TIPO_COMPROMISO
                      FROM ADM.ADM_ORDEN_PAGO AC,
                           ADM.ADM_PUC_ORDEN_PAGO APC,
                           ADM.ADM_PROVEEDORES AP,
                           CNT.CNT_MAYORES CM,
                           CNT.CNT_AUXILIARES CA
                     WHERE AC.CODIGO_PRESUPUESTO = p_CODIGO_PRESUPUESTO
                       AND AC.CODIGO_EMPRESA = p_CODIGO_EMPRESA
                       AND APC.CODIGO_ORDEN_PAGO = AC.CODIGO_ORDEN_PAGO
                       AND AP.CODIGO_PROVEEDOR = AC.CODIGO_PROVEEDOR
                       AND CM.CODIGO_MAYOR = 1
                       AND CM.CODIGO_MAYOR = CA.CODIGO_MAYOR
                       AND CA.CODIGO_PROVEEDOR = AC.CODIGO_PROVEEDOR
                       AND CA.CODIGO_AUXILIAR NOT IN (
                            SELECT DISTINCT NUE.CODIGO_AUXILIAR
                              FROM CNT.CNT_AUXILIARES_PUC NUE,
                                   ADM.ADM_PUC_ORDEN_PAGO APC2
                             WHERE APC2.CODIGO_PUC = NUE.CODIGO_PUC
                               AND APC2.CODIGO_ORDEN_PAGO = AC.CODIGO_ORDEN_PAGO
                       )
                       AND NVL(ADM.ADM_F_DESCRIPTIVAS_ID('CODIGO', AC.TIPO_ORDEN_PAGO_ID, AC.CODIGO_EMPRESA), ' ') <> 'A'
                       AND EXISTS (
                            SELECT 1
                              FROM ADM.ADM_COMPROMISO_OP COMOP
                             WHERE COMOP.CODIGO_ORDEN_PAGO = AC.CODIGO_ORDEN_PAGO
                               AND COMOP.CODIGO_EMPRESA = AC.CODIGO_EMPRESA
                               AND NVL(ADM.ADM_F_DESCRIPTIVAS_ID('CODIGO', COMOP.ORIGEN_COMPROMISO_ID, COMOP.CODIGO_EMPRESA), ' ') <> 'CON'
                       )
                       AND (
                            (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ODPAUT', 'ODC')
                             AND AC.STATUS <> 'AN'
                             AND AC.FECHA_ORDEN_PAGO >= p_FECHA_DESDE
                             AND AC.FECHA_ORDEN_PAGO < p_FECHA_HASTA + 1)
                            OR
                            (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ANODPAUT', 'ODC')
                             AND AC.STATUS = 'AN'
                             AND TRUNC(AC.FECHA_UPD) >= p_FECHA_DESDE
                             AND TRUNC(AC.FECHA_UPD) <= p_FECHA_HASTA)
                       )
                     GROUP BY AC.CODIGO_ORDEN_PAGO,
                              AC.NUMERO_ORDEN_PAGO,
                              AC.FECHA_ORDEN_PAGO,
                              AP.NOMBRE_PROVEEDOR,
                              NVL(CA.CODIGO_MAYOR, 1),
                              NVL(CA.CODIGO_AUXILIAR, 1094),
                              NVL(CM.NUMERO_MAYOR, '101'),
                              NVL(CA.SEGMENTO1, '001'),
                              NVL(CA.SEGMENTO2, '000'),
                              NVL(CA.DENOMINACION, '999'),
                              RTRIM(AC.MOTIVO)
                    UNION ALL
                    SELECT 'D' COLUMNA_BALANCE,
                           AC.CODIGO_ORDEN_PAGO,
                           AC.NUMERO_ORDEN_PAGO,
                           AC.FECHA_ORDEN_PAGO,
                           ADM.ADM_F_PROVEEDOR_ID('NOMBRE_PROVEEDOR', AC.CODIGO_PROVEEDOR) NOMBRE_PROVEEDOR,
                           SUM(APC.MONTO) MONTO,
                           NVL(CA.CODIGO_MAYOR, 3) CODIGO_MAYOR,
                           NVL(CA.CODIGO_AUXILIAR, 1094) CODIGO_AUXILIAR,
                           NVL(CM.NUMERO_MAYOR, '103') NUMERO_MAYOR,
                           NVL(CA.SEGMENTO1, '001') SEGMENTO1,
                           NVL(CA.SEGMENTO2, '000') SEGMENTO2,
                           NVL(CA.DENOMINACION, '999') DENOMINACION,
                           RTRIM(AC.MOTIVO) MOTIVO,
                           CASE WHEN p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ANODPAUT', 'ODC') THEN 'AN' ELSE 'OP' END TIPO_COMPROMISO
                      FROM ADM.ADM_ORDEN_PAGO AC,
                           ADM.ADM_PUC_ORDEN_PAGO APC,
                           CNT.CNT_MAYORES CM,
                           CNT.CNT_AUXILIARES CA
                     WHERE AC.CODIGO_PRESUPUESTO = p_CODIGO_PRESUPUESTO
                       AND AC.CODIGO_EMPRESA = p_CODIGO_EMPRESA
                       AND APC.CODIGO_ORDEN_PAGO = AC.CODIGO_ORDEN_PAGO
                       AND CM.CODIGO_MAYOR = 3
                       AND CM.CODIGO_MAYOR = CA.CODIGO_MAYOR
                       AND CA.CODIGO_AUXILIAR IN (
                            SELECT DISTINCT NUE.CODIGO_AUXILIAR
                              FROM CNT.CNT_AUXILIARES_PUC NUE,
                                   ADM.ADM_PUC_ORDEN_PAGO APC2
                             WHERE APC2.CODIGO_PUC = NUE.CODIGO_PUC
                               AND APC2.CODIGO_ORDEN_PAGO = AC.CODIGO_ORDEN_PAGO
                       )
                       AND NVL(ADM.ADM_F_DESCRIPTIVAS_ID('CODIGO', AC.TIPO_ORDEN_PAGO_ID, AC.CODIGO_EMPRESA), ' ') <> 'A'
                       AND (
                            (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ODPAUT', 'ODC') AND AC.STATUS <> 'AN' AND AC.FECHA_ORDEN_PAGO >= p_FECHA_DESDE AND AC.FECHA_ORDEN_PAGO < p_FECHA_HASTA + 1)
                            OR
                            (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ANODPAUT', 'ODC') AND AC.STATUS = 'AN' AND TRUNC(AC.FECHA_UPD) >= p_FECHA_DESDE AND TRUNC(AC.FECHA_UPD) <= p_FECHA_HASTA)
                       )
                     GROUP BY AC.CODIGO_ORDEN_PAGO, AC.NUMERO_ORDEN_PAGO, AC.FECHA_ORDEN_PAGO, AC.CODIGO_PROVEEDOR,
                              NVL(CA.CODIGO_MAYOR, 3), NVL(CA.CODIGO_AUXILIAR, 1094), NVL(CM.NUMERO_MAYOR, '103'),
                              NVL(CA.SEGMENTO1, '001'), NVL(CA.SEGMENTO2, '000'), NVL(CA.DENOMINACION, '999'), RTRIM(AC.MOTIVO)
                    UNION ALL
                    SELECT 'H' COLUMNA_BALANCE,
                           AC.CODIGO_ORDEN_PAGO,
                           AC.NUMERO_ORDEN_PAGO,
                           AC.FECHA_ORDEN_PAGO,
                           ADM.ADM_F_PROVEEDOR_ID('NOMBRE_PROVEEDOR', AC.CODIGO_PROVEEDOR) NOMBRE_PROVEEDOR,
                           SUM(APC.MONTO) MONTO,
                           NVL(CA.CODIGO_MAYOR, 1) CODIGO_MAYOR,
                           NVL(CA.CODIGO_AUXILIAR, 1094) CODIGO_AUXILIAR,
                           NVL(CM.NUMERO_MAYOR, '101') NUMERO_MAYOR,
                           NVL(CA.SEGMENTO1, '001') SEGMENTO1,
                           NVL(CA.SEGMENTO2, '000') SEGMENTO2,
                           NVL(CA.DENOMINACION, '999') DENOMINACION,
                           RTRIM(AC.MOTIVO) MOTIVO,
                           CASE WHEN p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ANODPAUT', 'ODC') THEN 'AN' ELSE 'OP' END TIPO_COMPROMISO
                      FROM ADM.ADM_ORDEN_PAGO AC,
                           ADM.ADM_PUC_ORDEN_PAGO APC,
                           CNT.CNT_MAYORES CM,
                           CNT.CNT_AUXILIARES CA
                     WHERE AC.CODIGO_PRESUPUESTO = p_CODIGO_PRESUPUESTO
                       AND AC.CODIGO_EMPRESA = p_CODIGO_EMPRESA
                       AND APC.CODIGO_ORDEN_PAGO = AC.CODIGO_ORDEN_PAGO
                       AND CM.CODIGO_MAYOR = 1
                       AND CM.CODIGO_MAYOR = CA.CODIGO_MAYOR
                       AND CA.CODIGO_AUXILIAR IN (
                            SELECT DISTINCT NUE.CODIGO_AUXILIAR
                              FROM CNT.CNT_AUXILIARES_PUC NUE,
                                   ADM.ADM_PUC_ORDEN_PAGO APC2
                             WHERE APC2.CODIGO_PUC = NUE.CODIGO_PUC
                               AND APC2.CODIGO_ORDEN_PAGO = AC.CODIGO_ORDEN_PAGO
                       )
                       AND NVL(ADM.ADM_F_DESCRIPTIVAS_ID('CODIGO', AC.TIPO_ORDEN_PAGO_ID, AC.CODIGO_EMPRESA), ' ') <> 'A'
                       AND (
                            (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ODPAUT', 'ODC') AND AC.STATUS <> 'AN' AND AC.FECHA_ORDEN_PAGO >= p_FECHA_DESDE AND AC.FECHA_ORDEN_PAGO < p_FECHA_HASTA + 1)
                            OR
                            (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ANODPAUT', 'ODC') AND AC.STATUS = 'AN' AND TRUNC(AC.FECHA_UPD) >= p_FECHA_DESDE AND TRUNC(AC.FECHA_UPD) <= p_FECHA_HASTA)
                       )
                     GROUP BY AC.CODIGO_ORDEN_PAGO, AC.NUMERO_ORDEN_PAGO, AC.FECHA_ORDEN_PAGO, AC.CODIGO_PROVEEDOR,
                              NVL(CA.CODIGO_MAYOR, 1), NVL(CA.CODIGO_AUXILIAR, 1094), NVL(CM.NUMERO_MAYOR, '101'),
                              NVL(CA.SEGMENTO1, '001'), NVL(CA.SEGMENTO2, '000'), NVL(CA.DENOMINACION, '999'), RTRIM(AC.MOTIVO)
                    UNION ALL
                    SELECT 'D' COLUMNA_BALANCE,
                           AC.CODIGO_ORDEN_PAGO,
                           AC.NUMERO_ORDEN_PAGO,
                           AC.FECHA_ORDEN_PAGO,
                           AP.NOMBRE_PROVEEDOR || ' (CAJA CHICA)' NOMBRE_PROVEEDOR,
                           SUM(APC.MONTO) MONTO,
                           NVL(CA.CODIGO_MAYOR, 11) CODIGO_MAYOR,
                           NVL(CA.CODIGO_AUXILIAR, 1094) CODIGO_AUXILIAR,
                           NVL(CM.NUMERO_MAYOR, '126') NUMERO_MAYOR,
                           NVL(CA.SEGMENTO1, '001') SEGMENTO1,
                           NVL(CA.SEGMENTO2, '000') SEGMENTO2,
                           NVL(CA.DENOMINACION, '999') DENOMINACION,
                           RTRIM(AC.MOTIVO) MOTIVO,
                           CASE WHEN p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ANODPAUT', 'ODC') THEN 'AN' ELSE 'OP' END TIPO_COMPROMISO
                      FROM ADM.ADM_ORDEN_PAGO AC,
                           ADM.ADM_BENEFICIARIOS_OP APC,
                           ADM.ADM_PROVEEDORES AP,
                           CNT.CNT_MAYORES CM,
                           CNT.CNT_AUXILIARES CA
                     WHERE AC.CODIGO_PRESUPUESTO = p_CODIGO_PRESUPUESTO
                       AND AC.CODIGO_EMPRESA = p_CODIGO_EMPRESA
                       AND APC.CODIGO_ORDEN_PAGO = AC.CODIGO_ORDEN_PAGO
                       AND AP.CODIGO_PROVEEDOR = AC.CODIGO_PROVEEDOR
                       AND CM.CODIGO_MAYOR = 11
                       AND CM.CODIGO_MAYOR = CA.CODIGO_MAYOR
                       AND CA.CODIGO_PROVEEDOR = AC.CODIGO_PROVEEDOR
                       AND ADM.ADM_F_DESCRIPTIVAS_ID('CODIGO', AC.TIPO_ORDEN_PAGO_ID, AC.CODIGO_EMPRESA) = 'A'
                       AND (
                            (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ODPAUT', 'ODC') AND AC.STATUS <> 'AN' AND AC.FECHA_ORDEN_PAGO >= p_FECHA_DESDE AND AC.FECHA_ORDEN_PAGO < p_FECHA_HASTA + 1)
                            OR
                            (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ANODPAUT', 'ODC') AND AC.STATUS = 'AN' AND TRUNC(AC.FECHA_UPD) >= p_FECHA_DESDE AND TRUNC(AC.FECHA_UPD) <= p_FECHA_HASTA)
                       )
                     GROUP BY AC.CODIGO_ORDEN_PAGO, AC.NUMERO_ORDEN_PAGO, AC.FECHA_ORDEN_PAGO, AP.NOMBRE_PROVEEDOR,
                              NVL(CA.CODIGO_MAYOR, 11), NVL(CA.CODIGO_AUXILIAR, 1094), NVL(CM.NUMERO_MAYOR, '126'),
                              NVL(CA.SEGMENTO1, '001'), NVL(CA.SEGMENTO2, '000'), NVL(CA.DENOMINACION, '999'), RTRIM(AC.MOTIVO)
                    UNION ALL
                    SELECT 'H' COLUMNA_BALANCE,
                           AC.CODIGO_ORDEN_PAGO,
                           AC.NUMERO_ORDEN_PAGO,
                           AC.FECHA_ORDEN_PAGO,
                           AP.NOMBRE_PROVEEDOR || ' (CAJA CHICA)' NOMBRE_PROVEEDOR,
                           SUM(APC.MONTO) MONTO,
                           NVL(CA.CODIGO_MAYOR, 1) CODIGO_MAYOR,
                           NVL(CA.CODIGO_AUXILIAR, 1094) CODIGO_AUXILIAR,
                           NVL(CM.NUMERO_MAYOR, '101') NUMERO_MAYOR,
                           NVL(CA.SEGMENTO1, '001') SEGMENTO1,
                           NVL(CA.SEGMENTO2, '000') SEGMENTO2,
                           NVL(CA.DENOMINACION, '999') DENOMINACION,
                           RTRIM(AC.MOTIVO) MOTIVO,
                           CASE WHEN p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ANODPAUT', 'ODC') THEN 'AN' ELSE 'OP' END TIPO_COMPROMISO
                      FROM ADM.ADM_ORDEN_PAGO AC,
                           ADM.ADM_BENEFICIARIOS_OP APC,
                           ADM.ADM_PROVEEDORES AP,
                           CNT.CNT_MAYORES CM,
                           CNT.CNT_AUXILIARES CA
                     WHERE AC.CODIGO_PRESUPUESTO = p_CODIGO_PRESUPUESTO
                       AND AC.CODIGO_EMPRESA = p_CODIGO_EMPRESA
                       AND APC.CODIGO_ORDEN_PAGO = AC.CODIGO_ORDEN_PAGO
                       AND AP.CODIGO_PROVEEDOR = AC.CODIGO_PROVEEDOR
                       AND CM.CODIGO_MAYOR = 1
                       AND CM.CODIGO_MAYOR = CA.CODIGO_MAYOR
                       AND CA.CODIGO_PROVEEDOR = AC.CODIGO_PROVEEDOR
                       AND ADM.ADM_F_DESCRIPTIVAS_ID('CODIGO', AC.TIPO_ORDEN_PAGO_ID, AC.CODIGO_EMPRESA) = 'A'
                       AND (
                            (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ODPAUT', 'ODC') AND AC.STATUS <> 'AN' AND AC.FECHA_ORDEN_PAGO >= p_FECHA_DESDE AND AC.FECHA_ORDEN_PAGO < p_FECHA_HASTA + 1)
                            OR
                            (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ANODPAUT', 'ODC') AND AC.STATUS = 'AN' AND TRUNC(AC.FECHA_UPD) >= p_FECHA_DESDE AND TRUNC(AC.FECHA_UPD) <= p_FECHA_HASTA)
                       )
                     GROUP BY AC.CODIGO_ORDEN_PAGO, AC.NUMERO_ORDEN_PAGO, AC.FECHA_ORDEN_PAGO, AP.NOMBRE_PROVEEDOR,
                              NVL(CA.CODIGO_MAYOR, 1), NVL(CA.CODIGO_AUXILIAR, 1094), NVL(CM.NUMERO_MAYOR, '101'),
                              NVL(CA.SEGMENTO1, '001'), NVL(CA.SEGMENTO2, '000'), NVL(CA.DENOMINACION, '999'), RTRIM(AC.MOTIVO)
                    UNION ALL
                    SELECT 'D' COLUMNA_BALANCE,
                           AC.CODIGO_ORDEN_PAGO,
                           AC.NUMERO_ORDEN_PAGO,
                           AC.FECHA_ORDEN_PAGO,
                           AP.NOMBRE_PROVEEDOR,
                           SUM(APC.MONTO) MONTO,
                           NVL(CA.CODIGO_MAYOR, 12) CODIGO_MAYOR,
                           NVL(CA.CODIGO_AUXILIAR, 1094) CODIGO_AUXILIAR,
                           NVL(CM.NUMERO_MAYOR, '127') NUMERO_MAYOR,
                           NVL(CA.SEGMENTO1, '001') SEGMENTO1,
                           NVL(CA.SEGMENTO2, '000') SEGMENTO2,
                           NVL(CA.DENOMINACION, '999') DENOMINACION,
                           RTRIM(AC.MOTIVO) MOTIVO,
                           CASE WHEN p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ANODPAUT', 'ODC') THEN 'AN' ELSE 'OP' END TIPO_COMPROMISO
                      FROM ADM.ADM_ORDEN_PAGO AC,
                           ADM.ADM_PUC_ORDEN_PAGO APC,
                           ADM.ADM_PROVEEDORES AP,
                           CNT.CNT_MAYORES CM,
                           CNT.CNT_AUXILIARES CA
                     WHERE AC.CODIGO_PRESUPUESTO = p_CODIGO_PRESUPUESTO
                       AND AC.CODIGO_EMPRESA = p_CODIGO_EMPRESA
                       AND APC.CODIGO_ORDEN_PAGO = AC.CODIGO_ORDEN_PAGO
                       AND AP.CODIGO_PROVEEDOR = AC.CODIGO_PROVEEDOR
                       AND CM.CODIGO_MAYOR = 12
                       AND CM.CODIGO_MAYOR = CA.CODIGO_MAYOR
                       AND CA.CODIGO_PROVEEDOR = AC.CODIGO_PROVEEDOR
                       AND EXISTS (
                            SELECT 1
                              FROM ADM.ADM_COMPROMISO_OP COMOP
                             WHERE COMOP.CODIGO_ORDEN_PAGO = AC.CODIGO_ORDEN_PAGO
                               AND COMOP.CODIGO_EMPRESA = AC.CODIGO_EMPRESA
                               AND ADM.ADM_F_DESCRIPTIVAS_ID('CODIGO', COMOP.ORIGEN_COMPROMISO_ID, COMOP.CODIGO_EMPRESA) = 'CON'
                       )
                       AND (
                            (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ODPAUT', 'ODC') AND AC.STATUS <> 'AN' AND AC.FECHA_ORDEN_PAGO >= p_FECHA_DESDE AND AC.FECHA_ORDEN_PAGO < p_FECHA_HASTA + 1)
                            OR
                            (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ANODPAUT', 'ODC') AND AC.STATUS = 'AN' AND TRUNC(AC.FECHA_UPD) >= p_FECHA_DESDE AND TRUNC(AC.FECHA_UPD) <= p_FECHA_HASTA)
                       )
                     GROUP BY AC.CODIGO_ORDEN_PAGO, AC.NUMERO_ORDEN_PAGO, AC.FECHA_ORDEN_PAGO, AP.NOMBRE_PROVEEDOR,
                              NVL(CA.CODIGO_MAYOR, 12), NVL(CA.CODIGO_AUXILIAR, 1094), NVL(CM.NUMERO_MAYOR, '127'),
                              NVL(CA.SEGMENTO1, '001'), NVL(CA.SEGMENTO2, '000'), NVL(CA.DENOMINACION, '999'), RTRIM(AC.MOTIVO)
                    UNION ALL
                    SELECT 'H' COLUMNA_BALANCE,
                           AC.CODIGO_ORDEN_PAGO,
                           AC.NUMERO_ORDEN_PAGO,
                           AC.FECHA_ORDEN_PAGO,
                           AP.NOMBRE_PROVEEDOR,
                           SUM(APC.MONTO) MONTO,
                           NVL(CA.CODIGO_MAYOR, 1) CODIGO_MAYOR,
                           NVL(CA.CODIGO_AUXILIAR, 1094) CODIGO_AUXILIAR,
                           NVL(CM.NUMERO_MAYOR, '101') NUMERO_MAYOR,
                           NVL(CA.SEGMENTO1, '001') SEGMENTO1,
                           NVL(CA.SEGMENTO2, '000') SEGMENTO2,
                           NVL(CA.DENOMINACION, '999') DENOMINACION,
                           RTRIM(AC.MOTIVO) MOTIVO,
                           CASE WHEN p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ANODPAUT', 'ODC') THEN 'AN' ELSE 'OP' END TIPO_COMPROMISO
                      FROM ADM.ADM_ORDEN_PAGO AC,
                           ADM.ADM_PUC_ORDEN_PAGO APC,
                           ADM.ADM_PROVEEDORES AP,
                           CNT.CNT_MAYORES CM,
                           CNT.CNT_AUXILIARES CA
                     WHERE AC.CODIGO_PRESUPUESTO = p_CODIGO_PRESUPUESTO
                       AND AC.CODIGO_EMPRESA = p_CODIGO_EMPRESA
                       AND APC.CODIGO_ORDEN_PAGO = AC.CODIGO_ORDEN_PAGO
                       AND AP.CODIGO_PROVEEDOR = AC.CODIGO_PROVEEDOR
                       AND CM.CODIGO_MAYOR = 1
                       AND CM.CODIGO_MAYOR = CA.CODIGO_MAYOR
                       AND CA.CODIGO_PROVEEDOR = AC.CODIGO_PROVEEDOR
                       AND EXISTS (
                            SELECT 1
                              FROM ADM.ADM_COMPROMISO_OP COMOP
                             WHERE COMOP.CODIGO_ORDEN_PAGO = AC.CODIGO_ORDEN_PAGO
                               AND COMOP.CODIGO_EMPRESA = AC.CODIGO_EMPRESA
                               AND ADM.ADM_F_DESCRIPTIVAS_ID('CODIGO', COMOP.ORIGEN_COMPROMISO_ID, COMOP.CODIGO_EMPRESA) = 'CON'
                       )
                       AND (
                            (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ODPAUT', 'ODC') AND AC.STATUS <> 'AN' AND AC.FECHA_ORDEN_PAGO >= p_FECHA_DESDE AND AC.FECHA_ORDEN_PAGO < p_FECHA_HASTA + 1)
                            OR
                            (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ANODPAUT', 'ODC') AND AC.STATUS = 'AN' AND TRUNC(AC.FECHA_UPD) >= p_FECHA_DESDE AND TRUNC(AC.FECHA_UPD) <= p_FECHA_HASTA)
                       )
                     GROUP BY AC.CODIGO_ORDEN_PAGO, AC.NUMERO_ORDEN_PAGO, AC.FECHA_ORDEN_PAGO, AP.NOMBRE_PROVEEDOR,
                              NVL(CA.CODIGO_MAYOR, 1), NVL(CA.CODIGO_AUXILIAR, 1094), NVL(CM.NUMERO_MAYOR, '101'),
                              NVL(CA.SEGMENTO1, '001'), NVL(CA.SEGMENTO2, '000'), NVL(CA.DENOMINACION, '999'), RTRIM(AC.MOTIVO)
              )
             WHERE (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ODPAUT', 'ODC') OR COLUMNA_BALANCE IN ('D', 'H'))
        ) q
     WHERE NVL(q.MONTO, 0) <> 0
     ORDER BY q.TIPO_COMPROMISO, q.CODIGO_ORDEN_PAGO, q.CODIGO_MAYOR;

    IF p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ANODPAUT', 'ODC') THEN
        UPDATE CNT.CNT_AUT_LINE_WRK
           SET MONTO = -MONTO
         WHERE RUN_ID = p_RUN_ID;
    END IF;

    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        DELETE FROM CNT.CNT_AUT_LINE_WRK
         WHERE RUN_ID = p_RUN_ID;
        p_Message := SQLERRM;
END;
/

-- END R58 OBJECT SP_CNT_AUT_BUILD_OP

DECLARE
    v_count NUMBER;
BEGIN
    FOR r IN (SELECT LINE,POSITION,TEXT FROM USER_ERRORS
               WHERE NAME='SP_CNT_AUT_BUILD_OP' AND TYPE='PROCEDURE' AND ATTRIBUTE='ERROR'
               ORDER BY SEQUENCE) LOOP
        DBMS_OUTPUT.PUT_LINE('LINEA '||r.LINE||':'||r.POSITION||' '||r.TEXT);
    END LOOP;
    SELECT COUNT(*) INTO v_count FROM USER_OBJECTS
     WHERE OBJECT_NAME='SP_CNT_AUT_BUILD_OP' AND OBJECT_TYPE='PROCEDURE' AND STATUS='VALID';
    IF v_count<>1 THEN RAISE_APPLICATION_ERROR(-20058,'R58: SP_CNT_AUT_BUILD_OP INVALID. Usar respaldo generado.'); END IF;
END;
/

-- BEGIN R58 OBJECT SP_CNT_AUT_BUILD_CH_RET

-- R58: explicit rejection of incompatible type; calculation unchanged.
CREATE OR REPLACE PROCEDURE CNT.SP_CNT_AUT_BUILD_CH_RET (
    p_RUN_ID              IN VARCHAR2,
    p_ORIGEN_ID           IN NUMBER,
    p_TIPO_COMPROBANTE_ID IN NUMBER,
    p_FECHA_DESDE         IN DATE,
    p_FECHA_HASTA         IN DATE,
    p_CODIGO_PRESUPUESTO  IN NUMBER,
    p_CODIGO_EMPRESA      IN NUMBER,
    p_Message             OUT VARCHAR2
) AS
    v_CODIGO_PROVEEDOR NUMBER;
    v_TIPO_CODIGO      VARCHAR2(20);
BEGIN
    DELETE FROM CNT.CNT_AUT_LINE_WRK
     WHERE RUN_ID = p_RUN_ID
        OR FECHA_REGISTRO < SYSDATE - 1;

    IF TRUNC(p_FECHA_DESDE) < TO_DATE('01/01/2022', 'DD/MM/RRRR')
       OR TRUNC(p_FECHA_HASTA) < TO_DATE('01/01/2022', 'DD/MM/RRRR') THEN
        p_Message := 'La rama legacy de cheques anterior a 01/01/2022 aun no esta migrada.';
        RETURN;
    END IF;

    BEGIN
        SELECT ap.CODIGO_PROVEEDOR
          INTO v_CODIGO_PROVEEDOR
          FROM ADM.ADM_PROVEEDORES ap
         WHERE ap.TIPO_PROVEEDOR_ID IN (
               SELECT ad.DESCRIPCION_ID
                 FROM ADM.ADM_DESCRIPTIVAS ad
                WHERE ad.CODIGO = 'FISCO'
         )
           AND ROWNUM = 1;
    EXCEPTION
        WHEN NO_DATA_FOUND THEN
            p_Message := 'Debe validar si existe el proveedor tipo FISCO.';
            RETURN;
    END;

    SELECT CODIGO
      INTO v_TIPO_CODIGO
      FROM CNT.CNT_DESCRIPTIVAS
     WHERE DESCRIPCION_ID = p_TIPO_COMPROBANTE_ID
       AND TITULO_ID = 5
       AND (CODIGO_EMPRESA IS NULL OR CODIGO_EMPRESA = p_CODIGO_EMPRESA);

    IF p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ANCHEAUT', 'ODC') THEN
        IF TO_NUMBER('99') <> TO_NUMBER(v_TIPO_CODIGO) THEN
            p_Message := 'Combinacion incompatible: ANCHEAUT requiere el codigo de tipo 99.';
            RETURN;
        END IF;
    ELSIF TO_NUMBER('04') <> TO_NUMBER(v_TIPO_CODIGO) THEN
        p_Message := 'Combinacion incompatible: este origen requiere el codigo de tipo 04.';
        RETURN;
    END IF;

    IF p_ORIGEN_ID IN (
        CNT.CNT_DESCRIPTIVA_ID('CHEAUT', 'ODC'),
        CNT.CNT_DESCRIPTIVA_ID('CHEPROAUT', 'ODC')
    ) THEN
        INSERT INTO CNT.CNT_AUT_LINE_WRK (
            RUN_ID, SECUENCIA, CODIGO_MAYOR, MAYOR, CODIGO_AUXILIAR, AUXILIAR,
            REFERENCIA1, REFERENCIA2, REFERENCIA3, DESCRIPCION, MONTO
        )
        SELECT p_RUN_ID,
               NVL((SELECT MAX(SECUENCIA) FROM CNT.CNT_AUT_LINE_WRK WHERE RUN_ID = p_RUN_ID), 0) + ROWNUM,
               q.CODIGO_MAYOR,
               q.NUMERO_MAYOR,
               q.CODIGO_AUXILIAR,
               q.SEGMENTO1 || '-' || q.SEGMENTO2 || ' ' || q.NOMBRE_PROVEEDOR,
               SUBSTR(TO_CHAR(q.NUMERO_CHEQUE), 1, 20),
               SUBSTR(q.NUMERO_ORDEN_PAGO, 1, 20),
               NULL,
               SUBSTRB(q.DESCRIPCION, 1, 200),
               -ABS(NVL(q.MONTO, 0))
          FROM (
                SELECT DISTINCT AC.NUMERO_CHEQUE,
                       OP.NUMERO_ORDEN_PAGO,
                       SUBSTR(ADM.ADM_F_PROVEEDOR(OP.CODIGO_PROVEEDOR), 11, 100) NOMBRE_PROVEEDOR,
                       NVL((SELECT SUM(BO.MONTO)
                              FROM ADM.ADM_BENEFICIARIOS_OP BO
                             WHERE BO.CODIGO_ORDEN_PAGO = OP.CODIGO_ORDEN_PAGO
                               AND BO.CODIGO_PRESUPUESTO = OP.CODIGO_PRESUPUESTO
                               AND BO.CODIGO_PROVEEDOR = OP.CODIGO_PROVEEDOR), 0) MONTO,
                       CA.CODIGO_MAYOR,
                       CA.CODIGO_AUXILIAR,
                       CM.NUMERO_MAYOR,
                       CA.SEGMENTO1,
                       CA.SEGMENTO2,
                       CASE
                         WHEN EXISTS (SELECT 1 FROM ADM.ADM_RETENCIONES_OP RETOP
                                       WHERE RETOP.CODIGO_ORDEN_PAGO = OP.CODIGO_ORDEN_PAGO
                                         AND RETOP.MONTO_RETENCION > 0)
                         THEN '(OP)(CR) ' || CA.DENOMINACION
                         ELSE '(OP)(SR) ' || CA.DENOMINACION
                       END DESCRIPCION
                  FROM ADM.ADM_CHEQUES AC,
                       ADM.ADM_BENEFICIARIOS_CH APC,
                       ADM.ADM_BENEFICIARIOS_OP BENOP,
                       ADM.ADM_ORDEN_PAGO OP,
                       CNT.CNT_MAYORES CM,
                       CNT.CNT_AUXILIARES CA
                 WHERE AC.CODIGO_PRESUPUESTO = p_CODIGO_PRESUPUESTO
                   AND AC.CODIGO_EMPRESA = p_CODIGO_EMPRESA
                   AND APC.CODIGO_CHEQUE = AC.CODIGO_CHEQUE
                   AND APC.CODIGO_BENEFICIARIO_OP = BENOP.CODIGO_BENEFICIARIO_OP
                   AND BENOP.CODIGO_ORDEN_PAGO = OP.CODIGO_ORDEN_PAGO
                   AND OP.CODIGO_PRESUPUESTO = p_CODIGO_PRESUPUESTO
                   AND CM.CODIGO_MAYOR = 1
                   AND CM.CODIGO_MAYOR = CA.CODIGO_MAYOR
                   AND CA.CODIGO_PROVEEDOR = OP.CODIGO_PROVEEDOR
                   AND (AC.TIPO_CHEQUE_ID = ADM.ADM_F_DESCRIPTIVAS('A.DESCRIPCION_ID','TDCH','CODIGO','CHOP',AC.CODIGO_EMPRESA)
                        OR AC.TIPO_CHEQUE_ID = ADM.ADM_F_DESCRIPTIVAS('A.DESCRIPCION_ID','TDCH','CODIGO','NDOP',AC.CODIGO_EMPRESA))
                   AND AC.FECHA_CHEQUE >= p_FECHA_DESDE
                   AND AC.FECHA_CHEQUE < p_FECHA_HASTA + 1
                   AND OP.STATUS <> 'AN'
                   AND AC.STATUS <> 'AN'
                   AND AC.CODIGO_PROVEEDOR <> v_CODIGO_PROVEEDOR
                 ORDER BY 1, 2
          ) q
         WHERE NVL(q.MONTO, 0) <> 0;

        INSERT INTO CNT.CNT_AUT_LINE_WRK (
            RUN_ID, SECUENCIA, CODIGO_MAYOR, MAYOR, CODIGO_AUXILIAR, AUXILIAR,
            REFERENCIA1, REFERENCIA2, REFERENCIA3, DESCRIPCION, MONTO
        )
        SELECT p_RUN_ID,
               NVL((SELECT MAX(SECUENCIA) FROM CNT.CNT_AUT_LINE_WRK WHERE RUN_ID = p_RUN_ID), 0) + ROWNUM,
               q.CODIGO_MAYOR,
               q.NUMERO_MAYOR,
               q.CODIGO_AUXILIAR,
               q.SEGMENTO1 || '-' || q.SEGMENTO2 || ' ' || q.NOMBRE_PROVEEDOR,
               SUBSTR(TO_CHAR(q.NUMERO_CHEQUE), 1, 20),
               SUBSTR(q.NUMERO_ORDEN_PAGO, 1, 20),
               NULL,
               SUBSTRB('(' || q.TIPO_COMPROMISO || ') (CR) ' || q.MOTIVO, 1, 200),
               ABS(NVL(q.MONTO, 0))
          FROM (
                SELECT AC.NUMERO_CHEQUE,
                       OP.NUMERO_ORDEN_PAGO,
                       CA.DENOMINACION NOMBRE_PROVEEDOR,
                       SUM(APC.MONTO) MONTO,
                       CA.CODIGO_MAYOR,
                       CA.CODIGO_AUXILIAR,
                       CM.NUMERO_MAYOR,
                       CA.SEGMENTO1,
                       CA.SEGMENTO2,
                       RTRIM(AC.MOTIVO) MOTIVO,
                       'CH' TIPO_COMPROMISO
                  FROM ADM.ADM_CHEQUES AC,
                       ADM.ADM_BENEFICIARIOS_CH APC,
                       ADM.ADM_BENEFICIARIOS_OP BENOP,
                       ADM.ADM_ORDEN_PAGO OP,
                       ADM.ADM_PROVEEDORES AP,
                       CNT.CNT_MAYORES CM,
                       CNT.CNT_AUXILIARES CA,
                       SIS.SIS_CUENTAS_BANCOS CUEBAN
                 WHERE AC.CODIGO_PRESUPUESTO = p_CODIGO_PRESUPUESTO
                   AND AC.CODIGO_EMPRESA = p_CODIGO_EMPRESA
                   AND APC.CODIGO_CHEQUE = AC.CODIGO_CHEQUE
                   AND AP.CODIGO_PROVEEDOR = AC.CODIGO_PROVEEDOR
                   AND CM.CODIGO_MAYOR = 2
                   AND CUEBAN.NO_CUENTA = CA.EXTRA3
                   AND AC.CODIGO_CUENTA_BANCO = CUEBAN.CODIGO_CUENTA_BANCO
                   AND APC.CODIGO_BENEFICIARIO_OP = BENOP.CODIGO_BENEFICIARIO_OP
                   AND BENOP.CODIGO_ORDEN_PAGO = OP.CODIGO_ORDEN_PAGO
                   AND CUEBAN.CODIGO_BANCO IN (SELECT BAN.CODIGO_BANCO
                                                 FROM SIS.SIS_BANCOS BAN
                                                WHERE BAN.CODIGO_INTERBANCARIO = CA.DESCRIPCION)
                   AND CM.CODIGO_MAYOR = CA.CODIGO_MAYOR
                   AND (AC.TIPO_CHEQUE_ID = ADM.ADM_F_DESCRIPTIVAS('A.DESCRIPCION_ID','TDCH','CODIGO','CHOP',AC.CODIGO_EMPRESA)
                        OR AC.TIPO_CHEQUE_ID = ADM.ADM_F_DESCRIPTIVAS('A.DESCRIPCION_ID','TDCH','CODIGO','NDOP',AC.CODIGO_EMPRESA))
                   AND AC.FECHA_CHEQUE >= p_FECHA_DESDE
                   AND AC.FECHA_CHEQUE < p_FECHA_HASTA + 1
                   AND OP.STATUS <> 'AN'
                   AND AC.STATUS <> 'AN'
                   AND AC.CODIGO_PROVEEDOR <> v_CODIGO_PROVEEDOR
                 GROUP BY AC.NUMERO_CHEQUE, OP.NUMERO_ORDEN_PAGO, CA.DENOMINACION,
                          CA.CODIGO_MAYOR, CA.CODIGO_AUXILIAR, CM.NUMERO_MAYOR,
                          CA.SEGMENTO1, CA.SEGMENTO2, RTRIM(AC.MOTIVO)
                 ORDER BY 1, 2
          ) q
         WHERE NVL(q.MONTO, 0) <> 0;
    ELSIF p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('CHERETAUT', 'ODC') THEN
        INSERT INTO CNT.CNT_AUT_LINE_WRK (
            RUN_ID, SECUENCIA, CODIGO_MAYOR, MAYOR, CODIGO_AUXILIAR, AUXILIAR,
            REFERENCIA1, REFERENCIA2, REFERENCIA3, DESCRIPCION, MONTO
        )
        SELECT p_RUN_ID,
               NVL((SELECT MAX(SECUENCIA) FROM CNT.CNT_AUT_LINE_WRK WHERE RUN_ID = p_RUN_ID), 0) + ROWNUM,
               q.CODIGO_MAYOR, q.NUMERO_MAYOR, q.CODIGO_AUXILIAR,
               q.SEGMENTO1 || '-' || q.SEGMENTO2 || ' ' || q.NOMBRE_PROVEEDOR,
               SUBSTR(TO_CHAR(q.NUMERO_CHEQUE), 1, 20), SUBSTR(q.NUMERO_ORDEN_PAGO, 1, 20), NULL,
               SUBSTRB(' (CR) ' || q.MOTIVO, 1, 200),
               ABS(NVL(q.MONTO, 0))
          FROM (
                SELECT DISTINCT OP.NUMERO_ORDEN_PAGO,
                       AC.NUMERO_CHEQUE,
                       CA.DENOMINACION NOMBRE_PROVEEDOR,
                       NVL((SELECT SUM(BO.MONTO)
                              FROM ADM.ADM_BENEFICIARIOS_OP BO
                             WHERE BO.CODIGO_ORDEN_PAGO = OP.CODIGO_ORDEN_PAGO
                               AND BO.CODIGO_PRESUPUESTO = OP.CODIGO_PRESUPUESTO
                               AND BO.CODIGO_PROVEEDOR = v_CODIGO_PROVEEDOR), 0) MONTO,
                       CA.CODIGO_MAYOR, CA.CODIGO_AUXILIAR, CM.NUMERO_MAYOR,
                       CA.SEGMENTO1, CA.SEGMENTO2, OP.MOTIVO
                  FROM ADM.ADM_CHEQUES AC,
                       ADM.ADM_BENEFICIARIOS_CH APC,
                       ADM.ADM_BENEFICIARIOS_OP BENOP,
                       ADM.ADM_ORDEN_PAGO OP,
                       ADM.ADM_PROVEEDORES AP,
                       CNT.CNT_MAYORES CM,
                       CNT.CNT_AUXILIARES CA,
                       SIS.SIS_CUENTAS_BANCOS CUEBAN
                 WHERE OP.CODIGO_PRESUPUESTO = p_CODIGO_PRESUPUESTO
                   AND APC.CODIGO_CHEQUE = AC.CODIGO_CHEQUE
                   AND AP.CODIGO_PROVEEDOR = AC.CODIGO_PROVEEDOR
                   AND CM.CODIGO_MAYOR = 2
                   AND CUEBAN.NO_CUENTA = CA.EXTRA3
                   AND AC.CODIGO_CUENTA_BANCO = CUEBAN.CODIGO_CUENTA_BANCO
                   AND APC.CODIGO_BENEFICIARIO_OP = BENOP.CODIGO_BENEFICIARIO_OP
                   AND BENOP.CODIGO_ORDEN_PAGO = OP.CODIGO_ORDEN_PAGO
                   AND CUEBAN.CODIGO_BANCO IN (SELECT BAN.CODIGO_BANCO
                                                 FROM SIS.SIS_BANCOS BAN
                                                WHERE BAN.CODIGO_INTERBANCARIO = CA.DESCRIPCION)
                   AND CM.CODIGO_MAYOR = CA.CODIGO_MAYOR
                   AND EXISTS (SELECT 1 FROM ADM.ADM_RETENCIONES_OP RETOP
                                WHERE RETOP.CODIGO_ORDEN_PAGO = OP.CODIGO_ORDEN_PAGO
                                  AND RETOP.MONTO_RETENCION > 0)
                   AND (AC.TIPO_CHEQUE_ID = ADM.ADM_F_DESCRIPTIVAS('A.DESCRIPCION_ID','TDCH','CODIGO','CHOP',AC.CODIGO_EMPRESA)
                        OR AC.TIPO_CHEQUE_ID = ADM.ADM_F_DESCRIPTIVAS('A.DESCRIPCION_ID','TDCH','CODIGO','NDOP',AC.CODIGO_EMPRESA))
                   AND AC.FECHA_CHEQUE >= p_FECHA_DESDE
                   AND AC.FECHA_CHEQUE < p_FECHA_HASTA + 1
                   AND OP.STATUS <> 'AN'
                   AND AC.STATUS <> 'AN'
                   AND AC.CODIGO_PROVEEDOR = v_CODIGO_PROVEEDOR
                 ORDER BY 2, 1
          ) q
         WHERE NVL(q.MONTO, 0) <> 0;

        INSERT INTO CNT.CNT_AUT_LINE_WRK (
            RUN_ID, SECUENCIA, CODIGO_MAYOR, MAYOR, CODIGO_AUXILIAR, AUXILIAR,
            REFERENCIA1, REFERENCIA2, REFERENCIA3, DESCRIPCION, MONTO
        )
        SELECT p_RUN_ID,
               NVL((SELECT MAX(SECUENCIA) FROM CNT.CNT_AUT_LINE_WRK WHERE RUN_ID = p_RUN_ID), 0) + ROWNUM,
               q.CODIGO_MAYOR, q.NUMERO_MAYOR, q.CODIGO_AUXILIAR,
               q.SEGMENTO1 || '-' || q.SEGMENTO2 || ' ' || q.NOMBRE_PROVEEDOR,
               SUBSTR(TO_CHAR(q.NUMERO_CHEQUE), 1, 20), SUBSTR(q.NUMERO_ORDEN_PAGO, 1, 20), NULL,
               SUBSTRB(' (CR) ' || q.MOTIVO, 1, 200),
               -ABS(NVL(q.MONTO, 0))
          FROM (
                SELECT AC.NUMERO_CHEQUE, OP.NUMERO_ORDEN_PAGO, AD.DESCRIPCION NOMBRE_PROVEEDOR,
                       SUM(RETOP.MONTO_RETENCION) MONTO, CA.CODIGO_MAYOR, CA.CODIGO_AUXILIAR,
                       CM.NUMERO_MAYOR, CA.SEGMENTO1, CA.SEGMENTO2, RTRIM(AC.MOTIVO) MOTIVO
                  FROM ADM.ADM_CHEQUES AC, ADM.ADM_BENEFICIARIOS_CH APC, ADM.ADM_BENEFICIARIOS_OP BENOP,
                       ADM.ADM_ORDEN_PAGO OP, ADM.ADM_PROVEEDORES AP, CNT.CNT_MAYORES CM,
                       CNT.CNT_AUXILIARES CA, ADM.ADM_RETENCIONES_OP RETOP, ADM.ADM_DESCRIPTIVAS AD
                 WHERE AC.CODIGO_PRESUPUESTO = p_CODIGO_PRESUPUESTO
                   AND APC.CODIGO_CHEQUE = AC.CODIGO_CHEQUE
                   AND AP.CODIGO_PROVEEDOR = AC.CODIGO_PROVEEDOR
                   AND CM.CODIGO_MAYOR = 14
                   AND CA.DESCRIPCION = AD.CODIGO
                   AND APC.CODIGO_BENEFICIARIO_OP = BENOP.CODIGO_BENEFICIARIO_OP
                   AND BENOP.CODIGO_ORDEN_PAGO = OP.CODIGO_ORDEN_PAGO
                   AND CM.CODIGO_MAYOR = CA.CODIGO_MAYOR
                   AND RETOP.CODIGO_ORDEN_PAGO = OP.CODIGO_ORDEN_PAGO
                   AND RETOP.MONTO_RETENCION > 0
                   AND AD.DESCRIPCION_ID = RETOP.TIPO_RETENCION_ID
                   AND (AC.TIPO_CHEQUE_ID = ADM.ADM_F_DESCRIPTIVAS('A.DESCRIPCION_ID','TDCH','CODIGO','CHOP',AC.CODIGO_EMPRESA)
                        OR AC.TIPO_CHEQUE_ID = ADM.ADM_F_DESCRIPTIVAS('A.DESCRIPCION_ID','TDCH','CODIGO','NDOP',AC.CODIGO_EMPRESA))
                   AND AC.FECHA_CHEQUE >= p_FECHA_DESDE
                   AND AC.FECHA_CHEQUE < p_FECHA_HASTA + 1
                   AND OP.STATUS <> 'AN'
                   AND AC.STATUS <> 'AN'
                   AND AP.CODIGO_PROVEEDOR = v_CODIGO_PROVEEDOR
                 GROUP BY AC.NUMERO_CHEQUE, OP.NUMERO_ORDEN_PAGO, AD.DESCRIPCION,
                          CA.CODIGO_MAYOR, CA.CODIGO_AUXILIAR, CM.NUMERO_MAYOR,
                          CA.SEGMENTO1, CA.SEGMENTO2, RTRIM(AC.MOTIVO)
                 ORDER BY 1, 2
          ) q
         WHERE NVL(q.MONTO, 0) <> 0;
    ELSIF p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('CHERETDAUT', 'ODC') THEN
        INSERT INTO CNT.CNT_AUT_LINE_WRK (
            RUN_ID, SECUENCIA, CODIGO_MAYOR, MAYOR, CODIGO_AUXILIAR, AUXILIAR,
            REFERENCIA1, REFERENCIA2, REFERENCIA3, DESCRIPCION, MONTO
        )
        SELECT p_RUN_ID,
               NVL((SELECT MAX(SECUENCIA) FROM CNT.CNT_AUT_LINE_WRK WHERE RUN_ID = p_RUN_ID), 0) + ROWNUM,
               q.CODIGO_MAYOR, q.NUMERO_MAYOR, q.CODIGO_AUXILIAR,
               q.SEGMENTO1 || '-' || q.SEGMENTO2 || ' ' || q.NOMBRE_PROVEEDOR,
               SUBSTR(q.NUMERO_CHEQUE, 1, 20), SUBSTR(q.NUMERO_ORDEN_PAGO, 1, 20), NULL,
               SUBSTRB(' (CR) ' || q.MOTIVO, 1, 200),
               -ABS(NVL(q.MONTO, 0))
          FROM (
                SELECT DISTINCT OP.NUMERO_ORDEN_PAGO,
                       OP.NUMERO_ORDEN_PAGO NUMERO_CHEQUE,
                       SUBSTR(ADM.ADM_F_PROVEEDOR(OP.CODIGO_PROVEEDOR), 11, 100) NOMBRE_PROVEEDOR,
                       NVL((SELECT SUM(BO.MONTO)
                              FROM ADM.ADM_BENEFICIARIOS_OP BO
                             WHERE BO.CODIGO_ORDEN_PAGO = OP.CODIGO_ORDEN_PAGO
                               AND BO.CODIGO_PRESUPUESTO = OP.CODIGO_PRESUPUESTO
                               AND BO.CODIGO_PROVEEDOR = v_CODIGO_PROVEEDOR), 0) MONTO,
                       CA.CODIGO_MAYOR, CA.CODIGO_AUXILIAR, CM.NUMERO_MAYOR,
                       CA.SEGMENTO1, CA.SEGMENTO2, OP.MOTIVO
                  FROM ADM.ADM_ORDEN_PAGO OP, CNT.CNT_MAYORES CM, CNT.CNT_AUXILIARES CA
                 WHERE OP.CODIGO_PRESUPUESTO = p_CODIGO_PRESUPUESTO
                   AND CM.CODIGO_MAYOR = 1
                   AND EXISTS (SELECT 1 FROM ADM.ADM_RETENCIONES_OP RETOP
                                WHERE RETOP.CODIGO_ORDEN_PAGO = OP.CODIGO_ORDEN_PAGO
                                  AND RETOP.MONTO_RETENCION > 0)
                   AND CM.CODIGO_MAYOR = CA.CODIGO_MAYOR
                   AND CA.CODIGO_PROVEEDOR = OP.CODIGO_PROVEEDOR
                   AND OP.STATUS <> 'AN'
                   AND EXISTS (
                        SELECT 1
                          FROM ADM.ADM_BENEFICIARIOS_OP BENOP,
                               ADM.ADM_BENEFICIARIOS_CH APC,
                               ADM.ADM_CHEQUES AC
                         WHERE BENOP.CODIGO_ORDEN_PAGO = OP.CODIGO_ORDEN_PAGO
                           AND BENOP.CODIGO_PRESUPUESTO = p_CODIGO_PRESUPUESTO
                           AND APC.CODIGO_PRESUPUESTO = p_CODIGO_PRESUPUESTO
                           AND APC.CODIGO_BENEFICIARIO_OP = BENOP.CODIGO_BENEFICIARIO_OP
                           AND AC.CODIGO_CHEQUE = APC.CODIGO_CHEQUE
                           AND AC.FECHA_CHEQUE >= p_FECHA_DESDE
                           AND AC.FECHA_CHEQUE < p_FECHA_HASTA + 1
                           AND AC.CODIGO_PRESUPUESTO = p_CODIGO_PRESUPUESTO
                           AND AC.STATUS <> 'AN'
                           AND AC.CODIGO_PROVEEDOR = v_CODIGO_PROVEEDOR
                   )
                 ORDER BY 1
          ) q
         WHERE NVL(q.MONTO, 0) <> 0;

        INSERT INTO CNT.CNT_AUT_LINE_WRK (
            RUN_ID, SECUENCIA, CODIGO_MAYOR, MAYOR, CODIGO_AUXILIAR, AUXILIAR,
            REFERENCIA1, REFERENCIA2, REFERENCIA3, DESCRIPCION, MONTO
        )
        SELECT p_RUN_ID,
               NVL((SELECT MAX(SECUENCIA) FROM CNT.CNT_AUT_LINE_WRK WHERE RUN_ID = p_RUN_ID), 0) + ROWNUM,
               q.CODIGO_MAYOR, q.NUMERO_MAYOR, q.CODIGO_AUXILIAR,
               q.SEGMENTO1 || '-' || q.SEGMENTO2 || ' ' || q.NOMBRE_PROVEEDOR,
               SUBSTR(TO_CHAR(q.NUMERO_CHEQUE), 1, 20), SUBSTR(q.NUMERO_ORDEN_PAGO, 1, 20), NULL,
               SUBSTRB(' (CR) ' || q.MOTIVO, 1, 200),
               ABS(NVL(q.MONTO, 0))
          FROM (
                SELECT AC.NUMERO_CHEQUE, OP.NUMERO_ORDEN_PAGO, AD.DESCRIPCION NOMBRE_PROVEEDOR,
                       SUM(RETOP.MONTO_RETENCION) MONTO, CA.CODIGO_MAYOR, CA.CODIGO_AUXILIAR,
                       CM.NUMERO_MAYOR, CA.SEGMENTO1, CA.SEGMENTO2, RTRIM(AC.MOTIVO) MOTIVO
                  FROM ADM.ADM_CHEQUES AC, ADM.ADM_BENEFICIARIOS_CH APC, ADM.ADM_BENEFICIARIOS_OP BENOP,
                       ADM.ADM_ORDEN_PAGO OP, ADM.ADM_PROVEEDORES AP, CNT.CNT_MAYORES CM,
                       CNT.CNT_AUXILIARES CA, ADM.ADM_RETENCIONES_OP RETOP, ADM.ADM_DESCRIPTIVAS AD
                 WHERE AC.CODIGO_PRESUPUESTO = p_CODIGO_PRESUPUESTO
                   AND APC.CODIGO_CHEQUE = AC.CODIGO_CHEQUE
                   AND AP.CODIGO_PROVEEDOR = AC.CODIGO_PROVEEDOR
                   AND CM.CODIGO_MAYOR = 17
                   AND AD.CODIGO = CA.DESCRIPCION
                   AND APC.CODIGO_BENEFICIARIO_OP = BENOP.CODIGO_BENEFICIARIO_OP
                   AND BENOP.CODIGO_ORDEN_PAGO = OP.CODIGO_ORDEN_PAGO
                   AND CM.CODIGO_MAYOR = CA.CODIGO_MAYOR
                   AND RETOP.CODIGO_ORDEN_PAGO = OP.CODIGO_ORDEN_PAGO
                   AND RETOP.MONTO_RETENCION > 0
                   AND AD.DESCRIPCION_ID = RETOP.TIPO_RETENCION_ID
                   AND (AC.TIPO_CHEQUE_ID = ADM.ADM_F_DESCRIPTIVAS('A.DESCRIPCION_ID','TDCH','CODIGO','CHOP',AC.CODIGO_EMPRESA)
                        OR AC.TIPO_CHEQUE_ID = ADM.ADM_F_DESCRIPTIVAS('A.DESCRIPCION_ID','TDCH','CODIGO','NDOP',AC.CODIGO_EMPRESA))
                   AND AC.FECHA_CHEQUE >= p_FECHA_DESDE
                   AND AC.FECHA_CHEQUE < p_FECHA_HASTA + 1
                   AND OP.STATUS <> 'AN'
                   AND AC.STATUS <> 'AN'
                   AND AP.CODIGO_PROVEEDOR = v_CODIGO_PROVEEDOR
                 GROUP BY AC.NUMERO_CHEQUE, OP.NUMERO_ORDEN_PAGO, AD.DESCRIPCION,
                          CA.CODIGO_MAYOR, CA.CODIGO_AUXILIAR, CM.NUMERO_MAYOR,
                          CA.SEGMENTO1, CA.SEGMENTO2, RTRIM(AC.MOTIVO)
                 ORDER BY 1, 2
          ) q
         WHERE NVL(q.MONTO, 0) <> 0;
    ELSIF p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('RETENFTDT', 'ODC') THEN
        INSERT INTO CNT.CNT_AUT_LINE_WRK (
            RUN_ID, SECUENCIA, CODIGO_MAYOR, MAYOR, CODIGO_AUXILIAR, AUXILIAR,
            REFERENCIA1, REFERENCIA2, REFERENCIA3, DESCRIPCION, MONTO
        )
        SELECT p_RUN_ID,
               NVL((SELECT MAX(SECUENCIA) FROM CNT.CNT_AUT_LINE_WRK WHERE RUN_ID = p_RUN_ID), 0) + ROWNUM,
               q.CODIGO_MAYOR, q.NUMERO_MAYOR, q.CODIGO_AUXILIAR,
               q.SEGMENTO1 || '-' || q.SEGMENTO2 || ' ' || q.NOMBRE_PROVEEDOR,
               SUBSTR(TO_CHAR(q.NUMERO_CHEQUE), 1, 20), SUBSTR(q.NUMERO_ORDEN_PAGO, 1, 20), NULL,
               SUBSTRB('(' || q.TIPO_COMPROMISO || ') (CR) ' || q.MOTIVO, 1, 200),
               ABS(NVL(q.MONTO, 0))
          FROM (
                SELECT AC.NUMERO_CHEQUE, OP.NUMERO_ORDEN_PAGO, AD.DESCRIPCION NOMBRE_PROVEEDOR,
                       SUM(RETOP.MONTO_RETENCION) MONTO, CA.CODIGO_MAYOR, CA.CODIGO_AUXILIAR,
                       CM.NUMERO_MAYOR, CA.SEGMENTO1, CA.SEGMENTO2, RTRIM(AC.MOTIVO) MOTIVO,
                       'CH' TIPO_COMPROMISO, RETOP.TIPO_RETENCION_ID
                  FROM ADM.ADM_CHEQUES AC, ADM.ADM_BENEFICIARIOS_CH APC, ADM.ADM_BENEFICIARIOS_OP BENOP,
                       ADM.ADM_ORDEN_PAGO OP, ADM.ADM_PROVEEDORES AP, CNT.CNT_MAYORES CM,
                       CNT.CNT_AUXILIARES CA, ADM.ADM_RETENCIONES_OP RETOP, ADM.ADM_DESCRIPTIVAS AD
                 WHERE AC.CODIGO_PRESUPUESTO = p_CODIGO_PRESUPUESTO
                   AND APC.CODIGO_CHEQUE = AC.CODIGO_CHEQUE
                   AND AP.CODIGO_PROVEEDOR = AC.CODIGO_PROVEEDOR
                   AND CM.CODIGO_MAYOR = 17
                   AND AD.CODIGO = CA.DESCRIPCION
                   AND APC.CODIGO_BENEFICIARIO_OP = BENOP.CODIGO_BENEFICIARIO_OP
                   AND BENOP.CODIGO_ORDEN_PAGO = OP.CODIGO_ORDEN_PAGO
                   AND CM.CODIGO_MAYOR = CA.CODIGO_MAYOR
                   AND RETOP.CODIGO_ORDEN_PAGO = OP.CODIGO_ORDEN_PAGO
                   AND RETOP.MONTO_RETENCION > 0
                   AND AD.DESCRIPCION_ID = RETOP.TIPO_RETENCION_ID
                   AND (AC.TIPO_CHEQUE_ID = ADM.ADM_F_DESCRIPTIVAS('A.DESCRIPCION_ID','TDCH','CODIGO','CHOP',AC.CODIGO_EMPRESA)
                        OR AC.TIPO_CHEQUE_ID = ADM.ADM_F_DESCRIPTIVAS('A.DESCRIPCION_ID','TDCH','CODIGO','NDOP',AC.CODIGO_EMPRESA))
                   AND AC.FECHA_CHEQUE >= p_FECHA_DESDE
                   AND AC.FECHA_CHEQUE < p_FECHA_HASTA + 1
                   AND OP.STATUS <> 'AN'
                   AND AC.STATUS <> 'AN'
                   AND AP.CODIGO_PROVEEDOR = v_CODIGO_PROVEEDOR
                 GROUP BY AC.NUMERO_CHEQUE, OP.NUMERO_ORDEN_PAGO, AD.DESCRIPCION,
                          CA.CODIGO_MAYOR, CA.CODIGO_AUXILIAR, CM.NUMERO_MAYOR,
                          CA.SEGMENTO1, CA.SEGMENTO2, RTRIM(AC.MOTIVO), RETOP.TIPO_RETENCION_ID
                 ORDER BY 1, 2
          ) q
         WHERE NVL(q.MONTO, 0) <> 0;

        INSERT INTO CNT.CNT_AUT_LINE_WRK (
            RUN_ID, SECUENCIA, CODIGO_MAYOR, MAYOR, CODIGO_AUXILIAR, AUXILIAR,
            REFERENCIA1, REFERENCIA2, REFERENCIA3, DESCRIPCION, MONTO
        )
        SELECT p_RUN_ID,
               NVL((SELECT MAX(SECUENCIA) FROM CNT.CNT_AUT_LINE_WRK WHERE RUN_ID = p_RUN_ID), 0) + ROWNUM,
               q.CODIGO_MAYOR, q.NUMERO_MAYOR, q.CODIGO_AUXILIAR,
               q.SEGMENTO1 || '-' || q.SEGMENTO2 || ' ' || q.NOMBRE_PROVEEDOR,
               SUBSTR(TO_CHAR(q.NUMERO_CHEQUE), 1, 20), SUBSTR(q.NUMERO_ORDEN_PAGO, 1, 20), NULL,
               SUBSTRB(q.MOTIVO, 1, 200),
               -ABS(NVL(q.MONTO, 0))
          FROM (
                SELECT AC.NUMERO_CHEQUE, OP.NUMERO_ORDEN_PAGO, CA.DENOMINACION NOMBRE_PROVEEDOR,
                       SUM(RETOP.MONTO_RETENCION) MONTO, CA.CODIGO_MAYOR, CA.CODIGO_AUXILIAR,
                       CM.NUMERO_MAYOR, CA.SEGMENTO1, CA.SEGMENTO2, SUBSTR(RTRIM(AC.MOTIVO), 1, 200) MOTIVO
                  FROM ADM.ADM_CHEQUES AC, ADM.ADM_BENEFICIARIOS_CH APC, ADM.ADM_BENEFICIARIOS_OP BENOP,
                       ADM.ADM_ORDEN_PAGO OP, ADM.ADM_PROVEEDORES AP, CNT.CNT_MAYORES CM,
                       CNT.CNT_AUXILIARES CA, ADM.ADM_RETENCIONES_OP RETOP, ADM.ADM_DESCRIPTIVAS AD
                 WHERE AC.CODIGO_PRESUPUESTO = p_CODIGO_PRESUPUESTO
                   AND APC.CODIGO_CHEQUE = AC.CODIGO_CHEQUE
                   AND AP.CODIGO_PROVEEDOR = AC.CODIGO_PROVEEDOR
                   AND CM.CODIGO_MAYOR = 14
                   AND AD.CODIGO = CA.DESCRIPCION
                   AND APC.CODIGO_BENEFICIARIO_OP = BENOP.CODIGO_BENEFICIARIO_OP
                   AND BENOP.CODIGO_ORDEN_PAGO = OP.CODIGO_ORDEN_PAGO
                   AND CM.CODIGO_MAYOR = CA.CODIGO_MAYOR
                   AND RETOP.CODIGO_ORDEN_PAGO = OP.CODIGO_ORDEN_PAGO
                   AND RETOP.MONTO_RETENCION > 0
                   AND AD.DESCRIPCION_ID = RETOP.TIPO_RETENCION_ID
                   AND (AC.TIPO_CHEQUE_ID = ADM.ADM_F_DESCRIPTIVAS('A.DESCRIPCION_ID','TDCH','CODIGO','CHOP',AC.CODIGO_EMPRESA)
                        OR AC.TIPO_CHEQUE_ID = ADM.ADM_F_DESCRIPTIVAS('A.DESCRIPCION_ID','TDCH','CODIGO','NDOP',AC.CODIGO_EMPRESA))
                   AND AC.FECHA_CHEQUE >= p_FECHA_DESDE
                   AND AC.FECHA_CHEQUE < p_FECHA_HASTA + 1
                   AND OP.STATUS <> 'AN'
                   AND AC.STATUS <> 'AN'
                   AND AP.CODIGO_PROVEEDOR = v_CODIGO_PROVEEDOR
                 GROUP BY AC.NUMERO_CHEQUE, OP.NUMERO_ORDEN_PAGO, CA.DENOMINACION,
                          CA.CODIGO_MAYOR, CA.CODIGO_AUXILIAR, CM.NUMERO_MAYOR,
                          CA.SEGMENTO1, CA.SEGMENTO2, SUBSTR(RTRIM(AC.MOTIVO), 1, 200), RETOP.TIPO_RETENCION_ID
                 ORDER BY 1, 2
         ) q
         WHERE NVL(q.MONTO, 0) <> 0;
    ELSIF p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ANCHEAUT', 'ODC') THEN
        INSERT INTO CNT.CNT_AUT_LINE_WRK (
            RUN_ID, SECUENCIA, CODIGO_MAYOR, MAYOR, CODIGO_AUXILIAR, AUXILIAR,
            REFERENCIA1, REFERENCIA2, REFERENCIA3, DESCRIPCION, MONTO
        )
        SELECT p_RUN_ID,
               ROWNUM,
               q.CODIGO_MAYOR,
               q.NUMERO_MAYOR,
               q.CODIGO_AUXILIAR,
               q.SEGMENTO1 || '-' || q.SEGMENTO2 || ' ' || q.NOMBRE_PROVEEDOR,
               SUBSTR(TO_CHAR(q.NUMERO_CHEQUE), 1, 20),
               SUBSTR(q.NUMERO_ORDEN_PAGO, 1, 20),
               NULL,
               SUBSTRB('(' || q.TIPO_COMPROMISO || ') ' || q.MOTIVO, 1, 200),
               CASE q.COLUMNA_BALANCE
                 WHEN 'D' THEN -ABS(NVL(q.MONTO, 0))
                 ELSE ABS(NVL(q.MONTO, 0))
               END
          FROM (
                SELECT 'H' COLUMNA_BALANCE, AC.CODIGO_CHEQUE, AC.NUMERO_CHEQUE, OP.NUMERO_ORDEN_PAGO,
                       AP.NOMBRE_PROVEEDOR, SUM(APC.MONTO) MONTO, CA.CODIGO_MAYOR,
                       CA.CODIGO_AUXILIAR, CM.NUMERO_MAYOR, CA.SEGMENTO1, CA.SEGMENTO2,
                       RTRIM(AC.MOTIVO) MOTIVO, 'CH' TIPO_COMPROMISO
                  FROM ADM.ADM_CHEQUES AC, ADM.ADM_BENEFICIARIOS_CH APC, ADM.ADM_BENEFICIARIOS_OP BENOP,
                       ADM.ADM_ORDEN_PAGO OP, ADM.ADM_PROVEEDORES AP, CNT.CNT_MAYORES CM,
                       CNT.CNT_AUXILIARES CA
                 WHERE AC.CODIGO_PRESUPUESTO = p_CODIGO_PRESUPUESTO
                   AND AC.CODIGO_EMPRESA = p_CODIGO_EMPRESA
                   AND APC.CODIGO_CHEQUE = AC.CODIGO_CHEQUE
                   AND AP.CODIGO_PROVEEDOR = AC.CODIGO_PROVEEDOR
                   AND APC.CODIGO_BENEFICIARIO_OP = BENOP.CODIGO_BENEFICIARIO_OP
                   AND BENOP.CODIGO_ORDEN_PAGO = OP.CODIGO_ORDEN_PAGO
                   AND CM.CODIGO_MAYOR = 1
                   AND NOT EXISTS (SELECT 1 FROM ADM.ADM_RETENCIONES_OP RETOP
                                    WHERE RETOP.CODIGO_ORDEN_PAGO = OP.CODIGO_ORDEN_PAGO
                                      AND RETOP.MONTO_RETENCION > 0)
                   AND CM.CODIGO_MAYOR = CA.CODIGO_MAYOR
                   AND CA.CODIGO_PROVEEDOR = AC.CODIGO_PROVEEDOR
                   AND AC.TIPO_CHEQUE_ID = ADM.ADM_F_DESCRIPTIVAS('A.DESCRIPCION_ID','TDCH','CODIGO','CHOP',AC.CODIGO_EMPRESA)
                   AND TRUNC(AC.FECHA_UPD) >= p_FECHA_DESDE
                   AND TRUNC(AC.FECHA_UPD) <= p_FECHA_HASTA
                   AND AC.STATUS = 'AN'
                 GROUP BY AC.CODIGO_CHEQUE, AC.NUMERO_CHEQUE, OP.NUMERO_ORDEN_PAGO,
                          AP.NOMBRE_PROVEEDOR, CA.CODIGO_MAYOR, CA.CODIGO_AUXILIAR,
                          CM.NUMERO_MAYOR, CA.SEGMENTO1, CA.SEGMENTO2, RTRIM(AC.MOTIVO)
                UNION
                SELECT 'D' COLUMNA_BALANCE, AC.CODIGO_CHEQUE, AC.NUMERO_CHEQUE, OP.NUMERO_ORDEN_PAGO,
                       CA.DENOMINACION NOMBRE_PROVEEDOR, SUM(APC.MONTO) MONTO, CA.CODIGO_MAYOR,
                       CA.CODIGO_AUXILIAR, CM.NUMERO_MAYOR, CA.SEGMENTO1, CA.SEGMENTO2,
                       RTRIM(AC.MOTIVO) MOTIVO, 'CH' TIPO_COMPROMISO
                  FROM ADM.ADM_CHEQUES AC, ADM.ADM_BENEFICIARIOS_CH APC, ADM.ADM_BENEFICIARIOS_OP BENOP,
                       ADM.ADM_ORDEN_PAGO OP, ADM.ADM_PROVEEDORES AP, CNT.CNT_MAYORES CM,
                       CNT.CNT_AUXILIARES CA, SIS.SIS_CUENTAS_BANCOS CUEBAN
                 WHERE AC.CODIGO_PRESUPUESTO = p_CODIGO_PRESUPUESTO
                   AND AC.CODIGO_EMPRESA = p_CODIGO_EMPRESA
                   AND APC.CODIGO_CHEQUE = AC.CODIGO_CHEQUE
                   AND AP.CODIGO_PROVEEDOR = AC.CODIGO_PROVEEDOR
                   AND CM.CODIGO_MAYOR = 2
                   AND CUEBAN.NO_CUENTA = CA.EXTRA3
                   AND AC.CODIGO_CUENTA_BANCO = CUEBAN.CODIGO_CUENTA_BANCO
                   AND APC.CODIGO_BENEFICIARIO_OP = BENOP.CODIGO_BENEFICIARIO_OP
                   AND BENOP.CODIGO_ORDEN_PAGO = OP.CODIGO_ORDEN_PAGO
                   AND CUEBAN.CODIGO_BANCO = (SELECT BAN.CODIGO_BANCO
                                                FROM SIS.SIS_BANCOS BAN
                                               WHERE BAN.CODIGO_INTERBANCARIO = CA.DESCRIPCION)
                   AND CM.CODIGO_MAYOR = CA.CODIGO_MAYOR
                   AND NOT EXISTS (SELECT 1 FROM ADM.ADM_RETENCIONES_OP RETOP
                                    WHERE RETOP.CODIGO_ORDEN_PAGO = OP.CODIGO_ORDEN_PAGO
                                      AND RETOP.MONTO_RETENCION > 0)
                   AND AC.TIPO_CHEQUE_ID = ADM.ADM_F_DESCRIPTIVAS('A.DESCRIPCION_ID','TDCH','CODIGO','CHOP',AC.CODIGO_EMPRESA)
                   AND TRUNC(AC.FECHA_UPD) >= p_FECHA_DESDE
                   AND TRUNC(AC.FECHA_UPD) <= p_FECHA_HASTA
                   AND AC.STATUS = 'AN'
                 GROUP BY AC.CODIGO_CHEQUE, AC.NUMERO_CHEQUE, OP.NUMERO_ORDEN_PAGO,
                          CA.DENOMINACION, CA.CODIGO_MAYOR, CA.CODIGO_AUXILIAR,
                          CM.NUMERO_MAYOR, CA.SEGMENTO1, CA.SEGMENTO2, RTRIM(AC.MOTIVO)
                UNION
                SELECT 'H' COLUMNA_BALANCE, AC.CODIGO_CHEQUE, AC.NUMERO_CHEQUE, OP.NUMERO_ORDEN_PAGO,
                       AP.NOMBRE_PROVEEDOR, NVL(SUM(DOCUM.MONTO_DOCUMENTO), 0) MONTO, CA.CODIGO_MAYOR,
                       CA.CODIGO_AUXILIAR, CM.NUMERO_MAYOR, CA.SEGMENTO1, CA.SEGMENTO2,
                       RTRIM(AC.MOTIVO) MOTIVO, 'CH' TIPO_COMPROMISO
                  FROM ADM.ADM_CHEQUES AC, ADM.ADM_BENEFICIARIOS_CH APC, ADM.ADM_BENEFICIARIOS_OP BENOP,
                       ADM.ADM_ORDEN_PAGO OP, ADM.ADM_PROVEEDORES AP, CNT.CNT_MAYORES CM,
                       CNT.CNT_AUXILIARES CA, ADM.ADM_DOCUMENTOS_OP DOCUM
                 WHERE AC.CODIGO_PRESUPUESTO = p_CODIGO_PRESUPUESTO
                   AND AC.CODIGO_EMPRESA = p_CODIGO_EMPRESA
                   AND APC.CODIGO_CHEQUE = AC.CODIGO_CHEQUE
                   AND AP.CODIGO_PROVEEDOR = AC.CODIGO_PROVEEDOR
                   AND APC.CODIGO_BENEFICIARIO_OP = BENOP.CODIGO_BENEFICIARIO_OP
                   AND BENOP.CODIGO_ORDEN_PAGO = OP.CODIGO_ORDEN_PAGO
                   AND DOCUM.CODIGO_ORDEN_PAGO = OP.CODIGO_ORDEN_PAGO
                   AND CM.CODIGO_MAYOR = 1
                   AND AP.CODIGO_PROVEEDOR <> v_CODIGO_PROVEEDOR
                   AND EXISTS (SELECT 1 FROM ADM.ADM_RETENCIONES_OP RETOP
                                WHERE RETOP.CODIGO_ORDEN_PAGO = OP.CODIGO_ORDEN_PAGO
                                  AND RETOP.MONTO_RETENCION > 0)
                   AND CM.CODIGO_MAYOR = CA.CODIGO_MAYOR
                   AND CA.CODIGO_PROVEEDOR = AC.CODIGO_PROVEEDOR
                   AND AC.TIPO_CHEQUE_ID = ADM.ADM_F_DESCRIPTIVAS('A.DESCRIPCION_ID','TDCH','CODIGO','CHOP',AC.CODIGO_EMPRESA)
                   AND TRUNC(AC.FECHA_UPD) >= p_FECHA_DESDE
                   AND TRUNC(AC.FECHA_UPD) <= p_FECHA_HASTA
                   AND AC.STATUS = 'AN'
                 GROUP BY AC.CODIGO_CHEQUE, AC.NUMERO_CHEQUE, OP.NUMERO_ORDEN_PAGO,
                          AP.NOMBRE_PROVEEDOR, CA.CODIGO_MAYOR, CA.CODIGO_AUXILIAR,
                          CM.NUMERO_MAYOR, CA.SEGMENTO1, CA.SEGMENTO2, RTRIM(AC.MOTIVO)
                UNION
                SELECT 'D' COLUMNA_BALANCE, AC.CODIGO_CHEQUE, AC.NUMERO_CHEQUE, OP.NUMERO_ORDEN_PAGO,
                       CA.DENOMINACION NOMBRE_PROVEEDOR, SUM(APC.MONTO) MONTO, CA.CODIGO_MAYOR,
                       CA.CODIGO_AUXILIAR, CM.NUMERO_MAYOR, CA.SEGMENTO1, CA.SEGMENTO2,
                       RTRIM(AC.MOTIVO) MOTIVO, 'CH' TIPO_COMPROMISO
                  FROM ADM.ADM_CHEQUES AC, ADM.ADM_BENEFICIARIOS_CH APC, ADM.ADM_BENEFICIARIOS_OP BENOP,
                       ADM.ADM_ORDEN_PAGO OP, ADM.ADM_PROVEEDORES AP, CNT.CNT_MAYORES CM,
                       CNT.CNT_AUXILIARES CA, SIS.SIS_CUENTAS_BANCOS CUEBAN
                 WHERE AC.CODIGO_PRESUPUESTO = p_CODIGO_PRESUPUESTO
                   AND AC.CODIGO_EMPRESA = p_CODIGO_EMPRESA
                   AND APC.CODIGO_CHEQUE = AC.CODIGO_CHEQUE
                   AND AP.CODIGO_PROVEEDOR = AC.CODIGO_PROVEEDOR
                   AND CM.CODIGO_MAYOR = 2
                   AND AP.CODIGO_PROVEEDOR <> v_CODIGO_PROVEEDOR
                   AND CUEBAN.NO_CUENTA = CA.EXTRA3
                   AND AC.CODIGO_CUENTA_BANCO = CUEBAN.CODIGO_CUENTA_BANCO
                   AND APC.CODIGO_BENEFICIARIO_OP = BENOP.CODIGO_BENEFICIARIO_OP
                   AND BENOP.CODIGO_ORDEN_PAGO = OP.CODIGO_ORDEN_PAGO
                   AND CUEBAN.CODIGO_BANCO = (SELECT BAN.CODIGO_BANCO
                                                FROM SIS.SIS_BANCOS BAN
                                               WHERE BAN.CODIGO_INTERBANCARIO = CA.DESCRIPCION)
                   AND CM.CODIGO_MAYOR = CA.CODIGO_MAYOR
                   AND EXISTS (SELECT 1 FROM ADM.ADM_RETENCIONES_OP RETOP
                                WHERE RETOP.CODIGO_ORDEN_PAGO = OP.CODIGO_ORDEN_PAGO
                                  AND RETOP.MONTO_RETENCION > 0)
                   AND AC.TIPO_CHEQUE_ID = ADM.ADM_F_DESCRIPTIVAS('A.DESCRIPCION_ID','TDCH','CODIGO','CHOP',AC.CODIGO_EMPRESA)
                   AND TRUNC(AC.FECHA_UPD) >= p_FECHA_DESDE
                   AND TRUNC(AC.FECHA_UPD) <= p_FECHA_HASTA
                   AND AC.STATUS = 'AN'
                 GROUP BY AC.CODIGO_CHEQUE, AC.NUMERO_CHEQUE, OP.NUMERO_ORDEN_PAGO,
                          CA.DENOMINACION, CA.CODIGO_MAYOR, CA.CODIGO_AUXILIAR,
                          CM.NUMERO_MAYOR, CA.SEGMENTO1, CA.SEGMENTO2, RTRIM(AC.MOTIVO)
                UNION
                SELECT 'D' COLUMNA_BALANCE, AC.CODIGO_CHEQUE, AC.NUMERO_CHEQUE, OP.NUMERO_ORDEN_PAGO,
                       CA.DENOMINACION NOMBRE_PROVEEDOR,
                       NVL(RETOP.MONTO_RETENCION, (NVL(SUM(DOCUM.MONTO_DOCUMENTO), 0) - NVL(SUM(DOCUM.MONTO_IMPUESTO), 0)) * NVL((RETOP.POR_RETENCION / 100), 0)) MONTO,
                       CA.CODIGO_MAYOR, CA.CODIGO_AUXILIAR, CM.NUMERO_MAYOR,
                       CA.SEGMENTO1, CA.SEGMENTO2, RTRIM(AC.MOTIVO) MOTIVO, 'CH' TIPO_COMPROMISO
                  FROM ADM.ADM_CHEQUES AC, ADM.ADM_BENEFICIARIOS_CH APC, ADM.ADM_BENEFICIARIOS_OP BENOP,
                       ADM.ADM_ORDEN_PAGO OP, ADM.ADM_PROVEEDORES AP, CNT.CNT_MAYORES CM,
                       CNT.CNT_AUXILIARES CA, ADM.ADM_RETENCIONES_OP RETOP, ADM.ADM_DOCUMENTOS_OP DOCUM
                 WHERE AC.CODIGO_PRESUPUESTO = p_CODIGO_PRESUPUESTO
                   AND AC.CODIGO_EMPRESA = p_CODIGO_EMPRESA
                   AND APC.CODIGO_CHEQUE = AC.CODIGO_CHEQUE
                   AND AP.CODIGO_PROVEEDOR = AC.CODIGO_PROVEEDOR
                   AND APC.CODIGO_BENEFICIARIO_OP = BENOP.CODIGO_BENEFICIARIO_OP
                   AND BENOP.CODIGO_ORDEN_PAGO = OP.CODIGO_ORDEN_PAGO
                   AND DOCUM.CODIGO_ORDEN_PAGO = OP.CODIGO_ORDEN_PAGO
                   AND RETOP.CODIGO_ORDEN_PAGO = OP.CODIGO_ORDEN_PAGO
                   AND CM.CODIGO_MAYOR = 17
                   AND AP.CODIGO_PROVEEDOR <> v_CODIGO_PROVEEDOR
                   AND EXISTS (SELECT 1 FROM ADM.ADM_RETENCIONES_OP RETOP2
                                WHERE RETOP2.CODIGO_ORDEN_PAGO = OP.CODIGO_ORDEN_PAGO
                                  AND RETOP2.MONTO_RETENCION > 0)
                   AND CM.CODIGO_MAYOR = CA.CODIGO_MAYOR
                   AND CA.CODIGO_AUXILIAR = (SELECT AUXIL.CODIGO_AUXILIAR
                                               FROM CNT.CNT_AUXILIARES AUXIL
                                              WHERE AUXIL.CODIGO_MAYOR = 17
                                                AND AUXIL.DESCRIPCION = ADM.ADM_F_DESCRIPTIVAS_ID('A.CODIGO', RETOP.TIPO_RETENCION_ID, AC.CODIGO_EMPRESA))
                   AND AC.TIPO_CHEQUE_ID = ADM.ADM_F_DESCRIPTIVAS('A.DESCRIPCION_ID','TDCH','CODIGO','CHOP',AC.CODIGO_EMPRESA)
                   AND TRUNC(AC.FECHA_UPD) >= p_FECHA_DESDE
                   AND TRUNC(AC.FECHA_UPD) <= p_FECHA_HASTA
                   AND AC.STATUS = 'AN'
                 GROUP BY AC.CODIGO_CHEQUE, AC.NUMERO_CHEQUE, OP.NUMERO_ORDEN_PAGO,
                          CA.DENOMINACION, RETOP.MONTO_RETENCION, RETOP.POR_RETENCION,
                          CA.CODIGO_MAYOR, CA.CODIGO_AUXILIAR, CM.NUMERO_MAYOR,
                          CA.SEGMENTO1, CA.SEGMENTO2, RTRIM(AC.MOTIVO)
                UNION
                SELECT 'H' COLUMNA_BALANCE, AC.CODIGO_CHEQUE, AC.NUMERO_CHEQUE, NULL NUMERO_ORDEN_PAGO,
                       AP.NOMBRE_PROVEEDOR, SUM(APC.MONTO) MONTO, CA.CODIGO_MAYOR,
                       CA.CODIGO_AUXILIAR, CM.NUMERO_MAYOR, CA.SEGMENTO1, CA.SEGMENTO2,
                       RTRIM(AC.MOTIVO) MOTIVO, 'CH' TIPO_COMPROMISO
                  FROM ADM.ADM_CHEQUES AC, ADM.ADM_BENEFICIARIOS_CH APC, ADM.ADM_PROVEEDORES AP,
                       CNT.CNT_MAYORES CM, CNT.CNT_AUXILIARES CA
                 WHERE AC.CODIGO_PRESUPUESTO = p_CODIGO_PRESUPUESTO
                   AND AC.CODIGO_EMPRESA = p_CODIGO_EMPRESA
                   AND APC.CODIGO_CHEQUE = AC.CODIGO_CHEQUE
                   AND AP.CODIGO_PROVEEDOR = AC.CODIGO_PROVEEDOR
                   AND CM.CODIGO_MAYOR = 17
                   AND CM.CODIGO_MAYOR = CA.CODIGO_MAYOR
                   AND CA.CODIGO_PROVEEDOR = AC.CODIGO_PROVEEDOR
                   AND AC.TIPO_CHEQUE_ID = ADM.ADM_F_DESCRIPTIVAS('A.DESCRIPCION_ID','TDCH','CODIGO','CHT',AC.CODIGO_EMPRESA)
                   AND TRUNC(AC.FECHA_UPD) >= p_FECHA_DESDE
                   AND TRUNC(AC.FECHA_UPD) <= p_FECHA_HASTA
                   AND AC.STATUS = 'AN'
                 GROUP BY AC.CODIGO_CHEQUE, AC.NUMERO_CHEQUE, AP.NOMBRE_PROVEEDOR,
                          CA.CODIGO_MAYOR, CA.CODIGO_AUXILIAR, CM.NUMERO_MAYOR,
                          CA.SEGMENTO1, CA.SEGMENTO2, RTRIM(AC.MOTIVO)
                UNION
                SELECT 'D' COLUMNA_BALANCE, AC.CODIGO_CHEQUE, AC.NUMERO_CHEQUE, NULL NUMERO_ORDEN_PAGO,
                       CA.DENOMINACION NOMBRE_PROVEEDOR, SUM(APC.MONTO) MONTO, CA.CODIGO_MAYOR,
                       CA.CODIGO_AUXILIAR, CM.NUMERO_MAYOR, CA.SEGMENTO1, CA.SEGMENTO2,
                       RTRIM(AC.MOTIVO) MOTIVO, 'CH' TIPO_COMPROMISO
                  FROM ADM.ADM_CHEQUES AC, ADM.ADM_BENEFICIARIOS_CH APC, ADM.ADM_PROVEEDORES AP,
                       CNT.CNT_MAYORES CM, CNT.CNT_AUXILIARES CA, SIS.SIS_CUENTAS_BANCOS CUEBAN
                 WHERE AC.CODIGO_PRESUPUESTO = p_CODIGO_PRESUPUESTO
                   AND AC.CODIGO_EMPRESA = p_CODIGO_EMPRESA
                   AND APC.CODIGO_CHEQUE = AC.CODIGO_CHEQUE
                   AND AP.CODIGO_PROVEEDOR = AC.CODIGO_PROVEEDOR
                   AND CM.CODIGO_MAYOR = 14
                   AND CUEBAN.NO_CUENTA = CA.EXTRA3
                   AND AC.CODIGO_CUENTA_BANCO = CUEBAN.CODIGO_CUENTA_BANCO
                   AND CUEBAN.CODIGO_BANCO = (SELECT BAN.CODIGO_BANCO
                                                FROM SIS.SIS_BANCOS BAN
                                               WHERE BAN.CODIGO_INTERBANCARIO = CA.EXTRA2)
                   AND CM.CODIGO_MAYOR = CA.CODIGO_MAYOR
                   AND AC.TIPO_CHEQUE_ID = ADM.ADM_F_DESCRIPTIVAS('A.DESCRIPCION_ID','TDCH','CODIGO','CHT',AC.CODIGO_EMPRESA)
                   AND TRUNC(AC.FECHA_UPD) >= p_FECHA_DESDE
                   AND TRUNC(AC.FECHA_UPD) <= p_FECHA_HASTA
                   AND AC.STATUS = 'AN'
                 GROUP BY AC.CODIGO_CHEQUE, AC.NUMERO_CHEQUE, CA.DENOMINACION,
                          CA.CODIGO_MAYOR, CA.CODIGO_AUXILIAR, CM.NUMERO_MAYOR,
                          CA.SEGMENTO1, CA.SEGMENTO2, RTRIM(AC.MOTIVO)
                 ORDER BY 12, 3, 1, 9
          ) q
         WHERE NVL(q.MONTO, 0) <> 0;
    END IF;

    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        DELETE FROM CNT.CNT_AUT_LINE_WRK
         WHERE RUN_ID = p_RUN_ID;
        p_Message := SQLERRM;
END;
/

-- END R58 OBJECT SP_CNT_AUT_BUILD_CH_RET

DECLARE
    v_count NUMBER;
BEGIN
    FOR r IN (SELECT LINE,POSITION,TEXT FROM USER_ERRORS
               WHERE NAME='SP_CNT_AUT_BUILD_CH_RET' AND TYPE='PROCEDURE' AND ATTRIBUTE='ERROR'
               ORDER BY SEQUENCE) LOOP
        DBMS_OUTPUT.PUT_LINE('LINEA '||r.LINE||':'||r.POSITION||' '||r.TEXT);
    END LOOP;
    SELECT COUNT(*) INTO v_count FROM USER_OBJECTS
     WHERE OBJECT_NAME='SP_CNT_AUT_BUILD_CH_RET' AND OBJECT_TYPE='PROCEDURE' AND STATUS='VALID';
    IF v_count<>1 THEN RAISE_APPLICATION_ERROR(-20058,'R58: SP_CNT_AUT_BUILD_CH_RET INVALID. Usar respaldo generado.'); END IF;
END;
/

-- BEGIN R58 OBJECT SP_CNT_AUT_PREV

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_AUT_PREV (
    p_CODIGO_PERIODO      IN NUMBER,
    p_TIPO_COMPROBANTE_ID IN NUMBER,
    p_ORIGEN_ID           IN NUMBER,
    p_FECHA_DESDE         IN DATE,
    p_FECHA_HASTA         IN DATE,
    p_USUARIO_ID          IN NUMBER,
    p_CODIGO_EMPRESA      IN NUMBER,
    p_ResultSet           OUT SYS_REFCURSOR,
    p_Message             OUT VARCHAR2
) AS
    v_dummy              NUMBER;
    v_codigo_presupuesto NUMBER;
    v_run_id             VARCHAR2(32) := RAWTOHEX(SYS_GUID());

    PROCEDURE open_empty_result IS
    BEGIN
        OPEN p_ResultSet FOR
            SELECT CAST(NULL AS NUMBER) SECUENCIA,
                   CAST(NULL AS NUMBER) CODIGO_MAYOR,
                   CAST(NULL AS VARCHAR2(400)) MAYOR,
                   CAST(NULL AS NUMBER) CODIGO_AUXILIAR,
                   CAST(NULL AS VARCHAR2(400)) AUXILIAR,
                   CAST(NULL AS VARCHAR2(20)) REFERENCIA1,
                   CAST(NULL AS VARCHAR2(20)) REFERENCIA2,
                   CAST(NULL AS VARCHAR2(20)) REFERENCIA3,
                   CAST(NULL AS VARCHAR2(1000)) DESCRIPCION,
                   CAST(NULL AS NUMBER) MONTO
              FROM DUAL
             WHERE 1 = 0;
    END;
BEGIN
    IF p_FECHA_DESDE IS NULL OR p_FECHA_HASTA IS NULL THEN
        p_Message := 'El rango de fechas es requerido.';
        open_empty_result;
        RETURN;
    END IF;

    IF p_FECHA_DESDE > p_FECHA_HASTA THEN
        p_Message := 'La fecha desde no puede ser mayor que la fecha hasta.';
        open_empty_result;
        RETURN;
    END IF;

    SELECT 1
      INTO v_dummy
      FROM CNT.CNT_PERIODOS
     WHERE CODIGO_PERIODO = p_CODIGO_PERIODO
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA
       AND FECHA_CIERRE IS NULL;

    SELECT 1
      INTO v_dummy
      FROM CNT.CNT_DESCRIPTIVAS
     WHERE DESCRIPCION_ID = p_TIPO_COMPROBANTE_ID
       AND TITULO_ID = 5
       AND (CODIGO_EMPRESA IS NULL OR CODIGO_EMPRESA = p_CODIGO_EMPRESA);

    SELECT 1
      INTO v_dummy
      FROM CNT.CNT_DESCRIPTIVAS
     WHERE DESCRIPCION_ID = p_ORIGEN_ID
       AND TITULO_ID = 1
       AND (CODIGO_EMPRESA IS NULL OR CODIGO_EMPRESA = p_CODIGO_EMPRESA);

    CNT.SP_CNT_AUT_PRES_GET(
        p_FECHA_DESDE, p_FECHA_HASTA, p_CODIGO_EMPRESA,
        v_codigo_presupuesto, p_Message
    );
    IF NVL(p_Message, 'Error') <> 'Success' THEN
        open_empty_result;
        RETURN;
    END IF;

    IF p_ORIGEN_ID IN (
        CNT.CNT_DESCRIPTIVA_ID('ODPAUT', 'ODC'),
        CNT.CNT_DESCRIPTIVA_ID('ANODPAUT', 'ODC'),
        CNT.CNT_DESCRIPTIVA_ID('COMP', 'ODC'),
        CNT.CNT_DESCRIPTIVA_ID('ANCOMP', 'ODC')
    ) THEN
        IF v_codigo_presupuesto IS NULL THEN
            open_empty_result;
            p_Message := 'No existe presupuesto vigente para el rango indicado.';
            RETURN;
        END IF;

        IF p_ORIGEN_ID IN (
            CNT.CNT_DESCRIPTIVA_ID('ODPAUT', 'ODC'),
            CNT.CNT_DESCRIPTIVA_ID('ANODPAUT', 'ODC')
        ) THEN
            CNT.SP_CNT_AUT_BUILD_OP(
                v_run_id,
                p_ORIGEN_ID,
                p_FECHA_DESDE,
                p_FECHA_HASTA,
                v_codigo_presupuesto,
                p_CODIGO_EMPRESA,
                p_Message,
                p_TIPO_COMPROBANTE_ID => p_TIPO_COMPROBANTE_ID
            );
        ELSE
            CNT.SP_CNT_AUT_BUILD_COMP(
                v_run_id,
                p_ORIGEN_ID,
                p_FECHA_DESDE,
                p_FECHA_HASTA,
                v_codigo_presupuesto,
                p_CODIGO_EMPRESA,
                p_Message,
                p_TIPO_COMPROBANTE_ID => p_TIPO_COMPROBANTE_ID
            );
        END IF;

        IF NVL(p_Message, 'Error') <> 'Success' THEN
            open_empty_result;
            RETURN;
        END IF;

        OPEN p_ResultSet FOR
            SELECT SECUENCIA,
                   CODIGO_MAYOR,
                   MAYOR,
                   CODIGO_AUXILIAR,
                   AUXILIAR,
                   REFERENCIA1,
                   REFERENCIA2,
                   REFERENCIA3,
                   DESCRIPCION,
                   MONTO
              FROM CNT.CNT_AUT_LINE_WRK
             WHERE RUN_ID = v_run_id
             ORDER BY SECUENCIA;

        p_Message := 'Success';
        RETURN;
    END IF;

    IF p_ORIGEN_ID IN (
        CNT.CNT_DESCRIPTIVA_ID('CHEAUT', 'ODC'),
        CNT.CNT_DESCRIPTIVA_ID('CHEPROAUT', 'ODC'),
        CNT.CNT_DESCRIPTIVA_ID('CHERETAUT', 'ODC'),
        CNT.CNT_DESCRIPTIVA_ID('CHERETDAUT', 'ODC'),
        CNT.CNT_DESCRIPTIVA_ID('RETENFTDT', 'ODC'),
        CNT.CNT_DESCRIPTIVA_ID('ANCHEAUT', 'ODC')
    ) THEN
        IF v_codigo_presupuesto IS NULL THEN
            open_empty_result;
            p_Message := 'No existe presupuesto vigente para el rango indicado.';
            RETURN;
        END IF;

        CNT.SP_CNT_AUT_BUILD_CH_RET(
            v_run_id,
            p_ORIGEN_ID,
            p_TIPO_COMPROBANTE_ID,
            p_FECHA_DESDE,
            p_FECHA_HASTA,
            v_codigo_presupuesto,
            p_CODIGO_EMPRESA,
            p_Message
        );

        IF NVL(p_Message, 'Error') <> 'Success' THEN
            open_empty_result;
            RETURN;
        END IF;

        OPEN p_ResultSet FOR
            SELECT SECUENCIA,
                   CODIGO_MAYOR,
                   MAYOR,
                   CODIGO_AUXILIAR,
                   AUXILIAR,
                   REFERENCIA1,
                   REFERENCIA2,
                   REFERENCIA3,
                   DESCRIPCION,
                   MONTO
              FROM CNT.CNT_AUT_LINE_WRK
             WHERE RUN_ID = v_run_id
             ORDER BY SECUENCIA;

        p_Message := 'Success';
        RETURN;
    END IF;

    IF p_ORIGEN_ID IN (
        CNT.CNT_DESCRIPTIVA_ID('COMPORDCOM', 'ODC'),
        CNT.CNT_DESCRIPTIVA_ID('ANCOMPORDC', 'ODC')
    ) THEN
        IF v_codigo_presupuesto IS NULL THEN
            open_empty_result;
            p_Message := 'No existe presupuesto vigente para el rango indicado.';
            RETURN;
        END IF;

        OPEN p_ResultSet FOR
            SELECT ROWNUM SECUENCIA,
                   ordered_rows.CODIGO_MAYOR,
                   ordered_rows.MAYOR,
                   ordered_rows.CODIGO_AUXILIAR,
                   ordered_rows.AUXILIAR,
                   ordered_rows.REFERENCIA1,
                   ordered_rows.REFERENCIA2,
                   ordered_rows.REFERENCIA3,
                   ordered_rows.DESCRIPCION,
                   ordered_rows.MONTO
              FROM (
                    SELECT x.CODIGO_MAYOR,
                           x.NUMERO_MAYOR MAYOR,
                           x.CODIGO_AUXILIAR,
                           x.SEGMENTO1 || '-' || x.SEGMENTO2 || ' ' || x.DENOMINACION AUXILIAR,
                           SUBSTR(x.NUMERO_COMPROMISO, 1, 20) REFERENCIA1,
                           TO_CHAR(x.FECHA_COMPROMISO, 'DD/MM/RRRR') REFERENCIA2,
                           NULL REFERENCIA3,
                           SUBSTRB('(' || x.TIPO_COMPROMISO || ') ' || x.MOTIVO, 1, 200) DESCRIPCION,
                           CASE
                               WHEN p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('COMPORDCOM', 'ODC') AND x.CODIGO_MAYOR = 9 THEN -ABS(NVL(x.MONTO_COMPROMISO, 0))
                               WHEN p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('COMPORDCOM', 'ODC') THEN ABS(NVL(x.MONTO_COMPROMISO, 0))
                               WHEN x.CODIGO_MAYOR = 9 THEN ABS(NVL(x.MONTO_COMPROMISO, 0))
                               ELSE -ABS(NVL(x.MONTO_COMPROMISO, 0))
                           END MONTO
                      FROM (
                            SELECT A.CODIGO_ORDEN_COMPRA CODIGO_COMPROMISO,
                                   A.NUMERO_ORDEN_COMPRA NUMERO_COMPROMISO,
                                   A.FECHA_ORDEN_COMPRA FECHA_COMPROMISO,
                                   A.CODIGO_PROVEEDOR,
                                   B.NOMBRE_PROVEEDOR,
                                   NULL FINANCIADO_POR,
                                   NVL(SUM(D.MONTO), 0) MONTO_COMPROMISO,
                                   NVL(CA.CODIGO_MAYOR, 9) CODIGO_MAYOR,
                                   NVL(CA.CODIGO_AUXILIAR, 1094) CODIGO_AUXILIAR,
                                   NVL(CA.SEGMENTO1, '001') SEGMENTO1,
                                   NVL(CA.SEGMENTO2, '000') SEGMENTO2,
                                   NVL(CA.DENOMINACION, '999') DENOMINACION,
                                   NVL(CM.NUMERO_MAYOR, '300') NUMERO_MAYOR,
                                   NULL STATUS,
                                   A.MOTIVO,
                                   'OC' TIPO_COMPROMISO
                              FROM ADM.ADM_ORDEN_COMPRA A,
                                   ADM.ADM_PROVEEDORES B,
                                   ADM.ADM_DETALLE_ORDEN_COMPRA C,
                                   ADM.ADM_PUC_ORDEN_COMPRA D,
                                   CNT.CNT_MAYORES CM,
                                   CNT.CNT_AUXILIARES CA
                             WHERE A.CODIGO_PRESUPUESTO = v_codigo_presupuesto
                               AND B.CODIGO_PROVEEDOR = A.CODIGO_PROVEEDOR
                               AND C.CODIGO_ORDEN_COMPRA(+) = A.CODIGO_ORDEN_COMPRA
                               AND D.CODIGO_DETALLE_ORDEN_COMPRA(+) = C.CODIGO_DETALLE_ORDEN_COMPRA
                               AND CM.CODIGO_MAYOR = 9
                               AND CM.CODIGO_MAYOR = CA.CODIGO_MAYOR
                               AND CA.CODIGO_PROVEEDOR = B.CODIGO_PROVEEDOR
                               AND (
                                    (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('COMPORDCOM', 'ODC')
                                     AND A.FECHA_ORDEN_COMPRA >= p_FECHA_DESDE
                                     AND A.FECHA_ORDEN_COMPRA < p_FECHA_HASTA + 1)
                                    OR
                                    (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ANCOMPORDC', 'ODC')
                                     AND A.FECHA_UPD >= p_FECHA_DESDE
                                     AND A.FECHA_UPD < p_FECHA_HASTA + 1)
                               )
                             GROUP BY A.CODIGO_ORDEN_COMPRA,
                                      A.NUMERO_ORDEN_COMPRA,
                                      A.FECHA_ORDEN_COMPRA,
                                      A.CODIGO_PROVEEDOR,
                                      B.NOMBRE_PROVEEDOR,
                                      NULL,
                                      NVL(CA.CODIGO_MAYOR, 9),
                                      NVL(CA.CODIGO_AUXILIAR, 1094),
                                      NVL(CA.SEGMENTO1, '001'),
                                      NVL(CA.SEGMENTO2, '000'),
                                      NVL(CA.DENOMINACION, '999'),
                                      NVL(CM.NUMERO_MAYOR, '300'),
                                      NULL,
                                      A.MOTIVO,
                                      'OC'
                            UNION ALL
                            SELECT A.CODIGO_ORDEN_COMPRA CODIGO_COMPROMISO,
                                   A.NUMERO_ORDEN_COMPRA NUMERO_COMPROMISO,
                                   A.FECHA_ORDEN_COMPRA FECHA_COMPROMISO,
                                   A.CODIGO_PROVEEDOR,
                                   B.NOMBRE_PROVEEDOR,
                                   NULL FINANCIADO_POR,
                                   NVL(SUM(D.MONTO - D.MONTO_ANULADO), 0) MONTO_COMPROMISO,
                                   NVL(CA.CODIGO_MAYOR, 3) CODIGO_MAYOR,
                                   NVL(CA.CODIGO_AUXILIAR, 1094) CODIGO_AUXILIAR,
                                   NVL(CA.SEGMENTO1, '001') SEGMENTO1,
                                   NVL(CA.SEGMENTO2, '000') SEGMENTO2,
                                   NVL(CA.DENOMINACION, '999') DENOMINACION,
                                   NVL(CM.NUMERO_MAYOR, '103') NUMERO_MAYOR,
                                   NULL STATUS,
                                   A.MOTIVO,
                                   'OC' TIPO_COMPROMISO
                              FROM ADM.ADM_ORDEN_COMPRA A,
                                   ADM.ADM_PROVEEDORES B,
                                   ADM.ADM_DETALLE_ORDEN_COMPRA C,
                                   ADM.ADM_PUC_ORDEN_COMPRA D,
                                   CNT.CNT_MAYORES CM,
                                   CNT.CNT_AUXILIARES CA
                             WHERE A.CODIGO_PRESUPUESTO = v_codigo_presupuesto
                               AND B.CODIGO_PROVEEDOR = A.CODIGO_PROVEEDOR
                               AND C.CODIGO_ORDEN_COMPRA(+) = A.CODIGO_ORDEN_COMPRA
                               AND D.CODIGO_DETALLE_ORDEN_COMPRA(+) = C.CODIGO_DETALLE_ORDEN_COMPRA
                               AND CM.CODIGO_MAYOR = 3
                               AND CM.CODIGO_MAYOR = CA.CODIGO_MAYOR
                               AND CA.CODIGO_PROVEEDOR = B.CODIGO_PROVEEDOR
                               AND (
                                    (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('COMPORDCOM', 'ODC')
                                     AND A.FECHA_ORDEN_COMPRA >= p_FECHA_DESDE
                                     AND A.FECHA_ORDEN_COMPRA < p_FECHA_HASTA + 1)
                                    OR
                                    (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ANCOMPORDC', 'ODC')
                                     AND A.FECHA_UPD >= p_FECHA_DESDE
                                     AND A.FECHA_UPD < p_FECHA_HASTA + 1)
                               )
                             GROUP BY A.CODIGO_ORDEN_COMPRA,
                                      A.NUMERO_ORDEN_COMPRA,
                                      A.FECHA_ORDEN_COMPRA,
                                      A.CODIGO_PROVEEDOR,
                                      B.NOMBRE_PROVEEDOR,
                                      NULL,
                                      NVL(CA.CODIGO_MAYOR, 3),
                                      NVL(CA.CODIGO_AUXILIAR, 1094),
                                      NVL(CA.SEGMENTO1, '001'),
                                      NVL(CA.SEGMENTO2, '000'),
                                      NVL(CA.DENOMINACION, '999'),
                                      NVL(CM.NUMERO_MAYOR, '103'),
                                      NULL,
                                      A.MOTIVO,
                                      'OC'
                      ) x
                     WHERE NVL(x.MONTO_COMPROMISO, 0) <> 0
                     ORDER BY x.TIPO_COMPROMISO, x.CODIGO_COMPROMISO, x.MONTO_COMPROMISO DESC
              ) ordered_rows;

        p_Message := 'Success';
        RETURN;
    END IF;

    IF p_ORIGEN_ID IN (
        CNT.CNT_DESCRIPTIVA_ID('COMPCONTOB', 'ODC'),
        CNT.CNT_DESCRIPTIVA_ID('ANCOCONTOB', 'ODC')
    ) THEN
        IF v_codigo_presupuesto IS NULL THEN
            open_empty_result;
            p_Message := 'No existe presupuesto vigente para el rango indicado.';
            RETURN;
        END IF;

        OPEN p_ResultSet FOR
            SELECT ROWNUM SECUENCIA,
                   ordered_rows.CODIGO_MAYOR,
                   ordered_rows.MAYOR,
                   ordered_rows.CODIGO_AUXILIAR,
                   ordered_rows.AUXILIAR,
                   ordered_rows.REFERENCIA1,
                   ordered_rows.REFERENCIA2,
                   ordered_rows.REFERENCIA3,
                   ordered_rows.DESCRIPCION,
                   ordered_rows.MONTO
              FROM (
                    SELECT x.CODIGO_MAYOR,
                           x.NUMERO_MAYOR MAYOR,
                           x.CODIGO_AUXILIAR,
                           x.SEGMENTO1 || '-' || x.SEGMENTO2 || ' ' || x.DENOMINACION AUXILIAR,
                           SUBSTR(x.NUMERO_COMPROMISO, 1, 20) REFERENCIA1,
                           TO_CHAR(x.FECHA_COMPROMISO, 'DD/MM/RRRR') REFERENCIA2,
                           NULL REFERENCIA3,
                           SUBSTRB('(' || x.TIPO_COMPROMISO || ') ' || x.MOTIVO, 1, 200) DESCRIPCION,
                           CASE
                               WHEN p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('COMPCONTOB', 'ODC') AND x.CODIGO_MAYOR = 9 THEN -ABS(NVL(x.MONTO_COMPROMISO, 0))
                               WHEN p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('COMPCONTOB', 'ODC') THEN ABS(NVL(x.MONTO_COMPROMISO, 0))
                               WHEN x.CODIGO_MAYOR = 9 THEN ABS(NVL(x.MONTO_COMPROMISO, 0))
                               ELSE -ABS(NVL(x.MONTO_COMPROMISO, 0))
                           END MONTO
                      FROM (
                            SELECT A.CODIGO_CONTRATO CODIGO_COMPROMISO,
                                   A.NUMERO_CONTRATO NUMERO_COMPROMISO,
                                   A.FECHA_CONTRATO FECHA_COMPROMISO,
                                   A.CODIGO_PROVEEDOR,
                                   B.NOMBRE_PROVEEDOR,
                                   NULL FINANCIADO_POR,
                                   A.MONTO_CONTRATO MONTO_COMPROMISO,
                                   NVL(CA.CODIGO_MAYOR, 9) CODIGO_MAYOR,
                                   NVL(CA.CODIGO_AUXILIAR, 1094) CODIGO_AUXILIAR,
                                   NVL(CA.SEGMENTO1, '001') SEGMENTO1,
                                   NVL(CA.SEGMENTO2, '000') SEGMENTO2,
                                   NVL(CA.DENOMINACION, '999') DENOMINACION,
                                   NVL(CM.NUMERO_MAYOR, '300') NUMERO_MAYOR,
                                   NULL STATUS,
                                   SUBSTRB(A.OBRA, 1, 200) MOTIVO,
                                   'CO' TIPO_COMPROMISO
                              FROM ADM.ADM_CONTRATOS A,
                                   ADM.ADM_PROVEEDORES B,
                                   CNT.CNT_MAYORES CM,
                                   CNT.CNT_AUXILIARES CA
                             WHERE A.CODIGO_PRESUPUESTO = v_codigo_presupuesto
                               AND B.CODIGO_PROVEEDOR = A.CODIGO_PROVEEDOR
                               AND CM.CODIGO_MAYOR = 9
                               AND CM.CODIGO_MAYOR = CA.CODIGO_MAYOR
                               AND CA.CODIGO_PROVEEDOR = B.CODIGO_PROVEEDOR
                               AND A.MONTO_CONTRATO <> 0
                               AND (
                                    (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('COMPCONTOB', 'ODC')
                                     AND A.STATUS <> 'AN'
                                     AND TRUNC(A.FECHA_INS) >= p_FECHA_DESDE
                                     AND TRUNC(A.FECHA_INS) <= p_FECHA_HASTA)
                                    OR
                                    (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ANCOCONTOB', 'ODC')
                                     AND A.STATUS = 'AN'
                                     AND TRUNC(A.FECHA_INS) >= p_FECHA_DESDE
                                     AND TRUNC(A.FECHA_INS) <= p_FECHA_HASTA)
                               )
                               AND EXISTS (
                                    SELECT 1
                                      FROM ADM.ADM_PUC_CONTRATO APC
                                     WHERE APC.CODIGO_CONTRATO = A.CODIGO_CONTRATO
                               )
                            UNION ALL
                            SELECT A.CODIGO_CONTRATO CODIGO_COMPROMISO,
                                   A.NUMERO_CONTRATO NUMERO_COMPROMISO,
                                   A.FECHA_CONTRATO FECHA_COMPROMISO,
                                   A.CODIGO_PROVEEDOR,
                                   B.NOMBRE_PROVEEDOR,
                                   NULL FINANCIADO_POR,
                                   A.MONTO_CONTRATO MONTO_COMPROMISO,
                                   NVL(CA.CODIGO_MAYOR, 3) CODIGO_MAYOR,
                                   NVL(CA.CODIGO_AUXILIAR, 1094) CODIGO_AUXILIAR,
                                   NVL(CA.SEGMENTO1, '001') SEGMENTO1,
                                   NVL(CA.SEGMENTO2, '000') SEGMENTO2,
                                   NVL(CA.DENOMINACION, '999') DENOMINACION,
                                   NVL(CM.NUMERO_MAYOR, '103') NUMERO_MAYOR,
                                   NULL STATUS,
                                   SUBSTRB(A.OBRA, 1, 200) MOTIVO,
                                   'CO' TIPO_COMPROMISO
                              FROM ADM.ADM_CONTRATOS A,
                                   ADM.ADM_PROVEEDORES B,
                                   CNT.CNT_MAYORES CM,
                                   CNT.CNT_AUXILIARES CA
                             WHERE A.CODIGO_PRESUPUESTO = v_codigo_presupuesto
                               AND B.CODIGO_PROVEEDOR = A.CODIGO_PROVEEDOR
                               AND CM.CODIGO_MAYOR = 3
                               AND CM.CODIGO_MAYOR = CA.CODIGO_MAYOR
                               AND CA.CODIGO_PROVEEDOR = B.CODIGO_PROVEEDOR
                               AND A.MONTO_CONTRATO <> 0
                               AND (
                                    (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('COMPCONTOB', 'ODC')
                                     AND A.STATUS <> 'AN'
                                     AND TRUNC(A.FECHA_INS) >= p_FECHA_DESDE
                                     AND TRUNC(A.FECHA_INS) <= p_FECHA_HASTA)
                                    OR
                                    (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ANCOCONTOB', 'ODC')
                                     AND A.STATUS = 'AN'
                                     AND A.FECHA_UPD >= p_FECHA_DESDE
                                     AND A.FECHA_UPD < p_FECHA_HASTA + 1
                                     AND TRUNC(A.FECHA_INS) >= p_FECHA_DESDE
                                     AND TRUNC(A.FECHA_INS) <= p_FECHA_HASTA)
                               )
                               AND EXISTS (
                                    SELECT 1
                                      FROM ADM.ADM_PUC_CONTRATO APC
                                     WHERE APC.CODIGO_CONTRATO = A.CODIGO_CONTRATO
                               )
                      ) x
                     WHERE NVL(x.MONTO_COMPROMISO, 0) <> 0
                     ORDER BY x.TIPO_COMPROMISO, x.CODIGO_COMPROMISO, x.MONTO_COMPROMISO DESC
              ) ordered_rows;

        p_Message := 'Success';
        RETURN;
    END IF;

    open_empty_result;
    p_Message := 'El origen seleccionado no tiene reglas de generacion automatica configuradas.';
EXCEPTION
    WHEN NO_DATA_FOUND THEN
        open_empty_result;
        p_Message := 'Periodo, tipo u origen no valido para el proceso automatico.';
    WHEN OTHERS THEN
        open_empty_result;
        p_Message := SQLERRM;
END;
/

-- END R58 OBJECT SP_CNT_AUT_PREV

DECLARE
    v_count NUMBER;
BEGIN
    FOR r IN (SELECT LINE,POSITION,TEXT FROM USER_ERRORS
               WHERE NAME='SP_CNT_AUT_PREV' AND TYPE='PROCEDURE' AND ATTRIBUTE='ERROR'
               ORDER BY SEQUENCE) LOOP
        DBMS_OUTPUT.PUT_LINE('LINEA '||r.LINE||':'||r.POSITION||' '||r.TEXT);
    END LOOP;
    SELECT COUNT(*) INTO v_count FROM USER_OBJECTS
     WHERE OBJECT_NAME='SP_CNT_AUT_PREV' AND OBJECT_TYPE='PROCEDURE' AND STATUS='VALID';
    IF v_count<>1 THEN RAISE_APPLICATION_ERROR(-20058,'R58: SP_CNT_AUT_PREV INVALID. Usar respaldo generado.'); END IF;
END;
/

-- BEGIN R58 OBJECT SP_CNT_AUT_CONF

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_AUT_CONF (
    p_CODIGO_PERIODO      IN NUMBER,
    p_TIPO_COMPROBANTE_ID IN NUMBER,
    p_ORIGEN_ID           IN NUMBER,
    p_FECHA_DESDE         IN DATE,
    p_FECHA_HASTA         IN DATE,
    p_USUARIO_ID          IN NUMBER,
    p_CODIGO_EMPRESA      IN NUMBER,
    p_FECHA_COMPROBANTE   IN DATE,
    p_OBSERVACION         IN VARCHAR2,
    p_CODIGO_COMPROBANTE  OUT NUMBER,
    p_NUMERO_COMPROBANTE  OUT VARCHAR2,
    p_CANTIDAD_LINEAS     OUT NUMBER,
    p_TOTAL_DEBE          OUT NUMBER,
    p_TOTAL_HABER         OUT NUMBER,
    p_Message             OUT VARCHAR2
) AS
    v_dummy              NUMBER;
    v_codigo_presupuesto NUMBER;
    v_codigo_detalle     NUMBER;
    v_det_message        VARCHAR2(4000);
    v_cmp_message        VARCHAR2(4000);
    v_run_id             VARCHAR2(32) := RAWTOHEX(SYS_GUID());

    PROCEDURE cleanup_comprobante IS
    BEGIN
        DELETE FROM CNT.CNT_AUT_LINE_WRK
         WHERE RUN_ID = v_run_id;

        IF NVL(p_CODIGO_COMPROBANTE, 0) > 0 THEN
            DELETE FROM CNT.CNT_DETALLE_COMPROBANTE
             WHERE CODIGO_COMPROBANTE = p_CODIGO_COMPROBANTE
               AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

            DELETE FROM CNT.CNT_COMPROBANTES
             WHERE CODIGO_COMPROBANTE = p_CODIGO_COMPROBANTE
               AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;
        END IF;
    END;
BEGIN
    CNT.CNT_CMP_GUARD.LOCK_ALL;
    p_CODIGO_COMPROBANTE := 0;
    p_NUMERO_COMPROBANTE := NULL;
    p_CANTIDAD_LINEAS := 0;
    p_TOTAL_DEBE := 0;
    p_TOTAL_HABER := 0;

    IF p_FECHA_DESDE IS NULL OR p_FECHA_HASTA IS NULL THEN
        p_Message := 'El rango de fechas es requerido.';
        RETURN;
    END IF;

    IF p_FECHA_DESDE > p_FECHA_HASTA THEN
        p_Message := 'La fecha desde no puede ser mayor que la fecha hasta.';
        RETURN;
    END IF;

    IF p_FECHA_COMPROBANTE IS NULL THEN
        p_Message := 'La fecha del comprobante es requerida.';
        RETURN;
    END IF;

    IF p_FECHA_COMPROBANTE < TRUNC(p_FECHA_DESDE)
       OR p_FECHA_COMPROBANTE >= TRUNC(p_FECHA_HASTA) + 1 THEN
        p_Message := 'La fecha del comprobante debe estar entre fecha desde y fecha hasta.';
        RETURN;
    END IF;

    SELECT 1
      INTO v_dummy
      FROM CNT.CNT_PERIODOS
     WHERE CODIGO_PERIODO = p_CODIGO_PERIODO
       AND CODIGO_EMPRESA = p_CODIGO_EMPRESA
       AND p_FECHA_COMPROBANTE BETWEEN FECHA_DESDE AND FECHA_HASTA
       AND FECHA_CIERRE IS NULL;

    SELECT 1
      INTO v_dummy
      FROM CNT.CNT_DESCRIPTIVAS
     WHERE DESCRIPCION_ID = p_TIPO_COMPROBANTE_ID
       AND TITULO_ID = 5
       AND (CODIGO_EMPRESA IS NULL OR CODIGO_EMPRESA = p_CODIGO_EMPRESA);

    SELECT 1
      INTO v_dummy
      FROM CNT.CNT_DESCRIPTIVAS
     WHERE DESCRIPCION_ID = p_ORIGEN_ID
       AND TITULO_ID = 1
       AND (CODIGO_EMPRESA IS NULL OR CODIGO_EMPRESA = p_CODIGO_EMPRESA);

    CNT.SP_CNT_AUT_PRES_GET(
        p_FECHA_DESDE, p_FECHA_HASTA, p_CODIGO_EMPRESA,
        v_codigo_presupuesto, p_Message
    );
    IF NVL(p_Message, 'Error') <> 'Success' THEN
        RETURN;
    END IF;

    IF p_ORIGEN_ID IN (
        CNT.CNT_DESCRIPTIVA_ID('ODPAUT', 'ODC'),
        CNT.CNT_DESCRIPTIVA_ID('ANODPAUT', 'ODC'),
        CNT.CNT_DESCRIPTIVA_ID('COMP', 'ODC'),
        CNT.CNT_DESCRIPTIVA_ID('ANCOMP', 'ODC')
    ) THEN
        IF v_codigo_presupuesto IS NULL THEN
            p_Message := 'No existe presupuesto vigente para el rango indicado.';
            RETURN;
        END IF;

        IF p_ORIGEN_ID IN (
            CNT.CNT_DESCRIPTIVA_ID('ODPAUT', 'ODC'),
            CNT.CNT_DESCRIPTIVA_ID('ANODPAUT', 'ODC')
        ) THEN
            CNT.SP_CNT_AUT_BUILD_OP(
                v_run_id,
                p_ORIGEN_ID,
                p_FECHA_DESDE,
                p_FECHA_HASTA,
                v_codigo_presupuesto,
                p_CODIGO_EMPRESA,
                p_Message,
                p_TIPO_COMPROBANTE_ID => p_TIPO_COMPROBANTE_ID
            );
        ELSE
            CNT.SP_CNT_AUT_BUILD_COMP(
                v_run_id,
                p_ORIGEN_ID,
                p_FECHA_DESDE,
                p_FECHA_HASTA,
                v_codigo_presupuesto,
                p_CODIGO_EMPRESA,
                p_Message,
                p_TIPO_COMPROBANTE_ID => p_TIPO_COMPROBANTE_ID
            );
        END IF;

        IF NVL(p_Message, 'Error') <> 'Success' THEN
            RETURN;
        END IF;

        CNT.SP_CNT_CMP_INS(
            p_CODIGO_PERIODO,
            p_TIPO_COMPROBANTE_ID,
            p_FECHA_COMPROBANTE,
            p_ORIGEN_ID,
            p_OBSERVACION,
            p_USUARIO_ID,
            p_CODIGO_EMPRESA,
            p_CODIGO_COMPROBANTE,
            v_cmp_message
        );

        IF NVL(v_cmp_message, 'Error') <> 'Success' OR NVL(p_CODIGO_COMPROBANTE, 0) = 0 THEN
            p_Message := v_cmp_message;
            RETURN;
        END IF;

        CNT.CNT_CMP_GUARD.ALLOW_WRITE(p_CODIGO_COMPROBANTE, p_CODIGO_EMPRESA);
        UPDATE CNT.CNT_COMPROBANTES SET ESTADO_CMP = 'PENDIENTE', EXTRA1 = 'AUTOMATICO'
         WHERE CODIGO_COMPROBANTE = p_CODIGO_COMPROBANTE AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

        SELECT NUMERO_COMPROBANTE
          INTO p_NUMERO_COMPROBANTE
          FROM CNT.CNT_COMPROBANTES
         WHERE CODIGO_COMPROBANTE = p_CODIGO_COMPROBANTE
           AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

        FOR line IN (
            SELECT CODIGO_MAYOR,
                   CODIGO_AUXILIAR,
                   REFERENCIA1,
                   REFERENCIA2,
                   REFERENCIA3,
                   DESCRIPCION,
                   MONTO
              FROM CNT.CNT_AUT_LINE_WRK
             WHERE RUN_ID = v_run_id
             ORDER BY SECUENCIA
        ) LOOP
            CNT.SP_CNT_DET_INS(
                p_CODIGO_COMPROBANTE,
                line.CODIGO_MAYOR,
                line.CODIGO_AUXILIAR,
                line.REFERENCIA1,
                line.REFERENCIA2,
                line.REFERENCIA3,
                line.DESCRIPCION,
                -line.MONTO, -- API/WORK -> Oracle: positive=Debito
                p_USUARIO_ID,
                p_CODIGO_EMPRESA,
                v_codigo_detalle,
                v_det_message,
                1
            );

            IF NVL(v_det_message, 'Error') <> 'Success' THEN
                cleanup_comprobante;
                p_Message := v_det_message;
                RETURN;
            END IF;

            p_CANTIDAD_LINEAS := p_CANTIDAD_LINEAS + 1;
            p_TOTAL_DEBE := p_TOTAL_DEBE + CASE WHEN line.MONTO < 0 THEN ABS(line.MONTO) ELSE 0 END;
            p_TOTAL_HABER := p_TOTAL_HABER + CASE WHEN line.MONTO > 0 THEN line.MONTO ELSE 0 END;
        END LOOP;

        DELETE FROM CNT.CNT_AUT_LINE_WRK
         WHERE RUN_ID = v_run_id;

        IF p_CANTIDAD_LINEAS = 0 THEN
            cleanup_comprobante;
            p_CODIGO_COMPROBANTE := 0;
            p_NUMERO_COMPROBANTE := NULL;
            p_Message := 'El proceso automatico no genero lineas para el rango indicado.';
            RETURN;
        END IF;

        SELECT NVL(SUM(CASE WHEN MONTO > 0 THEN MONTO ELSE 0 END), 0),
               NVL(SUM(CASE WHEN MONTO < 0 THEN -MONTO ELSE 0 END), 0)
          INTO p_TOTAL_DEBE, p_TOTAL_HABER FROM CNT.CNT_DETALLE_COMPROBANTE
         WHERE CODIGO_COMPROBANTE = p_CODIGO_COMPROBANTE AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

        SELECT HABILITADO INTO v_dummy FROM CNT.CNT_CMP_CTL WHERE CODIGO = 1;
        IF p_TOTAL_DEBE <> p_TOTAL_HABER AND v_dummy <> 1 THEN
            cleanup_comprobante;
            p_Message := 'La generacion pendiente requiere completar la instalacion y validacion CNT 37.';
            RETURN;
        END IF;

        UPDATE CNT.CNT_COMPROBANTES
           SET EXTRA1 = 'AUTOMATICO',
               ESTADO_CMP = CASE WHEN p_TOTAL_DEBE = p_TOTAL_HABER THEN 'FINALIZADO' ELSE 'PENDIENTE' END,
               USUARIO_UPD = p_USUARIO_ID,
               FECHA_UPD = SYSDATE
         WHERE CODIGO_COMPROBANTE = p_CODIGO_COMPROBANTE
           AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

        p_Message := 'Success';
        RETURN;
    END IF;

    IF p_ORIGEN_ID IN (
        CNT.CNT_DESCRIPTIVA_ID('CHEAUT', 'ODC'),
        CNT.CNT_DESCRIPTIVA_ID('CHEPROAUT', 'ODC'),
        CNT.CNT_DESCRIPTIVA_ID('CHERETAUT', 'ODC'),
        CNT.CNT_DESCRIPTIVA_ID('CHERETDAUT', 'ODC'),
        CNT.CNT_DESCRIPTIVA_ID('RETENFTDT', 'ODC'),
        CNT.CNT_DESCRIPTIVA_ID('ANCHEAUT', 'ODC')
    ) THEN
        IF v_codigo_presupuesto IS NULL THEN
            p_Message := 'No existe presupuesto vigente para el rango indicado.';
            RETURN;
        END IF;

        CNT.SP_CNT_AUT_BUILD_CH_RET(
            v_run_id,
            p_ORIGEN_ID,
            p_TIPO_COMPROBANTE_ID,
            p_FECHA_DESDE,
            p_FECHA_HASTA,
            v_codigo_presupuesto,
            p_CODIGO_EMPRESA,
            p_Message
        );

        IF NVL(p_Message, 'Error') <> 'Success' THEN
            RETURN;
        END IF;

        CNT.SP_CNT_CMP_INS(
            p_CODIGO_PERIODO,
            p_TIPO_COMPROBANTE_ID,
            p_FECHA_COMPROBANTE,
            p_ORIGEN_ID,
            p_OBSERVACION,
            p_USUARIO_ID,
            p_CODIGO_EMPRESA,
            p_CODIGO_COMPROBANTE,
            v_cmp_message
        );

        IF NVL(v_cmp_message, 'Error') <> 'Success' OR NVL(p_CODIGO_COMPROBANTE, 0) = 0 THEN
            p_Message := v_cmp_message;
            RETURN;
        END IF;

        CNT.CNT_CMP_GUARD.ALLOW_WRITE(p_CODIGO_COMPROBANTE, p_CODIGO_EMPRESA);
        UPDATE CNT.CNT_COMPROBANTES SET ESTADO_CMP = 'PENDIENTE', EXTRA1 = 'AUTOMATICO'
         WHERE CODIGO_COMPROBANTE = p_CODIGO_COMPROBANTE AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

        SELECT NUMERO_COMPROBANTE
          INTO p_NUMERO_COMPROBANTE
          FROM CNT.CNT_COMPROBANTES
         WHERE CODIGO_COMPROBANTE = p_CODIGO_COMPROBANTE
           AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

        FOR line IN (
            SELECT CODIGO_MAYOR,
                   CODIGO_AUXILIAR,
                   REFERENCIA1,
                   REFERENCIA2,
                   REFERENCIA3,
                   DESCRIPCION,
                   MONTO
              FROM CNT.CNT_AUT_LINE_WRK
             WHERE RUN_ID = v_run_id
             ORDER BY SECUENCIA
        ) LOOP
            CNT.SP_CNT_DET_INS(
                p_CODIGO_COMPROBANTE,
                line.CODIGO_MAYOR,
                line.CODIGO_AUXILIAR,
                line.REFERENCIA1,
                line.REFERENCIA2,
                line.REFERENCIA3,
                line.DESCRIPCION,
                -line.MONTO, -- API/WORK -> Oracle: positive=Debito
                p_USUARIO_ID,
                p_CODIGO_EMPRESA,
                v_codigo_detalle,
                v_det_message,
                1
            );

            IF NVL(v_det_message, 'Error') <> 'Success' THEN
                cleanup_comprobante;
                p_Message := v_det_message;
                RETURN;
            END IF;

            p_CANTIDAD_LINEAS := p_CANTIDAD_LINEAS + 1;
            p_TOTAL_DEBE := p_TOTAL_DEBE + CASE WHEN line.MONTO < 0 THEN ABS(line.MONTO) ELSE 0 END;
            p_TOTAL_HABER := p_TOTAL_HABER + CASE WHEN line.MONTO > 0 THEN line.MONTO ELSE 0 END;
        END LOOP;

        DELETE FROM CNT.CNT_AUT_LINE_WRK
         WHERE RUN_ID = v_run_id;

        IF p_CANTIDAD_LINEAS = 0 THEN
            cleanup_comprobante;
            p_CODIGO_COMPROBANTE := 0;
            p_NUMERO_COMPROBANTE := NULL;
            p_Message := 'El proceso automatico no genero lineas para el rango indicado.';
            RETURN;
        END IF;

        SELECT NVL(SUM(CASE WHEN MONTO > 0 THEN MONTO ELSE 0 END), 0),
               NVL(SUM(CASE WHEN MONTO < 0 THEN -MONTO ELSE 0 END), 0)
          INTO p_TOTAL_DEBE, p_TOTAL_HABER FROM CNT.CNT_DETALLE_COMPROBANTE
         WHERE CODIGO_COMPROBANTE = p_CODIGO_COMPROBANTE AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

        SELECT HABILITADO INTO v_dummy FROM CNT.CNT_CMP_CTL WHERE CODIGO = 1;
        IF p_TOTAL_DEBE <> p_TOTAL_HABER AND v_dummy <> 1 THEN
            cleanup_comprobante;
            p_Message := 'La generacion pendiente requiere completar la instalacion y validacion CNT 37.';
            RETURN;
        END IF;

        UPDATE CNT.CNT_COMPROBANTES
           SET EXTRA1 = 'AUTOMATICO',
               ESTADO_CMP = CASE WHEN p_TOTAL_DEBE = p_TOTAL_HABER THEN 'FINALIZADO' ELSE 'PENDIENTE' END,
               USUARIO_UPD = p_USUARIO_ID,
               FECHA_UPD = SYSDATE
         WHERE CODIGO_COMPROBANTE = p_CODIGO_COMPROBANTE
           AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

        p_Message := 'Success';
        RETURN;
    END IF;

    IF p_ORIGEN_ID IN (
        CNT.CNT_DESCRIPTIVA_ID('COMPORDCOM', 'ODC'),
        CNT.CNT_DESCRIPTIVA_ID('ANCOMPORDC', 'ODC')
    ) THEN
        IF v_codigo_presupuesto IS NULL THEN
            p_Message := 'No existe presupuesto vigente para el rango indicado.';
            RETURN;
        END IF;

        CNT.SP_CNT_CMP_INS(
            p_CODIGO_PERIODO,
            p_TIPO_COMPROBANTE_ID,
            p_FECHA_COMPROBANTE,
            p_ORIGEN_ID,
            p_OBSERVACION,
            p_USUARIO_ID,
            p_CODIGO_EMPRESA,
            p_CODIGO_COMPROBANTE,
            v_cmp_message
        );

        IF NVL(v_cmp_message, 'Error') <> 'Success' OR NVL(p_CODIGO_COMPROBANTE, 0) = 0 THEN
            p_Message := v_cmp_message;
            RETURN;
        END IF;

        CNT.CNT_CMP_GUARD.ALLOW_WRITE(p_CODIGO_COMPROBANTE, p_CODIGO_EMPRESA);
        UPDATE CNT.CNT_COMPROBANTES SET ESTADO_CMP = 'PENDIENTE', EXTRA1 = 'AUTOMATICO'
         WHERE CODIGO_COMPROBANTE = p_CODIGO_COMPROBANTE AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

        SELECT NUMERO_COMPROBANTE
          INTO p_NUMERO_COMPROBANTE
          FROM CNT.CNT_COMPROBANTES
         WHERE CODIGO_COMPROBANTE = p_CODIGO_COMPROBANTE
           AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

        FOR line IN (
            SELECT ordered_rows.CODIGO_MAYOR,
                   ordered_rows.CODIGO_AUXILIAR,
                   ordered_rows.REFERENCIA1,
                   ordered_rows.REFERENCIA2,
                   ordered_rows.REFERENCIA3,
                   ordered_rows.DESCRIPCION,
                   ordered_rows.MONTO
              FROM (
                    SELECT x.CODIGO_MAYOR,
                           x.CODIGO_AUXILIAR,
                           SUBSTR(x.NUMERO_COMPROMISO, 1, 20) REFERENCIA1,
                           TO_CHAR(x.FECHA_COMPROMISO, 'DD/MM/RRRR') REFERENCIA2,
                           NULL REFERENCIA3,
                           SUBSTRB('(' || x.TIPO_COMPROMISO || ') ' || x.MOTIVO, 1, 200) DESCRIPCION,
                           CASE
                               WHEN p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('COMPORDCOM', 'ODC') AND x.CODIGO_MAYOR = 9 THEN -ABS(NVL(x.MONTO_COMPROMISO, 0))
                               WHEN p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('COMPORDCOM', 'ODC') THEN ABS(NVL(x.MONTO_COMPROMISO, 0))
                               WHEN x.CODIGO_MAYOR = 9 THEN ABS(NVL(x.MONTO_COMPROMISO, 0))
                               ELSE -ABS(NVL(x.MONTO_COMPROMISO, 0))
                           END MONTO,
                           x.TIPO_COMPROMISO,
                           x.CODIGO_COMPROMISO,
                           x.MONTO_COMPROMISO
                      FROM (
                            SELECT A.CODIGO_ORDEN_COMPRA CODIGO_COMPROMISO,
                                   A.NUMERO_ORDEN_COMPRA NUMERO_COMPROMISO,
                                   A.FECHA_ORDEN_COMPRA FECHA_COMPROMISO,
                                   A.CODIGO_PROVEEDOR,
                                   B.NOMBRE_PROVEEDOR,
                                   NULL FINANCIADO_POR,
                                   NVL(SUM(D.MONTO), 0) MONTO_COMPROMISO,
                                   NVL(CA.CODIGO_MAYOR, 9) CODIGO_MAYOR,
                                   NVL(CA.CODIGO_AUXILIAR, 1094) CODIGO_AUXILIAR,
                                   NVL(CA.SEGMENTO1, '001') SEGMENTO1,
                                   NVL(CA.SEGMENTO2, '000') SEGMENTO2,
                                   NVL(CA.DENOMINACION, '999') DENOMINACION,
                                   NVL(CM.NUMERO_MAYOR, '300') NUMERO_MAYOR,
                                   NULL STATUS,
                                   A.MOTIVO,
                                   'OC' TIPO_COMPROMISO
                              FROM ADM.ADM_ORDEN_COMPRA A,
                                   ADM.ADM_PROVEEDORES B,
                                   ADM.ADM_DETALLE_ORDEN_COMPRA C,
                                   ADM.ADM_PUC_ORDEN_COMPRA D,
                                   CNT.CNT_MAYORES CM,
                                   CNT.CNT_AUXILIARES CA
                             WHERE A.CODIGO_PRESUPUESTO = v_codigo_presupuesto
                               AND B.CODIGO_PROVEEDOR = A.CODIGO_PROVEEDOR
                               AND C.CODIGO_ORDEN_COMPRA(+) = A.CODIGO_ORDEN_COMPRA
                               AND D.CODIGO_DETALLE_ORDEN_COMPRA(+) = C.CODIGO_DETALLE_ORDEN_COMPRA
                               AND CM.CODIGO_MAYOR = 9
                               AND CM.CODIGO_MAYOR = CA.CODIGO_MAYOR
                               AND CA.CODIGO_PROVEEDOR = B.CODIGO_PROVEEDOR
                               AND (
                                    (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('COMPORDCOM', 'ODC')
                                     AND A.FECHA_ORDEN_COMPRA >= p_FECHA_DESDE
                                     AND A.FECHA_ORDEN_COMPRA < p_FECHA_HASTA + 1)
                                    OR
                                    (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ANCOMPORDC', 'ODC')
                                     AND A.FECHA_UPD >= p_FECHA_DESDE
                                     AND A.FECHA_UPD < p_FECHA_HASTA + 1)
                               )
                             GROUP BY A.CODIGO_ORDEN_COMPRA,
                                      A.NUMERO_ORDEN_COMPRA,
                                      A.FECHA_ORDEN_COMPRA,
                                      A.CODIGO_PROVEEDOR,
                                      B.NOMBRE_PROVEEDOR,
                                      NULL,
                                      NVL(CA.CODIGO_MAYOR, 9),
                                      NVL(CA.CODIGO_AUXILIAR, 1094),
                                      NVL(CA.SEGMENTO1, '001'),
                                      NVL(CA.SEGMENTO2, '000'),
                                      NVL(CA.DENOMINACION, '999'),
                                      NVL(CM.NUMERO_MAYOR, '300'),
                                      NULL,
                                      A.MOTIVO,
                                      'OC'
                            UNION ALL
                            SELECT A.CODIGO_ORDEN_COMPRA CODIGO_COMPROMISO,
                                   A.NUMERO_ORDEN_COMPRA NUMERO_COMPROMISO,
                                   A.FECHA_ORDEN_COMPRA FECHA_COMPROMISO,
                                   A.CODIGO_PROVEEDOR,
                                   B.NOMBRE_PROVEEDOR,
                                   NULL FINANCIADO_POR,
                                   NVL(SUM(D.MONTO - D.MONTO_ANULADO), 0) MONTO_COMPROMISO,
                                   NVL(CA.CODIGO_MAYOR, 3) CODIGO_MAYOR,
                                   NVL(CA.CODIGO_AUXILIAR, 1094) CODIGO_AUXILIAR,
                                   NVL(CA.SEGMENTO1, '001') SEGMENTO1,
                                   NVL(CA.SEGMENTO2, '000') SEGMENTO2,
                                   NVL(CA.DENOMINACION, '999') DENOMINACION,
                                   NVL(CM.NUMERO_MAYOR, '103') NUMERO_MAYOR,
                                   NULL STATUS,
                                   A.MOTIVO,
                                   'OC' TIPO_COMPROMISO
                              FROM ADM.ADM_ORDEN_COMPRA A,
                                   ADM.ADM_PROVEEDORES B,
                                   ADM.ADM_DETALLE_ORDEN_COMPRA C,
                                   ADM.ADM_PUC_ORDEN_COMPRA D,
                                   CNT.CNT_MAYORES CM,
                                   CNT.CNT_AUXILIARES CA
                             WHERE A.CODIGO_PRESUPUESTO = v_codigo_presupuesto
                               AND B.CODIGO_PROVEEDOR = A.CODIGO_PROVEEDOR
                               AND C.CODIGO_ORDEN_COMPRA(+) = A.CODIGO_ORDEN_COMPRA
                               AND D.CODIGO_DETALLE_ORDEN_COMPRA(+) = C.CODIGO_DETALLE_ORDEN_COMPRA
                               AND CM.CODIGO_MAYOR = 3
                               AND CM.CODIGO_MAYOR = CA.CODIGO_MAYOR
                               AND CA.CODIGO_PROVEEDOR = B.CODIGO_PROVEEDOR
                               AND (
                                    (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('COMPORDCOM', 'ODC')
                                     AND A.FECHA_ORDEN_COMPRA >= p_FECHA_DESDE
                                     AND A.FECHA_ORDEN_COMPRA < p_FECHA_HASTA + 1)
                                    OR
                                    (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ANCOMPORDC', 'ODC')
                                     AND A.FECHA_UPD >= p_FECHA_DESDE
                                     AND A.FECHA_UPD < p_FECHA_HASTA + 1)
                               )
                             GROUP BY A.CODIGO_ORDEN_COMPRA,
                                      A.NUMERO_ORDEN_COMPRA,
                                      A.FECHA_ORDEN_COMPRA,
                                      A.CODIGO_PROVEEDOR,
                                      B.NOMBRE_PROVEEDOR,
                                      NULL,
                                      NVL(CA.CODIGO_MAYOR, 3),
                                      NVL(CA.CODIGO_AUXILIAR, 1094),
                                      NVL(CA.SEGMENTO1, '001'),
                                      NVL(CA.SEGMENTO2, '000'),
                                      NVL(CA.DENOMINACION, '999'),
                                      NVL(CM.NUMERO_MAYOR, '103'),
                                      NULL,
                                      A.MOTIVO,
                                      'OC'
                      ) x
                     WHERE NVL(x.MONTO_COMPROMISO, 0) <> 0
                     ORDER BY x.TIPO_COMPROMISO, x.CODIGO_COMPROMISO, x.MONTO_COMPROMISO DESC
              ) ordered_rows
        ) LOOP
            CNT.SP_CNT_DET_INS(
                p_CODIGO_COMPROBANTE,
                line.CODIGO_MAYOR,
                line.CODIGO_AUXILIAR,
                line.REFERENCIA1,
                line.REFERENCIA2,
                line.REFERENCIA3,
                line.DESCRIPCION,
                -line.MONTO, -- API/WORK -> Oracle: positive=Debito
                p_USUARIO_ID,
                p_CODIGO_EMPRESA,
                v_codigo_detalle,
                v_det_message,
                1
            );

            IF NVL(v_det_message, 'Error') <> 'Success' THEN
                cleanup_comprobante;
                p_Message := v_det_message;
                RETURN;
            END IF;

            p_CANTIDAD_LINEAS := p_CANTIDAD_LINEAS + 1;
            p_TOTAL_DEBE := p_TOTAL_DEBE + CASE WHEN line.MONTO < 0 THEN ABS(line.MONTO) ELSE 0 END;
            p_TOTAL_HABER := p_TOTAL_HABER + CASE WHEN line.MONTO > 0 THEN line.MONTO ELSE 0 END;
        END LOOP;

        IF p_CANTIDAD_LINEAS = 0 THEN
            cleanup_comprobante;
            p_CODIGO_COMPROBANTE := 0;
            p_NUMERO_COMPROBANTE := NULL;
            p_Message := 'El proceso automatico no genero lineas para el rango indicado.';
            RETURN;
        END IF;

        SELECT NVL(SUM(CASE WHEN MONTO > 0 THEN MONTO ELSE 0 END), 0),
               NVL(SUM(CASE WHEN MONTO < 0 THEN -MONTO ELSE 0 END), 0)
          INTO p_TOTAL_DEBE, p_TOTAL_HABER FROM CNT.CNT_DETALLE_COMPROBANTE
         WHERE CODIGO_COMPROBANTE = p_CODIGO_COMPROBANTE AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

        SELECT HABILITADO INTO v_dummy FROM CNT.CNT_CMP_CTL WHERE CODIGO = 1;
        IF p_TOTAL_DEBE <> p_TOTAL_HABER AND v_dummy <> 1 THEN
            cleanup_comprobante;
            p_Message := 'La generacion pendiente requiere completar la instalacion y validacion CNT 37.';
            RETURN;
        END IF;

        UPDATE CNT.CNT_COMPROBANTES
           SET EXTRA1 = 'AUTOMATICO',
               ESTADO_CMP = CASE WHEN p_TOTAL_DEBE = p_TOTAL_HABER THEN 'FINALIZADO' ELSE 'PENDIENTE' END,
               USUARIO_UPD = p_USUARIO_ID,
               FECHA_UPD = SYSDATE
         WHERE CODIGO_COMPROBANTE = p_CODIGO_COMPROBANTE
           AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

        p_Message := 'Success';
        RETURN;
    END IF;

    IF p_ORIGEN_ID IN (
        CNT.CNT_DESCRIPTIVA_ID('COMPCONTOB', 'ODC'),
        CNT.CNT_DESCRIPTIVA_ID('ANCOCONTOB', 'ODC')
    ) THEN
        IF v_codigo_presupuesto IS NULL THEN
            p_Message := 'No existe presupuesto vigente para el rango indicado.';
            RETURN;
        END IF;

        CNT.SP_CNT_CMP_INS(
            p_CODIGO_PERIODO,
            p_TIPO_COMPROBANTE_ID,
            p_FECHA_COMPROBANTE,
            p_ORIGEN_ID,
            p_OBSERVACION,
            p_USUARIO_ID,
            p_CODIGO_EMPRESA,
            p_CODIGO_COMPROBANTE,
            v_cmp_message
        );

        IF NVL(v_cmp_message, 'Error') <> 'Success' OR NVL(p_CODIGO_COMPROBANTE, 0) = 0 THEN
            p_Message := v_cmp_message;
            RETURN;
        END IF;

        CNT.CNT_CMP_GUARD.ALLOW_WRITE(p_CODIGO_COMPROBANTE, p_CODIGO_EMPRESA);
        UPDATE CNT.CNT_COMPROBANTES SET ESTADO_CMP = 'PENDIENTE', EXTRA1 = 'AUTOMATICO'
         WHERE CODIGO_COMPROBANTE = p_CODIGO_COMPROBANTE AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

        SELECT NUMERO_COMPROBANTE
          INTO p_NUMERO_COMPROBANTE
          FROM CNT.CNT_COMPROBANTES
         WHERE CODIGO_COMPROBANTE = p_CODIGO_COMPROBANTE
           AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

        FOR line IN (
            SELECT ordered_rows.CODIGO_MAYOR,
                   ordered_rows.CODIGO_AUXILIAR,
                   ordered_rows.REFERENCIA1,
                   ordered_rows.REFERENCIA2,
                   ordered_rows.REFERENCIA3,
                   ordered_rows.DESCRIPCION,
                   ordered_rows.MONTO
              FROM (
                    SELECT x.CODIGO_MAYOR,
                           x.CODIGO_AUXILIAR,
                           SUBSTR(x.NUMERO_COMPROMISO, 1, 20) REFERENCIA1,
                           TO_CHAR(x.FECHA_COMPROMISO, 'DD/MM/RRRR') REFERENCIA2,
                           NULL REFERENCIA3,
                           SUBSTRB('(' || x.TIPO_COMPROMISO || ') ' || x.MOTIVO, 1, 200) DESCRIPCION,
                           CASE
                               WHEN p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('COMPCONTOB', 'ODC') AND x.CODIGO_MAYOR = 9 THEN -ABS(NVL(x.MONTO_COMPROMISO, 0))
                               WHEN p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('COMPCONTOB', 'ODC') THEN ABS(NVL(x.MONTO_COMPROMISO, 0))
                               WHEN x.CODIGO_MAYOR = 9 THEN ABS(NVL(x.MONTO_COMPROMISO, 0))
                               ELSE -ABS(NVL(x.MONTO_COMPROMISO, 0))
                           END MONTO,
                           x.TIPO_COMPROMISO,
                           x.CODIGO_COMPROMISO,
                           x.MONTO_COMPROMISO
                      FROM (
                            SELECT A.CODIGO_CONTRATO CODIGO_COMPROMISO,
                                   A.NUMERO_CONTRATO NUMERO_COMPROMISO,
                                   A.FECHA_CONTRATO FECHA_COMPROMISO,
                                   A.CODIGO_PROVEEDOR,
                                   B.NOMBRE_PROVEEDOR,
                                   NULL FINANCIADO_POR,
                                   A.MONTO_CONTRATO MONTO_COMPROMISO,
                                   NVL(CA.CODIGO_MAYOR, 9) CODIGO_MAYOR,
                                   NVL(CA.CODIGO_AUXILIAR, 1094) CODIGO_AUXILIAR,
                                   NVL(CA.SEGMENTO1, '001') SEGMENTO1,
                                   NVL(CA.SEGMENTO2, '000') SEGMENTO2,
                                   NVL(CA.DENOMINACION, '999') DENOMINACION,
                                   NVL(CM.NUMERO_MAYOR, '300') NUMERO_MAYOR,
                                   NULL STATUS,
                                   SUBSTRB(A.OBRA, 1, 200) MOTIVO,
                                   'CO' TIPO_COMPROMISO
                              FROM ADM.ADM_CONTRATOS A,
                                   ADM.ADM_PROVEEDORES B,
                                   CNT.CNT_MAYORES CM,
                                   CNT.CNT_AUXILIARES CA
                             WHERE A.CODIGO_PRESUPUESTO = v_codigo_presupuesto
                               AND B.CODIGO_PROVEEDOR = A.CODIGO_PROVEEDOR
                               AND CM.CODIGO_MAYOR = 9
                               AND CM.CODIGO_MAYOR = CA.CODIGO_MAYOR
                               AND CA.CODIGO_PROVEEDOR = B.CODIGO_PROVEEDOR
                               AND A.MONTO_CONTRATO <> 0
                               AND (
                                    (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('COMPCONTOB', 'ODC')
                                     AND A.STATUS <> 'AN'
                                     AND TRUNC(A.FECHA_INS) >= p_FECHA_DESDE
                                     AND TRUNC(A.FECHA_INS) <= p_FECHA_HASTA)
                                    OR
                                    (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ANCOCONTOB', 'ODC')
                                     AND A.STATUS = 'AN'
                                     AND TRUNC(A.FECHA_INS) >= p_FECHA_DESDE
                                     AND TRUNC(A.FECHA_INS) <= p_FECHA_HASTA)
                               )
                               AND EXISTS (
                                    SELECT 1
                                      FROM ADM.ADM_PUC_CONTRATO APC
                                     WHERE APC.CODIGO_CONTRATO = A.CODIGO_CONTRATO
                               )
                            UNION ALL
                            SELECT A.CODIGO_CONTRATO CODIGO_COMPROMISO,
                                   A.NUMERO_CONTRATO NUMERO_COMPROMISO,
                                   A.FECHA_CONTRATO FECHA_COMPROMISO,
                                   A.CODIGO_PROVEEDOR,
                                   B.NOMBRE_PROVEEDOR,
                                   NULL FINANCIADO_POR,
                                   A.MONTO_CONTRATO MONTO_COMPROMISO,
                                   NVL(CA.CODIGO_MAYOR, 3) CODIGO_MAYOR,
                                   NVL(CA.CODIGO_AUXILIAR, 1094) CODIGO_AUXILIAR,
                                   NVL(CA.SEGMENTO1, '001') SEGMENTO1,
                                   NVL(CA.SEGMENTO2, '000') SEGMENTO2,
                                   NVL(CA.DENOMINACION, '999') DENOMINACION,
                                   NVL(CM.NUMERO_MAYOR, '103') NUMERO_MAYOR,
                                   NULL STATUS,
                                   SUBSTRB(A.OBRA, 1, 200) MOTIVO,
                                   'CO' TIPO_COMPROMISO
                              FROM ADM.ADM_CONTRATOS A,
                                   ADM.ADM_PROVEEDORES B,
                                   CNT.CNT_MAYORES CM,
                                   CNT.CNT_AUXILIARES CA
                             WHERE A.CODIGO_PRESUPUESTO = v_codigo_presupuesto
                               AND B.CODIGO_PROVEEDOR = A.CODIGO_PROVEEDOR
                               AND CM.CODIGO_MAYOR = 3
                               AND CM.CODIGO_MAYOR = CA.CODIGO_MAYOR
                               AND CA.CODIGO_PROVEEDOR = B.CODIGO_PROVEEDOR
                               AND A.MONTO_CONTRATO <> 0
                               AND (
                                    (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('COMPCONTOB', 'ODC')
                                     AND A.STATUS <> 'AN'
                                     AND TRUNC(A.FECHA_INS) >= p_FECHA_DESDE
                                     AND TRUNC(A.FECHA_INS) <= p_FECHA_HASTA)
                                    OR
                                    (p_ORIGEN_ID = CNT.CNT_DESCRIPTIVA_ID('ANCOCONTOB', 'ODC')
                                     AND A.STATUS = 'AN'
                                     AND A.FECHA_UPD >= p_FECHA_DESDE
                                     AND A.FECHA_UPD < p_FECHA_HASTA + 1
                                     AND TRUNC(A.FECHA_INS) >= p_FECHA_DESDE
                                     AND TRUNC(A.FECHA_INS) <= p_FECHA_HASTA)
                               )
                               AND EXISTS (
                                    SELECT 1
                                      FROM ADM.ADM_PUC_CONTRATO APC
                                     WHERE APC.CODIGO_CONTRATO = A.CODIGO_CONTRATO
                               )
                      ) x
                     WHERE NVL(x.MONTO_COMPROMISO, 0) <> 0
                     ORDER BY x.TIPO_COMPROMISO, x.CODIGO_COMPROMISO, x.MONTO_COMPROMISO DESC
              ) ordered_rows
        ) LOOP
            CNT.SP_CNT_DET_INS(
                p_CODIGO_COMPROBANTE,
                line.CODIGO_MAYOR,
                line.CODIGO_AUXILIAR,
                line.REFERENCIA1,
                line.REFERENCIA2,
                line.REFERENCIA3,
                line.DESCRIPCION,
                -line.MONTO, -- API/WORK -> Oracle: positive=Debito
                p_USUARIO_ID,
                p_CODIGO_EMPRESA,
                v_codigo_detalle,
                v_det_message,
                1
            );

            IF NVL(v_det_message, 'Error') <> 'Success' THEN
                cleanup_comprobante;
                p_Message := v_det_message;
                RETURN;
            END IF;

            p_CANTIDAD_LINEAS := p_CANTIDAD_LINEAS + 1;
            p_TOTAL_DEBE := p_TOTAL_DEBE + CASE WHEN line.MONTO < 0 THEN ABS(line.MONTO) ELSE 0 END;
            p_TOTAL_HABER := p_TOTAL_HABER + CASE WHEN line.MONTO > 0 THEN line.MONTO ELSE 0 END;
        END LOOP;

        IF p_CANTIDAD_LINEAS = 0 THEN
            cleanup_comprobante;
            p_CODIGO_COMPROBANTE := 0;
            p_NUMERO_COMPROBANTE := NULL;
            p_Message := 'El proceso automatico no genero lineas para el rango indicado.';
            RETURN;
        END IF;

        SELECT NVL(SUM(CASE WHEN MONTO > 0 THEN MONTO ELSE 0 END), 0),
               NVL(SUM(CASE WHEN MONTO < 0 THEN -MONTO ELSE 0 END), 0)
          INTO p_TOTAL_DEBE, p_TOTAL_HABER FROM CNT.CNT_DETALLE_COMPROBANTE
         WHERE CODIGO_COMPROBANTE = p_CODIGO_COMPROBANTE AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

        SELECT HABILITADO INTO v_dummy FROM CNT.CNT_CMP_CTL WHERE CODIGO = 1;
        IF p_TOTAL_DEBE <> p_TOTAL_HABER AND v_dummy <> 1 THEN
            cleanup_comprobante;
            p_Message := 'La generacion pendiente requiere completar la instalacion y validacion CNT 37.';
            RETURN;
        END IF;

        UPDATE CNT.CNT_COMPROBANTES
           SET EXTRA1 = 'AUTOMATICO',
               ESTADO_CMP = CASE WHEN p_TOTAL_DEBE = p_TOTAL_HABER THEN 'FINALIZADO' ELSE 'PENDIENTE' END,
               USUARIO_UPD = p_USUARIO_ID,
               FECHA_UPD = SYSDATE
         WHERE CODIGO_COMPROBANTE = p_CODIGO_COMPROBANTE
           AND CODIGO_EMPRESA = p_CODIGO_EMPRESA;

        p_Message := 'Success';
        RETURN;
    END IF;

    p_Message := 'El origen seleccionado no tiene reglas de generacion automatica configuradas.';
EXCEPTION
    WHEN NO_DATA_FOUND THEN
        cleanup_comprobante;
        p_Message := 'No existe un periodo abierto para la fecha del comprobante.';
    WHEN OTHERS THEN
        cleanup_comprobante;
        p_Message := SQLERRM;
END;
/

-- END R58 OBJECT SP_CNT_AUT_CONF

DECLARE
    v_count NUMBER;
BEGIN
    FOR r IN (SELECT LINE,POSITION,TEXT FROM USER_ERRORS
               WHERE NAME='SP_CNT_AUT_CONF' AND TYPE='PROCEDURE' AND ATTRIBUTE='ERROR'
               ORDER BY SEQUENCE) LOOP
        DBMS_OUTPUT.PUT_LINE('LINEA '||r.LINE||':'||r.POSITION||' '||r.TEXT);
    END LOOP;
    SELECT COUNT(*) INTO v_count FROM USER_OBJECTS
     WHERE OBJECT_NAME='SP_CNT_AUT_CONF' AND OBJECT_TYPE='PROCEDURE' AND STATUS='VALID';
    IF v_count<>1 THEN RAISE_APPLICATION_ERROR(-20058,'R58: SP_CNT_AUT_CONF INVALID. Usar respaldo generado.'); END IF;
END;
/

-- BEGIN R58 OBJECT SP_CNT_CMP_GET_ID

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_CMP_GET_ID (
    p_CODIGO_COMPROBANTE IN NUMBER,
    p_CODIGO_EMPRESA     IN NUMBER,
    p_ResultSet          OUT SYS_REFCURSOR,
    p_Message            OUT VARCHAR2
) AS
BEGIN
    OPEN p_ResultSet FOR
        SELECT c.CODIGO_COMPROBANTE,
               c.CODIGO_PERIODO,
               p.NOMBRE_PERIODO AS PERIODO,
               c.TIPO_COMPROBANTE_ID,
               t.DESCRIPCION AS TIPO_COMPROBANTE,
               c.NUMERO_COMPROBANTE,
               c.FECHA_COMPROBANTE,
               c.ORIGEN_ID,
               o.DESCRIPCION AS ORIGEN,
               c.OBSERVACION,
               NVL(d.TOTAL_DEBE, 0) AS TOTAL_DEBE,
               NVL(d.TOTAL_HABER, 0) AS TOTAL_HABER,
               CASE WHEN NVL(c.EXTRA1, ' ') = 'AUTOMATICO' THEN 1 ELSE 0 END AS ES_AUTOMATICO,
               c.ESTADO_CMP, c.AJUSTE_MANUAL, c.VERSION_CMP,
               c.CODIGO_EMPRESA
          FROM CNT.CNT_COMPROBANTES c
          LEFT JOIN CNT.CNT_PERIODOS p ON p.CODIGO_PERIODO = c.CODIGO_PERIODO
          LEFT JOIN CNT.CNT_DESCRIPTIVAS t ON t.DESCRIPCION_ID = c.TIPO_COMPROBANTE_ID
          LEFT JOIN CNT.CNT_DESCRIPTIVAS o ON o.DESCRIPCION_ID = c.ORIGEN_ID
          LEFT JOIN (
                SELECT CODIGO_COMPROBANTE,
                       SUM(CASE WHEN MONTO > 0 THEN MONTO ELSE 0 END) AS TOTAL_DEBE,
                       SUM(CASE WHEN MONTO < 0 THEN -MONTO ELSE 0 END) AS TOTAL_HABER
                  FROM CNT.CNT_DETALLE_COMPROBANTE
                 WHERE CODIGO_EMPRESA = p_CODIGO_EMPRESA
                 GROUP BY CODIGO_COMPROBANTE
          ) d ON d.CODIGO_COMPROBANTE = c.CODIGO_COMPROBANTE
         WHERE c.CODIGO_COMPROBANTE = p_CODIGO_COMPROBANTE
           AND c.CODIGO_EMPRESA = p_CODIGO_EMPRESA;

    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SQLERRM;
END;
/

-- END R58 OBJECT SP_CNT_CMP_GET_ID

DECLARE
    v_count NUMBER;
BEGIN
    FOR r IN (SELECT LINE,POSITION,TEXT FROM USER_ERRORS
               WHERE NAME='SP_CNT_CMP_GET_ID' AND TYPE='PROCEDURE' AND ATTRIBUTE='ERROR'
               ORDER BY SEQUENCE) LOOP
        DBMS_OUTPUT.PUT_LINE('LINEA '||r.LINE||':'||r.POSITION||' '||r.TEXT);
    END LOOP;
    SELECT COUNT(*) INTO v_count FROM USER_OBJECTS
     WHERE OBJECT_NAME='SP_CNT_CMP_GET_ID' AND OBJECT_TYPE='PROCEDURE' AND STATUS='VALID';
    IF v_count<>1 THEN RAISE_APPLICATION_ERROR(-20058,'R58: SP_CNT_CMP_GET_ID INVALID. Usar respaldo generado.'); END IF;
END;
/

-- BEGIN R58 OBJECT SP_CNT_CMP_GET_ALL

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_CMP_GET_ALL (
    p_PageSize            IN NUMBER,
    p_PageNumber          IN NUMBER,
    p_SearchText          IN VARCHAR2,
    p_CODIGO_PERIODO      IN NUMBER,
    p_ORIGEN_ID           IN NUMBER,
    p_FECHA_DESDE         IN DATE,
    p_FECHA_HASTA         IN DATE,
    p_ES_AUTOMATICO       IN NUMBER,
    p_CODIGO_EMPRESA      IN NUMBER,
    p_ResultSet           OUT SYS_REFCURSOR,
    p_Message             OUT VARCHAR2,
    p_TotalRecords        OUT NUMBER,
    p_TotalPages          OUT NUMBER,
    p_ESTADO_CMP          IN VARCHAR2 DEFAULT NULL,
    p_TIPO_COMPROBANTE_ID IN NUMBER DEFAULT NULL,
    p_TOTAL_DEBE          OUT NUMBER,
    p_TOTAL_HABER         OUT NUMBER
) AS
    v_page_size   NUMBER := NVL(NULLIF(p_PageSize, 0), 10);
    v_page_number NUMBER := NVL(NULLIF(p_PageNumber, 0), 1);
    v_start_row   NUMBER;
    v_end_row     NUMBER;
BEGIN
    v_start_row := ((v_page_number - 1) * v_page_size) + 1;
    v_end_row := v_page_number * v_page_size;

    SELECT COUNT(DISTINCT c.CODIGO_COMPROBANTE),
           NVL(SUM(CASE WHEN d.MONTO > 0 THEN d.MONTO ELSE 0 END), 0),
           NVL(SUM(CASE WHEN d.MONTO < 0 THEN -d.MONTO ELSE 0 END), 0)
      INTO p_TotalRecords, p_TOTAL_DEBE, p_TOTAL_HABER
      FROM CNT.CNT_COMPROBANTES c
      LEFT JOIN CNT.CNT_PERIODOS p ON p.CODIGO_PERIODO = c.CODIGO_PERIODO
      LEFT JOIN CNT.CNT_DESCRIPTIVAS t ON t.DESCRIPCION_ID = c.TIPO_COMPROBANTE_ID
      LEFT JOIN CNT.CNT_DESCRIPTIVAS o ON o.DESCRIPCION_ID = c.ORIGEN_ID
      LEFT JOIN CNT.CNT_DETALLE_COMPROBANTE d
        ON d.CODIGO_COMPROBANTE = c.CODIGO_COMPROBANTE
       AND d.CODIGO_EMPRESA = p_CODIGO_EMPRESA
     WHERE c.CODIGO_EMPRESA = p_CODIGO_EMPRESA
       AND (p_ESTADO_CMP IS NULL OR c.ESTADO_CMP = p_ESTADO_CMP)
       AND (p_CODIGO_PERIODO IS NULL OR c.CODIGO_PERIODO = p_CODIGO_PERIODO)
       AND (p_ORIGEN_ID IS NULL OR c.ORIGEN_ID = p_ORIGEN_ID)
       AND (p_TIPO_COMPROBANTE_ID IS NULL OR c.TIPO_COMPROBANTE_ID = p_TIPO_COMPROBANTE_ID)
       AND (p_FECHA_DESDE IS NULL OR c.FECHA_COMPROBANTE >= p_FECHA_DESDE)
       AND (p_FECHA_HASTA IS NULL OR c.FECHA_COMPROBANTE < p_FECHA_HASTA + 1)
       AND (p_ES_AUTOMATICO IS NULL
            OR (p_ES_AUTOMATICO = 1 AND NVL(c.EXTRA1, ' ') = 'AUTOMATICO')
            OR (p_ES_AUTOMATICO = 0 AND NVL(c.EXTRA1, ' ') <> 'AUTOMATICO'))
       AND (p_SearchText IS NULL
            OR UPPER(c.NUMERO_COMPROBANTE) LIKE '%' || UPPER(p_SearchText) || '%'
            OR UPPER(c.OBSERVACION) LIKE '%' || UPPER(p_SearchText) || '%'
            OR UPPER(t.DESCRIPCION) LIKE '%' || UPPER(p_SearchText) || '%'
            OR UPPER(o.DESCRIPCION) LIKE '%' || UPPER(p_SearchText) || '%');

    p_TotalPages := CASE WHEN p_TotalRecords = 0 THEN 0 ELSE CEIL(p_TotalRecords / v_page_size) END;

    OPEN p_ResultSet FOR
        SELECT *
          FROM (
                SELECT q.*, ROWNUM rn
                  FROM (
                        SELECT c.CODIGO_COMPROBANTE,
                               c.CODIGO_PERIODO,
                               p.NOMBRE_PERIODO AS PERIODO,
                               c.TIPO_COMPROBANTE_ID,
                               t.DESCRIPCION AS TIPO_COMPROBANTE,
                               c.NUMERO_COMPROBANTE,
                               c.FECHA_COMPROBANTE,
                               c.ORIGEN_ID,
                               o.DESCRIPCION AS ORIGEN,
                               c.OBSERVACION,
                               NVL(d.TOTAL_DEBE, 0) AS TOTAL_DEBE,
                               NVL(d.TOTAL_HABER, 0) AS TOTAL_HABER,
                               CASE WHEN NVL(c.EXTRA1, ' ') = 'AUTOMATICO' THEN 1 ELSE 0 END AS ES_AUTOMATICO,
                               c.ESTADO_CMP, c.AJUSTE_MANUAL, c.VERSION_CMP,
                               c.CODIGO_EMPRESA
                          FROM CNT.CNT_COMPROBANTES c
                          LEFT JOIN CNT.CNT_PERIODOS p ON p.CODIGO_PERIODO = c.CODIGO_PERIODO
                          LEFT JOIN CNT.CNT_DESCRIPTIVAS t ON t.DESCRIPCION_ID = c.TIPO_COMPROBANTE_ID
                          LEFT JOIN CNT.CNT_DESCRIPTIVAS o ON o.DESCRIPCION_ID = c.ORIGEN_ID
                          LEFT JOIN (
                                SELECT CODIGO_COMPROBANTE,
                                       SUM(CASE WHEN MONTO > 0 THEN MONTO ELSE 0 END) AS TOTAL_DEBE,
                                       SUM(CASE WHEN MONTO < 0 THEN -MONTO ELSE 0 END) AS TOTAL_HABER
                                  FROM CNT.CNT_DETALLE_COMPROBANTE
                                 WHERE CODIGO_EMPRESA = p_CODIGO_EMPRESA
                                 GROUP BY CODIGO_COMPROBANTE
                          ) d ON d.CODIGO_COMPROBANTE = c.CODIGO_COMPROBANTE
                         WHERE c.CODIGO_EMPRESA = p_CODIGO_EMPRESA
       AND (p_ESTADO_CMP IS NULL OR c.ESTADO_CMP = p_ESTADO_CMP)
                           AND (p_CODIGO_PERIODO IS NULL OR c.CODIGO_PERIODO = p_CODIGO_PERIODO)
                           AND (p_ORIGEN_ID IS NULL OR c.ORIGEN_ID = p_ORIGEN_ID)
       AND (p_TIPO_COMPROBANTE_ID IS NULL OR c.TIPO_COMPROBANTE_ID = p_TIPO_COMPROBANTE_ID)
                           AND (p_FECHA_DESDE IS NULL OR c.FECHA_COMPROBANTE >= p_FECHA_DESDE)
                           AND (p_FECHA_HASTA IS NULL OR c.FECHA_COMPROBANTE < p_FECHA_HASTA + 1)
                           AND (p_ES_AUTOMATICO IS NULL
                                OR (p_ES_AUTOMATICO = 1 AND NVL(c.EXTRA1, ' ') = 'AUTOMATICO')
                                OR (p_ES_AUTOMATICO = 0 AND NVL(c.EXTRA1, ' ') <> 'AUTOMATICO'))
                           AND (p_SearchText IS NULL
                                OR UPPER(c.NUMERO_COMPROBANTE) LIKE '%' || UPPER(p_SearchText) || '%'
                                OR UPPER(c.OBSERVACION) LIKE '%' || UPPER(p_SearchText) || '%'
                                OR UPPER(t.DESCRIPCION) LIKE '%' || UPPER(p_SearchText) || '%'
                                OR UPPER(o.DESCRIPCION) LIKE '%' || UPPER(p_SearchText) || '%')
                         ORDER BY c.FECHA_COMPROBANTE DESC, c.NUMERO_COMPROBANTE DESC
                  ) q
                 WHERE ROWNUM <= v_end_row
          )
         WHERE rn >= v_start_row;

    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SQLERRM;
        p_TotalRecords := 0;
        p_TotalPages := 0;
        p_TOTAL_DEBE := 0;
        p_TOTAL_HABER := 0;
END;
/

-- END R58 OBJECT SP_CNT_CMP_GET_ALL

DECLARE
    v_count NUMBER;
BEGIN
    FOR r IN (SELECT LINE,POSITION,TEXT FROM USER_ERRORS
               WHERE NAME='SP_CNT_CMP_GET_ALL' AND TYPE='PROCEDURE' AND ATTRIBUTE='ERROR'
               ORDER BY SEQUENCE) LOOP
        DBMS_OUTPUT.PUT_LINE('LINEA '||r.LINE||':'||r.POSITION||' '||r.TEXT);
    END LOOP;
    SELECT COUNT(*) INTO v_count FROM USER_OBJECTS
     WHERE OBJECT_NAME='SP_CNT_CMP_GET_ALL' AND OBJECT_TYPE='PROCEDURE' AND STATUS='VALID';
    IF v_count<>1 THEN RAISE_APPLICATION_ERROR(-20058,'R58: SP_CNT_CMP_GET_ALL INVALID. Usar respaldo generado.'); END IF;
END;
/

-- BEGIN R58 OBJECT SP_CNT_CAT_DESC_GET

CREATE OR REPLACE PROCEDURE CNT.SP_CNT_CAT_DESC_GET (
    p_TITULO_ID      IN NUMBER,
    p_SEARCH_TEXT    IN VARCHAR2,
    p_CODIGO_EMPRESA IN NUMBER,
    p_ResultSet      OUT SYS_REFCURSOR,
    p_Message        OUT VARCHAR2,
    p_CODIGO_TITULO  IN VARCHAR2 DEFAULT NULL
) AS
BEGIN
    IF p_CODIGO_TITULO IS NOT NULL THEN
        OPEN p_ResultSet FOR
            SELECT A.DESCRIPCION_ID AS ID,
                   A.CODIGO,
                   A.DESCRIPCION,
                   A.EXTRA1,
                   A.EXTRA2,
                   A.EXTRA3
              FROM CNT.CNT_DESCRIPTIVAS A,
                   CNT.CNT_TITULOS B
             WHERE B.CODIGO = p_CODIGO_TITULO
               AND A.TITULO_ID = B.TITULO_ID
               AND (p_SEARCH_TEXT IS NULL
                    OR UPPER(A.CODIGO) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%'
                    OR UPPER(A.DESCRIPCION) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%')
             ORDER BY CASE WHEN p_CODIGO_TITULO = 'ODC'
                           THEN A.DESCRIPCION ELSE A.CODIGO END;
    ELSE
        OPEN p_ResultSet FOR
            SELECT DESCRIPCION_ID AS ID,
                   CODIGO,
                   DESCRIPCION,
                   EXTRA1,
                   EXTRA2,
                   EXTRA3
              FROM CNT.CNT_DESCRIPTIVAS
             WHERE TITULO_ID = p_TITULO_ID
               AND (CODIGO_EMPRESA IS NULL OR CODIGO_EMPRESA = p_CODIGO_EMPRESA)
               AND (p_SEARCH_TEXT IS NULL
                    OR UPPER(CODIGO) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%'
                    OR UPPER(DESCRIPCION) LIKE '%' || UPPER(p_SEARCH_TEXT) || '%')
             ORDER BY CODIGO, DESCRIPCION;

    END IF;

    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SQLERRM;
END;
/

-- END R58 OBJECT SP_CNT_CAT_DESC_GET

DECLARE
    v_count NUMBER;
BEGIN
    FOR r IN (SELECT LINE,POSITION,TEXT FROM USER_ERRORS
               WHERE NAME='SP_CNT_CAT_DESC_GET' AND TYPE='PROCEDURE' AND ATTRIBUTE='ERROR'
               ORDER BY SEQUENCE) LOOP
        DBMS_OUTPUT.PUT_LINE('LINEA '||r.LINE||':'||r.POSITION||' '||r.TEXT);
    END LOOP;
    SELECT COUNT(*) INTO v_count FROM USER_OBJECTS
     WHERE OBJECT_NAME='SP_CNT_CAT_DESC_GET' AND OBJECT_TYPE='PROCEDURE' AND STATUS='VALID';
    IF v_count<>1 THEN RAISE_APPLICATION_ERROR(-20058,'R58: SP_CNT_CAT_DESC_GET INVALID. Usar respaldo generado.'); END IF;
END;
/


PROMPT R58 - Comprobacion final de objetos y contratos.
DECLARE
    v_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_count FROM USER_OBJECTS
     WHERE OBJECT_NAME IN ('SP_CNT_AUT_PRES_GET','SP_CNT_AUT_BUILD_COMP02','SP_CNT_AUT_BUILD_COMP','SP_CNT_AUT_BUILD_OP','SP_CNT_AUT_BUILD_CH_RET','SP_CNT_AUT_PREV','SP_CNT_AUT_CONF','SP_CNT_CMP_GET_ID','SP_CNT_CMP_GET_ALL','SP_CNT_CAT_DESC_GET') AND OBJECT_TYPE='PROCEDURE' AND STATUS='VALID';
    IF v_count<>10 THEN RAISE_APPLICATION_ERROR(-20058,'R58: instalacion incompleta; revisar respaldo.'); END IF;
    SELECT COUNT(*) INTO v_count FROM USER_ARGUMENTS
     WHERE OBJECT_NAME='SP_CNT_CMP_GET_ALL'
       AND ARGUMENT_NAME IN ('P_TOTAL_DEBE','P_TOTAL_HABER') AND IN_OUT='OUT';
    IF v_count<>2 THEN RAISE_APPLICATION_ERROR(-20058,'R58: faltan OUT de totales.'); END IF;
    SELECT COUNT(*) INTO v_count FROM USER_ARGUMENTS
     WHERE OBJECT_NAME='SP_CNT_CAT_DESC_GET' AND ARGUMENT_NAME='P_CODIGO_TITULO' AND IN_OUT='IN';
    IF v_count<>1 THEN RAISE_APPLICATION_ERROR(-20058,'R58: falta codigo ODC/TDC.'); END IF;
    DBMS_OUTPUT.PUT_LINE('R58: 10 procedimientos VALID. Contratos de totales y catalogos verificados.');
    DBMS_OUTPUT.PUT_LINE('Publicar API/frontend compatibles antes de reanudar uso.');
END;
/
SELECT OBJECT_NAME||' '||STATUS FROM USER_OBJECTS
 WHERE OBJECT_NAME IN ('SP_CNT_AUT_PRES_GET','SP_CNT_AUT_BUILD_COMP02','SP_CNT_AUT_BUILD_COMP','SP_CNT_AUT_BUILD_OP','SP_CNT_AUT_BUILD_CH_RET','SP_CNT_AUT_PREV','SP_CNT_AUT_CONF','SP_CNT_CMP_GET_ID','SP_CNT_CMP_GET_ALL','SP_CNT_CAT_DESC_GET') AND OBJECT_TYPE='PROCEDURE' ORDER BY OBJECT_NAME;
SET DEFINE ON
PROMPT Respaldo: R58_RESTAURAR_&&R58_TAG..sql
PROMPT Bitacora: R58_INSTALAR_&&R58_TAG..log
PROMPT R58 - FIN. No se modificaron datos de negocio.
SPOOL OFF
UNDEFINE R58_EMPRESA
UNDEFINE R58_TAG

