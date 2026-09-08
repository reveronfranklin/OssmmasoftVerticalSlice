# Contrato Frontend - FacturacionElectronica

Requerimiento 32. Modulo de facturacion electronica sobre PostgreSQL (schema `FED`).

**Estado: Fase 5.** Endpoint de salud, CRUD de emisores, nucleo de asignacion del
numero de control, registro del Art. 32 con su reporte mensual, emision y consulta de
documentos fiscales, los contadores del panel, y **la emision de notas de debito y credito con
el estado derivado del documento**. La nota de entrega (Fase 6) todavia no se emite.

**Ningun documento emitido tiene validez fiscal** hasta que el SENIAT autorice a Ossmmasoft como
imprenta digital: todos vienen con `esPrueba: true` y su motivo.

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

---

# Documentos fiscales (Fase 4)

## Lo que el frontend tiene que entender antes de consumirlo

**Un documento lleva DOS numeraciones distintas y no son intercambiables.** La `numeracion`
es la del emisor (Art. 7.2), consecutiva por emisor, tipo y serie. El `numeroControl` lo asigna
la imprenta digital (Art. 7.4). La norma exige las dos, y exige que sean distintas: mostrar una
en lugar de la otra es un incumplimiento, no un detalle de presentacion.

**El frontend no manda la numeracion.** La genera el sistema. Solo un emisor declarado en modo
`externa` puede traer la suya, en `numeracionExterna`, y el backend la rechaza si el emisor no
esta en ese modo.

**La emision es idempotente y el frontend es responsable de la clave.** `claveIdempotencia`
debe generarse **una vez por formulario abierto**, no por envio. Sin ella, un doble clic produce
dos documentos fiscales, y emitir dos ejemplares del mismo documento es causal de revocatoria de
la autorizacion del emisor (Art. 21.2). Si la clave ya existe, la respuesta trae el documento
original con `yaExistia: true` y **no se creo nada nuevo**: el mensaje al usuario tiene que
distinguir los dos casos.

**Todo documento sale marcado como prueba** mientras el SENIAT no autorice a Ossmmasoft:
`esPrueba: true` y `motivoPrueba` con la razon. Falta el numeral 7.14 -nomenclatura y fecha de
la providencia de autorizacion-, asi que ninguno tiene validez fiscal. Eso debe verse en
pantalla, no quedar en el JSON.

## Tipos de documento y su denominacion

| `tipoDocumento` | `denominacion` que devuelve | Se emite por | Estado |
|---|---|---|---|
| `factura` | `FACTURA` | `facturaCreate` | construido |
| `debito` | `NOTA DE DÉBITO` | **`notaCreate`** | construido |
| `credito` | `NOTA DE CRÉDITO` | **`notaCreate`** | construido |
| `entrega` | `GUÍA DE DESPACHO` | `facturaCreate` | Fase 6, no construido |

**`facturaCreate` RECHAZA `debito` y `credito`.** No es un olvido: el Art. 23 de la
SNAT/2011/00071 exige que la nota haga referencia a la fecha, numero y monto de la factura que
soporto la operacion, y ese dato no existe en esa solicitud. El mensaje de rechazo nombra
`notaCreate`. Ver la seccion de notas al final.

La denominacion la fija la norma y viene armada del backend. **No se traduce ni se adorna en el
frontend**: «guia de despacho» no es «nota de entrega».

## facturaCreate

Emite un documento fiscal. Todo-o-nada: la numeracion del documento y el numero de control se
asignan en la misma transaccion, o no se asigna ninguno.

```http
POST /api/FacturacionElectronica/facturaCreate
```

### Request

```json
{
  "emisorId": 12,
  "tipoDocumento": "factura",
  "serie": "",
  "numeracionExterna": "",
  "adqNombre": "COMERCIAL SABANA GRANDE, C.A.",
  "adqRif": "J-31558240-6",
  "adqDocumentoId": "",
  "usuarioIns": "avanessa",
  "claveIdempotencia": "ui-1788210656-a4f2c1",
  "renglones": [
    { "descripcion": "Servicio profesional", "cantidad": 2, "precio": 100, "alicuota": 16, "exento": false, "codigo": "SRV-01" },
    { "descripcion": "Libro", "cantidad": 3, "precio": 10, "alicuota": 0, "exento": true, "codigo": "LIB-02" }
  ]
}
```

