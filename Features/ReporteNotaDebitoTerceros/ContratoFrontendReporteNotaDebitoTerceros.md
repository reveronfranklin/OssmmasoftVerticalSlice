# Contrato Frontend - ReporteNotaDebitoTerceros

## Objetivo

Migrar el reporte `debit-note-third-parties` del ReportServer al backend vertical slice y mostrarlo en preview desde `/apps/adm/pagos/lotes/`.

## Endpoints

### Obtener datos

- Metodo: `POST`
- Ruta: `/api/ReporteNotaDebitoTerceros/GetByLotePago`
- Body:

```json
{
  "codigoLotePago": 100,
  "codigoPago": 0
}
```

### Generar PDF

- Metodo: `POST`
- Ruta: `/api/ReporteNotaDebitoTerceros/pdf`
- Body:

```json
{
  "codigoLotePago": 100,
  "codigoPago": 0
}
```

La respuesta es `application/pdf` con `Content-Disposition: inline`.

## Notas de integracion

- El frontend debe consumir `/ReporteNotaDebitoTerceros/pdf` mediante `ossmmasofApiVertical`.
- La respuesta debe convertirse a `Blob` y mostrarse en `ReportViewAsync`.
- No descargar automaticamente ni abrir en una pestana externa.
- Si `codigoPago` es mayor que cero, el reporte se filtra por ese pago; con `0` genera todos los pagos del lote.
