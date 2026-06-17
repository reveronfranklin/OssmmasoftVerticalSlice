# Contrato Frontend - Reporte General Nomina Completo

## Endpoint

```Base:
 http://ossmmasoft.com.ve:5142
```

```http
POST /api/ReporteGeneralNominaCompletoGetAll/GetAll
Content-Type: application/json
```

Este endpoint devuelve en una sola llamada la informacion necesaria para pintar el Reporte General de Nomina:

- `periodo`: datos descriptivos del periodo consultado, cuando se envia `p_codigo_periodo`.
- `general`: resumen consolidado por concepto.
- `detalle`: movimientos de nomina por trabajador/concepto.
- `firma`: firmantes activos del reporte.

El frontend solo debe enviar parametros funcionales. No debe enviar tablas, filtros SQL ni condiciones internas.

## Request

```json
{
  "p_tipo_nomina": 21,
  "codigo_empresa": 13,
  "p_fecha_pago": "2025-10-13",
  "p_tipo_generacion": 2,
  "p_codigo_periodo": 5959,
  "p_cedula": null
}
```

## Campos del request

| Campo               | Tipo        | Requerido   | Descripcion                                                                                 |
| ------------------- | ----------- | ----------- | ------------------------------------------------------------------------------------------- |
| `p_tipo_nomina`     | number      | Si          | Codigo del tipo de nomina. Debe ser mayor que cero.                                         |
| `codigo_empresa`    | number      | Si          | Codigo de la empresa. Debe ser mayor que cero.                                              |
| `p_fecha_pago`      | string date | Si          | Fecha de pago o fecha de nomina en formato ISO `YYYY-MM-DD`.                                |
| `p_tipo_generacion` | number      | Si          | Define el origen de datos: `1` prenomina temporal, `2` temporal por periodo, `3` historico. |
| `p_codigo_periodo`  | number/null | Condicional | Obligatorio para `p_tipo_generacion = 2` y `p_tipo_generacion = 3`. Opcional para `1`.      |
| `p_cedula`          | string/null | No          | Cedula para filtrar una sola persona. Enviar `null` para consultar todos.                   |

## Tipos de generacion

| Valor | Uso en UI                               | Periodo requerido |
| ----- | --------------------------------------- | ----------------- |
| `1`   | Pre-nomina temporal sin obligar periodo | No                |
| `2`   | Nomina temporal asociada a periodo      | Si                |
| `3`   | Nomina historica/cerrada                | Si                |

## Ejemplos de request

### Pre-nomina temporal

```json
{
  "p_tipo_nomina": 21,
  "codigo_empresa": 13,
  "p_fecha_pago": "2025-10-13",
  "p_tipo_generacion": 1,
  "p_codigo_periodo": null,
  "p_cedula": null
}
```

### Temporal por periodo

```json
{
  "p_tipo_nomina": 21,
  "codigo_empresa": 13,
  "p_fecha_pago": "2025-10-13",
  "p_tipo_generacion": 2,
  "p_codigo_periodo": 5959,
  "p_cedula": null
}
```

### Historico completo

```json
{
  "p_tipo_nomina": 13,
  "codigo_empresa": 13,
  "p_fecha_pago": "2025-10-09",
  "p_tipo_generacion": 3,
  "p_codigo_periodo": 5958,
  "p_cedula": null
}
```

### Historico filtrado por cedula

```json
{
  "p_tipo_nomina": 13,
  "codigo_empresa": 13,
  "p_fecha_pago": "2025-10-09",
  "p_tipo_generacion": 3,
  "p_codigo_periodo": 5958,
  "p_cedula": "14892529"
}
```

## Response exitoso

