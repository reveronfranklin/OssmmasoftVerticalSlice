# Contrato frontend Bienes Municipales - Entregable 2

## Base

Todos los endpoints responden `ResultDto<T>`.

La ficha del bien y fotos usan `DefaultConnectionBM`.

## Bienes

### `POST /api/BmBienes/GetAll`

Request:

```json
{
  "searchText": "CPU",
  "page": 1,
  "pageSize": 25
}
```

Response `data[]`:

```json
{
  "codigoBien": 10,
  "codigoArticulo": 3,
  "articulo": "COMPUTADOR",
  "numeroPlaca": "BM-000001",
  "numeroLote": "L-001",
  "valorInicial": 1200.5,
  "valorActual": 950,
  "fechaCompra": "2026-01-15T00:00:00",
  "fechaCompraString": "2026-01-15",
  "fechaFactura": "2026-01-15T00:00:00",
  "fechaFacturaString": "2026-01-15",
  "numeroFactura": "F-100",
  "numeroOrdenCompra": "OC-25",
  "codigoProveedor": 0,
  "proveedor": "",
  "origenId": 1,
  "origen": "COMPRA",
  "tipoImpuestoId": 2,
  "tipoImpuesto": "IVA",
  "especificacion": "COMPUTADOR MARCA X",
  "servicio": "",
  "responsableBien": "RESPONSABLE",
  "unidadTrabajo": "DIRECCION",
  "searchText": "BM-000001 COMPUTADOR L-001"
}
```

### `POST /api/BmBienes/GetById`

Request:

```json
{
  "codigoBien": 10
}
```

Response: mismo item de `GetAll`.

### `POST /api/BmBienes/GetByNumeroPlaca`

Request:

```json
{
  "numeroPlaca": "BM-000001"
}
```

Response: mismo item de `GetAll`.

### `POST /api/BmBienes/Create`

Request:

```json
{
  "codigoBien": 0,
  "codigoArticulo": 3,
  "codigoDirBien": 15,
  "cantidad": 2,
  "codigoProveedor": 0,
  "codigoOrdenCompra": 0,
  "origenId": 1,
  "fechaFabricacion": null,
  "numeroOrdenCompra": "OC-25",
  "fechaCompra": "2026-01-15",
  "numeroPlaca": "",
  "numeroLote": "",
  "valorInicial": 1200.5,
  "valorActual": 1200.5,
  "numeroFactura": "F-100",
  "fechaFactura": "2026-01-15",
  "tipoImpuestoId": 2,
  "usuarioId": 0
}
```

Response: lista con el o los bienes creados.

Validaciones en base de datos:

- `codigoDirBien` es obligatorio para registrar el movimiento inicial.
- `cantidad` nula o menor/igual a cero se interpreta como `1`.
- Si `numeroLote` viene vacio, se genera con `MAX(NUMERO_LOTE) + 1` por empresa.
- Si `numeroPlaca` viene vacia, se genera desde `BM.NUMBER_PLACA(codigoArticulo, codigoEmpresa)`, adaptada desde la funcion legacy `number_placa`.
- Si `cantidad` es mayor a `1`, se genera una placa por cada bien.
- `numeroPlaca` debe ser unico por `codigoEmpresa`.
- `numeroPlaca` no puede existir en `BM_PLACAS_CUARENTENA`.
- Cada bien creado registra un movimiento inicial `TIPO_MOVIMIENTO = 'I'`.
- Cada bien creado registra auditoria en `BM_TMP_INSERT_BIENES`.

### `POST /api/BmBienes/Update`

Request: mismo cuerpo de `Create`, con `codigoBien` existente.

Response: lista con el bien actualizado.

Reglas:

- `numeroPlaca` no se actualiza desde ficha.
- `numeroLote` no se actualiza desde ficha.
- La ubicacion no se actualiza desde ficha; cambia por movimientos.
- `usuarioId` es opcional; si se envia mayor a cero se registra en `USUARIO_UPD`.
- El backend registra `FECHA_UPD = SYSDATE` en cada actualizacion.

No se expone endpoint de eliminacion de bienes. La baja, desincorporacion o exclusion del bien se registra en el flujo de movimientos para preservar historial.

## Especificaciones del bien

### `POST /api/BmDescriptivas/GetByFk`

Request:

```json
{
  "descripcionFkId": 100
}
```

Response `data[]`:

```json
{
  "id": 200,
  "descripcionId": 200,
  "descripcion": "DELL",
  "codigo": "01",
  "extra1": "",
  "extra2": "",
  "extra3": ""
}
```

Uso:

