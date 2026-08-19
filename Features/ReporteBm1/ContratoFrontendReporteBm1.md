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

---

## `POST api/ReporteBm1/pdfEspecial`

Formulario oficial **BM-1 (Inventario de Bienes Muebles)**. Requerimiento 27.

Comparte el query de negocio con `pdf` -es el mismo `SP_REP_BM1_GET`- y cambia el
layout y los filtros disponibles.

```json
{
  "codigoDirBien": 12,
  "placaDesde": "04000",
  "placaHasta": "04999",
  "codigoArticulo": null,
  "fechaDesde": "2026-01-01",
  "fechaHasta": "2026-08-19",
  "responsable": "Juan Perez"
}
```

**Todos los campos son opcionales**, igual que en el reporte legado que
sustituye: sin ninguno imprime el inventario completo de la empresa.

`responsable` **no filtra**: es el nombre que se imprime en el bloque de firmas.

Devuelve el PDF con `Content-Disposition: inline`, o `ResultDto` con
`isValid: false` cuando no hay datos o el rango de fechas esta invertido.

### Diferencias con `pdf`

| | `pdf` | `pdfEspecial` |
| --- | --- | --- |
| Layout | Tabla plana | Formulario oficial con encabezado de entidad y firmas |
| Fechas | Obligatorias | Opcionales |
| Filtros | ICP | Unidad, placa (rango), articulo, fecha (rango) |
| Agrupamiento | Ninguno | Por unidad, con salto de pagina y subtotal |
| Orden | Fecha de movimiento | Unidad, luego clasificacion |

### Desde el Motor de Formularios

Este reporte esta ademas enlazado al formulario `REP_BM1_ESP`
(`/apps/mfo/reporte/REP_BM1_ESP`), que es la via recomendada: los filtros se
pintan solos desde la definicion y se pueden cambiar sin desplegar. El endpoint
directo se mantiene para integraciones que no pasen por el motor.
