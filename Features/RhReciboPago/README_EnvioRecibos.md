# R53 - Instalacion de recibos por correo

Requiere R49/R51 (historico y PDF), R50 (permisos) y R52 (SMTP/cola) instalados.
Esta implementacion esta preparada localmente. No se ejecutaron migraciones ni
se habilitaron envios de recibos en el servidor durante el desarrollo.

## Instalacion Oracle 10g

- Respaldar antes de instalar: DDL hace commit implicito y no se revierte con
  ROLLBACK si falla un paso posterior. No borrar objetos para resolver conflictos.
- Opcion unica: Sql/INSTALL_R53_COMPLETO.sql con una cuenta administradora con
  permisos para crear objetos en RH y SIS. Seleccionar TODO y ejecutar como UNA
  sentencia DBeaver, sin agregar slash ni comandos SQL*Plus.
- Si se usan cuentas por esquema: ejecutar INSTALL_R53_RH.sql como RH y
  INSTALL_R53_SIS.sql como SIS. No ejecutar ambos caminos por duplicado.
- Verificar RH.PKG_RH_REC_MAIL y SIS.PKG_EMAIL_RECIBO: PACKAGE y PACKAGE BODY VALID;
  si fallan, consultar ALL_ERRORS. Verificar existencia de tipo 1115 en las
  comunicaciones del ambiente y permisos de las conexiones de aplicacion.
- Las tablas nuevas son RH_REC_MAIL_LOT, RH_REC_MAIL_DET, RH_REC_MAIL_GUARD,
  RH_REC_MAIL_CTL, SIS_EMAIL_CTL. SIS_EMAIL_QUEUE se amplia con referencia,
  BLOB, hash y confirmacion de sincronizacion del recibo.
- El instalador registra acciones enviar/reenviar, sin asignarlas a roles y sin
  cambiar la visibilidad del menu Recibos para quienes solo consultan PDFs.

## Configuracion y publicacion

Publicar backend y frontend juntos. Mantener el transporte SMTP probado R52.
Nuevo objeto bajo settings, en configuracion privada del ambiente:

```json
"RhReciboEmail": {
  "Enabled": false,
  "MaxItems": 100,
  "MaxDailyMessages": 200,
  "IntervalSeconds": 10
}
```

Son limites iniciales de aplicacion, no cuotas confirmadas del proveedor.
MaxItems entre 1 y 100; MaxDailyMessages entre 1 y 10000; intervalo entre 10 y
3600 segundos. Limite diario por dia de Oracle (SYSDATE), compartido por todos los
workers y por correo manual R52 cuando esta ampliacion esta habilitada. Cuenta
intentos SMTP, no destinatarios de un correo manual. Cap de PDF fijo 5 MiB.

La empresa siempre settings:EmpresaConfig=13. Cuenta/clave/remitente y TLS usan
settings:Email existentes; no se crean credenciales nuevas ni secretos en SQL.
Asignar nomina.recibos.enviar mediante Seguridad > Opciones por rol > permisos de
acciones. Conceder nomina.recibos.reenviar solo a quienes puedan repetir recibos
ya aceptados. Acceso al historial propio. Estas acciones dan acceso a nominas y
oficinas de la empresa; no hay un modelo nuevo de permisos por oficina.

Tras instalar y validar, habilitar RhReciboEmail.Enabled=true privadamente y
reiniciar la API. Requiere Email.Enabled=true. No ejecutar instancias de versiones
anteriores del worker en paralelo al activar el limitador compartido. Los envios
manuales R52 mantienen su flujo; otros origenes antiguos permanecen excluidos.
Usar el modo vertical de recibos al validar que el adjunto coincide con su vista previa.

## Recuperacion y operacion

El detalle RH es una outbox durable. Se congela el JSON de datos historicos validos
al confirmar. El worker valida persona/empresa/nomina/periodo y huella, comprueba
la version del logo y genera con RhReciboPagoPdfGenerator. Guarda el PDF y SHA256
en RH; lo copia a SIS con SOL_SMTP unica. Cualquier corte entre insertar SIS y
confirmar RH repite la insercion idempotentemente, nunca crea otra entrega.

SIS toma la fila exclusivamente, registra intento antes de contactar SMTP y finaliza
con token. Un resultado se sincroniza a RH y solo despues se confirma su consumo
en SIS, comparando estado y solicitud para no perder transiciones concurrentes.
Generaciones interrumpidas se recuperan tras 30 minutos; envios SMTP interrumpidos
pasan a INCIERTO, sin reencolar. Se conserva el mismo PDF para un reintento seguro.
Los duplicados se reservan por empresa/persona/nomina/periodo/huella bajo bloqueo
por empresa. Reenvios conservan relacion al item anterior y motivo.

Un fallo de conexion/autenticacion pausa el limitador SMTP de la empresa y los
lotes activos. Corregir el problema, luego Reanudar desde un lote propio pausado. La accion
libera SMTP y todos los lotes pausados de la empresa (incluidos pendientes manuales
ya autorizados), como informa el boton; los items ERROR
se reintentan explicitamente. No reenvia exitosos ni estados INCIERTO. Una pausa
puede propagarse al lote en el siguiente ciclo. Una cuota diaria agotada conserva
los pendientes para el siguiente dia; ampliar el limite requiere acuerdo de cuota.

Los PDFs y datos congelados permanecen en BLOB/CLOB privados de Oracle; no se
escriben en wwwroot ni se exponen por URL. La API no devuelve el cuerpo ni BLOB al
listar estados. No hay purga automatica: acordar retencion y copias de seguridad
antes de operar a volumen. Motivos e historial persisten, sin registrar claves o
contenido de PDF en logs.

## Verificacion antes de activar

1. Trabajador con un correo no principal unico: destino correcto de tipo 1115.
2. Principal unico, varios sin principal, principal invalido y sin correo.
3. Seleccion fuera del filtro, cambio de correo/datos y analisis vencido.
4. Dos confirmaciones concurrentes del mismo recibo/version: una sola entrega.
5. Cortar proceso antes/despues de PDF, insercion SIS, aceptacion SMTP y ACK RH.
   Comprobar que no se repite un correo aceptado y se informa lo incierto.
6. Probar pausa, reanudar, reintento ERROR y reenvio con permiso y motivo.
7. Con un trabajador autorizado, comprobar en pruebas destinatario, contenido PDF
   exclusivo y recepcion. Luego una seleccion pequena; no usar datos reales de
   terceros como prueba a una cuenta ajena sin autorizacion.

Compilacion y pruebas automatizadas no sustituyen estas pruebas Oracle/SMTP.
Regeneracion: python3 scripts/build-recibo-email-installer.py.
Identificadores: bash scripts/check-oracle-identifiers.sh Features/RhReciboPago/Sql.
Pruebas versionadas: bash scripts/check-recibo-email.sh (10 pruebas xUnit sin SMTP).
Regresion manual R52: bash scripts/check-email-manual.sh.
