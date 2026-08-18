-- =============================================================================
-- Motor de Formularios (MFO) - Fase 1 - Vistas (3)
-- Requerimiento 16.
--
-- Estas vistas no las consume el motor: existen para diagnostico y para que
-- cualquier herramienta de reporteria pueda leer las respuestas sin entender el
-- modelo EAV.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- Definicion plana de la version PUBLICADA de cada formulario.
-- Se une por MFO_FORMULARIO.VERSION_PUBL_ID y no por ESTADO='PUBLICADA' para que
-- la vista siga al puntero oficial del formulario: si por cualquier motivo
-- hubiera discrepancia, la fuente de verdad es el puntero.
-- -----------------------------------------------------------------------------
CREATE OR REPLACE VIEW MFO_V_DEF_PUBL AS
SELECT f.FORMULARIO_ID,
       f.ALIAS,
       f.NOMBRE          AS FORMULARIO,
       v.VERSION_ID,
       v.NUMERO          AS VERSION_NUMERO,
       s.SECCION_ID,
       s.CLAVE           AS CLAVE_SECCION,
       s.TITULO          AS TITULO_SECCION,
       s.ORDEN           AS ORDEN_SECCION,
       s.REPETIBLE,
       s.ES_PASO,
       c.CAMPO_ID,
       c.CLAVE           AS CLAVE_CAMPO,
       c.ETIQUETA,
       c.ORDEN           AS ORDEN_CAMPO,
       c.REQUERIDO,
       c.SOLO_LECTURA,
       t.CODIGO          AS TIPO_CAMPO,
       t.COLUMNA_VALOR,
       t.ADMITE_MULTIPLE,
       t.ES_PRESENTACION
  FROM MFO_FORMULARIO f
  JOIN MFO_VERSION    v ON v.VERSION_ID   = f.VERSION_PUBL_ID
  JOIN MFO_SECCION    s ON s.VERSION_ID   = v.VERSION_ID
  JOIN MFO_CAMPO      c ON c.SECCION_ID   = s.SECCION_ID
  JOIN MFO_TIPO_CAMPO t ON t.TIPO_CAMPO_ID = c.TIPO_CAMPO_ID;

-- -----------------------------------------------------------------------------
-- Formato largo universal: una fila por valor capturado, con el valor
-- normalizado a texto. Es la base de cualquier reporte que no conozca de
-- antemano los campos del formulario.
--
-- VALOR_TEXTO usa COALESCE en el orden TXT -> NUM -> FEC -> CLB porque un valor
-- ocupa exactamente una de esas columnas segun COLUMNA_VALOR de su tipo. El CLOB
-- se corta a 4000 caracteres: la vista es para consulta y exportacion, no para
-- recuperar el contenido integro, que se lee por MFO_VALOR.VALOR_CLB.
-- -----------------------------------------------------------------------------
CREATE OR REPLACE VIEW MFO_V_RESP_PLANA AS
SELECT f.ALIAS,
       f.FORMULARIO_ID,
       r.RESPUESTA_ID,
       r.VERSION_ID,
       r.ESTADO          AS ESTADO_RESPUESTA,
       r.CODIGO_EMPRESA,
       r.USUARIO_LLENA,
       r.FECHA_INICIO,
       r.FECHA_ENVIO,
       r.ENTIDAD_REF,
       r.CLAVE_REF,
       val.VALOR_ID,
       val.CLAVE_CAMPO,
       val.FILA,
       val.ORDEN_VAL,
       COALESCE(val.VALOR_TXT,
                TRIM(TO_CHAR(val.VALOR_NUM)),
                TO_CHAR(val.VALOR_FEC, 'YYYY-MM-DD HH24:MI:SS'),
                DBMS_LOB.SUBSTR(val.VALOR_CLB, 4000, 1)) AS VALOR_TEXTO,
       val.VALOR_NUM,
       val.VALOR_FEC,
       val.ETIQUETA_VAL
  FROM MFO_RESPUESTA  r
  JOIN MFO_FORMULARIO f   ON f.FORMULARIO_ID = r.FORMULARIO_ID
  JOIN MFO_VALOR      val ON val.RESPUESTA_ID = r.RESPUESTA_ID;

-- -----------------------------------------------------------------------------
-- Por clave de campo: en cuantas versiones aparece y cuantos valores capturados
-- tiene. Sirve para responder "puedo retirar este campo sin perder historia".
-- -----------------------------------------------------------------------------
CREATE OR REPLACE VIEW MFO_V_CAMPO_USO AS
SELECT f.FORMULARIO_ID,
       f.ALIAS,
       c.CLAVE                       AS CLAVE_CAMPO,
       MIN(c.ETIQUETA)               AS ETIQUETA,
       COUNT(DISTINCT c.VERSION_ID)  AS VERSIONES,
       COUNT(val.VALOR_ID)           AS VALORES
  FROM MFO_CAMPO      c
  JOIN MFO_VERSION    v   ON v.VERSION_ID    = c.VERSION_ID
  JOIN MFO_FORMULARIO f   ON f.FORMULARIO_ID = v.FORMULARIO_ID
  LEFT JOIN MFO_VALOR val ON val.CAMPO_ID    = c.CAMPO_ID
 GROUP BY f.FORMULARIO_ID, f.ALIAS, c.CLAVE;
