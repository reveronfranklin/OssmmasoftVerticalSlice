# Contrato Frontend - Motor de Formularios (MFO)

Requerimiento 16. Cubre el backend completo: catalogo y definicion (Fase 4),
respuestas, adjuntos y autorizacion (Fase 5), y el modo parametros de reporte
(Fase 9).

Todas las rutas devuelven `ResultDto<T>`:

```json
{
  "data": null,
  "isValid": false,
  "message": "Texto en español, ya listo para mostrar",
  "page": 0,
  "totalPage": 0,
  "cantidadRegistros": 0
}
```

Reglas que aplican a todo el contrato:

- **`codigoEmpresa` no viaja en ningun request.** El backend lo resuelve desde
  `settings:EmpresaConfig`.
- **Los flags son `bool`**, no `"S"`/`"N"`. La conversion la hace el backend.
- **Los fallos de negocio llegan con `isValid: false` y un mensaje en español**,
  no como HTTP 400 ni como excepcion. El unico caso de error HTTP es una caida
  real del servidor.
- **El usuario conectado viaja en la cabecera `X-Usuario`.** Si falta, la
  operacion se ejecuta igual y queda sin usuario en la auditoria.

---

## `api/MfoCatalogo`

### `GET api/MfoCatalogo/GetAll?soloActivos=true`

Catalogo de tipos de campo. Cada fila se corresponde con un componente
registrado en `registroTipos.ts`; `componente` es la clave que los une.

Item:

```json
{
  "tipoCampoId": 7,
  "codigo": "FECHA",
  "nombre": "Fecha",
  "columnaValor": "FEC",
  "admiteOpciones": false,
  "admiteMultiple": false,
  "esPresentacion": false,
  "admiteArchivo": false,
  "componente": "CampoFecha",
  "orden": 70,
  "icono": "mdi:calendar",
  "activo": true
}
```

Un tipo que llegue con un `componente` que el frontend no tenga registrado debe
degradar a un aviso visible, no romper la pantalla.

---

## `api/MfoFormulario`

### `POST api/MfoFormulario/GetAll`

Request:

```json
{
  "searchText": "evaluacion",
  "estado": "ACTIVO",
  "modoUso": "CAPTURA",
  "page": 1,
  "pageSize": 50
}
```

Los tres filtros son opcionales. `modoUso` admite `CAPTURA`, `PARAMETROS` y
`MIXTO`.

Item:

```json
{
  "formularioId": 1,
  "alias": "REP_BM1",
  "nombre": "Parametros - Reporte de Bienes Municipales",
  "descripcion": "...",
  "categoria": "Reportes",
  "estado": "ACTIVO",
  "versionPublId": 1,
  "entidadDestino": null,
  "maxRespUsuario": 0,
  "permiteBorrador": false,
  "modoUso": "PARAMETROS",
  "registraEjec": false,
  "versionNumero": 1,
  "borradores": 0
}
```

`borradores` es la cantidad de versiones en `BORRADOR`. Sirve para que la lista
marque los formularios con una edicion en curso sin pedir el detalle de cada uno.

Paginacion en `page`, `totalPage` y `cantidadRegistros`.

### `GET api/MfoFormulario/getById?id=1` o `?alias=REP_BM1`

Acepta cualquiera de los dos. Devuelve el formulario y su historial de versiones:

```json
{
  "formulario": { "...": "igual que el item de GetAll, con versionNumero y borradores en 0" },
  "versiones": [
    {
      "versionId": 2,
      "numero": 2,
      "estado": "PUBLICADA",
      "notas": "Se agrego el campo MOTIVO",
      "hashDef": "1a2b3c4d5e6f7a8b",
      "versionOrigenId": 1,
      "fechaPubl": "2026-08-16T10:00:00",
      "usuarioPubl": "jperez",
      "fechaArch": null,
      "campos": 6,
      "respuestas": 0
    }
  ]
}
```

`respuestas` por version es lo que permite avisar antes de archivar: una version
con respuestas se sigue necesitando para renderizarlas.

### `POST api/MfoFormulario/create`

