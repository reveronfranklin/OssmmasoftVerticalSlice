# Contrato frontend - Galeria de fotos BM1 (requerimiento 39)

Fecha: 2026-10-14. Implementado en codigo; instalacion SQL y pruebas Oracle pendientes.

## Identidad y seguridad

Siempre consultar por `nroPlaca`, nunca por `numeroPlaca` derivado de BM1.
La empresa se obtiene de `settings:EmpresaConfig`; no se recibe del cliente.
En cargas se resuelve la placa desde `codigoBien` y empresa: si no existe o falla
la resolucion, se rechaza la carga, sin usar la placa enviada como alternativa.

Carga, eliminacion, edicion y principal requieren JWT valido (Authorization Bearer,
mediante el cliente Axios existente). Devuelven HTTP 401 si no hay sesion valida.
La slice no tenia una matriz de permisos por accion ni una politica de roles BM;
no se introduce una matriz ficticia. Las operaciones se limitan a fotos de la
empresa configurada. La pantalla mantiene su acceso mediante el menu existente.
Las rutas de lectura de imagen conservan su comportamiento para las URLs historicas.

## Respuesta JSON

Todas las operaciones JSON usan `ResultDto<List<BmBienFotoResponse>>`:

```json
{
  "isValid": true,
  "message": "success",
  "data": [{
    "codigoBienFoto": 125,
    "codigoBien": 4453,
    "numeroPlaca": "2-01-00-04032",
    "foto": "archivo.jpg",
    "titulo": "Vista frontal",
    "patch": "/api/BmBienesFotos/Image?numeroPlaca=2-01-00-04032&foto=archivo.jpg",
    "fechaCarga": "2026-10-14T10:30:00",
    "esPrincipal": true
  }],
  "page": 0,
  "totalPage": 0,
  "cantidadRegistros": 1
}
```

La lista no tiene paginacion: no acepta `page` ni `pageSize`. `page` y `totalPage`
son campos heredados del contenedor; no deben usarse para calcular navegacion de fotos.
`cantidadRegistros` corresponde a la consulta. `fechaCarga` procede de `FECHA_INS`
como hora local del servidor Oracle, sin offset ni conversion UTC; puede ser null.
Mostrar "Fecha no disponible" cuando sea null. `esPrincipal` es false para fotos
historicas no marcadas. Seleccion inicial: principal, o menor codigo de foto.
Orden de miniaturas: codigo ascendente. Cambiar seleccion no persiste principal.

Ante errores de negocio: HTTP 200, `isValid: false`, `data: null` y `message`
visible en español. No tratar HTTP 200 como confirmacion de guardado por si solo.
Errores de autenticacion, limites HTTP y binding pueden usar HTTP 401/413/400.

## Endpoints

### POST `/api/BmBienesFotos/GetByNumeroPlaca`

Body: `{ "numeroPlaca": "2-01-00-04032" }`.
Devuelve todas las fotos de esa placa en la empresa configurada. Placa obligatoria.

### GET `/api/BmBienesFotos/Image`

Query: `numeroPlaca=2-01-00-04032&foto=archivo.jpg`.
Respuesta binaria con tipo del archivo, o imagen sustituta/404 segun disponibilidad.
Se mantiene tambien GET `/api/BmBienesFotos/Image/{numeroPlaca}/{foto}`.
Resolver `patch` relativo usando el origen del backend, conservando URLs absolutas.

Compatibilidad historica (2026-10-15): buscar primero el archivo exacto dentro de
la carpeta de la placa y luego en la raiz configurada. Si no existe, intentar
`min_{foto}` en esas mismas ubicaciones antes de usar la imagen sustituta/404.
No buscar en otra placa ni sustituir nombres parecidos (singular/plural).
El backend registra la ruta esperada si no encuentra archivo ni miniatura, y
advierte cuando sirve una miniatura por falta del original. La resolucion de
esa alternativa puede ser menor que la del original; no se renombra ni mueve nada.

