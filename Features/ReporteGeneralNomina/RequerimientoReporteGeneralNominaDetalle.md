# Requerimiento: Reporte General de Nomina Detalle

Dado el siguiente query, crear un stored procedure en Oracle 10 que retorne el resultado de la consulta y un parametro de salida `message` con el posible error o `Success` cuando sea satisfactorio.

Usar como guia la estructura del stored procedure `RH.SP_REP_GRAL_NOMINA_GET_ALL`.

## Detalle del reporte

El reporte debe devolver el detalle de movimientos de nomina por persona, cargo, concepto, monto, asignacion y deduccion, filtrado por tipo de nomina, empresa y fecha de pago.

## Parametros

```sql
&LP_FROM_TABLE1
,&LP_FROM_TABLE2
:P_TIPO_NOMINA
:CODIGO_EMPRESA
:P_FECHA_PAGO
&LP_WHERE
&LP_CEDULA
```

## Query base

```sql
SELECT UNIQUE
  RTN.FECHA_NOMINA FECHA_PERIODO_NOMINA
 ,:P_FECHA_PAGO FECHA_EMISION_NOMINA
 ,RTN.CODIGO_PERIODO
 ,RTN.CODIGO_TIPO_NOMINA
 ,PVIO.CODIGO_SECTOR||'-'||
  PVIO.CODIGO_PROGRAMA||'-'||
  PVIO.CODIGO_SUBPROGRAMA||'-'||
  PVIO.CODIGO_PROYECTO||'-'||
  PVIO.CODIGO_ACTIVIDAD||
  DECODE(PVIO.CODIGO_OFICINA,'00',NULL,'-'||PVIO.CODIGO_OFICINA) CODIGO_OFICINA
 ,PVIO.CODIGO_ICP
 ,PVIO.DENOMINACION
 ,RVPC.DESCRIPCION_CARGO DENOMINACION_CARGO
 ,RVPC.CEDULA
 ,RVPC.APELLIDO||' '||RVPC.NOMBRE NOMBRE
 ,RVPC.NO_CUENTA
 ,RC.CODIGO NUMERO_CONCEPTO
 ,DECODE(RTN.TIPO,'F','FIJO','V','VARIABLE','E','ESPECIAL','P','PROGRAMADO') TIPO_MOV_CONCEPTO
 ,RC.DENOMINACION DENOMINACION_CONCEPTO
 ,RTN.COMPLEMENTO_CONCEPTO
 ,NULL PORCENTAJE
 ,RC.TIPO_CONCEPTO
 ,SIS_RECONVERTIR_OLD('DUMMY',RTN.FECHA_NOMINA,RTN.MONTO) MONTO
 ,SIS_RECONVERTIR_OLD('DUMMY',RTN.FECHA_NOMINA,DECODE(SIGN(RTN.MONTO),1,RTN.MONTO,0)) ASIGNACION
 ,SIS_RECONVERTIR_OLD('DUMMY',RTN.FECHA_NOMINA,DECODE(SIGN(RTN.MONTO),-1,-(RTN.MONTO),0)) DEDUCCION
 ,RVPC.STATUS
 ,RVPC.DESCRIPCION_STATUS
 ,RVPC.CODIGO_PERSONA
 ,RVPC.FECHA_INGRESO
 ,RVPC.CARGO_CODIGO
 ,REPLACE(RVPC.DESCRIPCION_BANCO,'BANCO ','') BANCO
 ,RTN.CODIGO_CONCEPTO
 ,RTN.EXTRA1 MODULO
 ,RTN.EXTRA2 CODIGO_IDENTIFICADOR
FROM
  &LP_FROM_TABLE1
 ,&LP_FROM_TABLE2
 ,RH_CONCEPTOS RC
 ,PRE_INDICE_CAT_PRG PVIO
WHERE RVPC.CODIGO_TIPO_NOMINA  = :P_TIPO_NOMINA
  AND RVPC.CODIGO_TIPO_NOMINA  = RTN.CODIGO_TIPO_NOMINA
  AND RVPC.CODIGO_EMPRESA      = :CODIGO_EMPRESA
  AND RTN.CODIGO_PERSONA       = RVPC.CODIGO_PERSONA
  AND RTN.FECHA_NOMINA         = :P_FECHA_PAGO
  AND RC.CODIGO_CONCEPTO       = RTN.CODIGO_CONCEPTO
  AND PVIO.CODIGO_ICP          = RVPC.CODIGO_ICP
  &LP_WHERE
  &LP_CEDULA
ORDER BY 5,9,17,18 DESC
```

## Requerimiento stored procedure

Crear un stored procedure para Oracle 10 basado en `RH.SP_REP_GRAL_NOMINA_GET_ALL`.

Nota: el nombre del stored procedure debe tener maximo 30 caracteres por compatibilidad con Oracle.
Nombre propuesto: `RH.SP_REP_GRAL_NOM_DET_GET_ALL`.

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

### Consideraciones

- Mantener validacion de fragmentos dinamicos `FROM` y `WHERE` similar a `RH.SP_REP_GRAL_NOMINA_GET_ALL`.
- Incorporar `p_cedula` como fragmento opcional equivalente a `&LP_CEDULA`.
- Retornar `Success` en `p_Message` cuando la consulta se ejecute correctamente.
- Retornar un cursor vacio con la misma estructura del resultado cuando exista error o parametros dinamicos invalidos.
- Usar `COUNT(*)` sobre la consulta base para llenar `p_TotalRecords`.
- Mantener el orden `ORDER BY 5,9,17,18 DESC`.

## Requerimiento C#

Crear una clase C# usando arquitectura vertical slice, tomando como guia la estructura de la clase `ReporteGeneralNominaGetAll`.

- Crear la clase en la carpeta existente `Features/ReporteGeneralNomina`.
- Nombrar la clase `ReporteGeneralNominaDetalleGetAll`.
- Crear el request record con los parametros requeridos por el stored procedure.
- Crear el response record con las columnas retornadas por el query base.
- Crear el handler que invoque el nuevo stored procedure.
- Crear el endpoint siguiendo el patron del controlador existente.
- Reutilizar los helpers existentes para validacion de `WHERE`, lectura segura del `IDataReader` y resultado `ResultDto`.
