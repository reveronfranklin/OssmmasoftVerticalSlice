# Contrato Frontend - SIS Seguridad

Fecha: 2026-06-10

## Objetivo

Administrar seguridad normalizada de menus, roles y permisos, manteniendo `SIS.OSS_USUARIO_ROL.JSON_MENU` como cache compatible con el menu actual.

Todos los endpoints requieren autenticacion JWT y el permiso:

```text
soporte.usuarios.configurar
```

## Instalacion BD

Ejecutar:

```sql
@Features/SisSeguridad/Sql/00_INSTALL_SIS_SEGURIDAD.sql
```

El instalador crea:

- `SIS.OSS_MOD`
- `SIS.OSS_MENU`
- `SIS.OSS_PERM`
- `SIS.OSS_MENU_PERM`
- `SIS.OSS_ROL`
- `SIS.OSS_ROL_MENU`
- `SIS.OSS_ROL_PERM`
- `SIS.OSS_USR_ROL`
- `SIS.OSS_USR_PERM`
- `SIS.OSS_SEG_AUD`

Tambien carga el seed inicial del modulo Soporte.
Tambien carga menus/roles base de `CNT`, `RH`, `PRE` y `ADM` desde la estructura legacy conocida.

## Respaldo y Rollback

Antes de migrar ejecutar:

```sql
@Features/SisSeguridad/Sql/04_BACKUP_SIS_SEG.sql
```

El script crea tablas con sufijo timestamp:

- `B_OSS_UROL_<sufijo>`: respaldo de `OSS_USUARIO_ROL`.
- `B_OSS_NUROL_<sufijo>`: respaldo de `OSS_USR_ROL`.
- `B_OSS_NUPERM_<sufijo>`: respaldo de `OSS_USR_PERM`.

Para rollback, editar `05_ROLLBACK_SIS_SEG.sql`, reemplazar `__SUFIJO__` por el timestamp generado y ejecutar manualmente.

## Auditoria

Las operaciones que modifican seguridad registran eventos en `SIS.OSS_SEG_AUD`:

- `SAVE_USR_SEG`
- `REGEN_CACHE`
- `MIGRAR_USR`
- `MIGRAR_MASIVA`
- `SAVE_ROL`
- `SAVE_MENU`
- `SAVE_ROL_PERM`
- `SAVE_ROL_MENU`

Campos principales:

```text
CODIGO_USUARIO
CODIGO_EMPRESA
ACCION
DETALLE
USUARIO_ACCION
FECHA_ACCION
```

## `POST /api/SisSeguridad/getCatalogos`

Lista modulos, menus, permisos y roles activos/inactivos.

### Request

```json
{}
```

### Response

```json
{
  "data": {
    "modulos": [
      {
        "codigoMod": 2,
        "codigo": "SOP",
        "nombre": "Soporte",
        "icono": "mdi:lifebuoy",
        "orden": 20,
        "activo": true
      }
    ],
    "menus": [
      {
        "codigoMenu": 1110,
        "codigoMod": 2,
        "codigoPadre": 1100,
        "titulo": "Tickets",
        "path": "/apps/soporte/tickets",
        "icono": "",
        "orden": 10,
        "activo": true
      }
    ],
    "permisos": [
      {
        "codigoPerm": 100,
        "codigoMod": 2,
        "clave": "soporte.tickets.crear",
        "nombre": "Crear tickets",
        "descripcion": "Crear solicitudes de soporte.",
        "activo": true
      }
    ],
    "roles": [
      {
        "codigoRol": 102,
        "codigoMod": 2,
        "clave": "SOPORTE_ADMIN",
        "nombre": "Administrador de Soporte",
        "descripcion": "Administrador completo de soporte.",
        "activo": true
      }
    ],
    "rolPermisos": [
      {
        "codigoRol": 102,
        "codigoPerm": 100
      }
    ],
    "rolMenus": [
      {
        "codigoRol": 102,
        "codigoMenu": 1110
      }
    ]
  },
  "isValid": true,
  "message": "Success"
}
```

## `POST /api/SisSeguridad/getEstadoInstalacion`

Valida si las tablas requeridas de seguridad normalizada existen en el esquema `SIS`.

### Request

```json
{}
```

### Response

```json
{
  "data": {
    "instalacionCompleta": false,
    "tablasFaltantes": ["OSS_USR_ROL", "OSS_USR_PERM"],
    "mensaje": "Faltan tablas requeridas en SIS: OSS_USR_PERM, OSS_USR_ROL."
  },
  "isValid": true,
  "message": "Success"
}
```

## `POST /api/SisSeguridad/saveRol`

Crear o actualizar un rol.

```json
{
  "codigoRol": 0,
  "codigoMod": 2,
  "clave": "SOPORTE_ADMIN",
  "nombre": "Administrador de Soporte",
  "descripcion": "Administrador completo de soporte.",
  "activo": true,
  "usuarioUpd": 0
}
```

## `POST /api/SisSeguridad/saveMenu`

Crear o actualizar una opcion de menu normalizada.