```json
{
  "alias": "EVAL_DESEMPENO",
  "nombre": "Evaluacion de desempeño",
  "descripcion": null,
  "categoria": "Recursos Humanos",
  "entidadDestino": null,
  "maxRespUsuario": null,
  "permiteBorrador": true,
  "modoUso": "CAPTURA",
  "registraEjec": false
}
```

`alias`: solo `A-Z`, `0-9` y `_`, empezando por letra, maximo 24 caracteres. El
limite es porque la vista de proyeccion se llama `MFO_V_<ALIAS>` y en Oracle un
identificador no pasa de 30.

`data` trae el `formularioId` creado. Un alias repetido devuelve
`isValid: false` con el mensaje correspondiente.

**El formulario nace sin version.** Hay que llamar despues a
`api/MfoVersion/create`.

### `POST api/MfoFormulario/update`

Mismo cuerpo que `create` mas `formularioId`, y **sin `alias`**: el alias no se
puede cambiar. Es la identidad estable que usan las rutas del frontend y el
enlace de reportes; cambiarlo romperia enlaces ya repartidos sin que nada avise.
Para renombrar de cara al usuario se cambia `nombre`.

Los campos `bool?` y `string?` nulos significan "no lo cambies".

### `POST api/MfoFormulario/delete`

```json
{ "formularioId": 1, "estado": "INACTIVO" }
```

No borra: activa o inactiva. Un formulario con respuestas no se elimina nunca.
`INACTIVO` impide abrirlo para llenar; las respuestas ya capturadas se siguen
consultando.

---

## `api/MfoVersion`

### `POST api/MfoVersion/create`

```json
{ "formularioId": 1, "notas": "Version inicial" }
```

Crea una version **vacia** en `BORRADOR`. `data` trae el `versionId`.

Un formulario admite **un solo BORRADOR a la vez**. Si ya hay uno, devuelve
`isValid: false`: dos borradores simultaneos obligarian a decidir cual se publica
y que pasa con el otro, y esa pregunta no tiene respuesta buena.

### `POST api/MfoVersion/clone`

```json
{ "versionOrigenId": 1, "notas": "Se agrega el campo MOTIVO" }
```

Clona la definicion completa a un `BORRADOR` nuevo, preservando las `CLAVE` y
remapeando los ids internos.

```json
{ "versionId": 2, "condicionesOmitidas": 0 }
```

**`condicionesOmitidas` hay que mostrarlo si es mayor que cero.** Son condiciones
cuyo destino u origen ya no existia y que no se pudieron clonar: son ramas de
logica que se pierden, y el usuario tiene que enterarse ahora y no cuando el
formulario no se comporte como esperaba.

### `POST api/MfoVersion/validar`

```json
{ "versionId": 2 }
```

Valida sin publicar. `data` es la lista de hallazgos:

```json
{
  "severidad": "ERROR",
  "codigo": "OPC_FALTA",
  "entidad": "CAMPO",
  "entidadId": 12,
  "clave": "TIPO_SOL",
  "mensaje": "El campo Tipo es de tipo SELECT y no tiene opciones activas ni catalogo."
}
```

`total1` trae la cantidad de hallazgos con `severidad: "ERROR"`. **Solo esos
impiden publicar**; los `AVISO` no. Es lo que permite habilitar o deshabilitar el
boton de publicar sin recorrer la lista.

Codigos de hallazgo: `SIN_CAMPOS`, `CLAVE_SEC_DUP`, `CLAVE_CAMPO_DUP`,
`OPC_FALTA`, `OPC_SOBRA`, `COND_DESTINO`, `COND_ORIGEN`, `COND_CICLO`,
`REGLA_TIPO`, `PRES_REQUERIDO`, `SEC_FILAS`, `SEC_VACIA`.

### `POST api/MfoVersion/publicar`

```json
{ "versionId": 2 }
```

Valida, calcula la huella, archiva la version publicada anterior y mueve el
puntero del formulario. Todo en una transaccion.

Si hay errores de validacion devuelve `isValid: false` diciendo cuantos; hay que
llamar a `validar` para verlos.

### `POST api/MfoVersion/archivar`

```json
{ "versionId": 2 }
```

Sobre una `PUBLICADA`: la archiva y **el formulario queda sin version vigente**,
asi que no se puede llenar hasta publicar otra. Conviene confirmarlo en pantalla.

