# R53 - Envio individual de recibos seleccionados

Pantalla existente: /apps/rh/recibos. API base /api/RhReciboPago, cliente
ossmmasofApiVertical. Reutiliza visor PDF y SMTP R52; no enviar desde el navegador.

## Autorizacion y empresa

Bearer obligatorio. Permiso nomina.recibos.enviar para analizar, confirmar,
consultar historial propio, reintentar errores y reanudar. El reenvio requiere
nomina.recibos.reenviar. La concesion es para nominas/oficinas de la empresa
configurada; no se introduce una ACL nueva por oficina. El backend verifica que
cada persona pertenezca al historico y los filtros solicitados. Los lotes y acciones
posteriores se restringen al actor propietario; no hay historial de otros operadores.
Empresa desde settings:EmpresaConfig, usuario desde sesion; no se aceptan esos
valores, correos arbitrarios, rutas de archivo ni PDFs en la solicitud.

Fuente confirmada: RH.RH_COMUNICACIONES, TIPO_COMUNICACION_ID=1115,
LINEA_COMUNICACION, filtrada por CODIGO_EMPRESA y CODIGO_PERSONA. Un principal
unico tiene prioridad; sin principal se admite un correo distinto unico. Varios
registros con la misma direccion no generan ambiguedad. Principal invalido o
varios principales distintos bloquean el destinatario. No se usa RH_FAMILIARES.

## Operaciones POST

| Ruta | Cuerpo | Data |
| --- | --- | --- |
| analizarEnvio | codigoTipoNomina, codigoPeriodo, codigoIcp, personas:[int] | lote en ANALISIS, revision y vencimiento; todos los items (hasta 100) |
| confirmarEnvio | analisisId, revision, solicitud GUID | lote confirmado, sin promesa de entrega |
| getLoteEnvio | loteId, page=1, pageSize=20 | cabecera, conteos globales e items paginados |
| getLotesEnvio | page=1, pageSize=20 | items de cabeceras, total, page, pageSize |
| reintentarEnvio | itemId, solicitud GUID | mensaje; solo estado ERROR, mismo documento/destinatario congelado |
| reanudarLoteEnvio | loteId | mensaje; reanuda SMTP y los lotes pausados de la empresa, desde un lote propio pausado |
| analizarReenvio | itemId | itemId, nombre, correo actual de Comunicaciones, revision |
| reenviarRecibo | itemId, solicitud GUID, motivo, revision | nuevo lote de un recibo, vinculado al anterior |

GetResumen conserva su contrato y agrega correo/estadoCorreo. Solo se enriquecen
cuando RhReciboEmail.Enabled=true y el usuario tiene permiso de envio; en caso
contrario son cadenas vacias. No cambia la consulta/PDF historico existentes.
La revision del envio siempre vuelve a consultar Comunicaciones y los datos.

JSON camelCase y envoltura ResultDto habitual: isValid, message, data. 401 sin
sesion, 403 sin permiso; errores esperados con isValid=false y mensaje seguro.
JSON mal formado puede producir 400. Claves GUID canonicas en minusculas; mantener
la misma solicitud y contenido al reintentar por un fallo de red. Distinta solicitud
permite un nuevo envio del mismo recibo/version. Una misma solicitud GUID sigue siendo idempotente.

Analizar no envia. Nomina/periodo positivos, oficina cero (todas) o positiva,
1..100 personas positivas sin duplicados, limite efectivo configurable hasta 100.
Una persona ajena a los filtros rechaza el analisis completo. Sin correo o invalido
se marca OMITIDO. Al confirmar se comprueba revision, actor, vencimiento (15 minutos),
correo y version del recibo. Un cambio exige nuevo analisis. La seleccion vacia nunca
significa todos. Cada analisis nuevo puede confirmar el mismo recibo aunque existan entregas anteriores; la repeticion de una misma confirmacion no crea otro envio.

