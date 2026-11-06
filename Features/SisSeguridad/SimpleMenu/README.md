# R50 Menu simple por roles

La pantalla /apps/sis/seguridad abre Opciones por rol. Seleccionar rol, marcar
opciones y Guardar. Copiar de otro rol cambia la seleccion pendiente; no persiste
hasta Guardar. Las acciones se conservan salvo edicion explicita del apartado
avanzado. Roles por usuario permite asignar varios roles y ver su union.

## Instalacion

Ejecutar Sql/INSTALL_MENU_SIMPLE.sql como SIS en mantenimiento. Es autocontenido,
crea las tablas base faltantes, el paquete PKG_OSS_MENU_SIMPLE, secuencias y controles
R50; no aplica semillas ni asigna roles a usuarios. Requiere SIS_USUARIOS y
OSS_USUARIO_ROL existentes. Tomar respaldo completo antes de ejecutar DDL.
Oracle 10g: identificadores <=30; listas CLOB analizadas en PL/SQL sin JSON_TABLE.
La compilacion Oracle debe validarse en el servidor. La compilacion C# no la sustituye.

IMPORTANTE: el instalador cierra la escritura anterior sobre catalogos,
asignaciones y JSON_MENU mediante TRG_OSS_SM_G01 a G10. Leer el menu legacy sigue
permitido. Las pantallas anteriores que escriben directamente esas tablas,
clonacion y aplicacion de perfiles antiguos quedan bloqueadas con un mensaje.
Publicar la nueva API/frontend en la misma ventana; no volver a ejecutar las
semillas antiguas ni habilitar edicion JSON paralela. No deshabilitar los triggers
para uso diario: se perderia la revision y las secuencias podrian quedar atrasadas.

## Persistencia

Se reutilizan OSS_MOD, OSS_MENU, OSS_ROL, OSS_ROL_MENU, OSS_USR_ROL, OSS_PERM,
OSS_MENU_PERM y OSS_ROL_PERM. No se crea OSS_USR_MENU. Las excepciones de acciones
existentes se incluyen en el analisis efectivo y se desactivan al migrar, despues
de convertir su resultado al perfil por rol.

Tablas tecnicas nuevas:

- OSS_SEG_MENU_REV: revision global y bloqueo transaccional; rechaza escrituras obsoletas.
- OSS_SEG_MENU_CTL: estrategia por usuario/empresa.
- OSS_SEG_MENU_MAP: identidad de nodo migrado a partir del contexto, metadatos y permisos.
- OSS_SEG_MENU_MIG: respaldo CLOB completo del origen, roles y excepciones previos,
  permisos efectivos, hash, actor, fecha y revision del lote individual.
- OSS_SEG_MENU_AUD: detalle completo CLOB de las mutaciones. OSS_SEG_AUD conserva
  tambien el resumen compatible de hasta 1000 caracteres.

El catalogo/los roles son compartidos como en el esquema existente. El contador
de usuarios afectados incluye otras empresas. Las asignaciones de usuarios estan
limitadas a la empresa configurada. Cambiar un rol compartido actualiza las caches
de todos sus usuarios migrados, incluidas otras empresas, en la misma transaccion.

## Migracion y reverso

En Roles por usuario seleccionar usuario, Analizar migracion y Aplicar perfil
comparado. Se exige la revision y el hash observados; un cambio exige reanalizar.
El proceso lee los CLOB completos, comprueba correspondencia codigo/login,
jerarquia, rutas y campos soportados. Perfiles identicos comparten un rol MIG_...
que puede renombrarse. Un perfil unico conserva su rol exacto. Las diferencias de
orden/contexto pueden generar variantes de catalogo para preservar la presentacion.
La union de roles fusiona nodos de presentacion equivalentes y conserva sus permisos.

La migracion verifica el arbol y los permisos efectivos en la transaccion; una
diferencia cancela todos los cambios del usuario. Un usuario ya migrado no se
vuelve a migrar ni duplica filas. Usuarios inactivos conservan su estado. Los
conflictos se resuelven antes de activar; no se adivina un rol por el nombre del modulo.

El resolvedor de menu no concede opciones por IS_SUPERUSER; su perfil visual se
migra exactamente. Las reglas existentes de superusuario en APIs de acciones
permanecen; este cambio no reescribe toda la autorizacion de los modulos.
La comprobacion de permisos incluye SupportSecurity y las claves del JSON.
Los casos reales de otros consumidores legacy deben comprobarse durante el piloto.

La cache JSON se regenera por modulo con los roles actuales, eliminando registros
obsoletos incluso cuando se retira el ultimo acceso. Se conserva para consumidores
legacy y para reverso de lectura. Cambiar estrategia no restaura una instantanea
antigua: usa la cache compatible vigente. getMiMenu informa legacy para usuarios
sin migrar, vertical para activos y compatibilidad para lectura reversa ya migrada;
solo el primer caso conserva la inyeccion comun Reportes del frontend. La migracion
incorpora Reportes al catalogo y al perfil exacto en servidor.

No hay fallback automatico por errores. La revision se invalida en la sesion que
guarda; otras sesiones consultan al recuperar foco y cada 60 segundos. Las APIs
siguen siendo responsables de autorizar sus operaciones; ocultar opciones no
constituye por si solo una autorizacion de recursos.

Assets/MenuRoutes.json se publica con la API y valida las rutas migradas/nuevas.
Actualizarlo con python3 scripts/update-menu-routes.py cuando cambien paginas del
frontend. Rutas desconocidas o propiedades legacy no soportadas producen incidencias.

## Verificacion local

- dotnet build --no-restore en el backend.
- bash scripts/check-oracle-identifiers.sh Features/SisSeguridad/Sql.
- dotnet run --no-restore --project '../Requerimientos/50 - Menu Por Roles Y Usuarios/Pruebas/MenuChecks.csproj'.
- node '../Requerimientos/50 - Menu Por Roles Y Usuarios/Pruebas/verificar-menu.cjs'.
- ESLint de los archivos cambiados y npm run build en NextOssmasoft.

La activacion de Oracle y las pruebas con usuarios reales se realizan durante
publicacion. No usar las pruebas sin base de datos como certificacion del SQL instalado.

Correccion de instalacion ORA-00955: el instalador usa ALL_* filtrado por SIS,
comprueba CURRENT_SCHEMA y detecta conflictos de tipo antes de crear objetos.
PRECHECK_MENU_SIMPLE.sql permite diagnosticar cuenta/esquema y OSS_MOD sin DDL.
En DBeaver ejecutar el archivo actualizado completo, deteniendo ante el primer
error; no continuar desde una sentencia CREATE TABLE aislada.