### POST `/api/BmBienesFotos/AddImage/{codigoBien}`

Multipart compatible con el contrato anterior:

- `files`: entre 1 y 20 archivos; titulo compartido si se envia mas de uno.
- `numeroPlaca`: placa canonica, conservada como campo por compatibilidad.
- `titulo`: obligatorio, trim, hasta 500 bytes UTF-8 (columna Oracle de 500 bytes).
- `cargaId`: UUID opcional, permitido solo para un archivo por peticion.

La galeria envia una peticion por archivo, con su titulo y un UUID estable.
Ejemplo: codigoBien `4453`, numeroPlaca `2-01-00-04032`, titulo `Vista frontal`,
files `frente.jpg`, cargaId `86a6d1eb-4af5-48ef-a8a0-915eca119f11`.

JPEG (.jpg/.jpeg), PNG o WebP: extension, MIME y firma inicial deben concordar.
Cada archivo debe tener contenido y pesar hasta 10 MiB; lote hasta 50 MiB y 20 fotos.
El limite HTTP del endpoint es 55 MiB incluyendo multipart; revisar limites del
proxy/IIS del ambiente si admite clientes que envian lotes grandes. La galeria
solo envia un archivo por peticion, de hasta 10 MiB.

Archivo invalido: `isValid: false` y mensaje de validacion; no se inicia ese envio.
Respuesta exitosa: lista actualizada por placa. El backend conserva archivo final
ante resultado incierto, para permitir recuperar mediante reintento.

El nombre nuevo combina empresa, bien, UUID y SHA-256. La insercion detecta un
archivo ya registrado para empresa/bien y devuelve la lista, sin duplicarlo.
Reintentar con el mismo archivo y UUID es seguro incluso si se perdio la respuesta.
Sin `cargaId`, cada envio conserva la semantica anterior de una carga nueva.
Los titulos quedan de solo lectura tras el primer intento para mantener el envio
estable; despues de guardar se pueden editar desde la galeria.

El panel muestra estado por archivo, conserva pendientes y no reenvia los exitosos.
Si se cierra, confirma descarte de pendientes; lo guardado permanece. Al terminar
un intento vuelve a consultar la lista, tambien si hubo errores parciales.
Los UUID de pendientes se conservan mientras el panel permanezca abierto; no se
promete recuperacion automatica de pendientes al recargar el navegador.

### POST `/api/BmBienesFotos/UpdateTitulo`

Body: `{ "codigoBienFoto": 125, "titulo": "Vista posterior" }`.
Valida identificador positivo, titulo no vacio y hasta 500 bytes UTF-8.
Devuelve lista actualizada. No cambia archivo ni fecha original de carga.

### POST `/api/BmBienesFotos/SetPrincipal`

Body: `{ "codigoBienFoto": 125 }`.
Marca una unica principal por empresa y bien; actualizacion serializada e indice
unico funcional como garantia adicional ante concurrencia. Devuelve lista actualizada.
No reemplaza el titulo. Foto inexistente o de otra empresa: `isValid: false`.

### POST `/api/BmBienesFotos/Delete`

Body: `{ "codigoBienFoto": 125 }`.
Confirmar en UI con titulo y placa. Elimina el registro conforme al comportamiento
anterior; conserva el archivo fisico. Si era principal, marca la foto restante de
menor codigo del mismo bien/empresa. Si no hay ninguna, no existe principal.
Devuelve lista por placa; si el registro ya no existe, devuelve lista vacia exitosa.
El frontend debe volver a consultar por placa para reconciliar ese ultimo caso.

## Instalacion y reverso

1. Respaldar definiciones actuales de SP_BM_FOTO y verificar estado previo.
2. Ejecutar como BM `Sql/SP_BM_FOTO_GALERIA.sql` y revisar USER_ERRORS.
   Es reejecutable, agrega ES_PRINCIPAL sin cambiar archivos ni FECHA_INS historica.
