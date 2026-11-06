# Contrato frontend - Historico individual y complemento

Requerimiento 48, ampliado el 2026-11-06 con correcciones de filtros/consulta.

## Columna y origen

Pantalla `/apps/rh/individual/`: Cod Concepto, Concepto, Complemento de concepto,
Monto. Complemento es de solo lectura, sin filtro/orden propios, con tooltip y
soporte de enfoque; null/vacio se presenta vacio.

`NextOssmasoft/plan_migracion_sql.sql` define la vista con
`B.COMPLEMENTO_CONCEPTO COMPLEMENTO`, desde RH_HISTORICO_NOMINA B.
El SQL devuelve COMPLEMENTO y C# Complemento lo serializa como `complemento`.
No se modifica la vista ni se agrega una columna fisica.

## Consulta

POST `/api/RhHistoricoMovimiento/GetHistoricoFecha`, JSON y Bearer requerido.
Empresa siempre desde `settings:EmpresaConfig`, nunca desde el cliente.
Movimientos y catalogos se limitan a esa empresa. El alcance es consulta
para usuarios autenticados; no se incorpora una nueva matriz de roles.

```json
{
  "desde": "2025-01-01",
  "hasta": "2026-11-06",
  "tipoQuery": "INDIVIDUAL",
  "codigoPersona": 123,
  "codigoTipoNomina": [{ "codigoTipoNomina": 1 }],
  "codigoConcepto": [{ "codigoConcepto": 22 }],
  "page": 0,
  "pageSize": 100,
  "tipoSort": "desc",
  "sortColumn": "monto",
  "searchText": "detalle",
  "codigoProceso": 0
}
```

- Fechas de calendario yyyy-MM-dd; ambas requeridas y Hasta >= Desde. El servidor
  incluye el dia Hasta completo. El frontend no convierte estas fechas a UTC.
- Individual exige persona positiva. Arreglos vacios o null no restringen nominas
  o conceptos; IDs enviados deben ser positivos. No se descartan IDs invalidos
  silenciosamente para ampliar resultados.
- `page` base cero y no negativo; `pageSize` positivo se limita a 500. Cero indica
  exportacion. Valores negativos son invalidos.
- `searchText` opcional, hasta 200 caracteres despues de trim. Busqueda literal,
  sin distincion de mayusculas, sobre nombre, apellido, cedula, nomina, codigo,
  denominacion y complemento. Se aplica antes de contar y paginar.
- `sortColumn`: fechaNominaMov, fechaNomina, fullName, tipoNomina, sueldo, monto,
  codigo, denominacion, unidadEjecutora o cedula. Se toleran full_name/full_Name.
  `tipoSort`: asc/desc. Valores no soportados son rechazados. Valores omitidos:
  fechaNominaMov desc. SQL usa CASE estatico, sin concatenar instrucciones.
- `codigoProceso` conserva su lugar en el contrato compartido; el individual
  envia 0. No se implementa filtro por proceso en esta ampliacion.
- El procedimiento calcula el orden global antes de paginar. Conserva ID de
  presentacion mediante ROW_NUMBER; no es la PK persistida del movimiento.
  Los desempates incluyen fecha, persona, nomina, concepto, periodo, relacion,
  complemento, tipo y monto. Cambios concurrentes de datos entre peticiones
  siguen requiriendo una nueva consulta; no se ofrece un snapshot multipagina.

Fragmento de respuesta; cada fila conserva las demas propiedades existentes:

```json
{
  "isValid": true,
  "message": "",
  "data": [{
    "codigoHistoricoNomina": 1,
    "codigoPersona": 123,
    "codigo": "022",
    "denominacion": "SUELDO BASICO",
    "complemento": "Detalle del movimiento historico",
    "monto": 195
  }],
  "page": 0,
  "totalPage": 1,
  "cantidadRegistros": 1,
  "linkData": ""
}
```

