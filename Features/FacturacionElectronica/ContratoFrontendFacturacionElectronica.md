# Contrato Frontend - FacturacionElectronica

Requerimiento 32. Modulo de facturacion electronica sobre PostgreSQL (schema `FED`).

**Estado: Fase 3.** Endpoint de salud, CRUD de emisores, nucleo de asignacion del numero de
control y **registro del Art. 32 con su reporte mensual**. Los documentos fiscales llegan en
las fases siguientes.

## Base

```http
Base URL: https://ossmmasoft.com.ve:5143
Content-Type: application/json
```

Todos los endpoints usan `POST` y devuelven el wrapper `ResultDto`.

El frontend no debe enviar `codigoEmpresa`. El backend toma la empresa desde
`settings:EmpresaConfig`. El endpoint de esta fase no usa empresa.

**Autenticacion:** el controller lleva `[Authorize]`. Se requiere JWT valido, en el header
`Authorization` o en la cookie `X-Auth-Token`. El cliente `ossmmasofApiVertical` ya lo
resuelve; no agregar headers a mano.

## Wrapper de respuesta

```json
{
  "data": "OSSMMASOFT / fed / schema fed",
  "isValid": true,
  "message": "suscces",
  "linkData": null,
  "linkDataArlternative": null,
  "page": 0,
  "totalPage": 0,
  "cantidadRegistros": 0,
  "total1": 0,
  "total2": 0,
  "total3": 0,
  "total4": 0
}
```

| Campo | Tipo | Descripcion |
| --- | --- | --- |
| `data` | `string \| null` | Identidad de la conexion. **Escalar, no lista** |
| `isValid` | `boolean` | Exito real de la operacion |
| `message` | `string` | `suscces` en exito; el error en falla |
| `linkDataArlternative` | `string \| null` | Typo historico del wrapper. Es contrato: no se corrige |

Los campos de paginacion y totales vienen en cero: este endpoint no los usa.

## health

Comprueba que el backend alcanza el schema `FED` en PostgreSQL. No lee ni escribe datos de
negocio: ejecuta una consulta de identidad de la conexion.

```http
POST /api/FacturacionElectronica/health
```

### Request

```json
{}
```

Sin parametros.

### Response - exito

```json
{
  "data": "OSSMMASOFT / fed / schema fed",
  "isValid": true,
  "message": "suscces"
}
```

`data` trae base, usuario y schema separados por ` / `. Sirve para confirmar de un vistazo
contra que ambiente esta hablando el backend.

### Response - PostgreSQL inalcanzable

```json
{
  "data": null,
  "isValid": false,
  "message": "Error técnico al abrir conexión FED: <detalle>"
}
```

HTTP 200, como el resto del proyecto. El fallo viaja en `isValid`, no en el codigo de estado.

## Notas para el frontend

- El modulo vive en `src/fed/facturacion/`. La pagina es `src/pages/apps/fed/index.tsx`.
- Menu lateral: **Facturacion Electronica > Emisores** (`/apps/fed`), **Numeros de Control**
  (`/apps/fed/numeros-control`) y **Reporte Mensual** (`/apps/fed/reporte-mensual`). El menu
  no sale de `src/navigation/vertical`: ver `SqlOracle/README.md`.
- `data` es un escalar. `IResponseBase<T>` del proyecto tipa `data` como `T[]`, asi que el
  modulo declara su propio `IHealthResponse` en `interfaces/api.interface.ts`.

---

# Emisores

El emisor es el contribuyente al que la imprenta digital presta servicio. **El RIF es la
clave de negocio**: el Articulo 30 ata la secuencia de numero de control a ese RIF.

## Modelo Emisor

```json
{
  "id": 1,
  "rif": "J-30412887-5",
  "razonSocial": "Servicios Integrales Aramendi, C.A.",
  "domicilioFiscal": "Av. Francisco de Miranda, Caracas",
  "correo": "facturacion@aramendi.com",
  "estado": "activo",
  "rifVerificadoEl": "",
  "rifVerificadoEstado": "",
  "usuarioIns": "arivas",
  "fechaIns": "03/09/2026 11:14",
  "usuarioUpd": "",
  "fechaUpd": ""
}
```

| Campo | Tipo | Notas |
| --- | --- | --- |
| `rif` | `string` | Formato `J-12345678-9`. **Unico**, y no se modifica despues del alta |
| `estado` | `string` | `activo` o `inactivo`. No hay borrado: un emisor se desactiva |
| `rifVerificadoEl` | `string` | `dd/MM/yyyy`. Vacio si nunca se verifico |
| `rifVerificadoEstado` | `string` | `vigente`, `no_vigente` o `sin_verificar`. Art. 29.2 |

