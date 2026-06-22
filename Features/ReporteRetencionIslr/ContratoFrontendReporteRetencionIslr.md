# Contrato Frontend - ReporteRetencionIslr

Fecha: 2026-06-22.

## Endpoint PDF

```http
POST /api/ReporteRetencionIslr/pdf
```

Genera el comprobante de retencion ISLR directamente desde `OssmmasoftVerticalSlice`.

### Request

```json
{
  "codigoOrdenPago": 123
}
```

### Response Exitoso

- HTTP `200`
- `Content-Type: application/pdf`
- `Content-Disposition: inline; filename="retencion-islr-123.pdf"`
- Body: bytes del PDF.

## Frontend

Pantalla:

- `NextOssmasoft/src/adm/ordenesPago/forms/viewer/FormViewerPdf.tsx`

La opcion `UrlServices.GETREPORTBYRETENCIONES` debe mostrarse en el preview existente, consumiendo:

```ts
ossmmasofApiVertical.post('/ReporteRetencionIslr/pdf', { codigoOrdenPago }, { responseType: 'blob' })
```

Timbre Fiscal permanece temporalmente por `AdmOrdenPago/Report`.
