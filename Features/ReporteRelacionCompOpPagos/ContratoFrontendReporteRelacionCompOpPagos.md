# Contrato del reporte de compromisos con orden de pago y cheques

Requerimiento 17. La entrada de negocio tiene tres campos: proveedores opcionales, fecha desde y fecha hasta obligatorias.

## Consulta de datos

POST `/api/ReporteRelacionCompOpPagos/GetAll`

```json
{
  "fechaDesde": "2026-01-01",
  "fechaHasta": "2026-07-31",
  "codigosProveedores": [7, 12]
}
```

Para consultar todos los proveedores, omitir `codigosProveedores`, enviarlo como `null` o como `[]`. Los tres casos tienen el mismo comportamiento. Los codigos deben ser enteros positivos; los repetidos se normalizan. La lista normalizada admite hasta 32767 caracteres en su representacion CSV interna; no se usa SQL dinamico.

Ambas fechas son obligatorias y `fechaDesde <= fechaHasta`. Se consultan dias completos de FECHA_COMPROMISO, sin limitar fechas futuras. Las fechas de orden y cheque no reciben un filtro adicional: se muestran los hijos del compromiso seleccionado.

La empresa se obtiene de `settings:EmpresaConfig`. El presupuesto es el de cada compromiso: se incluyen los presupuestos de la empresa que aporten compromisos dentro de las fechas, incluso si el rango cruza ejercicios. No se requiere presupuesto ni PUC en la entrada.

Ejemplo resumido de respuesta con una partida, una orden y un cheque:

```json
{
  "data": {
    "items": [{
      "clave": { "codigoPresupuesto": 26, "codigoCompromiso": 12, "codigoIcp": 3, "codigoPuc": 4 },
      "numeroCompromiso": "CMC-CMP-00012",
      "fechaCompromiso": "2026-01-13T00:00:00",
      "codigoProveedor": 7,
      "nombreProveedor": "Proveedor de ejemplo",
      "cedRif": "J-123456 / J123456",
      "motivo": "Concepto del compromiso",
      "partida": "01-02-02-00-53 / 4.01.04.47.00.00",
      "montoCompromiso": 100,
      "entidad": "Entidad del presupuesto",
      "ordenes": [{
        "clave": {
          "compromiso": { "codigoPresupuesto": 26, "codigoCompromiso": 12, "codigoIcp": 3, "codigoPuc": 4 },
          "codigoCompromisoOp": 1,
          "codigoOrdenPago": 100
        },
        "numeroOrdenPago": "OP-100",
        "fechaOrdenPago": "2026-02-01T00:00:00",
        "montoOrdenPago": 60,
        "pagos": [{
          "orden": {
            "compromiso": { "codigoPresupuesto": 26, "codigoCompromiso": 12, "codigoIcp": 3, "codigoPuc": 4 },
            "codigoCompromisoOp": 1,
            "codigoOrdenPago": 100
          },
          "codigoCheque": 5,
          "numeroCheque": "555",
          "fechaCheque": "2026-03-01T00:00:00",
          "montoCheque": 50
        }]
      }]
    }],
    "cantidadCompromisos": 1,
    "cantidadFilas": 3,
    "totalComprometido": 100,
    "totalOrdenado": 60,
    "totalPagado": 50
  },
  "isValid": true,
  "message": "",
  "page": 0,
  "totalPage": 0,
  "cantidadRegistros": 1,
  "total1": 100,
  "total2": 60,
  "total3": 50,
  "total4": 0
}
```

Cada item representa un compromiso/ICP/PUC dentro de un presupuesto. `cantidadRegistros` cuenta esos items; `cantidadCompromisos` cuenta las parejas presupuesto/compromiso distintas y `cantidadFilas` suma items, grupos de orden y grupos de cheque. No hay paginacion de negocio: `page` y `totalPage` son 0. Las propiedades comunes adicionales de ResultDto pueden venir nulas.

Los montos de orden y cheque representan imputaciones a la partida/compromiso, no el total de cabecera del documento. Un mismo documento puede aparecer bajo varias partidas con importes distintos. No deduplicar por numero o importe. Los compromisos sin orden y las ordenes sin cheque conservan colecciones vacias.

