PROMPT Grants runtime Bienes Municipales

/*
  Ejecutar con un usuario DBA o con privilegio GRANT ANY OBJECT PRIVILEGE.

  Ajustar estos usuarios segun appsettings del ambiente:
  - BM_RUNTIME_USER: usuario configurado en DefaultConnectionBM.
  - BMC_RUNTIME_USER: usuario configurado en DefaultConnectionBMC.

  Si el runtime coincide con el owner, el grant se omite para evitar ORA-01749.
*/

SET SERVEROUTPUT ON

DEFINE BM_RUNTIME_USER = BM
DEFINE BMC_RUNTIME_USER = BMC

DECLARE
  v_bm_user  VARCHAR2(30) := UPPER('&&BM_RUNTIME_USER');
  v_bmc_user VARCHAR2(30) := UPPER('&&BMC_RUNTIME_USER');

  PROCEDURE grant_obj(
    p_priv    VARCHAR2,
    p_owner   VARCHAR2,
    p_object  VARCHAR2,
    p_grantee VARCHAR2
  ) IS
  BEGIN
    IF UPPER(p_owner) = UPPER(p_grantee) THEN
      DBMS_OUTPUT.PUT_LINE('SKIP ' || p_priv || ' ON ' || p_owner || '.' || p_object || ' TO ' || p_grantee);
    ELSE
      EXECUTE IMMEDIATE
        'GRANT ' || p_priv || ' ON ' || p_owner || '.' || p_object || ' TO ' || p_grantee;
      DBMS_OUTPUT.PUT_LINE('OK   ' || p_priv || ' ON ' || p_owner || '.' || p_object || ' TO ' || p_grantee);
    END IF;
  END;
