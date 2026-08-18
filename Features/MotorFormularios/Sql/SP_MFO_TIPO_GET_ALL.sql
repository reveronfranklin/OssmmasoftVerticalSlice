-- =============================================================================
-- MFO - Catalogo de tipos de campo.
-- Alimenta la paleta del diseñador y el registro de componentes del frontend.
-- =============================================================================
CREATE OR REPLACE PROCEDURE MFO.SP_MFO_TIPO_GET_ALL (
    p_SoloActivos  IN  CHAR,
    p_ResultSet    OUT SYS_REFCURSOR,
    p_Message      OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
    v_solo_activos CHAR(1) := NVL(p_SoloActivos, 'S');
BEGIN
    SELECT COUNT(1)
      INTO p_TotalRecords
      FROM MFO_TIPO_CAMPO
     WHERE (v_solo_activos = 'N' OR ACTIVO = 'S');

    OPEN p_ResultSet FOR
        SELECT TIPO_CAMPO_ID,
               CODIGO,
               NOMBRE,
               COLUMNA_VALOR,
               ADMITE_OPCIONES,
               ADMITE_MULTIPLE,
               ES_PRESENTACION,
               ADMITE_ARCHIVO,
               COMPONENTE,
               ORDEN,
               ICONO,
               ACTIVO
          FROM MFO_TIPO_CAMPO
         WHERE (v_solo_activos = 'N' OR ACTIVO = 'S')
         ORDER BY ORDEN;

    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT NULL TIPO_CAMPO_ID FROM DUAL WHERE 1 = 0;
END;
/
