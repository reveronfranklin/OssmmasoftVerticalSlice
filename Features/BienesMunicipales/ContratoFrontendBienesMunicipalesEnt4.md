# Contrato frontend Bienes Municipales - Entregable 4

## Base

Los endpoints de ubicacion maestra usan `DefaultConnectionBM` y responden `ResultDto<T>`.

La ubicacion por responsable del flujo movil se mantiene separada en `BmUbicacionesResponsable` y usa `DefaultConnectionBMC`, porque depende de conteos activos.

## ICP / Unidad de trabajo

### `GET /api/BmUbicaciones/GetIcp`

Response `data[]`:

```json
{
  "codigoIcp": 1001,
  "unidadTrabajo": "DIRECCION DE ADMINISTRACION"
}
```

## Ubicaciones

### `POST /api/BmUbicaciones/GetAll`

Request:

```json
{
  "searchText": "ADMINISTRACION",
  "page": 1,
  "pageSize": 50
}
```

### `POST /api/BmUbicaciones/GetByIcp`

Request:

```json
{
  "codigoIcp": 1001
}
```

Response `data[]`:

```json
{
  "codigoDirBien": 10,
  "codigoIcp": 1001,
  "unidadEjecutora": "DIRECCION DE ADMINISTRACION",
  "paisId": 58,
  "estadoId": 1,
  "municipioId": 1,
  "ciudadId": 0,
  "parroquiaId": 0,
  "sectorId": 0,
  "urbanizacionId": 0,
  "manzanaId": 0,
  "parcelaId": 0,
  "vialidadId": 0,
  "vialidad": "AVENIDA PRINCIPAL",
  "tipoViviendaId": 0,
  "vivienda": "SEDE",
  "tipoNivelId": 0,
  "nivel": "PB",
  "tipoUnidadId": 0,
  "numeroUnidad": "01",
  "complementoDir": "ARCHIVO CENTRAL",
  "tenenciaId": 0,
  "codigoPostal": 0,
  "fechaIni": "2026-01-01T00:00:00",
  "fechaFin": null,
  "unidadTrabajoId": 0,
  "direccion": "AVENIDA PRINCIPAL SEDE PB 01 ARCHIVO CENTRAL",
  "searchText": "1001 DIRECCION DE ADMINISTRACION ARCHIVO CENTRAL"
}
```

### `POST /api/BmUbicaciones/Create`

Request:

```json
{
  "codigoDirBien": 0,
  "codigoIcp": 1001,
  "paisId": 58,
  "estadoId": 1,
  "municipioId": 1,
  "ciudadId": 0,
  "parroquiaId": 0,
  "sectorId": 0,
  "urbanizacionId": 0,
  "manzanaId": 0,
  "parcelaId": 0,
  "vialidadId": 0,
  "vialidad": "AVENIDA PRINCIPAL",
  "tipoViviendaId": 0,
  "vivienda": "SEDE",
  "tipoNivelId": 0,
  "nivel": "PB",
  "tipoUnidadId": 0,
  "numeroUnidad": "01",
  "complementoDir": "ARCHIVO CENTRAL",
  "tenenciaId": 0,
  "codigoPostal": 0,
  "fechaIni": "2026-01-01",
  "fechaFin": null,
  "unidadTrabajoId": 0
}
```

### `POST /api/BmUbicaciones/Update`

Mismo cuerpo de `Create`, con `codigoDirBien` existente.

El trigger legacy `BM_AFT_UPD_DEL` registra historico en `BM_DIR_H_BIEN` al actualizar la direccion.

## Historico

### `POST /api/BmUbicacionesHistorico/GetByDir`

Request:

```json
{
  "codigoDirBien": 10
}
```

Response `data[]`:

```json
{
  "codigoHDirBien": 1,
  "codigoDirBien": 10,
  "codigoIcp": 1001,
  "unidadEjecutora": "DIRECCION DE ADMINISTRACION",
  "direccion": "AVENIDA PRINCIPAL SEDE PB 01 ARCHIVO CENTRAL",
  "fechaIni": "2026-01-01T00:00:00",
  "fechaFin": null,
  "fechaHIns": "2026-06-25T10:00:00"
}
```

## Dependencias

- `PRE.PRE_INDICE_CAT_PRG` para resolver `codigoIcp` y unidad ejecutora.
- `SIS.SIS_UBICACION_NACIONAL` existe en el modelo legacy para direccion nacional, pero este corte no desencripta sus descripciones.
- `BM_CONCAT_DIRECCION` queda como funcion legacy disponible; los endpoints generan `direccion` con campos directos de `BM_DIR_BIEN` para evitar dependencia con `RM_DESCRIPTIVAS`.