```json
{
  "data": {
    "periodo": {
      "codigoPeriodo": 5959,
      "descripcion": "NOMINA OCTUBRE 2025",
      "codigoTipoNomina": 21,
      "descripcionTipoNomina": "CONCEJALES",
      "fechaNomina": "2025-10-13T00:00:00",
      "periodo": 1,
      "descripcionPeriodo": "1ra. Quincena",
      "tipoNomina": "N",
      "tipoNominaDescripcion": "NORMAL"
    },
    "general": [
      {
        "rTipoConcepto": "A",
        "rNumeroConcepto": "072",
        "rDenominacionConcepto": "BONO TRANSPORTE",
        "rAsignacion": 67900,
        "rDeduccion": 0,
        "rMontoVisible": 67900,
        "rMonto": 67900,
        "rDeducible": 0
      }
    ],
    "detalle": [
      {
        "fechaPeriodoNomina": "2025-10-13T00:00:00",
        "fechaEmisionNomina": "2025-10-13T00:00:00",
        "codigoPeriodo": 5959,
        "codigoTipoNomina": 21,
        "codigoOficina": "01-02-01-00-51",
        "codigoIcp": 2251,
        "denominacion": "SERVICIOS DE LEGISLACION DE CAPITAL HUMANO",
        "denominacionCargo": "CONCEJAL TITULAR",
        "cedula": "6979855",
        "nombre": "GONZALEZ FLORES OSCAR ALBERTO",
        "noCuenta": "01340038520383096118",
        "numeroConcepto": "072",
        "tipoMovConcepto": "ESPECIAL",
        "denominacionConcepto": "BONO TRANSPORTE",
        "complementoConcepto": "OCTUBRE 2025",
        "porcentaje": 0,
        "tipoConcepto": "A",
        "monto": 9700,
        "asignacion": 9700,
        "deduccion": 0,
        "status": "A",
        "descripcionStatus": "ACTIVO",
        "activos": 1,
        "permisos": 0,
        "vacaciones": 0,
        "reposos": 0,
        "codigoPersona": 1068,
        "fechaIngreso": "2018-12-12T00:00:00",
        "cargoCodigo": "10414",
        "banco": "BANESCO",
        "codigoConcepto": 1753,
        "modulo": "",
        "codigoIdentificador": ""
      }
    ],
    "firma": [
      {
        "oficina": "1",
        "descripcionOficina": "GERENCIA DE BENEFICIOS Y GESTION HUMANA",
        "orden": "1-1",
        "codigoPersona": 2330,
        "nombre": "ANA CAROLINA",
        "apellido": "GUZMAN DE BARROETA",
        "cedula": "7926904",
        "descripcionCargo": "GERENTE DE BIENESTAR SOCIAL"
      }
    ]
  },
  "isValid": true,
  "linkData": null,
  "linkDataArlternative": null,
  "message": "Success",
  "page": 0,
  "totalPage": 0,
  "cantidadRegistros": 15,
  "total1": 0,
  "total2": 0,
  "total3": 0,
  "total4": 0
}
```

## Wrapper de respuesta

| Campo                                  | Tipo        | Descripcion                                                                                                                     |
| -------------------------------------- | ----------- | ------------------------------------------------------------------------------------------------------------------------------- |
| `data`                                 | object/null | Contiene `periodo`, `general`, `detalle` y `firma` cuando `isValid = true`. Puede venir `null` si hay error de validacion o base de datos. |
| `isValid`                              | boolean     | Indica si la consulta fue exitosa.                                                                                              |
| `message`                              | string      | Mensaje de resultado. En exito normalmente viene `Success`.                                                                     |
| `cantidadRegistros`                    | number      | Total reportado por la orquestacion del backend.                                                                                |
| `page`, `totalPage`                    | number      | Actualmente no se usan para paginacion en este endpoint.                                                                        |
| `total1`, `total2`, `total3`, `total4` | number      | Totales genericos del `ResultDto`; actualmente no se usan para este reporte.                                                    |

## Modelo `data.periodo`

Puede venir `null` cuando `p_codigo_periodo` no se envia, por ejemplo en una pre-nomina temporal con `p_tipo_generacion = 1`.