```json
{
  "codigoMenu": 0,
  "codigoMod": 2,
  "codigoPadre": null,
  "titulo": "Tickets",
  "path": "/apps/soporte/tickets",
  "icono": "mdi:ticket-outline",
  "orden": 10,
  "activo": true,
  "usuarioUpd": 0
}
```

## `POST /api/SisSeguridad/saveRolPermisos`

Reemplaza los permisos asociados al rol.

```json
{
  "codigoRol": 102,
  "permisos": [100, 101, 102],
  "usuarioUpd": 0
}
```

## `POST /api/SisSeguridad/saveRolMenus`

Reemplaza los menus asociados al rol.

```json
{
  "codigoRol": 102,
  "menus": [1100, 1110],
  "usuarioUpd": 0
}
```

## `POST /api/SisSeguridad/getUsuario`

Obtiene roles, permisos efectivos y `JSON_MENU` legacy/cache de un usuario.

### Request

```json
{
  "codigoUsuario": 10
}
```

### Response

```json
{
  "data": {
    "codigoUsuario": 10,
    "usuario": "ADMIN",
    "login": "admin",
    "isSuperuser": true,
    "roles": [],
    "permisos": [],
    "excepciones": [
      {
        "codigoPerm": 107,
        "codigoMod": 2,
        "clave": "soporte.configuracion.ver",
        "nombre": "Ver configuracion",
        "descripcion": "Acceder a configuracion de soporte.",
        "activo": true,
        "tipo": "DENY"
      }
    ],
    "jsonMenu": []
  },
  "isValid": true,
  "message": "Success"
}
```

## `POST /api/SisSeguridad/saveUsuarioRoles`

Guarda roles y excepciones de permisos de un usuario. Al finalizar regenera `SIS.OSS_USUARIO_ROL.JSON_MENU`.

### Request

```json
{
  "codigoUsuario": 10,
  "roles": [102],
  "permisos": [
    {
      "codigoPerm": 107,
      "tipo": "DENY",
      "activo": true
    }
  ],
  "usuarioUpd": 1
}
```

### Reglas

- `roles` reemplaza la asignacion activa actual del usuario.
- `permisos` reemplaza las excepciones activas actuales.
- `tipo` acepta `ALLOW` o `DENY`.
- Si `usuarioUpd` es `0`, el backend usa el usuario autenticado.
- El backend actualiza la cache en `SIS.OSS_USUARIO_ROL`.

### Response

```json
{
  "data": {
    "codigoUsuario": 10,
    "modulosActualizados": ["SIS"],
    "jsonMenu": [
      {
        "title": "Sistema",
        "icon": "mdi:shield-account-outline",
        "children": []
      }
    ]
  },
  "isValid": true,
  "message": "Seguridad de usuario guardada y cache regenerada."
}
```

## `POST /api/SisSeguridad/regenerarCache`

Regenera `JSON_MENU` desde tablas normalizadas sin modificar roles/permisos.

### Request

```json
{
  "codigoUsuario": 10,
  "codigoModulo": null
}
```

### Response

```json
{
  "data": {
    "codigoUsuario": 10,
    "modulosActualizados": ["SIS"],
    "jsonMenu": []
  },
  "isValid": true,
  "message": "Cache de menu regenerada."
}
```

## `POST /api/SisSeguridad/clonarUsuario`

Clona la seguridad normalizada de un usuario origen hacia un usuario destino existente o hacia un usuario nuevo. Copia roles activos y excepciones activas de permisos, y al finalizar regenera `SIS.OSS_USUARIO_ROL.JSON_MENU` del destino.

No copia datos personales del origen: nombre, login, email, cedula, password ni flags operativos.

### Destino existente

```json
{
  "codigoUsuarioOrigen": 100,
  "codigoUsuarioDestino": 200,
  "sobrescribirAccesos": true
}
```

### Destino nuevo

```json
{
  "codigoUsuarioOrigen": 100,
  "usuarioDestino": {
    "usuario": "JUAN PEREZ",
    "login": "JPEREZ",
    "clave": "Temporal123",
    "cedula": 12345678,
    "email": "jperez@dominio.com",
    "recibeEmail": true
  },
  "sobrescribirAccesos": true
}
```

### Reglas

- `codigoUsuarioOrigen` es requerido.
- Debe enviarse `codigoUsuarioDestino` o `usuarioDestino`.
- Si se envia `codigoUsuarioDestino`, el destino debe existir.
- Si se envia `usuarioDestino`, `usuario`, `login` y `clave` son requeridos.
- Origen y destino no pueden ser el mismo usuario.
- `sobrescribirAccesos = true` reemplaza roles/excepciones actuales del destino.
- `sobrescribirAccesos = false` fusiona roles/excepciones del origen con las actuales del destino.
- El backend audita la operacion y regenera cache.

### Respuesta

```json
{
  "data": {
    "codigoUsuarioOrigen": 100,
    "codigoUsuarioDestino": 200,
    "usuarioDestinoCreado": false,
    "rolesCopiados": 3,
    "excepcionesCopiadas": 2,
    "jsonMenu": []
  },
  "isValid": true,
  "message": "Seguridad clonada y cache regenerada."
}
```

## `POST /api/SisSeguridad/getMigracionSugerida`

