# OracleAnalisis - contrato HTTP y publicacion de fase 1

R56. Implementacion inicial: autenticacion de servicio y consulta de metadatos,
fuentes y datos con politicas explicitas. No hay pantalla frontend. No hay SQL libre ni ejecucion de
procedimientos, escritura, DDL ni enlaces remotos. No se crean objetos Oracle.

## Autenticacion

Todas las rutas son POST bajo /api/OracleAnalisis. Requieren una unica cabecera
X-Oracle-Analysis-Token y Content-Type application/json. El token se compara con
OracleAnalysis:ServiceAccess:Token desde IConfiguration; no usa AppSettings:Token
ni requiere JWT/login SIS adicional. Este esquema no autentica otros endpoints.

Enabled global y ServiceAccess.Enabled deben estar activos, ClientId no vacio,
Token con al menos 32 caracteres y ExpiresAtUtc futuro. Generar un secreto aleatorio
con al menos 32 bytes de entropia. El valor no se incluye en Git ni en documentacion.
El sample de appsettings esta deshabilitado y sin token, listo para configurar.
Usar overrides privados o variables de entorno en el despliegue para el valor real.

Sin token, token incorrecto/vencido o feature deshabilitada: HTTP 401. Permiso
insuficiente: HTTP 403. Saturacion: HTTP 429. Validacion y fallos Oracle:
HTTP 200 con ResultDto.isValid=false, message en espanol y data {requestId, code}.
No se devuelven mensajes Oracle con posible contenido sensible.

## Configuracion

Targets contiene aliases explicitos SIS/RH/PRE/ADM/CNT/BM/CAT/RM/MFO y por cada
uno Schema, ExpectedDatabaseName, ExpectedServerHost, ExpectedSessionUser y
SourceObjects. Las identidades esperadas son obligatorias; confirmar con el DBA,
no deducirlas del nombre de la conexion. BMC/RHC estan excluidos aun si se agregan
a AllowedAliases. Alias y esquema del cliente deben estar en mayusculas.

Ejemplo parcial sin secretos:

```json
{
  "OracleAnalysis": {
    "Enabled": false,
    "ServiceAccess": {
      "Enabled": false,
      "ClientId": "codex-oracle-analisis",
      "Token": "",
      "ExpiresAtUtc": null,
      "AllowedAliases": ["SIS", "RH", "PRE", "ADM", "CNT", "BM", "CAT", "RM", "MFO"],
      "Permissions": ["oracle.analisis.metadatos", "oracle.analisis.fuentes"]
    },
    "Targets": {
      "SIS": {
        "Schema": "SIS",
        "ExpectedDatabaseName": "",
        "ExpectedServerHost": "",
        "ExpectedSessionUser": "SIS",
        "SourceObjects": []
      }
    },
    "TimeoutSeconds": 15,
    "MaxRows": 500,
    "MaxResponseBytes": 2097152
  }
}
```

SourceObjects requiere nombres exactos de los programas aprobados para compartir.
Vacio deniega toda lectura de fuentes y errores. Conceder permiso de fuentes no
anula esta lista. Revisar primero secretos literales en el codigo que se autorice.

Comprobar identidad desde la cuenta de la conexion, mediante DBeaver:

```sql
SELECT SYS_CONTEXT('USERENV','DB_NAME') DATABASE_NAME,
       SYS_CONTEXT('USERENV','SERVER_HOST') SERVER_HOST,
       SYS_CONTEXT('USERENV','SESSION_USER') SESSION_USER
  FROM DUAL;
```

Esta version compara nombre de base, host y usuario de sesion, no DBID. No se
cambia el destino automaticamente si no coincide. La visibilidad depende de los
privilegios de la cuenta configurada; no se conceden privilegios adicionales.

## Operaciones

Campos C# PascalCase; JSON camelCase de ASP.NET Core. No enviar conexion Oracle.

| Ruta | Permiso | Cuerpo |
| --- | --- | --- |
| conexiones | metadatos | {} |
| verificarConexion | metadatos | {"conexion":"SIS"} |
| objetos | metadatos | {"conexion":"SIS","tipo":"TABLE","buscar":"USU","page":1,"pageSize":50} |
| describirObjeto | metadatos | {"conexion":"SIS","nombre":"SIS_USUARIOS"} |
| fuente | fuentes | {"conexion":"SIS","nombre":"NOMBRE_APROBADO","tipo":"PACKAGE BODY","desdeLinea":1,"cantidadLineas":200} |
| erroresCompilacion | fuentes | {"conexion":"SIS","nombre":"NOMBRE_APROBADO","tipo":"PACKAGE BODY"} |
| argumentos | metadatos | {"conexion":"SIS","nombre":"NOMBRE_RUTINA_O_PAQUETE"} |
| dependencias | metadatos | {"conexion":"SIS","nombre":"NOMBRE_OBJETO","tipo":"PACKAGE BODY","direccion":"salientes"} |

