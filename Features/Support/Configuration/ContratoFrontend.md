# Responsable predeterminado de Soporte

- POST `api/SupportConfiguration/get`, sin cuerpo.
- POST `api/SupportConfiguration/save`, `{ "responsableId": 123 }`.
- Autenticacion obligatoria y permiso `soporte.usuarios.configurar` (incluye superusuarios).
- Empresa exclusivamente de `settings:EmpresaConfig`; actor de la sesion autenticada.
- Ambas operaciones devuelven ResultDto: `isValid`, `message`, `data` con
  `{ "responsableId": 123, "responsables": [{ "codigoUsuario": 123,
  "usuario": "Analista", "login": "ANALISTA", "email": "analista@example.com" }] }`.
- Sin paginacion: lista de analistas activos de la empresa. Guardar valida correo y
  permiso para ver tickets asignados o todos. La lista no cambia roles del analista.
- Persistencia SIS.SOP_CONFIG, con actor y fecha de ultima modificacion.

## Creacion de tickets

`api/SupportTickets/create` conserva su contrato y respuesta numerica. Cuando el
actor no es superusuario, solo puede ser solicitante de su propia cuenta. El
servidor obtiene el responsable de la configuracion, sin aceptarlo en el DTO.
Valida responsable activo, marcado analista, con acceso a tickets asignados;
correo valido de ambos usuarios en SIS_USUARIOS.EMAIL y SMTP habilitado.

El procedimiento SP_SOP_TKT_AUTO registra ticket, asignacion, historial,
notificaciones internas y un correo SOPORTE_AUTO en una sola transaccion. Ante
cualquier fallo se revierte todo. La validacion se repite en Oracle al crear para
rechazar cambios concurrentes de configuracion/contactos. Para=responsable,
CC=solicitante (sin duplicar si ambas direcciones coinciden). El correo incluye
numero de ticket, asunto y descripcion; se envia como texto por el worker existente.
No se exige al solicitante permiso de envio manual ni permiso de asignacion.

El exito confirma ticket creado y correo en cola; no garantiza entrega SMTP.
No se reintenta automaticamente una entrega incierta. No se procesan correos
legacy SOPORTE ni se notifican tickets historicos al instalar. Los superusuarios
conservan la creacion anterior y asignacion manual.

## Instalacion

Ejecutar Features/Support/Sql/INSTALL_SUPPORT_DEFAULT.sql como una sola sentencia
con esquema SIS. Incluye tabla, procedimientos y actualizacion de los paquetes de
correo R52/R53 para aceptar SOPORTE_AUTO en los dos modos del worker. Mantiene
los limites compartidos R53 cuando ese modo esta habilitado. No cambia ajustes SMTP.
Despues publicar API/frontend y seleccionar CJHONNY desde Soporte > Configuracion.
Las cuentas sin correo deben actualizar su registro de Sistema > Usuarios.


## Comprobacion realizada en pruebas (2026-11-07)

SQL instalado sin objetos invalidos. Configurado CJHONNY (usuario 3), analista activo
con correo confirmado por el usuario. API compilada y frontend construido sin errores.
TypeScript global mantiene errores anteriores en otros modulos; ninguno en los archivos
cambiados. ESLint de los archivos cambiados paso.

Prueba Oracle en transaccion revertida: creacion/asignacion, una unica fila SOPORTE_AUTO
con Para y CC correctos, ausencia de correos legacy duplicados, rechazo de responsable
y CC alterados. Rollback comprobado sin dejar tickets o correos de prueba.

Respaldo privado de paquetes previo a instalar:
`/srv/ossmmasoft-pruebas/shared/config/backups/support-default-20261107T185255Z.json`.
El correo de CJHONNY estaba vacio antes de configurarlo. No se modificaron roles,
estados de cuentas ni ajustes SMTP. Los tickets existentes no se reasignaron.