| Campo | Significado cuando viene vacio |
|---|---|
| `serie` | Sin serie. La unicidad es por emisor + tipo + serie + numeracion |
| `numeracionExterna` | El sistema numera. **Solo** se admite con valor si el emisor esta en modo `externa` |
| `adqRif` | Se admite `adqDocumentoId` -cedula o pasaporte- en su lugar, para personas naturales |
| `claveIdempotencia` | **No dejarlo vacio.** Sin clave no hay proteccion contra el doble envio |

Los renglones no llevan la alicuota a criterio del frontend: el porcentaje se valida contra las
alicuotas que declara el ERP, y el valor aplicado queda guardado en el documento.

### Response - exito

```json
{
  "data": {
    "documentoId": 41,
    "numeracion": "1",
    "numeracionConSerie": "1",
    "serie": "",
    "tipoDocumento": "factura",
    "denominacion": "FACTURA",
    "numeroControl": "00-00000001",
    "numeroControlTexto": "N° de Control 00-00000001",
    "rangoNumerosControl": "desde el N° 00-00000001 hasta el N° 00-00000001",
    "fechaEmision8d": "04092026",
    "horaEmision": "08.47.39 p.m.",
    "fechaAsignacion8d": "04092026",
    "totalExento": 30.00,
    "totalBase": 250.00,
    "totalIva": 36.00,
    "totalGeneral": 316.00,
    "esPrueba": true,
    "motivoPrueba": "Falta la Providencia de autorización del SENIAT (Art. 7.14)",
    "leyendaProvidencia": "Emitida conforme a lo dispuesto en la Providencia Administrativa SNAT/2024/000102",
    "yaExistia": false
  },
  "isValid": true,
  "message": "suscces"
}
```

`fechaEmision8d` viene en ocho digitos `DDMMAAAA` y `horaEmision` como `HH.MM.SS` **con puntos**
y con `a.m.`/`p.m.`, tal como exige el Art. 7.6. Vienen ya formateadas: no reformatear.

### Response - la misma clave llega dos veces

Identica a la anterior salvo `"yaExistia": true`, y con los datos del documento original. El
`documentoId` es el mismo. **No se creo un documento nuevo**, y el mensaje al usuario debe
decirlo.

### Response - el documento no cumple el Art. 7

```json
{ "data": null, "isValid": false, "message": "El documento no cumple el Artículo 7: falta el numeral 7.7 (adquiriente). falta el numeral 7.8 (al menos un renglón)." }
```

HTTP 200 con `isValid: false`, como todo el proyecto. Devuelve **todos** los numerales
incumplidos, no el primero: quien corrige necesita verlos juntos. El rechazo queda registrado en
la bitacora.

## facturaGetAll

Listado de documentos emitidos, paginado.

```http
POST /api/FacturacionElectronica/facturaGetAll
```

### Request

```json
{ "emisorId": 0, "tipoDocumento": "", "pageSize": 10, "pageNumber": 1 }
```

| Campo | Significado cuando viene vacio |
|---|---|
| `emisorId` | `0` = todos los emisores |
| `tipoDocumento` | `""` = los cuatro tipos |

### Response

```json
{
  "data": [
    {
      "id": 41, "emisorId": 12, "tipoDocumento": "factura", "denominacion": "FACTURA",
      "serie": "", "numeracion": "1", "numeracionConSerie": "1",
      "numeroControl": "00-00000001",
      "emitidoEn": "04/09/2026 20:47", "fechaEmision8d": "04092026", "horaEmision": "08.47.39 p.m.",
      "emisorRif": "J-77777777-7", "emisorRazonSocial": "PRUEBA EMISION FASE 4",
      "adqNombre": "CLIENTE DEMO", "adqRif": "V-12345678-9",
      "totalExento": 30.00, "totalBase": 250.00, "totalIva": 36.00, "totalGeneral": 316.00,
      "esPrueba": true
    }
  ],
  "isValid": true, "message": "suscces",
  "page": 1, "totalPage": 1, "cantidadRegistros": 1, "total1": 1
}
```

`total1` = **cuantos de los documentos listados son de prueba**, contados sobre la pagina
traida. Sirve para el aviso de la grilla; **no es un total del conjunto** y no debe presentarse
como tal.

