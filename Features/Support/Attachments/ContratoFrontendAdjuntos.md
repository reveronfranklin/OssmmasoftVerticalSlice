# Visualizar y descargar adjuntos

POST `api/SupportAttachments/file` (sesion autenticada).
Body: `{ "ticketId": 62, "usuarioId": 3, "adjuntoId": 12 }`.
No tiene paginacion. La empresa se obtiene de settings:EmpresaConfig.
Valida identidad de sesion, acceso al ticket, adjunto activo del ticket y archivo
local dentro de la carpeta de la empresa/ticket. No acepta una ruta del cliente
ni sirve enlaces simbolicos. Reutiliza SP_SOP_ATT_GET_TKT; no requiere nuevo SQL.

Exito: bytes del archivo, Content-Type segun extension y Content-Disposition
con nombre original. Acceso denegado: ResultDto con isValid=false y message.
Archivo inexistente: HTTP 404 JSON con message. Error de lectura: HTTP 400 JSON.
Ejemplo de error: `{ "message": "Adjunto no disponible." }`.

Frontend: consulta autenticada con responseType=blob. PDF usa el visor existente
DialogReportInfo; imagenes y texto se visualizan en dialogo. Word y Excel ofrecen
descarga para abrirlos en su aplicacion. La descarga solo se inicia al pulsar
Descargar. La previsualizacion no abre ventanas ni descarga automaticamente.
Los fallos de listado/archivo se muestran en la ficha; el listado permite reintentar.
Las URL temporales se liberan al cerrar/cambiar adjunto o salir de la ficha.

Desplegar API y frontend. Los adjuntos previamente registrados sin archivo fisico
en settings:SupportAttachmentsPath se muestran como no disponibles al abrirlos.
