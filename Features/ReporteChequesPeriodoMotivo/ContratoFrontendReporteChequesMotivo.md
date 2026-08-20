# Contrato Frontend - ReporteChequesMotivo

Fecha: 2026-08-20. Requerimiento 23.

Migracion del reporte Oracle Reports `ADM_PERIODOS_CHEQUES_MOTIVO1.rdf`
("Relacion de Cheques Emitidos Por Periodos, con Motivo").

## Como se consume normalmente: por el Motor de Formularios

**Este reporte no necesita pantalla propia.** El PLAN.md original del
requerimiento 23 preveia una pantalla de filtros codificada a mano y dejaba
abierta la pregunta de en que modulo vivia el boton -no existe `src/adm/cheques`-.
El formulario de parametros vive en el Motor de Formularios (requerimiento 16) y
se abre en la pantalla generica:

```txt
/apps/mfo/reporte/REP_CHEQ_MOTIVO
```

Campos del formulario:

| Campo | Tipo | Obligatorio |
| --- | --- | --- |
| `FECHA_DESDE` | Fecha | Si |
| `FECHA_HASTA` | Fecha | Si |
| `BANCO` | Catalogo `SIS_BANCO_NOMBRE` | No |
| `CUENTA` | Catalogo `SIS_CUENTA_BANCO` | No |
| `STATUS` | Lista AP/AN | No |
| `PROVEEDOR` | Numero | No |

`CodigoEmpresa` y `Usuario` son parametros de origen `SISTEMA`: los resuelve el
servidor y un valor enviado en el payload se descarta sin mirarlo.

El PDF vuelve por el endpoint de ejecucion del motor
(`POST api/MfoReporte/ejecutar`), ya cableado al visor existente. No hay que
agregar servicio, slice ni ruta en el frontend.

**Los dos catalogos son nuevos** (`MfoCatalogoRegistro.cs`) y devuelven el
**nombre del banco** y el **numero de cuenta** como valor, no el codigo, porque es
contra esas columnas que filtra el reporte.

## Endpoint directo (opcional)

```http
POST /api/ReporteChequesMotivo/pdf
```

### Request

```json
{
  "fechaDesde": "2026-07-16",
  "fechaHasta": "2026-07-30",
  "nombreBanco": "BANESCO",
  "numeroCuenta": "01340031800311163500",
  "status": "AP",
  "codigoProveedor": 4210,
  "usuario": "jperez"
}
```

| Campo | Obligatorio | Notas |
| --- | --- | --- |
| `fechaDesde` | Si | Primer dia del periodo, inclusive. |
| `fechaHasta` | Si | Ultimo dia, inclusive. La hora de `FECHA_CHEQUE` no excluye el ultimo dia. |
| `nombreBanco` | No | Igualdad exacta contra `SIS_BANCOS.NOMBRE`. |
| `numeroCuenta` | No | Igualdad exacta contra `SIS_CUENTAS_BANCOS.NO_CUENTA`. |
| `status` | No | `AP` o `AN`. Vacio o nulo = ambos. Otro valor devuelve `IsValid = false`. |
| `codigoProveedor` | No | Cero o nulo = todos. |
| `usuario` | Si | Usuario conectado, para el pie de auditoria. Vacio devuelve HTTP `400`. |

No lleva `codigoEmpresa`: se resuelve desde `settings:EmpresaConfig`.

**`status` y `codigoProveedor` se combinan.** En el reporte legado eran
mutuamente excluyentes por un `ELSIF` en `AfterPForm`: informar los dos aplicaba
solo el estatus y **descartaba el filtro de proveedor en silencio**. Aqui los dos
son independientes y se aplican con AND.

### Response exitoso

- HTTP `200`
- `Content-Type: application/pdf`
- `Content-Disposition: inline; filename="relacion-cheques-motivo.pdf"`

### Response con error o sin datos

HTTP `200` con `ResultDto<T>` de `IsValid = false` y el mensaje en `Message`,
mismo patron que `ReporteBm1/pdf`:

- `"Indique la fecha desde y la fecha hasta del periodo."`
- `"La fecha desde no puede ser posterior a la fecha hasta."`
- `"El status debe ser AP (aprobado) o AN (anulado)."`
- `"No hay cheques emitidos en el periodo seleccionado."`

El unico HTTP `400` es el de `usuario` vacio.

## Endpoint de datos

```http
POST /api/ReporteChequesMotivo/GetAll
```

Mismo request (sin `usuario`), devuelve `ResultDto<List<Grupo>>` ya agrupado por
banco/cuenta y con los subtotales calculados.

```json
{
  "data": [
    {
      "nombreBanco": "BANESCO",
      "numeroCuenta": "01340031800311163500",
      "cantidadValidos": 15,
      "montoValidos": 120805879.24,
      "cantidadAnulados": 0,
      "montoAnulados": 0,
      "items": [
        {
          "fechaCheque": "2026-07-21T00:00:00",
          "numeroDocumento": "PAEL 10025",
          "status": "AP",
          "estatusDescripcion": "APRO",
          "beneficiario": "SEGUROS CONSTITUCION, C.A.",
          "monto": 60268456.0,
          "motivo": "PAGO CORRESPONDIENTE A ... N OP: 372 FECHA OP: 21/07/26\n(01-02-02-00-53 / 4.07.01.06.17.00)"
        }
      ]
    }
  ],
  "isValid": true,
  "cantidadRegistros": 46,
  "total1": 208234142.39,
  "total2": 74240000.0
}
```

`cantidadRegistros` es el numero de cheques (filas de detalle), no de grupos.
`total1` es el monto total de cheques validos y `total2` el de anulados: son los
dos numeros de la columna TOTAL GENERAL del PDF.

### Tres cosas que conviene entender

- **`monto` viene negativo cuando el cheque esta anulado**, como en el reporte
  legado y en el PDF de muestra. Los subtotales `montoAnulados` en cambio son
  **positivos**: es lo que hacia la formula `CF_1`.
- **`motivo` es un parrafo multilinea.** Trae el motivo del cheque, la referencia
  a la orden de pago y las partidas presupuestarias imputadas, con saltos de
  linea reales (el SP los emite como `CHR(13)` y el backend los normaliza).
  Renderizarlo en una grilla exige respetar `\n` o el texto sale pegado.
- **La lista de partidas puede venir vacia sin que sea un error.**
  `ADM_F_GET_PARTIDAS_CHEQUE` acumula en un `VARCHAR2(500)` y captura
  `WHEN OTHERS` devolviendo NULL, asi que un cheque con mas de una docena de
  partidas pierde la lista completa. Es una limitacion heredada del schema ADM
  que no se toco; ver el PLAN.md del requerimiento 23.