`numeroControl` llega por `LEFT JOIN`. Si viniera vacio hay una anomalia -la emision crea las
dos cosas en una transaccion- y el frontend debe mostrarla, no esconderla en una celda en
blanco.

---

# Panel

## panelResumen

Los contadores del modulo para un periodo, en una sola llamada y una sola fila.

```http
POST /api/FacturacionElectronica/panelResumen
```

Devuelve un objeto propio de contadores, **no** los campos `total1..total4` del wrapper: esos,
en el resto del proyecto, acompanan a una lista.

### Request

```json
{ "periodo": "" }
```

| Campo | Significado cuando viene vacio |
|---|---|
| `periodo` | El **mes en curso**. Con valor, debe ser `AAAAMM` -por ejemplo `202609`- o la respuesta es `isValid: false` |

### Response

```json
{
  "data": {
    "periodo": "202609",
    "documentosPeriodo": 4,
    "documentosPrueba": 4,
    "numerosAsignadosPeriodo": 36,
    "emisoresTotal": 3,
    "emisoresActivos": 3,
    "emisoresRifSinVerificar": 2,
    "periodoEstado": "pendiente",
    "periodoVence": "10/10/2026",
    "periodoDiasRestantes": 36,
    "periodoCantidadReportada": 0,
    "periodosVencidosAnio": 1
  },
  "isValid": true,
  "message": "suscces"
}
```

| Campo | Que significa |
|---|---|
| `documentosPeriodo` / `documentosPrueba` | Documentos emitidos en el periodo, y cuantos de ellos no tienen validez fiscal. Hoy los dos numeros coinciden, y coincidiran hasta que el SENIAT autorice |
| `numerosAsignadosPeriodo` | Numeros de control asignados en el periodo. **Puede ser mayor** que `documentosPeriodo`: asignar sin documento es valido (Arts. 7.5 y 7.15) |
| `emisoresRifSinVerificar` | Emisores cuyo RIF nunca se verifico. El Art. 29.2 obliga a exigir el comprobante **vigente**, no una sola vez al dar de alta |
| `periodoDiasRestantes` | Dias hasta el vencimiento del reporte. **Negativo** si ya vencio |
| `periodosVencidosAnio` | Periodos del **ano calendario** en estado `vencido`. **Es el numero critico: dos revocan la autorizacion, sin sancion previa (Art. 34.3).** Si es mayor que cero, la pantalla tiene que gritarlo |

Los contadores salen de una sola consulta para que sean coherentes entre si: contarlos en
momentos distintos permitiria que la pantalla afirme que hay 30 documentos y 29 numeros
asignados.

**Los ultimos documentos no vienen aca.** La pantalla los pide a `facturaGetAll` con
`pageSize: 5`, que ya existe y trae el numero de control por join.

---

# Notas de debito y credito (Fase 5)

## Lo que el frontend tiene que entender antes de consumirlo

**`facturaCreate` NO emite notas.** Devuelve `isValid: false` si le llega
`tipoDocumento` `debito` o `credito`, y el mensaje nombra `notaCreate`. No es una
restriccion de la implementacion: el **Art. 23 de la Providencia SNAT/2011/00071**
exige que la nota haga referencia a la fecha, numero y monto de la factura que
soporto la operacion, y ese dato no existe si la emision no arranca desde un
documento. En este modulo una nota mal emitida **no se puede reparar**: no hay
`UPDATE` ni `DELETE` sobre las tablas de documentos.

**La nota se emite DESDE un documento.** El formulario no se llena en blanco: la
pantalla parte de la fila del documento que se corrige, y los tres datos del Art.
23 se muestran de solo lectura.

**Una nota es un documento fiscal completo.** Tiene su propia numeracion (Art.
7.2), su propio **numero de control** (Art. 7.4) y sus propios totales. No hereda
los de la factura.

**Los montos de la nota son POSITIVOS.** El signo lo pone la denominacion
-`NOTA DE CRÉDITO` o `NOTA DE DÉBITO`-, no el importe, igual que en papel. El
sentido se aplica al calcular el saldo del documento corregido.

