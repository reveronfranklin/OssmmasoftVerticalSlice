# Contrato frontend - Bienes Municipales Entregable 1

## Conexion backend

El frontend consume endpoints verticales bajo `api/...`.

- Procesos BM1, descriptivas y placas en cuarentena usan `DefaultConnectionBM`.
- Procesos de conteo, detalle, recepcion movil, ubicaciones por responsable e historico usan `DefaultConnectionBMC`.

Todas las respuestas backend usan `ResultDto<T>`:

```json
{
  "data": [],
  "isValid": true,
  "message": "Success",
  "cantidadRegistros": 0
}
```

## BM1

### `GET api/Bm1/GetListICP`

Devuelve ICP/unidades con bienes vigentes.

Item:

```json
{
  "codigoIcp": 1001,
  "unidadTrabajo": "DIRECCION DE ADMINISTRACION"
}
```

### `GET api/Bm1/GetPlacas`

Devuelve placas disponibles para selectores y cuarentena.

Item:

```json
{
  "numeroPlaca": "BM-00000001",
  "articulo": "COMPUTADOR",
  "searchText": "BM-00000001 COMPUTADOR"
}
```

### `GET api/Bm1/GetFechaPrimerMovimiento`

Devuelve la primera fecha de movimiento registrada para la empresa configurada.

### `POST api/Bm1/GetByListIcp`

Request:

```json
{
  "fechaDesde": "2026-01-01",
  "fechaHasta": "2026-06-25",
  "listIcpSeleccionado": [
    {
      "codigoIcp": 1001,
      "unidadTrabajo": "DIRECCION DE ADMINISTRACION"
    }
  ]
}
```

Item:

```json
{
  "unidadTrabajo": "DIRECCION DE ADMINISTRACION",
  "codigoGrupo": "1",
  "codigoNivel1": "01",
  "codigoNivel2": "01",
  "numeroLote": "L-001",
  "cantidad": 1,
  "numeroPlaca": "00001",
  "valorActual": 100.0,
  "articulo": "COMPUTADOR",
  "especificacion": "COMPUTADOR / 2026-06-25",
  "servicio": "",
  "responsableBien": "RESPONSABLE",
  "searchText": "DIRECCION DE ADMINISTRACION COMPUTADOR 00001",
  "linkData": "",
  "codigoBien": 10,
  "codigoMovBien": 20,
  "fechaMovimiento": "2026-06-25T00:00:00",
  "year": 2026,
  "month": 6,
  "nroPlaca": "BM-00000001"
}
```

### `POST api/Bm1/GetProductMobil`

Request:

```json
{
  "codigoBmConteo": 1,
  "codigoDirBien": 0
}
```

Devuelve bienes para captura movil.

## Descriptivas

### `POST api/BmDescriptivas/GetByTitulo`

Request:

```json
{
  "descripcionId": 0,
  "tituloId": 7
}
```

Item:

```json
{
  "id": 1,
  "descripcionId": 1,
  "descripcion": "Un conteo",
  "codigo": "1",
  "extra1": "",
  "extra2": "",
  "extra3": ""
}
```

## Conteo

### `GET api/BmConteo/GetAll`

Lista conteos activos en BMC.

### `POST api/BmConteo/Create`

Request:

```json
{
  "codigoBmConteo": 0,
  "titulo": "Inventario fisico junio 2026",
  "comentario": "",
  "codigoPersonaResponsable": 123,
  "conteoId": 1,
  "fecha": "2026-06-25",
  "fechaString": "2026-06-25",
  "fechaObj": {
    "year": 2026,
    "month": 6,
    "day": 25
  },
  "listIcpSeleccionado": []
}
```

El backend crea `BMC.BM_CONTEO` y ejecuta `BMC.BM_P_CONTEO`.

### `POST api/BmConteo/Update`

Actualiza datos del conteo activo.

### `POST api/BmConteo/Delete`

Request:

```json
{
  "codigoBmConteo": 1
}
```

Elimina conteo activo y su detalle.

### `POST api/BmConteo/CerrarConteo`

Request:

```json
{
  "codigoBmConteo": 1,
  "comentario": "Cierre validado"
}
```

Copia conteo y detalle a historico, calcula totales y elimina el conteo activo.

## Detalle de conteo

### `POST api/BmConteoDetalle/GetAllByConteo`

Request:

```json
{
  "codigoBmConteo": 1
}
```

### `POST api/BmConteoDetalle/GetAllByConteoComparar`

Misma entrada que `GetAllByConteo`; devuelve estructura compatible para pantalla de comparacion.

### `POST api/BmConteoDetalle/Update`

Request:

```json
{
  "codigoBmConteoDetalle": 100,
  "cantidadContada": 1,
  "comentario": "Verificado",
  "replicarComentario": false
}
```

### `POST api/BmConteoDetalle/RecibeConteo`

Request:

```json
[
  {
    "articulo": "COMPUTADOR",
    "codigoDirBien": 10,
    "id": 50,
    "keyUbicacionResponsable": "1-1-DIRECCION",
    "nroPlaca": "BM-00000001",
    "unidadEjecutora": "DIRECCION",
    "ubicacionFisica": 1001
  }
]
```

Nota: el primer corte recibe el lote y responde `Success`. La normalizacion definitiva del lote movil queda pendiente de cerrar con el formato final de captura.

## Ubicaciones por responsable

### `POST api/BmUbicacionesResponsable/GetByUsuarioResponsable`

Request:

```json
{
  "codigoUsuario": 1
}
```

Devuelve conteos/ubicaciones disponibles para captura movil.

## Historico

### `GET api/BmConteoHistorico/GetAll`

Devuelve conteos cerrados desde BMC.

## Placas en cuarentena

### `GET api/BmPlacaCuarentena/GetAll`

Lista placas bloqueadas.

### `POST api/BmPlacaCuarentena/Create`

Request:

```json
{
  "codigoPlacaCuarentena": 0,
  "numeroPlaca": "BM-00000001"
}
```

### `POST api/BmPlacaCuarentena/Delete`

Request:

```json
{
  "codigoPlacaCuarentena": 1,
  "numeroPlaca": ""
}
```
