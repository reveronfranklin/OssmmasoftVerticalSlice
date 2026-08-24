# Contrato frontend - Contribuyentes de Catastro

Ambos endpoints consultan el esquema RM y obtienen la empresa desde `settings:EmpresaConfig`.

## Listado

- `POST /api/CatastroContribuyentes/GetAll`
- Request: `{ "pageSize": 10, "pageNumber": 1, "searchText": "texto" }`
- Busca por codigo, identificacion, nombre o apellido/acronimo.
- Devuelve `ResultDto<CatastroContribuyente[]>` con paginacion.

## Detalle

- `POST /api/CatastroContribuyentes/getById`
- Request: `{ "codigoContribuyente": 123 }`
- Devuelve maestro `contribuyente` y colecciones `direcciones` y `comunicaciones`.
- Las colecciones vacias se devuelven como `[]`.

Este contrato es de solo lectura. No se habilitan altas o modificaciones hasta validar triggers, secuencias, cifrado de ubicaciones y reglas de identificacion del Form original.
