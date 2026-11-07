# R52 - Instalacion y operacion

Implementa pantalla manual con CC/CCO, sin adjuntos ni recibos automaticos.
MailKit soporta TLS implicito en 465 (`SslOnConnect`) y STARTTLS explicito en 587;
el certificado se valida normalmente. No hay fallback inseguro.

## Instalacion

1. En una ventana de mantenimiento con R50 instalado y esquema activo SIS,
   abrir `Sql/INSTALL_R52_EMAIL.sql`, seleccionar TODO y ejecutar como UNA
   sentencia en DBeaver. No agregar `/`, SET ni otros comandos SQL*Plus.
   El instalador crea/amplia SIS_EMAIL_QUEUE, crea PKG_EMAIL_MANUAL y registra
   menu/permiso R50 dentro de Sistema. Si la opcion ya existe, corrige su padre
   conservando su identificador y asignaciones. Es repetible y conserva datos; DDL realiza commit implicito.
   No borrar objetos existentes para resolver un conflicto: revisar tipo/esquema.
2. Verificar que PACKAGE y PACKAGE BODY de SIS.PKG_EMAIL_MANUAL estan VALID;
   consultar ALL_ERRORS si no compilan. El instalador detiene su flujo al fallar,
   pero los DDL ya realizados no pueden revertirse con ROLLBACK.
3. Publicar API y frontend juntos. La ruta requiere el server.js actualizado.
4. En Seguridad > Opciones por rol asignar Enviar correo y el permiso
   `sistema.correos.enviar` a un rol autorizado; asignar el rol al usuario.
   El instalador no concede acceso a usuarios/roles. Sesiones con estrategia
   legacy necesitan migrar al perfil por roles para ver esta opcion del catalogo.
5. Mantener `settings:Email:Enabled=false` hasta verificar instalacion/configuracion.
   El secreto ya cargado privadamente no debe incorporarse a Git ni imprimirse.
6. Al autorizar la prueba, cambiar Enabled en la configuracion privada del ambiente
   y reiniciar el servicio para cargarla. Enviar manualmente SOLO al destinatario
   acordado, franklin.reveron@gmail.com, y comprobar recepcion/bandeja spam.

Configuracion: `settings:Email` (Enabled, Host, Port, Security, Username, Password,
FromEmail, FromName), y `settings:EmpresaConfig`. La cuenta indicada es
pruebas@mmproductosve.com.ve; nombre Ossmmasoft; host mail.mmproductosve.com.ve;
puerto 465; Security SslOnConnect. Password solo en el archivo privado del servidor.
La empresa proviene de EmpresaConfig=13. No se usa SIS_EMAIL_CONFIG para credenciales.
El antiguo EmailWorkerEnabled no activa este flujo: el interruptor es Email.Enabled.

El worker revisa cada 10 segundos y toma una solicitud MANUAL_R52 por ciclo.
NO procesa filas antiguas de soporte/otros modulos ni errores para reenvio.
Transaccion de toma exclusiva antes de contactar SMTP; actualizacion final exige
claim token. Una fila ENVIANDO de mas de 30 minutos pasa a INCIERTO cuando el worker
vuelva a procesar, nunca a PENDIENTE. Antes de reactivar un ambiente revisar sus
filas MANUAL_R52 PENDIENTE: esas solicitudes si se procesaran al habilitarlo.
No compartir conexion del worker con handlers. Idempotencia y cuota se serializan
por bloqueo del usuario; dos workers no deben tomar la misma fila.

La cola almacena destinatarios y texto para procesar el envio. No hay eliminacion
automatica en esta entrega; definir retencion antes de uso prolongado. La API solo
expone historial propio y logs de identificador/estado; no registra texto ni claves.
No permite marcar enviado/error desde HTTP. La aceptacion no implica entrega.

## Verificacion

- `dotnet build --no-restore`
- `bash scripts/check-email-manual.sh`: reglas, huella, limites, CC/CCO en MIME y
  guardas que impiden conexiones cuando esta deshabilitado o mal configurado.
- `bash scripts/check-oracle-identifiers.sh Features/Email/Sql`
- `python3 scripts/build-email-installer.py`: regenera instalador desde schema,
  package y seed; no ejecutar las fuentes y el instalador por duplicado.
- Frontend: lint de archivos tocados y `npm run build`.

La compilacion y estas pruebas no sustituyen ejecutar el instalador en Oracle 10g
ni validar autenticacion/cuota/roles con una base real y recepcion SMTP autorizada.