3. Si runtime no es BM, otorgar EXECUTE sobre SP_BM_FOTO_EDIT al usuario configurado.
   Los scripts 08_GRANT_BM_RUNTIME y 07_VAL_BM_GRANTS incluyen la nueva operacion;
   configurar sus variables con los usuarios del ambiente antes de usarlos.
4. Desplegar API y luego frontend. La API tolera respuestas de consulta antiguas
   sin fecha/principal, pero las operaciones completas necesitan la migracion.
5. Verificar listado historico, carga/reintento, titulo, principal concurrente y borrado.

Las escrituras serializan brevemente BM_BIENES_FOTO para preservar el esquema
historico MAX+1 y la unicidad con clientes anteriores. Evaluar contencion en ambiente
si hay muchas cargas concurrentes; no mantener el bloqueo durante la copia del archivo.
No se incorpora una secuencia ajena al mecanismo historico.

Reverso: volver primero al frontend/API previos; luego, si hace falta, ejecutar
`Sql/SP_BM_FOTO_GAL_REV.sql` o restaurar los SP respaldados. Conservar columna,
indice y datos nuevos, sin borrar fotos. No volver a ejecutar el instalador base
01 despues de la migracion sin reaplicar esta ultima.

Los archivos definitivos sin registro tras una falla se conservan para reintento;
los temporales se eliminan al finalizar el intento. No se borra automaticamente un
archivo definitivo ante una respuesta incierta, porque la transaccion pudo confirmar.

## Sincronizacion transparente al consultar (2026-10-15)

`GetByNumeroPlaca` mantiene su request y response. Tras una consulta exitosa,
la API inventaria solo `settings:BmFiles/<placa>` (sin subcarpetas). Incorpora
JPEG, PNG y WebP no vacios que faltan en tabla. Original y `min_` cuentan como
una sola foto, con el nombre logico sin `min_`. No renombra archivos ni corrige
por semejanza; conserva titulos y principal de todos los registros existentes.
Los nuevos registros recuperados tienen titulo y fecha desconocidos (null).
No se utiliza fecha del disco como fecha historica de carga.

El SP valida que la placa canonica identifique exactamente un bien de la empresa
configurada. La insercion se serializa con las otras escrituras y se comprueba
otra vez la existencia para evitar duplicados ante consultas concurrentes.
Si la carpeta no se puede inventariar completamente, no se inicia sincronizacion.
Si falla un registro o falta el nuevo SP, se registra el problema y se conserva
la consulta normal. Las inserciones ya confirmadas se conservan y son idempotentes.

La comprobacion de nuevas inserciones tiene presupuesto de dos segundos entre
archivos y timeout de dos segundos por llamada, con bloqueo NOWAIT en Oracle.
No es una garantia de latencia total: el acceso al disco depende del sistema.
Carpetas grandes se completan en consultas sucesivas. No hay pasos nuevos en UI.

Para evitar que una eliminacion reaparezca por conservarse el archivo, el SP de
borrado registra su nombre logico en `BM_FOTO_OMIT` dentro de la misma transaccion.
La sincronizacion respeta esas exclusiones. No se puede deducir retrospectivamente
que archivos sin registro fueron borrados antes de instalar esta version.

Instalacion adicional: ejecutar `Sql/SP_BM_FOTO_SYNC.sql` DESPUES de la migracion
`SP_BM_FOTO_GALERIA.sql`; conceder EXECUTE de `BM.SP_BM_FOTO_SYNC` al runtime
(ver scripts 07/08 actualizados). Luego publicar la API. No requiere actualizar
frontend. No reaplicar la migracion de galeria sin reaplicar despues la de sync,
pues ambas definen `SP_BM_FOTO_DEL`.

Reverso: volver a la API anterior para desactivar el inventario automatico;
conservar la tabla de exclusiones y los registros recuperados. Si se necesita
restaurar el SP de borrado, reaplicar la version de galeria. No borrar archivos.
