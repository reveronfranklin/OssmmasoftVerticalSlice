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
  ],
  "searchValue": "00213"
}
```

`searchValue` es opcional. Vacio o ausente no filtra; con valor se compara sin distinguir mayusculas
contra `codigoBien`, `numeroPlaca`, `nroPlaca`, `articulo`, `responsableBien` y `unidadTrabajo`. El
filtro se resuelve en `BM.SP_BM1_GET_BY_ICP`, de modo que `cantidadRegistros` corresponde siempre al
conjunto filtrado.

Los bienes con placa registrada en `BM.BM_PLACAS_CUARENTENA` quedan excluidos del resultado.

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
  "nroPlaca": "BM-00000001",
  "placaBarra": "1-01-01-00001"
}
```

`placaBarra` es el valor que se imprime como codigo de barras en la etiqueta, compuesto en el
procedimiento como `codigoGrupo-codigoNivel1-codigoNivel2-numeroPlaca`. Reproduce el formato del
sistema anterior para que los lectores reconozcan las etiquetas ya impresas.

### `POST api/Bm1/PlacasPdf`

Genera el PDF de etiquetas de placas. Recibe el **mismo** cuerpo que `GetByListIcp` y resuelve el
filtro con la misma lectura, de modo que el conjunto de etiquetas coincide siempre con el del grid.

Request: identico a `POST api/Bm1/GetByListIcp`.

Respuestas:

| Caso | Respuesta |
|---|---|
| Hay bienes | `application/pdf` con `Content-Disposition: inline` y cabecera `X-Bm1-Placas-Count` con la cantidad de etiquetas |
| Error de configuracion o de base de datos | `ResultDto` con `isValid: false` y el mensaje correspondiente |
| El filtro no devuelve bienes | `ResultDto` con `isValid: false` y `message: "No hay bienes para generar placas con los filtros seleccionados."` |

El PDF trae una etiqueta por pagina, sin paginas en blanco: la cantidad de paginas es igual a
`X-Bm1-Placas-Count`. El nombre de archivo incluye la marca de tiempo, por lo que no hay archivo
compartido entre usuarios.

Debajo del codigo de barras se imprime el numero de placa en claro, igual que en el sistema anterior,
para poder identificar el bien a simple vista si el lector falla.

Las dos imagenes de la etiqueta se resuelven igual que en el sistema anterior: las claves
`ESCUDO_CHACAO` (izquierda) y `LOGO_CHACAO` (derecha) de `SIS.OSS_CONFIG` guardan el nombre del
archivo, que se lee de la carpeta `settings:BmFiles`. Requiere `SIS.SP_OSS_CONFIG_GET_VALOR`
(`Sql/11_SP_OSS_CONFIG_GET_VALOR.sql`). Si la carpeta no esta disponible se cae a los assets
versionados de `Assets/Reports`, que son equivalentes pero no identicos -el logotipo versionado es la
version a color-, y si tampoco estan, el recuadro queda vacio y la etiqueta se emite igual. Ni un
fallo de base de datos ni uno de disco impiden generar el PDF.

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
  "usuarioResponsable": "nombre.login"
}
```

Devuelve conteos/ubicaciones disponibles para captura movil. Conserva la logica legacy: el filtro
se realiza por `LOGIN`, sin distinguir mayusculas y minusculas.

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
