# Contrato Frontend - FacturacionElectronica

Requerimiento 32. Modulo de facturacion electronica sobre PostgreSQL (schema `FED`).

**Estado: Fase 0.** Solo existe el endpoint de salud. Los endpoints de negocio -emisores,
numeros de control, documentos- llegan en las fases siguientes.

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
