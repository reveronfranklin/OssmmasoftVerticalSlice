# Reglas de Negocio Funcionales

## Objetivo

El Reporte General de Nomina permite consultar la informacion consolidada de una nomina, su detalle por trabajador y las firmas asociadas al reporte.

El punto de entrada funcional es el reporte completo, que devuelve tres secciones:

- `General`: resumen por conceptos.
- `Detalle`: movimientos de nomina por persona, cargo, concepto, asignacion y deduccion.
- `Firma`: personal con documento activo de firma.

## Parametros funcionales

### p_tipo_nomina

Identifica el tipo de nomina que se desea consultar.

Ejemplos:

- `21`: tipo de nomina usado en el caso de prenomina o archivo de pago del ejemplo.
- `13`: tipo de nomina usado en el caso historico del ejemplo.

### codigo_empresa

Identifica la empresa sobre la cual se consulta la nomina.

Ejemplo:

- `13`

### p_fecha_pago

Fecha de pago o fecha de nomina usada para ubicar la informacion del reporte.

Debe enviarse en formato ISO desde el API:

```json
"p_fecha_pago": "2026-10-09"
```

### p_tipo_generacion

Indica desde donde debe obtenerse la informacion.

| Valor | Uso funcional | Origen |
| --- | --- | --- |
| `1` | Ejecucion de prenomina sin obligar periodo | Tablas temporales |
| `2` | Generacion del archivo de pago o consulta temporal asociada a un periodo | Tablas temporales |
| `3` | Consulta de informacion ya historificada | Tablas historicas |

### p_codigo_periodo

Identifica el periodo de nomina.

Es obligatorio cuando:

- `p_tipo_generacion = 2`
- `p_tipo_generacion = 3`

Puede enviarse tambien con `p_tipo_generacion = 1` si se requiere acotar la prenomina a un periodo especifico.

### p_cedula

Filtro opcional para consultar una sola persona.

Si se envia `null` o vacio, el reporte devuelve todos los trabajadores que cumplan los filtros de nomina.

## Casos funcionales

### Caso 1: prenomina o archivo de pago

Se usa cuando la informacion aun esta en tablas temporales.

Para una ejecucion asociada a periodo, usar:

- `p_tipo_generacion = 2`
- `p_codigo_periodo = 5959`
- `p_tipo_nomina = 21`
- `codigo_empresa = 13`
- `p_fecha_pago = "2026-10-13"`

### Caso 2: historico de nomina

Se usa cuando la nomina ya fue cerrada/historificada.

Usar:

- `p_tipo_generacion = 3`
- `p_codigo_periodo = 5958`
- `p_tipo_nomina = 13`
- `codigo_empresa = 13`
- `p_fecha_pago = "2026-10-09"`

## Consideraciones funcionales

- El frontend no debe construir filtros SQL.
- El usuario solo debe seleccionar tipo de generacion, tipo de nomina, empresa, fecha, periodo y cedula opcional.
- Si falta `p_codigo_periodo` para generacion `2` o `3`, el reporte debe responder como invalido.
- La fecha del ejemplo original del historico aparece como `09-OCT-25` en una llamada SQL y como `09/10/2026` en los parametros. Los ejemplos JSON usan `2026-10-09`, que corresponde al parametro funcional indicado.
- La seccion de firmas actualmente no se filtra por nomina porque el stored procedure disponible no recibe esos parametros.