**El estado del documento es DERIVADO, no una columna.** Sale de las notas que lo
corrigen y de la bitacora. Y **saldo cero no significa anulado**: el Art. 22
distingue operaciones que *quedaren sin efecto* de las que *originaren un ajuste*,
asi que una nota por el importe total sigue siendo un ajuste si nadie declaro la
anulacion. Son actos juridicos distintos con la misma aritmetica.

**La anulacion es una intencion declarada**, no un endpoint aparte: se emite una
nota de credito con `esAnulacion: true`. Un documento anulado no admite mas
correcciones, y su original **se conserva intacto** -Art. 36 obliga a conservarlo,
Art. 41 prohibe alterarlo-.

## notaCreate

```http
POST /api/FacturacionElectronica/notaCreate
```

### Request

```json
{
  "emisorId": 8,
  "documentoOrigenId": 24,
  "tipoDocumento": "credito",
  "motivo": "Devolución parcial de mercancía",
  "esAnulacion": false,
  "adqNombre": "COMERCIAL SABANA GRANDE, C.A.",
  "adqRif": "J-31558240-6",
  "serie": "",
  "moneda": "",
  "tasaCambio": 0,
  "usuarioIns": "avanessa",
  "claveIdempotencia": "ui-nota-1788210656-a4f2c1",
  "renglones": [
    { "descripcion": "Devolución de mercancía", "cantidad": 1, "precio": 200, "alicuota": 16 }
  ]
}
```

| Campo | Obligatorio | Nota |
|---|---|---|
| `documentoOrigenId` | **si** | El documento que se corrige. Art. 23 |
| `tipoDocumento` | **si** | `debito` o `credito`. Cualquier otro se rechaza |
| `motivo` | **si** | Art. 22: la norma admite *cualquier causa*, pero no la ausencia de causa |
| `esAnulacion` | no | Solo valido en una nota de **credito**. Declara que la operacion queda sin efecto |
| `moneda` | no | Vacio es `VES`. **Debe coincidir con la del documento corregido** |
| `tasaCambio` | condicional | Obligatorio cuando la moneda no es `VES` (Art. 13.14) |
| `claveIdempotencia` | **no dejarlo vacio** | Sin ella, un doble clic acredita dos veces la misma devolucion |

### Response - exito

Es el **mismo objeto** que devuelve `facturaCreate`, con tres campos mas que en
una factura vienen vacios:

```json
{
  "data": {
    "documentoId": 25,
    "numeracion": "1",
    "numeracionConSerie": "1",
    "tipoDocumento": "credito",
    "denominacion": "NOTA DE CRÉDITO",
    "numeroControl": "00-00000002",
    "numeroControlTexto": "N° de Control 00-00000002",
    "rangoNumerosControl": "desde el N° 00-00000002 hasta el N° 00-00000002",
    "fechaEmision8d": "07092026",
    "horaEmision": "01.50.31 p.m.",
    "fechaAsignacion8d": "07092026",
    "totalExento": 0.00,
    "totalBase": 200.00,
    "totalIva": 32.00,
    "totalGeneral": 232.00,
    "esPrueba": true,
    "motivoPrueba": "Falta la Providencia de autorización del SENIAT (Art. 7.14)",
    "leyendaProvidencia": "Emitida conforme a lo dispuesto en la Providencia Administrativa SNAT/2024/000102",
    "yaExistia": false,
    "documentoOrigenId": 24,
    "referenciaOriginal": "Factura N° 1 del 07092026 por 1.160,00 VES",
    "motivo": "Devolución parcial de mercancía",
    "leyendaContribuyente": ""
  },
  "isValid": true,
  "message": "suscces"
}
```

| Campo | Que es |
|---|---|
| `numeroControl` | **El de la nota, no el de la factura.** Cada documento consume el suyo |
| `referenciaOriginal` | La frase del Art. 23, **ya armada por el backend**. El texto lo fija la norma: no se rearma en la pantalla |
| `leyendaContribuyente` | Art. 15.6. `Contribuyente Formal` o `no sujeto al impuesto al valor agregado` cuando el emisor **no** es contribuyente ordinario. Vacio si lo es |

### Response - la misma clave llega dos veces

Identica salvo `"yaExistia": true`, con los datos de la nota original y el mismo
`documentoId`. **No se acredito dos veces.** El mensaje al usuario tiene que
distinguirlo.

### Response - fallas de negocio

Todas con HTTP 200 y `isValid: false`. Cada una sale de un articulo:

| Caso | Mensaje, abreviado |
|---|---|
| Sin motivo | *El Articulo 22 admite cualquier causa, pero no la ausencia de causa* |
| Sin documento origen | *El Articulo 23 exige la referencia a la fecha, numero y monto...* |
| Origen inexistente | *No existe un documento con el identificador N* |
| Origen de otro emisor | *El documento que se intenta corregir pertenece a otro emisor* |
| Origen que no es factura | *Solo se corrige una factura... el Articulo 22 habla de operaciones por las cuales se otorgaron facturas* |
| Origen ya anulado | *El documento que se intenta corregir ya esta anulado* |
| Moneda distinta a la del origen | *Deben coincidir para que la referencia al monto del Articulo 23 signifique algo* |
| **Credito que excede el saldo** | *La nota de credito es por X y al documento le queda un saldo pendiente de Y* |
| Nota de debito que declara anulacion | *El Articulo 22 la reserva para operaciones que quedan sin efecto, y una nota de debito aumenta el monto* |

El rechazo de contenido -numerales del Art. 13 o del 15- queda registrado en la
bitacora como `rechazo`, igual que en la emision directa.

## Lo que cambio en facturaGetAll

El listado suma cuatro campos:

| Campo | Que es |
|---|---|
| `moneda` | `VES` o el codigo de la divisa |
| `estado` | `emitido`, `ajustado` o `anulado`. **Derivado**, ver arriba |
| `saldo` | Lo que queda por corregir. Total menos creditos mas debitos |
| `cantidadNotas` | Cuantas notas afectan a este documento |

Llegan por `LEFT JOIN` a la vista de estado. Si `estado` viniera vacio hay una
anomalia y la pantalla debe mostrarla, no esconderla.

**La accion de emitir nota se deshabilita, no se oculta**, cuando la fila no es
una factura o ya esta anulada, con el motivo en el tooltip: que no se pueda
corregir un documento es informacion util.

## Lo que cambio en el emisor

`create`, `update`, `GetAll` y `getById` suman **`tipoContribuyente`**:
`ordinario`, `formal` o `no_sujeto`.

De ese dato depende contra que articulo se valida una nota de ese emisor -el 13 o
el 15 de la 00071, *"segun sea el caso"* del Art. 23- y si el documento lleva la
leyenda del Art. 15.6. Por defecto `ordinario`, que es el conjunto **mas
estricto**: un emisor sin declarar queda sobre-validado y no sub-validado.


---

# Fase 6B - Comprobante de retencion (Art. 11)

## Lo PRIMERO que hay que entender antes de integrarlo

**Este documento NO lleva numero de control.** No hay campo `numeroControl` en el
request ni en la respuesta, y no es una omision del contrato: es como esta
definido el documento.

El Art. 11 de la SNAT/2024/000102 **no remite a los numerales 4 y 5 del Art. 7**,
que son los que crean el numero de control -a diferencia del Art. 10.2, que si lo
hace para la guia de despacho-. Su numeral 5 pide el numero de control **de la
factura que se esta reteniendo**, no uno propio del comprobante. La unica
identificacion del comprobante es la numeracion de catorce caracteres del 11.1.

Comprobado contra la base: emitir un comprobante **no mueve** `FED_NUM_CONTROL`
ni `FED_DOCUMENTO`. Solo crece `FED_RETENCION`.

Pero **la imprenta digital interviene igual**: el numeral 11.9 exige sus datos.
Aporta identidad sin aportar numeracion, y es el unico documento del alcance con
esa forma (`D-39`).

Segunda cosa que sorprende: **el comprobante no cuelga de un documento del
sistema**. El agente de retencion retiene las facturas que le emiten **sus
proveedores**, y esos proveedores no son emisores de esta imprenta: sus facturas
no viven en `FED_DOCUMENTO`. Por eso el desglose se escribe, no se referencia, y
por eso no hay `documentoId` en ninguna parte del request.

## retencionCreate

```http
POST /api/FacturacionElectronica/retencionCreate
```

### Request

