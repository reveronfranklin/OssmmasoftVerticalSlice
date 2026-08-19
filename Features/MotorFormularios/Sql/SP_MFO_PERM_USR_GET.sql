-- =============================================================================
-- MFO - Permisos de un formulario, por usuario.
--
-- Devuelve dos cursores porque la pantalla de administracion necesita los dos a
-- la vez y son ejes distintos: que puede hacer cada persona, y que reportes
-- tiene acotados. Pedirlos por separado seria dos viajes para pintar una tabla.
--
-- Con p_Usuario se acota a esa persona, que es la forma en que el backend
-- resuelve la autorizacion en cada peticion.
-- =============================================================================
CREATE OR REPLACE PROCEDURE MFO.SP_MFO_PERM_USR_GET (
    p_FormularioId IN  NUMBER,
    p_Usuario      IN  VARCHAR2,
    p_ResultSet    OUT SYS_REFCURSOR,
    p_Reportes     OUT SYS_REFCURSOR,
    p_Message      OUT VARCHAR2,
    p_TotalRecords OUT NUMBER
) AS
    v_usuario VARCHAR2(60) := UPPER(TRIM(p_Usuario));
BEGIN
    SELECT COUNT(1)
      INTO p_TotalRecords
      FROM MFO_PERMISO_USR
     WHERE FORMULARIO_ID = p_FormularioId
       AND (v_usuario IS NULL OR USUARIO = v_usuario);

    OPEN p_ResultSet FOR
        SELECT PERM_USR_ID, FORMULARIO_ID, USUARIO, ACCION
          FROM MFO_PERMISO_USR
         WHERE FORMULARIO_ID = p_FormularioId
           AND (v_usuario IS NULL OR USUARIO = v_usuario)
         ORDER BY USUARIO, ACCION;

    OPEN p_Reportes FOR
        SELECT P.PERM_REP_ID, P.REPORTE_ID, P.USUARIO, R.CLAVE, R.NOMBRE
          FROM MFO_PERMISO_REP P
          JOIN MFO_REPORTE R ON R.REPORTE_ID = P.REPORTE_ID
         WHERE R.FORMULARIO_ID = p_FormularioId
           AND (v_usuario IS NULL OR P.USUARIO = v_usuario)
         ORDER BY P.USUARIO, R.ORDEN;

    p_Message := 'success';
EXCEPTION
    WHEN OTHERS THEN
        p_TotalRecords := 0;
        p_Message := 'Error tecnico: ' || SQLERRM;
        OPEN p_ResultSet FOR SELECT NULL PERM_USR_ID FROM DUAL WHERE 1 = 0;
        OPEN p_Reportes  FOR SELECT NULL PERM_REP_ID FROM DUAL WHERE 1 = 0;
END;
/
