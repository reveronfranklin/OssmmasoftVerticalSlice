-- =============================================================================
-- MFO - Busqueda paginada de respuestas.
--
-- Ademas de los filtros del sobre (formulario, estado, fechas, usuario, entidad
-- de negocio), admite filtrar **por el valor de un campo** dada su CLAVE_CAMPO.
-- Es la consulta que justifica que MFO_VALOR lleve CLAVE_CAMPO denormalizado y
-- que existan IDX_MFO_VALOR_TXT / _NUM / _FEC: sin eso, "dame las respuestas
-- donde CEDULA = X" recorreria toda la tabla de valores.
--
-- El filtro por valor se resuelve con EXISTS y no con un JOIN para que una
-- respuesta con varios valores del mismo campo (multivalor o seccion repetible)
-- aparezca una sola vez.
--
-- p_ValorTexto se compara contra la columna que corresponda al tipo del campo,
-- resuelta por la definicion. El cliente manda texto y la base decide como
-- interpretarlo; nunca al reves.
-- =============================================================================
CREATE OR REPLACE PROCEDURE MFO.SP_MFO_RESP_SEARCH (
    p_CodigoEmpresa IN  NUMBER,
    p_Alias         IN  VARCHAR2,
    p_Estado        IN  VARCHAR2,
    p_FechaDesde    IN  DATE,
    p_FechaHasta    IN  DATE,
    p_Usuario       IN  VARCHAR2,
    p_EntidadRef    IN  VARCHAR2,
    p_ClaveRef      IN  VARCHAR2,
    p_ClaveCampo    IN  VARCHAR2,
    p_ValorTexto    IN  VARCHAR2,
    p_Page          IN  NUMBER,
    p_PageSize      IN  NUMBER,
    p_ResultSet     OUT SYS_REFCURSOR,
    p_Message       OUT VARCHAR2,
    p_TotalRecords  OUT NUMBER,
    p_TotalPage     OUT NUMBER
) AS
    v_Page     NUMBER := NVL(p_Page, 1);
    v_PageSize NUMBER := NVL(p_PageSize, 25);
    v_FromRow  NUMBER;
    v_ToRow    NUMBER;
    v_clave    VARCHAR2(30) := UPPER(TRIM(p_ClaveCampo));
