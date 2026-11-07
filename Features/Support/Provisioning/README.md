# Acceso de solicitantes de Soporte

Aplicacion administrativa realizada el 2026-11-07 en pruebas para la empresa
obtenida de `settings:EmpresaConfig` (13).

## Resultado

- Rol 804, clave `SOP_SOLICITANTE_13`, nombre `Solicitante de Soporte (13)`.
- 196 usuarios no superusuarios asignados: 44 activos y 152 inactivos.
- Tres superusuarios excluidos. No se cambiaron estados de cuenta.
- Permisos agregados: `soporte.tickets.crear`, `soporte.tickets.ver_propios`,
  `soporte.comentarios.crear`.
- Opciones Tickets y Notificaciones dentro de Soporte. Se reutilizaron grupos
  existentes cuando fue posible, manteniendo la estrategia de menu de cada usuario.
- Se conservaron roles y permisos anteriores. Este rol no concede administracion,
  consulta de tickets ajenos, asignacion ni comentarios internos.
- 27 fuentes legacy actualizadas y 167 fuentes agregadas. Dos fuentes del usuario
  699 tenian errores de sintaxis previos; se corrigieron una coma faltante y cierres
  de estructura, sin agregar opciones ajenas al requerimiento.
- Los menus de las 44 cuentas activas se comprobaron mediante el manejador Session
  publicado. Antes de confirmar se compararon los permisos efectivos de los 196
  usuarios contra los originales mas los tres permisos solicitados.

Respaldo privado del servidor:
`/srv/ossmmasoft-pruebas/shared/config/backups/soporte-solicitantes-20261107T182652Z.json`.
Contiene usuarios, fuentes originales y permisos efectivos anteriores; no se copia
al repositorio. La operacion queda auditada como `SOPORTE_SOLICITANTES`.

## Operacion posterior

Los usuarios deben renovar la sesion para actualizar el menu y los permisos.
Para nuevos usuarios, asignar este rol desde Seguridad > Roles por usuario.
La asignacion masiva fue puntual; no modifica automaticamente futuras cuentas.
Para modificar las opciones compartidas, usar Seguridad > Opciones por rol.

## Evidencia y herramienta administrativa

`SupportRequesterGrant.cs.txt` conserva el codigo de la consola utilizada. No forma
parte de los endpoints ni se ejecuta al iniciar la API. Usa la configuracion privada
del servidor, Oracle.ManagedDataAccess y los procedimientos de R50 para bloqueo,
rol, cache y auditoria; las asignaciones se agregan sin reemplazar los otros roles.
La transaccion rechaza cambios concurrentes de revision y diferencias de permisos
fuera de alcance. El modo sin argumento `apply` solo inspecciona.

La consola necesita .NET 10, Microsoft.AspNetCore.App, Oracle.ManagedDataAccess.dll
y `SupportRequesterMenu.cs`. Los paths de despliegue estan fijados al servidor de
pruebas. No ejecutar en otro ambiente sin ajustar y revisar su alcance. Las dos
correcciones de sintaxis se restringen a las fuentes identificadas; no es una
herramienta general para reparar menus.

Verificacion local: `dotnet build --no-restore` sin errores (advertencias existentes)
y `bash scripts/check-support-requester.sh` con 14 comprobaciones satisfactorias.
No se crearon tickets ni se enviaron correos durante la verificacion.
