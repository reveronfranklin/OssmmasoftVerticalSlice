# R54 - Puesta en produccion: fase 1

Estado: implementacion local para publicar y verificar desde produccion. No se
realizan conexiones locales a produccion. No existe ejecutor DDL en esta fase.

## Configuracion e instalacion

No instalar tablas, paquetes ni seeds para este modulo. Los archivos appsettings
incluyen una plantilla DatabaseDeployment deshabilitada, sin conexiones adicionales
reales ni credenciales copiadas. La aplicacion de negocio conserva sus conexiones.
ExecutionEnabled es informativo: la ejecucion esta bloqueada por codigo siempre.

Forma recomendada de preparar el archivo privado usando las configuraciones
locales que indico el usuario (la herramienta no se conecta a ninguna base):

```sh
python3 scripts/prepare-database-deployment-config.py \
  --output /private/tmp/r54/database-deployment.private.json \
  --storage-path /srv/ossmmasoft/shared/database-deployment
```

Se copian exclusivamente RH, SIS, CNT, ADM, PRE, RM, CAT, BM y MFO de appsettings.Development.json al perfil
Pruebas y de appsettings.json a ProduccionLectura. BMC y RHC nunca se incluyen.
El archivo de salida debe estar fuera del workspace y Git, tiene permisos 0600
en Unix y no se sobrescribe. Contiene credenciales: trasladarlo privadamente al
servidor y no adjuntarlo a incidencias, logs, artefactos publicos o repositorios.
Las cuentas copiadas son las existentes: el DBA debe proporcionar/restringir
cuentas apropiadas de metadatos; el script no concede permisos ni cambia usuarios.

En el servidor de produccion:

1. Publicar API/frontend con el ambiente ASP.NET Core correcto de produccion.
   No mezclar el archivo Development con la configuracion principal de negocio.
2. Guardar el archivo privado fuera del directorio publicado. Establecer
   DatabaseDeployment:PrivateConfigPath con su ruta absoluta en la configuracion
   privada de la aplicacion o por variable de entorno.
3. Verificar StoragePath: directorio privado persistente y escribible por la cuenta
   del servicio, fuera de la aplicacion, wwwroot y cualquier ruta publica de Nginx/IIS.
   En Windows usar una ruta absoluta del servidor y ACL restringidas al servicio y
   administradores; en Unix el modulo usa directorio 0700 y archivos 0600.
4. Ingresar como superusuario y abrir /apps/sis/puesta-produccion/. Se agrega un
   enlace de arranque a Sistema solo para superusuarios, sin escribir el menu en BD.
5. Comprobar conexiones. Se consulta V$DATABASE para identificar DBID y nombre y se
   verifica visibilidad completa del inventario del esquema. Leer metadatos debe
   estar autorizado por el DBA; no otorgar SELECT ANY TABLE como solucion generica.
   Con cuenta propietaria se usan vistas ALL_; para una cuenta de catalogo se exige
   SELECT_CATALOG_ROLE o SELECT ANY DICTIONARY y se usan vistas DBA_ acotadas por OWNER.
   Revisar los privilegios efectivos para cumplir la politica de solo metadatos.
6. Confirmar las identidades descubiertas y configurar ExpectedDatabaseId en ambos
   perfiles. Si un ambiente usa bases distintas por esquema, configurar
   ExpectedDatabaseIds: { "RH": "...", "SIS": "...", "CNT": "..." }; esta
   configuracion especifica prevalece sobre el DBID comun del perfil.
7. Comparar. Un DBID ausente, incorrecto o igual entre origen y destino bloquea la
   comparacion. No se confia solamente en la etiqueta del archivo de configuracion.

El archivo privado se aplica solo a las opciones de este feature: nunca sobrescribe
ConnectionStrings ni settings de negocio. No existe perfil de ejecucion registrado.
No se ejecutaron estos pasos ni se publico en produccion desde la conexion local.

## Seguridad

Todos los endpoints requieren autenticacion y resuelven al actor con la seguridad
actual de SIS y settings:EmpresaConfig. Inicialmente el superusuario puede usar el
modulo sin instalar nuevos permisos. Otros usuarios necesitan los permisos
sistema.despliegues.comparar o sistema.despliegues.preparar, respectivamente;
no se asignan automaticamente. Cada usuario accede unicamente a sus comparaciones.
Las operaciones sobre historiales y descargas tambien comprueban propiedad.