| Campo                    | Tipo        | Uso sugerido                                                          |
| ------------------------ | ----------- | --------------------------------------------------------------------- |
| `codigoPeriodo`          | number      | Codigo del periodo consultado.                                        |
| `descripcion`            | string      | Descripcion registrada en `RH_PERIODOS`.                              |
| `codigoTipoNomina`       | number      | Codigo del tipo de nomina asociado al periodo.                        |
| `descripcionTipoNomina`  | string      | Descripcion del tipo de nomina.                                       |
| `fechaNomina`            | string/null | Fecha de nomina del periodo.                                          |
| `periodo`                | number      | Numero de periodo/quincena.                                           |
| `descripcionPeriodo`     | string      | Texto calculado: `1ra. Quincena`, `2da. Quincena` o no definido.      |
| `tipoNomina`             | string      | Codigo de tipo de nomina: `E`, `N` u otro valor registrado.           |
| `tipoNominaDescripcion`  | string      | Texto calculado: `ESPECIAL`, `NORMAL` o no definido.                  |

## Modelo `data.general`

| Campo                   | Tipo   | Uso sugerido                                                          |
| ----------------------- | ------ | --------------------------------------------------------------------- |
| `rTipoConcepto`         | string | Tipo de concepto. Ejemplo: `A`. Puede venir vacio para filas resumen. |
| `rNumeroConcepto`       | string | Numero del concepto. Puede venir vacio para filas resumen.            |
| `rDenominacionConcepto` | string | Nombre del concepto o agrupador.                                      |
| `rAsignacion`           | number | Monto de asignacion.                                                  |
| `rDeduccion`            | number | Monto de deduccion.                                                   |
| `rMontoVisible`         | number | Monto que puede mostrarse como total visible de la fila.              |
| `rMonto`                | number | Monto base del concepto.                                              |
| `rDeducible`            | number | Indicador/valor deducible retornado por backend.                      |

## Modelo `data.detalle`

| Campo                  | Tipo        | Uso sugerido                                                  |
| ---------------------- | ----------- | ------------------------------------------------------------- |
| `fechaPeriodoNomina`   | string/null | Fecha del periodo de nomina.                                  |
| `fechaEmisionNomina`   | string/null | Fecha de emision.                                             |
| `codigoPeriodo`        | number      | Codigo del periodo.                                           |
| `codigoTipoNomina`     | number      | Codigo del tipo de nomina.                                    |
| `codigoOficina`        | string      | Codigo de oficina/estructura.                                 |
| `codigoIcp`            | number      | Codigo ICP.                                                   |
| `denominacion`         | string      | Denominacion de la unidad/oficina.                            |
| `denominacionCargo`    | string      | Cargo del trabajador.                                         |
| `cedula`               | string      | Cedula del trabajador.                                        |
| `nombre`               | string      | Nombre completo del trabajador.                               |
| `noCuenta`             | string      | Numero de cuenta bancaria. Tratar como texto, no como numero. |
| `numeroConcepto`       | string      | Numero del concepto.                                          |
| `tipoMovConcepto`      | string      | Tipo de movimiento.                                           |
| `denominacionConcepto` | string      | Nombre del concepto.                                          |
| `complementoConcepto`  | string      | Complemento o descripcion adicional.                          |
| `porcentaje`           | number      | Porcentaje asociado, si aplica.                               |
| `tipoConcepto`         | string      | Tipo de concepto. Ejemplo: `A`.                               |
| `monto`                | number      | Monto del movimiento.                                         |
| `asignacion`           | number      | Monto de asignacion.                                          |
| `deduccion`            | number      | Monto de deduccion.                                           |
| `status`               | string      | Codigo de estatus.                                            |
| `descripcionStatus`    | string      | Descripcion del estatus.                                      |
| `activos`              | number      | Indicador 1/0 cuando `status = A`.                            |
| `permisos`             | number      | Indicador 1/0 cuando `status = P`.                            |
| `vacaciones`           | number      | Indicador 1/0 cuando `status = V`.                            |
| `reposos`              | number      | Indicador 1/0 cuando `status = R`.                            |
| `codigoPersona`        | number      | Identificador interno de persona.                             |
| `fechaIngreso`         | string/null | Fecha de ingreso del trabajador.                              |
| `cargoCodigo`          | string      | Codigo del cargo.                                             |
| `banco`                | string      | Banco asociado a la cuenta.                                   |
| `codigoConcepto`       | number      | Identificador interno del concepto.                           |
| `modulo`               | string      | Modulo asociado, puede venir vacio.                           |
| `codigoIdentificador`  | string      | Identificador adicional, puede venir vacio.                   |

