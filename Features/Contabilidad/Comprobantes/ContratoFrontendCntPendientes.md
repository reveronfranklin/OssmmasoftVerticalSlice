# Contrato CNT 37: automaticos pendientes de correccion

Fecha: 2026-10-09. Implementacion local; requiere instalacion coordinada de SQL, API, frontend y convertidor. La generacion pendiente se activa por separado despues del inventario y pruebas Oracle.

## Respuesta comun

Todos los endpoints usan POST y devuelven HTTP 200 con `ResultDto<T>`: `data`, `isValid`, `message`. Las consultas paginadas conservan `page`, `totalPage`, `cantidadRegistros`. Validaciones funcionales y conflictos se expresan en `isValid=false`. La empresa procede de `settings:EmpresaConfig`; no se envia en el cuerpo. El usuario debe coincidir con la identidad autenticada.

## Generacion

`POST api/CntAutomaticos/preview`: cuerpo `{ "usuarioId": 7, "codigoPeriodo": 10, "tipoComprobanteId": 37, "origenId": 50, "fechaDesde": "2026-09-01", "fechaHasta": "2026-09-02" }`. Conserva el previo del rango sin escrituras contables. Los importes visualizados se normalizan por linea. Un rango cuadrado no garantiza que cada dia cuadre.

`POST api/CntAutomaticos/confirm`: mismo cuerpo y `observacion` opcional. Recalcula el origen por dia. No acepta lineas, totales ni estado del cliente. Requiere `contabilidad.comprobantes.generar_automatico`. Respuesta de ejemplo:

```json
{
  "isValid": true,
  "message": "Proceso automatico finalizado. Comprobantes generados: 1.",
  "data": {
    "cantidadComprobantes": 1,
    "cantidadDiasSinLineas": 0,
    "cantidadErrores": 0,
    "cantidadPendientes": 1,
    "cantidadFinalizados": 0,
    "cantidadDiasProtegidos": 1,
    "totalLineas": 2,
    "totalDebe": 100,
    "totalHaber": 90,
    "diferencia": 10,
    "comprobantes": [
      {
        "fechaComprobante": "2026-09-01T00:00:00",
        "codigoComprobante": 12345,
        "numeroComprobante": "26-09-01-001",
        "cantidadLineas": 2,
        "totalDebe": 100,
        "totalHaber": 90,
        "diferencia": 10,
        "estado": "GENERADO",
        "mensaje": "Success",
        "estadoContable": "PENDIENTE",
        "ajusteManual": false
      },
      {
        "fechaComprobante": "2026-09-02T00:00:00",
        "codigoComprobante": 12346,
        "numeroComprobante": "26-09-01-002",
        "cantidadLineas": 2,
        "totalDebe": 50,
        "totalHaber": 50,
        "diferencia": 0,
        "estado": "PROTEGIDO",
        "mensaje": "Se conserva el comprobante existente manual, historico, con ajustes o con relaciones de libro banco.",
        "estadoContable": "FINALIZADO",
        "ajusteManual": true
      }
    ]
  }
}
```

`estado` describe la ejecucion: `GENERADO`, `REEMPLAZADO`, `SIN_LINEAS`, `PROTEGIDO`, `ERROR`. `estadoContable` describe lo persistido: `PENDIENTE` o `FINALIZADO`; vacio si no existe un comprobante informado. `PROTEGIDO` no es un error y no cuenta como generado. Las cantidades y totales del rango solo suman creados/reemplazados. Ante errores diarios, `isValid=false` y **data permanece disponible**, incluyendo exitos de otros dias. No descartar `data` en ese caso.

El reemplazo usa una transaccion por dia y se revierte ante fallos o ausencia de lineas. Se conservan automaticos historicos, con ajustes o con movimientos vinculados en libro banco, y comprobantes manuales. Los duplicados existentes no se borran automaticamente. La identidad se conserva durante correccion/finalizacion; un reemplazo permitido crea una identidad nueva.

