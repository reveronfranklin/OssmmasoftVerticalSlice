# Contrato Frontend - ReporteChequesPeriodo

Fecha: 2026-08-20. Requerimientos 23 y 24.

Migracion de los **dos** reportes Oracle Reports de relacion de cheques, que
comparten backend porque uno es un subconjunto del otro:

| Reporte legado | Requerimiento | Variante |
| --- | --- | --- |
| `ADM_PERIODOS_CHEQUES1.RDF` | 24 | Listado simple (`conMotivo: false`) |
| `ADM_PERIODOS_CHEQUES_MOTIVO1.rdf` | 23 | Con motivo, orden de pago y partidas (`conMotivo: true`) |

Comparten stored procedure (`ADM.SP_REP_CHEQ_PERIODO_GET`), handler y generador
de PDF. Difieren en dos cosas visibles: la columna del numero de documento
(`Nro. CHEQUE` crudo vs `Nro. DOCUMENTO` con el descriptivo del tipo) y el bloque
de motivo debajo de cada fila.

## Como se consume normalmente: por el Motor de Formularios

**Ninguno de los dos reportes necesita pantalla propia.** Los PLAN.md originales
de los requerimientos 23 y 24 preveian pantallas de filtros codificadas a mano y
dejaban abierta la pregunta de en que modulo vivia el boton -no existe
`src/adm/cheques`-. Los formularios de parametros viven en el Motor de Formularios
(requerimiento 16) y se abren en la pantalla generica:

```txt
/apps/mfo/reporte/REP_CHEQ_PERIODO    <- listado simple (requerimiento 24)
/apps/mfo/reporte/REP_CHEQ_MOTIVO     <- con motivo (requerimiento 23)
```

Campos de cada formulario:

| Campo | Tipo | Obligatorio | `REP_CHEQ_PERIODO` | `REP_CHEQ_MOTIVO` |
| --- | --- | --- | --- | --- |
| `FECHA_DESDE` | Fecha | Si | Si | Si |
| `FECHA_HASTA` | Fecha | Si | Si | Si |
| `BANCO` | Catalogo `SIS_BANCO_NOMBRE` | No | Si | Si |
| `CUENTA` | Catalogo `SIS_CUENTA_BANCO` | No | Si | Si |
| `STATUS` | Lista AP/AN | No | - | Si |
| `PROVEEDOR` | Numero | No | - | Si |

**Son dos formularios y no uno con dos reportes** -que es lo que `MFO_REPORTE`
permite y lo que hace `REP_BM1`- porque no comparten el juego de parametros: el
.rdf del requerimiento 24 no define status ni proveedor, y colgar los dos del
mismo formulario mostraria al usuario del listado simple dos filtros que su
reporte nunca tuvo.

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
POST /api/ReporteChequesPeriodo/pdf
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
  "usuario": "jperez",
  "conMotivo": true
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
| `conMotivo` | No | `true` (por omision) = variante del requerimiento 23. `false` = listado simple del 24. |

No lleva `codigoEmpresa`: se resuelve desde `settings:EmpresaConfig`.

**`status` y `codigoProveedor` se combinan.** En el reporte legado eran
mutuamente excluyentes por un `ELSIF` en `AfterPForm`: informar los dos aplicaba
solo el estatus y **descartaba el filtro de proveedor en silencio**. Aqui los dos
son independientes y se aplican con AND.

### Response exitoso

- HTTP `200`
- `Content-Type: application/pdf`
- `Content-Disposition: inline; filename="relacion-cheques-motivo.pdf"` con
  `conMotivo: true`, o `relacion-cheques.pdf` con `false`

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
POST /api/ReporteChequesPeriodo/GetAll
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
          "numeroCheque": "10025",
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

### Cuatro cosas que conviene entender

- **`numeroCheque` vs `numeroDocumento`.** El primero es el numero crudo
  (`10025`), que es lo que imprime el listado simple del requerimiento 24 en su
  columna "Nro. CHEQUE". El segundo lleva delante el descriptivo del tipo de
  cheque (`PAEL 10025`), que es la columna "Nro. DOCUMENTO" del requerimiento 23.
  Si el cheque no tiene tipo, los dos coinciden.

- **`monto` viene negativo cuando el cheque esta anulado**, como en el reporte
  legado y en el PDF de muestra. Los subtotales `montoAnulados` en cambio son
  **positivos**: es lo que hacia la formula `CF_1`.
- **`motivo` es un parrafo multilinea.** Trae el motivo del cheque, la referencia
  a la orden de pago y las partidas presupuestarias imputadas, con saltos de
  linea reales (el SP los emite como `CHR(13)` y el backend los normaliza).
  Renderizarlo en una grilla exige respetar `\n` o el texto sale pegado.
- **Con `conMotivo: false`, `motivo` viene vacio en todas las filas.** No es que
  no haya motivo: el SP no lo calcula, y a proposito, porque calcularlo implica
  recorrer `PRE_V_SALDOS` una vez por cheque.
- **La lista de partidas puede venir vacia sin que sea un error.**
  `ADM_F_GET_PARTIDAS_CHEQUE` acumula en un `VARCHAR2(500)` y captura
  `WHEN OTHERS` devolviendo NULL, asi que un cheque con mas de una docena de
  partidas pierde la lista completa. Es una limitacion heredada del schema ADM
  que no se toco; ver el PLAN.md del requerimiento 23. Solo aplica a
  `conMotivo: true`.