Sobre un `BORRADOR`: lo descarta por completo.

### `GET api/MfoVersion/getFull?alias=REP_BM1` o `?versionId=2`

La definicion completa en **una sola llamada**. Por alias devuelve la version
publicada vigente (lo que necesita el renderizador); por id, la que se pida (lo
que necesita el diseñador para trabajar sobre un borrador).

```json
{
  "versionId": 1,
  "formularioId": 1,
  "numero": 1,
  "estado": "PUBLICADA",
  "hashDef": "1a2b3c4d5e6f7a8b",
  "alias": "REP_BM1",
  "nombre": "Parametros - Reporte de Bienes Municipales",
  "descripcion": "...",
  "categoria": "Reportes",
  "modoUso": "PARAMETROS",
  "registraEjec": false,
  "permiteBorrador": false,
  "entidadDestino": null,
  "secciones": [
    {
      "seccion": {
        "seccionId": 1, "clave": "FILTROS", "titulo": "Filtros del reporte",
        "descripcion": "...", "orden": 10, "columnas": 2,
        "esPaso": false, "repetible": false, "minFilas": null, "maxFilas": null,
        "colapsable": false
      },
      "campos": [
        {
          "campo": {
            "campoId": 1, "seccionId": 1, "clave": "FECHA_DESDE",
            "etiqueta": "Desde", "ayuda": "...", "placeholder": null,
            "orden": 10, "ancho": 6, "requerido": true, "soloLectura": false,
            "valorDefecto": null, "origenOpciones": null, "catalogoClave": null,
            "mascara": null, "unidad": null,
            "tipoCampoId": 7, "tipoCodigo": "FECHA", "componente": "CampoFecha",
            "columnaValor": "FEC", "admiteOpciones": false,
            "admiteMultiple": false, "esPresentacion": false,
            "admiteArchivo": false
          },
          "opciones": [],
          "reglas": [
            {
              "reglaId": 1, "campoId": 1, "claveCampo": "FECHA_DESDE",
              "tipoRegla": "REQUERIDO", "param1": null, "param2": null,
              "mensaje": "Indique la fecha desde.", "orden": 10, "activo": true
            }
          ]
        }
      ]
    }
  ],
  "condiciones": [
    {
      "condicionId": 1, "accion": "MOSTRAR", "destinoTipo": "CAMPO",
      "destinoId": 5, "claveDestino": "MOTIVO",
      "campoOrigenId": 4, "claveOrigen": "TIPO_SOL",
      "operador": "IGUAL", "valorCompara": "B",
      "grupo": 1, "conector": "Y", "orden": 10
    }
  ]
}
```

Los atributos del tipo (`componente`, `columnaValor`, `admiteMultiple`, ...)
vienen ya dentro de cada campo: el frontend no tiene que cruzar contra el
catalogo.

Las condiciones traen `claveDestino` y `claveOrigen` resueltas. El frontend
trabaja con claves; no necesita los ids.

**Esta respuesta esta cacheada en el backend por `versionId`.** Una version
publicada es inmutable -lo garantizan los triggers de la base-, asi que la cache
no necesita invalidacion. Solo la resolucion `alias -> version` se invalida al
publicar o archivar. Las versiones en `BORRADOR` no se cachean nunca.

---

## `api/MfoDefinicion`

Todas las operaciones de edicion. **Solo funcionan sobre una version en
`BORRADOR`**: sobre una publicada devuelven
`"No se puede modificar una version publicada. Cree una version nueva."`.

Eso no es una comprobacion del backend sino de los triggers de la base, que
cubren `INSERT`, `UPDATE` y `DELETE`. La UI deberia mostrar una version publicada
en modo lectura con ese aviso, para que el usuario entienda el modelo de
inmutabilidad desde la pantalla y no lo descubra por un error.

