# Menú del ERP - registro del módulo FED

Requerimiento 32, tarea `T1.11`. **Oracle**, esquema `SIS`. Nada de esto es PostgreSQL: por eso vive en `SqlOracle/` y no en `Sql/`.

## De dónde sale el menú lateral

No de `src/navigation/vertical/index.ts`. En `src/layouts/UserLayout.tsx` ese import está comentado y el layout usa `ServerSideVerticalNavItems`, que hace `POST /SisUsuarios/GetMenuByUsuario`. Ese endpoint devuelve las filas de:

```sql
SELECT our.*, our.ROWID FROM SIS.OSS_USUARIO_ROL our WHERE USUARIO IN ('<login>');
```

El frontend **concatena** el `JSON_MENU` de todas las filas del usuario y lo renderiza. Una fila por rol.

## Hay dos mecanismos, y no dan el mismo resultado

| | A - JSON a mano | B - modelo normalizado |
|---|---|---|
| Dónde se escribe | `OSS_USUARIO_ROL.JSON_MENU`, por usuario | `OSS_MOD` / `OSS_MENU` / `OSS_ROL_MENU` |
| Cómo llega al menú | directo, ya es el JSON que se renderiza | `POST /api/SisSeguridad/regenerarCache` lo genera |
| Granularidad | por usuario | por rol y módulo |

**El mecanismo en uso hoy es el A.** Los `JSON_MENU` vivos están escritos a mano: tienen raíces que agrupan módulos distintos (`Soporte` conteniendo `Nomina`) y anidamiento de cuatro niveles.

> **Advertencia.** `regenerarCache` hace `MERGE` sobre `OSS_USUARIO_ROL` por `(CODIGO_USUARIO, DESCRIPCION)` y **reemplaza** el `JSON_MENU`. Si se ejecuta sobre un usuario cuyo menú está escrito a mano, ese menú pasa a ser lo que produzca el modelo normalizado. **No ejecutarlo sin comprobar antes, usuario por usuario, que el modelo reproduce el menú actual.**

## Camino A - agregar el módulo al JSON de un usuario

El bloque a insertar, como **nuevo elemento raíz** del arreglo:

```json
{
  "title": "Facturacion Electronica",
  "icon": "mdi:receipt-text-outline",
  "children": [
    {
      "title": "Emisores",
      "path": "/apps/fed"
    },
    {
      "title": "Numeros de Control",
      "path": "/apps/fed/numeros-control"
    },
    {
      "title": "Reporte Mensual",
      "path": "/apps/fed/reporte-mensual"
    },
    {
      "title": "Documentos Fiscales",
      "path": "/apps/fed/facturas"
    }
  ]
}
```

Sin acentos, como el resto del dato: en el menú vivo se lee `Administracion`, `Tesoreria`, `Nomina`.

Los `path` tienen que coincidir con las páginas reales:

| `path` | Archivo |
|---|---|
| `/apps/fed` | `src/pages/apps/fed/index.tsx` |
| `/apps/fed/numeros-control` | `src/pages/apps/fed/numeros-control/index.tsx` |
| `/apps/fed/reporte-mensual` | `src/pages/apps/fed/reporte-mensual/index.tsx` |
| `/apps/fed/facturas` | `src/pages/apps/fed/facturas/index.tsx` |

Procedimiento: `SELECT` con `ROWID` del usuario, editar el CLOB agregando el bloque al arreglo, guardar. Reversa: quitar el bloque.

**Es por usuario.** Cada `USUARIO` que deba ver el módulo necesita su propia edición.

## Camino B - `SIS_MENU_FED.sql`

`SIS_MENU_FED.sql` registra el módulo en el modelo normalizado: `OSS_MOD` código 9 (`FED`) y las opciones 9000 padre, 9010 Emisores, 9020 Números de Control, 9030 Reporte Mensual, 9040 Documentos Fiscales, con `MERGE` reejecutable.

**Registrar no es mostrar.** Sin fila en `OSS_ROL_MENU` esas opciones no llegan a ningún `JSON_MENU`, y el script deja `v_rol_clave` vacío a propósito.

Sirve para dos cosas, ninguna urgente:

1. Que el módulo exista en el modelo normalizado el día que se migre a él, y que ese día el menú no pierda la opción.
2. Que la pantalla de seguridad del ERP (`src/sis/seguridad`) pueda otorgarlo por rol sin escribir SQL.

**Hoy no hace falta ejecutarlo**, y ejecutarlo no cambia ningún menú por sí solo. Lo que sí cambia menús es `regenerarCache`, con la advertencia de arriba.
