CREATE OR REPLACE PROCEDURE SP_RM_CONTRI_GET_ALL
(
  p_PageSize IN NUMBER, p_PageNumber IN NUMBER, p_SearchText IN VARCHAR2,
  p_CodigoEmpresa IN NUMBER, p_ResultSet OUT SYS_REFCURSOR,
  p_Message OUT VARCHAR2, p_TotalRecords OUT NUMBER, p_TotalPages OUT NUMBER
)
AS
  v_Size NUMBER := LEAST(GREATEST(NVL(p_PageSize, 10), 1), 100);
  v_Page NUMBER := GREATEST(NVL(p_PageNumber, 1), 1);
  v_Search VARCHAR2(200) := UPPER(TRIM(p_SearchText));
BEGIN
  SELECT COUNT(*) INTO p_TotalRecords FROM RM_CONTRIBUYENTES c
   WHERE c.CODIGO_EMPRESA = p_CodigoEmpresa
     AND (v_Search IS NULL OR TO_CHAR(c.CODIGO_CONTRIBUYENTE) = v_Search
       OR TO_CHAR(c.NUMERO_IDENTIFICACION) LIKE '%' || v_Search || '%'
       OR UPPER(c.NOMBRE_RAZON_SOCIAL) LIKE '%' || v_Search || '%'
       OR UPPER(c.APELLIDO_ACRONIMO) LIKE '%' || v_Search || '%');
  p_TotalPages := CEIL(p_TotalRecords / v_Size);
  OPEN p_ResultSet FOR
    SELECT CODIGO_CONTRIBUYENTE, IDENTIFICACION_ID, NUMERO_IDENTIFICACION,
           NOMBRE_RAZON_SOCIAL, APELLIDO_ACRONIMO, ESTATUS_ID, FECHA_INGRESO
      FROM (SELECT q.*, ROWNUM RN FROM
             (SELECT c.CODIGO_CONTRIBUYENTE, c.IDENTIFICACION_ID,
                     c.NUMERO_IDENTIFICACION, c.NOMBRE_RAZON_SOCIAL,
                     c.APELLIDO_ACRONIMO, c.ESTATUS_ID, c.FECHA_INGRESO
                FROM RM_CONTRIBUYENTES c
               WHERE c.CODIGO_EMPRESA = p_CodigoEmpresa
                 AND (v_Search IS NULL OR TO_CHAR(c.CODIGO_CONTRIBUYENTE) = v_Search
                   OR TO_CHAR(c.NUMERO_IDENTIFICACION) LIKE '%' || v_Search || '%'
                   OR UPPER(c.NOMBRE_RAZON_SOCIAL) LIKE '%' || v_Search || '%'
                   OR UPPER(c.APELLIDO_ACRONIMO) LIKE '%' || v_Search || '%')
               ORDER BY c.CODIGO_CONTRIBUYENTE) q
            WHERE ROWNUM <= v_Page * v_Size)
     WHERE RN >= ((v_Page - 1) * v_Size) + 1;
  p_Message := 'success';
EXCEPTION WHEN OTHERS THEN
  p_TotalRecords := 0; p_TotalPages := 0;
  p_Message := 'Error al consultar contribuyentes: ' || SQLERRM;
  OPEN p_ResultSet FOR SELECT 1 CODIGO_CONTRIBUYENTE FROM DUAL WHERE 1 = 0;
END SP_RM_CONTRI_GET_ALL;
/