Sin datos: `isValid: true`, `data.items: []` y totales en cero. Errores esperados de validacion, configuracion o base: HTTP 200 con `isValid: false`, `data: null` y `message` visible. Un cuerpo JSON mal formado o tipos incompatibles puede producir HTTP 400 por ApiController.

## PDF

POST `/api/ReporteRelacionCompOpPagos/pdf`, mismo cuerpo. El usuario para el pie se obtiene de `User.Identity.Name` cuando esta disponible; en el acceso directo se admite el campo tecnico `usuario`, siguiendo las convenciones de reportes existentes. En el motor se obtiene del parametro SISTEMA y no es un campo editable.

Exito: `Content-Type: application/pdf`, `Content-Disposition: inline; filename="relacion-compromisos-op-pagos.pdf"`. Fallo o sin datos: JSON ResultDto con `isValid: false` y mensaje. Antes de mostrar un Blob comprobar Content-Type y procesar el JSON de error.

El PDF distingue total comprometido, total ordenado y total pagado; incluye el numero de filas por partida y la cantidad de compromisos unicos. La tabla repite cabeceras de dos niveles y permite continuar motivos largos entre paginas. El diseno reproduce las proporciones de la muestra: compromiso (numero/fecha), identificacion/proveedor/motivo/partidas, monto comprometido, orden de pago (numero/fecha/monto) y cheque (numero/fecha/monto). Incluye el logo institucional `Assets/Reports/logoLeft.jpeg`, marcos redondeados y cuadricula vertical completa. El pie identifica la forma como Ossmmasoft y conserva usuario, fecha/hora y pagina. Cuando no existen hijos, sus celdas e importes de cierre quedan vacios como en la muestra.

## Motor de Formularios

- Alias: `REP_REL_COP`.
- Ejecutor: `REPORTE_REL_COP_PDF` (ENDPOINT).
- Catalogo: `ADM_PROVEEDORES_COP`, proveedores de la empresa, etiqueta con codigo, nombre y RIF.
- Campo `PROVEEDORES`: MULTI_SELECT, opcional, mapeado a `CodigosProveedores` NUMERO multivalor.
- Campos `FECHA_DESDE` y `FECHA_HASTA`: FECHA, requeridos.
- Parametros SISTEMA: CodigoEmpresa y Usuario.
- Ruta del visor: `/apps/mfo/reporte/REP_REL_COP`.

Usar la ejecucion y el visor integrados en EjecutarReporteView. No requiere un componente React ni un servicio Axios adicional. El motor registra OK, VACIO y ERROR. La semilla configura 10000 filas base/hijas y 240 segundos; al superar el limite devuelve ERROR para refinar filtros, sin PDF parcial ni TRUNCADO. Estos limites son iniciales y deben ajustarse con volumen real.

Se respetan los permisos de formulario y de ejecucion de reporte existentes. La semilla no concede acceso general.

## Diferencias respecto del legado

Se generaliza proveedor individual a lista opcional y se exigen ambas fechas, segun las aclaratorias del requerimiento. Se consultan todos los presupuestos que aportan compromisos del periodo para no agregar una entrada de presupuesto.

Las ramas de asignacion y modificacion del RDF exigen `A.CODIGO_PUC = :P_CODIGO_PUC`. Sin PUC de entrada no aportan filas (igualdad con NULL); no se convierten en movimientos de todos los PUC ni se mezclan en los totales del reporte por proveedores. Si se requiere un estado de asignaciones y modificaciones, necesita un contrato de filtrado adicional.

Los hijos enlazan, ademas de compromiso/ICP/PUC, mediante `ADM_PUC_ORDEN_PAGO.CODIGO_PUC_COMPROMISO` al PUC real de PRE. Esto evita relacionar por accidente un identificador de otra familia de compromiso; las imputaciones sin ese enlace deben investigarse en la validacion de datos Oracle. No se activa el filtro COMPRE comentado en el RDF.

PRE_V_SALDOS se agrupa por su clave de saldo y el plan de cuentas se comprueba con EXISTS para evitar multiplicaciones por filas descriptivas. Se normalizan montos nulos a cero. Estas decisiones deben contrastarse contra un corte comparable de Oracle antes de considerar conciliado el reporte.

El medio de pago migrado es CHEQUE, que es la fuente del RDF. No se agregan transferencias ni pagos electronicos sin una relacion presupuestaria confirmada.
