PROMPT Validacion de objetos y grants Bienes Municipales

/*
  Ejecutar este script desde los usuarios configurados en:
  - DefaultConnectionBM
  - DefaultConnectionBMC

  La columna ACCESS_STATUS indica:
  - OK: el usuario actual ve el objeto por ownership, grant directo, rol o sinonimo resoluble.
  - NO_ACCESS: el objeto no es visible para el usuario actual.
*/

SET PAGESIZE 200
SET LINESIZE 220

PROMPT Usuario actual
SELECT USER USUARIO_ACTUAL FROM dual;

PROMPT Objetos BM/PRE requeridos por DefaultConnectionBM
WITH req AS (
  SELECT 'BM' owner, 'BM_V_BM1' object_name, 'VIEW' object_type FROM dual UNION ALL
  SELECT 'BM', 'SP_REP_BM1_GET', 'PROCEDURE' FROM dual UNION ALL
  SELECT 'PRE', 'PRE_INDICE_CAT_PRG', 'TABLE' FROM dual
),
vis AS (
  SELECT owner, object_name, object_type
    FROM all_objects
)
SELECT r.owner,
       r.object_name,
       r.object_type,
       CASE WHEN v.object_name IS NULL THEN 'NO_ACCESS' ELSE 'OK' END access_status
  FROM req r
  LEFT JOIN vis v
    ON v.owner = r.owner
   AND v.object_name = r.object_name
   AND v.object_type = r.object_type
 ORDER BY r.owner, r.object_name;

PROMPT Funciones/paquetes utilitarios BM visibles por DefaultConnectionBM
SELECT owner,
       object_name,
       object_type,
       status
  FROM all_objects
 WHERE owner = 'BM'
   AND (object_name LIKE 'BM_PKG_UTIL%' OR object_name = 'NUMBER_PLACA')
 ORDER BY object_name, object_type;

PROMPT Grants visibles para usuario actual sobre objetos BM/PRE
SELECT owner,
       table_name object_name,
       privilege,
       grantor,
       grantee
  FROM all_tab_privs
 WHERE owner IN ('BM', 'PRE')
   AND table_name IN ('BM_V_BM1', 'SP_REP_BM1_GET', 'PRE_INDICE_CAT_PRG')
   AND (grantee = USER OR grantee IN (SELECT granted_role FROM user_role_privs))
 ORDER BY owner, table_name, privilege;

