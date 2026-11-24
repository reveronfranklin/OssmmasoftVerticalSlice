# Contrato frontend - Comprobantes automaticos

Actualizado: 2026-11-24. El lote de siete procedimientos R58 esta instalado en
QA y sus trece previews COMP/02 pasaron con WORK revertido. Las guardas
adicionales C# y los cambios de interfaz siguen locales; releases sin cambiar.

## Rutas y acceso

POST /api/CntAutomaticos/preview y POST /api/CntAutomaticos/confirm.
Requieren la autenticacion y permiso de comprobante automatico existentes.
La empresa se obtiene de settings:EmpresaConfig; no se recibe en el body.
El token de OracleAnalisis no autentica estas operaciones de negocio.

## Solicitudes

Preview (IDs ilustrativos del catalogo QA; UsuarioId debe ser el usuario real):

```json
{
  "UsuarioId": 1,
  "CodigoPeriodo": 198,
  "TipoComprobanteId": 20,
  "OrigenId": 120,
  "FechaDesde": "2022-07-04",
  "FechaHasta": "2022-07-04"
}
```

Confirm recibe los mismos campos y ademas FechaComprobante (fecha nullable) y
Observacion (texto nullable). Conserva la generacion diaria existente dentro del
rango; FechaComprobante no reemplaza las fechas de cada dia generado.
No ejecutar confirm para validar solamente una seleccion: genera comprobantes.

## Validacion parcial R58

Ademas de los controles existentes de usuario, permiso, periodo, fechas y origen:

- CHEAUT, CHEPROAUT, CHERETAUT, CHERETDAUT y RETENFTDT requieren codigo de tipo04.
- ANCHEAUT requiere codigo99 segun el constructor instalado. QA no tiene ese
  codigo en el catalogo aplicable; no sustituirlo por13,14 ni50.
- Se compara el CODIGO numerico del catalogo, no TipoComprobanteId. En QA el ID20
  corresponde a04. Se normalizan espacios y ceros iniciales; textos invalidos
  se rechazan. El tipo debe pertenecer al titulo5 y empresa configurada o ser global.
- Esta regla es parcial. No acredita las parejas COMP/OP/contrato/orden de compra
  ni modifica sus cuentas, importes o signos. Su correccion sigue pendiente.

La validacion compartida aplica tanto a preview como a confirm antes del
constructor. Las incompatibilidades devuelven HTTP200 con ResultDto.IsValid=false,
Data=null y Message explicativo, conservando la convencion del controlador:

```json
{
  "data": null,
  "isValid": false,
  "message": "La combinacion de origen y tipo no es compatible. El origen CHEAUT requiere el codigo de tipo 04."
}
```

El sobre conserva los demas campos de ResultDto. Mostrar Message y detener el
flujo; no presentar este error como SIN_LINEAS o como generacion exitosa.
No confundir validacion de codigo con certificacion de cuentas o montos.

## Respuestas satisfactorias y paginacion

Preview: Data incluye CodigoPeriodo, TipoComprobanteId, OrigenId, FechaDesde,
FechaHasta, TotalDebe, TotalHaber, Diferencia y Lineas. Cada linea conserva
Secuencia, CodigoMayor, Mayor, CodigoAuxiliar, Auxiliar, Referencia1/2/3,
Descripcion, Monto, Debe y Haber.

Confirm: Data incluye CantidadComprobantes, CantidadDiasSinLineas, CantidadErrores,
TotalLineas, TotalDebe, TotalHaber, Diferencia y Comprobantes (resultado por dia).
Cada resultado conserva FechaComprobante, CodigoComprobante, NumeroComprobante,
CantidadLineas, TotalDebe, TotalHaber, Diferencia, Estado, Mensaje, EstadoContable
y AjusteManual. Conserva los contadores derivados CantidadPendientes,
CantidadFinalizados y CantidadDiasProtegidos.

Ambas operaciones devuelven colecciones completas; no reciben PageSize ni
PageNumber. Los campos genericos de paginacion de ResultDto no paginan Lineas
ni Comprobantes. Los nombres se serializan conforme a la configuracion JSON
existente (camelCase en los ejemplos de respuesta).

## SQL relacionado y verificacion

Sql/SP_CNT_AUT_BUILD_CH_RET.sql es la instalacion independiente de la misma
validacion de incompatibilidad ya definida por codigo04/99. Solo cambia los dos
mensajes que antes devolvian Success sin filas. SP_CNT_CORE.sql incorpora los
mismos cambios para instalaciones completas. El script acotado ya se instalo
en QA; el cheque con tipo03 devuelve error explicito en el preview integrado.

Build y846 pruebas backend aprobados localmente. Falta validar peticiones con
sesion SIS real, despliegue compatible y comportamiento integrado Oracle/UI.

## R58 - Presupuesto y transmision del tipo (SQL instalado en QA)