Las fechas viajan **ya formateadas como texto**, no como ISO: es lo que hace el resto del
proyecto y lo que la tabla del frontend consume directo.

## Validaciones de negocio

- RIF, razon social y domicilio fiscal son obligatorios.
- **RIF duplicado** responde HTTP 200 con `isValid = false` y el mensaje
  `Ya existe un emisor registrado con el RIF <rif>.` La defensa es la restriccion `UNIQUE` de
  la tabla, no una consulta previa: entre el `SELECT` y el `INSERT` cabe otra peticion.
- **El RIF no se puede modificar.** El request de `update` no lo acepta. Para corregir un RIF
  equivocado se desactiva el emisor y se da de alta el correcto.
- `estado` solo admite `activo` o `inactivo`; lo sostiene un `CHECK` en la tabla.

## GetAll

```http
POST /api/FacturacionElectronica/GetAll
```

### Request

```json
{ "pageSize": 10, "pageNumber": 1, "searchText": "" }
```

`searchText` busca por RIF o razon social, sin distinguir mayusculas. Vacio devuelve todo.
`pageSize` se limita a 100.

### Response

```json
{
  "data": [ { "id": 1, "rif": "J-30412887-5", "razonSocial": "..." } ],
  "isValid": true,
  "message": "suscces",
  "page": 1,
  "totalPage": 1,
  "cantidadRegistros": 1
}
```

## getById

```http
POST /api/FacturacionElectronica/getById
```

### Request

```json
{ "id": 1 }
```

### Response

`data` trae un emisor. Si no existe, `isValid = false` con
`No se encontró el emisor solicitado.`

## create

```http
POST /api/FacturacionElectronica/create
```

### Request

```json
{
  "rif": "J-30412887-5",
  "razonSocial": "Servicios Integrales Aramendi, C.A.",
  "domicilioFiscal": "Av. Francisco de Miranda, Caracas",
  "correo": "facturacion@aramendi.com",
  "usuarioIns": "arivas"
}
```

`estado` no viaja: todo emisor nace `activo`.

### Response

```json
{ "data": 1, "isValid": true, "message": "suscces" }
```

`data` es el **id generado**.

## update

```http
POST /api/FacturacionElectronica/update
```

### Request

```json
{
  "id": 1,
  "razonSocial": "Servicios Integrales Aramendi, C.A.",
  "domicilioFiscal": "Av. Francisco de Miranda, Caracas",
  "correo": "facturacion@aramendi.com",
  "estado": "activo",
  "rifVerificadoEl": "2026-09-03",
  "rifVerificadoEstado": "vigente",
  "usuarioUpd": "arivas"
}
```

**Sin `rif`.** Ver validaciones de negocio.

### Response

```json
{ "data": "suscces", "isValid": true, "message": "suscces" }
```

Si el id no existe, `isValid = false` con
`No se encontró el emisor que se intenta actualizar.`


---

# Numeros de control

El nucleo del **Rol A - imprenta digital**. El Articulo 7.4 exige que toda factura lleve el
numero de control asignado por la imprenta digital autorizada, y el Articulo 30 establece que
es esa imprenta quien lo asigna. **Los cuatro documentos en alcance lo requieren**, asi que
ninguno se emite sin pasar por aqui.

## Lo que el frontend tiene que entender antes de consumirlo

Este endpoint sostiene `INV-1`: **nunca dos numeros de control distintos para el mismo
documento de un mismo emisor**. Violarlo es la causal del Articulo 34.2 y cuesta la
autorizacion de Ossmmasoft como imprenta digital. De ahi tres consecuencias de contrato:

1. **La operacion es idempotente cuando se envia `documentoId`.** Una segunda solicitud para
   el mismo documento **devuelve el numero ya asignado**, no uno nuevo, y lo indica con
   `yaExistia = true`. El frontend debe distinguir los dos desenlaces: mostrarlos igual
   esconderia justo lo que la invariante vigila.
2. **Sin `documentoId` no hay idempotencia posible.** Cada llamada asigna un numero nuevo. Es
   valido en esta fase -el Rol B todavia no existe- pero quien integre debe saberlo.
3. **El formato lo fija la norma, no la UI.** El backend devuelve el numero ya formateado en
   `numeroControl` (`00-00000001`) y `numeroControlTexto` (`N° de Control 00-00000001`). No
   armarlo en el frontend.

## Modelo NumeroControlAsignado

```json
{
  "id": 1,
  "emisorId": 1,
  "documentoId": 0,
  "identificador": "00",
  "secuencial": 1,
  "numeroControl": "00-00000001",
  "numeroControlTexto": "N° de Control 00-00000001",
  "tipoDocumento": "factura",
  "fechaAsignacion": "03/09/2026 13:41:20",
  "yaExistia": false
}
```