PROMPT Procedimientos BM requeridos por backend
WITH req AS (
  SELECT 'SP_BM1_GET_LIST_ICP' object_name FROM dual UNION ALL
  SELECT 'SP_BM1_GET_PLACAS' FROM dual UNION ALL
  SELECT 'SP_BM1_GET_FIRST_MOV' FROM dual UNION ALL
  SELECT 'SP_BM1_GET_BY_ICP' FROM dual UNION ALL
  SELECT 'SP_BM1_GET_PRODUCT_MOB' FROM dual UNION ALL
  SELECT 'SP_BM_DESC_GET_TIT' FROM dual UNION ALL
  SELECT 'SP_BM_PLACA_CUA_GET' FROM dual UNION ALL
  SELECT 'SP_BM_PLACA_CUA_INS' FROM dual UNION ALL
  SELECT 'SP_BM_PLACA_CUA_DEL' FROM dual UNION ALL
  SELECT 'SP_BM_BIEN_GET_ALL' FROM dual UNION ALL
  SELECT 'SP_BM_BIEN_GET_ID' FROM dual UNION ALL
  SELECT 'SP_BM_BIEN_GET_PLACA' FROM dual UNION ALL
  SELECT 'SP_BM_DET_BIEN_GET' FROM dual UNION ALL
  SELECT 'SP_BM_DET_BIEN_INS' FROM dual UNION ALL
  SELECT 'SP_BM_DET_BIEN_UPD' FROM dual UNION ALL
  SELECT 'SP_BM_BIEN_INS' FROM dual UNION ALL
  SELECT 'SP_BM_BIEN_UPD' FROM dual UNION ALL
  SELECT 'SP_BM_FOTO_GET_PLACA' FROM dual UNION ALL
  SELECT 'SP_BM_FOTO_INS' FROM dual UNION ALL
  SELECT 'SP_BM_FOTO_DEL' FROM dual UNION ALL
  SELECT 'SP_BM_TIT_GET_ALL' FROM dual UNION ALL
  SELECT 'SP_BM_TIT_INS' FROM dual UNION ALL
  SELECT 'SP_BM_TIT_UPD' FROM dual UNION ALL
  SELECT 'SP_BM_DESC_GET_ALL' FROM dual UNION ALL
  SELECT 'SP_BM_DESC_GET_FK' FROM dual UNION ALL
  SELECT 'SP_BM_DESC_INS' FROM dual UNION ALL
  SELECT 'SP_BM_DESC_UPD' FROM dual UNION ALL
  SELECT 'SP_BM_CLASIF_GET_ALL' FROM dual UNION ALL
  SELECT 'SP_BM_CLASIF_INS' FROM dual UNION ALL
  SELECT 'SP_BM_CLASIF_UPD' FROM dual UNION ALL
  SELECT 'SP_BM_ART_GET_ALL' FROM dual UNION ALL
  SELECT 'SP_BM_ART_INS' FROM dual UNION ALL
  SELECT 'SP_BM_ART_UPD' FROM dual UNION ALL
  SELECT 'SP_BM_DET_ART_GET' FROM dual UNION ALL
  SELECT 'SP_BM_DET_ART_INS' FROM dual UNION ALL
  SELECT 'SP_BM_DET_ART_UPD' FROM dual UNION ALL
  SELECT 'SP_BM_UBI_GET_ALL' FROM dual UNION ALL
  SELECT 'SP_BM_UBI_GET_ICP' FROM dual UNION ALL
  SELECT 'SP_BM_UBI_GET_ICP_LST' FROM dual UNION ALL
  SELECT 'SP_BM_UBI_INS' FROM dual UNION ALL
  SELECT 'SP_BM_UBI_UPD' FROM dual UNION ALL
  SELECT 'SP_BM_UBI_HIST_GET' FROM dual UNION ALL
  SELECT 'SP_BM_MOV_GET_BIEN' FROM dual UNION ALL
  SELECT 'SP_BM_MOV_INS' FROM dual UNION ALL
  SELECT 'SP_BM_SOL_MOV_GET' FROM dual UNION ALL
  SELECT 'SP_BM_SOL_MOV_INS' FROM dual UNION ALL
  SELECT 'SP_BM_SOL_MOV_APR' FROM dual UNION ALL
  SELECT 'SP_BM_REP_PLACA' FROM dual UNION ALL
  SELECT 'SP_BM_REP_LOTE' FROM dual UNION ALL
  SELECT 'SP_BM_REP_FICHA' FROM dual UNION ALL
  SELECT 'SP_BM_REP_UBI_ICP' FROM dual UNION ALL
  SELECT 'SP_BM_REP_MOV_BIEN' FROM dual UNION ALL
  SELECT 'SP_BM_REP_MOV_FILT' FROM dual UNION ALL
  SELECT 'SP_BM_REP_SOL_MOV' FROM dual UNION ALL
  SELECT 'SP_BM_REP_PROC_MAS' FROM dual UNION ALL
  SELECT 'SP_BM_PROC_MAS_PRE' FROM dual UNION ALL
  SELECT 'SP_BM_PROC_MAS_EJE' FROM dual
),
vis AS (
  SELECT owner, object_name, object_type, status
    FROM all_objects
   WHERE owner = 'BM'
     AND object_type = 'PROCEDURE'
)
SELECT 'BM' owner,
       r.object_name,
       'PROCEDURE' object_type,
       NVL(v.status, 'NO_ACCESS') access_status
  FROM req r
  LEFT JOIN vis v
    ON v.object_name = r.object_name
 ORDER BY r.object_name;

