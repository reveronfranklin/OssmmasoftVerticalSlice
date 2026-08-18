# Contrato Frontend - Motor de Formularios (MFO)

Requerimiento 16. Cubre la **Fase 4**: catalogo y definicion. Las respuestas
(`api/MfoRespuesta`) son de la Fase 5 y no estan en este documento todavia.

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

## Pendiente de la Fase 5

- `api/MfoRespuesta` completo (crear, guardar valores, enviar, consultar, buscar,
  anular, exportar, adjuntos).
- `api/MfoCatalogo/opciones`, que resuelve `catalogoClave` contra la lista blanca
  del backend. Hasta que exista, un campo con `origenOpciones: "CATALOGO"` no
  tiene de donde sacar sus opciones.
- La aplicacion efectiva de `MFO_PERMISO` en cada slice. Hoy los endpoints de
  definicion **no verifican permisos**.
