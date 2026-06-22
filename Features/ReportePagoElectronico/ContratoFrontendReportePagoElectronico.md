# Contrato Frontend - ReportePagoElectronico

## Objetivo

Migrar los reportes `electronic-payment` y `electronic-payment-third-parties` del ReportServer al backend vertical slice y mostrarlos en preview desde `/apps/adm/pagos/lotes/`.

## Endpoints

### Obtener datos del lote

- Metodo: `POST`
- Ruta: `/api/ReportePagoElectronico/GetByLote`
- Body:

```json
{
  "codigoLotePago": 100
}
```

### Generar PDF pago electronico

- Metodo: `POST`
- Ruta: `/api/ReportePagoElectronico/pdf`
- Body:

```json
{
  "codigoLotePago": 100,
  "codigoPago": 0
}
```

### Generar PDF pago electronico terceros

- Metodo: `POST`
- Ruta: `/api/ReportePagoElectronico/terceros/pdf`
- Body:

```json
{
  "codigoLotePago": 100,
  "codigoPago": 0
}
```

La respuesta de ambos endpoints PDF es `application/pdf` con `Content-Disposition: inline`.

## Notas de integracion

- El frontend debe consumir estos endpoints mediante `ossmmasofApiVertical`.
- La respuesta debe convertirse a `Blob` y mostrarse en `ReportViewAsync`.
- No descargar automaticamente ni abrir en una pestana externa.
- Para mantener compatibilidad con ReportServer, el PDF se genera por `codigoLotePago`; `codigoPago` se acepta en el contrato pero no filtra el resultado.
