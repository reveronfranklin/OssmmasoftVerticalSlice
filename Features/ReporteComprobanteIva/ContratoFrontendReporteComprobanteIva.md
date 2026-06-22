# Contrato Frontend - ReporteComprobanteIva

Fecha: 2026-06-22.

## Endpoint PDF

```http
POST /api/ReporteComprobanteIva/pdf
```

Genera el comprobante de retencion IVA directamente desde `OssmmasoftVerticalSlice`.

### Request

```json
{
  "codigoOrdenPago": 123
}
```

### Response Exitoso

- HTTP `200`
- `Content-Type: application/pdf`
- `Content-Disposition: inline; filename="comprobante-iva-123.pdf"`
- Body: bytes del PDF.

### Frontend

Pantalla:

- `NextOssmasoft/src/adm/ordenesPago/forms/viewer/FormViewerPdf.tsx`

La opcion `UrlServices.GETREPORTBYCOMPROBANTE` debe mostrarse en el preview existente, consumiendo:

```ts
ossmmasofApiVertical.post('/ReporteComprobanteIva/pdf', { codigoOrdenPago }, { responseType: 'blob' })
```

ISLR y Timbre Fiscal permanecen temporalmente por `AdmOrdenPago/Report`.
