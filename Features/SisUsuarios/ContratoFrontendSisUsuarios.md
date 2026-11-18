# Estatus de usuarios SIS

La vista /apps/sis/usuarios muestra Estatus junto a Login y permite cambiarlo
con un selector. Valores: "0" Inactivo, "1" Activo, "2" Suspendido. Los valores
legacy desconocidos se muestran sin convertirlos automaticamente a Activo.
Al seleccionar se guarda inmediatamente, se bloquea el selector durante el
proceso y se recarga el listado. Los errores se muestran sin simular un guardado.

## Actualizar estatus

POST /api/SisUsuarios/updateStatus. Requiere la validacion de sesion y el permiso
soporte.usuarios.configurar existentes en SIS. Empresa desde settings:EmpresaConfig;
usuario auditor desde la sesion validada, nunca desde el cuerpo.

Solicitud:
```json
{ "codigoUsuario": 545, "status": "1" }
```
Respuesta exitosa (ResultDto; campos adicionales habituales sin cambios):
```json
{ "data": "OK", "isValid": true, "message": "Success" }
```
Usuario invalido, estatus distinto de 0/1/2, falta de permisos o usuario ajeno a
la empresa: isValid=false y message con el motivo. No hay paginacion en esta
operacion. GetAll conserva page, totalPage y cantidadRegistros y ya incluye status.
Solo se modifican STATUS, USUARIO_UPD y FECHA_UPD; no se cambian datos de contacto,
roles ni fechas de tokens. El cambio no modifica las reglas del login legacy.

## Publicacion

1. Ejecutar Sql/SP_SIS_USR_STATUS_UPD.sql en el esquema SIS de produccion.
2. Publicar API vertical y frontend juntos.
3. Verificar cambio de Suspendido a Activo y persistencia al recargar; comprobar
   tambien rechazo sin permiso y aislamiento por empresa con usuarios de prueba.

El instalador 00_INSTALL_SIS_USR.sql incluye el nuevo procedimiento para instalaciones
completas. Para esta actualizacion basta el script individual. No modifica datos
hasta que un administrador guarde un cambio desde la vista.
