# Contrato Frontend - ReporteControlPerceptivo

Fecha: 2026-07-21.

Migracion del reporte legacy Oracle Reports `ADM_CONTROL_PERCEPTIVO.rdf` (ver
`Requerimientos/15 - Reporte Control Perceptivo/`). El SQL reconstruido esta
pendiente de validacion contra datos reales (Fase 1 del PLAN.md de ese
requerimiento).

## Endpoint PDF

```http
POST /api/ReporteControlPerceptivo/pdf
```

Genera el PDF de Control Perceptivo directamente desde `OssmmasoftVerticalSlice`.

### Request

```json
{
  "codigoCompromiso": 123
}
```

| Campo | Tipo | Requerido | Descripcion |
| --- | --- | --- | --- |
| `codigoCompromiso` | number | Si | Codigo de Compromiso Presupuestario o de Contrato. El backend detecta automaticamente la fuente (compromiso via `ADM_COMPROMISOS`/`PRE_COMPROMISOS` o contrato via `ADM_CONTRATOS`); el frontend no necesita indicar el tipo. |

### Response Exitoso

- HTTP `200`
- `Content-Type: application/pdf`
- `Content-Disposition: inline; filename="control-perceptivo-123.pdf"`
- Body: bytes del PDF.

### Response de Error

Cuando el compromiso/contrato no existe, el parametro es invalido o falla la consulta de datos:

```json
{
  "data": null,
  "isValid": false,
  "message": "No se encontro el compromiso o contrato solicitado.",
  "cantidadRegistros": 0
}
```

## Endpoint de Datos

```http
POST /api/ReporteControlPerceptivo/GetByCodigo
```

Devuelve la informacion usada para construir el PDF. Util para diagnostico y pruebas.

### Response de Datos

```json
{
  "data": {
    "header": {
      "codigoCompromiso": 123,
      "numeroCompromiso": "CMC-CMP-00419",
      "fechaCompromiso": "2026-07-21T00:00:00",
      "proveedor": "FONDO ESPECIAL DE JUBILACIONES Y PENSIONES",
      "solicitante": "DIRECCION DE ADMINISTRACION",
      "direccionEmpresa": "Edf. Atrium, Piso 2. Av. Venezuela Con Calle Sorocaima. El Rosal. Edo. Miranda.",
      "nombreEmpresa": "Concejo Municipal Del Municipio Chacao",
      "fechaEmisionTexto": "a los Veintiuno dias del mes de Julio de 2026"
    },
    "detalle": [
      {
        "cantidad": 1,
        "udm": "NOMINA",
        "descripcionArticulo": "RETENCIONES FJP, NOMINA ALTO NIVEL, JULIO 2026",
        "precioUnitario": 1689.38,
        "precio": 1689.38,
        "porImpuesto": 0,
        "montoImpuesto": 0
      }
    ],
    "subTotal": 3828.26,
    "montoImpuesto": 0,
    "montoTotal": 3828.26,
    "montoLetras": "TRES MIL OCHOCIENTOS VEINTIOCHO CON 26 CENTIMOS"
  },
  "isValid": true,
  "message": "Success",
  "cantidadRegistros": 4
}
```

## Validaciones

- `codigoCompromiso` debe ser mayor que cero.
- Si el compromiso/contrato no existe en ninguna de las 3 fuentes soportadas
  (`ADM_COMPROMISOS`, `PRE_COMPROMISOS`, `ADM_CONTRATOS`), la respuesta es
  invalida con mensaje "No se encontro el compromiso o contrato solicitado.".
- Un compromiso sin lineas de detalle no es un error: el PDF se genera con la
  tabla vacia ("Sin lineas registradas") y totales en cero.

## Integracion Frontend Esperada

Pantalla actual del compromiso (reporte legacy disparado via
`/ReporteCompromisoPresupuestario/ReportData` + `/Files/GetPdfFiles/{nombre}`):

- `NextOssmasoft/src/presupuesto/compromiso/components/viewerPdf/viewer.tsx`

Cambio esperado: agregar "Control Perceptivo" como una segunda opcion de
reporte seleccionable en ese mismo visor (patron de
`src/adm/ordenesPago/forms/viewer/FormViewerPdf.tsx` +
`src/adm/ordenesPago/config/reportOptions.tsx`), llamando directo a
`ossmmasofApiVertical.post('/ReporteControlPerceptivo/pdf', { codigoCompromiso }, { responseType: 'blob' })`
en vez del flujo legacy de nombre de archivo. El reporte de Compromiso
Presupuestario existente no se modifica ni se retira en este cambio.
