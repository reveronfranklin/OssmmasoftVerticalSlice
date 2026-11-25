# Catalogos de origen y tipo de comprobante

`POST api/CntCatalogos/GetAll`

```json
{ "catalogo": "origenes-comprobante" }
```

```json
{ "catalogo": "tipos-comprobante" }
```

`origenes` y `tipos` son alias admitidos. `searchText` es opcional y busca
por codigo o descripcion. No hay paginacion ni cambio en los nombres de campos.
Un catalogo no soportado devuelve `isValid=false` y mensaje en espanol.

La seleccion usa `CNT.CNT_DESCRIPTIVAS A` unida a `CNT.CNT_TITULOS B` por
`A.TITULO_ID=B.TITULO_ID`, sin asumir IDs fijos de titulo:

- Origen: `B.CODIGO='ODC'`, ordenado por `A.DESCRIPCION`.
- Tipo: `B.CODIGO='TDC'`, ordenado por `A.CODIGO`.

Estas dos consultas siguen el alcance solicitado, sin filtrar empresa. La
configuracion de empresa sigue siendo requerida por el handler y utilizada
por operaciones de negocio; esta lectura de catalogos no modifica permisos,
validaciones de generacion, comprobantes ni reglas de calculo.

Respuesta existente `ResultDto<List<CntCatalogResponse>>`:

```json
{
  "data": [
    {
      "id": 18,
      "codigo": "02",
      "descripcion": "Ejemplo",
      "extra1": "",
      "extra2": "",
      "extra3": ""
    }
  ],
  "isValid": true,
  "message": "Success"
}
```

El ID corresponde a `A.DESCRIPCION_ID`; se mantienen `codigo` y los tres extras
para los consumidores existentes. En particular, Automaticos identifica las
familias de origen mediante `codigo`, aunque el SELECT solicitado para origen
solo mostraba ID y descripcion. No cambia el payload de los selectores.

`CNT.SP_CNT_CAT_DESC_GET` recibe al final `p_CODIGO_TITULO VARCHAR2 DEFAULT NULL`.
El handler de catalogos envia ODC/TDC y `p_TITULO_ID=NULL`. Los llamadores
anteriores que envian solo ID de titulo conservan la rama numerica y su filtro
de empresa. Instalar el procedimiento actualizado junto con la API; el
frontend utiliza los mismos endpoints y no requiere cambios para esta carga.

Verificacion local:858 pruebas backend aprobadas y nombres Oracle <=30.
En QA, el cuerpo PL/SQL local reprodujo los SELECT solicitados y su orden:
17 origenes ODC y56 tipos TDC. Las dos consultas legacy tambien pasaron.
Solo READ ONLY; no se instalo SQL ni se publico esta modificacion.

## Publicacion QA - 2026-11-24

API: `r58-catalogos-api-20261124T232218Z`.
Frontend: `r58-catalogos-ui-20261124T232350Z`.
Procedimiento `SP_CNT_CAT_DESC_GET` instalado VALID con respaldo previo.
Cuatro casos sobre el procedimiento real y cuatro llamadas del handler
publicado aprobados: ODC17, TDC56 y alias. Se compararon IDs, textos, codigos
y orden con los SELECT solicitados; ramas numericas legacy conservadas.

El frontend fue desplegado en un release nuevo reutilizando el build compatible
activo: no cambia el contrato ni requiere una modificacion de codigo frontend.
Se preservaron la configuracion de QA y la fuente previamente activa; no se
incluyo el cambio local ajeno a catalogos en `auth.ts` del commit c1db461.
Comprobantes y Proceso Automatico devolvieron200; sus archivos JavaScript se
verificaron por hash desde el puerto local y la URL externa. No fue una prueba
visual autenticada con sesion SIS. API health200 y configuracion sin cambios.

Respaldo SQL:
`/srv/ossmmasoft-pruebas/shared/req58/r58-catalogos-api-20261124T232218Z/SP_CNT_CAT_DESC_GET.txt`.
Reversa coordinada: restaurar ese SQL y el release API previo
`r58-filtro-tipo-api-20261124T222626Z`; frontend previo registrado en evidencia.
Sin escrituras de datos de negocio. Evidencias del req58:
`PublicacionApiCatalogosQA.json` y `PublicacionFrontendCatalogosQA.json`.