## Modelo `data.firma`

| Campo              | Tipo   | Uso sugerido                           |
| ------------------ | ------ | -------------------------------------- |
| `oficina`          | string | Grupo/oficina de firma.                |
| `descripcionOficina` | string | Descripcion calculada desde `oficina`. Si `oficina = "1"`, retorna `GERENCIA DE BENEFICIOS Y GESTION HUMANA`. Si `oficina = "2"`, retorna `GERENCIA DE NOMINA, COMPENSACION LABORAL Y CONTROL PRESUPUESTARIO`. Para otros valores retorna vacio. |
| `orden`            | string | Orden de presentacion. Ejemplo: `2-1`. |
| `codigoPersona`    | number | Identificador interno de persona.      |
| `nombre`           | string | Nombre del firmante.                   |
| `apellido`         | string | Apellido del firmante.                 |
| `cedula`           | string | Cedula del firmante.                   |
| `descripcionCargo` | string | Cargo usado en la firma.               |

## Response invalido

El endpoint puede responder HTTP 200 con `isValid = false` cuando falla una validacion de negocio.

```json
{
  "data": null,
  "isValid": false,
  "message": "El parametro p_codigo_periodo es obligatorio para p_tipo_generacion 2 y 3.",
  "cantidadRegistros": 0
}
```

Tambien puede responder HTTP 500 si ocurre una excepcion no controlada:

```json
{
  "message": "Error interno en el servidor",
  "detail": "Detalle tecnico del error"
}
```

## Validaciones sugeridas en frontend

- `p_tipo_nomina` requerido y mayor que `0`.
- `codigo_empresa` requerido y mayor que `0`.
- `p_fecha_pago` requerida en formato `YYYY-MM-DD`.
- `p_tipo_generacion` requerido y limitado a `1`, `2`, `3`.
- `p_codigo_periodo` requerido cuando `p_tipo_generacion` sea `2` o `3`.
- `p_codigo_periodo`, si se envia, debe ser mayor que `0`.
- `p_cedula` es opcional. Si no se filtra por cedula, enviar `null`.
- No permitir caracteres de riesgo en cedula: comilla simple (`'`), punto y coma (`;`), `--`, `/*`, `*/`.

## Consideraciones para UI

- Mostrar estado de carga durante la consulta.
- Si `isValid = false`, mostrar `message` al usuario y no intentar leer `data`.
- Si `isValid = true` pero alguna lista viene vacia, mostrar estado vacio por seccion.
- `data.periodo` contiene el encabezado/descriptivo del periodo cuando la consulta incluye `p_codigo_periodo`.
- Formatear montos como moneda/numero decimal segun la configuracion visual del sistema.
- Tratar cedulas, numeros de cuenta y codigos con ceros o guiones como texto.
- Las fechas llegan como ISO con hora (`YYYY-MM-DDT00:00:00`) en la respuesta; para visualizacion se pueden mostrar como `DD/MM/YYYY`.
- La seccion `firma` actualmente no se filtra por nomina, empresa, fecha, periodo ni cedula porque el procedimiento almacenado de firma no recibe parametros.

## Archivos de ejemplo en backend

Los payloads de referencia estan en:

```text
Features/ReporteGeneralNomina/Examples/
```

Archivos disponibles:

- `reporte-general-nomina-temporal-prenomina.json`
- `reporte-general-nomina-temporal-periodo.json`
- `reporte-general-nomina-historico.json`
- `reporte-general-nomina-historico-cedula.json`
- `result.json`

Nota: este documento usa las fechas de los JSON de ejemplo actuales (`2025-10-13` y `2025-10-09`).