Lee `SIS.OSS_USUARIO_ROL.JSON_MENU` actual y propone roles normalizados sin modificar datos.

### Request

```json
{
  "codigoModulo": null
}
```

`codigoModulo` es opcional. Valores esperados: `SIS`, `CNT`, `RH`, `PRE`, `ADM`.

### Response

```json
{
  "data": [
    {
      "codigoUsuario": 10,
      "usuario": "ADMIN",
      "login": "admin",
      "descripcion": "CNT",
      "isSuperuser": false,
      "jsonValido": true,
      "permisos": [
        "contabilidad.comprobantes.ver"
      ],
      "rutas": [
        "/apps/cnt/comprobantes"
      ],
      "rolesSugeridos": [
        "CNT_USUARIO"
      ],
      "permisosExcepcionSugeridos": [
        "contabilidad.conciliacion.ocr"
      ]
    }
  ],
  "isValid": true,
  "message": "Success",
  "cantidadRegistros": 1
}
```

### Reglas de sugerencia

- Soporte con permisos administrativos: `SOPORTE_ADMIN`.
- Soporte con permisos de asignacion/atencion: `SOPORTE_AGENTE`.
- Soporte basico o rutas `/apps/soporte/`: `SOPORTE_USUARIO`.
- CNT administrativo: `CNT_ADMIN`.
- CNT operativo: `CNT_ANALISTA`.
- CNT basico o rutas `/apps/cnt/`: `CNT_USUARIO`.
- Rutas o descripcion `RH`: `RH_USUARIO`.
- Rutas o descripcion `PRE`: `PRE_USUARIO`.
- Rutas o descripcion `ADM`: `ADM_USUARIO`.
- Permisos legacy no cubiertos por el rol sugerido se devuelven en `permisosExcepcionSugeridos`.

## `POST /api/SisSeguridad/aplicarMigracionSugerida`

Aplica los roles sugeridos y permisos excepcionales `ALLOW` para un usuario a partir del `JSON_MENU` legacy y regenera cache.

Esta operacion no elimina roles existentes. Solo activa o inserta los roles sugeridos.

### Request

```json
{
  "codigoUsuario": 10,
  "codigoModulo": "CNT",
  "usuarioUpd": 1
}
```

`codigoModulo` es opcional. Si se envia, solo usa el registro legacy de ese modulo para sugerir roles.

### Response

```json
{
  "data": {
    "codigoUsuario": 10,
    "modulosActualizados": ["CNT"],
    "jsonMenu": [
      {
        "title": "Contabilidad",
        "icon": "mdi:calculator-variant-outline",
        "children": []
      }
    ]
  },
  "isValid": true,
  "message": "Migracion sugerida aplicada. Roles: CNT_USUARIO. Excepciones: 0."
}
```

## `POST /api/SisSeguridad/getResumenMigracion`

Devuelve conteos para comparar seguridad legacy contra seguridad normalizada.

### Request

```json
{
  "codigoModulo": null
}
```

### Response

```json
{
  "data": {
    "usuariosLegacy": 10,
    "usuariosNormalizados": 8,
    "registrosLegacy": 12,
    "rolesNormalizados": 9,
    "excepcionesNormalizadas": 2,
    "jsonInvalidos": 1,
    "pendientes": 1
  },
  "isValid": true,
  "message": "Success"
}
```

## `POST /api/SisSeguridad/aplicarMigracionMasiva`

Aplica la migracion sugerida para todos los usuarios filtrados. Requiere confirmacion explicita.

Esta operacion:

- Requiere `IS_SUPERUSER = 1` ademas de `soporte.usuarios.configurar`.
- Omite registros con `jsonValido = false`.
- Inserta o activa roles sugeridos.
- Inserta o activa permisos excepcionales como `ALLOW`.
- Regenera cache para cada usuario procesado.
- No elimina roles ni excepciones existentes.

### Request

```json
{
  "codigoModulo": null,
  "usuarioUpd": 1,
  "confirmar": true
}
```

Si `confirmar` no es `true`, el backend rechaza la operacion.

### Response

```json
{
  "data": {
    "usuariosProcesados": 10,
    "rolesAplicados": 12,
    "excepcionesAplicadas": 2,
    "mensajes": []
  },
  "isValid": true,
  "message": "Migracion masiva aplicada."
}
```

## Formato de Cache

La cache conserva el formato actual:

```json
[
  {
    "title": "Sistema",
    "icon": "mdi:shield-account-outline",
    "children": [
      {
        "title": "Soporte",
        "children": [
          {
            "title": "Tickets",
            "path": "/apps/soporte/tickets",
            "permissions": [
              "soporte.tickets.crear",
              "soporte.tickets.ver_propios"
            ]
          }
        ]
      }
    ]
  }
]
```

## Notas de Compatibilidad

- El seed inicial publica Soporte dentro del arbol `Sistema`; por eso el registro legacy actualizado usa `DESCRIPCION = 'SIS'`.
- `IS_SUPERUSER = 1` recibe todos los menus y permisos activos.
- La validacion actual de permisos de Soporte puede seguir leyendo `JSON_MENU`.