El modelo de lote incluye loteId, revision, estado, motivo, codigoTipoNomina,
codigoPeriodo, codigoIcp, creado, vence, habilitado, total, conteos, items, page,
pageSize. Item: itemId, codigoPersona, nombre, correo, estado, error, intentos.
GetLote: pageSize hasta 100. Historial: pageSize hasta 50; cabeceras sin detalles.
Los conteos siempre cubren el lote completo, no solo la pagina.

```json
{
  "codigoTipoNomina": 20,
  "codigoPeriodo": 5952,
  "codigoIcp": 0,
  "personas": [101, 102]
}
```

```json
{
  "analisisId": "ce2e87e2-9426-49a1-9061-f6e43b69f8d5",
  "revision": "HUELLA_DEVUELTA_POR_EL_ANALISIS",
  "solicitud": "724d1b48-dad1-447a-a087-7fedacb189aa"
}
```

## Estados y archivos

APTO/OMITIDO en revision; PENDIENTE, GENERANDO, LISTO, EN_COLA, ENVIANDO,
ACEPTADO_SMTP, ERROR, INCIERTO por item. No hay reintento SMTP automatico.
Lote derivado: ANALISIS, PROCESANDO, PAUSADO, FINALIZADO,
FINALIZADO_INCIDENCIAS. ACEPTADO_SMTP no acredita entrega ni lectura.

El backend congela datos historicos y destinatario al confirmar; genera PDF
exclusivo del trabajador con el mismo generador vertical. Persiste bytes y SHA256
antes de insertar la entrega en SIS. Un mensaje, un destinatario, un PDF hasta
5 MiB; sin CC/CCO. El reenvio es una nueva entrega con motivo hasta 500 caracteres,
revision de destinatario actual y permiso separado; exige que el documento siga
correspondiendo a la misma version y conserva el PDF original. Si hay una version
posterior debe analizarse de nuevo; no se sustituye el adjunto silenciosamente.
No ofrecer reintento para INCIERTO; revisar primero con el proveedor.

Seleccion estable por persona/nomina/periodo/oficina/fecha. Filas del mismo
trabajador se consolidan en un recibo individual del periodo, como el generador
existente; los filtros limpian seleccion.
La revision permite quitar personas mediante nuevo analisis. El historial permite
recuperar el lote despues de recargar sin almacenar borradores o PDFs en localStorage.
Cerrar el navegador no cancela los envios confirmados.


Cambio de politica (2026-11-07): se retiro el bloqueo por empresa/persona/nomina/
periodo/huella. Los envios anteriores no causan OMITIDO en un analisis nuevo, incluso
si cambia o se conserva el correo. El flujo especializado reenviarRecibo tambien
permite varias solicitudes nuevas desde un mismo item aceptado, sin exigir que sea
el ultimo. Mantiene su permiso y motivo propios. Las comprobaciones de contacto,
recibo, empresa, autenticacion y limites SMTP siguen vigentes.
Los lotes historicos omitidos no se alteran: crear un analisis nuevo.
Instalacion incremental RH: INSTALL_R53_REPETIR.sql y publicar API.

La unicidad de solicitud se aplica solamente a valores no nulos mediante el indice
UX_RRM_SOL. Se retira UK_RRM_SOL, cuya combinacion de columnas en Oracle impedia
coexistir a varios analisis sin confirmar del mismo actor. No se eliminan lotes.

Verificacion en pruebas: dos analisis simultaneos del mismo recibo, confirmacion
independiente de ambos, repeticion idempotente de confirmacion, rechazo de otra
solicitud sobre un lote ya confirmado y dos reenvios distintos desde el mismo
original. Todo revertido sin dejar lotes ni correos de prueba. Diez pruebas locales
de recibos satisfactorias. Backups privados del paquete anterior al cambio:
`/srv/ossmmasoft-pruebas/shared/config/backups/receipt-repeat-20261107T203855Z.json`;
instalacion de indice y paquete validada con respaldo adicional
`receipt-repeat-20261107T204326Z.json` en ese mismo directorio.
