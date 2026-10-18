# Eliminar comprobante completo desde mantenimiento

Requerimiento 43. Fecha: 2026-10-18.
Estado: accion frontend implementada; reutiliza el backend existente.

## Operacion y solicitud

POST `/api/CntComprobantes/delete` por `ossmmasofApiVertical`.
Requiere autenticacion y permiso `contabilidad.comprobantes.eliminar`.
Para automaticos requiere ademas `contabilidad.comprobantes.editar_automatico`.

```json
{
  "codigoComprobante": 123,
  "usuarioId": 7,
  "version": 4
}
```

Los identificadores del ejemplo son ilustrativos. No ejecutar contra datos reales
como prueba. El usuario debe coincidir con la identidad autenticada y validada
por `CntSecurity.ValidateRequestUserAsync`. Empresa procede de configuracion.
Version se obtiene del comprobante consultado; no se omite aunque el DTO backend
la declare nullable. No requiere paginacion.

## Respuesta

Se conserva `ResultDto<string>`, HTTP 200 para resultados del handler:

```json
{ "data": "Success", "isValid": true, "message": "Success" }
```

Ejemplos de rechazo; los mensajes pueden incluir prefijos y pila Oracle:

```json
{ "data": null, "isValid": false, "message": "El periodo esta cerrado." }
```

```json
{ "data": null, "isValid": false, "message": "No tiene permiso para editar comprobantes automaticos." }
```

```json
{ "data": null, "isValid": false, "message": "El comprobante tiene movimientos en libro banco. Resuelva esas relaciones antes de modificarlo." }
```

```json
{ "data": null, "isValid": false, "message": "El comprobante fue modificado. Recargue antes de guardar." }
```

Sin permiso de eliminar, `isValid` es false y `message` contiene la razon de
`CntSecurity.CheckPermissionAsync`; `data` puede ser null o cadena vacia segun
el camino de validacion. No depender de un texto exacto ni de `data` para determinar
exito: comprobar `isValid`. Sin autenticacion puede responder 401.

## Protecciones conservadas

El handler existente valida usuario y permiso, determina empresa, inicia una
transaccion y llama `CNT.CNT_CMP_GUARD.CHECK_EDIT`. La guarda protege empresa,
periodo cerrado, automaticos, movimientos de libro banco y version; bloquea y
audita antes de borrar. Se invalida el precierre y se ejecuta `CNT.SP_CNT_CMP_DEL`
para detalles y cabecera. Solo se confirma si BD devuelve success/suscces;
la transaccion se revierte al fallar.

No ejecutar el procedimiento de borrado de forma aislada ni simular la operacion
mediante sucesivas llamadas a borrar lineas. No se exige cuadratura ni cantidad
minima de lineas desde la nueva accion. No se modifican contratos de guardado,
finalizacion, cierre o eliminacion por linea.

No hay SQL nuevo ni cambios al backend para esta entrega. Se requiere que los
procedimientos, guarda y triggers del modulo contable vigente esten instalados
antes de usar la accion. El esquema instalado y la concurrencia deben verificarse
en un ambiente de prueba autorizado; no se ejecutaron migraciones.

## Flujo frontend

`CntComprobanteForm.tsx` presenta "Eliminar comprobante" solo al editar. Se
habilita con permiso confirmado, periodo abierto consultado, version disponible
y permiso adicional cuando es automatico; ante fallo de consultas se bloquea.
La API vuelve a comprobar todas las reglas al confirmar.

La confirmacion identifica numero, periodo, fecha e ID persistidos, advierte
sobre todas las lineas y sobre los cambios locales sin guardar. Cancelar no
escribe. Mientras la confirmacion esta abierta o se procesa el borrado, se
bloquean edicion y guardado. Un bloqueo inmediato evita solicitudes duplicadas.

Con `isValid: false` conserva el formulario y los cambios locales, muestra el
mensaje y permite recargar explicitamente descartando cambios. Si se pierde la
respuesta, no afirma que se elimino: bloquea escrituras y ofrece "Comprobar estado"
(solo lectura) y "Volver al listado". No reintenta borrar automaticamente.
Para continuar editando o eliminar de nuevo, exige una recarga exitosa. Una
recarga fallida conserva el bloqueo; no se deduce ausencia a partir de un error.

Con exito, muestra "Comprobante eliminado", retira caches de cabecera y detalles,
invalida lista, periodos, cierre, mayor analitico, movimientos de auxiliar y libro
banco sin consultar otra vez el registro eliminado. Navega al listado usando su
persistencia de filtros existente. Si falla la navegacion, conserva un estado
de exito con "Volver al listado", sin habilitar edicion del registro eliminado.
