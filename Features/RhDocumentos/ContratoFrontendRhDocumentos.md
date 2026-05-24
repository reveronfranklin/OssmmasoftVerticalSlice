# Contrato Frontend - RH Documentos

## Base

```http
Base URL: http://ossmmasoft.com.ve:5142
Content-Type: application/json
```

Todos los endpoints usan `POST` y devuelven el wrapper `ResultDto`.

El frontend no debe enviar `codigoEmpresa`. El backend toma la empresa desde `settings:EmpresaConfig` en `appsettings.json` o `appsettings.Development.json` y la aplica en todas las operaciones.

## Wrapper de respuesta

```json
{
  "data": null,
  "isValid": true,
  "linkData": null,
  "linkDataArlternative": null,
  "message": "suscces",
  "page": 1,
  "totalPage": 1,
  "cantidadRegistros": 0,
  "total1": 0,
  "total2": 0,
  "total3": 0,
  "total4": 0
}
```

| Campo | Tipo | Descripcion |
| --- | --- | --- |
| `data` | object/array/number/null | Resultado de la operacion. |
| `isValid` | boolean | `true` cuando el stored procedure retorna exito. |
| `message` | string | En exito retorna `suscces`. En error retorna el mensaje de validacion o error de base de datos. |
| `page` | number | Pagina actual. Solo aplica en `GetAll`. |
| `totalPage` | number | Total de paginas. Solo aplica en `GetAll`. |
| `cantidadRegistros` | number | Total de registros encontrados. |
| `total1..total4` | number | Campos genericos del backend; no se usan en este CRUD. |

## Modelo Documento

```json
{
  "codigoPersona": 123,
  "codigoDocumento": 10,
  "tipoDocumentoId": 1,
  "tipoDocumento": "CEDULA",
  "numeroDocumento": "12345678",
  "fechaVencimiento": "2026-12-31T00:00:00",
  "tipoGradoId": null,
  "tipoGrado": "ACADEMICO",
  "gradoId": null,
  "grado": "LICENCIADO",
  "extra1": "",
  "extra2": "",
  "extra3": "",
  "usuarioIns": 1,
  "fechaIns": "2026-05-23T10:30:00",
  "usuarioUpd": null,
  "fechaUpd": null,
  "codigoEmpresa": 13,
  "persona": "NOMBRE APELLIDO"
}
```

| Campo | Tipo | Descripcion |
| --- | --- | --- |
| `codigoPersona` | number | Codigo de persona asociado al documento. |
| `codigoDocumento` | number | Identificador del documento. Lo genera backend en create. |
| `tipoDocumentoId` | number | FK contra `RH.RH_DESCRIPTIVAS.DESCRIPCION_ID`. |
| `tipoDocumento` | string | Descripcion del tipo de documento. |
| `numeroDocumento` | string | Numero del documento. |
| `fechaVencimiento` | string/null | Fecha ISO. Puede venir `null`. |
| `tipoGradoId` | number/null | FK contra `RH.RH_DESCRIPTIVAS.DESCRIPCION_ID`. Solo se valida si es mayor a `0`. |
| `tipoGrado` | string | Descripcion del tipo de grado. |
| `gradoId` | number/null | FK contra `RH.RH_DESCRIPTIVAS.DESCRIPCION_ID`. Solo se valida si es mayor a `0`. |
| `grado` | string | Descripcion del grado. |
| `extra1`, `extra2`, `extra3` | string | Campos auxiliares. |
| `usuarioIns` | number | Usuario que creo el registro. |
| `fechaIns` | string/null | Fecha de creacion. |
| `usuarioUpd` | number/null | Usuario que actualizo el registro. |
| `fechaUpd` | string/null | Fecha de actualizacion. |
| `codigoEmpresa` | number | Empresa tomada por backend desde `settings:EmpresaConfig`. |
| `persona` | string | Nombre y apellido concatenado. |

## Validaciones de negocio

El backend valida en los stored procedures:

- `codigoPersona` debe existir en `RH.RH_PERSONAS.CODIGO_PERSONA` para la empresa configurada.
- `tipoDocumentoId` debe existir en `RH.RH_DESCRIPTIVAS.DESCRIPCION_ID`.
- `tipoGradoId` debe existir en `RH.RH_DESCRIPTIVAS.DESCRIPCION_ID` solo cuando es mayor a `0`.
- `gradoId` debe existir en `RH.RH_DESCRIPTIVAS.DESCRIPCION_ID` solo cuando es mayor a `0`.
- `codigoDocumento` se genera en backend como el ultimo numero mas 1.
- `create`, `update`, `delete`, `getById`, `GetAll` y `getByPersona` limitan la operacion a `settings:EmpresaConfig`.

Cuando una validacion falla, el endpoint puede responder HTTP 200 con `isValid = false`.

## Crear Documento

```http
POST /api/RhDocumentos/create
```

### Request

```json
{
  "codigoPersona": 123,
  "tipoDocumentoId": 1,
  "numeroDocumento": "12345678",
  "fechaVencimiento": "2026-12-31",
  "tipoGradoId": null,
  "gradoId": null,
  "usuarioIns": 1,
  "extra1": null,
  "extra2": null,
  "extra3": null
}
```

### Response exitoso

`data` contiene el `codigoDocumento` generado.

```json
{
  "data": 10,
  "isValid": true,
  "message": "suscces",
  "page": 0,
  "totalPage": 0,
  "cantidadRegistros": 0,
  "total1": 0,
  "total2": 0,
  "total3": 0,
  "total4": 0
}
```

