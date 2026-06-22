# Contrato Frontend - Reporte BM1

## Base

Modulo backend: `api/ReporteBm1`.

Los reportes PDF deben presentarse en preview. El Excel se descarga como archivo `.xlsx`.

## Obtener Datos

```http
POST /api/ReporteBm1/GetAll
```

Request:

```json
{
  "fechaDesde": "2026-01-01",
  "fechaHasta": "2026-06-22",
  "codigosIcp": [2251, 2252]
}
```

`codigosIcp` puede enviarse vacio o `null` para consultar todos los ICP.

Response:

```json
{
  "data": [
    {
      "unidadTrabajo": "UNIDAD",
      "codigoGrupo": "01",
      "codigoNivel1": "02",
      "codigoNivel2": "03",
      "numeroLote": "1",
      "cantidad": 1,
      "numeroPlaca": "00001",
      "valorActual": 10.5,
      "articulo": "ARTICULO",
      "especificacion": "ESPECIFICACION",
      "servicio": "SERVICIO",
      "responsableBien": "RESPONSABLE",
      "fechaMovimiento": "2026-06-22T00:00:00"
    }
  ],
  "isValid": true,
  "message": "Success",
  "cantidadRegistros": 1
}
```

## Obtener ICP

```http
GET /api/ReporteBm1/GetIcps
```

Response:

```json
{
  "data": [
    {
      "codigoIcp": 2251,
      "unidadTrabajo": "UNIDAD EJECUTORA"
    }
  ],
  "isValid": true,
  "message": "Success",
  "cantidadRegistros": 1
}
```

## PDF

```http
POST /api/ReporteBm1/pdf
```

Usa el mismo request de `GetAll`.

Respuesta exitosa:

- `Content-Type: application/pdf`
- `Content-Disposition: inline`

El frontend debe consumirlo como `blob` y presentarlo con preview, no descargarlo automaticamente.

## Excel

```http
POST /api/ReporteBm1/excel
```

Usa el mismo request de `GetAll`.

Respuesta exitosa:

- `Content-Type: application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`
- Archivo sugerido: `reporte-bm1-{yyyyMMddHHmmss}.xlsx`

## Validaciones

- `fechaDesde` es obligatoria.
- `fechaHasta` es obligatoria.
- `fechaDesde` no puede ser mayor que `fechaHasta`.
- Si la conexion BM falla, el response JSON retorna `isValid: false`.