| Ruta | Cuerpo | `data` devuelto |
| --- | --- | --- |
| `POST seccion/upsert` | `MfoSeccionUpsertRequest` | `seccionId` |
| `POST seccion/delete` | `{ "id": 1 }` | condiciones borradas |
| `POST campo/upsert` | `MfoCampoUpsertRequest` | `campoId` |
| `POST campo/delete` | `{ "id": 1 }` | condiciones borradas |
| `POST campo/reorder` | `{ "seccionId": 1, "camposId": [3,1,2] }` | campos reordenados |
| `POST opcion/upsert` | `MfoOpcionUpsertRequest` | `opcionId` |
| `POST opcion/delete` | `{ "id": 1 }` | 0 |
| `POST regla/upsert` | `MfoReglaUpsertRequest` | `reglaId` |
| `POST regla/delete` | `{ "id": 1 }` | 0 |
| `POST condicion/upsert` | `MfoCondicionUpsertRequest` | `condicionId` |
| `POST condicion/delete` | `{ "id": 1 }` | 0 |

Notas que importan al construir el diseñador:

- En los `upsert`, el id nulo significa alta. La identidad real es la **clave**
  (`clave` en secciones y campos, `valor` en opciones, `tipoRegla` en reglas), no
  el id: si se manda una clave que ya existe, se actualiza esa fila.
- `seccion/delete` y `campo/delete` devuelven **cuantas condiciones se
  perdieron**. Hay que avisarlo.
- `campo/reorder` reescribe `orden` como 10, 20, 30... Los ids que no pertenezcan
  a la seccion se ignoran en vez de moverse.
- `condicion/upsert` recibe `claveDestino` y `claveOrigen`, no ids. Rechaza un
  destino que no exista en la version y rechaza que un campo dependa de si mismo.
- `regla/upsert` rechaza reglas incoherentes con el tipo del campo (un `MIN`
  sobre un texto, un `SEL_MAX` sobre un campo no multivalor).
- La regla `REQUERIDO` y el atajo `campo.requerido` se mantienen sincronizados
  por el backend: no hay que escribir los dos.

---

## `api/MfoPermiso`

### `POST api/MfoPermiso/set`

```json
{ "formularioId": 1, "rolCodigo": "ADMIN", "acciones": ["LLENAR", "VER"] }
```

Reemplaza el conjunto completo de acciones de ese rol. Una lista vacia revoca
todo. Acciones validas: `DISENAR`, `LLENAR`, `VER`, `EXPORTAR`, `ANULAR`.

### `GET api/MfoPermiso/GetAll?formularioId=1&roles=ADMIN,RH`

Sin `roles`: todos los permisos del formulario. Con `roles`: solo las acciones
permitidas a esa lista.

---

## `api/MfoRespuesta`

Formato de un valor, comun a guardar, enviar y ejecutar un reporte:

```json
{ "clave": "FECHA_DESDE", "fila": 0, "orden": 0, "valor": "2026-01-01", "etiqueta": null }
```

- `valor` es **siempre texto**, incluso para numeros y fechas. El backend
  convierte segun el tipo del campo. Fechas en `yyyy-MM-dd`.
- `fila` es 0 salvo en secciones repetibles (1, 2, 3...).
- `orden` es 0 salvo en campos multivalor (1, 2, 3... por cada seleccion).
- Booleanos viajan como `"S"` / `"N"`.

### `POST api/MfoRespuesta/create`

```json
{ "alias": "SOL_MANT", "claveIdem": "uuid-del-cliente", "entidadRef": null, "claveRef": null }
```

Devuelve `{ "respuestaId": 12, "versionId": 3 }`. `claveIdem` da idempotencia:
dos llamadas con la misma clave producen una sola respuesta.

### `POST api/MfoRespuesta/saveValores`

```json
{ "respuestaId": 12, "valores": [ ... ] }
```

Solo validacion **estructural**: es el autoguardado del borrador y no exige los
obligatorios.

### `POST api/MfoRespuesta/submit`

Mismo cuerpo que `saveValores`. Aplica ademas reglas, condiciones y `UNICO`.

Cuando falla la validacion, `data` trae los errores ubicados:

```json
{
  "data": [
    { "clave": "JUSTIFICA", "fila": 0, "orden": 0, "codigo": "REQUERIDO", "mensaje": "Justifique la urgencia." }
  ],
  "isValid": false,
  "message": "La respuesta tiene 1 error(es) de validacion."
}
```

Cada error se pinta junto a su control usando `clave` + `fila` + `orden`. No es
un toast.