BMC y RHC estan excluidos por codigo aunque AllowedSchemas los incluya o el cliente
los envie. No se abren sus conexiones ni se extraen sus objetos. Las referencias a
ellos se informan como dependencias, sin consultarlos, y su SQL candidato queda
comentado y marcado manual en los planes. La comparacion no consulta filas de
negocio; la autenticacion reutiliza las consultas normales de seguridad SIS.

## API

Base: api/DatabaseDeployment. Operaciones POST; respuesta ResultDto con isValid,
message y data. Fallos de autorizacion/configuracion del handler se expresan con
isValid=false; una peticion sin autenticacion recibe 401 del middleware.
No se devuelven conexiones ni contrasenas. Errores Oracle muestran numero seguro.

| Operacion | Cuerpo | data |
| --- | --- | --- |
| config | sin cuerpo | habilitado, ejecucionHabilitada=false, schemas, types, mensaje |
| check | { solicitud:"", schemas:["RH","SIS","CNT"], types:[] } | lista de esquema/ambiente, identidad, validado y mensaje |
| compare | { solicitud:GUID canonico, schemas:["RH"], types:["PACKAGE","PACKAGE BODY","PROCEDURE"] } | resumen del trabajo registrado |
| history | sin cuerpo | hasta 30 trabajos propios recientes, dentro de los ultimos 500 archivos del repositorio |
| get | { id:GUID } | resumen, estado, identidades y diferencias |
| object | { id:GUID comparacion, key:"RH\u007cPROCEDURE\u007cNOMBRE" } | pruebas y produccion: DDL, estado, columnas y dependencias |
| prepare | { id:GUID comparacion, objects:[claves] } | plan con id, hash, version, items y script |
| download | { id:GUID comparacion, key:GUID plan } | archivo application/sql; fallo como ResultDto |
| execute | cualquier cuerpo | siempre isValid=false: no disponible en fase 1 |

No hay paginacion de backend en esta primera fase: get entrega el inventario de
la comparacion acotado por limites; el DataGrid pagina localmente de 10 en 10.
history devuelve una lista acotada. No aceptar page/pageSize como si afectaran la
consulta; se incorporaran si crece el volumen de historiales.

Estados del trabajo: EN_COLA, COMPARANDO, COMPLETO, ERROR, INTERRUMPIDO. Se procesa
en segundo plano con una cola acotada y un consumidor; no abre bases al iniciar la
aplicacion con el modulo deshabilitado. Maximo dos trabajos activos por actor.
Tiempo maximo de comparacion: 20 minutos. Un reinicio marca trabajos pendientes
como interrumpidos; no los vuelve a ejecutar silenciosamente.

Una misma solicitud y filtros devuelve el mismo trabajo; otra combinacion o actor
con esa clave se rechaza. El cliente conserva la solicitud ante fallos de red.
Si la cola esta llena, el trabajo queda ERROR y se requiere una nueva solicitud.

Cada diferencia incluye key, schema, type, name, estado, sourceStatus y targetStatus.
Estados: IGUAL, NUEVO, MODIFICADO, SOLO_PRODUCCION, INVALIDO, NO_COMPARABLE. Un fallo
al obtener una definicion no se interpreta como objeto inexistente. Una falta de
visibilidad de esquema bloquea su inventario completo en vez de informar falsos nuevos.

## Planes y limites

Tipos iniciales: TABLE, VIEW, PROCEDURE, FUNCTION, PACKAGE, PACKAGE BODY, TRIGGER,
INDEX, SEQUENCE, SYNONYM y GRANT sobre objetos. No incluye usuarios, enlaces de base,
jobs, roles globales, sinonimos publicos o datos. Las restricciones de tablas se
comparan dentro de su DDL. No se infieren renombrados ni se generan borrados.

DBMS_METADATA entrega definiciones; se ignoran almacenamiento y atributos de segmento.
La normalizacion conserva literales, identificadores entre comillas y hints; ignora
comentarios ordinarios y diferencias de formato fuera de literales. El contador
START WITH de secuencias no provoca cambios por si solo. Los triggers conservan
su estado habilitado/deshabilitado en la comparacion.

prepare acepta de 1 a 500 diferencias seleccionadas y hasta diez planes diferentes
por comparacion. Preparar otra vez el mismo contenido/version devuelve el plan
existente. La seleccion se obtiene del snapshot privado; nunca se acepta SQL del
navegador. El hash vincula contenido, metadatos y version de referencia.

