# Totales de la consulta de comprobantes

Estado: publicado en QA, con API, Oracle y frontend coordinados.

`POST api/CntComprobantes/GetAll`. Se conserva el request del listado y la
respuesta `ResultDto<List<CntComprobanteResponse>>` con `data` como arreglo.
Los campos existentes `total1` y `total2` ahora contienen Debe y Haber de todos
los comprobantes que cumplen la consulta, antes de paginar.

```json
{
  "usuarioId": 7,
  "pageSize": 2,
  "pageNumber": 2,
  "searchText": "",
  "origenId": 125,
  "tipoComprobanteId": 18,
  "estadoContable": "FINALIZADO",
  "esAutomatico": true,
  "fechaDesde": "2023-01-01",
  "fechaHasta": "2023-01-31"
}
```

Ejemplo de metadatos de la consulta validada en QA, omitiendo aqui los campos
de las filas:

```json
{
  "isValid": true,
  "message": "Success",
  "page": 2,
  "totalPage": 3,
  "cantidadRegistros": 5,
  "total1": 452733.83,
  "total2": 452733.83
}
```

- `total1`: total Debe de la consulta completa.
- `total2`: total Haber de la consulta completa.
- `cantidadRegistros`: numero de comprobantes, incluidos aquellos sin detalles.
- `page`, `totalPage` y `data`: conservan su significado paginado.

Se aplican exactamente periodo, origen, tipo, rango de fechas, estado contable,
registro automatico/manual y texto de busqueda del listado. La empresa sigue
viniendo de `settings:EmpresaConfig`; el detalle se suma solo para esa empresa.
Oracle usa MONTO positivo=Debe y negativo=Haber. No se suman los importes de una
sola pagina en el navegador. No se cambian datos ni signos almacenados.

Sin coincidencias: cantidad0, paginas0, `data=[]`, total1/total2=0. Una pagina
fuera de rango devuelve `data=[]`, conservando los totales reales de la consulta.
Las validaciones de permisos/usuario/fechas y errores del listado se conservan.

El servicio frontend traduce total1/total2 a totalDebe/totalHaber. La pantalla
muestra “Totales de la consulta” con ambos importes en formato es-VE, separados
del desplazamiento horizontal de la grilla. Cambiar filtros recalcula ambos;
paginar no altera su alcance. Durante una recarga muestra “Calculando...”;
no muestra totales cuando hay un error de consulta o de rango de fechas.

## Oracle e instalacion

`CNT.SP_CNT_CMP_GET_ALL` agrega al final dos salidas NUMBER requeridas:
`p_TOTAL_DEBE` y `p_TOTAL_HABER`. El primer SELECT calcula COUNT DISTINCT del
comprobante y suma sus detalles filtrados; el cursor mantiene las filas de la
pagina. Asi los detalles no multiplican el conteo de comprobantes.

Instalar coordinadamente API y procedimiento: el handler enlaza las dos nuevas
salidas por nombre. Los llamadores anteriores del procedimiento deben agregar
ambos OUT. Actualizar frontend despues de API/SQL para evitar presentar ceros
provenientes de un backend anterior que no calculaba esos campos. Respaldar el
procedimiento previo; revertir API/SQL juntos si la validacion falla.

## Verificacion

861 pruebas backend (incluidas lectura decimal Oracle con es-VE/en-US y null),
6 pruebas del JSX/handlers y servicio reales y lint aprobados. Identificadores
Oracle <=30 verificados.14 casos SQL READ ONLY QA compararon IDs, conteos,
totales de detalles independientes, suma de todas las paginas y una pagina
adicional vacia. Enero2023 COMP/tipo18/finalizados/automaticos:5 comprobantes,
452733.83 por lado en las tres paginas de dos filas. La prueba inicial fue sin DDL; la publicacion posterior se documenta abajo. Evidencia:req58/Evidencias/TotalesConsultaSoloLecturaQA.json.

## Publicacion QA

API: `r58-totales-api-20261125T001319Z`.
Frontend: `r58-totales-ui-20261125T001146Z`.
`SP_CNT_CMP_GET_ALL` instalado VALID, con respaldo previo:
`/srv/ossmmasoft-pruebas/shared/req58/r58-totales-api-20261125T001319Z/SP_CNT_CMP_GET_ALL.txt`.

Los14 casos pasaron contra el procedimiento instalado y el lector decimal del
binario nuevo. Se validaron totales, conteos, filtros, todas las paginas y pagina
extra vacia. Enero2023 mantiene5 registros y452733.83 por lado en tres paginas.
La API responde health200; configuracion/dependencias se conservaron. Solo DDL
de procedimiento y lecturas de prueba, sin escrituras de datos de negocio.

El frontend se recompilo desde2206 fuentes actuales cotejadas, conservando las
validaciones y el orden de Contabilidad. Se verifico el resumen compilado y el
hash del JavaScript servido por URL externa. Auth compilado sigue en QA:
login/refresh/sesion `/legacy/api` y API vertical `/vertical/api`. Comprobantes,
Automaticos y login respondieron200. No se inicio una sesion SIS para prueba
visual autenticada. Evidencias:req58/Evidencias/PublicacionApiTotalesQA.json y
PublicacionFrontendTotalesQA.json.

Reversa: frontend previo `r58-auth-qa-final-20261124T234403Z`, API previa
`r58-catalogos-api-20261124T232218Z` y fuente SQL respaldada. Coordinar las tres
capas; no dejar el resumen nuevo apuntando a un backend que no calcule totales.
