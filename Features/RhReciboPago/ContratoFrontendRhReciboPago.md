# Contrato frontend - RhReciboPago

Requerimiento 49, fase 2. Fecha: 2026-11-06.

## Alcance

Migra a Vertical Slice la generacion del recibo, su resumen y sus oficinas.
Se conservan pantalla /apps/rh/recibos/, visor, Blob, manejo de errores y filtros.
Los selectores compartidos de tipos de nomina y periodos siguen usando sus rutas
legacy existentes: no se migra todo RH. El backend legacy de fase 1 permanece
funcional como reverso y referencia; no se ejecutan ambos generadores en paralelo.

## Rutas

Todas son POST, JSON y Bearer requerido. Cliente ossmmasofApiVertical.
Empresa exclusivamente desde settings:EmpresaConfig, no desde el body.

| Ruta | Solicitud | Respuesta exitosa |
| --- | --- | --- |
| /api/RhReciboPago/GetResumen | Nomina, periodo, oficina/persona opcionales | ResultDto con arreglo de trabajadores historicos |
| /api/RhReciboPago/GetOficinas | Nomina, periodo | ResultDto con arreglo de oficinas historicas |
| /api/RhReciboPago/pdf | Nomina, periodo, oficina/persona opcionales | Bytes application/pdf inline, no-store |

```json
{
  "codigoTipoNomina": 23,
  "codigoPeriodo": 5952,
  "codigoIcp": 2261,
  "codigoPersona": 123,
  "sendEmail": false
}
```

- Nomina/periodo positivos. Oficina/persona omitida, null o cero no restringe
  ese filtro. Negativos invalidos. Para individual enviar la persona positiva.
- Lote: omitir persona. Oficina positiva se combina con persona si ambas existen.
- sendEmail=true se rechaza; generar/reintentar no envia correo.
- GetOficinas ignora oficina/persona al consultar el catalogo completo del periodo.
- No hay paginacion en resumen, oficinas o PDF. En ResultDto los campos page y
  totalPage conservan su valor por defecto; cantidadRegistros informa el conteo
  del arreglo. Una consulta vacia devuelve data=[], isValid=true; PDF sin filas
  devuelve isValid=false y mensaje, no un archivo vacio.
- Sin token: 401. JSON mal formado: puede producir 400. Validacion, conexion o
  generacion fallida: HTTP 200 con ResultDto, isValid=false y mensaje en espanol.
  El cliente Blob interpreta los errores JSON y no los abre como PDF.

Fragmento ilustrativo de GetResumen:

```json
{
  "isValid": true,
  "message": "success",
  "data": [{
    "id": 1,
    "codigoTipoNomina": 23,
    "codigoPeriodo": 5952,
    "periodo": 1,
    "fechaNomina": "2025-10-09T00:00:00",
    "fechaNominaString": "09/10/2025",
    "fechaNominaObj": { "year": "2025", "month": "10", "day": "09" },
    "codigoIcpConcat": "01-02",
    "codigoIcp": 2261,
    "denominacion": "OFICINA DE PRUEBA",
    "codigoPersona": 123,
    "nombre": "PERSONA DE PRUEBA",
    "descripcionPeriodo": "1ra. Quincena"
  }],
  "cantidadRegistros": 1,
  "page": 0,
  "totalPage": 0
}
```

Id es de presentacion, no una clave persistida. GetOficinas devuelve objetos con
codigoIcpConcat, codigoIcp y denominacion. Los IDs del ejemplo son ilustrativos.

## Reglas de equivalencia con legacy

- PDF: elegir temporal si existe para empresa/nomina/periodo ANTES de filtrar
  oficina/persona. Si hay temporal para otros trabajadores y no para el solicitado,
  informar sin datos; no hacer fallback silencioso a historico.
- En ausencia de temporal, consultar historico. Un error de temporal no se oculta
  usando historico. Se conserva la marca PROVISIONAL del lote temporal y ausencia
  de marca en individual e historico, conforme a fase 1.
