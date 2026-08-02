# Contrato Frontend - PrePresupuesto

## RUTA DEL FRONT

/Users/freveron/Developer/Projects/MM/NextOssmasoft

## Base

```http
Content-Type: application/json
```

Cliente Axios: `ossmmasofApiVertical` (`src/MyApis/ossmmasofApiVertical.ts`).

## GetListPresupuesto

```http
GET api/PrePresupuesto/GetListPresupuesto
```

Sin parametros y sin body. Devuelve **todos** los presupuestos, sin paginacion
ni filtros. Varias vistas dependen de recibir la lista completa para llenar sus
combos.

Orden: `PRE_PRESUPUESTOS.FECHA_HASTA` descendente. El orden es parte del
contrato: varias vistas toman `listpresupuestoDto[0]` como presupuesto vigente y
lo seleccionan por defecto.

### Respuesta

```json
{
  "data": [
    {
      "codigoPresupuesto": 20,
      "descripcion": "PRESUPUESTO AÑO 2026",
      "ano": 2026,
      "presupuestoEnEjecucion": true,
      "preFinanciadoDto": [
        { "financiadoId": 92, "descripcionFinanciado": "ORDINARIO" },
        { "financiadoId": 288, "descripcionFinanciado": "TRASPASO PRESUPUESTARIO" }
      ]
    }
  ],
  "isValid": true,
  "message": ""
}
```

### Diferencias contra el endpoint legacy

Este endpoint reemplaza a
`https://ossmmasoft.com.ve:5001/api/PrePresupuesto/GetListPresupuesto` del
proyecto `Ossmmsoft_convertidor-main`. Hay dos diferencias de contrato que el
frontend debe absorber:

1. **Envoltura `ResultDto`.** El legacy devolvia el arreglo pelado con
   `Ok(result.Data)`. Aqui el arreglo viene en `data`, acompanado de `isValid` y
   `message`. El frontend debe leer `response.data.data` y validar
   `response.data.isValid`.
2. **`preFinanciadoDto` nunca es `null`.** El legacy dejaba la propiedad en
   `null` cuando el presupuesto no tenia financiados, porque solo la asignaba si
   `Count > 0`. Aqui siempre es un arreglo, vacio cuando no hay financiados.
   Consumir la lista con `filter` o `map` es seguro sin guarda previa.

El resto de la forma es identico: mismos nombres de campo en camelCase, mismos
tipos y mismo orden.

### Validacion y errores

- Cero presupuestos es exito: `isValid: true` con `data: []`. No es un error.
  El legacy en ese caso devolvia `IsValid = false`, lo que impedia distinguir
  "no hay presupuestos" de "fallo la consulta".
- Fallo al abrir la conexion PRE: `isValid: false` con
  `Error técnico al abrir conexión PRE: ...`.
- Fallo del procedimiento o de la lectura: `isValid: false` con
  `Error tecnico: ...`.
- En cualquier caso de error `data` viene en `null`, por lo que el frontend debe
  validar `isValid` antes de usarla.

## Stored procedures

| Procedimiento                                            | Proposito                                                       |
| -------------------------------------------------------- | --------------------------------------------------------------- |
| [SP_PRE_PRESUP_LIST_GET.sql](SP_PRE_PRESUP_LIST_GET.sql) | Presupuestos con `PRESUPUESTO_EN_EJECUCION` resuelto en la base |
| [SP_PRE_PRESUP_FIN_GET.sql](SP_PRE_PRESUP_FIN_GET.sql)   | Financiados de todos los presupuestos en una sola consulta      |

El handler ejecuta los dos sobre una unica conexion y agrupa los financiados por
presupuesto en memoria. Son dos consultas fijas, independientes de la cantidad
de presupuestos. El backend legacy hacia `1 + 2N` consultas en serie.

## Pendiente

Los demas endpoints de `PrePresupuestoController` siguen en el backend legacy:
`GetList`, `GetAll`, `GetAllFilter`, `Create`, `Update`, `Delete`,
`AprobarPresupuesto`, `GetAllPresupuestoEntity` y `GetByCode`. Ver
`Requerimientos/20 - Migrar PrePresupuesto A VerticalSlice/PLAN.md`, fase 6.
