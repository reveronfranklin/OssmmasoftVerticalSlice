CREATE OR REPLACE PROCEDURE RH.SP_REPORTE_PERSONAL_GET_ALL (
    p_CodigoEmpresa     IN NUMBER,
    p_CodigoTipoNomina  IN NUMBER,
    p_Status            IN VARCHAR2,
    p_ResultSet         OUT SYS_REFCURSOR,
    p_Message           OUT VARCHAR2,
    p_TotalRecords      OUT NUMBER
) AS
    v_status VARCHAR2(100);
BEGIN
    v_status := NULLIF(TRIM(p_Status), '');

    SELECT COUNT(*)
      INTO p_TotalRecords
      FROM (
        SELECT a.cedula cedula,
               a.apellido || ' ' || a.nombre nombre,
               e.fecha_ingreso fecha_ingreso,
               c.codigo_sector || '-' || c.codigo_programa || '-' || c.codigo_subprograma || '-' ||
               c.codigo_proyecto || '-' || c.codigo_actividad || '-' || c.codigo_oficina || '     ' ||
               c.denominacion departamento,
               b.cargo_codigo codigo,
               d.denominacion cargo,
               b.sueldo sueldo,
               INITCAP(f.descripcion_status) descripcion_status,
               INITCAP(f.tipo_nomina) tipo_nomina
          FROM rh_personas a,
               rh_relacion_cargos b,
               pre_indice_cat_prg c,
               pre_cargos d,
               rh_administrativos e,
               RH_V_PERSONAL_CARGO f
         WHERE a.codigo_empresa = p_CodigoEmpresa
           AND a.status <> 'E'
           AND b.fecha_fin IS NULL
           AND b.codigo_persona = a.codigo_persona
           AND e.codigo_persona = a.codigo_persona
           AND b.codigo_tipo_nomina = NVL(p_CodigoTipoNomina, b.codigo_tipo_nomina)
           AND f.status = NVL(v_status, f.status)
           AND c.codigo_icp = b.codigo_icp
           AND d.codigo_cargo = b.codigo_cargo
           AND b.codigo_persona = a.codigo_persona
           AND a.codigo_persona = b.codigo_persona
           AND a.codigo_persona = f.codigo_persona
        UNION
        SELECT NULL cedula,
               'VACANTE' nombre,
               NULL fecha_ingreso,
               c.codigo_sector || '-' || c.codigo_programa || '-' || c.codigo_subprograma || '-' ||
               c.codigo_proyecto || '-' || c.codigo_actividad || '-' || c.codigo_oficina || '     ' ||
               c.denominacion departamento,
               b.cargo_codigo codigo,
               d.denominacion cargo,
               b.sueldo sueldo,
               NULL descripcion_status,
               INITCAP(f.siglas_tipo_nomina) tipo_nomina
          FROM rh_relacion_cargos b,
               pre_indice_cat_prg c,
               pre_cargos d,
               rh_tipos_nomina f
         WHERE b.fecha_fin IS NULL
           AND b.codigo_persona IS NULL
           AND b.codigo_tipo_nomina = NVL(p_CodigoTipoNomina, b.codigo_tipo_nomina)
           AND c.codigo_icp = b.codigo_icp
           AND d.codigo_cargo = b.codigo_cargo
           AND b.codigo_tipo_nomina = f.codigo_tipo_nomina
           AND b.codigo_presupuesto = (SELECT MAX(x.codigo_presupuesto) FROM pre_presupuestos x)
      );

    OPEN p_ResultSet FOR
        SELECT a.cedula cedula,
               a.apellido || ' ' || a.nombre nombre,
               e.fecha_ingreso fecha_ingreso,
               c.codigo_sector || '-' || c.codigo_programa || '-' || c.codigo_subprograma || '-' ||
               c.codigo_proyecto || '-' || c.codigo_actividad || '-' || c.codigo_oficina || '     ' ||
               c.denominacion departamento,
               b.cargo_codigo codigo,
               d.denominacion cargo,
               b.sueldo sueldo,
               INITCAP(f.descripcion_status) descripcion_status,
               INITCAP(f.tipo_nomina) tipo_nomina
          FROM rh_personas a,
               rh_relacion_cargos b,
               pre_indice_cat_prg c,
               pre_cargos d,
               rh_administrativos e,
               RH_V_PERSONAL_CARGO f
         WHERE a.codigo_empresa = p_CodigoEmpresa
           AND a.status <> 'E'
           AND b.fecha_fin IS NULL
           AND b.codigo_persona = a.codigo_persona
           AND e.codigo_persona = a.codigo_persona
           AND b.codigo_tipo_nomina = NVL(p_CodigoTipoNomina, b.codigo_tipo_nomina)
           AND f.status = NVL(v_status, f.status)
           AND c.codigo_icp = b.codigo_icp
           AND d.codigo_cargo = b.codigo_cargo
           AND b.codigo_persona = a.codigo_persona
           AND a.codigo_persona = b.codigo_persona
           AND a.codigo_persona = f.codigo_persona
        UNION
        SELECT NULL cedula,
               'VACANTE' nombre,
               NULL fecha_ingreso,
               c.codigo_sector || '-' || c.codigo_programa || '-' || c.codigo_subprograma || '-' ||
               c.codigo_proyecto || '-' || c.codigo_actividad || '-' || c.codigo_oficina || '     ' ||
               c.denominacion departamento,
               b.cargo_codigo codigo,
               d.denominacion cargo,
               b.sueldo sueldo,
               NULL descripcion_status,
               INITCAP(f.siglas_tipo_nomina) tipo_nomina
          FROM rh_relacion_cargos b,
               pre_indice_cat_prg c,
               pre_cargos d,
               rh_tipos_nomina f
         WHERE b.fecha_fin IS NULL
           AND b.codigo_persona IS NULL
           AND b.codigo_tipo_nomina = NVL(p_CodigoTipoNomina, b.codigo_tipo_nomina)
           AND c.codigo_icp = b.codigo_icp
           AND d.codigo_cargo = b.codigo_cargo
           AND b.codigo_tipo_nomina = f.codigo_tipo_nomina
           AND b.codigo_presupuesto = (SELECT MAX(x.codigo_presupuesto) FROM pre_presupuestos x)
         ORDER BY 6;

    p_Message := 'Success';

EXCEPTION
    WHEN OTHERS THEN
        p_Message := SUBSTR(SQLERRM, 1, 4000);
        p_TotalRecords := 0;
        OPEN p_ResultSet FOR
            SELECT CAST(NULL AS VARCHAR2(50)) cedula,
                   CAST(NULL AS VARCHAR2(4000)) nombre,
                   CAST(NULL AS DATE) fecha_ingreso,
                   CAST(NULL AS VARCHAR2(4000)) departamento,
                   CAST(NULL AS VARCHAR2(100)) codigo,
                   CAST(NULL AS VARCHAR2(4000)) cargo,
                   CAST(NULL AS NUMBER) sueldo,
                   CAST(NULL AS VARCHAR2(4000)) descripcion_status,
                   CAST(NULL AS VARCHAR2(4000)) tipo_nomina
              FROM dual
             WHERE 1 = 0;
END SP_REPORTE_PERSONAL_GET_ALL;
/
