# Comprobantes - filtro por tipo

## Catalogo y selector

El listado usa el mismo catalogo que Proceso Automatico: `tipos-comprobante`,
mediante `fetchCntCatalog`. Muestra la descripcion y envia el ID del catalogo;
no el codigo ni el texto. Orden de filtros: Periodo, Origen, Tipo, Registro.
Sin seleccion incluye todos los tipos. El selector permite buscar y limpiar.

## Consulta

`POST api/CntComprobantes/GetAll`

```json
{
  "usuarioId": 7,
  "pageSize": 10,
  "pageNumber": 1,
  "searchText": "",
  "codigoPeriodo": 198,
  "origenId": 125,
  "tipoComprobanteId": 18,
  "estadoContable": "FINALIZADO",
  "esAutomatico": true
}
```

`TipoComprobanteId` es opcional y nullable. Omitido/null no restringe tipos;
la interfaz traduce su estado interno0 a propiedad omitida. Un ID sin filas
no ignora el filtro: retorna lista vacia. Se combina mediante AND con periodo,
origen, texto, fechas, estado contable y registro automatico/manual.
La empresa sigue viniendo de `settings:EmpresaConfig`; no se recibe del cliente.
Validaciones de usuario/permisos y rango de fechas existentes se conservan.

Respuesta `ResultDto<List<CntComprobanteResponse>>`, sin cambios de campos:

```json
{
  "data": [],
  "isValid": true,
  "message": "Success",
  "page": 1,
  "totalPage": 0,
  "cantidadRegistros": 0
}
```

El filtro se aplica en Oracle antes de contar y paginar: tanto el COUNT como
el cursor usan `p_TIPO_COMPROBANTE_ID`. Cada fila conserva `tipoComprobanteId`
y su descripcion. No filtrar solo las filas de la pagina en React.

Cambiar o limpiar tipo reinicia pagina a1. El tipo se incluye en la clave de
React Query y en `cnt.comprobantes.filters` de localStorage. Volver al listado
restaura la seleccion; “Limpiar” elimina el tipo junto con los otros filtros.
Errores del catalogo se muestran en el campo; errores API conservan el flujo
existente de `isValid=false` a mensaje visible.

## Instalacion

Cambios coordinados de API (query/handler), `SP_CNT_CMP_GET_ALL` y frontend.
El parametro Oracle se agrega al final con DEFAULT NULL para conservar los
llamadores previos. Conservar la correccion de signos ya publicada; no instalar
una version anterior del procedimiento. No requiere modificar comprobantes.

Actualizacion de totales: la version actual del procedimiento tambien requiere
las salidas `p_TOTAL_DEBE` y `p_TOTAL_HABER`. La compatibilidad del parametro
opcional de tipo no permite instalar solo SQL contra una API anterior sin esas
salidas. Ver `ContratoFrontendTotalesConsulta.md` para el contrato combinado
y la publicacion coordinada API/SQL/frontend.

## Verificacion de esta implementacion

858 pruebas backend y3 pruebas del JSX/handlers reales aprobadas. Lint y
verificacion de identificadores Oracle aprobados.13 casos en QA ejecutaron
el cuerpo SQL local como procedimiento PL/SQL local en transaccion READ ONLY,
sin CREATE ni cambios de datos: todos los7855 comprobantes y siete tipos,
intersecciones con origen/periodo/texto/estado/registro y tipo inexistente.
Se contrastaron IDs completos, ausencia de duplicados, conteos y paginas con
SELECT de referencia. Esa ejecucion inicial no equivalia a un despliegue. La publicacion posterior
y la prueba del procedimiento instalado se registran abajo.

## Publicacion QA (2026-11-24)

API: `r58-filtro-tipo-api-20261124T222626Z`. `SP_CNT_CMP_GET_ALL` instalado
VALID con respaldo previo y parametro final opcional.13 casos ejecutados contra
el procedimiento instalado en transaccion READ ONLY, contrastando todos los
IDs, tipos, totales y paginas con SELECT independientes. No hubo escrituras
en datos de negocio. Configuracion/dependencias conservadas; health200.

Frontend activo: `f4e38bc8530b1848d87d03b0731dbd3e7a8270d6-45500666977-1`.
Una publicacion concurrente incluia exactamente el selector de este commit;
se preservo en lugar de activar nuestro candidato basado en el release anterior.
Se verificaron el campo compilado, catalogo, orden Origen/Tipo, pagina200 y hash
del JavaScript servido tanto localmente como por URL externa. Esta comprobacion
no es una prueba visual autenticada con sesion SIS.

Respaldo SQL:
`/srv/ossmmasoft-pruebas/shared/req58/r58-filtro-tipo-api-20261124T222626Z/SP_CNT_CMP_GET_ALL.txt`.
Reversa API al release anterior registrado en `PublicacionApiFiltroTipoQA.json`;
coordinar con el procedimiento. El frontend requiere API con soporte del filtro:
no revertir solo API/SQL dejando visible un filtro que el servidor ignore.
Evidencias del req58: `PublicacionApiFiltroTipoQA.json` y
`PublicacionFrontendFiltroTipoQA.json`.