## Consulta y listado

`POST api/CntComprobantes/GetAll`:

```json
{
  "usuarioId": 7,
  "pageSize": 10,
  "pageNumber": 1,
  "searchText": "",
  "codigoPeriodo": 10,
  "origenId": null,
  "fechaDesde": "2026-09-01",
  "fechaHasta": "2026-09-30",
  "esAutomatico": true,
  "estadoContable": "PENDIENTE"
}
```

Filtros opcionales: periodo, origen, fechas, automatico y estado. Omitir estado para incluir ambos. El filtro de estado se aplica tambien al conteo y la paginacion.

`POST api/CntComprobantes/getById` y `print`: `{ "codigoComprobante": 12345, "usuarioId": 7 }`. `getDetails` conserva el mismo cuerpo. Requieren permiso de consulta. Cabecera de listado/detalle/impresion:

```json
{
  "codigoComprobante": 12345,
  "codigoPeriodo": 10,
  "periodo": "Septiembre 2026",
  "tipoComprobanteId": 37,
  "tipoComprobante": "Diario",
  "numeroComprobante": "26-09-01-001",
  "fechaComprobante": "2026-09-01T00:00:00",
  "origenId": 50,
  "origen": "Automatico",
  "observacion": "Pendiente de correccion",
  "totalDebe": 100,
  "totalHaber": 90,
  "diferencia": 10,
  "esAutomatico": true,
  "codigoEmpresa": 1,
  "estadoContable": "PENDIENTE",
  "esPendienteCorreccion": true,
  "ajusteManual": false,
  "version": 1
}
```

`print.data` sigue siendo `{ "encabezado": <cabecera>, "detalles": [...] }`. Cada detalle conserva codigo de detalle/comprobante, mayor/auxiliar y sus etiquetas, referencias 1/2/3, descripcion, monto, debe, haber y empresa. El PDF identifica los pendientes en todas sus paginas y se muestra en el visor existente.

## Guardar avances

`POST api/CntComprobantes/update`:

```json
{
  "codigoComprobante": 12345,
  "usuarioId": 7,
  "version": 1,
  "codigoPeriodo": 10,
  "tipoComprobanteId": 37,
  "fechaComprobante": "2026-09-01",
  "origenId": 50,
  "observacion": "Correccion de cuenta y referencia",
  "detalles": [
    { "codigoMayor": 100, "codigoAuxiliar": 101, "referencia1": "DOC-1", "referencia2": "", "referencia3": "", "descripcion": "Debe", "monto": -100 },
    { "codigoMayor": 200, "codigoAuxiliar": 201, "referencia1": "DOC-1", "referencia2": "", "referencia3": "", "descripcion": "Haber", "monto": 95 }
  ]
}
```

Requiere `contabilidad.comprobantes.editar` y, para automaticos, `contabilidad.comprobantes.editar_automatico`. `version` es obligatoria en las modificaciones aunque el DTO la declare opcional por compatibilidad de deserializacion. La version se obtiene del ultimo `getById`; no se debe inventar ni actualizar ante un conflicto.

Respuesta conserva el entero existente: `{ "data": 12345, "isValid": true, "message": "Cambios guardados. El comprobante permanece pendiente de finalizacion." }`. Volver a consultar cabecera y detalles despues de guardar para obtener version y totales reales.

Solo un automatico pendiente admite guardar con diferencia. Guardar cuadrado mantiene `PENDIENTE`. Cada guardado incrementa version y protege de regeneracion con `ajusteManual=true`. No admite vaciar todos los detalles ni importes que redondeen a cero ni cuentas de otra empresa o auxiliar que no pertenezca al mayor. Empresa, periodo, tipo, fecha, numero y origen automatico permanecen protegidos. La observacion permite registrar el motivo de la correccion.

