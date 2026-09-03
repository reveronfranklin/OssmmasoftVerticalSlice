# Contrato Frontend - FacturacionElectronica

Requerimiento 32. Modulo de facturacion electronica sobre PostgreSQL (schema `FED`).

**Estado: Fase 1.** Endpoint de salud y CRUD de emisores. Los numeros de control y los
documentos llegan en las fases siguientes.

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
- **Todavia no hay entrada en el menu lateral.** Se llega por URL directa hasta que el
  modulo tenga algo que mostrar; el item entra al cerrar la Fase 1.
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
