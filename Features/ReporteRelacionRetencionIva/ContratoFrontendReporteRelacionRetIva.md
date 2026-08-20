# Contrato Frontend - ReporteRelacionRetIva

Fecha: 2026-08-20. Requerimiento 22.

Migracion del reporte Oracle Reports `ADM_RELACION_RETENCION_IVA_OP2.rdf`
("Relacion de Retenciones por IVA", por periodos de orden de pago).

## Como se consume normalmente: por el Motor de Formularios

**Este reporte no necesita pantalla propia.** Su formulario de parametros vive
en el Motor de Formularios (requerimiento 16) y se abre en la pantalla generica:

```txt
/apps/mfo/reporte/REP_RET_IVA_PER
```

Los campos del formulario son `FECHA_DESDE` (obligatorio), `FECHA_HASTA`
(obligatorio) y `ESTATUS` (opcional, lista AP/PE/AN). `CodigoEmpresa` y
`Usuario` son parametros de origen `SISTEMA`: los resuelve el servidor y un valor
enviado en el payload se descarta sin mirarlo.

El PDF vuelve por el endpoint de ejecucion del motor
(`api/MfoReporte/ejecutar`), que ya esta cableado al visor existente. No hay que
agregar servicio, slice ni ruta en el frontend para este reporte.

## Endpoint directo (opcional)

Existe tambien el endpoint propio de la feature, para el caso de querer el
reporte desde una pantalla de ADM en vez del motor.

```http
POST /api/ReporteRelacionRetIva/pdf
```

### Request

```json
{
  "fechaDesde": "2026-07-16",
  "fechaHasta": "2026-07-31",
  "estatus": "AP",
  "usuario": "jperez"
}
```

| Campo | Obligatorio | Notas |
| --- | --- | --- |
| `fechaDesde` | Si | Primer dia del periodo, inclusive. |
| `fechaHasta` | Si | Ultimo dia del periodo, inclusive. La hora de `FECHA_INS` no excluye el ultimo dia. |
| `estatus` | No | `AP`, `PE` o `AN`. Vacio o nulo = todos. Cualquier otro valor devuelve `IsValid = false`. |
| `usuario` | Si | Usuario conectado. Alimenta el pie de auditoria (requerimiento 17). Vacio devuelve HTTP `400`. |

No lleva `codigoEmpresa`: se resuelve en el backend desde
`settings:EmpresaConfig`.

### Response exitoso

- HTTP `200`
- `Content-Type: application/pdf`
- `Content-Disposition: inline; filename="relacion-retenciones-iva.pdf"`
- Body: bytes del PDF.

### Response con error o sin datos

HTTP `200` con un `ResultDto<T>` de `IsValid = false` y el mensaje en `Message`
(mismo patron que `ReporteBm1/pdf`). Se usa en dos casos:

- validacion (`"Indique la fecha desde y la fecha hasta del periodo."`,
  `"La fecha desde no puede ser posterior a la fecha hasta."`,
  `"El estatus debe ser AP (aprobado), PE (pendiente) o AN (anulado)."`)
- periodo sin retenciones (`"No hay retenciones de IVA en el periodo
  seleccionado."`)

El unico HTTP `400` es el de `usuario` vacio.

## Endpoint de datos

```http
POST /api/ReporteRelacionRetIva/GetAll
```

Mismo request (sin `usuario`), devuelve `ResultDto<List<Comprobante>>` para
consumirlo en una grilla en vez de PDF.

```json
{
  "data": [
    {
      "numeroComprobante": "20260700000758",
      "fecha": "2026-07-20T00:00:00",
      "numeroOrdenPago": "362",
      "estatusDescripcion": "APROBADO",
      "nombreProveedor": "CANTV",
      "rifProveedor": "0000000000",
      "totalRetenido": 22231.77,
      "documentos": [
        {
          "numeroOperacion": 1,
          "fechaDocumento": "2026-07-04T00:00:00",
          "numeroFactura": "NCEF000931260436",
          "numeroDocumento": "NCEF000931260436",
          "montoDocumento": 161180.33,
          "baseImponible": 138948.56,
          "montoImpuestoExento": 0,
          "alicuota": "16%",
          "montoImpuesto": 22231.77,
          "montoRetenido": 22231.77,
          "montoRetenidoNeto": 22231.77
        }
      ]
    }
  ],
  "isValid": true,
  "cantidadRegistros": 1,
  "total1": 22231.77
}
```

`cantidadRegistros` es el numero de documentos (filas de detalle), no de
comprobantes. `total1` es el total retenido del periodo, el mismo que imprime la
linea TOTAL del PDF.

### Dos campos que conviene entender

- **`montoRetenido` vs `montoRetenidoNeto`.** El primero es
  `ADM_DOCUMENTOS_OP.MONTO_RETENIDO` tal cual. El segundo es la columna `DECODE`
  del reporte legado: igual al anterior salvo cuando la orden de pago esta
  anulada, donde va en negativo. **El PDF y `total1` usan el neto**, para que un
  periodo que incluya anulaciones cuadre.
- **`rifProveedor` no se imprime en el PDF**, igual que en el reporte legado
  (el layout V2 solo muestra el nombre). Se devuelve porque el query lo calcula
  y porque una grilla si lo puede querer.