Los manuales y finalizados deben permanecer cuadrados. Un comprobante con movimientos relacionados en libro banco no se modifica ni elimina desde este flujo hasta resolver esas relaciones. No se incorpora reapertura de finalizados.

### Endpoints de detalle y eliminacion

- `POST api/CntComprobantes/addDetail`: `{ "codigoComprobante":12345, "usuarioId":7, "version":2, "codigoMayor":100, "codigoAuxiliar":101, "referencia1":"DOC-1", "referencia2":"", "referencia3":"", "descripcion":"Ajuste", "monto":-5 }`.
- `POST api/CntComprobantes/updateDetail`: mismo detalle, reemplazar `codigoComprobante` por `codigoDetalleComprobante`.
- `POST api/CntComprobantes/deleteDetail`: `{ "codigoDetalleComprobante":54321, "usuarioId":7, "version":2 }`.
- `POST api/CntComprobantes/delete`: `{ "codigoComprobante":12345, "usuarioId":7, "version":2 }`, con permiso habitual de eliminacion y permiso especial si es automatico.

Add/update devuelven `ResultDto<int>` con codigo del detalle. DeleteDetail/delete conservan `ResultDto<string>`. Todas las escrituras consultan estado, empresa, periodo, version y permisos; no permiten desbalancear un finalizado ni dejar un comprobante sin detalles. Los detalles de un pendiente incrementan la version de su cabecera. La eliminacion queda auditada antes de borrar. Un conflicto devuelve `isValid=false`; conservar los cambios locales y ofrecer recarga explicita.

`reorderNumbers` conserva su cuerpo anterior, pero rechaza un tipo/periodo que contenga automaticos para preservar sus numeros. Reordenar manuales incrementa sus versiones.

## Finalizar

`POST api/CntComprobantes/finalizar`: `{ "codigoComprobante":12345, "usuarioId":7, "version":3 }`. Requiere ambos permisos de edicion. El servidor valida los importes persistidos, cuentas, periodo abierto y version bajo el mismo bloqueo que guardado y cierre.

```json
{
  "data": { "codigoComprobante": 12345, "estadoContable": "FINALIZADO", "diferencia": 0, "version": 4 },
  "isValid": true,
  "message": "Comprobante finalizado correctamente."
}
```

Una repeticion del resultado ya finalizado con la version actual o la version de la transicion no vuelve a incrementar la version ni a producir efectos contables; se mantiene la comprobacion de periodo abierto. La contabilizacion sigue el mecanismo existente de precierre/cierre: finalizar invalida el precierre y el movimiento entra en su siguiente reconstruccion, sin insertar saldos incrementales adicionales.

Finalizar descuadrado, sin detalles, con cuentas invalidas, periodo cerrado o version obsoleta devuelve `isValid=false` sin persistir cambios. Ejemplo: `{ "data": null, "isValid": false, "message": "ORA-20001: El comprobante no esta cuadrado. ..." }`; Oracle puede incluir informacion de la pila. No depender del texto exacto para decidir exito.

## Precision, reportes y cierre

Cada monto se redondea a dos decimales, empate alejandose de cero: `0.004 -> 0`, `0.005 -> 0.01`, `-0.005 -> -0.01`. Se suman los montos normalizados y se exige diferencia exactamente cero. `monto<0` corresponde a Debe en este slice y `monto>0` a Haber. No usar tolerancia de `0.01` para finalizar.

Pendientes excluidos de mayor analitico, movimientos de auxiliar, generacion de libro banco y acumulados de precierre/cierre. La consulta de periodos de cierre incorpora `cantidadPendientes`; la pantalla enlaza al listado filtrado de comprobantes. El cierre, incluido el cierre directo desde catalogo, rechaza cualquier pendiente aun si esta cuadrado o las diferencias de varios dias se compensan. Precierre permite revisar solo finalizados; guardar/finalizar/generar/eliminar invalida el precierre anterior.