| Campo | Tipo | Notas |
| --- | --- | --- |
| `identificador` | `string` | Dos digitos. Inicia en `00` y **rota al agotarse el secuencial** (decision `D-2`) |
| `secuencial` | `number` | Hasta ocho digitos. Inicia en `1`. Consecutivo **por emisor** |
| `documentoId` | `number` | `0` significa asignado sin documento. Es un estado valido en la Fase 2 |
| `yaExistia` | `boolean` | `true` = se devolvio un numero ya asignado, no se genero uno nuevo |

El relleno con ceros y el guion de `numeroControl` son **interpretacion nuestra**: el
Articulo 30 fija la cantidad de digitos, no como se escriben. Se eligio el formato de uso
corriente en Venezuela. Si cambia, cambia en el backend y el frontend no se toca.

## Tipos de documento admitidos

| Valor | Documento | Articulo |
| --- | --- | --- |
| `factura` | Factura (`DOC-1`) | 7 |
| `debito` | Nota de debito (`DOC-2`) | 8 |
| `credito` | Nota de credito (`DOC-3`) | 8 |
| `entrega` | Nota de entrega (`DOC-4`) | 10 |

Cualquier otro valor responde `isValid = false` con
`El tipo de documento debe ser factura, débito, crédito o entrega.` Lo sostiene tambien un
`CHECK` en la tabla.

## asignarNumeroControl

```http
POST /api/FacturacionElectronica/asignarNumeroControl
```

### Request

```json
{
  "emisorId": 1,
  "tipoDocumento": "factura",
  "documentoId": 0,
  "usuarioIns": "arivas"
}
```

`documentoId` es opcional; `0` significa sin documento.

### Response - exito

```json
{
  "data": {
    "numeroControl": "00-00000001",
    "numeroControlTexto": "N° de Control 00-00000001",
    "yaExistia": false
  },
  "isValid": true,
  "message": "suscces"
}
```

### Response - el documento ya tenia numero

Mismo `isValid = true`, pero `yaExistia = true` y el numero es el que ya estaba. **No es un
error**: es la idempotencia funcionando.

### Response - fallas de negocio

| Situacion | `message` |
| --- | --- |
| Emisor inexistente | `No existe un emisor con el identificador <id>.` |
| Emisor inactivo | `El emisor está inactivo: no se le pueden asignar números de control.` |
| Tipo fuera de alcance | `El tipo de documento debe ser factura, débito, crédito o entrega.` |
| Secuencia agotada | `La secuencia de números de control del emisor se agotó: se consumieron los 99 identificadores de dos dígitos.` |

Todas HTTP 200 con `isValid = false`.

## numeroControlGetAll

Lectura del registro del Articulo 32: por emisor -numeral 1- y por fecha de asignacion
-numeral 2-.

```http
POST /api/FacturacionElectronica/numeroControlGetAll
```

### Request

```json
{
  "emisorId": 0,
  "fechaDesde": null,
  "fechaHasta": null,
  "pageSize": 10,
  "pageNumber": 1
}
```

| Campo | Significado cuando viene vacio |
| --- | --- |
| `emisorId` | `0` = todos los emisores |
| `fechaDesde` / `fechaHasta` | `null` = sin limite por ese extremo. `fechaHasta` **incluye** el dia completo |

### Response

```json
{
  "data": [
    {
      "id": 1,
      "emisorId": 1,
      "emisorRif": "J-30412887-5",
      "emisorRazonSocial": "Servicios Integrales Aramendi, C.A.",
      "documentoId": 0,
      "identificador": "00",
      "secuencial": 1,
      "numeroControl": "00-00000001",
      "tipoDocumento": "factura",
      "fechaAsignacion": "03/09/2026 13:41:20",
      "reporteId": 0,
      "usuarioIns": "arivas"
    }
  ],
  "isValid": true,
  "message": "suscces",
  "page": 1,
  "totalPage": 1,
  "cantidadRegistros": 1
}
```

`reporteId` en `0` significa **pendiente de informar al SENIAT**. El control del plazo de los
10 dias continuos del Articulo 29.7 es la Fase 3; aqui solo se expone el dato.

Este listado **no** trae `numeroControlTexto`: la frase `N° de Control` corresponde a la
representacion grafica del documento, no a una tabla de consulta.


---

# Registro del Art. 32 y reporte mensual

El **Rol A** frente al SENIAT. Aqui vive `INV-2`: nunca dejar de informar la totalidad de
numeros de control asignados en un periodo mensual. **Dos periodos omitidos en un ano
calendario, consecutivos o no, son causal de revocatoria** (Art. 34.3), y no hace falta
sancion previa.

## Lo que el frontend tiene que entender

1. **Las filas de periodo existen desde antes de que haya algo que reportar.** No se crean al
   enviar. Es lo que convierte un periodo omitido en una fila visible en vez de una ausencia
   que hay que salir a calcular.
