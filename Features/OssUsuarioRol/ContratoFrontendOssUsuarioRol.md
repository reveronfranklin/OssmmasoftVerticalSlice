# Contrato Frontend - OSS Usuario Rol

## Base

```http
Base URL: http://ossmmasoft.com.ve:5142
Content-Type: application/json
```

Todos los endpoints usan `POST` y devuelven el wrapper `ResultDto`.

## Nota Oracle 10g

La tabla original tiene `JSON_MENU LONG`. Para este CRUD se recomienda migrar `JSON_MENU` a `CLOB` usando:

[MIGRATE_OSS_USUARIO_ROL_CLOB.sql](/Users/freveron/Developer/Projects/MM/OssmmasoftVerticalSlice/Features/OssUsuarioRol/MIGRATE_OSS_USUARIO_ROL_CLOB.sql)

Oracle 10g no valida JSON nativamente. El API valida que `jsonMenu` sea un objeto o arreglo JSON antes de guardar.

## Modelo

```json
{
  "codigoUsuarioRol": 1,
  "usuario": "admin",
  "codigoUsuario": 10,
  "descripcion": "Administrador",
  "jsonMenu": [
    {
      "title": "Administracion",
      "icon": "mdi:file-document-outline",
      "children": [
        {
          "title": "Solicitud de compromiso",
          "path": "/apps/adm/solicitudCompromiso"
        }
      ]
    }
  ]
}
```

| Campo | Tipo | Descripcion |
| --- | --- | --- |
| `codigoUsuarioRol` | number | Identificador del registro. Lo genera backend en create. |
| `usuario` | string | Usuario/login asociado al menu. |
| `codigoUsuario` | number | Codigo numerico del usuario. Debe ser mayor a cero. |
| `descripcion` | string/null | Descripcion del rol o perfil. |
| `jsonMenu` | object/array | Menu que consume frontend. Debe ser JSON valido. |

## Crear

```http
POST /api/OssUsuarioRol/create
```

### Request

```json
{
  "usuario": "admin",
  "codigoUsuario": 10,
  "descripcion": "Administrador",
  "jsonMenu": [
    {
      "title": "Administracion",
      "icon": "mdi:file-document-outline",
      "children": [
        {
          "title": "Solicitud de compromiso",
          "path": "/apps/adm/solicitudCompromiso"
        }
      ]
    }
  ]
}
```

### Response

`data` contiene el `codigoUsuarioRol` generado.

```json
{
  "data": 1,
  "isValid": true,
  "message": "suscces"
}
```

## Actualizar

```http
POST /api/OssUsuarioRol/update
```

### Request

```json
{
  "codigoUsuarioRol": 1,
  "usuario": "admin",
  "codigoUsuario": 10,
  "descripcion": "Administrador",
  "jsonMenu": []
}
```

## Eliminar

```http
POST /api/OssUsuarioRol/delete
```

```json
{
  "codigoUsuarioRol": 1
}
```

## Consultar Por ID

```http
POST /api/OssUsuarioRol/getById
```

```json
{
  "codigoUsuarioRol": 1
}
```

## Listar Paginado

```http
POST /api/OssUsuarioRol/GetAll
```

```json
{
  "pageSize": 10,
  "pageNumber": 1,
  "searchText": ""
}
```

`searchText` filtra por `usuario` y `descripcion`.

## Consultar Por Usuario

```http
POST /api/OssUsuarioRol/getByUsuario
```

```json
{
  "usuario": "admin"
}
```

Respuesta:

```json
{
  "data": [
    {
      "codigoUsuarioRol": 2,
      "usuario": "admin",
      "codigoUsuario": 1,
      "descripcion": "Administrador",
      "jsonMenu": []
    }
  ],
  "isValid": true,
  "message": "suscces"
}
```

## Errores Comunes

```json
{
  "data": null,
  "isValid": false,
  "message": "El campo jsonMenu debe ser un objeto o arreglo JSON."
}
```

```json
{
  "data": null,
  "isValid": false,
  "message": "USUARIO ya existe en SIS.OSS_USUARIO_ROL"
}
```
