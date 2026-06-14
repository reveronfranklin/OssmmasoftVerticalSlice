# Runbook Produccion - Seguridad Modulo Soporte

Fecha: 2026-06-13

## Objetivo

Garantizar que, al montar el modulo de Soporte en produccion, existan los menus, permisos y roles requeridos, y que la seguridad quede asignada la primera vez tomando como fuente el `SIS.OSS_USUARIO_ROL.JSON_MENU` legacy cuando aplique.

La estrategia oficial es:

1. Instalar seguridad normalizada.
2. Cargar seed de menus/permisos/roles de Soporte.
3. Migrar usuarios desde `JSON_MENU` legacy hacia `SIS.OSS_USR_ROL` y `SIS.OSS_USR_PERM`.
4. Regenerar `SIS.OSS_USUARIO_ROL.JSON_MENU` como cache compatible con el menu actual.
5. Usar un usuario bootstrap `SOPORTE_ADMIN` solo si no existe un administrador inicial.

## Archivos

- `Features/SisSeguridad/Sql/00_INSTALL_SIS_SEGURIDAD.sql`
- `Features/SisSeguridad/Sql/04_BACKUP_SIS_SEG.sql`
- `Features/SisSeguridad/Sql/06_VAL_SOP_SEC.sql`
- `Features/SisSeguridad/Sql/07_BOOT_SOP_ADMIN.sql`
- `Features/SisSeguridad/Sql/08_SEED_APP_ROUTES.sql`
- `Features/Support/Sql/00_INSTALL_SUPPORT.sql`

## Pre-requisitos

- Ejecutar conectado con el esquema `SIS` o con permisos equivalentes sobre objetos `SIS`.
- Tener definido el `codigoEmpresa` usado por la API en `settings:EmpresaConfig`.
- Tener identificado un usuario bootstrap para administrar Soporte en primera instalacion.
- Validar si `SIS.OSS_USUARIO_ROL.JSON_MENU` ya fue migrado de `LONG` a `CLOB`. El API actual opera mejor con `CLOB`.

## Paso A - Respaldo

Ejecutar antes de cualquier migracion:

```sql
@Features/SisSeguridad/Sql/04_BACKUP_SIS_SEG.sql
```

Guardar el sufijo generado por el script. Ese sufijo se usa si se requiere rollback con `05_ROLLBACK_SIS_SEG.sql`.

## Paso B - Instalar Seguridad Normalizada

```sql
@Features/SisSeguridad/Sql/00_INSTALL_SIS_SEGURIDAD.sql
```

Este instalador crea o valida las tablas `OSS_*`, carga el modulo `SOP`, crea menus de Soporte, permisos y roles:

- `SOPORTE_USUARIO`
- `SOPORTE_AGENTE`
- `SOPORTE_ADMIN`

Tambien ejecuta `08_SEED_APP_ROUTES.sql`, que sincroniza el catalogo de menus con las rutas reales bajo `NextOssmasoft/src/pages/apps`. Las rutas de detalle, creacion, impresion y rutas dinamicas quedan registradas inactivas para evitar que aparezcan como opciones visibles del menu lateral.

El mismo seeder crea roles genericos por modulo para asignacion de menu:

- `SIS_MENU`
- `SOP_MENU`
- `CNT_MENU`
- `RH_MENU`
- `PRE_MENU`
- `ADM_MENU`
- `BM_MENU`
- `APP_MENU`

Para dar acceso visual a un modulo se puede asignar el rol generico al usuario y luego regenerar cache. Para acciones protegidas del backend, asignar tambien el rol operativo correspondiente.

## Paso C - Instalar Modulo Soporte

```sql
@Features/Support/Sql/00_INSTALL_SUPPORT.sql
```

## Paso D - Validar Semilla Soporte

```sql
@Features/SisSeguridad/Sql/06_VAL_SOP_SEC.sql
```

El resultado esperado:

- `FALTANTES = 0` en tablas requeridas.
- Menus de Soporte presentes.
- Permisos `soporte.*` presentes.
- Roles `SOPORTE_*` presentes.

## Paso E - Bootstrap De Administrador Inicial

Si ya existe un usuario con permiso `soporte.usuarios.configurar`, este paso no es obligatorio.

Si no existe, ejecutar:

```sql
@Features/SisSeguridad/Sql/07_BOOT_SOP_ADMIN.sql
```

El script solicita:

- `CODIGO_USUARIO_ADMIN`
- `CODIGO_EMPRESA`
- `USUARIO_ACCION`

Resultado:

- Asigna rol normalizado `SOPORTE_ADMIN` en `SIS.OSS_USR_ROL`.
- Registra auditoria en `SIS.OSS_SEG_AUD`.
- Crea/actualiza cache legacy `SIS.OSS_USUARIO_ROL` con `DESCRIPCION = 'SIS'` y menu Soporte administrador.

## Paso F - Migrar Desde JSON_MENU Legacy

Desde el frontend:

1. Ir a `Sistema > Seguridad`.
2. Abrir la vista de migracion.
3. Consultar migracion sugerida.
4. Revisar usuarios con `JSON_MENU` invalido.
5. Aplicar migracion sugerida por usuario o migracion masiva.

Endpoints disponibles:

- `POST /api/SisSeguridad/getResumenMigracion`
- `POST /api/SisSeguridad/getMigracionSugerida`
- `POST /api/SisSeguridad/aplicarMigracionSugerida`
- `POST /api/SisSeguridad/aplicarMigracionMasiva`

Para primera migracion masiva:

```json
{
  "codigoModulo": null,
  "usuarioUpd": 0,
  "confirmar": true
}
```

La migracion:

- Lee `SIS.OSS_USUARIO_ROL.JSON_MENU`.
- Extrae rutas y `permissions`.
- Sugiere roles normalizados.
- Inserta roles en `SIS.OSS_USR_ROL`.
- Inserta excepciones `ALLOW` en `SIS.OSS_USR_PERM` cuando el rol no cubre un permiso legacy.
- Regenera `SIS.OSS_USUARIO_ROL.JSON_MENU`.

## Paso G - Validacion Funcional

Validar con el usuario bootstrap:

1. Iniciar sesion.
2. Verificar menu `Sistema > Soporte`.
3. Abrir `/apps/soporte/tickets`.
4. Abrir `/apps/soporte/dashboard`.
5. Abrir `/apps/soporte/configuracion`.
6. Abrir `/apps/sis/seguridad`.
7. Consultar `POST /api/SupportPermissions/getByUser` y confirmar permisos `soporte.*`.

## Criterios De Aceptacion

- El modulo Soporte aparece en menu para usuarios autorizados.
- Los permisos se resuelven desde `JSON_MENU`.
- Los usuarios migrados tienen roles normalizados.
- `JSON_MENU` se regenera luego de guardar roles o clonar seguridad.
- Existe al menos un usuario con `soporte.usuarios.configurar`.
- La migracion queda auditada en `SIS.OSS_SEG_AUD`.

## Riesgos Y Controles

- Si se usa `SQL-Actualizar-JSON_MENU-Soporte.sql`, se puede reemplazar el menu completo del usuario. Usarlo solo como referencia, no como mecanismo principal de produccion.
- Si `JSON_MENU` contiene JSON invalido, el usuario no se migra automaticamente. Corregir el JSON o asignar rol manualmente.
- Si no hay usuario bootstrap, nadie podra administrar seguridad desde UI. Usar `07_BOOT_SOP_ADMIN.sql`.
- Si falta una tabla `OSS_*`, ejecutar nuevamente `00_INSTALL_SIS_SEGURIDAD.sql`.
