# Correo de creacion del ticket

Instalar `../Sql/SP_SOP_EMAIL_GET.sql` en SIS despues de la instalacion de soporte.
Desplegar API y frontend juntos. La instalacion no envia correos.

POST `api/SupportTicketEmails/getByTicket`, autenticado.
Request: `{ "ticketId": 123, "usuarioId": 3 }`.
La identidad debe coincidir con la sesion y tener acceso al ticket de la empresa
configurada en settings:EmpresaConfig. No se recibe codigoEmpresa.

Respuesta ResultDto: `{ "isValid": true, "message": "success", "data": [
{ "emailId": 10, "para": "analista@example.com", "cc": "solicitante@example.com",
"asunto": "Ticket SOP-123 asignado", "estado": "PENDIENTE", "intentos": 0,
"fechaSolicitud": "2026-11-16T10:00:00", "fechaEnvio": null, "error": "" }
] }`.
Sin paginacion: devuelve exclusivamente notificaciones automaticas de creacion
con origen SOPORTE_AUTO y asunto generado por SP_SOP_TKT_CREATE_AUTO.
No devuelve cuerpos ni notificaciones de comentarios internos.
Sin registros devuelve data=[]; acceso denegado o fallo devuelve isValid=false.

La ficha muestra destinatarios, asunto, estado, intentos, fechas y error.
Consulta cada 10 segundos mientras esta abierta y permite actualizar manualmente.
PENDIENTE=En cola, ENVIANDO=En proceso, ACEPTADO_SMTP=Aceptado por SMTP,
ERROR=Error, INCIERTO=Resultado incierto. ACEPTADO_SMTP no confirma entrega
al buzon. La consulta no envia ni reintenta correos.