```json
{
  "emisorId": 9,
  "periodo": "202609",
  "proveedorRif": "J-30111222-3",
  "proveedorRazonSocial": "SUMINISTROS DEMO, C.A.",
  "proveedorDomicilio": "Caracas",
  "proveedorCorreo": "pagos@demo.com",
  "usuarioIns": "avanessa",
  "claveIdempotencia": "ui-ret-1788210656-a4f2c1",
  "documentos": [
    {
      "documentoNumero": "00012345",
      "documentoControl": "00-00000777",
      "documentoFecha": "2026-09-01",
      "montoTotal": 1160,
      "baseImponible": 1000,
      "impuestoCausado": 160,
      "montoRetenido": 120,
      "porcentaje": 75
    }
  ]
}
```

| Campo | Obligatorio | Nota |
|---|---|---|
| `emisorId` | **si** | El **agente de retencion**. De el salen nombre, RIF y domicilio del 11.2 |
| `periodo` | **si** | `AAAAMM`. Art. 11.7. De aca sale el prefijo de la numeracion |
| `proveedorRif` | **si** | Art. 11.4 |
| `proveedorRazonSocial` | **si** | Art. 11.4 |
| `proveedorCorreo` | **si** | Art. 11.4. **El numeral lo nombra expresamente**, asi que no es opcional |
| `proveedorDomicilio` | no | Art. 11.4 |
| `documentos` | **si**, al menos uno | Arts. 11.5, 11.6 y 11.8. Un comprobante sin documentos no retiene nada |
| `claveIdempotencia` | **no dejarlo vacio** | Sin ella un doble clic emite dos comprobantes por un solo hecho |

**Los totales de la cabecera no se envian.** El backend los suma del desglose.
Pedirlos seria dejar que quien llama declare un total que no cierra contra sus
propias lineas.

Cada elemento de `documentos`:

| Campo | Numeral | Nota |
|---|---|---|
| `documentoNumero` | 11.6 | Numero de la factura o nota de debito retenida |
| `documentoControl` | 11.5 | Su numero de **control**, el de la factura del proveedor |
| `documentoFecha` | - | No lo pide un numeral con ese nombre, pero sin el el periodo del 11.7 no se sustenta |
| `montoTotal`, `baseImponible`, `impuestoCausado`, `montoRetenido` | 11.8 | Los cuatro montos, por documento |
| `porcentaje` | - | No lo pide el Art. 11. Sin el no se puede auditar como se llego al monto retenido (Art. 18.2) |

### Response - exito

```json
{
  "data": {
    "retencionId": 24,
    "numeracion": "20260900000001",
    "periodo": "202609",
    "fechaEmision8d": "07092026",
    "horaEmision": "07.04.29 p.m.",
    "agenteRif": "J-66666666-6",
    "agenteRazonSocial": "ALCALDIA DE PRUEBA",
    "proveedorRif": "J-30111222-3",
    "proveedorRazonSocial": "SUMINISTROS DEMO, C.A.",
    "totalDocumentos": 1740,
    "totalBase": 1500,
    "totalImpuesto": 240,
    "totalRetenido": 180,
    "cantidadDocumentos": 2,
    "esPrueba": true,
    "motivoPrueba": "Sin providencia de autorizacion del SENIAT...",
    "yaExistia": false
  },
  "isValid": true,
  "message": "suscces"
}
```

**No busque `numeroControl` en esta respuesta.** No esta, y arriba esta explicado
por que.

`numeracion` son **catorce caracteres**: `AAAAMMSSSSSSSS`. El secuencial
**reinicia cada mes** -el primero de octubre vuelve a `00000001` con el prefijo
`202610`-. Es una interpretacion del 11.1 y esta declarada como tal en `D-38`.

### Response - la misma clave llega dos veces

`yaExistia: true` y **el mismo `retencionId`**. No se emite un comprobante nuevo.

### Response - el documento no cumple el Art. 11

```json
{
  "data": null,
  "isValid": false,
  "message": "El documento no cumple el Articulo 11: 11.4: falta el correo electronico del proveedor, que el numeral nombra expresamente."
}
```

El mensaje **cita el numeral**, igual que el validador de la factura cita los del
Art. 7. Los rechazos verificados: sin correo (11.4), sin RIF (11.4), periodo mal
formado (11.7), mes inexistente (11.7), sin documentos (11.6), retener mas que el
impuesto causado (11.8) y documento sin su numero de control (11.5).

## retencionGetAll

```http
POST /api/FacturacionElectronica/retencionGetAll
```