2. **Cero es un reporte valido.** El Art. 29.7 obliga a reportar "con independencia de no
   haber asignado ningun numero de control". Un periodo con `cantidadReportada: 0` **no** es
   un error ni un periodo sin procesar.
3. **`generado` no es `enviado`.** Ver la tabla de estados.

## Estados del periodo

| Estado | Significa |
| --- | --- |
| `pendiente` | El periodo existe y su reporte todavia no se genero. Si el mes no cerro, es lo normal |
| `generado` | El reporte esta calculado y sus numeros atados al periodo, pero **NO se transmitio al SENIAT** |
| `enviado` | Transmitido, con constancia en `enviadoEn` |
| `vencido` | Pasaron los diez dias continuos sin transmitir. **Es la alerta** |

**Por que existe `generado`.** Hoy no hay canal para transmitirle al SENIAT: el Art. 29.7 dice
que la informacion se remite "en los terminos y condiciones que se establezca en el Portal
Fiscal", y esas especificaciones no estan en nuestras manos. Marcar `enviado` algo que no se
envio seria escribir una constancia falsa en la tabla con la que justamente se prueba que se
reporto. Ver decision `D-19`.

## registroArt32GetAll

Lectura del registro. Sale de una **vista**, no de una tabla: por construccion no puede
diferir de lo asignado.

```http
POST /api/FacturacionElectronica/registroArt32GetAll
```

### Request

```json
{ "emisorRif": "", "periodo": "202607", "pageSize": 10, "pageNumber": 1 }
```

`emisorRif` vacio = todos. `periodo` en `AAAAMM`, vacio = todos.

### Response

```json
{
  "data": [
    {
      "numControlId": 1,
      "emisorRif": "J-30412887-5",
      "emisorRazonSocial": "Servicios Integrales Aramendi, C.A.",
      "fechaAsignacion": "15/07/2026 10:00:00",
      "fechaAsignacion8d": "15072026",
      "tipoDocumento": "factura",
      "numeroControl": "00-00000001",
      "numeracionFormato": "",
      "documentoId": 0,
      "facturaServicio": "",
      "estadoConciliacion": "sin_documento",
      "datosAdicionales": "",
      "reporteId": 3,
      "periodo": "202607"
    }
  ],
  "isValid": true,
  "message": "suscces",
  "cantidadRegistros": 3
}
```

Tres campos necesitan explicacion:

- **`numeracionFormato` viaja vacio** hasta la Fase 4. Es el numeral 5 y sale del documento,
  que todavia no existe. No es un error de datos.
- **El numeral 6 viene desdoblado** en `documentoId` y `facturaServicio`. La norma admite dos
  lecturas y se guardan las dos: ver `D-15`.
- **`estadoConciliacion` en `sin_documento` es valido**, no un pendiente que alguien olvido.
  Asignar un numero antes de que exista el documento es lo que la norma prevee (`D-16`).

## reporteMensualGetAll

```http
POST /api/FacturacionElectronica/reporteMensualGetAll
```

### Request

```json
{ "estado": "", "pageSize": 24, "pageNumber": 1 }
```

### Response

```json
{
  "data": [
    {
      "id": 3,
      "periodo": "202607",
      "fechaCierre": "31/07/2026",
      "fechaVence": "10/08/2026",
      "enviadoEn": "",
      "cantidadReportada": 3,
      "estado": "vencido",
      "ultimoIntentoEn": "03/09/2026 19:20",
      "ultimoError": "",
      "vencido": true
    }
  ],
  "isValid": true,
  "message": "suscces",
  "cantidadRegistros": 3,
  "total1": 1
}
```

**`total1` trae la cantidad de periodos vencidos.** Viaja aparte para que la pantalla pueda
avisar sin recorrer las filas: dos en un ano calendario cuestan la autorizacion.

`vencido` lo calcula el backend y no la pantalla. Es la condicion que dispara `INV-2` y no
puede depender de que cada consumidor la reimplemente igual.

## reporteMensualEjecutar

Corre el ciclo a mano: asegura las filas de periodo, genera lo que corresponda y vence lo que
paso el plazo. Un worker lo hace solo cada hora; este endpoint existe para el operador de la
imprenta y para poder verificar sin esperar un tick.

```http
POST /api/FacturacionElectronica/reporteMensualEjecutar
```

### Request

```json
{}
```

### Response

```json
{ "data": 2, "isValid": true, "message": "suscces", "cantidadRegistros": 2, "total1": 1 }
```

`data` y `cantidadRegistros` = periodos generados en esta corrida. `total1` = periodos que
**acaban de vencer**. **Es idempotente**: dos corridas seguidas devuelven cero en la segunda y
no duplican nada.
