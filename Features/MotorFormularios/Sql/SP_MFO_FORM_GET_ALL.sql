-- =============================================================================
-- MFO - Listado paginado de formularios.
-- Alimenta la pantalla de formularios disponibles y la bandeja.
-- =============================================================================
CREATE OR REPLACE PROCEDURE MFO.SP_MFO_FORM_GET_ALL (
    p_CodigoEmpresa IN  NUMBER,
    p_SearchText    IN  VARCHAR2,
    p_Estado        IN  VARCHAR2,
    p_ModoUso       IN  VARCHAR2,
    p_Usuario       IN  VARCHAR2,
    p_EsSuperuser   IN  NUMBER,
    p_Page          IN  NUMBER,
    p_PageSize      IN  NUMBER,
    p_ResultSet     OUT SYS_REFCURSOR,
    p_Message       OUT VARCHAR2,
    p_TotalRecords  OUT NUMBER
) AS
    v_Page     NUMBER := NVL(p_Page, 1);
    v_PageSize NUMBER := NVL(p_PageSize, 50);
    v_FromRow  NUMBER;
    v_ToRow    NUMBER;
BEGIN
    v_FromRow := ((v_Page - 1) * v_PageSize) + 1;
    v_ToRow   := v_Page * v_PageSize;

    SELECT COUNT(1)
      INTO p_TotalRecords
      FROM MFO_FORMULARIO F
     WHERE F.CODIGO_EMPRESA = p_CodigoEmpresa
       AND (NVL(p_EsSuperuser, 0) = 1 OR EXISTS (
            SELECT 1
              FROM MFO_PERMISO_USR P
             WHERE P.FORMULARIO_ID = F.FORMULARIO_ID
               AND UPPER(P.USUARIO) = UPPER(TRIM(p_Usuario))))
       AND (p_Estado   IS NULL OR F.ESTADO   = p_Estado)
       AND (p_ModoUso  IS NULL OR F.MODO_USO = p_ModoUso)
       AND (p_SearchText IS NULL
            OR UPPER(F.NOMBRE)         LIKE '%' || UPPER(p_SearchText) || '%'
            OR UPPER(F.ALIAS)          LIKE '%' || UPPER(p_SearchText) || '%'
            OR UPPER(NVL(F.CATEGORIA, '')) LIKE '%' || UPPER(p_SearchText) || '%');

    OPEN p_ResultSet FOR
        SELECT *
          FROM (
                SELECT X.*, ROWNUM RN
                  FROM (
                        SELECT F.FORMULARIO_ID,
                               F.ALIAS,
                               F.NOMBRE,
                               F.DESCRIPCION,
                               F.CATEGORIA,
                               F.CODIGO_EMPRESA,
                               F.ESTADO,
                               F.VERSION_PUBL_ID,
                               F.ENTIDAD_DESTINO,
                               F.MAX_RESP_USUARIO,
                               F.PERMITE_BORRADOR,
                               F.MODO_USO,
                               F.REGISTRA_EJEC,
                               V.NUMERO AS VERSION_NUMERO,
                               -- Cuenta de versiones en BORRADOR: el diseñador
                               -- necesita saber si hay una edicion en curso sin
                               -- pedir el detalle de cada formulario.
                               (SELECT COUNT(1) FROM MFO_VERSION B
                                 WHERE B.FORMULARIO_ID = F.FORMULARIO_ID
                                   AND B.ESTADO = 'BORRADOR') AS BORRADORES,
                               F.FECHA_INS,
                               F.USUARIO_INS
                          FROM MFO_FORMULARIO F
                          LEFT JOIN MFO_VERSION V ON V.VERSION_ID = F.VERSION_PUBL_ID
                         WHERE F.CODIGO_EMPRESA = p_CodigoEmpresa
                           AND (NVL(p_EsSuperuser, 0) = 1 OR EXISTS (
                                SELECT 1
                                  FROM MFO_PERMISO_USR P
                                 WHERE P.FORMULARIO_ID = F.FORMULARIO_ID
                                   AND UPPER(P.USUARIO) = UPPER(TRIM(p_Usuario))))
                           AND (p_Estado  IS NULL OR F.ESTADO   = p_Estado)
                           AND (p_ModoUso IS NULL OR F.MODO_USO = p_ModoUso)
                           AND (p_SearchText IS NULL
                                OR UPPER(F.NOMBRE)             LIKE '%' || UPPER(p_SearchText) || '%'
                                OR UPPER(F.ALIAS)              LIKE '%' || UPPER(p_SearchText) || '%'
                                OR UPPER(NVL(F.CATEGORIA, '')) LIKE '%' || UPPER(p_SearchText) || '%')
                         ORDER BY NVL(F.CATEGORIA, 'ZZZ'), F.NOMBRE
                       ) X
                 WHERE ROWNUM <= v_ToRow
               )
         WHERE RN >= v_FromRow;

    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT NULL FORMULARIO_ID FROM DUAL WHERE 1 = 0;
END;
/