BEGIN
  DBMS_OUTPUT.PUT_LINE('DefaultConnectionBM => ' || v_bm_user);
  DBMS_OUTPUT.PUT_LINE('DefaultConnectionBMC => ' || v_bmc_user);

  grant_obj('SELECT', 'PRE', 'PRE_INDICE_CAT_PRG', v_bm_user);
  grant_obj('SELECT', 'BM', 'BM_V_BM1', v_bmc_user);
  grant_obj('EXECUTE', 'BM', 'BM_PKG_UTIL', v_bm_user);
  grant_obj('EXECUTE', 'BM', 'NUMBER_PLACA', v_bm_user);

  grant_obj('EXECUTE', 'BM', 'SP_BM1_GET_LIST_ICP', v_bm_user);
  grant_obj('EXECUTE', 'BM', 'SP_BM1_GET_PLACAS', v_bm_user);
  grant_obj('EXECUTE', 'BM', 'SP_BM1_GET_FIRST_MOV', v_bm_user);
  grant_obj('EXECUTE', 'BM', 'SP_BM1_GET_BY_ICP', v_bm_user);
  grant_obj('EXECUTE', 'BM', 'SP_BM1_GET_PRODUCT_MOB', v_bm_user);
  grant_obj('EXECUTE', 'BM', 'SP_BM_DESC_GET_TIT', v_bm_user);
  grant_obj('EXECUTE', 'BM', 'SP_BM_PLACA_CUA_GET', v_bm_user);
  grant_obj('EXECUTE', 'BM', 'SP_BM_PLACA_CUA_INS', v_bm_user);
  grant_obj('EXECUTE', 'BM', 'SP_BM_PLACA_CUA_DEL', v_bm_user);

  grant_obj('EXECUTE', 'BM', 'SP_BM_BIEN_GET_ALL', v_bm_user);
  grant_obj('EXECUTE', 'BM', 'SP_BM_BIEN_GET_ID', v_bm_user);
  grant_obj('EXECUTE', 'BM', 'SP_BM_BIEN_GET_PLACA', v_bm_user);
  grant_obj('EXECUTE', 'BM', 'SP_BM_DET_BIEN_GET', v_bm_user);
  grant_obj('EXECUTE', 'BM', 'SP_BM_DET_BIEN_INS', v_bm_user);
  grant_obj('EXECUTE', 'BM', 'SP_BM_DET_BIEN_UPD', v_bm_user);
  grant_obj('EXECUTE', 'BM', 'SP_BM_BIEN_INS', v_bm_user);
  grant_obj('EXECUTE', 'BM', 'SP_BM_BIEN_UPD', v_bm_user);
  grant_obj('EXECUTE', 'BM', 'SP_BM_FOTO_GET_PLACA', v_bm_user);
  grant_obj('EXECUTE', 'BM', 'SP_BM_FOTO_INS', v_bm_user);
  grant_obj('EXECUTE', 'BM', 'SP_BM_FOTO_DEL', v_bm_user);

  grant_obj('EXECUTE', 'BM', 'SP_BM_TIT_GET_ALL', v_bm_user);
  grant_obj('EXECUTE', 'BM', 'SP_BM_TIT_INS', v_bm_user);
  grant_obj('EXECUTE', 'BM', 'SP_BM_TIT_UPD', v_bm_user);
  grant_obj('EXECUTE', 'BM', 'SP_BM_DESC_GET_ALL', v_bm_user);
  grant_obj('EXECUTE', 'BM', 'SP_BM_DESC_GET_FK', v_bm_user);
  grant_obj('EXECUTE', 'BM', 'SP_BM_DESC_INS', v_bm_user);
  grant_obj('EXECUTE', 'BM', 'SP_BM_DESC_UPD', v_bm_user);
  grant_obj('EXECUTE', 'BM', 'SP_BM_CLASIF_GET_ALL', v_bm_user);
  grant_obj('EXECUTE', 'BM', 'SP_BM_CLASIF_INS', v_bm_user);
  grant_obj('EXECUTE', 'BM', 'SP_BM_CLASIF_UPD', v_bm_user);
  grant_obj('EXECUTE', 'BM', 'SP_BM_ART_GET_ALL', v_bm_user);
  grant_obj('EXECUTE', 'BM', 'SP_BM_ART_INS', v_bm_user);
  grant_obj('EXECUTE', 'BM', 'SP_BM_ART_UPD', v_bm_user);
  grant_obj('EXECUTE', 'BM', 'SP_BM_DET_ART_GET', v_bm_user);
  grant_obj('EXECUTE', 'BM', 'SP_BM_DET_ART_INS', v_bm_user);
  grant_obj('EXECUTE', 'BM', 'SP_BM_DET_ART_UPD', v_bm_user);

  grant_obj('EXECUTE', 'BM', 'SP_BM_UBI_GET_ALL', v_bm_user);
  grant_obj('EXECUTE', 'BM', 'SP_BM_UBI_GET_ICP', v_bm_user);
  grant_obj('EXECUTE', 'BM', 'SP_BM_UBI_GET_ICP_LST', v_bm_user);
  grant_obj('EXECUTE', 'BM', 'SP_BM_UBI_INS', v_bm_user);
  grant_obj('EXECUTE', 'BM', 'SP_BM_UBI_UPD', v_bm_user);
  grant_obj('EXECUTE', 'BM', 'SP_BM_UBI_HIST_GET', v_bm_user);

  grant_obj('EXECUTE', 'BM', 'SP_BM_MOV_GET_BIEN', v_bm_user);
  grant_obj('EXECUTE', 'BM', 'SP_BM_MOV_INS', v_bm_user);
  grant_obj('EXECUTE', 'BM', 'SP_BM_SOL_MOV_GET', v_bm_user);
  grant_obj('EXECUTE', 'BM', 'SP_BM_SOL_MOV_INS', v_bm_user);
  grant_obj('EXECUTE', 'BM', 'SP_BM_SOL_MOV_APR', v_bm_user);

  grant_obj('EXECUTE', 'BM', 'SP_BM_REP_PLACA', v_bm_user);
  grant_obj('EXECUTE', 'BM', 'SP_BM_REP_LOTE', v_bm_user);
  grant_obj('EXECUTE', 'BM', 'SP_BM_REP_FICHA', v_bm_user);
  grant_obj('EXECUTE', 'BM', 'SP_BM_REP_UBI_ICP', v_bm_user);
  grant_obj('EXECUTE', 'BM', 'SP_BM_REP_MOV_BIEN', v_bm_user);
  grant_obj('EXECUTE', 'BM', 'SP_BM_REP_MOV_FILT', v_bm_user);
  grant_obj('EXECUTE', 'BM', 'SP_BM_REP_SOL_MOV', v_bm_user);
  grant_obj('EXECUTE', 'BM', 'SP_BM_REP_PROC_MAS', v_bm_user);
  grant_obj('EXECUTE', 'BM', 'SP_BM_PROC_MAS_PRE', v_bm_user);
  grant_obj('EXECUTE', 'BM', 'SP_BM_PROC_MAS_EJE', v_bm_user);

  grant_obj('EXECUTE', 'BMC', 'BM_P_CONTEO', v_bmc_user);
  grant_obj('EXECUTE', 'BMC', 'SP_BM_CONTEO_GET_ALL', v_bmc_user);
  grant_obj('EXECUTE', 'BMC', 'SP_BM_CONTEO_INS', v_bmc_user);
  grant_obj('EXECUTE', 'BMC', 'SP_BM_CONTEO_UPD', v_bmc_user);
  grant_obj('EXECUTE', 'BMC', 'SP_BM_CONTEO_DEL', v_bmc_user);
  grant_obj('EXECUTE', 'BMC', 'SP_BM_CONTEO_CERRAR', v_bmc_user);
  grant_obj('EXECUTE', 'BMC', 'SP_BM_CONT_DET_GET', v_bmc_user);
  grant_obj('EXECUTE', 'BMC', 'SP_BM_CONT_DET_CMP', v_bmc_user);
  grant_obj('EXECUTE', 'BMC', 'SP_BM_CONT_DET_UPD', v_bmc_user);
  grant_obj('EXECUTE', 'BMC', 'SP_BM_CONT_DET_REC', v_bmc_user);
  grant_obj('EXECUTE', 'BMC', 'SP_BM_CONT_HIST_GET', v_bmc_user);
  grant_obj('EXECUTE', 'BMC', 'SP_BM_UBI_RESP_GET', v_bmc_user);
  grant_obj('EXECUTE', 'BMC', 'SP_BM_REP_CONT_DIF', v_bmc_user);
  grant_obj('EXECUTE', 'BMC', 'SP_BM_REP_CONT_HIST', v_bmc_user);
END;
/

PROMPT Grants runtime Bienes Municipales listos
