# Contrato Frontend - ReporteTimbreFiscal

## Objetivo

Migrar el reporte `tax-stamp-voucher` del ReportServer al backend vertical slice y mostrar el PDF en preview desde `/apps/adm/ordenPago/`.

## Endpoints

### Obtener datos

- Metodo: `POST`
- Ruta: `/api/ReporteTimbreFiscal/GetByCodigo`
- Body:

```json
{
  "codigoOrdenPago": 149
}
```

### Generar PDF

- Metodo: `POST`
- Ruta: `/api/ReporteTimbreFiscal/pdf`
- Body:

```json
{
  "codigoOrdenPago": 149
}
```

La respuesta es `application/pdf` con `Content-Disposition: inline`.

## Respuesta de datos

```json
{
  "data": {
    "header": {
      "codigoOrdenPago": 149,
      "numeroOrdenPago": "OP-000149",
      "nombreAgenteRetencion": "CONCEJO MUNICIPAL DEL MUNICIPIO CHACAO",
      "rifAgenteRetencion": "G200000000",
      "nombreContribuyente": "PROVEEDOR",
      "rifContribuyente": "J000000000",
      "motivo": "Concepto de la orden de pago",
      "status": "AP",
      "baseImponible": 1000.00,
      "montoRetencion": 1.00
    },
    "documentos": [
      {
        "numeroControlFactura": "00-00000001",
        "numeroFactura": "FAC-001",
        "montoDocumento": 1160.00,
        "montoExento": 0.00,
        "montoIva": 160.00
      }
    ]
  },
  "isValid": true,
  "message": "Success"
}
```

## Notas de integracion

- El frontend debe consumir `/ReporteTimbreFiscal/pdf` mediante `ossmmasofApiVertical`.
- La respuesta debe convertirse a `Blob` y mostrarse en `ReportViewAsync`.
- No descargar automaticamente ni abrir en una pestana externa.
- El payload se toma de la orden seleccionada: `{ codigoOrdenPago }`.
