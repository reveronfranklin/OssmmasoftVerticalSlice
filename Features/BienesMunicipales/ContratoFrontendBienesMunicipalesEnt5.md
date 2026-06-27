# Contrato frontend Bienes Municipales - Entregable 5

## Base

Todos los endpoints responden `ResultDto<T>` y usan `DefaultConnectionBM`.

No existe eliminacion fisica de bienes. La salida del patrimonio se registra como movimiento final. En el SQL legacy los movimientos tipo `D` y `E` dejan el bien fuera de la vista operativa `BM_V_BM1`.

Los valores validos de `tipoMovimiento` y conceptos deben venir de `BmDescriptivas`, especialmente las descriptivas con `tituloId = 4`.

## Movimientos

### `POST /api/BmMovBienes/GetByBien`

Request:

```json
{
  "codigoBien": 10
}
```

Response `data[]`:

```json
{
  "codigoMovBien": 100,
  "codigoBien": 10,
  "numeroPlaca": "BM-000001",
  "articulo": "COMPUTADOR",
  "tipoMovimiento": "T",
  "tipoMovimientoDescripcion": "TRASLADO",
  "fechaMovimiento": "2026-06-25T00:00:00",
  "fechaMovimientoString": "2026-06-25",
  "codigoDirBien": 12,
  "codigoIcp": 1001,
  "unidadEjecutora": "DIRECCION DE ADMINISTRACION",
  "conceptoMovId": 50,
  "conceptoMovimiento": "CAMBIO DE UBICACION",
  "codigoSolMovBien": 0,
  "esMovimientoFinal": false,
  "extra1": "",
  "extra2": "",
  "extra3": ""
}
```

### `POST /api/BmMovBienes/Create`

Request:

```json
{
  "codigoBien": 10,
  "tipoMovimiento": "T",
  "fechaMovimiento": "2026-06-25",
  "codigoDirBien": 12,
  "conceptoMovId": 50,
  "codigoSolMovBien": 0,
  "extra1": "",
  "extra2": "",
  "extra3": ""
}
```

Validaciones:

- El bien debe existir.
- `tipoMovimiento`, `fechaMovimiento`, `codigoDirBien` y `conceptoMovId` son obligatorios.
- La ubicacion debe existir.
- Si el ultimo movimiento del bien es final (`D` o `E`), solo se permite reincorporacion (`R`).
- La reincorporacion (`R`) solo se permite cuando el ultimo movimiento es final (`D` o `E`).
- `D` y `E` son movimientos finales; no eliminan el bien.

Matriz de transiciones:

| Ultimo movimiento | `T` traslado | `D` desincorporacion | `R` reincorporacion |
| --- | --- | --- | --- |
| Sin movimiento | Permitido | Permitido | No permitido |
| `T` | Permitido | Permitido | No permitido |
| `R` | Permitido | Permitido | No permitido |
| `D` o `E` | No permitido | No permitido | Permitido |

## Solicitudes de movimiento

### `POST /api/BmSolMovBienes/GetAll`

Request:

```json
{
  "aprobado": 0,
  "searchText": "BM-000001",
  "tipoMovimiento": "T",
  "fechaDesde": "2026-06-01",
  "fechaHasta": "2026-06-30",
  "codigoDirBien": 12,
  "page": 1,
  "pageSize": 50
}
```

`aprobado = -1` lista todas las solicitudes.

Filtros opcionales:

- `tipoMovimiento`: `T`, `D` o `R`.
- `fechaDesde` y `fechaHasta`: filtran por `FECHA_MOVIMIENTO`.
- `codigoDirBien`: filtra por ubicacion.

### `POST /api/BmSolMovBienes/Create`

Request:

```json
{
  "codigoBien": 10,
  "tipoMovimiento": "T",
  "fechaMovimiento": "2026-06-25",
  "codigoDirBien": 12,
  "conceptoMovId": 50,
  "numeroSolicitud": "",
  "usuarioSolicita": 1,
  "fechaIncidencia": "2026-06-25",
  "notaIncidencia": "Traslado solicitado",
  "extra1": "",
  "extra2": "",
  "extra3": ""
}
```

Reglas:

- Tipos permitidos: `T` traslado, `D` desincorporacion, `R` reincorporacion.
- `codigoBien`, `tipoMovimiento`, `fechaMovimiento`, `codigoDirBien` y `conceptoMovId` son obligatorios.
- Si `numeroSolicitud` viene vacio, el backend genera `SOL-########`.
- No se permite una solicitud pendiente duplicada para el mismo bien y tipo.
- La desincorporacion no elimina fisicamente el bien.

### `POST /api/BmSolMovBienes/Aprobar`

Request:

```json
{
  "codigoSolMovBien": 25
}
```

Al aprobar, el backend crea el registro correspondiente en `BM_MOV_BIENES` y marca la solicitud como aprobada. Aplica la misma matriz de transiciones del movimiento directo.

Response `data[]`:

```json
{
  "codigoSolMovBien": 25,
  "codigoBien": 10,
  "numeroPlaca": "BM-000001",
  "articulo": "COMPUTADOR",
  "tipoMovimiento": "T",
  "tipoMovimientoDescripcion": "TRASLADO",
  "fechaMovimiento": "2026-06-25T00:00:00",
  "fechaMovimientoString": "2026-06-25",
  "codigoDirBien": 12,
  "codigoIcp": 1001,
  "unidadEjecutora": "DIRECCION DE ADMINISTRACION",
  "conceptoMovId": 50,
  "conceptoMovimiento": "CAMBIO DE UBICACION",
  "numeroSolicitud": "SOL-001",
  "aprobado": true,
  "usuarioSolicita": 1,
  "fechaSolicita": "2026-06-25T10:00:00",
  "fechaSolicitaString": "2026-06-25",
  "fechaIncidencia": "2026-06-25T00:00:00",
  "notaIncidencia": "Traslado solicitado"
}
```
