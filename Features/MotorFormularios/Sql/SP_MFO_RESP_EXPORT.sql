-- =============================================================================
-- MFO - Exportacion en formato largo.
--
-- Una fila por valor: respuesta, clave de campo, fila, orden y el valor
-- normalizado a texto. Es el unico formato que no pierde nada, y por eso es el
-- que produce la base.
--
-- El formato ANCHO -una fila por respuesta, una columna por campo- se arma en el
-- backend a partir de esto. No se genera aqui a proposito: en Oracle 10g no hay
-- PIVOT, asi que habria que construir SQL dinamico con los nombres de campo de
-- cada formulario, y eso significa concatenar en una sentencia texto que viene
-- de una tabla. Es exactamente el patron que este requerimiento evita en el modo
-- reporte, y no vale la pena reintroducirlo para un CSV.
--
-- Consecuencia honesta: el formato ancho pierde filas cuando hay secciones
-- repetibles o campos multivalor, porque varios valores compiten por la misma
-- columna. Para esos formularios el formato largo es el unico correcto, y el
-- frontend debe decirlo en vez de dejar que el usuario lo descubra.
-- =============================================================================
CREATE OR REPLACE PROCEDURE MFO.SP_MFO_RESP_EXPORT (
    p_CodigoEmpresa IN  NUMBER,
    p_Alias         IN  VARCHAR2,
    p_Estado        IN  VARCHAR2,
    p_FechaDesde    IN  DATE,
    p_FechaHasta    IN  DATE,
    p_ResultSet     OUT SYS_REFCURSOR,
    p_Message       OUT VARCHAR2,
    p_TotalRecords  OUT NUMBER
) AS
BEGIN
    SELECT COUNT(1)
      INTO p_TotalRecords
      FROM MFO_RESPUESTA  R
      JOIN MFO_FORMULARIO F ON F.FORMULARIO_ID = R.FORMULARIO_ID
      JOIN MFO_VALOR      V ON V.RESPUESTA_ID  = R.RESPUESTA_ID
     WHERE R.CODIGO_EMPRESA = p_CodigoEmpresa
       AND (p_Alias      IS NULL OR F.ALIAS = UPPER(TRIM(p_Alias)))
       AND (p_Estado     IS NULL OR R.ESTADO = p_Estado)
       AND (p_FechaDesde IS NULL OR R.FECHA_INICIO >= TRUNC(p_FechaDesde))
       AND (p_FechaHasta IS NULL OR R.FECHA_INICIO <  TRUNC(p_FechaHasta) + 1);

    OPEN p_ResultSet FOR
        SELECT F.ALIAS,
               R.RESPUESTA_ID,
               R.VERSION_ID,
               VER.NUMERO       AS VERSION_NUMERO,
               R.ESTADO,
               R.USUARIO_LLENA,
               R.FECHA_INICIO,
               R.FECHA_ENVIO,
               R.ENTIDAD_REF,
               R.CLAVE_REF,
               V.CLAVE_CAMPO,
               C.ETIQUETA,
               V.FILA,
               V.ORDEN_VAL,
               T.CODIGO         AS TIPO_CAMPO,
               COALESCE(V.VALOR_TXT,
                        TRIM(TO_CHAR(V.VALOR_NUM)),
                        TO_CHAR(V.VALOR_FEC, 'YYYY-MM-DD'),
                        DBMS_LOB.SUBSTR(V.VALOR_CLB, 4000, 1)) AS VALOR,
               V.ETIQUETA_VAL
          FROM MFO_RESPUESTA  R
          JOIN MFO_FORMULARIO F   ON F.FORMULARIO_ID = R.FORMULARIO_ID
          JOIN MFO_VERSION    VER ON VER.VERSION_ID  = R.VERSION_ID
          JOIN MFO_VALOR      V   ON V.RESPUESTA_ID  = R.RESPUESTA_ID
          JOIN MFO_CAMPO      C   ON C.CAMPO_ID      = V.CAMPO_ID
          JOIN MFO_TIPO_CAMPO T   ON T.TIPO_CAMPO_ID = C.TIPO_CAMPO_ID
         WHERE R.CODIGO_EMPRESA = p_CodigoEmpresa
           AND (p_Alias      IS NULL OR F.ALIAS = UPPER(TRIM(p_Alias)))
           AND (p_Estado     IS NULL OR R.ESTADO = p_Estado)
           AND (p_FechaDesde IS NULL OR R.FECHA_INICIO >= TRUNC(p_FechaDesde))
           AND (p_FechaHasta IS NULL OR R.FECHA_INICIO <  TRUNC(p_FechaHasta) + 1)
         ORDER BY R.RESPUESTA_ID, V.FILA, C.ORDEN, V.ORDEN_VAL;

    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT NULL RESPUESTA_ID FROM DUAL WHERE 1 = 0;
END;
/