Se ordenan dependencias incluidas y especificacion antes del cuerpo de paquete.
Referencias excluidas, remotas, ciclos o dependencias no verificadas quedan para
revision manual. No se consulta otro esquema para completar una dependencia fuera
del alcance solicitado. Estas limitaciones son visibles en el motivo de cada item.

Los paquetes, procedimientos, funciones y vistas validos pueden aportar DDL candidato
al script. Triggers, indices, sinonimos, permisos, secuencias y tablas requieren
revision manual y se incluyen comentados. Para una tabla existente solo se proponen
columnas nuevas anulables sin default de tipos reconocidos; otras diferencias
estructurales se muestran para revision y no se convierten en ALTER destructivos.
No se sustituye una tabla existente con CREATE TABLE ni se reinicia una secuencia.

El script descargado es un plan de revision; no una migracion completa certificada.
No existe aprobacion ni ejecucion desde la aplicacion hasta implementar la fase 2.
DDL Oracle no tiene rollback global. Esta fase solo conserva snapshots, planes y
versiones previas de metadatos; no presenta esos archivos como respaldo de los datos.

## Almacenamiento y operacion

Un archivo JSON privado por solicitud, escritura atomica y nombres GUID. Conserva
identidades, filtros, actor, fechas, snapshots, diferencias, planes y resultados.
MaxObjects por esquema: 3000 por defecto (tope 5000). MaxDefinitionChars: 1 millon
por objeto (tope 2 millones). MaxTotalDefinitionChars: 32 millones por captura y
64 millones para la comparacion completa. Si se exceden, restringir tipos/esquemas.
El almacenamiento debe tener respaldo y politica de retencion operados por el
administrador; no hay borrado automatico de auditorias en esta fase.

La implementacion inicial presupone una unica instancia con almacenamiento local
privado. No activar varias replicas apuntando a la misma carpeta; antes se requerira
coordinacion distribuida. No subir el repositorio de snapshots a Git ni servirlo
por HTTP. Los objetos pueden contener codigo sensible y solo los revisan usuarios
autorizados. Las credenciales de los perfiles nunca se serializan en el historial.

## Verificacion local

- dotnet build --no-restore.
- bash scripts/check-database-deployment.sh (datos simulados; sin Oracle).
- ESLint y comprobacion TypeScript de los archivos nuevos.
- Compilacion de frontend; conectividad y comparacion reales quedan para el servidor
  de produccion como indico el usuario. No instalar SQL para probar esta fase.


Diagnostico de comprobaciones: los errores de conexion indican la etapa
(abrir conexion, consultar identidad, verificar visibilidad, inventario), el
codigo y una referencia para logs tecnicos sin credenciales. ORA-01017 se
clasifica como credenciales rechazadas; ORA-50000 no se clasifica como falta
de permisos. Un permiso concedido en una base no acredita acceso en la otra.

Los fallos del worker distinguen acceso denegado/IO del almacenamiento de la
captura de metadatos. La captura informa ambiente, esquema y etapa (conexion,
transformaciones, inventario, dependencias, columnas o definicion) sin contenido
DDL ni credenciales; errores TTC se identifican expresamente. Un registro ERROR
visible no demuestra que el fallo sea de disco: ese registro pudo guardarse antes
y despues de un error de lectura de Oracle. Iniciar una nueva comparacion despues
de publicar el diagnostico; los historiales anteriores no se reejecutan.

Correccion TTC al leer GET_DDL: la captura obtiene el CLOB solo dentro de un
bloque PL/SQL anonimo y devuelve fragmentos VARCHAR2 de hasta 1000 caracteres
junto con longitud y posicion siguiente. No requiere instalar objetos auxiliares,
no ejecuta el DDL obtenido y no convierte toda la definicion a un VARCHAR2 unico.
El bloque libera su CLOB temporal. Cada fragmento reconsulta GET_DDL; esto aumenta
el tiempo de captura para objetos grandes. La lectura rechaza cambios de longitud,
falta de avance y exceso del limite configurado. No constituye una instantanea
transaccional: evitar modificar definiciones mientras se compara y revisar el
plan antes de cualquier ejecucion futura.
Referencia Oracle 10g: https://docs.oracle.com/cd/B13789_01/appdev.101/b10802/d_lob.htm
Publicar solo la API e iniciar otra comparacion. La comprobacion real de protocolo
se realiza en el servidor; la compilacion local no acredita que TTC este resuelto.