### Request

```json
{ "emisorId": 0, "periodo": "", "pageSize": 10, "pageNumber": 1 }
```

`emisorId` en 0 son todos los agentes; `periodo` vacio, todos los periodos. El
periodo va en `AAAAMM`.

### Response

Lista de comprobantes, sin `numeroControl` por lo ya dicho. Trae ademas
`periodoFormato` -`09/2026`, para mostrar- y `cantidadDocumentos`.

En el `ResultDto`:

| Campo | Que es |
|---|---|
| `total1` | Cuantos de esta pagina son de **prueba** |
| `total2` | Lo **retenido** en la pagina |

## Lo que el comprobante viejo del ERP no cumple

`Features/ReporteComprobanteIva/` ya emite un comprobante de retencion hoy, y
**no lo reemplaza automaticamente**. Contrastado numeral por numeral, cumple el
11.2, el 11.5, el 11.6 y el 11.8; **no cumple** el 11.1 -su
`ADM_ORDEN_PAGO.NUMERO_COMPROBANTE` es un `decimal`, un numero corrido sin
prefijo `AAAAMM`-, el 11.3 -tiene fecha de emision pero no de **entrega**-, el
11.4 -el proveedor va sin domicilio y sin correo- y el 11.9, que no podia cumplir
porque hasta ahora no habia imprenta.

Los dos documentos conviven. Migrar al cliente de uno al otro es un tema de la
Fase 9, no de esta.


---

# Fase 6 - Guia de despacho (Art. 10)

## Lo PRIMERO que hay que entender antes de integrarlo

**Este documento no lleva montos. Ninguno.** Ni precio por renglon, ni ajustes,
ni base imponible, ni IVA, ni valor total.

El Art. 10.2 remite a los numerales **2, 3, 4, 5, 6 y 14** del Art. 7, y a
ninguno mas. El precio vive en el 7.8; la base, el IVA y el total viven en el
7.11, 7.12 y 7.13. Ninguno esta remitido. El numeral 10.4 **no completa** al 7.8:
lo reemplaza por *"la descripcion de los bienes que se trasladan, senalando su
capacidad, peso o volumen"*.

Asi que una guia con montos no es un documento incompleto: es un documento que
dice algo que la norma no admite que diga. `guiaCreate` la **rechaza** en vez de
poner ceros en silencio.

Segunda cosa: **no se llama "nota de entrega"**. El Art. 10.1 admite `orden de
entrega` o `guia de despacho` y ninguna otra. El tipo en base sigue siendo
`entrega` -contrato desde la Fase 4- pero la denominacion que sale impresa es
`GUIA DE DESPACHO`.

Tercera: **si lleva numero de control**, a diferencia del comprobante de
retencion, porque el 10.2 remite a los numerales 4 y 5 expresamente.

## `facturaCreate` ya NO acepta el tipo `entrega`

Cambio de contrato de esta fase. La razon es la misma que dejo fuera a las notas
en la Fase 5: el Art. 10 le exige tres datos que la emision directa no sabe pedir
-el motivo del traslado, el receptor **con RIF** por el 10.5, y la medida por
renglon del 10.4- y le prohibe cuatro que si sabe mandar.

El rechazo cita el Art. 10 y dice a donde ir:

```json
{
  "data": null,
  "isValid": false,
  "message": "El documento no cumple el Articulo 10: una guia de despacho se emite por guiaCreate, que exige el motivo del traslado, el RIF del receptor (Art. 10.5) y la capacidad, peso o volumen de cada bien (Art. 10.4), y que no admite precio ni IVA porque el Art. 10.2 no remite a los numerales 8, 11, 12 ni 13 del Articulo 7."
}
```

## guiaCreate

```http
POST /api/FacturacionElectronica/guiaCreate
```

### Request

```json
{
  "emisorId": 16,
  "motivoTraslado": "Traslado a taller de mantenimiento. No representa venta.",
  "destino": "Taller Municipal, Av. Bolivar",
  "receptorNombre": "TALLER MUNICIPAL DE PRUEBA, C.A.",
  "receptorRif": "J-31200500-7",
  "serie": "",
  "usuarioIns": "avanessa",
  "claveIdempotencia": "ui-guia-1788210656-a4f2c1",
  "renglones": [
    {
      "descripcion": "Motobomba centrifuga 3 HP",
      "cantidad": 3,
      "medidaTipo": "peso",
      "medidaValor": 62.5,
      "medidaUnidad": "kg"
    }
  ]
}
```

