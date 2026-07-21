-- Reporte Control Perceptivo (ADM) - encabezado.
-- Reconstruido por ingenieria inversa de ADM_CONTROL_PERCEPTIVO.rdf (Oracle
-- Reports). Ver Requerimientos/15 - Reporte Control Perceptivo/README.md.
-- PENDIENTE DE VALIDACION contra datos reales (Fase 1 del PLAN.md de ese
-- requerimiento): no se ejecuto contra un Oracle real en esta sesion.
--
-- p_CodigoCompromiso identifica un Compromiso Presupuestario (ADM_COMPROMISOS
-- o PRE_COMPROMISOS) o un Contrato (ADM_CONTRATOS). El procedimiento intenta
-- las 3 fuentes por UNION ALL y devuelve la primera fila encontrada, evitando
-- que el llamador deba indicar de antemano el tipo de compromiso.
CREATE OR REPLACE PROCEDURE ADM.SP_ADM_CTRL_PERCEP_HDR_GET (
    p_CodigoCompromiso IN NUMBER,
    p_ResultSet         OUT SYS_REFCURSOR,
    p_Message           OUT VARCHAR2,
    p_TotalRecords      OUT NUMBER
) AS
    v_FechaEmisionTexto VARCHAR2(200);
BEGIN
    IF TO_CHAR(SYSDATE, 'DD') = '01' THEN
        v_FechaEmisionTexto := 'al ' || INITCAP(SIS.SIS_MONTOESCRITO(TO_NUMBER(TO_CHAR(SYSDATE, 'DD')), '1'))
            || ' dia del mes de ' || RTRIM(LTRIM(INITCAP(TO_CHAR(SYSDATE, 'MONTH')))) || ' de ' || TO_CHAR(SYSDATE, 'RRRR');
    ELSE
        v_FechaEmisionTexto := 'a los ' || INITCAP(SIS.SIS_MONTOESCRITO(TO_NUMBER(TO_CHAR(SYSDATE, 'DD')), '1'))
            || ' dias del mes de ' || RTRIM(LTRIM(INITCAP(TO_CHAR(SYSDATE, 'MONTH')))) || ' de ' || TO_CHAR(SYSDATE, 'RRRR');
    END IF;

    SELECT COUNT(*)
      INTO p_TotalRecords
      FROM (
            SELECT A.CODIGO_COMPROMISO
              FROM ADM.ADM_COMPROMISOS A
             WHERE A.CODIGO_COMPROMISO = p_CodigoCompromiso
            UNION ALL
            SELECT A.CODIGO_COMPROMISO
              FROM PRE.PRE_COMPROMISOS A
             WHERE A.CODIGO_COMPROMISO = p_CodigoCompromiso
            UNION ALL
            SELECT A.CODIGO_CONTRATO CODIGO_COMPROMISO
              FROM ADM.ADM_CONTRATOS A
             WHERE A.CODIGO_CONTRATO = p_CodigoCompromiso
      );

    OPEN p_ResultSet FOR
        SELECT *
          FROM (
                -- Rama 1: compromiso via solicitud de compromiso (ADM_SOL_COMPROMISO).
                SELECT
                    A.CODIGO_COMPROMISO,
                    NVL(A.NUMERO_COMPROMISO, '') NUMERO_COMPROMISO,
                    A.FECHA_COMPROMISO,
                    NVL(E.NOMBRE_PROVEEDOR, '') PROVEEDOR,
                    (SELECT Z.DENOMINACION
                       FROM PRE.PRE_INDICE_CAT_PRG Z
                      WHERE Z.CODIGO_ICP = D.CODIGO_SOLICITANTE) SOLICITANTE,
                    (SELECT SE.EXTRA4
                       FROM SIS.SIS_EMPRESAS SE
                      WHERE SE.CODIGO_EMPRESA = D.CODIGO_EMPRESA) DIRECCION_EMPRESA,
                    NVL(SIS.SIS_F_EMPRESAS('NOMBRE_EMPRESA', D.CODIGO_EMPRESA), '') NOMBRE_EMPRESA,
                    v_FechaEmisionTexto FECHA_EMISION_TEXTO
                  FROM ADM.ADM_COMPROMISOS A
                  JOIN ADM.ADM_SOL_COMPROMISO D
                    ON A.CODIGO_SOLICITUD = D.CODIGO_SOL_COMPROMISO
                  JOIN ADM.ADM_PROVEEDORES E
                    ON D.CODIGO_PROVEEDOR = E.CODIGO_PROVEEDOR
                 WHERE A.CODIGO_COMPROMISO = p_CodigoCompromiso
                UNION ALL
                -- Rama 2: compromiso via solicitud generica (ADM_SOLICITUDES), sin PUC.
                SELECT
                    A.CODIGO_COMPROMISO,
                    NVL(A.NUMERO_COMPROMISO, '') NUMERO_COMPROMISO,
                    A.FECHA_COMPROMISO,
                    NVL(E.NOMBRE_PROVEEDOR, '') PROVEEDOR,
                    (SELECT Z.DENOMINACION
                       FROM PRE.PRE_INDICE_CAT_PRG Z
                      WHERE Z.CODIGO_ICP = D.CODIGO_SOLICITANTE) SOLICITANTE,
                    (SELECT SE.EXTRA4
                       FROM SIS.SIS_EMPRESAS SE
                      WHERE SE.CODIGO_EMPRESA = D.CODIGO_EMPRESA) DIRECCION_EMPRESA,
                    NVL(SIS.SIS_F_EMPRESAS('NOMBRE_EMPRESA', D.CODIGO_EMPRESA), '') NOMBRE_EMPRESA,
                    v_FechaEmisionTexto FECHA_EMISION_TEXTO
                  FROM PRE.PRE_COMPROMISOS A
                  JOIN ADM.ADM_SOLICITUDES D
                    ON A.CODIGO_SOLICITUD = D.CODIGO_SOLICITUD
                  JOIN ADM.ADM_PROVEEDORES E
                    ON D.CODIGO_PROVEEDOR = E.CODIGO_PROVEEDOR
                 WHERE A.CODIGO_COMPROMISO = p_CodigoCompromiso
                UNION ALL
                -- Rama 3: contrato (ADM_CONTRATOS).
                SELECT
                    A.CODIGO_CONTRATO CODIGO_COMPROMISO,
                    NVL(A.NUMERO_CONTRATO, '') NUMERO_COMPROMISO,
                    A.FECHA_CONTRATO FECHA_COMPROMISO,
                    NVL(E.NOMBRE_PROVEEDOR, '') PROVEEDOR,
                    CAST(NULL AS VARCHAR2(4000)) SOLICITANTE,
                    (SELECT SE.EXTRA4
                       FROM SIS.SIS_EMPRESAS SE
                      WHERE SE.CODIGO_EMPRESA = A.CODIGO_EMPRESA) DIRECCION_EMPRESA,
                    NVL(SIS.SIS_F_EMPRESAS('NOMBRE_EMPRESA', A.CODIGO_EMPRESA), '') NOMBRE_EMPRESA,
                    v_FechaEmisionTexto FECHA_EMISION_TEXTO
                  FROM ADM.ADM_CONTRATOS A
                  JOIN ADM.ADM_PROVEEDORES E
                    ON A.CODIGO_PROVEEDOR = E.CODIGO_PROVEEDOR
                 WHERE A.CODIGO_CONTRATO = p_CodigoCompromiso
          )
         WHERE ROWNUM = 1;

    p_Message := 'Success';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := SUBSTR(SQLERRM, 1, 4000);
        p_TotalRecords := 0;
        OPEN p_ResultSet FOR
            SELECT
                CAST(NULL AS NUMBER) CODIGO_COMPROMISO,
                CAST(NULL AS VARCHAR2(100)) NUMERO_COMPROMISO,
                CAST(NULL AS DATE) FECHA_COMPROMISO,
                CAST(NULL AS VARCHAR2(4000)) PROVEEDOR,
                CAST(NULL AS VARCHAR2(4000)) SOLICITANTE,
                CAST(NULL AS VARCHAR2(4000)) DIRECCION_EMPRESA,
                CAST(NULL AS VARCHAR2(4000)) NOMBRE_EMPRESA,
                CAST(NULL AS VARCHAR2(200)) FECHA_EMISION_TEXTO
              FROM DUAL
             WHERE 1 = 0;
END SP_ADM_CTRL_PERCEP_HDR_GET;
/