`cantidadRegistros` y `totalPage` corresponden al filtro completo. Se conserva
ResultDto y HTTP 200 con `isValid: false` para validaciones o errores esperados.
Sin autenticacion: 401. JSON invalido puede producir 400 por ApiController.
La UI valida isValid y no interpreta errores como consultas vacias exitosas.

## Catalogos dependientes

Ambas rutas son POST, Bearer y ResultDto con arreglo en data, sin paginacion:

- `/api/RhTipoNomina/GetTipoNominaByCodigoPersona`:
  `{ "codigoPersona": 123, "desde": "2025-01-01", "hasta": "2026-11-06", "sinRestriccionFecha": false }`.
- `/api/RhConceptos/GetConceptosByPersonas`: mismo cuerpo mas
  `"codigoTipoNomina": [{ "codigoTipoNomina": 1 }]`.

La seleccion individual respeta fechas y empresa; si no hay movimientos devuelve
catalogos vacios, sin buscar automaticamente en otras fechas. Se conserva la
bandera ya existente sinRestriccionFecha para el consumidor masivo: solo true
explicito omite las fechas. Las respuestas de catalogo mantienen su forma actual.

Cambiar fechas/persona limpia selecciones dependientes en una accion Redux;
cambiar nominas limpia conceptos. Los catalogos se consultan por efecto con
cancelacion y descarte de respuestas antiguas; carga, vacio y error se distinguen,
y los errores permiten reintentar. La tabla deriva pagina cero al cambiar filtros,
texto, orden o tamano sin consultar primero la pagina antigua.

## Excel

Misma ruta, filtros, texto y orden que pantalla, con page=0 y pageSize=0.
Solo se genera archivo con una respuesta valida no vacia y vigente. Una respuesta
anterior a un cambio de filtros se cancela/descarta. Un error conserva su mensaje.
No se genera archivo vacio ni se truncan resultados para aparentar exito.

Limite por defecto: 20000 filas; configurable en settings:HistoricoExportMaxRows
hasta 100000. El SQL valida el conteo antes de abrir el cursor de datos. Si excede
el limite, responde isValid=false e indica reducir rango/filtros.

Se conservan los encabezados tecnicos y campos de historicoIndividual.xlsx,
colocando complemento despues de denominacion y preservando texto e importes.
La busqueda espera 300 ms antes de consultar; mientras el texto esta pendiente,
la exportacion queda deshabilitada para no usar filtros diferentes.

## Instalacion y reverso

Esta ampliacion modifica tres firmas de procedimientos. Ejecutar como RH desde
esta carpeta, con SQL*Plus/SQLcl, `@INSTALL_RH_HIST_FILTROS.sql`. Verifica que los
tres procedimientos queden VALID. No crea tablas, columnas ni cambia datos.
Se renombra el archivo del catalogo a SP_RH_TN_BY_PERSONA_GET.sql para coincidir
con el procedimiento y cumplir el chequeo de identificadores; no cambia su nombre
Oracle existente. Revisar permisos EXECUTE habituales si la API usa otro usuario.

Publicar scripts, API y frontend coordinadamente: la API nueva requiere firmas
nuevas. Los consumidores compartidos deben enviar Bearer; las pantallas masivas
existentes ya usan el cliente que lo incorpora. Se mantiene la bandera explicita
sin fecha, rutas y forma de respuesta. Probar regresion del masivo en el ambiente.
Reverso: restaurar juntos API y procedimientos anteriores, y el frontend si procede.

No se instalaron scripts ni se ejecuto una validacion contra Oracle en esta entrega.

### Script autonomo para el servidor

`INSTALL_RH_HIST_COMPLETO.sql` contiene los tres procedimientos y la verificacion
final en un solo archivo, sin referencias @/@@. Es una alternativa al instalador
por archivos: ejecutar uno de los dos, no ambos. Copiar solo este archivo al
servidor y ejecutarlo como script conectado como RH. Al terminar muestra
"Instalacion correcta: 3 procedimientos VALID."; si hay errores, revisar la
salida ALL_ERRORS y coordinar la publicacion con las firmas nuevas.
