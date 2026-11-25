#!/usr/bin/env python3
"""Build the standalone SQL*Plus R58 production bundle; never connects to a database."""
from pathlib import Path
import hashlib,json,re
ROOT=Path(__file__).resolve().parents[2]
SQL=ROOT/'OssmmasoftVerticalSlice/Features/Contabilidad/Sql'
OUT=SQL/'INSTALL_CNT_R58_PROD.sql'
DOC=ROOT/'Requerimientos/58 - Revision Compronbantes Automaticos/Produccion'
NAMES=['SP_CNT_AUT_PRES_GET','SP_CNT_AUT_BUILD_COMP02','SP_CNT_AUT_BUILD_COMP','SP_CNT_AUT_BUILD_OP','SP_CNT_AUT_BUILD_CH_RET','SP_CNT_AUT_PREV','SP_CNT_AUT_CONF','SP_CNT_CMP_GET_ID','SP_CNT_CMP_GET_ALL','SP_CNT_CAT_DESC_GET']
SOURCES={n:(SQL/(n+'.sql')).read_text() for n in NAMES}
REFS=sorted({(o,n) for source in SOURCES.values() for o,n in re.findall(r'\b(CNT|PRE|ADM|SIS)\.([A-Z][A-Z0-9_]*)',source) if not(o=='CNT' and n in NAMES)})
COLS={
 'CNT_COMPROBANTES':['CODIGO_COMPROBANTE','CODIGO_PERIODO','TIPO_COMPROBANTE_ID','ORIGEN_ID','FECHA_COMPROBANTE','NUMERO_COMPROBANTE','CODIGO_EMPRESA','ESTADO_CMP','AJUSTE_MANUAL','VERSION_CMP','EXTRA1','USUARIO_UPD','FECHA_UPD','OBSERVACION'],
 'CNT_DETALLE_COMPROBANTE':['CODIGO_DETALLE_COMPROBANTE','CODIGO_COMPROBANTE','CODIGO_EMPRESA','CODIGO_MAYOR','CODIGO_AUXILIAR','MONTO'],
 'CNT_CMP_CTL':['CODIGO','HABILITADO'],
 'CNT_AUT_LINE_WRK':['RUN_ID','SECUENCIA','CODIGO_MAYOR','CODIGO_AUXILIAR','REFERENCIA1','REFERENCIA2','REFERENCIA3','DESCRIPCION','MONTO','FECHA_REGISTRO'],
 'CNT_AUXILIARES_PUC':['CODIGO_AUXILIAR','CODIGO_PUC','TIPO_DOCUMENTO_ID','CODIGO_EMPRESA'],
 'CNT_DESCRIPTIVAS':['DESCRIPCION_ID','TITULO_ID','CODIGO','DESCRIPCION','CODIGO_EMPRESA','EXTRA1','EXTRA2','EXTRA3'],
 'CNT_TITULOS':['TITULO_ID','CODIGO'],
 'CNT_PERIODOS':['CODIGO_PERIODO','CODIGO_EMPRESA','FECHA_DESDE','FECHA_HASTA','FECHA_CIERRE','NOMBRE_PERIODO']}