- Para detalles del bien, `descripcionFkId` corresponde al `tipoEspecificacionId`.
- El `id` retornado se envia como `especificacionId` en `BmDetalleBienes/Create` o `Update`.

### `POST /api/BmDetalleBienes/GetByBien`

Request:

```json
{
  "codigoBien": 10
}
```

Response `data[]`:

```json
{
  "codigoDetalleBien": 1,
  "codigoBien": 10,
  "tipoEspecificacionId": 100,
  "tipoEspecificacion": "MARCA",
  "especificacionId": 200,
  "especificacionIdDescripcion": "DELL",
  "especificacion": "OPTIPLEX"
}
```

### `POST /api/BmDetalleBienes/Create`

Request:

```json
{
  "codigoDetalleBien": 0,
  "codigoBien": 10,
  "tipoEspecificacionId": 100,
  "especificacionId": 0,
  "especificacion": "Serial ABC123",
  "usuarioId": 0
}
```

Response: lista actualizada de detalles del bien.

Reglas:

- `codigoBien` es obligatorio y debe existir.
- `tipoEspecificacionId` es obligatorio.
- No se permite duplicar `tipoEspecificacionId` para el mismo `codigoBien`.
- `especificacionId` es opcional; cuando no aplica se envia `0`.
- Si se usa valor catalogado, `especificacionId` debe venir de `BmDescriptivas/GetByFk`.
- `especificacion` permite texto libre.
- `usuarioId` es opcional; si se envia mayor a cero se registra en auditoria.

### `POST /api/BmDetalleBienes/Update`

Request: mismo cuerpo de `Create`, con `codigoDetalleBien` existente.

Response: lista actualizada de detalles del bien.

Reglas:

- No se permite cambiar el detalle a un tipo ya usado por otro detalle del mismo bien.
- Registra `FECHA_UPD = SYSDATE` y `USUARIO_UPD` cuando se envie `usuarioId`.
- No se expone borrado de detalles. La eliminacion de especificaciones del bien no esta disponible en este modulo.

## Fotos del bien

### `POST /api/BmBienesFotos/GetByNumeroPlaca`

Request:

```json
{
  "numeroPlaca": "BM-000001"
}
```

Response `data[]`:

```json
{
  "codigoBienFoto": 1,
  "codigoBien": 10,
  "numeroPlaca": "BM-000001",
  "foto": "10_BM000001_20260625120000000_abcd.jpg",
  "titulo": "Frontal",
  "patch": "/api/BmBienesFotos/Image?numeroPlaca=BM-000001&foto=10_BM000001_20260625120000000_abcd.jpg"
}
```

`patch` es la URL que debe usar el frontend para visualizar o descargar la foto. El endpoint busca primero en
`settings:BmFiles/<numeroPlaca>/<foto>`, luego en `settings:BmFiles/<foto>` para compatibilidad con cargas
anteriores, y finalmente intenta `settings:BmFiles/no-product-image.png`.

### `GET /api/BmBienesFotos/Image`

Query string:

- `numeroPlaca`: placa del bien.
- `foto`: nombre del archivo registrado en BD.

Response: binario de la imagen con su `Content-Type`.

### `POST /api/BmBienesFotos/AddImage/{codigoBien}`

Content-Type: `multipart/form-data`.

Campos:

- `files`: uno o varios archivos.
- `numeroPlaca`: placa del bien.
- `titulo`: opcional.

Ejemplo:

```text
POST /api/BmBienesFotos/AddImage/10
files=<archivo>
numeroPlaca=BM-000001
titulo=Frontal
```

El backend guarda el archivo en `settings:BmFiles/<numeroPlaca>` y registra en BD solo el nombre del archivo.

Reglas de pantalla:

- La ficha muestra indicador de minimo esperado `fotos cargadas/3`.
- El indicador no bloquea la carga ni otros procesos; solo identifica si la ficha esta completa a nivel visual.
- No se define maximo de fotos en este corte.
- La eliminacion de fotos no se expone desde la pantalla de Ficha de Bienes en este corte.

### `POST /api/BmBienesFotos/Delete`

Request:

```json
{
  "codigoBienFoto": 1
}
```

Response: lista actualizada de fotos de la placa asociada al registro eliminado. El archivo fisico no se elimina en este corte.

## Validaciones

- `settings:EmpresaConfig` es obligatorio.
- Si no se adjunta archivo en `AddImage`, retorna `isValid = false`.
- Alta de bienes valida placa unica y cuarentena desde los stored procedures.
- Edicion de bienes no modifica placa, lote ni ubicacion.
- No existe eliminacion fisica de bienes. La salida del patrimonio se maneja por movimientos.
