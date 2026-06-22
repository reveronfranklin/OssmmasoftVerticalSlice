# Contrato Frontend - ReporteOrdenPago

Fecha: 2026-06-22.

## Endpoint PDF

```http
POST /api/ReporteOrdenPago/pdf
```

Genera el PDF de Orden de Pago directamente desde `OssmmasoftVerticalSlice`.

### Request

```json
{
  "codigoOrdenPago": 123
}
```

| Campo | Tipo | Requerido | Descripcion |
| --- | --- | --- | --- |
| `codigoOrdenPago` | number | Si | Codigo de la orden de pago seleccionada. Debe ser mayor que cero. |

### Response Exitoso

- HTTP `200`
- `Content-Type: application/pdf`
- `Content-Disposition: inline; filename="orden-pago-123.pdf"`
- Body: bytes del PDF.

### Response de Error

Cuando la orden no existe, el parametro es invalido o falla la consulta de datos:

```json
{
  "data": null,
  "isValid": false,
  "message": "No se encontro la orden de pago solicitada.",
  "cantidadRegistros": 0
}
```

## Endpoint de Datos

```http
POST /api/ReporteOrdenPago/GetByCodigo
```

Devuelve la informacion usada para construir el PDF. Es util para diagnostico y pruebas.

### Response de Datos

```json
{
  "data": {
    "header": {
      "codigoOrdenPago": 123,
      "tituloReporte": "ORDEN DE PAGO",
      "tipoOrdenPago": "SERVICIO",
      "numeroOrdenPago": "OP-000001",
      "fechaOrdenPago": "2026-06-22T00:00:00",
      "numeroCompromiso": "CP-000001",
      "fechaCompromiso": "2026-06-20T00:00:00",
      "nombreProveedor": "PROVEEDOR",
      "cedulaProveedor": "12345678",
      "rifProveedor": "J-00000000-0",
      "nombreBeneficiario": "NOMBRE",
      "apellidoBeneficiario": "APELLIDO",
      "cedulaBeneficiario": "12345678",
      "fechaPlazoDesde": "2026-06-01T00:00:00",
      "fechaPlazoHasta": "2026-06-30T00:00:00",
      "montoLetras": "CIEN BOLIVARES",
      "formaPago": "UNICO",
      "cantidadPago": 1,
      "motivo": "Pago de servicio",
      "status": "AP"
    },
    "fondos": [
      {
        "ano": 2026,
        "descripcionFinanciado": "ORDINARIO",
        "codigoIcpConcat": "01-01-01-00-00",
        "codigoPucConcat": "4.01.01.01",
        "denominacionPuc": "SERVICIOS",
        "monto": 100.00
      }
    ],
    "retenciones": [
      {
        "descripcion": "ISLR",
        "porRetencion": 3,
        "montoRetencion": 3.00
      }
    ]
  },
  "isValid": true,
  "message": "Success",
  "cantidadRegistros": 3
}
```

## Integracion Frontend Esperada

Pantalla actual:

- `NextOssmasoft/src/adm/ordenesPago/forms/viewer/FormViewerPdf.tsx`

Cambio esperado:

- Para `UrlServices.GETREPORTBYORDENPAGO`, llamar directo a `ossmmasofApiVertical.post('/ReporteOrdenPago/pdf', { codigoOrdenPago }, { responseType: 'blob' })`.
- Mantener `HandleReportApiTo` para:
  - `GETREPORTBYRETENCIONES`
  - `GETREPORTBYCOMPROBANTE`
  - `TIMBREFISCAL`

Esto evita retirar el gateway completo antes de migrar los comprobantes asociados.