GUARD="""DECLARE
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
/"""
PREFLIGHT=r"""DECLARE
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
__OBJECTS__
__COLUMNS__
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
     WHERE OWNER='CNT' AND TYPE='PROCEDURE' AND LINE=1 AND NAME IN (__NAMES__);
    DBMS_OUTPUT.PUT_LINE('R58: fuentes anteriores disponibles='||v_count);
    SELECT NVL(SUM(LENGTHB(TEXT)),0) INTO v_bytes FROM ALL_SOURCE
     WHERE OWNER='CNT' AND TYPE='PROCEDURE' AND NAME IN (__NAMES__);
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
/""".replace('__OBJECTS__','\n'.join("    objeto('%s','%s');"%x for x in REFS)).replace('__COLUMNS__','\n'.join("    columna('%s','%s');"%(t,c) for t,cols in COLS.items() for c in cols)).replace('__NAMES__',','.join("'"+n+"'" for n in NAMES))
BACKUP=r"""DECLARE
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
__CALLS__
    DBMS_OUTPUT.PUT_LINE('PROMPT R58: fuentes anteriores restauradas; revisar USER_ERRORS y dependencias.');
END;
/""".replace('__CALLS__','\n'.join("    guardar('%s');"%n for n in NAMES))
def validation(name):
    return """DECLARE
    v_count NUMBER;
BEGIN
    FOR r IN (SELECT LINE,POSITION,TEXT FROM USER_ERRORS
               WHERE NAME='%s' AND TYPE='PROCEDURE' AND ATTRIBUTE='ERROR'
               ORDER BY SEQUENCE) LOOP
        DBMS_OUTPUT.PUT_LINE('LINEA '||r.LINE||':'||r.POSITION||' '||r.TEXT);
    END LOOP;
    SELECT COUNT(*) INTO v_count FROM USER_OBJECTS
     WHERE OBJECT_NAME='%s' AND OBJECT_TYPE='PROCEDURE' AND STATUS='VALID';
    IF v_count<>1 THEN RAISE_APPLICATION_ERROR(-20058,'R58: %s INVALID. Usar respaldo generado.'); END IF;
END;
/"""%(name,name,name)
header="""-- R58 - INSTALADOR UNICO DE PRODUCCION / SQL*Plus / Oracle 10g.
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
"""
parts=[header,GUARD,'-- END R58 TARGET GUARD\n-- BEGIN R58 PREFLIGHT',PREFLIGHT,'-- END R58 PREFLIGHT',"""
PROMPT R58 - Prevalidacion aprobada. Generando respaldo de fuentes anteriores.
SPOOL OFF
SPOOL R58_RESTAURAR_&&R58_TAG..sql
-- BEGIN R58 BACKUP WRITER
""",BACKUP,"""-- END R58 BACKUP WRITER
SPOOL OFF
SPOOL R58_INSTALAR_&&R58_TAG..log APPEND
PROMPT R58 - Respaldo generado. Comienza DDL de los 10 procedimientos.
SET DEFINE OFF
"""]
for name,source in SOURCES.items():
    assert len(name)<=30
    assert source.count('CREATE OR REPLACE PROCEDURE CNT.'+name+' (')==1,name
    parts+=['-- BEGIN R58 OBJECT '+name,source.rstrip(),'-- END R58 OBJECT '+name,validation(name)]
parts.append("""
PROMPT R58 - Comprobacion final de objetos y contratos.
DECLARE
    v_count NUMBER;
BEGIN
    SELECT COUNT(*) INTO v_count FROM USER_OBJECTS
     WHERE OBJECT_NAME IN (__NAMES__) AND OBJECT_TYPE='PROCEDURE' AND STATUS='VALID';
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
 WHERE OBJECT_NAME IN (__NAMES__) AND OBJECT_TYPE='PROCEDURE' ORDER BY OBJECT_NAME;
SET DEFINE ON
PROMPT Respaldo: R58_RESTAURAR_&&R58_TAG..sql
PROMPT Bitacora: R58_INSTALAR_&&R58_TAG..log
PROMPT R58 - FIN. No se modificaron datos de negocio.
SPOOL OFF
UNDEFINE R58_EMPRESA
UNDEFINE R58_TAG
""".replace('__NAMES__',','.join("'"+n+"'" for n in NAMES)))
content='\n\n'.join(parts)+'\n';OUT.write_text(content)
manifest={'Script':str(OUT.relative_to(ROOT)),'ScriptSha256':hashlib.sha256(content.encode()).hexdigest(),'Bytes':len(content.encode()),'Objects':[{'Name':n,'File':str((SQL/(n+'.sql')).relative_to(ROOT)),'Sha256':hashlib.sha256(s.encode()).hexdigest()} for n,s in SOURCES.items()],'ExternalDependencies':['.'.join(x) for x in REFS],'SelfContained':True,'DeploysApplication':False,'InstallsPrerequisites':False,'BusinessDataWritesOnInstall':False}
DOC.mkdir(parents=True,exist_ok=True);(DOC/'Manifiesto.json').write_text(json.dumps(manifest,indent=2)+'\n')
if __name__=='__main__':print(json.dumps({'Script':str(OUT),'Objects':len(NAMES),'Bytes':manifest['Bytes'],'Sha256':manifest['ScriptSha256']}))
