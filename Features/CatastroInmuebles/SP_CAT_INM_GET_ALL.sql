CREATE OR REPLACE PROCEDURE SP_CAT_INM_GET_ALL
(
  p_PageSize      IN NUMBER,
  p_PageNumber    IN NUMBER,
  p_SearchText    IN VARCHAR2,
  p_CodigoEmpresa IN NUMBER,
  p_ResultSet     OUT SYS_REFCURSOR,
  p_Message       OUT VARCHAR2,
  p_TotalRecords  OUT NUMBER,
  p_TotalPages    OUT NUMBER
)
AS
  v_PageSize   NUMBER := LEAST(GREATEST(NVL(p_PageSize, 10), 1), 100);
  v_PageNumber NUMBER := GREATEST(NVL(p_PageNumber, 1), 1);
  v_SearchText VARCHAR2(200) := UPPER(TRIM(p_SearchText));
  v_FirstRow   NUMBER;
  v_LastRow    NUMBER;
BEGIN
  v_FirstRow := ((v_PageNumber - 1) * v_PageSize) + 1;
  v_LastRow := v_PageNumber * v_PageSize;

  SELECT COUNT(*)
    INTO p_TotalRecords
    FROM CAT_INMUEBLES i
   WHERE i.CODIGO_EMPRESA = p_CodigoEmpresa
     AND (v_SearchText IS NULL
       OR UPPER(i.CODIGO_CATASTRO) LIKE '%' || v_SearchText || '%'
       OR UPPER(i.NOMBRE_INMUEBLE) LIKE '%' || v_SearchText || '%'
       OR UPPER(i.NUMERO_INMUEBLE) LIKE '%' || v_SearchText || '%'
       OR TO_CHAR(i.CODIGO_INMUEBLE) = v_SearchText
       OR TO_CHAR(i.CODIGO_CONTRIBUYENTE) = v_SearchText);

  p_TotalPages := CEIL(p_TotalRecords / v_PageSize);

  OPEN p_ResultSet FOR
    SELECT CODIGO_INMUEBLE,
           CODIGO_CATASTRO,
           CODIGO_CONTRIBUYENTE,
           NOMBRE_INMUEBLE,
           NUMERO_INMUEBLE,
           AREA,
           VALOR_INMUEBLE,
           VALOR_TERRENO,
           VALOR_CONSTRUCCION,
           CODIGO_PARCELA,
           CODIGO_FICHA,
           OBSERVACION
      FROM (
        SELECT q.*, ROWNUM AS RN
          FROM (
            SELECT i.CODIGO_INMUEBLE,
                   i.CODIGO_CATASTRO,
                   i.CODIGO_CONTRIBUYENTE,
                   i.NOMBRE_INMUEBLE,
                   i.NUMERO_INMUEBLE,
                   i.AREA,
                   i.VALOR_INMUEBLE,
                   i.VALOR_TERRENO,
                   i.VALOR_CONSTRUCCION,
                   i.CODIGO_PARCELA,
                   i.CODIGO_FICHA,
                   i.OBSERVACION
              FROM CAT_INMUEBLES i
             WHERE i.CODIGO_EMPRESA = p_CodigoEmpresa
               AND (v_SearchText IS NULL
                 OR UPPER(i.CODIGO_CATASTRO) LIKE '%' || v_SearchText || '%'
                 OR UPPER(i.NOMBRE_INMUEBLE) LIKE '%' || v_SearchText || '%'
                 OR UPPER(i.NUMERO_INMUEBLE) LIKE '%' || v_SearchText || '%'
                 OR TO_CHAR(i.CODIGO_INMUEBLE) = v_SearchText
                 OR TO_CHAR(i.CODIGO_CONTRIBUYENTE) = v_SearchText)
             ORDER BY i.CODIGO_INMUEBLE
          ) q
         WHERE ROWNUM <= v_LastRow
      )
     WHERE RN >= v_FirstRow;

  p_Message := 'success';
EXCEPTION
  WHEN OTHERS THEN
    p_TotalRecords := 0;
    p_TotalPages := 0;
    p_Message := 'Error al consultar inmuebles: ' || SQLERRM;
    OPEN p_ResultSet FOR SELECT 1 AS CODIGO_INMUEBLE FROM DUAL WHERE 1 = 0;
END SP_CAT_INM_GET_ALL;
/
