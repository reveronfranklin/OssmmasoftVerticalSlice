# Contrato frontend Bienes Municipales - Entregable 3

## Base

Todos los endpoints responden `ResultDto<T>` y usan `DefaultConnectionBM`.

No se exponen endpoints de eliminacion para catalogos en este corte. Clasificaciones, articulos y descriptivas pueden estar referenciados por bienes, movimientos o conteos historicos.

## Titulos

### `POST /api/BmTitulos/GetAll`

```json
{
  "searchText": "MOVIMIENTO",
  "page": 1,
  "pageSize": 50
}
```

### `POST /api/BmTitulos/Create`

```json
{
  "tituloId": 0,
  "tituloFkId": 0,
  "titulo": "TIPO DE MOVIMIENTO",
  "codigo": "MOV",
  "extra1": "",
  "extra2": "",
  "extra3": ""
}
```

### `POST /api/BmTitulos/Update`

Mismo cuerpo de `Create`, con `tituloId` existente.

Response `data[]`:

```json
{
  "tituloId": 4,
  "tituloFkId": 0,
  "titulo": "TIPO DE MOVIMIENTO",
  "codigo": "MOV",
  "extra1": "",
  "extra2": "",
  "extra3": ""
}
```

## Descriptivas

### `POST /api/BmDescriptivas/GetAll`

```json
{
  "tituloId": 4,
  "searchText": "TRASLADO",
  "page": 1,
  "pageSize": 50
}
```

`tituloId = 0` lista todas las descriptivas.

### `POST /api/BmDescriptivas/GetByTitulo`

Endpoint existente para selects simples.

```json
{
  "descripcionId": 0,
  "tituloId": 4
}
```

### `POST /api/BmDescriptivas/Create`

```json
{
  "descripcionId": 0,
  "descripcionFkId": 0,
  "tituloId": 4,
  "descripcion": "TRASLADO",
  "codigo": "T",
  "extra1": "",
  "extra2": "",
  "extra3": ""
}
```

### `POST /api/BmDescriptivas/Update`

Mismo cuerpo de `Create`, con `descripcionId` existente.

## Clasificacion de bienes

### `POST /api/BmClasificacionBienes/GetAll`

```json
{
  "searchText": "EQUIPO",
  "page": 1,
  "pageSize": 50
}
```

### `POST /api/BmClasificacionBienes/Create`

```json
{
  "codigoClasificacionBien": 0,
  "codigoGrupo": "1",
  "codigoNivel1": "01",
  "codigoNivel2": "02",
  "codigoNivel3": "03",
  "denominacion": "EQUIPOS DE COMPUTACION",
  "descripcion": "Activos tecnologicos",
  "fechaIni": "2026-01-01",
  "fechaFin": null
}
```

### `POST /api/BmClasificacionBienes/Update`

Mismo cuerpo de `Create`, con `codigoClasificacionBien` existente.

## Articulos

### `POST /api/BmArticulos/GetAll`

```json
{
  "searchText": "COMPUTADOR",
  "page": 1,
  "pageSize": 50
}
```

### `POST /api/BmArticulos/Create`

```json
{
  "codigoArticulo": 0,
  "codigoClasificacionBien": 10,
  "codigo": "CPU",
  "denominacion": "COMPUTADOR",
  "descripcion": "Equipo de computacion"
}
```

### `POST /api/BmArticulos/Update`

Mismo cuerpo de `Create`, con `codigoArticulo` existente.

## Detalle requerido por articulo

### `POST /api/BmDetalleArticulos/GetByArticulo`

```json
{
  "codigoArticulo": 10
}
```

### `POST /api/BmDetalleArticulos/Create`

```json
{
  "codigoDetalleArticulo": 0,
  "codigoArticulo": 10,
  "tipoEspecificacionId": 819
}
```

### `POST /api/BmDetalleArticulos/Update`

Mismo cuerpo de `Create`, con `codigoDetalleArticulo` existente.

## Titulos/descriptivas funcionales identificados

- Origen del bien: se usa en `BM_BIENES.ORIGEN_ID`.
- Tipo de especificacion: se usa en `BM_DETALLE_ARTICULOS.TIPO_ESPECIFICACION_ID` y `BM_DETALLE_BIENES.TIPO_ESPECIFICACION_ID`.
- Tipo/concepto de movimiento: el SQL legacy usa descriptivas con `TITULO_ID = 4`.
- Cantidad de conteos y motivos de diferencia: usados por el flujo de conteo en `DefaultConnectionBMC`.
