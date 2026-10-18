# Estados de cuenta - bancos, cuentas y busqueda

Requerimiento 44. Fecha: 2026-10-18.
Estado: implementado en codigo; instalacion Oracle y validacion integral pendientes.
Pantalla: `/apps/cnt/conciliacion/estados-cuenta/`.

## Catalogos y autorizacion

Cliente `ossmmasofApiVertical`. Operaciones POST JSON autenticadas; se conserva
`contabilidad.conciliacion.ver` y la validacion de usuario del modulo. Empresa
siempre desde configuracion del servidor, sin recibirla del frontend.

Banco: `/api/CntBancos/GetAll`:

```json
{ "usuarioId": 7, "searchText": "" }
```

Respuesta ilustrativa:

```json
{
  "isValid": true,
  "message": "OK",
  "data": [{ "codigoBanco": 1, "nombre": "BANESCO", "codigoInterbancario": "0134", "codigoEmpresa": 1 }]
}
```

Cuenta: `/api/CntCuentasBanco/GetAll`:

```json
{ "usuarioId": 7, "codigoBanco": 1, "soloConfiguradas": false, "searchText": "" }
```

Con codigoBanco null devuelve las cuentas de todos los bancos de la empresa.
Respuesta abreviada (se conservan los campos existentes del DTO):

```json
{
  "isValid": true,
  "message": "OK",
  "data": [{
    "codigoCuentaBanco": 11,
    "codigoBanco": 1,
    "banco": "BANESCO",
    "noCuenta": "001234567890",
    "codigo": "A-01",
    "denominacionFuncional": "Fondos municipales",
    "codigoMayor": null,
    "codigoAuxiliar": null,
    "codigoEmpresa": 1
  }]
}
```

Los IDs son ejemplos. El DTO incluye tambien tipoCuentaId, formatoMascara,
denominacionFuncionalId, principal, recaudadora, mayor, auxiliar y searchText.
Sin datos: arreglo vacio con isValid true. Error: isValid false, message explicativo
 y data null; el servicio lo expone como error, no como lista vacia.

No se agrega paginacion a estos catalogos. Los procedimientos actuales devuelven
las opciones completas de la empresa/banco, sin limitarse a la pagina del DataGrid.
La pantalla las filtra localmente; no envia una solicitud por cada pulsacion.
Los parametros searchText de las APIs siguen disponibles para otros consumidores.

## Busqueda y seleccion

Banco usa nombre/codigo interbancario; Cuenta usa numero, banco, codigo y
 denominacion funcional. La busqueda local ignora mayusculas, acentos y espacios
extremos. Para cuentas tambien ignora espacios y guiones al comparar, conservando
el numero original, sus ceros y su identificador. No se habilita texto libre.

Todos/Todas son opciones de interfaz con ID 0; al elegirlas o limpiar el control,
se envia null al servidor. Escribir solo filtra opciones; no cambia banco/cuenta
aplicados ni el campo Buscar de la consulta principal.

Al cambiar Banco se limpia Cuenta y su busqueda. Elegir Cuenta sin Banco selecciona
el banco real contenido en su respuesta. La clave de consulta incluye usuario,
banco, contexto de Estados de cuenta y soloConfiguradas=false; las opciones se
filtran otra vez por banco para no ofrecer cuentas de respuestas ajenas.
Se deduplica por ID y se conserva la seleccion conocida durante un fallo de recarga.

Carga, ausencia de cuentas, ausencia de coincidencias y error tienen mensajes
separados. Los errores permiten reintentar; Actualizar recarga catalogos y estados.
Una cuenta historica que no aparezca en el catalogo genera advertencia, sin
inventar IDs ni modificar asociaciones bancarias.

## Estados, detalles y exportacion

Se conserva POST `/api/CntEstadosCuenta/GetAll`:

```json
{
  "usuarioId": 7,
  "codigoBanco": 1,
  "codigoCuentaBanco": 11,
  "fechaDesde": "2026-05-01",
  "fechaHasta": "2026-05-31",
  "searchText": ""
}
```

Respuesta `ResultDto<CntEstadoCuentaResponse[]>` sin paginacion de API; DataGrid
pagina localmente. La fila incluye ID de estado/cuenta/banco, noCuenta, banco,
numeroEstadoCuenta, fechaDesde/Hasta, saldoInicial/Final, cantidadMovimientos,
montoMovimientos y codigoEmpresa. El contrato principal no cambia.

Se conserva POST `/api/CntEstadosCuenta/Details` para movimientos:

```json
{ "usuarioId": 7, "codigoEstadoCuenta": 100, "status": "", "searchText": "" }
```

Respuesta `ResultDto<CntEstadoCuentaDetalleResponse[]>` sin paginacion de API.
No se agrega un endpoint nuevo de detalle.

Al cambiar banco, cuenta, fechas o Buscar se limpia la seleccion y sus filtros de
movimientos. Se selecciona el primer estado disponible cuando llega la lista;
solo se consulta/muestra detalle si su ID pertenece a la lista visible actual.
Un estado desaparecido al actualizar no deja sus movimientos en pantalla.
Excel exporta la consulta actual; se deshabilita durante carga o error y cuando
no hay filas. El detalle tambien requiere un estado visible seleccionado.

## Correccion Oracle e instalacion

Se elimina `c.ESTADO_CMP = 'FINALIZADO'` solo de la consulta de
`SIS.SIS_CUENTAS_BANCOS` en `CNT.SP_CNT_CTAS_BCO_GET`. ESTADO_CMP corresponde al
flujo de comprobantes, no aparece en el modelo bancario del repositorio y no es
un criterio para consultar cuentas historicas. No se tocan los filtros de
comprobantes finalizados que aparecen en otras partes del SQL.

Se mantienen empresa, banco y el parametro soloConfiguradas. Las pantallas que
requieren configuracion siguen enviando true y filtrando mayor/auxiliar no nulos.
Estados de cuenta envia false y admite cuentas sin esa configuracion.
No se alteran tablas, cuentas, mayores, auxiliares, historicos ni conciliaciones.

Ejecutar con SQL*Plus/SQLcl y privilegio para reemplazar el procedimiento CNT:

```sql
@SP_CNT_CTAS_BCO_GET.sql
SHOW ERRORS PROCEDURE CNT.SP_CNT_CTAS_BCO_GET;
```

El archivo esta en `Conciliacion/CuentasBanco/`, junto a su contrato y handlers.
La misma correccion queda en `Sql/SP_CNT_CONCILIACION.sql` para instalaciones
completas. Para este ajuste usar el archivo puntual; no hace falta reinstalar
los demas procedimientos. Nombre de objeto SP_CNT_CTAS_BCO_GET: 19 caracteres;
no se introducen tablas, columnas ni parametros nuevos.

Antes de reemplazar en el ambiente, conservar el DDL vigente y comprobar esquema
y permisos. Despues, confirmar estado VALID, ausencia de errores de compilacion
 y consulta con un banco real, usando soloConfiguradas=false y true. GitHub
Actions publica aplicaciones, pero no ejecuta este SQL automaticamente.

No se ejecuto el script ni se verifico el esquema en Oracle durante esta tarea.