BEGIN
    v_FromRow := ((v_Page - 1) * v_PageSize) + 1;
    v_ToRow   := v_Page * v_PageSize;

    SELECT COUNT(1)
      INTO p_TotalRecords
      FROM MFO_RESPUESTA  R
      JOIN MFO_FORMULARIO F ON F.FORMULARIO_ID = R.FORMULARIO_ID
     WHERE R.CODIGO_EMPRESA = p_CodigoEmpresa
       AND (p_Alias       IS NULL OR F.ALIAS = UPPER(TRIM(p_Alias)))
       AND (p_Estado      IS NULL OR R.ESTADO = p_Estado)
       AND (p_FechaDesde  IS NULL OR R.FECHA_INICIO >= TRUNC(p_FechaDesde))
       AND (p_FechaHasta  IS NULL OR R.FECHA_INICIO <  TRUNC(p_FechaHasta) + 1)
       AND (p_Usuario     IS NULL OR R.USUARIO_LLENA = p_Usuario)
       AND (p_EntidadRef  IS NULL OR R.ENTIDAD_REF = p_EntidadRef)
       AND (p_ClaveRef    IS NULL OR R.CLAVE_REF = p_ClaveRef)
       AND (v_clave IS NULL
            OR EXISTS (SELECT 1
                         FROM MFO_VALOR V
                         JOIN MFO_CAMPO C      ON C.CAMPO_ID = V.CAMPO_ID
                         JOIN MFO_TIPO_CAMPO T ON T.TIPO_CAMPO_ID = C.TIPO_CAMPO_ID
                        WHERE V.RESPUESTA_ID = R.RESPUESTA_ID
                          AND V.CLAVE_CAMPO = v_clave
                          AND (p_ValorTexto IS NULL
                               OR (T.COLUMNA_VALOR = 'TXT'
                                   AND UPPER(V.VALOR_TXT) LIKE '%' || UPPER(p_ValorTexto) || '%')
                               OR (T.COLUMNA_VALOR = 'NUM'
                                   AND V.VALOR_NUM = TO_NUMBER(p_ValorTexto))
                               OR (T.COLUMNA_VALOR = 'FEC'
                                   AND V.VALOR_FEC = TO_DATE(p_ValorTexto, 'YYYY-MM-DD'))
                               OR (T.COLUMNA_VALOR = 'CLB'
                                   AND DBMS_LOB.INSTR(V.VALOR_CLB, p_ValorTexto) > 0))));

    p_TotalPage := CEIL(p_TotalRecords / v_PageSize);

    OPEN p_ResultSet FOR
        SELECT *
          FROM (
                SELECT X.*, ROWNUM RN
                  FROM (
                        SELECT R.RESPUESTA_ID, R.VERSION_ID, R.FORMULARIO_ID,
                               F.ALIAS, F.NOMBRE FORMULARIO, V.NUMERO VERSION_NUMERO,
                               R.ESTADO, R.USUARIO_LLENA, R.FECHA_INICIO, R.FECHA_ENVIO,
                               R.ENTIDAD_REF, R.CLAVE_REF, R.MOTIVO_ANULA,
                               (SELECT COUNT(1) FROM MFO_VALOR W
                                 WHERE W.RESPUESTA_ID = R.RESPUESTA_ID) AS VALORES
                          FROM MFO_RESPUESTA  R
                          JOIN MFO_FORMULARIO F ON F.FORMULARIO_ID = R.FORMULARIO_ID
                          JOIN MFO_VERSION    V ON V.VERSION_ID    = R.VERSION_ID
                         WHERE R.CODIGO_EMPRESA = p_CodigoEmpresa
                           AND (p_Alias      IS NULL OR F.ALIAS = UPPER(TRIM(p_Alias)))
                           AND (p_Estado     IS NULL OR R.ESTADO = p_Estado)
                           AND (p_FechaDesde IS NULL OR R.FECHA_INICIO >= TRUNC(p_FechaDesde))
                           AND (p_FechaHasta IS NULL OR R.FECHA_INICIO <  TRUNC(p_FechaHasta) + 1)
                           AND (p_Usuario    IS NULL OR R.USUARIO_LLENA = p_Usuario)
                           AND (p_EntidadRef IS NULL OR R.ENTIDAD_REF = p_EntidadRef)
                           AND (p_ClaveRef   IS NULL OR R.CLAVE_REF = p_ClaveRef)
                           AND (v_clave IS NULL
                                OR EXISTS (SELECT 1
                                             FROM MFO_VALOR V2
                                             JOIN MFO_CAMPO C      ON C.CAMPO_ID = V2.CAMPO_ID
                                             JOIN MFO_TIPO_CAMPO T ON T.TIPO_CAMPO_ID = C.TIPO_CAMPO_ID
                                            WHERE V2.RESPUESTA_ID = R.RESPUESTA_ID
                                              AND V2.CLAVE_CAMPO = v_clave
                                              AND (p_ValorTexto IS NULL
                                                   OR (T.COLUMNA_VALOR = 'TXT'
                                                       AND UPPER(V2.VALOR_TXT) LIKE '%' || UPPER(p_ValorTexto) || '%')
                                                   OR (T.COLUMNA_VALOR = 'NUM'
                                                       AND V2.VALOR_NUM = TO_NUMBER(p_ValorTexto))
                                                   OR (T.COLUMNA_VALOR = 'FEC'
                                                       AND V2.VALOR_FEC = TO_DATE(p_ValorTexto, 'YYYY-MM-DD'))
                                                   OR (T.COLUMNA_VALOR = 'CLB'
                                                       AND DBMS_LOB.INSTR(V2.VALOR_CLB, p_ValorTexto) > 0))))
                         ORDER BY R.FECHA_INICIO DESC, R.RESPUESTA_ID DESC
                       ) X
                 WHERE ROWNUM <= v_ToRow
               )
         WHERE RN >= v_FromRow;

    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        p_TotalRecords := 0;
        p_TotalPage := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT NULL RESPUESTA_ID FROM DUAL WHERE 1 = 0;
END;
/
