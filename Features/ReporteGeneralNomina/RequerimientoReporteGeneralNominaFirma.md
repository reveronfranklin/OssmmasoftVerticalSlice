# Requerimiento: Reporte General de Nomina Firma

Dado el siguiente query, crear un stored procedure en Oracle 10 que retorne el resultado de la consulta y un parametro de salida `message` con posible error o `Success` cuando sea satisfactorio.

Usar la estructura del stored procedure `RH.SP_REP_GRAL_NOMINA_GET_ALL`.

El nombre del stored procedure debe ser de maximo 30 caracteres.

## Firmas del reporte

El reporte debe devolver las firmas asociadas al personal que tiene documento activo de tipo `FA`, ordenadas por numero de documento.

## Query o select del reporte

```sql
SELECT UNIQUE
       SUBSTR(RD.NUMERO_DOCUMENTO,1,1) OFICINA
      ,RD.NUMERO_DOCUMENTO ORDEN
      ,RHPC.CODIGO_PERSONA
      ,RHPC.NOMBRE
      ,RHPC.APELLIDO
      ,RHPC.CEDULA
      ,RHPC.DESCRIPCION_CARGO
  FROM RH_V_PERSONAL_CARGO RHPC
      ,RH_DOCUMENTOS RD
 WHERE RHPC.CODIGO_PERSONA = RD.CODIGO_PERSONA
   AND RD.TIPO_DOCUMENTO_ID = (
       SELECT RD1.DESCRIPCION_ID
         FROM RH_DESCRIPTIVAS RD1
        WHERE RD1.DESCRIPCION_ID = RD.TIPO_DOCUMENTO_ID
          AND RD1.CODIGO = 'FA'
   )
   AND RD.FECHA_VENCIMIENTO IS NULL
 ORDER BY 2
```

## Parametros

La pantalla activa los filtros dinamicos luego de recibir los parametros:

```plsql
function AfterPForm return boolean is
  V_DESCRIPCION VARCHAR2(1000):=NULL;
  V_FECHA_NOMINA DATE;
  V_TIPO_NOMINA VARCHAR2(10);
begin

   IF :P_TIPO_GENERACION = 1 THEN
      :LP_FROM_TABLE1 := ' RH_TMP_NOMINA RTN ';
      :LP_FROM_TABLE2 := ' RH_V_PERSONAL_CARGO RVPC ';
      :LP_WHERE       := NULL;
   ELSIF :P_TIPO_GENERACION = 2 THEN
      :LP_FROM_TABLE1 := ' RH_TMP_NOMINA RTN ';
      :LP_FROM_TABLE2 := ' RH_V_PERSONAL_CARGO RVPC ';
      :LP_WHERE       := ' AND RTN.CODIGO_TIPO_NOMINA = RVPC.CODIGO_TIPO_NOMINA
                           AND RTN.CODIGO_PERIODO     = :P_CODIGO_PERIODO ';
   ELSIF :P_TIPO_GENERACION = 3 THEN
      :LP_FROM_TABLE1 := ' RH_HISTORICO_NOMINA RTN ';
      :LP_FROM_TABLE2 := ' RH_HISTORICO_PERSONAL_CARGO RVPC ';
      :LP_WHERE       := ' AND RVPC.FECHA_NOMINA        = :P_FECHA_PAGO
                           AND RTN.CODIGO_TIPO_NOMINA = RVPC.CODIGO_TIPO_NOMINA
                           AND RTN.CODIGO_PERIODO     = RVPC.CODIGO_PERIODO
                           AND RTN.CODIGO_PERIODO     = :P_CODIGO_PERIODO ';
   END IF;

   IF :P_CEDULA IS NOT NULL THEN
      :LP_CEDULA := ' AND RVPC.CEDULA = :P_CEDULA';
   END IF;

   return (TRUE);
end;
```

## Requerimiento stored procedure

Crear un stored procedure para Oracle 10 basado en `RH.SP_REP_GRAL_NOMINA_GET_ALL`.

Nombre propuesto: `RH.SP_REP_GRAL_NOM_FIR_GET_ALL`.

### Entradas sugeridas

```sql
p_from_table1    IN VARCHAR2
p_from_table2    IN VARCHAR2
p_tipo_nomina    IN NUMBER
p_codigo_empresa IN NUMBER
p_fecha_pago     IN DATE
p_where          IN VARCHAR2
p_cedula         IN VARCHAR2
```

### Salidas sugeridas

```sql
p_ResultSet      OUT SYS_REFCURSOR
p_Message        OUT VARCHAR2
p_TotalRecords   OUT NUMBER
```

### Columnas de salida

```sql
OFICINA
ORDEN
CODIGO_PERSONA
NOMBRE
APELLIDO
CEDULA
DESCRIPCION_CARGO
```

### Consideraciones

- Retornar `Success` en `p_Message` cuando la consulta se ejecute correctamente.
- Retornar el error en `p_Message` cuando ocurra una excepcion.
- Retornar un cursor vacio con la misma estructura del resultado cuando exista error o parametros dinamicos invalidos.
- Usar `COUNT(*)` sobre la consulta base para llenar `p_TotalRecords`.
- Mantener validacion de fragmentos dinamicos `FROM` y `WHERE` similar a `RH.SP_REP_GRAL_NOMINA_GET_ALL`.
- Incorporar `p_cedula` como fragmento opcional equivalente a `&LP_CEDULA`.
- Adaptar el alias del query base a `RVPC` cuando se usen las tablas dinamicas de la pantalla.
- Mantener el filtro de documentos activos `RD.FECHA_VENCIMIENTO IS NULL`.
- Mantener el filtro de documento de firma `RD1.CODIGO = 'FA'`.
- Mantener el orden `ORDER BY 2`.

## Requerimiento C#

Crear una clase C# usando arquitectura vertical slice, tomando como guia la estructura de la clase `ReporteGeneralNominaGetAll`.

- Crear la clase en la carpeta existente `Features/ReporteGeneralNomina`.
- Nombrar la clase `ReporteGeneralNominaFirmaGetAll`.
- Crear el request record con los parametros requeridos por el stored procedure.
- Crear el response record con las columnas retornadas por el query base.
- Crear el handler que invoque `RH.SP_REP_GRAL_NOM_FIR_GET_ALL`.
- Crear el endpoint siguiendo el patron del controlador existente.
- Reutilizar los helpers existentes para validacion de `WHERE`, lectura segura del `IDataReader` y resultado `ResultDto`.

## Plan

1. Crear el stored procedure `RH.SP_REP_GRAL_NOM_FIR_GET_ALL` con nombre menor a 30 caracteres.
2. Validar los fragmentos dinamicos `p_from_table1`, `p_from_table2`, `p_where` y `p_cedula`.
3. Construir la consulta dinamica usando las tablas de nomina/persona recibidas y el alias `RVPC`.
4. Aplicar filtros fijos de tipo de nomina, empresa, fecha de pago, documento `FA` activo y filtros opcionales.
5. Agregar conteo total con `COUNT(*)` sobre la consulta base.
6. Abrir el `SYS_REFCURSOR`, asignar `p_TotalRecords` y devolver `p_Message = 'Success'`.
7. En C#, crear el vertical slice `ReporteGeneralNominaFirmaGetAll` con query, response, handler y controller.
8. Probar compilacion del proyecto y validar que el endpoint compile con el patron existente.
