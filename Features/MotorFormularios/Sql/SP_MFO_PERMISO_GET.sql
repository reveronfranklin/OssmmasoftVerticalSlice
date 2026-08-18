-- =============================================================================
-- MFO - Permisos de un formulario.
--
-- Dos modos:
--   - Sin p_RolesCsv: devuelve todos los permisos del formulario, agrupados por
--     rol. Es lo que consume la pantalla de permisos del diseñador.
--   - Con p_RolesCsv: devuelve solo las acciones permitidas a esa lista de roles,
--     que es la comprobacion que hace el backend en cada slice. Se resuelve con
--     una sola consulta para no traer todos los permisos y filtrarlos en C#.
--
-- El filtro por lista se hace con INSTR sobre una cadena delimitada y no con SQL
-- dinamico: p_RolesCsv viene de la identidad del usuario, y armar un IN() por
-- concatenacion seria inyeccion. Los delimitadores a ambos lados evitan que el
-- rol 'ADM' haga match dentro de 'ADMIN'.
-- =============================================================================
CREATE OR REPLACE PROCEDURE MFO.SP_MFO_PERMISO_GET (
    p_FormularioId IN  NUMBER,
    p_RolesCsv     IN  VARCHAR2,
    p_ResultSet    OUT SYS_REFCURSOR,
    p_Message      OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
    v_roles VARCHAR2(4000);
BEGIN
    IF p_RolesCsv IS NOT NULL THEN
        -- ',ADM,LLENAR,' permite buscar ',<rol>,' sin coincidencias parciales.
        v_roles := ',' || UPPER(REPLACE(TRIM(p_RolesCsv), ' ', '')) || ',';
    END IF;

    SELECT COUNT(1)
      INTO p_TotalRecords
      FROM MFO_PERMISO P
     WHERE P.FORMULARIO_ID = p_FormularioId
       AND (v_roles IS NULL OR INSTR(v_roles, ',' || P.ROL_CODIGO || ',') > 0);

    OPEN p_ResultSet FOR
        SELECT P.PERMISO_ID,
               P.FORMULARIO_ID,
               P.ROL_CODIGO,
               P.ACCION
          FROM MFO_PERMISO P
         WHERE P.FORMULARIO_ID = p_FormularioId
           AND (v_roles IS NULL OR INSTR(v_roles, ',' || P.ROL_CODIGO || ',') > 0)
         ORDER BY P.ROL_CODIGO, P.ACCION;

    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT NULL PERMISO_ID FROM DUAL WHERE 1 = 0;
END;
/
