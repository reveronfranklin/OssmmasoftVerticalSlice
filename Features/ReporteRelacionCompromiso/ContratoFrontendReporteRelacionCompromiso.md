# Contrato Frontend - ReporteRelacionCompromiso

Fecha: 2026-08-20. Requerimiento 25.

Migracion del reporte Oracle Reports `ADM_RELACION_COMPROMISO.rdf`
("Relacion de Compromisos", forma `SAMI-ADM_RELACION_COMPROMISO_OP_CH`).

Lista los compromisos de un presupuesto, netos de anulaciones, uniendo las tres
fuentes del sistema -compromisos administrativos, compromisos de presupuesto y
contratos- en un listado plano con un total de cierre.

## Como se consume normalmente: por el Motor de Formularios

**Este reporte no necesita pantalla propia.** El PLAN.md original preveia una
pantalla de filtros codificada a mano y dejaba abierta la pregunta de en que
modulo colgarla -no hay un modulo de "compromisos" en `src/adm` ni en
`src/presupuesto`-. El formulario de parametros vive en el Motor de Formularios
(requerimiento 16) y se abre en la pantalla generica:

```txt
/apps/mfo/reporte/REP_REL_COMPROMISO
```

Campos del formulario:

| Campo | Tipo | Obligatorio |
| --- | --- | --- |
| `PRESUPUESTO` | Catalogo `PRE_PRESUPUESTO` | **Si** |
| `FECHA_DESDE` | Fecha | No |
| `FECHA_HASTA` | Fecha | No |
| `PROVEEDOR` | Numero | No |

`CodigoEmpresa` y `Usuario` son parametros de origen `SISTEMA`: los resuelve el
servidor y un valor enviado en el payload se descarta sin mirarlo.

**El presupuesto es el unico parametro obligatorio de origen `CAMPO` de todo el
motor.** No se puede resolver en el servidor: no existe un
`settings:PresupuestoConfig` ni un concepto de "presupuesto activo" en el
backend, y el patron del ERP es que el usuario elija uno. El catalogo
`PRE_PRESUPUESTO` es nuevo y reusa `PRE.SP_PRE_PRESUP_LIST_GET` -el mismo
procedimiento que alimenta la pantalla de presupuestos-, marcando en la etiqueta
cual esta en ejecucion.

## Endpoint directo (opcional)

```http
POST /api/ReporteRelacionCompromiso/pdf
```

### Request

```json
{
  "codigoPresupuesto": 7,
  "fechaDesde": "2026-06-01",
  "fechaHasta": "2026-07-31",
  "codigoProveedor": 4210,
  "usuario": "cjhonny"
}
```

| Campo | Obligatorio | Notas |
| --- | --- | --- |
| `codigoPresupuesto` | Si | Cero o ausente devuelve `IsValid = false`. |
| `fechaDesde` | No | Vacio = sin limite inferior. |
| `fechaHasta` | No | Vacio = sin limite superior. Sin ninguna de las dos, lista el presupuesto completo. |
| `codigoProveedor` | No | Cero o nulo = todos. |
| `usuario` | Si | Usuario conectado, para el pie de auditoria. Vacio devuelve HTTP `400`. |

No lleva `codigoEmpresa`: se resuelve desde `settings:EmpresaConfig`.

Desde el frontend, el valor de `codigoPresupuesto` ya esta disponible en el store
de Redux `presupuesto` (`listpresupuestoDtoSeleccionado`), poblado por
`FilterOnlyPresupuesto.tsx`.

### Response exitoso

- HTTP `200`
- `Content-Type: application/pdf`
- `Content-Disposition: inline; filename="relacion-compromisos.pdf"`

### Response con error o sin datos

HTTP `200` con `ResultDto<T>` de `IsValid = false` y el mensaje en `Message`,
mismo patron que `ReporteBm1/pdf`:

- `"Indique el presupuesto del reporte."`
- `"La fecha desde no puede ser posterior a la fecha hasta."`
- `"No hay compromisos con los filtros seleccionados."`

El unico HTTP `400` es el de `usuario` vacio.

## Endpoint de datos

```http
POST /api/ReporteRelacionCompromiso/GetAll
```

Mismo request (sin `usuario`), devuelve `ResultDto<List<Item>>` plano.

```json
{
  "data": [
    {
      "codigoCompromiso": 4021,
      "numeroCompromiso": "CMC-CMP-00314",
      "fechaCompromiso": "2026-06-02T00:00:00",
      "nombreProveedor": "CONCEJO MUNICIPAL DE CHACAO",
      "montoCompromiso": 16.0,
      "origen": "PRE"
    }
  ],
  "isValid": true,
  "cantidadRegistros": 116,
  "total1": 490687911.95
}
```

`cantidadRegistros` y `total1` son exactamente los dos numeros de la linea de
cierre del PDF (`TOTAL 116 COMPROMISO POR Bs. 490.687.911,95`).

### Tres cosas que conviene entender

- **`origen` dice de que rama del UNION salio la fila**: `ADM` (compromisos
  administrativos), `PRE` (compromisos de presupuesto) o `CONTRATO`. El reporte
  legado no lo mostraba y el PDF tampoco lo imprime; se expone porque es lo unico
  que permite, ante un total que no cuadra, saber que fuente lo aporto.
- **`montoCompromiso` ya viene neto de anulaciones** a nivel de partida
  presupuestaria (`SUM(MONTO - MONTO_ANULADO)`), agrupado por compromiso. Cada
  compromiso sale en una sola fila.
- **Las filas con `origen = "CONTRATO"` tienen dos particularidades heredadas del
  reporte legado**: no se excluyen los contratos anulados, y el rango de fechas
  se les aplica sobre su fecha de insercion aunque lo que se muestre sea la fecha
  de contrato. Ver el PLAN.md del requerimiento 25.
