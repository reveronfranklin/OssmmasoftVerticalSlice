# Contrato frontend Bienes Municipales - Entregable 7

## Base

Procesos masivos usa `DefaultConnectionBM`.

El proceso implementado es cambio masivo de direccion. No edita fisicamente el bien: genera movimientos tipo `T` en `BM_MOV_BIENES`.

## Previsualizar

### `POST /api/BmProcesosMasivos/Preview`

Request:

```json
{
  "codigoIcp": 0,
  "codigoDirOrigen": 0,
  "codigoArticulo": 0,
  "placasCsv": "BM-000001,BM-000002",
  "responsableText": "",
  "codigoDirDestino": 12,
  "conceptoMovId": 50,
  "fechaMovimiento": "2026-06-26",
  "usuarioId": 1,
  "observacion": "Traslado masivo"
}
```

Los filtros de origen son opcionales y se aplican sobre los bienes vigentes de `BM.BM_V_BM1`.
`codigoDirDestino` se usa para mostrar la unidad destino si existe.

### Selectores de origen

`GET /api/Bm1/GetListICP` devuelve ICP/unidades con bienes existentes en `BM.BM_V_BM1`.

`POST /api/Bm1/GetUbicacionesOrigen` devuelve ubicaciones origen con bienes existentes en `BM.BM_V_BM1`.

Request:

```json
{
  "codigoIcp": 0,
  "codigoArticulo": 0,
  "responsableText": "",
  "searchText": "auditoria",
  "page": 1,
  "pageSize": 25
}
```

Response `data[]` usa el mismo formato de `BmUbicacionResponse`. Los campos de direccion no disponibles en `BM.BM_V_BM1` se devuelven en blanco o cero.

Las placas del selector usan `POST /api/Bm1/GetProductMobil` y tambien se filtran desde `BM.BM_V_BM1`.

Response `data[]`:

```json
{
  "codigoProcesoMasivo": 0,
  "codigoProcesoMasivoDet": 0,
  "codigoBien": 10,
  "numeroPlaca": "BM-000001",
  "articulo": "COMPUTADOR",
  "codigoDirOrigen": 20,
  "codigoIcpOrigen": 1001,
  "unidadOrigen": "DIRECCION A",
  "codigoDirDestino": 12,
  "unidadDestino": "DIRECCION B",
  "estado": "PREVIEW",
  "mensaje": "Bien seleccionado para cambio masivo.",
  "codigoMovBien": 0,
  "totalProcesados": 2,
  "totalExitosos": 0,
  "totalRechazados": 0
}
```

## Ejecutar

### `POST /api/BmProcesosMasivos/Execute`

Usa el mismo request de previsualizacion.

Validaciones:

- `codigoDirDestino`, `conceptoMovId` y `fechaMovimiento` son obligatorios.
- La ubicacion destino debe existir en `BM_DIR_BIEN`.
- Bienes con ultimo movimiento `D` o `E` son rechazados.
- Bienes ya ubicados en destino son rechazados.

Resultado:

- Crea cabecera en `BM_PROC_MASIVO`.
- Crea detalle en `BM_PROC_MAS_DET`.
- Inserta movimiento `T` para cada bien exitoso.
- Retorna detalle con totales del lote.

El manejo es parcial: los exitosos quedan aplicados y los rechazados se reportan con mensaje.