PROMPT Objetos BMC requeridos por DefaultConnectionBMC
WITH req AS (
  SELECT 'BMC' owner, 'BM_CONTEO' object_name, 'TABLE' object_type FROM dual UNION ALL
  SELECT 'BMC', 'BM_CONTEO_DETALLE', 'TABLE' FROM dual UNION ALL
  SELECT 'BMC', 'BM_CONTEO_HISTORICO', 'TABLE' FROM dual UNION ALL
  SELECT 'BMC', 'BM_CONTEO_DETALLE_HISTORICO', 'TABLE' FROM dual UNION ALL
  SELECT 'BMC', 'BM_CONTEO_MOTIVO', 'TABLE' FROM dual UNION ALL
  SELECT 'BMC', 'BM_P_CONTEO', 'PROCEDURE' FROM dual UNION ALL
  SELECT 'BM', 'BM_V_BM1', 'VIEW' FROM dual
),
vis AS (
  SELECT owner, object_name, object_type
    FROM all_objects
)
SELECT r.owner,
       r.object_name,
       r.object_type,
       CASE WHEN v.object_name IS NULL THEN 'NO_ACCESS' ELSE 'OK' END access_status
  FROM req r
  LEFT JOIN vis v
    ON v.owner = r.owner
   AND v.object_name = r.object_name
   AND v.object_type = r.object_type
 ORDER BY r.owner, r.object_name;

PROMPT Grants visibles para usuario actual sobre objetos BMC/BM
SELECT owner,
       table_name object_name,
       privilege,
       grantor,
       grantee
  FROM all_tab_privs
 WHERE owner IN ('BMC', 'BM')
   AND table_name IN (
       'BM_CONTEO',
       'BM_CONTEO_DETALLE',
       'BM_CONTEO_HISTORICO',
       'BM_CONTEO_DETALLE_HISTORICO',
       'BM_CONTEO_MOTIVO',
       'BM_P_CONTEO',
       'BM_V_BM1'
   )
   AND (grantee = USER OR grantee IN (SELECT granted_role FROM user_role_privs))
 ORDER BY owner, table_name, privilege;

PROMPT Procedimientos BMC requeridos por backend
WITH req AS (
  SELECT 'BM_P_CONTEO' object_name FROM dual UNION ALL
  SELECT 'SP_BM_CONTEO_GET_ALL' FROM dual UNION ALL
  SELECT 'SP_BM_CONTEO_INS' FROM dual UNION ALL
  SELECT 'SP_BM_CONTEO_UPD' FROM dual UNION ALL
  SELECT 'SP_BM_CONTEO_DEL' FROM dual UNION ALL
  SELECT 'SP_BM_CONTEO_CERRAR' FROM dual UNION ALL
  SELECT 'SP_BM_CONT_DET_GET' FROM dual UNION ALL
  SELECT 'SP_BM_CONT_DET_CMP' FROM dual UNION ALL
  SELECT 'SP_BM_CONT_DET_UPD' FROM dual UNION ALL
  SELECT 'SP_BM_CONT_DET_REC' FROM dual UNION ALL
  SELECT 'SP_BM_CONT_HIST_GET' FROM dual UNION ALL
  SELECT 'SP_BM_UBI_RESP_GET' FROM dual UNION ALL
  SELECT 'SP_BM_REP_CONT_DIF' FROM dual UNION ALL
  SELECT 'SP_BM_REP_CONT_HIST' FROM dual
),
vis AS (
  SELECT owner, object_name, object_type, status
    FROM all_objects
   WHERE owner = 'BMC'
     AND object_type = 'PROCEDURE'
)
SELECT 'BMC' owner,
       r.object_name,
       'PROCEDURE' object_type,
       NVL(v.status, 'NO_ACCESS') access_status
  FROM req r
  LEFT JOIN vis v
    ON v.object_name = r.object_name
 ORDER BY r.object_name;

PROMPT Validacion menu SIS Bienes Municipales
SELECT m.CODIGO_MENU,
       m.CODIGO_MOD,
       m.CODIGO_PADRE,
       m.TITULO,
       m.PATH,
       m.ACTIVO
  FROM SIS.OSS_MENU m
 WHERE m.CODIGO_MENU BETWEEN 7000 AND 7130
 ORDER BY m.CODIGO_MENU;

PROMPT Fin validacion de objetos y grants Bienes Municipales
