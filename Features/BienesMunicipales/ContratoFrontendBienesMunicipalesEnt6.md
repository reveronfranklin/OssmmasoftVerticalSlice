# Contrato frontend Bienes Municipales - Entregable 6

## Base

`ReporteBm1` se mantiene como feature separado porque ya tiene `GetAll`, `GetIcps`, `pdf` inline y `excel`, y el frontend lo usa con preview.

Los endpoints nuevos de `BmReportes` responden `ResultDto<T>`.

Conexiones:

- Reportes de bienes, lote, ficha, solicitudes, procesos masivos, ubicacion y movimientos usan `DefaultConnectionBM`.
- Reportes de conteo, diferencias e historico usan `DefaultConnectionBMC`.

## Reporte por placa

### `POST /api/BmReportes/Placa`

```json
{
  "numeroPlaca": "BM-000001"
}
```

Response `data[]`:

```json
{
  "codigoBien": 10,
  "numeroPlaca": "BM-000001",
  "articulo": "COMPUTADOR",
  "especificacion": "COMPUTADOR MARCA X",
  "valorInicial": 1200.5,
  "valorActual": 950,
  "codigoMovBien": 100,
  "tipoMovimiento": "T",
  "fechaMovimiento": "2026-06-25T00:00:00",
  "codigoDirBien": 12,
  "codigoIcp": 1001,
  "unidadEjecutora": "DIRECCION DE ADMINISTRACION",
  "responsableBien": "RESPONSABLE",
  "estadoOperativo": "ACTIVO"
}
```

## Reporte por ubicacion/ICP

### `POST /api/BmReportes/Ubicacion`

```json
{
  "codigoIcp": 1001
}
```

`codigoIcp = 0` lista todas las ubicaciones.

## Reporte de bienes incorporados por lote

### `POST /api/BmReportes/Lote`

```json
{
  "numeroLote": "20260001"
}
```

Response `data[]`: `codigoBien`, `numeroPlaca`, `numeroLote`, `articulo`, `fechaIns`, `fechaCompra`, `valorInicial`, `valorActual`, `codigoIcp`, `unidadEjecutora`, `responsableBien`.

## Reporte de ficha del bien

### `POST /api/BmReportes/Ficha`

```json
{
  "numeroPlaca": "BM-000001"
}
```

Response `data[]`: filas planas por `seccion` (`BIEN`, `FOTO`, `DETALLE`, `MOVIMIENTO`) con `referencia`, `descripcion`, `fecha`, `unidad` y `observacion`.

## Reporte de movimientos

### `POST /api/BmReportes/Movimientos`

```json
{
  "codigoBien": 10
}
```

Response: mismo shape de `BmMovBienes/GetByBien`.

## Reporte de movimientos por filtro

### `POST /api/BmReportes/MovimientosFiltro`

```json
{
  "tipoMovimiento": "T",
  "fechaDesde": "2026-01-01",
  "fechaHasta": "2026-06-25",
  "codigoIcp": 1001
}
```

Todos los filtros son opcionales. `codigoIcp = 0` retorna todos los ICP. Response: mismo shape de `BmMovBienes/GetByBien`.

## Reporte de solicitudes

### `POST /api/BmReportes/Solicitudes`

```json
{
  "aprobado": -1,
  "tipoMovimiento": "",
  "fechaDesde": "2026-01-01",
  "fechaHasta": "2026-06-25"
}
```

`aprobado = -1` retorna todas, `0` pendientes y `1` aprobadas.

## Reporte de procesos masivos

### `POST /api/BmReportes/ProcesosMasivos`

```json
{
  "codigoProcesoMasivo": 0,
  "fechaDesde": "2026-01-01",
  "fechaHasta": "2026-06-25"
}
```

`codigoProcesoMasivo = 0` retorna todos los procesos del rango.

## Diferencias de conteo

### `POST /api/BmReportes/ConteoDiferencias`

```json
{
  "codigoBmConteo": 12
}
```

Solo retorna filas con diferencia distinta de cero.

## Historico de conteos

### `POST /api/BmReportes/ConteoHistorico`

```json
{
  "fechaDesde": "2026-01-01",
  "fechaHasta": "2026-06-25"
}
```

Las fechas son opcionales. Si se omiten, retorna todo el historico disponible para la empresa configurada.

## Salidas PDF/Excel

- BM1: disponible hoy en `POST /api/ReporteBm1/pdf` y `POST /api/ReporteBm1/excel`.
- Resto de reportes: disponibles como PDF inline y Excel sobre los mismos filtros.
- PDF debe consumirse con `responseType: "blob"` y presentarse en el viewer/preview existente.
- Excel puede descargarse como archivo `.xlsx`.

Endpoints adicionales:

| Datos | PDF inline | Excel |
| --- | --- | --- |
| `POST /api/BmReportes/Placa` | `POST /api/BmReportes/PlacaPdf` | `POST /api/BmReportes/PlacaExcel` |
| `POST /api/BmReportes/Lote` | `POST /api/BmReportes/LotePdf` | `POST /api/BmReportes/LoteExcel` |
| `POST /api/BmReportes/Ficha` | `POST /api/BmReportes/FichaPdf` | `POST /api/BmReportes/FichaExcel` |
| `POST /api/BmReportes/Ubicacion` | `POST /api/BmReportes/UbicacionPdf` | `POST /api/BmReportes/UbicacionExcel` |
| `POST /api/BmReportes/Movimientos` | `POST /api/BmReportes/MovimientosPdf` | `POST /api/BmReportes/MovimientosExcel` |
| `POST /api/BmReportes/MovimientosFiltro` | `POST /api/BmReportes/MovimientosFiltroPdf` | `POST /api/BmReportes/MovimientosFiltroExcel` |
| `POST /api/BmReportes/Solicitudes` | `POST /api/BmReportes/SolicitudesPdf` | `POST /api/BmReportes/SolicitudesExcel` |
| `POST /api/BmReportes/ProcesosMasivos` | `POST /api/BmReportes/ProcesosMasivosPdf` | `POST /api/BmReportes/ProcesosMasivosExcel` |
| `POST /api/BmReportes/ConteoDiferencias` | `POST /api/BmReportes/ConteoDiferenciasPdf` | `POST /api/BmReportes/ConteoDiferenciasExcel` |
| `POST /api/BmReportes/ConteoHistorico` | `POST /api/BmReportes/ConteoHistoricoPdf` | `POST /api/BmReportes/ConteoHistoricoExcel` |
