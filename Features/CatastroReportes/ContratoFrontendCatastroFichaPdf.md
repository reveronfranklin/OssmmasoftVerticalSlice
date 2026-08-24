# Contrato frontend - Ficha catastral PDF

- `POST /api/CatastroReportes/fichaPdf`
- Request: `{ "codigoInmueble": 123, "codigoContribuyente": 456, "usuario": "usuario" }`
- Respuesta exitosa: `application/pdf`.
- Errores: JSON con propiedad `message` y HTTP 400.

La ficha se abre como blob en el visor integrado. Esta primera version es preliminar y no contiene firma; no sustituye aun una cedula catastral con validez legal.