- Listado y oficinas se consultan en historico, como en los endpoints anteriores;
  no se cambia implicitamente su origen para que coincida con el PDF.
- Se reutilizan RH_V_REPORTE_NOMINA_TEMPORAL/HISTORICO, con sus transformaciones
  de importes. No se recalcula nomina ni se escriben filas.
- No aplicar DISTINCT al detalle: los movimientos identicos se conservan. El
  mapper temporal legacy omitia Sueldo y presentaba cero: se preserva ese cero
  en el PDF temporal; historico mantiene SUELDO. Corregir esa regla requeriria
  otro cambio funcional, no se oculta dentro de la migracion.
- Agrupacion por departamento y persona, portada por departamento y dos recibos
  por pagina sin hoja vacia final. Componentes de cabecera/conceptos/totales/firma
  portados desde fase 1; se conserva el formato monetario es-AR y la fecha corta
  del proceso en el PDF. El resumen usa dd/MM/yyyy como antes.
- El detalle ahora tiene orden SQL explicito por oficina, persona, concepto,
  complemento y monto. Las vistas no proporcionaban un orden garantizado al EF
  legacy. El resumen sigue ordenado por denominacion y nombre.
- Las referencias sinteticas se comparan por texto normalizado Unicode, montos,
  marca y paginas, no por bytes. QuestPDF tiene versiones distintas en ambos
  proyectos; metadatos, ligaduras y metricas tipograficas pueden variar.

## Configuracion y publicacion

1. Ejecutar INSTALL_RH_RECIBO.sql como RH con SQL*Plus/SQLcl. Es un archivo
   autonomo que crea SP_RH_RECIBO_PAGO_GET y comprueba estado VALID. No modifica
   tablas, vistas ni datos. Si la API usa otro usuario, revisar permiso EXECUTE.
2. Publicar API vertical. Configurar settings:EmpresaConfig y el mismo
   settings:RhReciboLogo validado en legacy. En Ubuntu de pruebas ese logo esta
   en /srv/ossmmasoft-pruebas/shared/report-assets/rh/LogoIzquierda.jpeg. Configurar
   la propiedad en shared/config/vertical.json, conservando los demas valores.
3. Si no se define RhReciboLogo, se busca BmFiles/LogoIzquierda.jpeg y despues
   Assets/Reports/logoLeft.jpeg incluido en el publish vertical. Un RhReciboLogo
   explicito invalido produce error; no se sustituye silenciosamente.
4. Validar las tres rutas verticales con token y casos de referencia del ambiente.
5. Publicar frontend con NEXT_PUBLIC_RH_RECIBOS_API=vertical o sin definirla (default
   vertical). En GitHub de pruebas se expone mediante QA_RH_RECIBOS_API; el workflow
   utiliza vertical por defecto. Instalar SQL y API antes de este frontend.
6. Comprobar individual/lote, temporal/historico, totales, oficinas, visor y errores.
   Confirmar que Network usa RhReciboPago en el cliente vertical. Los selectores
   compartidos de nomina/periodo pueden seguir mostrando trafico legacy previsto.

No se instala SQL automaticamente desde los workflows y esta implementacion no
ha ejecutado el instalador ni publicado componentes en los servidores.

## Reverso

Compilar/publicar frontend con NEXT_PUBLIC_RH_RECIBOS_API=legacy (QA_RH_RECIBOS_API
para el workflow de pruebas). Es una variable de COMPILACION, no basta cambiarla
en el servidor con el frontend ya compilado. La configuracion legacy de fase 1
con su logo permanece disponible. No hay fallback automatico ante errores y
ninguna solicitud se duplica entre ambas APIs.

No eliminar el endpoint legacy ni las vistas al probar el reverso. El nuevo
procedimiento puede permanecer instalado: es de lectura y no cambia datos.