Preview y confirmacion usan el selector comun SP_CNT_AUT_PRES_GET, con la empresa
configurada y todo el rango. Rechaza ausencia de presupuesto, vigencias
solapadas o rangos que cruzan presupuestos; el frontend debe mostrar Message,
corregir el contexto y solicitar una nueva previsualizacion. No debe seleccionar
arbitrariamente un presupuesto ni reintentar confirmacion tras esos errores.
La confirmacion diaria conserva su contrato existente; no cambia el body.

Los constructores COMP y OP reciben ahora el tipo seleccionado mediante el
argumento final p_TIPO_COMPROBANTE_ID. Conservan la posicion de los siete
argumentos originales, pero omitir el tipo ahora devuelve un error. Preview y
confirmacion se actualizan juntos; revisar llamadores externos al instalarlos.
Esto agrega validacion de catalogo y transmision del contexto; por si solo no
resuelve la seleccion de partidas. El avance posterior de COMP/02 se describe
abajo; las otras parejas de estas ramas siguen pendientes. No se ofrece como correccion contable
completa ni como certificacion de las520 parejas candidatas.

Sql/INSTALL_CNT_R58_BASE.sql agrupa los siete procedimientos preparados y comprueba
su estado VALID. Se ejecuto con autorizacion y respaldo; siete objetos VALID.
Las pruebas sinteticas previas del selector usan
su cuerpo real con una relacion ficticia de DUAL y no instalan ningun objeto.

## R58 - COMP/02 instalado en QA; guardado y aceptacion completa pendientes

Cuando origen sea COMP y el CODIGO del tipo sea02, el constructor comun deriva
a SP_CNT_AUT_BUILD_COMP02. TipoComprobanteId sigue siendo un ID de catalogo;
no enviar2 en lugar del ID. La respuesta y el body de API conservan su forma.

El constructor solo usa mayores300 Debe y103 Haber de la empresa, sin IDs de
mayor fijos. Distribuye importes por PUC/auxiliar configurado, con fallback por
proveedor/mayor si falta mapa para ese PUC. Rechaza mayores ausentes/duplicados,
auxiliares faltantes o ambiguos, hijos con empresa/presupuesto inconsistentes y
repartos que descuadren un compromiso tras redondear a dos decimales.

Esos errores devuelven IsValid=false y Message con la causa; no interpretarlos
como SIN_LINEAS ni omitir la mitad del asiento. No se agregan ajustes de cuadre.
No aplicar esta regla a ANCOMP, OP ni otros tipos. Su compatibilidad funcional
sigue pendiente. No se altera retrospectivamente ningun comprobante existente.

Quince escenarios de SQL/PLSQL sintetico pasaron en Oracle QA con DUAL y
colecciones en memoria. Falta validar importes reales, INSERT a WORK, preview,
confirmacion, detalle y reporte con la version instalada y sesion SIS real.

## Verificacion integrada y guardas de interfaz

El procedimiento PREV instalado reprodujo100 lineas de50 compromisos, en13 dias,
con944112.86 por lado. El caso04/07/2022 produjo4 lineas y92959.42 Debe/Haber.
Las transacciones de preview se revirtieron. No se ejecuto CONF ni se altero el
historico; no es una prueba del guardado o de todas las parejas.

La interfaz local usa CODIGO de catalogo para deshabilitar tipos de cheque
incompatibles y bloquear preview/generacion ante un tipo incompatible o un ID
ya ausente del catalogo. Muestra la causa sin sustituir el ID. Si ANCHEAUT no
tiene99, no inventa13/14/50. No se agregan filtros de COMP/OP sin regla comprobada.
Cuatro pruebas de JSX/handlers simulados y lint aprobados; pendiente publicacion
y revision visual. El backend conserva la autoridad de validacion.

## Almacenamiento y contrato: conversion publicada en QA

El contrato de request/response no cambia: Monto negativo=Debe y positivo=Haber.
El frontend debe enviar/mostrar ese contrato sin invertirlo por su cuenta.
Oracle fisico usa positivo=Debito y negativo=Credito. La API convierte al
escribir manuales y leer detalle/reportes; CONF convierte WORK una sola vez
al persistir. GET_ALL/GET_ID totalizan con el signo fisico.

La API y tres procedimientos se publicaron coordinadamente el2026-11-24 en QA.
Se verificaron guardado manual, detalle, totales y Mayor con dos filas temporales
revertidas (125.35 por lado),26 filas legacy y huella financiera sin cambios.
858 pruebas backend y8 casos SQL sinteticos aprobados. No se ejecuto CONF ni
precierre/cierre; la integracion automatica completa sigue pendiente, al igual
que publicacion frontend y revision con sesion SIS real. No hay conversion
historica masiva. Ver req58/SignosCNT.Verificado.md yChecklist.md.
