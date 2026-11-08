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


## Comentarios internos y correo de eventos

- `POST api/SupportComments/create`: cuerpo existente `{ "ticketId": 123,
  "usuarioId": 5, "comentario": "Texto", "esInterno": true }`.
  El actor debe coincidir con la sesion y poder consultar el ticket. Un comentario
  publico exige `soporte.comentarios.crear`; uno interno exige
  `soporte.comentarios.internos`. El frontend muestra la casilla solo con este
  ultimo permiso y marca los comentarios internos con una etiqueta.
- `POST api/SupportTickets/changeStatus` y `close` conservan sus cuerpos actuales
  y ahora tambien verifican acceso al ticket. El cambio y su correo se guardan
  juntos en una transaccion; un fallo revierte ambos.
- Publicos: notificacion al solicitante y al responsable, excluyendo al autor.
  Si el autor es uno de ellos se notifica al otro. Internos: solo al responsable
  si tiene permiso de comentarios internos y no es el autor ni el solicitante.
  Estatus/cierre: ambos participantes salvo el actor. No hay autocopias ni
  direcciones duplicadas. Si no corresponde otro destinatario, no se crea correo.
- Las direcciones salen de SIS_USUARIOS.EMAIL, para usuarios activos de la empresa.
  Una direccion requerida ausente/invalida o SMTP deshabilitado impiden guardar
  el evento y se informa por `isValid=false`. El cliente no decide destinatarios.
- Comentarios e historial se filtran en Oracle mediante SP_SOP_COMM_VISIBLE y
  SP_SOP_HIST_VISIBLE; el backend calcula p_VER_INTERNO desde los permisos efectivos,
  no desde el navegador. Los comentarios internos antiguos tambien se filtran.
  Su texto no se expone mediante el historial a usuarios sin permiso.
- Correos en SOPORTE_AUTO, procesados por el worker activo. No se reactiva la cola
  legacy SOPORTE ni se envian eventos historicos. El mensaje de exito indica que
  el correo queda pendiente; no significa entregado al destinatario.

Instalacion incremental: `Features/Support/Sql/INSTALL_SUPPORT_EVENTS.sql`, una
sentencia completa en SIS sobre la instalacion anterior. No envia correos al instalar.
El instalador completo INSTALL_SUPPORT_DEFAULT.sql tambien incorpora estos objetos.
Verificacion de politica: `bash scripts/check-support-events.sh` (10 casos).


Comprobacion incremental en pruebas: instalacion sin objetos invalidos y prueba
Oracle revertida de comentarios publicos en ambos sentidos, internos dirigidos al
responsable autorizado, sin autocopia ni envio al solicitante, internos sin
receptor autorizado, filtrado de comentarios e historial, cambio de estatus,
cierre, estatus invalido atomico y ausencia de duplicados legacy. Se verifico el
rollback sin dejar ticket ni correos de prueba. Respaldo privado de definiciones:
`/srv/ossmmasoft-pruebas/shared/config/backups/support-events-20261107T195302Z.json`.

Diagnostico de creacion: los errores Oracle no previstos devuelven IsValid=false,
Data=0, codigo ORA y referencia de seguimiento, sin SQL ni datos de contactos.
Los errores de configuracion previstos conservan su mensaje funcional.

Listado de tickets: GetAll aplica desde backend la union de permisos ver_propios
(y solicitante autenticado) y ver_asignados (y responsable autenticado). Los filtros
opcionales se intersectan con ese alcance. ver_todos conserva su alcance actual.
Instalar 10_FIX_LISTA_SOLICITANTE.sql antes de publicar la API que envia los tres
parametros de alcance. El frontend no puede escoger usuario visible ni permisos.
