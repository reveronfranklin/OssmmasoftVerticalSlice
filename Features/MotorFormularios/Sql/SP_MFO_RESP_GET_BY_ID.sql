-- =============================================================================
-- MFO - Una respuesta con todos sus valores.
--
-- Devuelve el sobre y los valores. NO devuelve la definicion de la version: para
-- eso esta SP_MFO_VER_GET_FULL, y el backend la sirve desde MfoDefinicionCache.
-- Repetirla aqui significaria traer la definicion completa en cada consulta de
-- una respuesta, cuando una version publicada es inmutable y por tanto
-- cacheable para siempre. El sobre trae VERSION_ID, que es lo que el backend
-- necesita para resolverla.
--
-- Esa combinacion -sobre + valores + definicion de SU version, no de la vigente-
-- es lo que permite reabrir una respuesta de hace dos años y verla exactamente
-- como se lleno.
-- =============================================================================
CREATE OR REPLACE PROCEDURE MFO.SP_MFO_RESP_GET_BY_ID (
    p_RespuestaId IN  NUMBER,
    p_ResultSet   OUT SYS_REFCURSOR,
    p_Valores     OUT SYS_REFCURSOR,
    p_Message     OUT VARCHAR2
) AS
    v_existe NUMBER;
BEGIN
    SELECT COUNT(1) INTO v_existe FROM MFO_RESPUESTA WHERE RESPUESTA_ID = p_RespuestaId;

    IF v_existe = 0 THEN
        p_Message := 'La respuesta indicada no existe.';
        OPEN p_ResultSet FOR SELECT NULL RESPUESTA_ID FROM DUAL WHERE 1 = 0;
        OPEN p_Valores   FOR SELECT NULL VALOR_ID     FROM DUAL WHERE 1 = 0;
        RETURN;
    END IF;

    OPEN p_ResultSet FOR
        SELECT R.RESPUESTA_ID, R.VERSION_ID, R.FORMULARIO_ID, F.ALIAS, F.NOMBRE FORMULARIO,
               V.NUMERO VERSION_NUMERO, R.CODIGO_EMPRESA, R.ESTADO, R.CLAVE_IDEM,
               R.ENTIDAD_REF, R.CLAVE_REF, R.USUARIO_LLENA, R.FECHA_INICIO, R.FECHA_ENVIO,
               R.IP_ORIGEN, R.MOTIVO_ANULA, R.FECHA_INS, R.FECHA_UPD
          FROM MFO_RESPUESTA  R
          JOIN MFO_FORMULARIO F ON F.FORMULARIO_ID = R.FORMULARIO_ID
          JOIN MFO_VERSION    V ON V.VERSION_ID    = R.VERSION_ID
         WHERE R.RESPUESTA_ID = p_RespuestaId;

    -- VALOR_CLB se corta a 4000 en VALOR_TEXTO para que el cursor sea uniforme,
    -- pero se devuelve tambien entero en VALOR_CLB: el renderizador necesita el
    -- contenido completo de un textarea, no un resumen.
    OPEN p_Valores FOR
        SELECT V.VALOR_ID, V.CAMPO_ID, V.CLAVE_CAMPO, V.FILA, V.ORDEN_VAL,
               V.VALOR_TXT, V.VALOR_NUM, V.VALOR_FEC, V.VALOR_CLB, V.ETIQUETA_VAL,
               T.COLUMNA_VALOR,
               COALESCE(V.VALOR_TXT,
                        TRIM(TO_CHAR(V.VALOR_NUM)),
                        TO_CHAR(V.VALOR_FEC, 'YYYY-MM-DD HH24:MI:SS'),
                        DBMS_LOB.SUBSTR(V.VALOR_CLB, 4000, 1)) AS VALOR_TEXTO
          FROM MFO_VALOR V
          JOIN MFO_CAMPO C      ON C.CAMPO_ID = V.CAMPO_ID
          JOIN MFO_TIPO_CAMPO T ON T.TIPO_CAMPO_ID = C.TIPO_CAMPO_ID
         WHERE V.RESPUESTA_ID = p_RespuestaId
         ORDER BY V.FILA, C.ORDEN, V.ORDEN_VAL;

    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT NULL RESPUESTA_ID FROM DUAL WHERE 1 = 0;
        OPEN p_Valores   FOR SELECT NULL VALOR_ID     FROM DUAL WHERE 1 = 0;
END;
/
