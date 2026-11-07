# R52 - Correo manual

Ruta UI: `/apps/sis/correos` (incluida en server.js y manifiesto R50).
API base: `/api/Email`. JWT Bearer obligatorio, permiso `sistema.correos.enviar`.
Usuario: sesion autenticada. Empresa: `settings:EmpresaConfig` (13); no se reciben
usuario, empresa, remitente, host ni credenciales desde el cliente.

## Operaciones

| Metodo | Ruta | Entrada | Data de ResultDto |
| --- | --- | --- | --- |
| GET | configuracion | Ninguna | habilitado, configurado, remitente, nombre, limites, mensaje |
| POST | enviar | Solicitud, Para, Cc, Cco, Asunto, Mensaje | Estado de la solicitud registrada |
| POST | estado | Solicitud | Estado de una solicitud propia |
| POST | historial | Page (1...), PageSize (1..50) | items, total, page, pageSize |

Respuestas JSON camelCase: `{ "isValid": true, "message": "OK", "data": ... }`.
401: sesion no valida; 403: sin permiso; 422: validacion rechazada ANTES de encolar,
permite corregir el formulario conservando el texto. Otras respuestas isValid=false
pueden incluir error de cuota, conexion o resultado no confirmado. Consultar estado
antes de repetir; reutilizar la misma Solicitud y contenido ante error de red.
La misma clave con otro contenido se rechaza. No hay reintento SMTP automatico.

```json
{
  "solicitud": "198e4e51-4d93-4848-8ea5-56f56da0db12",
  "para": ["franklin.reveron@gmail.com"],
  "cc": [],
  "cco": [],
  "asunto": "Prueba R52",
  "mensaje": "Mensaje de prueba autorizado."
}
```

Ejemplo Data de estado:

```json
{
  "emailId": 100,
  "solicitud": "198e4e51-4d93-4848-8ea5-56f56da0db12",
  "asunto": "Prueba R52",
  "estado": "PENDIENTE",
  "intentos": 0,
  "fechaSolicitud": "2026-11-07T10:00:00",
  "fechaEnvio": null,
  "error": "",
  "para": ["franklin.reveron@gmail.com"],
  "cc": [],
  "cco": []
}
```

## Validaciones y estados

Maximo 20 destinatarios en total; direccion sin nombre visible, hasta 150 bytes UTF-8;
listas Para/CC/CCO separadas con coma o punto y coma en UI. Duplicados eliminados
sin distinguir mayusculas, primero Para, luego CC y CCO. Para obligatorio.
Asunto sin caracteres de control, hasta 250 bytes UTF-8; mensaje texto obligatorio,
hasta 10000 caracteres. Cada lista serializada admite 3000 bytes. Sin adjuntos.
Limite inicial de aplicacion: 20 solicitudes nuevas por usuario/hora (no es una
cuota confirmada del proveedor). Un reintento de la misma clave no consume otra.

- PENDIENTE: solicitud registrada, sin confirmacion SMTP.
- ENVIANDO: tomada exclusivamente por el worker; intento iniciado.
- ACEPTADO_SMTP: servidor SMTP acepto; NO acredita entrega final.
- ERROR: fallo antes de enviar (configuracion, conexion o autenticacion).
- INCIERTO: fallo durante SendAsync o proceso interrumpido; revisar con proveedor
  antes de crear un nuevo correo. Nunca se reencola automaticamente.

El historial pertenece al usuario/empresa autenticados y no devuelve el cuerpo.
CCO permanece en el sobre SMTP y se omite de las cabeceras del mensaje.
El navegador conserva el formulario solo en memoria. Tras recargar, consultar
Mis solicitudes antes de crear un correo equivalente.
Los antiguos endpoints `/api/EmailQueue/*` requieren autenticacion y responden 410;
ningun cliente puede modificar estados ni listar toda la cola.
