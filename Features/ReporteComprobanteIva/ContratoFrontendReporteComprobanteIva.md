# Contrato Frontend - ReporteComprobanteIva

Fecha: 2026-07-28.

## Endpoint PDF

```http
POST /api/ReporteComprobanteIva/pdf
```

Genera el comprobante de retencion IVA directamente desde `OssmmasoftVerticalSlice`.

### Request

```json
{
  "codigoOrdenPago": 123,
  "usuario": "jperez"
}
```

`usuario` corresponde a `user.username` del usuario conectado, obtenido
mediante `useAuth()`. Si llega vacio o nulo, el PDF muestra
`Usuario: No identificado`.

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
ossmmasofApiVertical.post(
  '/ReporteComprobanteIva/pdf',
  { codigoOrdenPago, usuario: user?.username ?? '' },
  { responseType: 'blob' }
)
```

El PDF muestra el usuario enviado y la fecha/hora de impresion en el lado
izquierdo del footer de todas las paginas.