### `GET api/MfoRespuesta/getById?id=12`

Devuelve el sobre y los valores. **No devuelve la definicion**: se pide con
`api/MfoVersion/getFull?versionId=` usando el `versionId` del sobre, que es
cacheable para siempre porque una version publicada es inmutable.

### `POST api/MfoRespuesta/GetAll`

Busqueda paginada. Filtros: `alias`, `estado`, `fechaDesde`, `fechaHasta`,
`usuario`, `entidadRef`, `claveRef`, y `claveCampo` + `valorTexto` para buscar
por el valor de un campo concreto.

### `POST api/MfoRespuesta/anular` · `delete` · `export`

`anular` exige `motivo`. `delete` solo borra borradores. `export` **exige
`alias`** y devuelve formato largo (una fila por valor).

---

## `api/MfoAdjunto`

### `POST api/MfoAdjunto/upload?valorId=99`

`multipart/form-data` con el campo `archivo`. Limite 10 MB. Extensiones
permitidas por lista blanca en el backend; ampliarla requiere despliegue.

### `GET api/MfoAdjunto/download?adjuntoId=5`

Devuelve el archivo. El MIME se resuelve por la extension real del archivo
guardado, nunca por el que declaro el cliente. Verifica permiso `VER` sobre el
formulario dueño.

---

## `api/MfoReporte`

Modo parametros de reporte. El formulario alimenta los parametros de un reporte
existente en vez de guardar una respuesta de negocio.

### `GET api/MfoReporte/getByFormulario?alias=REP_BM1`

Tambien admite `formularioId` y `soloActivos` (por defecto `true`). Devuelve las
tres cosas de una vez:

```json
{
  "data": {
    "reportes": [
      {
        "reporteId": 1, "formularioId": 1, "alias": "REP_BM1", "clave": "BM1_PDF",
        "nombre": "Reporte de Bienes Municipales (PDF)", "tipoEjec": "ENDPOINT",
        "claveRegistro": "REPORTE_BM1_PDF", "orientacion": "HORIZONTAL",
        "maxFilas": null, "timeoutSeg": 120, "orden": 10, "activo": true,
        "modoUso": "PARAMETROS", "registraEjec": false,
        "parametros": 4, "columnas": 0, "registrado": true
      }
    ],
    "parametros": [
      { "repParamId": 1, "reporteId": 1, "nombreParam": "FechaDesde", "origen": "CAMPO",
        "claveCampo": "FECHA_DESDE", "tipoDato": "FECHA", "obligatorio": true, "orden": 10 }
    ],
    "columnas": []
  },
  "isValid": true
}
```

`registrado` **no viene de la base**: dice si `claveRegistro` esta en la lista
blanca del backend. Si es `false`, el reporte esta configurado pero no habilitado
y ejecutarlo va a fallar. La pantalla debe avisarlo antes, no despues.

### `POST api/MfoReporte/ejecutar`

```json
{ "reporteId": 1, "valores": [ { "clave": "FECHA_DESDE", "valor": "2026-01-01" } ] }
```

**Devuelve dos cosas distintas segun el desenlace**, igual que
`api/ReporteBm1/pdf`:

- **Exito**: el PDF, con `Content-Type: application/pdf` y
  `Content-Disposition: inline`. Va al visor existente; **sin descarga forzada y
  sin `window.open`**.
- **Fallo esperado**: `ResultDto` JSON con `isValid: false`. Si el fallo es de
  validacion, `data` trae los errores por campo en el mismo formato que
  `submit`.

El frontend distingue por `Content-Type` de la respuesta.

Cabeceras que acompañan al PDF:

| Cabecera | Contenido |
| --- | --- |
| `X-Mfo-Resultado` | `OK` o `TRUNCADO` |
| `X-Mfo-Filas` | filas incluidas |
| `X-Mfo-Mensaje` | aviso cuando hubo truncamiento |

`TRUNCADO` **devuelve el PDF igual**: el usuario recibe lo que pidio pero tiene
que saber que esta incompleto. Mostrar el aviso no es opcional.

Mensajes propios que no son errores tecnicos:

- sin datos: "El reporte no devolvio datos con esos parametros."
- limite: "Se alcanzo el limite de N filas. Refine los parametros."
- no registrado: "El reporte '...' no esta registrado en el backend."

### `POST api/MfoReporte/ejecuciones`

Bitacora paginada. Con `formularioId` exige permiso `VER` sobre el; **sin
`formularioId` se acota al usuario en curso**.

### `POST api/MfoReporte/ultimos`

```json
{ "reporteId": 1, "cantidad": 10 }
```

Ultimas ejecuciones **del usuario en curso** con su `paramsJson`, para recargar
los filtros en el formulario. Es la capacidad que los dialogos de parametros
codificados a mano no tienen.

### `POST api/MfoReporte/upsert` · `delete` · `param/upsert` · `param/delete` · `columna/upsert`

Configuracion. Todas exigen `DISENAR`. En `delete`, `data` vale `1` si el reporte
se borro y `0` si solo se inactivo por tener ejecuciones en bitacora.

En `param/upsert` el campo se indica por `claveCampo`, no por id, y se resuelve
contra la version publicada. `origen` decide que fuente se usa y las otras dos se
ignoran:

| `origen` | Fuente | Lo controla |
| --- | --- | --- |
| `CAMPO` | valor del payload por `claveCampo` | el usuario |
| `FIJO` | `valorFijo` de la configuracion | el diseñador |
| `SISTEMA` | `claveSistema` ∈ `CODIGO_EMPRESA`, `USUARIO`, `FECHA_ACTUAL`, `IP_ORIGEN` | **solo el servidor** |

Un valor enviado en el payload para un parametro `SISTEMA` se descarta sin
mirarlo.

---

## Autorizacion

`MFO_PERMISO` guarda, por formulario y rol, cuales de estas acciones se permiten:
`DISENAR`, `LLENAR`, `VER`, `EXPORTAR`, `ANULAR`. El rol es el codigo de
`SIS.OSS_USUARIO_ROL` y el usuario viaja en `X-Usuario`.

| Endpoints | Accion exigida |
| --- | --- |
| `api/MfoFormulario` update/delete, `api/MfoVersion` (salvo `getFull`), todo `api/MfoDefinicion`, `api/MfoPermiso`, `api/MfoReporte` upsert/delete/param/columna | `DISENAR` |
| `api/MfoRespuesta` create/saveValores/submit/delete, `api/MfoAdjunto/upload` | `LLENAR` |
| `api/MfoRespuesta/getById`, `api/MfoAdjunto/download`, `api/MfoReporte/getByFormulario` | `VER` |
| `api/MfoRespuesta/export`, `api/MfoReporte/ejecutar` | `EXPORTAR` |
| `api/MfoRespuesta/anular` | `ANULAR` |

Reglas de la politica:

- **Un formulario sin permisos definidos esta abierto.** Es deliberado: el motor
  se instala con formularios sin configurar y denegar por defecto los dejaria
  inaccesibles sin pista de por que. En cuanto se define el primer permiso, ese
  formulario queda cerrado a quien no lo tenga.
- **Un fallo al comprobar el permiso deniega.** Lo contrario convertiria una
  caida de SIS en una puerta abierta.
- `api/MfoFormulario/GetAll`, `getById`, `create` y `api/MfoVersion/getFull` no
  exigen permiso: son catalogo y metadatos, y `getFull` lo necesita el
  renderizador para pintar el formulario a quien solo tiene `LLENAR`.

---

## Limites conocidos

- **El permiso de reporte es por formulario, no por reporte.** Quien pueda
  exportar un formulario puede ejecutar todos sus reportes. Distinguirlos
  necesitaria una tabla de permisos propia.
- **`TIMEOUT_SEG` acota lo que espera la peticion, no la consulta.** El comando
  sigue corriendo en Oracle hasta terminar; el usuario deja de esperar.
- **Habilitar un reporte nuevo requiere despliegue**: hay que registrarlo en
  `MfoRegistroReportes.cs`. Lo que el motor elimina es el trabajo repetido de la
  pantalla de parametros, no el despliegue del reporte.
- **La limpieza de adjuntos huerfanos no esta implementada.**
  `SP_MFO_RESP_DELETE` informa cuantos quedaron sin fila, pero nadie los borra.
