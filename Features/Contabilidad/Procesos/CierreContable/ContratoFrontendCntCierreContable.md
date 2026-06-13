# Contrato Frontend - Cierre Contable CNT

## Base

- Modulo: CNT
- Ruta API: `api/CntCierreContable`
- Autenticacion: JWT/cookie existente
- Respuesta: `ResultDto<T>`
- Empresa: se resuelve en backend desde configuracion, no se envia desde frontend.

## Permisos

- `contabilidad.cierre.ver`
- `contabilidad.cierre.precierre`
- `contabilidad.cierre.cierre`
- `contabilidad.cierre.reverso`

## Endpoints

### GetPeriodos

`POST api/CntCierreContable/GetPeriodos`

Request:

```json
{
  "usuarioId": 1,
  "anoPeriodo": 2026,
  "soloPendientes": false,
  "searchText": ""
}
```

Response `data[]`:

```json
{
  "codigoPeriodo": 1,
  "nombrePeriodo": "ENERO 2026",
  "fechaDesde": "2026-01-01T00:00:00",
  "fechaHasta": "2026-01-31T00:00:00",
  "anoPeriodo": 2026,
  "numeroPeriodo": 1,
  "fechaPrecierre": null,
  "usuarioPrecierre": null,
  "fechaCierre": null,
  "usuarioCierre": null,
  "estado": "ABIERTO",
  "cantidadTmpSaldos": 0,
  "cantidadTmpAnalitico": 0,
  "cantidadSaldos": 0,
  "cantidadHistAnalitico": 0,
  "cantidadModificaciones": 0,
  "codigoEmpresa": 1
}
```

Estados:

- `ABIERTO`
- `PRECIERRE`
- `MODIFICADO`
- `CERRADO`

### Modificaciones

`POST api/CntCierreContable/Modificaciones`

Request:

```json
{
  "usuarioId": 1,
  "codigoPeriodo": 1
}
```

Response `data`:

```json
{
  "codigoPeriodo": 1,
  "cantidadModificaciones": 0
}
```

### Precierre

`POST api/CntCierreContable/Precierre`

Request:

```json
{
  "usuarioId": 1,
  "codigoPeriodo": 1
}
```

Response `data`:

```json
{
  "codigoPeriodo": 1,
  "estado": "PRECIERRE",
  "mensaje": "OK",
  "cantidadSaldos": 20,
  "cantidadAnalitico": 100
}
```

Nota: en esta respuesta `cantidadSaldos` representa saldos temporales generados y `cantidadAnalitico` representa analitico temporal generado.

### Cierre

`POST api/CntCierreContable/Cierre`

Request:

```json
{
  "usuarioId": 1,
  "codigoPeriodo": 1
}
```

Response `data`:

```json
{
  "codigoPeriodo": 1,
  "estado": "CERRADO",
  "mensaje": "OK",
  "cantidadSaldos": 20,
  "cantidadAnalitico": 100
}
```

### Reverso

`POST api/CntCierreContable/Reverso`

Request:

```json
{
  "usuarioId": 1,
  "codigoPeriodo": 1
}
```

Response `data`:

```json
{
  "codigoPeriodo": 1,
  "estado": "ABIERTO",
  "mensaje": "OK",
  "cantidadSaldos": 20,
  "cantidadAnalitico": 100
}
```

## Validaciones esperadas

- Periodo requerido.
- Usuario autenticado debe coincidir con `usuarioId`.
- Precierre requiere periodo abierto.
- Cierre requiere periodo con precierre y sin modificaciones posteriores.
- Reverso requiere periodo cerrado.
- Errores esperados se devuelven con `isValid = false` y `message` en castellano.
