# Reglas de Negocio Tecnicas

## Endpoint principal

```http
POST /api/ReporteGeneralNominaCompletoGetAll/GetAll
```

Archivo:

```text
ReporteGeneralNominaCompletoGetAll.cs
```

## Contrato de entrada

```csharp
public record ReporteGeneralNominaCompletoGetAllQuery(
    int p_tipo_nomina,
    int codigo_empresa,
    DateTime p_fecha_pago,
    int p_tipo_generacion,
    int? p_codigo_periodo,
    string? p_cedula
);
```

## Responsabilidad del punto de entrada

`ReporteGeneralNominaCompletoGetAll` debe recibir parametros de negocio y derivar los fragmentos requeridos por los handlers internos:

- `FromTable1`
- `FromTable2`
- `Where`
- `Cedula`

El cliente no debe enviar:

- `p_from_table1`
- `p_from_table2`
- `p_where`

## Validaciones de entrada

Antes de invocar los handlers internos, se validan estas reglas:

- `p_tipo_nomina > 0`
- `codigo_empresa > 0`
- `p_tipo_generacion` debe ser `1`, `2` o `3`
- `p_codigo_periodo` es obligatorio para `p_tipo_generacion = 2`
- `p_codigo_periodo` es obligatorio para `p_tipo_generacion = 3`
- Si `p_codigo_periodo` viene informado, debe ser mayor que cero
- `p_cedula` no puede contener comilla simple, punto y coma, comentario SQL de linea ni comentario SQL de bloque

## Derivacion tecnica por tipo de generacion

### p_tipo_generacion = 1

Origen temporal de prenomina.

```text
FromTable1 = RH_TMP_NOMINA RTN
FromTable2 = RH_V_PERSONAL_CARGO RVPC
Where      = RTN.CODIGO_TIPO_NOMINA = RVPC.CODIGO_TIPO_NOMINA
```

Si `p_codigo_periodo` viene informado, se agrega:

```sql
RTN.CODIGO_PERIODO = {p_codigo_periodo}
```

### p_tipo_generacion = 2

Origen temporal asociado a periodo.

```text
FromTable1 = RH_TMP_NOMINA RTN
FromTable2 = RH_V_PERSONAL_CARGO RVPC
Where      = RTN.CODIGO_TIPO_NOMINA = RVPC.CODIGO_TIPO_NOMINA
             AND RTN.CODIGO_PERIODO = {p_codigo_periodo}
```

### p_tipo_generacion = 3

Origen historico.

```text
FromTable1 = RH_HISTORICO_NOMINA RTN
FromTable2 = RH_HISTORICO_PERSONAL_CARGO RVPC
Where      = RTN.CODIGO_TIPO_NOMINA = RVPC.CODIGO_TIPO_NOMINA
             AND RVPC.FECHA_NOMINA = DATE '{p_fecha_pago:yyyy-MM-dd}'
             AND RTN.CODIGO_PERIODO = RVPC.CODIGO_PERIODO
             AND RTN.CODIGO_PERIODO = {p_codigo_periodo}
```

## Cedula opcional

Si `p_cedula` viene con valor, se deriva:

```sql
RVPC.CEDULA = '{p_cedula}'
```

Si viene `null`, vacia o con espacios, se envia como cadena vacia al handler de detalle.

## Orquestacion interna

El handler completo ejecuta secuencialmente:

1. `GetReporteGeneralNominaGetAllHandler`
2. `GetReporteGeneralNominaDetalleGetAllHandler`
3. `GetReporteGeneralNominaFirmaGetAllHandler`

Si una ejecucion falla, se retorna `IsValid = false` con el mensaje del componente que fallo.

## Stored procedures usados

### General

```sql
RH.SP_REP_GRAL_NOMINA_GET_ALL
```

Parametros enviados:

- `p_from_table1`
- `p_from_table2`
- `p_tipo_nomina`
- `p_fecha_pago`
- `p_codigo_empresa`
- `p_where`

### Detalle

```sql
RH.SP_REP_GRAL_NOM_DET_GET_ALL
```

Parametros enviados:

- `p_from_table1`
- `p_from_table2`
- `p_tipo_nomina`
- `p_codigo_empresa`
- `p_fecha_pago`
- `p_where`
- `p_cedula`

### Firma

```sql
RH.SP_REP_GRAL_NOM_FIR_GET_ALL
```

Parametros enviados:

- Ningun parametro de entrada.

Limitacion actual: este stored procedure no recibe filtros de nomina, empresa, fecha, periodo ni cedula. Por lo tanto la seccion `Firma` no queda acotada por los mismos criterios de `General` y `Detalle`.

## Ejemplos de ejecucion

Ver archivos JSON en:

```text
Examples/
```

Cada archivo contiene un payload compatible con `ReporteGeneralNominaCompletoGetAllQuery`.