| Campo | Obligatorio | Nota |
|---|---|---|
| `emisorId` | **si** | Sus datos van al documento por el Art. 7.3, remitido por el 10.2 |
| `motivoTraslado` | **si** | **No sale de un numeral.** Lo pide el encabezado del Art. 10, que solo admite este documento para traslados que **no representen ventas** |
| `receptorNombre` | **si** | Art. 10.5 |
| `receptorRif` | **si** | Art. 10.5. **No admite cedula ni pasaporte en su lugar**, a diferencia del 7.7 para el adquiriente |
| `destino` | no | Ningun numeral lo pide. Se guarda porque el Art. 18.2 exige poder auditar |
| `renglones` | **si**, al menos uno | Art. 10.4 |
| `claveIdempotencia` | **no dejarlo vacio** | Sin ella, un doble clic ampara el mismo traslado con dos guias |

**No hay campo de precio, alicuota ni exento**, y no es que se ignoren: no
existen en el request.

Cada renglon:

| Campo | Numeral | Nota |
|---|---|---|
| `descripcion` | 10.4 | Que bien se traslada |
| `cantidad` | 10.4 | Cuantos |
| `medidaTipo` | 10.4 | `capacidad`, `peso` o `volumen`. **Solo esos tres**: son los que el numeral nombra |
| `medidaValor` | 10.4 | Mayor que cero |
| `medidaUnidad` | 10.4 | `kg`, `litros`, `m3`, la que corresponda |

Los tres campos de medida son opcionales **en el enlazado** y obligatorios **en
la validacion**, y la diferencia es a proposito: si fueran obligatorios en el
enlazado, un renglon sin medida lo rechazaria ASP.NET con un RFC 9110 en vez del
mensaje que cita el numeral.

### Response - exito

```json
{
  "data": {
    "documentoId": 67,
    "numeracion": "2",
    "numeracionConSerie": "2",
    "numeroControl": "00-00000002",
    "numeroControlTexto": "N. de Control 00-00000002",
    "rangoNumerosControl": "desde el N. 00-00000002 hasta el N. 00-00000002",
    "denominacion": "GUIA DE DESPACHO",
    "fechaEmision8d": "08092026",
    "horaEmision": "01.22.41 a.m.",
    "fechaAsignacion8d": "08092026",
    "receptorNombre": "TALLER MUNICIPAL DE PRUEBA, C.A.",
    "receptorRif": "J-31200500-7",
    "motivoTraslado": "Traslado a taller de mantenimiento. No representa venta.",
    "destino": "Taller Municipal, Av. Bolivar",
    "cantidadRenglones": 1,
    "leyendaSinCreditoFiscal": "sin derecho a Credito Fiscal",
    "leyendaProvidencia": "Emitida conforme a lo dispuesto en la Providencia Administrativa SNAT/2024/000102",
    "esPrueba": true,
    "motivoPrueba": "...",
    "yaExistia": false
  },
  "isValid": true,
  "message": "suscces"
}
```

**No busque `totalBase`, `totalIva` ni `totalGeneral`.** No estan, y arriba esta
explicado por que. En la base quedan en cero y `FED_DOC_IMPUESTO` no recibe una
sola fila: escribir filas en cero seria afirmar que el documento discrimina un
impuesto que no causa.

`leyendaSinCreditoFiscal` llega armada del backend. Es el literal del Art. 10.3 y
la pantalla no lo reescribe, igual que no reescribe la denominacion.

### Response - la misma clave llega dos veces

`yaExistia: true` y **el mismo `documentoId`**, con su motivo y su destino.

### Response - el documento no cumple el Art. 10

El mensaje cita el numeral. Los rechazos verificados: sin motivo del traslado
-que cita el **encabezado** del articulo y no un numeral, por eso el mensaje no
lleva prefijo-, sin RIF del receptor (10.5), sin nombre del receptor (10.5), sin
bienes (10.4), sin medida (10.4), medida que la norma no nombra (10.4), medida
sin valor (10.4), medida sin unidad (10.4) y bien sin descripcion (10.4).
