# Contrato frontend - Parametros de Catastro

- `POST /api/CatastroParametros/GetAll`
- Request: `{ "ano": 2026 }`; `ano` puede ser `null` para consultar todas las tablas.
- Devuelve colecciones `ajustes`, `tablaImpositiva` y `zonificaciones`.

El endpoint es de solo lectura. `CAT_AJUSTES` y `CAT_TABLA_IMP` no poseen columna de empresa en el esquema legado; se leen como configuracion global. Zonificaciones si se filtra por `settings:EmpresaConfig`.