Permisos completos oracle.analisis.metadatos y oracle.analisis.fuentes. Esquema
opcional: si se envia debe coincidir con Targets[conexion].Schema. Identificadores
simples en mayusculas hasta 30 caracteres; no nombres calificados o DB links.

conexiones informa aliases configurados sin abrir Oracle. verificarConexion abre
solo la conexion elegida y comprueba destino. Todas las demas consultas verifican
identidad primero. objetos usa ALL_OBJECTS y pagina estable por nombre/tipo;
page 1..10000, pageSize 1..500. El tamano efectivo se limita con MaxRows.
No se ejecuta COUNT para calcular totales; HasMore y Limite indican continuidad.

Describir devuelve columnas/tipos, asociaciones de constraints e indices. No incluye
todavia defaults, comentarios ni texto CHECK. Cero filas significa sin metadatos
visibles, no acredita inexistencia. Dependencias son solo directas: entrantes
solo dentro del propietario autorizado; salientes reporta referencias externas
sin abrirlas. No representa dependencias por SQL dinamico.

Fuentes devuelve lineas ordenadas y hash SHA256 del segmento, no de toda la fuente.
Continuar desde la ultima LINE + 1; el limite efectivo se devuelve como limite.
No garantiza snapshot entre solicitudes ni recupera texto original de codigo wrapped.
No hay aun definicion DDL con DBMS_METADATA, busqueda global de fuentes ni cursor.

## Respuestas y limites

Excepto conexiones, data contiene:
requestId, ambiente, conexion, consultadoUtc, duracionMs y resultado.
Las colecciones de filas tienen filas, hasMore, truncado y limite. Las claves de
filas conservan nombres del diccionario en mayusculas. NUMBER se entrega como
texto para no perder precision; fechas sin zona no se etiquetan como UTC.

Limites de implementacion: comando 1..30 segundos, hasta 1000 filas segun MaxRows,
texto por celda 16384 caracteres, hasta 2 MiB en filas (reserva 8192 bytes para el
sobre). Operaciones compuestas de descripcion limitan cada coleccion por separado;
se valida ademas el limite global del sobre antes de devolverlo. Si se supera,
se devuelve RESPUESTA_DEMASIADO_GRANDE sin entregar la respuesta parcial. Resultados con
limite excedido indican hasMore/truncado. No prometer resultado completo si hay
truncamiento. Rutas de metadatos limitadas a dos consultas simultaneas y 30/minuto
por proceso; conexiones no abre Oracle y no cuenta en esa cuota. No es una cuota
distribuida entre replicas. Rate limiter avanzado pendiente.

Auditoria inicial ILogger: referencia, cliente, alias, accion, duracion y resultado,
sin token, filas, fuente ni parametros. Configurar el destino protegido de logs.
Auditoria durable con fallo cerrado y retencion automatica queda pendiente.

## Uso local por el asistente con curl

El usuario autoriza leer el secreto de OracleAnalysis:ServiceAccess:Token del
archivo settings local. Debe coincidir con el servidor; nunca se lee appsettings
remotamente. No pegar el valor en chat, comandos visibles, ejemplos o Git.

Utilidad scripts/oracle-analysis-request.py: lee un archivo JSON local explicitado
con --settings, permite override OracleAnalysis__ServiceAccess__Token y entrega
el secreto a curl por stdin mediante --config -, sin argumento visible de token.
No sigue redirecciones. No imprime el token ni contenidos del archivo settings.
No intenta reproducir todas las fuentes/precedencias de IConfiguration del servidor.

```bash
python3 scripts/oracle-analysis-request.py \
  --settings /ruta/privada/appsettings.local.json \
  --url https://qa.ejemplo/api \
  --operation verificarConexion \
  --body '{"conexion":"SIS"}'
```

Archivo local minimo: {"OracleAnalysis":{"ServiceAccess":{"Token":"valor-real"}}}.
Guardar fuera de Git y con permisos del usuario. El token real no esta generado ni
configurado por esta implementacion. HTTPS por defecto; HTTP solo si se especifica
--allow-http para el entorno privado de QA acordado.

## Publicacion y validacion

1. Publicar API; no requiere instalador SQL ni frontend.
2. Configurar token y vencimiento privados, targets y SourceObjects minimos.
3. Habilitar ambos Enabled, reiniciar segun el proveedor de configuracion.
4. Probar 401 sin token, 403 sin permiso y verificarConexion con el cliente local.
5. Consultar un objeto y una fuente aprobada; contrastar con DBeaver en QA.
6. Probar destino incorrecto, revocacion, limite, timeout y cancelacion.

Compilacion y pruebas locales no certifican conectividad ni compatibilidad de cada
vista de diccionario del servidor real. El acceso estructurado a datos se detalla abajo; aun faltan plantillas SELECT
para joins, DDL segmentado, busquedas, auditoria durable y OpenAPI detallado.

La configuracion incluida en ambos appsettings contiene los nueve aliases y sus
Targets. Completar la identidad esperada de cada destino antes de habilitarlo;
no se abren conexiones automaticamente al listarlas. BMC y RHC siguen excluidos.


## Fase 2 - POST consultarDatos