## Actualizar Documento

```http
POST /api/RhDocumentos/update
```

### Request

```json
{
  "codigoDocumento": 10,
  "codigoPersona": 123,
  "tipoDocumentoId": 1,
  "numeroDocumento": "12345678",
  "fechaVencimiento": "2027-12-31",
  "tipoGradoId": 0,
  "gradoId": 0,
  "usuarioUpd": 1,
  "extra1": null,
  "extra2": null,
  "extra3": null
}
```

### Response exitoso

```json
{
  "data": "Registro actualizado correctamente",
  "isValid": true,
  "message": "suscces"
}
```

## Eliminar Documento

```http
POST /api/RhDocumentos/delete
```

### Request

```json
{
  "codigoDocumento": 10
}
```

### Response exitoso

```json
{
  "data": "Registro eliminado correctamente",
  "isValid": true,
  "message": "suscces"
}
```

## Consultar Por ID

```http
POST /api/RhDocumentos/getById
```

### Request

```json
{
  "codigoDocumento": 10
}
```

### Response exitoso

```json
{
  "data": {
    "codigoPersona": 123,
    "codigoDocumento": 10,
    "tipoDocumentoId": 1,
    "tipoDocumento": "CEDULA",
    "numeroDocumento": "12345678",
    "fechaVencimiento": "2026-12-31T00:00:00",
    "tipoGradoId": 2,
    "tipoGrado": "ACADEMICO",
    "gradoId": 3,
    "grado": "LICENCIADO",
    "extra1": "",
    "extra2": "",
    "extra3": "",
    "usuarioIns": 1,
    "fechaIns": "2026-05-23T10:30:00",
    "usuarioUpd": null,
    "fechaUpd": null,
    "codigoEmpresa": 13,
    "persona": "NOMBRE APELLIDO"
  },
  "isValid": true,
  "message": "suscces"
}
```

## Listar Paginado

```http
POST /api/RhDocumentos/GetAll
```

### Request

```json
{
  "pageSize": 10,
  "pageNumber": 1,
  "searchText": ""
}
```

| Campo | Tipo | Requerido | Descripcion |
| --- | --- | --- | --- |
| `pageSize` | number | Si | Cantidad de registros por pagina. Si llega `0` o menor, backend usa `10`. |
| `pageNumber` | number | Si | Pagina solicitada. Si llega `0` o menor, backend usa `1`. |
| `searchText` | string | Si | Texto para filtrar campos texto. Enviar `""` para listar todo. |

### Campos filtrados por `searchText`

- `numeroDocumento`
- `extra1`
- `extra2`
- `extra3`
- descripcion de `tipoDocumento`
- descripcion de `tipoGrado`
- descripcion de `grado`
- nombre de persona
- apellido de persona

### Response exitoso

```json
{
  "data": [
    {
      "codigoPersona": 123,
      "codigoDocumento": 10,
      "tipoDocumentoId": 1,
      "tipoDocumento": "CEDULA",
      "numeroDocumento": "12345678",
      "fechaVencimiento": "2026-12-31T00:00:00",
      "tipoGradoId": 2,
      "tipoGrado": "ACADEMICO",
      "gradoId": 3,
      "grado": "LICENCIADO",
      "extra1": "",
      "extra2": "",
      "extra3": "",
      "usuarioIns": 1,
      "fechaIns": "2026-05-23T10:30:00",
      "usuarioUpd": null,
      "fechaUpd": null,
      "codigoEmpresa": 13,
      "persona": "NOMBRE APELLIDO"
    }
  ],
  "isValid": true,
  "message": "suscces",
  "page": 1,
  "totalPage": 3,
  "cantidadRegistros": 25
}
```

## Listar Por Persona

```http
POST /api/RhDocumentos/getByPersona
```

### Request

```json
{
  "codigoPersona": 123
}
```

### Response exitoso

Devuelve la misma estructura de item que `GetAll`, pero sin paginacion y filtrado por `codigoPersona`.

```json
{
  "data": [
    {
      "codigoPersona": 123,
      "codigoDocumento": 10,
      "tipoDocumentoId": 1,
      "tipoDocumento": "CEDULA",
      "numeroDocumento": "12345678",
      "fechaVencimiento": "2026-12-31T00:00:00",
      "tipoGradoId": 2,
      "tipoGrado": "ACADEMICO",
      "gradoId": 3,
      "grado": "LICENCIADO",
      "extra1": "",
      "extra2": "",
      "extra3": "",
      "usuarioIns": 1,
      "fechaIns": "2026-05-23T10:30:00",
      "usuarioUpd": null,
      "fechaUpd": null,
      "codigoEmpresa": 13,
      "persona": "NOMBRE APELLIDO"
    }
  ],
  "isValid": true,
  "message": "suscces",
  "page": 1,
  "totalPage": 1,
  "cantidadRegistros": 1
}
```

## Errores comunes

### Persona inexistente

```json
{
  "data": null,
  "isValid": false,
  "message": "CODIGO_PERSONA no existe en RH.RH_PERSONAS"
}
```

### Descriptiva inexistente

```json
{
  "data": null,
  "isValid": false,
  "message": "TIPO_DOCUMENTO_ID no existe en RH.RH_DESCRIPTIVAS"
}
```

### Documento inexistente

```json
{
  "data": null,
  "isValid": false,
  "message": "CODIGO_DOCUMENTO no existe en RH.RH_DOCUMENTOS"
}
```