Permiso: oracle.analisis.datos. El token existente no obtiene este permiso de
manera implicita. Cada tabla requiere politica en
OracleAnalysis:Targets:<alias>:DataObjects:<tabla>. Vacio deniega todo acceso.
El archivo Examples/presupuestos-policy.json contiene la politica inicial PRE.
No se habilitan otras tablas de los nueve dominios por agregar este endpoint.

Fields: conexion, objeto, columnas (1..40, distintas), filtros (maximo 20), orden
(1..3 columnas), pageSize (1..500, limitado por MaxRows), cursor y esquema opcional.
No existe campo codigoEmpresa: se toma settings:EmpresaConfig. Filtros de cliente
sobre CODIGO_EMPRESA se combinan con el filtro obligatorio; no lo sustituyen.
Global=true requiere EmpresaColumn=null y una decision explicita de catalogo global.

Politica: Columns allowlist, EmpresaColumn o Global, UseRowId=true y DefaultOrder.
Solo tablas heap locales visibles en ALL_TABLES; no vistas, sinonimos, IOTs ni
DB links. No SQL arbitrario, expresiones, joins o funciones de cliente. Identificadores
verificados contra politica y ALL_TAB_COLUMNS, citados; valores siempre binds
Oracle tipados. Nombres de columnas con PASSWORD, CLAVE, TOKEN, SECRET, credenciales
o claves de servicio se deniegan incluso si se agregan accidentalmente a Columns.

Tipos iniciales: NUMBER (hasta 38 cifras significativas, valores como texto para
no perder precision), VARCHAR2, NVARCHAR2, CHAR, NCHAR y DATE. No CLOB/BLOB ni timestamp
en este endpoint inicial. Fechas ISO yyyy-MM-dd o yyyy-MM-ddTHH:mm:ss sin zona;
respuestas DATE con siete decimales por formato .NET, sin convertir a UTC.
Operadores eq, ne, gt, gte, lt, lte, in (1..100 valores), contains (texto literal),
isNull e isNotNull. Filtros combinados por AND. Null se consulta solo mediante
isNull/isNotNull. No aceptar valores nulos implicitos ni cadenas vacias de Oracle.

Ejemplo: Examples/ultimo-presupuesto.json. Puede enviarse con la utilidad curl
usando --operation consultarDatos y --body con el JSON correspondiente.

Respuesta en data.resultado:

```json
{
  "empresa": 13,
  "objeto": "PRE_PRESUPUESTOS",
  "columnas": [{"name":"CODIGO_PRESUPUESTO","type":"NUMBER","length":22}],
  "datos": {
    "filas": [{"CODIGO_PRESUPUESTO":"20"}],
    "hasMore": true,
    "truncado": false,
    "limite": 1
  },
  "nextCursor": "cursor-opaco-si-hay-mas-filas",
  "consistencia": "Sin snapshot entre paginas. ROWID como desempate en tabla heap; invalidar investigacion si se reorganiza la tabla."
}
```

Cursor cifrado/autenticado AES-GCM, vence a los 15 minutos. Vinculado a consulta,
orden, empresa, politica, columnas/tipos, cliente y destino. Una rotacion de token
invalida cursores anteriores. Enviar el mismo cuerpo con cursor=nextCursor; no
cambiar pageSize ni filtros. La revocacion del permiso se comprueba en cada llamada.
No devolver ROWID ni columnas internas OA$ en las filas. Los cursores no se registran.

Paginacion keyset Oracle 10g: comparaciones lexicograficas con NULLS LAST y ROWID
como desempate unico. PRE_PRESUPUESTOS no tiene PK visible en QA, por lo que UseRowId
es la estrategia aprobable explicita. No modificar/mover/reorganizar la tabla durante
la investigacion; entre paginas no hay snapshot y cambios concurrentes pueden afectar
resultados. No se publica un total inventado ni se ejecuta COUNT automaticamente.
Ordenar texto requiere longitud declarada hasta 1000 bytes. Limites de filas/bytes
pueden producir hasMore/truncado: continuar desde nextCursor cuando exista. Si una
sola fila supera el presupuesto disponible, reducir proyeccion o pedir soporte.

La descripcion de metadatos no requiere permiso de datos. Cambiar politica requiere
configuracion del servidor; ninguna solicitud puede registrar una politica. Auditar
requestId, cliente, alias, tabla, empresa, cantidad de filas y hash de consulta,
sin filtros con valores, token ni filas. Validacion local no acredita QA hasta
publicar el codigo y habilitar solo la politica de presupuestos.

### Validacion QA de datos

Publicado con autorizacion explicita en release r56-datos-20261118T175204Z.
Se verificaron ultimo presupuesto por HTTP, tres paginas sin duplicados, 401 sin
token, aislamiento por empresa, rechazo de columna sensible, tabla sin politica
y cursor alterado. La politica activada es exclusivamente PRE.PRE_PRESUPUESTOS.
No se cambiaron registros de negocio ni el token existente. Pruebas realizadas
por HTTP loopback mediante SSH; no certifican exposicion por URL publica.
