# RhMotorNomina - contrato frontend R57

Estado actual (2026-11-22): API r57-cap-srv-20261122T190224Z; frontend
r57-ce-manual-20261121T231141Z. Ruta UI: /apps/rh/motor-nomina/.
Variables, revisiones, vigencias, unidades y simulaciones/diagnosticos disponibles.
50 rutas/200 rechazos HTTP verificados; recorrido positivo HTTP/UI con sesion SIS
real pendiente. La nueva captura de servidor tiene contrato API, sin boton UI aun.
No calcula ni aplica nomina real, no certifica paridad y no ejecuta el paquete antiguo.
PublicacionQA.md del requerimiento 57 describe evidencia, permisos y retorno.

## 1. Operaciones disponibles

POST /api/RhMotorNomina/formulasValidate
POST /api/RhMotorNomina/modelosSimular

Autenticacion JWT normal del sistema, con sesion SIS activa; no acepta el token
OracleAnalysis. Empresa desde settings:EmpresaConfig y actor desde identidad
firmada. Sin usuario ni empresa en DTOs. Un superusuario puede acceder; para otros
usuarios se requieren respectivamente rh.nomina.formulas.validar y rh.nomina.simular.
Los permisos se asignan con el mecanismo existente de seguridad SIS; no se crean
ni conceden automaticamente en esta entrega.

Estas dos operaciones de expresiones no acceden a RH; SIS solo valida acceso.
Las operaciones de catalogos/revisiones de la seccion 8 si requieren RH y su paquete
nuevo de persistencia. Ninguna llama al paquete de calculo antiguo.

## 2. Lenguaje y numeros

Tokens [CODIGO], codigos ASCII de hasta 60 caracteres, normalizados a mayusculas.
Expresiones hasta 4000 caracteres, 500 nodos y profundidad 64. Operadores + - * /,
signos unarios y parentesis; funciones ABS(x), MIN(x,y), MAX(x,y), ROUND(x,escala)
y TRUNC(x,escala). Escala entera -28..28. No SQL, comentarios, funciones arbitrarias,
comparaciones, porcentajes implicitos, exponentes o separadores de miles.

Entrada valor es STRING decimal con punto, hasta 28 cifras significativas y escala
28, dentro de System.Decimal. Se rechazan cifras/escala no soportadas en vez de
redondear al deserializar JSON. Resultados/operandos en trazas son strings invariantes.
Los calculos usan System.Decimal (96 bits; precision finita), sin double/float.
Divisiones/intermedios usan su precision interna; no se presume equivalencia con
los 38 digitos de Oracle NUMBER. Desbordamiento y perdida a cero de un producto o
cociente no nulos son errores. La caracterizacion de precision con historicos es
una puerta pendiente antes de liquidacion real; esto no certifica paridad financiera.

ROUND usa empates alejandose de cero y TRUNC hacia cero; soporta escalas negativas.
Cada operador/funcion registra operandos y resultado, sin volver a ejecutar SQL.
La traza no se escribe en logs. El evaluador no usa reloj, cultura del sistema,
valores globales compartidos, variables de otro trabajador ni datos ocultos.

## 3. formulasValidate

Solicitud con datos de prueba:

```json
{
  "variableSalida": "SUELDO_PERIODO",
  "expresion": "ROUND([SUELDO_BASE] / [DIVISOR_PERIODO], 2)",
  "variables": [
    { "codigo": "SUELDO_BASE", "valor": "1300" },
    { "codigo": "DIVISOR_PERIODO", "valor": "2" }
  ]
}
```

Si se omite variables o es null, solo se valida sintaxis y autorreferencia:
estado=SINTAXIS_VALIDA, evaluacion=null, dependencias declaradas. Esto no demuestra
que existan variables en un catalogo ni que haya un modelo publicable.
Si se envia variables (incluso []), se intenta evaluar y no se sustituyen faltantes
por cero. Valores o codigos duplicados son invalidos.

Respuesta resumida (ResultDto conserva sus demas campos habituales):

```json
{
  "isValid": true,
  "message": "Formula validada con datos de prueba.",
  "data": {
    "estado": "VALIDADA_CON_DATOS_DE_PRUEBA",
    "normalizada": "ROUND(([SUELDO_BASE] / [DIVISOR_PERIODO]), 2)",
    "dependencias": ["DIVISOR_PERIODO", "SUELDO_BASE"],
    "evaluacion": {
      "variableSalida": "SUELDO_PERIODO",
      "normalizada": "ROUND(([SUELDO_BASE] / [DIVISOR_PERIODO]), 2)",
      "sustituida": "ROUND(((1300) / (2)), 2)",
      "resultado": "650",
      "dependencias": ["DIVISOR_PERIODO", "SUELDO_BASE"],
      "pasos": [
        {"orden":1,"operacion":"/","operandos":["1300","2"],"resultado":"650"},
        {"orden":2,"operacion":"ROUND","operandos":["650","2"],"resultado":"650"}
      ]
    },
    "errores": []
  }
}
```

## 4. modelosSimular

Es simulacion de expresiones ingresadas por el usuario con valores de prueba.
No equivale todavia a simular nomina individual/lote con modelos publicados, cargo,
periodo, fuentes, unidades y reglas de aplicabilidad. Ese contrato sigue pendiente.

Solicitud: formulas [{variableSalida,expresion}] y entradas [{codigo,valor}].
Ver Examples/simular-expresiones.json: sueldo 1300, tasa .01 y divisor 2 generan
SUELDO_PERIODO=650, PH=-6.5 y NETO=643.5 (solo ejemplo sintetico, no tasa normativa).
Las formulas pueden venir desordenadas: el servidor obtiene orden topologico,
detecta ciclos (incluidos indirectos), productores faltantes, duplicados y colisiones
entre entrada y salida. Requiere 1..100 formulas, hasta 200 entradas y 10000 pasos.

Respuesta ResultDto.data:

- estado: SIMULACION_EXPRESIONES_COMPLETA o INVALIDO.
- ordenCalculo: variables en el orden calculado.
- resultados: variableSalida, normalizada, sustituida, resultado, dependencias y pasos.
- errores: lista de codigo, mensaje, posicion (cuando corresponde) y variable.

Un error en cualquier formula invalida toda la simulacion y no devuelve resultados
parciales como exitosos. Cada corrida tiene sus propios diccionarios/AST/resultados.
No se guardan resultados ni se vuelven efectivos por calcular una expresion.

## 5. Errores y limites HTTP

Validaciones de negocio: HTTP 200, ResultDto.isValid=false y diagnosticos en data.
Ejemplos: SINTAXIS_INVALIDA, AUTORREFERENCIA, VARIABLE_SIN_VALOR, VARIABLE_DUPLICADA,
DEPENDENCIA_CIRCULAR, DIVISION_POR_CERO, ESCALA_INVALIDA, PRECISION_NO_SOPORTADA,
DESBORDAMIENTO y limites. No se devuelven montos cero como sustitucion de un error.

Sin JWT/sesion valida: 401. Sin permiso: 403. Body invalido: 400 de ASP.NET.
Limite de request: 128 KiB para formulasValidate y 256 KiB para modelosSimular;
ASP.NET puede responder 413. Respuesta de traza limitada a 2 MiB. Cancelacion
observada durante el calculo; cliente desconectado puede no recibir respuesta.
No hay paginacion ni persistencia en estas dos operaciones de prueba.

## 6. Comparador historico interno

RhNomComparadorHistorico es un componente puro, sin endpoint expuesto en esta fase.
Recibe baseline/entradas-completas y resultados independientes, verifica contexto
empresa/tipo/periodo/fecha, empareja persona/concepto/frecuencia/tipo de movimiento,
detecta duplicados y compara decimal exacto, sin redondeo adicional ni SUM para
ocultar filas. Conserva deducciones firmadas y neto=sum(asignaciones+deducciones).
Se requiere verificar esa convencion con los historicos seleccionados.

Resumir separa empresa y tipo de nomina; exige al menos dos periodos distintos,
todos los casos aprobados y ningun baseline duplicado. PARIDAD_CASOS_APROBADA
solo valida los casos ingresados; no certifica cobertura completa ni aprobacion
productiva. Ningun dato de salida del nuevo calculo se usa como entrada esperada.
Monto nulo/fuera de precision, contexto diferente, datos faltantes o duplicados
impiden certificar el caso. No se consultan tablas ni se ejecuta calculo antiguo.

## 7. Verificacion y pendientes

Pruebas enfocadas en ../OssmmasoftVerticalSlice.Tests/RhMotorNomina: sintaxis,
redondeo/truncado, negativos, cultura, rangos, ciclos, estado por corrida, baseline
exacto, netos compensados, multiples periodos/tipos e aislamiento de contexto.

DIAG_RH_NOM_HIST.sql es diagnostico de solo lectura para candidatos agregados,
no instalador ni seleccion automatica de la nomina de referencia. Contiene bind
p_EMPRESA, a tomar de settings:EmpresaConfig en cada ambiente.

Interfaz de catalogos/edicion y simulacion publicada en QA desde NextOssmasoft; pendiente prueba funcional con sesion SIS autorizada.
Pendientes: ampliar analisis dimensional y condiciones tipadas,
proveedores RH/valores de parametros historicos, seleccion de casos completos por tipo,
comparacion de nominas reales, UI de ejecucion, aprobacion/publicacion de modelos y ejecucion
transaccional. No se requiere tocar el paquete actual para avanzar estas fases.
No publicar ni presentar este prototipo como sustituto del calculo real todavia.


## 8. Catalogos y revisiones guardadas

Esta entrega reutiliza los IDs de SIS.OSS_MODELO_CALCULO (PK ID verificada en QA),
que ya referencia RH_CONCEPTOS.ID_MODELO_CALCULO. No crea otro maestro de modelos
ni cambia los conceptos. La creacion de identidades de modelo sigue en el catalogo
existente; este modulo agrega contenido/revisiones sobre identidades autorizadas.
No crea automaticamente modelos ni variables de ejemplo.

Nuevas rutas POST bajo /api/RhMotorNomina:

| Ruta | Permiso | Cuerpo |
| --- | --- | --- |
| variablesGetAll | rh.nomina.configuracion.leer | {pageNumber,pageSize,searchText} |
| variablesSave | rh.nomina.configuracion.editar | {id,versionEsperada,codigo,descripcion,tipo,unidad,origen,activa} |
| modelosGetAll | rh.nomina.configuracion.leer | {pageNumber,pageSize,searchText} |
| modelosSave | rh.nomina.configuracion.editar | Examples/guardar-revision.json |
| modelosVersionGet | rh.nomina.configuracion.leer | {modeloId,version} |
| modelosSimularVersion | rh.nomina.simular | Examples/simular-revision.json |

JWT SIS y superusuario/permisos como en el nucleo. Empresa y actor exclusivamente
servidor. Estos permisos no se conceden automaticamente. Variables y modelos se
filtran por empresa tanto en API como en SQL, incluyendo la propiedad de modelos SIS
y tipos de nomina RH. No se exponen cadenas de conexion ni excepciones Oracle crudas.

### Variables

Id=0 y versionEsperada=0 crean una variable; editar requiere Id y revision leida.
Codigo normalizado a mayusculas, ASCII de hasta 60 caracteres e inmutable despues
de crear. Descripcion obligatoria hasta 300 caracteres. Tipo NUMERO en esta fase.
Unidad: NUMERO, DINERO, TASA, PORCENTAJE, FACTOR, DIAS o CANTIDAD.
Origen: ENTRADA, PARAMETRO o CALCULADA. Activa permite desactivar sin borrar.
Las unidades son metadatos: la validacion dimensional completa sigue pendiente.
Origen PARAMETRO por si solo no implica valor guardado: la simulacion manual pide
valores de prueba. La extension local de la seccion 11 agrega reglas por vigencia
en el snapshot; ninguna recupera salarios operativos automaticamente.

Respuesta de guardado: ResultDto.data {id,version}; la version se incrementa al editar.
Codigo unico por empresa; se rechazan codigos duplicados y revisiones desactualizadas.
Revisiones de modelo antiguas conservan las definiciones de variables que capturaron.

### Guardado de modelo

modeloId debe existir en el catalogo SIS de la empresa. versionEsperada es la ultima
revision del contenido, cero si aun no tiene ninguna; no es el ID del modelo SIS.
Cada save agrega una revision nueva e inmutable: no modifica la revision anterior.
Permite ADJUNTAR_CARGO y RECALCULA_VARIACIONES. Requiere tipo de nomina de la empresa,
descripcion hasta 300 caracteres, 1..200 referencias de variables activas/versiones
y 1..100 formulas. Los IDs de ejemplos son ilustrativos: consultar el catalogo antes.

El servidor obtiene las variables por IDs, valida tipos/origenes, grafo, duplicados,
productores y dependencias, y construye un snapshot con empresa, modelo, escenario,
tipo de nomina, descripciones y formulas. Toda salida debe corresponder a una variable
CALCULADA, y cada CALCULADA debe tener formula. La formula original se conserva;
se valida mediante AST sin reemplazarla por SQL o por una representacion que pueda
crecer al volver a guardarse. Contenido CLOB JSON (sin funciones JSON nativas de Oracle),
limite 1 MiB y SHA256 sobre el texto exacto UTF-8.

Respuesta: ResultDto.data {modeloId,version,hash,estado:"BORRADOR_NO_PUBLICADO"}.
Guardar valida estructura; no demuestra que funcione con todo valor o contexto.
Una formula con division por un dato que luego vale cero puede guardarse como borrador
pero fallara al simular. No existe publicacion ni ejecucion de nomina en esta entrega.

El paquete bloquea la cabecera/version y las variables referenciadas; comprueba otra
vez las versiones antes de insertar. Los cambios de variables durante un guardado
provocan conflicto. Sin COMMIT interno: la API confirma todo o revierte todo. No
reintentar a ciegas un save tras error de comunicacion: recargar version primero.

### Recuperacion y simulacion

modelosVersionGet exige revision explicita y devuelve version, tipo, escenario,
descripcion, hash, usuario/fecha de creacion y snapshot. Estado siempre borrador.
Comprueba hash, formato, empresa/modelo, cabecera y grafo al leer. SHA256 detecta
alteracion de contenido, no reemplaza la autorizacion ni firma a un administrador.

modelosSimularVersion usa exclusivamente el snapshot de esa revision, incluso si
el catalogo cambio despues. Requiere exactamente los codigos de entradas/parametros
declarados; no acepta valores adicionales, faltantes o suplantacion de CALCULADAS.
Utiliza el mismo motor que modelosSimular y retorna su contrato de traza/errores.
No persiste el resultado ni incluye datos reales de trabajadores por su cuenta.

### Paginacion y errores

Listados: pageNumber 1..100000, pageSize 1..100 (default 20), searchText hasta 100.
Respuesta ResultDto con data, page, totalPage y cantidadRegistros; cero filas no
inventa una pagina de resultados. modelosGetAll incluye identidades SIS sin contenido
nuevo (ultimaVersion=0). Guardados/consulta de revision no tienen paginacion.

Conflictos y validaciones: HTTP 200 e isValid=false con mensaje espanol. Falta de
JWT: 401; falta de permiso: 403; payload invalido: 400. Configuracion de tablas o
paquete no instalada produce fallo sin exponer SQL. La UI futura debe invalidar
queries tras guardar y recargar antes de resolver un conflicto de version.

## 9. Instalacion y verificacion pendiente de Oracle

1. Revisar primero Sql/00_SIS_NOM_CFG_GRANT.sql: ejecutar como SIS concede unicamente
   SELECT y REFERENCES del catalogo de modelos a RH, sin DML sobre el catalogo.
2. Ejecutar Sql/01_RH_NOM_CFG_TABLES.sql y Sql/02_RH_PKG_NOM_CFG.sql como RH. Para
   SQL*Plus/SQLcl se incluye INSTALL_RH_NOM_CFG.sql con detencion ante errores.
   En DBeaver ejecutar archivos completos en orden, revisando errores de package/body.
3. Verificar objetos VALID y USER_ERRORS vacio. Scripts para instalacion inicial;
   no reparan una tabla homonima con estructura incompatible. DDL tiene commits
   implicitos Oracle: no asumir rollback completo de una instalacion incompleta.
4. Publicar API y asignar permisos a usuarios de prueba. Guardar las variables del
   ejemplo con sus origenes (SUELDO entrada, DIVISOR parametro, SUELDO_PERIODO calculada),
   consultar los IDs reales y crear revision sobre un modelo SIS de prueba.
5. Verificar save/get/simular, revision dos, snapshot anterior intacto, conflictos de
   version/concurrencia, rechazo entre empresas, rollback tras fallo e intento de
   actualizar/borrar una revision. Todo en configuracion de prueba, sin movimientos.

Objetos nuevos: RH_NOM_VAR, RH_NOM_VAR_SEQ, RH_NOM_MOD_CTL, RH_NOM_MOD_VER,
RH_PKG_NOM_CFG y trigger de inmutabilidad. El FK al modelo SIS impide eliminar una
identidad con configuracion nueva referenciada; no borrar a ciegas desde el CRUD viejo.

Precondicion: el catalogo SIS referido debe existir en la misma base accesible por RH.
Se verifico su estructura en QA, donde las conexiones comparten base; si los perfiles
SIS/RH usan bases distintas en otro ambiente, detener y disenar el mapeo. No crear
DB links o duplicar el maestro para resolverlo implicitamente.

Compilacion y tests locales no certifican por si solos sintaxis/privilegios/transacciones
Oracle. En QA ya se ejecutaron los pasos de instalacion y pruebas mediante handlers:
35 comprobaciones aprobadas (33 principales y dos adicionales), sin nomina real.
API publicada en QA; 16 rechazos HTTP 401 aprobados. Falta prueba HTTP con sesion
SIS autorizada. Ver ValidacionConfiguracionQA.md
bajo el requerimiento 57 para detalle de fixtures y limites de esta evidencia.
La recuperacion de configuracion usa fragmentos VARCHAR2 de hasta 1000 caracteres
en un cursor ordenado, valida secuencia/longitud/hash y evita locators CLOB de salida.

## 10. Interfaz de configuracion publicada en QA

NextOssmasoft: src/rh/motorNomina, pagina /apps/rh/motor-nomina/. Variables con
paginacion/edicion/version esperada; borradores con seleccion de variables,
insercion de tokens, validacion de sintaxis y consulta de revision explicita.
Simulacion de revision guardada con entradas string y traza. Cambios de borrador
deben guardarse antes de simular. No hay calculo financiero en navegador.

Enlace inicial solo para superusuarios; otros usuarios requieren ruta/permisos SIS
existentes. Cache separada por identidad e invalidacion despues de guardar.
Frontend publicado en QA; no se asignaron permisos. Detalle en InterfazConfiguracion.md
del requerimiento 57. No se cambiaron endpoints ni scripts en esta entrega de UI.

## 11. Parametros simples por vigencia (extension publicada en QA)

Extension publicada en QA en el release acumulado. Backend desplegado antes del frontend.
Sin cambios SQL para esta extension; formato 3 probado posteriormente en Oracle.

POST capacidades, cuerpo {}, permiso rh.nomina.configuracion.leer, data
{formatosRevision:[1,2,3],parametrosVigentes:true,maxParametros:500,fuentesHistoricas:true,
cargoHistorico:true,referenciaHistorica:true,auditoriaConceptos:true,validacionUnidades:true}.

modelosSave admite parametros[] opcional: clave, codigoVariable, valor string,
desde (yyyy-MM-dd), hasta nullable y codigoTipoNomina nullable. Solo variables
PARAMETRO de la revision. Tipo especifico debe coincidir con el modelo; null es
ambito general del modelo. Fechas inclusivas desde 1900, hasta opcional; maximo
500 reglas y claves unicas. Se rechazan solapamientos en el mismo ambito.
No se convierten unidades/tasas implicitamente. Snapshot formato 2 cuando tiene
reglas; formato 1 previo sigue legible en API nueva y sus bytes no se reescriben.

POST modelosSimularVigente, permiso rh.nomina.simular, request hasta 64 KiB:
{modeloId,version,fechaReferencia,entradas:[{codigo,valor}]}.
Entradas solo ENTRADA; no acepta suplantar PARAMETRO/CALCULADA. Resuelve todos los
parametros para esa fecha: especifico del tipo > general. Sin valor vigente falla,
sin fallback a cero/manual/actual. Respuesta ResultDto.data con modeloId, version,
hash, fechaReferencia, parametros resueltos (clave/codigoVariable/valor/unidad/
ambito/desde/hasta/codigoTipoNomina) y simulacion con el contrato de traza anterior.
Errores esperados isValid=false y HTTP 200; 401/403 y limite 2 MiB conservados.
Sin paginacion ni escritura de resultados. modelosSimularVersion sigue manual.

Frontend exige capacidades compatibles y las vuelve a consultar antes de guardar
reglas. Un 404/contrato antiguo impide el guardado de reglas. La API antigua no
puede leer formato 2: considerar esta compatibilidad al preparar un rollback.
No hay tablas de tramos, grupos, excepciones por persona/cargo ni aprobacion en
esta entrega. Ejemplos: guardar-revision-vigencias.json y simular-vigencia.json.
Detalle y evidencia en ParametrosVigentes.md del requerimiento 57.

## 12. Contexto y fuentes historicas (disponible en QA)

Capacidades incluye fuentesHistoricas=true en la API publicada en QA. Nuevas rutas POST, JWT y rh.nomina.historico.leer:

- fuentesCatalogo, cuerpo {}, data codigo/unidad/descripcion/requiereDiaIso/version.
- historicoContexto, cuerpo {codigoTipoNomina,codigoPeriodo,fechaNomina}, limite 8 KiB.
- historicoPreparar, cuerpo {modeloId,version,codigoPeriodo,fechaNomina,fuentes:[{codigoVariable,proveedor,diaSemanaIso}]}, limite 64 KiB.

Fecha yyyy-MM-ddTHH:mm:ss sin Z/offset/fracciones. Empresa por settings; tipo de la
preparacion desde revision. El contexto debe ser unico, exacto y cerrado. Sin
paginacion, respuesta hasta 2 MiB. Fuentes solo ENTRADA, unidades compatibles,
maximo 200, sin SQL libre; dia ISO 1..7 solo para el proveedor que lo requiere.

Fuentes registradas CALENDARIO_ISO_V1: ANIO_FECHA_NOMINA y MES_FECHA_NOMINA (NUMERO),
DIAS_MES_NOMINA (DIAS) y DIA_ISO_MES_NOMINA (CANTIDAD). No equivale a dias trabajados
ni presupone equivalencia NLS con el paquete anterior. La seccion 13 agrega despues
una fuente individual de sueldo capturado, aun sin certificar origen del calculo.

historicoPreparar devuelve ResultDto.data con modeloId/version/hash, fuente
(contexto/cierre/presupuesto/contadores agregados), entradas (valor string, unidad,
proveedor/version/evidencia), entradasPendientes, parametros y bloqueos. isValid=true
indica diagnostico ejecutado. entradasResueltas no certifica nomina; estado siempre
DIAGNOSTICO_DE_ENTRADAS y paridadCertificada siempre false. Sin salarios ni IDs de
personas. Errores esperados 200/isValid=false; 401/403 conservados.

Pestana local Fuentes historicas. Bindings son pruebas de diagnostico sin guardar,
no configuracion de ejecucion. SP y API/UI publicados en QA.
Ver FuentesHistoricas.md y ResultadoFuentesQA.json. Sigue pendiente prueba HTTP con
sesion SIS autorizada, captura de baseline/entradas salariales y paridad real.

## 13. Snapshot individual de sueldo/cargo (disponible en QA)

capacidades agrega cargoHistorico=true; fuentesCatalogo agrega requiereCargo.
Proveedor SUELDO_CARGO_SNAPSHOT (DINERO, CARGO_SNAPSHOT_V1) exige persona/cargo
explicitos, sin seleccionar datos actuales ni ejecutar calculos legacy.

POST historicoCargoGet, permiso rh.nomina.historico.datos, limite request 8 KiB:
{codigoTipoNomina,codigoPeriodo,fechaNomina,codigoPersona,codigoRelacionCargo}.
Empresa de settings, fecha exacta sin zona. data: contexto, codigoPersona,
codigoRelacionCargo, codigoCargo, sueldo string, fuente, versionFuente y
origenDelCalculoVerificado=false. No incluye datos bancarios/nombres/cedula.

POST historicoPrepararPersona, mismo permiso, hasta 64 KiB:
{modeloId,version,codigoPeriodo,fechaNomina,codigoPersona,codigoRelacionCargo,fuentes}.
Tipo desde revision, response data {cargo,preparacion}; preparacion es el diagnostico
anterior. Entradas incluyen version de proveedor y persona/cargo cuando corresponde.
Usar sueldo de snapshot agrega ORIGEN_SNAPSHOT_PENDIENTE; entradasResueltas y
paridadCertificada no se aprueban sin verificar origen original de captura.

Sin paginacion; respuesta 2 MiB. Fallos esperados 200/isValid=false; 401/403
conservados. Cero/duplicados/mala precision fallan; no fallback a sueldo actual.
Pestana Fuentes historicas con ambos IDs opcionales para activar modo individual.
QA tiene SP_RH_NOM_CAR_HIST y API/UI publicados. No se asignan
permisos automaticamente. Ver CargoSnapshot.md y ResultadoCargoSnapshotQA.json.

## 14. Referencia independiente de resultados (disponible en QA)

capacidades agrega referenciaHistorica=true. POST historicoReferenciaGet con JWT y
rh.nomina.historico.datos: {codigoTipoNomina,codigoPeriodo,fechaNomina,codigoPersona}.
Empresa de settings; fecha Oracle exacta sin zona; contexto unico/cerrado. Request
8 KiB, respuesta 2 MiB, maximo 1000 partidas sin captura parcial ni paginacion servidor.

ResultDto.data: documento {formato:1,contexto,codigoPersona,partidas}, hash SHA256 del
documento JSON UTF-8 camelCase, fuente, observaciones, integridadEstructural e indicadores
entradasCompletas/clasificacionConceptosVerificada/cargoIdentificadoEnPartidas/
paridadCertificada siempre false. Partida: codigoHistorico, codigoConcepto,
frecuenciaId, tipoMovimiento F/V, estadoRegistro y monto string/null. No hay A/D,
netos ni modelos/entradas en el request. No se usa catalogo actual ni se agrega SUM.

isValid=true significa lectura/captura realizada; observaciones se preservan aunque
la estructura no este completa. Cero filas/exceso/contexto invalido fallan con
200/isValid=false. 401/403 conservados. No se persiste ni acepta captura editable del
cliente; hash no es firma ni autorizacion. Panel local en Fuentes historicas con
paginacion visual y sin conversion de montos a Number. SP ya instalado QA; extension
API/UI publicada en QA. Ver ReferenciaHistorica.md y ResultadoReferenciaQA.json.

## 15. Auditoria temporal de conceptos (disponible en QA)

capacidades agrega auditoriaConceptos=true. POST historicoConceptoGet con JWT y
rh.nomina.historico.leer: {codigoTipoNomina,codigoPeriodo,fechaNomina,codigoConcepto}.
Empresa de settings; fecha Oracle exacta, contexto unico/cerrado y concepto presente
en partidas de ese contexto. Request 8 KiB, respuesta 2 MiB, sin paginacion.

ResultDto.data: contexto/codigoConcepto/estado, auditoria {fechaEvento,eventosSinFecha,
totalCandidatos,imagenes hasta 3}, candidato nullable, observaciones, fuente,
versionMetodo OLD_POSTERIOR_V1. coberturaTemporalVerificada/disponibleParaCalculo/
paridadCertificada siempre false. isValid=true indica diagnostico, no elegibilidad.

Selecciona primera imagen OLD en o despues de la referencia; mismo segundo y empates
son ambiguos, sin imagen no toma catalogo actual. Tipo de nomina de la imagen no se
filtra antes de elegir fecha. Imagen de otro tipo o campos incompletos impiden candidato.
Sin DML ni conversion a reglas/partidas. Fallos esperados 200/isValid=false; 401/403
conservados. UI permite consultar un concepto de la referencia sin aplicarlo.
SP_RH_NOM_CON_HIST y API/UI publicados en QA. Ver ConceptosHistoricos.md.

## 16. Unidades declaradas y formato estricto (disponible en QA)

capacidades agrega validacionUnidades=true y formatosRevision incluye 3.
modelosSave agrega validarUnidades=false por compatibilidad. Con true, grafo y unidades
de catalogo se validan antes de guardar; snapshot formato 3/versionUnidades DIMENSIONES_V1.
Al leer/simular se vuelve a validar. Version no soportada/formato incoherente falla.
Revisiones anteriores omiten versionUnidades y no se convierten automaticamente.
No hay SQL nuevo; lector antiguo no admite formato 3, considerar rollback.

POST unidadesValidate, permiso rh.nomina.formulas.validar:
{variables:[{codigo,unidad,origen}],formulas:[{variableSalida,expresion}]}.
1..200 variables, 1..100 formulas; request 512 KiB, respuesta 2 MiB, sin paginacion.
data: estado UNIDADES_DECLARADAS_COMPATIBLES, versionMetodo, formulas con unidadDeclarada/
dimensionInferida/normalizada y errores. Fallos 200/isValid=false con diagnostico por
variable y sin resultados parciales. JWT 401/permisos 403 conservados. No consulta RH
ni acredita que las unidades declaradas correspondan al catalogo real.

Dinero/dias/cantidad distintos; NUMERO/FACTOR/TASA adimensionales. PORCENTAJE tiene
representacion distinta. Sum/resta/MIN/MAX requieren compatibilidad; cero literal
neutro permitido. Multiplicacion/division combinan dimensiones; ROUND/TRUNC exigen
escala adimensional y mantienen dimension del importe. No se validan valores de
escala hasta evaluar. No se distingue moneda ni dinero mensual/diario en esta etapa.

Nuevas funciones numericas: TASA divide por 100 y PORCENTAJE multiplica por 100.
En modo dimensional TASA exige PORCENTAJE, PORCENTAJE exige adimensional. Ejemplo:
variable PORCENTAJE de valor 1 -> TASA([PORC]) = 0.01. No hay conversion implicita.
Guardarlas en revisiones exige validarUnidades=true. Expresiones de prueba sin catalogo
siguen siendo solo pruebas numericas/sintacticas, no dimensionales.

UI muestra control de validacion, metodo de la revision y resultados solo si el payload
no cambio. Nuevos borradores optan por validar; los cargados conservan su politica.
Servicio comprueba capacidad actual antes del guardado estricto. API/UI publicados en QA.  Ver ValidacionUnidades.md; 136 pruebas backend y 21 frontend aprobadas.

## 17. Condiciones numericas (formato 4)

Contrato detallado y ejemplos: `Requerimientos/57 - Motor Parametrizable De Calculo De Nomina/CondicionesNumericas.md`.
`modelosSave` incorpora `VersionCondicionesCliente=CONDICIONES_NUMERICAS_V1`.
Cada formula admite `Condicion` y `ExpresionAlternativa`; ambas son requeridas juntas.
Las condiciones requieren `ValidarUnidades=true`. `capacidades` anuncia formato 4,
`CondicionesNumericas` y `VersionCondiciones`. No hay rutas ni permisos nuevos.
La respuesta de simulacion agrega `Aplicabilidad` con rama y trazas de comparacion.
No se usa cero implicitamente ni se modifican movimientos. Fechas/texto/booleanos
siguen fuera de este primer contrato. Verificar estado de publicacion en PublicacionQA.md.

## 18. Tablas de tramos (formato 5)

POST modelosSave agrega TablasTramos[] y VersionTramosCliente=TRAMOS_NUMERICOS_V1,
con ValidarUnidades=true. Cada tabla selecciona valor de PARAMETRO por ENTRADA,
vigencia y tipo de nomina. Rangos numericos [Desde, Hasta), limites null abiertos;
fechas de vigencia inclusivas. No escalas progresivas ni fallback en huecos.
modelosSimularVigente conserva su request y agrega Tramo opcional a parametros[].
capacidades anuncia TablasTramos, VersionTramos y formato 5. No rutas/permisos nuevos.
Contrato completo, ejemplos, limites y errores en Requerimientos/57 - Motor
Parametrizable De Calculo De Nomina/TablasTramos.md. Estado QA en PublicacionQA.md.

## 19. Simulacion por lote

POST /api/RhMotorNomina/modelosSimularLote, permiso rh.nomina.simular. ModeloId,
Version y Casos[] con CasoId, FechaReferencia y solo ENTRADA por caso. Maximo 50
casos/1000 valores, sin paginacion, request 512 KiB y respuesta 2 MiB. Datos numericos
string. Un error invalida el lote sin resultados parciales. Respuesta ResultDto con
hash/modelo, casos y trazas o errores por caso; siempre ParidadCertificada=false y
MovimientosAplicados=false. No suma netos ni consulta datos personales adicionales.
capacidades anuncia SimulacionLote, VersionLote=SIMULACION_LOTE_V1 y MaxCasosLote.
Contrato y ejemplos: Requerimientos/57 - Motor Parametrizable De Calculo De
Nomina/SimulacionLote.md. Estado QA en PublicacionQA.md.

## 20. Comparacion diagnostica de prueba

POST historicoCompararPrueba: ModeloId/Version, CodigoPeriodo/FechaNomina exacta,
CodigoPersona, HashReferenciaEsperado de captura previa, Entradas y Asociaciones
(VariableSalida CALCULADA/DINERO, CodigoConcepto, FrecuenciaId, TipoMovimiento F/V).
Exige ambos permisos rh.nomina.historico.datos y rh.nomina.simular. Sin paginacion,
request 128 KiB, respuesta 2 MiB; decimales string. Hash/contexto cambiado, estructura
invalida o error de calculo bloquean sin resultados parciales. Se devuelven filas
firmadas, pendientes y trazas; no totales A/D/neto ni certificacion. Coincidir importes
no significa paridad. Capacidad ComparacionPrueba/COMPARACION_PRUEBA_V1.
Contrato completo: Requerimientos/57 - Motor Parametrizable De Calculo De
Nomina/ComparacionDiagnostica.md. Estado QA en PublicacionQA.md.

## 21. Reglas de concepto (formato 6) y fuentes actuales

conceptosCatalogo: POST CodigoTipoNomina/PageNumber/PageSize/SearchText, permiso
configuracion.leer; metadata actual paginada con Huella/Elegible. modelosSave agrega
ReglasConcepto[] y VersionReglasCliente=REGLAS_CONCEPTO_V1; unidades estrictas,
politicas explicitas y metadata recapturada. Formato 6 conserva esa metadata y
protege frente a clientes antiguos. ProyeccionConceptos en simulacion vigente/lote/
actual propone acciones sin escribir ni certificar clasificacion historica.

fuentesActualesCatalogo: lista cerrada. simularContextoActual: modelo/revision,
periodo/fecha/presupuesto/persona/relacion y Fuentes, PoliticaPresupuesto obligatoria
(COINCIDIR_PERIODO o TIPO_ACTUAL_EXPLICITO) y motivo si difieren. Permisos
rh.nomina.fuentes.datos y rh.nomina.simular; no se asignan a usuarios automaticamente.
No recibe sueldo manual. Cierre, contexto, vigencia, duplicados, unidad o fuentes
incompletas bloquean. Devuelve fuente/huella/entradas y simulacion, sin movimientos.
Ver ReglasConcepto.md y FuentesActuales.md en el requerimiento para contratos completos.

## 22. Corridas persistentes de simulacion

Requiere `RhMotorNomina:CorridasEnabled=true`, objetos de `Sql/03_RH_NOM_RUN.sql`
instalados y worker registrado. Capacidades: `corridasSimulacion` y
`versionCorridas=CORRIDAS_SIMULACION_V1`. Predeterminado deshabilitado.
Todas las operaciones son POST bajo `/api/RhMotorNomina`, JWT/SIS activo,
empresa desde settings y alcance restringido al actor autenticado. Permiso base:
`rh.nomina.simular`; crear/ver detalle ACTUAL requiere tambien
`rh.nomina.fuentes.datos`, COMPARACION_PRUEBA requiere `rh.nomina.historico.datos`.
No se usa el token OracleAnalysis. No se aplican movimientos ni se certifica paridad.

| Operacion | Body camelCase | Datos |
| --- | --- | --- |
| corridasCrear | idempotencyKey + exactamente uno: lote, actual, comparacion | Corrida pendiente o existente |
| corridasGetAll | pageNumber (1..100000), pageSize (1..100) | Corrida[] propias, mas page/totalPage/cantidadRegistros |
| corridasGet | idRun positivo | Cabecera propia |
| corridasCancelar | idRun, versionEsperada positivos | Cabecera con cancelacion solicitada/terminal |
| corridasDetalle | idRun, modo exacto | corrida, entrada congelada, resultado nullable |

Ejemplo de creacion (lote respeta el contrato de simulacion por lote):

```json
{"idempotencyKey":"UI_e318a59c","lote":{"modeloId":1,"version":7,"casos":[{"casoId":"A","fechaReferencia":"2025-06-30","entradas":[{"codigo":"BASE","valor":"1300"}]}]}}
```

`actual` usa el body completo de simularContextoActual; `comparacion` usa el de
historicoCompararPrueba, incluida la huella esperada. Clave: ASCII alfanumerico,
`:`/`_`/`-`, 1..80 caracteres. Misma clave/actor/empresa/contenido devuelve la misma
corrida sin recapturar. Contenido distinto falla; conservar clave al reintentar
ante una respuesta de red incierta. No reintento automatico en frontend.

Respuesta envuelta en ResultDto: `{data,isValid,message,page,totalPage,cantidadRegistros}`.
Cabecera `data`: idRun, codigoEmpresa, actor, modeloId, versionModelo, modo, estado,
idempotencyKey, hashSolicitud, hashDatos, hashResultado nullable, mensaje nullable,
version, cancelar, intentos, fechaIns/fechaInicio/fechaFin y movimientosAplicados=false.
Estados: PENDIENTE, EN_CURSO, COMPLETADO, ERROR, CANCELADO. Cancelar EN_CURSO solicita
cancelacion; esperar terminal. Conflictos de version y propiedad son IsValid=false.

Detalle devuelve `entrada` con formato=1, modelo completo, contexto/fuentes/entradas
congelados y referencia cuando corresponde. `resultado` es nullable hasta finalizar;
contiene modo, isValid, lote/actual/comparacion segun modalidad, errores opcionales,
movimientosAplicados=false y paridadCertificada=false. Modalidad ajena no entrega
documentos. Hash/tamano/fragmentos/contexto invalidos rechazan el detalle.
Entradas maximo 4 MiB; resultado 2 MiB; body HTTP crear 512 KiB.

La interfaz agrega Corridas (paginacion servidor, refresco 10 segundos, detalle y
cancelacion optimista), y Guardar y ejecutar simulacion en lote, contexto actual y
comparacion. No presentar una corrida COMPLETADO como nomina aplicada.

## 23. Acumuladores actuales (fuentes version 2)

`fuentesActualesCatalogo` agrega ACUMULADOR_MOV_ACTUAL para ENTRADA/DINERO.
Capacidad `versionFuentesActuales=FUENTES_ACTUALES_V2`; cliente anterior debe
refrescar antes de simular contextos nuevos. Mismos endpoints y permisos de fuentes
actuales; no se agrega SQL arbitrario ni acceso historico por esta modalidad.

Binding de ejemplo en `fuentes` de simularContextoActual/corridasCrear.actual:

```json
{"codigoVariable":"BASE_SSO","proveedor":"ACUMULADOR_MOV_ACTUAL","acumulador":{"conceptos":[1,2,3],"estados":["A"],"tiposMovimiento":["F","V"],"politicaFrecuencias":"TODAS","frecuencias":[],"sinFilas":"ERROR","conceptosExcluidos":[3]}}
```

Se exige `acumulador` solo para ese proveedor. ValorSiNulo y DiaSemanaIso no se
admiten en ese binding. Todas las validaciones y limites constan en AcumuladoresActuales.md.
Cada entrada resuelta agrega `acumulacion` nullable (se omite si no aplica): contexto,
definicion normalizada, filas [{partida,incluida,motivo}], total decimal textual y hash.
Partida: id, codigoEmpresa/tipo/persona/concepto, frecuenciaId, tipoMovimiento, estado,
complemento y monto textual nullable. El motor solo suma partidas incluidas y rechaza
sus importes/frecuencias invalidos. Retiene filas excluidas como evidencia.

No se filtra RH_MOV_NOMINA por un periodo inexistente en esa tabla. El periodo del
contexto debe estar abierto; no garantiza pertenencia temporal de cada movimiento.
Los factores mensuales/quincenales son formulas/parametros, nunca multiplicadores
ocultos del proveedor. Ninguna salida nueva de la corrida entra en esa lectura.

## 24. Proveedores guardados en el modelo (formato 7)

`modelosSave` agrega `fuentesActuales` (bindings completos del contrato anterior) y
`versionFuentesCliente=FUENTES_MODELO_V1`. Requiere capacidad fuentesModelo=true,
versionFuentesModelo coincidente y formatosRevision que incluya 7. Si se configura
una fuente, debe existir exactamente una por cada variable ENTRADA y ninguna para
PARAMETRO/CALCULADA; unidades estrictas obligatorias. Se validan y normalizan
proveedores, valores por defecto, dia ISO y definiciones de acumuladores al guardar.

La revision congela `configuracion.fuentesActuales` y `versionFuentesModelo` con su
hash. Coexiste con condiciones, tablas y reglas. Formatos 1..6 conservan su forma y
hash; los nuevos campos nulos se omiten. Un cliente que no reconozca proveedores no
puede eliminarlos al editar. Para quitarlos en una nueva revision se exige ACK de
version y lista vacia explicita.

`simularContextoActual.fuentes` admite null/lista vacia si la revision ya define
fuentes; el servidor usa las guardadas. Si el cliente envia una lista, debe coincidir
semanticamente con la normalizada del modelo. Cambiar origen, conceptos, filtros o
defaults exige otra revision. Los modelos antiguos sin fuentes siguen admitiendo
bindings de prueba por solicitud. Empresa, persona, periodo, fecha, presupuesto y
relacion siguen siendo contexto de ejecucion, no valores copiados del modelo.

La interfaz permite configurar proveedores en Modelos y simulacion, los recupera al
cargar la revision y bloquea su edicion en Fuentes actuales cuando vienen del modelo.
Esto no convierte una simulacion manual en paridad certificada ni publica el modelo
para liquidacion operativa.

## 25. Tasas y topes de concepto (fuentes actuales V3, esquema modelo V2)

Versiones actuales: FUENTES_ACTUALES_V3 y FUENTES_MODELO_V2. Se siguen leyendo
revisiones formato 7/esquema V1 sin modificar sus hashes. Para editar se exige ACK
V2; las revisiones V1 no pueden declarar parametroConcepto. Capacidad de formatos
sigue incluyendo 1..7. No cambian las rutas ni los permisos de fuentes actuales.

Ejemplo de binding de modelo o solicitud de prueba sin fuentes guardadas:

```json
{"codigoVariable":"TASA_SSO","proveedor":"PARAM_CON_PORCENTAJE","parametroConcepto":{"codigoConcepto":25,"politicaVigencia":"HASTA_NULA"}}
```

Se exige `parametroConcepto` solo en PARAM_CON_PORCENTAJE/PATRONAL/TOPE/EXTRA2/EXTRA3.
Las unidades son PORCENTAJE/PORCENTAJE/DINERO/NUMERO/NUMERO respectivamente. No se
admiten default, dia ISO o acumulador en esos bindings. Politicas FECHA_CONTEXTO o
HASTA_NULA; empresa/tipo/fecha vienen del contexto validado. Maximo 20 pares
concepto/politica distintos, con cache por par dentro de cada lectura de contexto.

La entrada resuelta agrega `parametroConcepto={datos,hash}` nullable. Datos: id,
codigoEmpresa/codigoTipoNomina/fechaNomina/codigoConcepto, politicaVigencia,
fechaDesde/fechaHasta, porcentaje/montoTope/porcentajePatronal/extra2/extra3/tipoSueldo,
origen=RH_FORMULA_CONCEPTOS, versionFuente=PARAMETROS_CONCEPTO_V1 y
estadoHistoricoVerificado=false. Montos/porcentajes son textos, nulls conservados.
El campo seleccionado debe ser decimal valido, sin sustituciones. Ausencia,
duplicidad, contexto ajeno, vigencia incompatible o huella alterada rechazan.

La interfaz permite elegir concepto/politica en el editor de fuentes del modelo o
en fuentes de prueba. El porcentaje se usa con TASA en la expresion; nunca se divide
por cien implicitamente al leer. Esto no aprueba ni publica reglas para liquidacion.

## 26. Recalculo historico desde fuentes

POST `/api/RhMotorNomina/historicoRecalcular`, limite de body 8 KiB. JWT/SIS y ambos
permisos historico.datos + simular. Capacidad recalculoHistorico=true y
versionRecalculoHistorico=RECALCULO_HISTORICO_V1. Empresa desde settings.

```json
{"modeloId":1,"version":10,"codigoPeriodo":5,"fechaNomina":"2025-06-30T00:00:00","codigoPersona":123,"codigoRelacionCargo":456,"politicaFuentes":"EQUIVALENTES_GUARDADOS","comparar":false}
```

Si comparar=true agregar hashReferenciaEsperado SHA-256 de historicoReferenciaGet.
No se admiten entradas manuales ni asociaciones por solicitud. La revision debe tener
fuentes guardadas y, para comparar, reglas de conceptos. Periodo cerrado, fecha/hora
exactas, cargo unico y vigencia de parametros FECHA_CONTEXTO obligatorios.

ResultDto.data: modeloId/version/hashModelo, contexto cerrado, cargo historico,
entradas [{codigoVariable,valor,unidad,proveedorModelo,proveedorUsado,evidencia,
huellaFuente,parametroConcepto?}], hashEntradas, simulacion, comparacion nullable,
observaciones[], origenEntradas=FUENTES_HISTORICAS_RECONSTRUIDAS, versionMetodo,
paridadCertificada=false, movimientosAplicados=false. Comparacion usa las reglas
guardadas y el contrato previo de filas/diferencias/sinAsociar; su origen de entradas
se marca FUENTES_HISTORICAS_RECONSTRUIDAS. No cambia los resultados manuales previos.

Fuentes admitidas: sueldo snapshot, calendario ISO, tipo nominal del contexto y los
cinco campos de parametro de concepto con FECHA_CONTEXTO. Sin equivalente historico,
ausencia/duplicidad, datos alterados, fallo aritmetico o referencia cambiada retornan
IsValid=false; no se sustituyen por lecturas actuales o importes esperados.
La interfaz agrega Recalculo historico, captura independiente y opcion de comparar.
No tiene paginacion propia: una persona/relacion/contexto por solicitud, con limites
del modelo/referencia existentes. Esta operacion no crea una corrida persistente nueva.

## 27. Matriz historica por tipo

POST `/api/RhMotorNomina/historicoRecalcularLote`, JWT/SIS y ambos permisos
historico.datos + simular. Body maximo 64 KiB. Capacidad matrizHistorica=true,
versionMatrizHistorica=MATRIZ_HISTORICA_V1, maxCasosMatriz=20.

```json
{"tiposEsperados":[10,12],"casos":[{"casoId":"A","solicitud":{"modeloId":1,"version":10,"codigoPeriodo":5,"fechaNomina":"2025-06-30T00:00:00","codigoPersona":123,"codigoRelacionCargo":456,"politicaFuentes":"EQUIVALENTES_GUARDADOS","comparar":true,"hashReferenciaEsperado":"SHA256_DE_64_CARACTERES"}}]}
```

1..20 casos y tipos positivos/distintos. Casos con id normalizado unico y sin repetir
modelo/version/contexto/persona/cargo. Cada comparacion exige su hash. Tipo se toma
del modelo, nunca del cliente; tipos ajenos a esperados producen error por caso.
Tiempo total 60 segundos y maximo 10000 pasos contando la reevaluacion de comparacion.
No se aceptan diccionarios de entradas manuales. No aplica movimientos ni persiste
una nueva corrida; informe diagnostico resumido con hashes de cada caso.

ResultDto.data: estado, tiposEsperados, tipos[], casos[], muestreoCompleto,
coincidenciaCompleta, hashLote, versionMetodo, paridadCertificada=false y
movimientosAplicados=false. Cada tipo informa casos, nominasReconstruidas,
nominasComparadas, casosConError/casosCoincidentesCompletos, partidasSinAsociar,
salidasSinAsociar y muestraSuficiente. Nomina distinta = periodo+fecha dentro del tipo;
varias personas de una misma nomina no aumentan ese conteo. Muestra suficiente exige
al menos dos nominas distintas comparadas. Tipos esperados ausentes se muestran con cero.

Casos: id/modelo/version/tipo nullable, contexto/persona/cargo, comparado,
coincideCompleto, coincidentes/diferencias/sinAsociar, hashEntradas/hashReferencia y
error nullable. Coincidencia completa exige claves/importes coincidentes y cobertura
sin partidas/salidas omitidas; nunca activa certificacion. Errores por caso se retienen:
IsValid=true significa informe evaluado; revisar estado DIAGNOSTICO_CON_ERRORES o
MUESTREO_INCOMPLETO. Errores estructurales/limites de matriz son IsValid=false.

UI: agregar contextos con referencia capturada, declarar tipos esperados, quitar
casos y evaluar. Se advierte antes de salir con lote preparado. Los resultados indican
cobertura/errores/coincidencias sin sustituir las pruebas de reglas migradas completas.

## 28. Controles de omision actuales

Capacidades de fuentes actuales FUENTES_ACTUALES_V4; proveedores modelo
FUENTES_MODELO_V3. Formatos de revision 1..7. Se conservan lectores/hash de V1/V2,
pero esos esquemas no admiten el campo control. Editar exige ACK V3.
Rutas y permisos de simulacion/corridas actuales permanecen iguales.

```json
{"codigoVariable":"OMITIR_SSO","proveedor":"CONTROL_OMITIR_CONCEPTO","control":{"codigoConcepto":25,"alcance":"MODULO_TIPO","ausencia":"NO_OMITIR","valorNulo":"ERROR"}}
```

ENTRADA NUMERO, exactamente un binding por variable. control obligatorio solo en ese
proveedor; no admite parametroConcepto, acumulador, default generico o dia ISO.
Alcance CONCEPTO/MODULO_TIPO/MODULO_EMPRESA y politicas ERROR/NO_OMITIR explicitas.
Concepto objetivo pertenece a empresa y tipo del modelo. Ausencia/nulo no equivalen
implicitamente a cero; un valor informado distinto de 0/1 falla.

Entrada resuelta agrega control nullable: codigoEmpresa/tipo/periodo/fecha/persona,
definicion, moduloObjetivo, filas [{id,codigoEmpresa,codigoPersona,codigoConcepto,
codigoTipoNomina,moduloId,controlAplica,fechaIns,fechaUpd}], omitir, hash, origen,
versionMetodo=CONTROLES_OMISION_V1 y estadoHistoricoVerificado=false.
CONTROL_APLICA y valor numerico de entrada se conservan como textos. La entrada vale
1 si cualquier fila elegible pide omision; de lo contrario 0. No se generan importes.

La UI agrega editor de concepto/alcance/politicas en modelos y fuentes de prueba, y
muestra decision, filas y huella. Condiciones numericas existentes configuran el uso
de la bandera. Sin datos historicos de controles no se completa historicoRecalcular
usando controles actuales. No se aplican/revierten movimientos en esta entrega.

## 29. Acumulador historico de meses anteriores (R57)

Proveedor `ACUMULADOR_HIST_MESES_ANT`, unidad `DINERO`, ENTRADA. Sin endpoint adicional:
`POST api/RhMotorNomina/modelosSave`, `simularContextoActual`, `historicoRecalcular`
y corridas actuales reutilizan sus contratos/permisos. Empresa proviene de settings.
`versionFuentesCliente=FUENTES_MODELO_V4`; capacidades anuncian fuentes actuales V5
y recalculo historico V2. Formato 7 mantiene lectores de V1/V2/V3 y hashes previos.

Binding de una entrada (sin parametros/control/acumulador actual adjuntos):

```json
{
  "codigoVariable": "BASE_ANTERIOR",
  "proveedor": "ACUMULADOR_HIST_MESES_ANT",
  "acumuladorHistorico": {
    "mesesAnteriores": 2,
    "filtros": {
      "conceptos": [25, 26], "conceptosExcluidos": [26],
      "estados": ["A"], "tiposMovimiento": ["F", "V"],
      "politicaFrecuencias": "LISTA", "frecuencias": [20],
      "sinFilas": "ERROR"
    }
  }
}
```

Ventana [primer dia mes objetivo - meses, primer dia mes objetivo), gregoriana.
Excluye periodo/mes objetivo, no incluye resultados recien calculados. Hasta 24 meses,
2000 filas/definicion, diez definiciones y 5000 filas historicas por contexto.
Periodo de partida ausente/duplicado/no cerrado y datos ajenos fallan; no se ignoran.
No se multiplica por frecuencia ni se cambia signo. Cero vacio requiere CERO_EXPLICITO.

Respuesta ResultDto existente, sin paginacion nueva: `entradas[].valor` texto decimal
y `entradas[].acumulacionHistorica` con contexto, definicion, desde/hastaExclusiva,
partidas (periodo/fecha/cierre/partida original), calculo (filas con motivos/total),
hash, origen, versionMetodo y banderas false de certificacion/salidas de corrida.
En recalc historico se conserva tambien `huellaFuente`; en corridas ACTUAL el worker
verifica integridad y valor antes de usar el snapshot. Errores esperados retornan
`isValid=false` y mensaje espanol. No interpretar HTTP 200 como validacion financiera.

La revision fija filtros; cambiar ventana/conceptos requiere revision nueva. No es
un equivalente automatico del acumulador legacy del mismo mes ni un certificado
historico. Ver `AcumuladoresHistoricos.md` y `PlanCierre.md` para alcances restantes.

## 30. Grupos de acumulacion capturados por revision

Dos operaciones POST protegidas por `rh.nomina.configuracion.leer`, empresa de settings:

- `api/RhMotorNomina/gruposAcumulacionCatalogo`: body `{"codigoTipoNomina":10}`.
  `data` lista `{tipoAcumuladoId:7,codigo:"SSO"}`. Hasta 200, sin paginacion/truncamiento.
  La lista orienta; no acredita asociaciones validas. Campos de paginacion ResultDto
  permanecen en sus valores por defecto, no representan una pagina parcial.
- `api/RhMotorNomina/grupoAcumulacionCapturar`: body del ejemplo siguiente; `data`
  contiene contexto, grupo/codigo, politica/fecha, filas, conceptos y hash.

```json
{
  "codigoTipoNomina": 10,
  "tipoAcumuladoId": 7,
  "politicaVigencia": "FECHA_CONTEXTO",
  "fechaReferencia": "2025-01-31T00:00:00"
}
```

Politicas FECHA_CONTEXTO inclusiva (simulacion en la fecha exacta capturada) o
HASTA_NULA (solo FECHA_HASTA nula, sin inferir condicion sobre FECHA_DESDE).
Recalculo cerrado rechaza HASTA_NULA. Ambos conservan EstadoHistoricoVerificado=false.

`acumulador.grupo` y `acumuladorHistorico.filtros.grupo` son opcionales y contienen
la captura completa: codigoEmpresa, codigoTipoNomina, tipoAcumuladoId, codigo,
politicaVigencia, fechaReferencia, filas (id, empresa, concepto, tipoAcumuladoId
original, conceptosCoincidentes, desde/hasta, conceptoAsociado, extra1/2/3), conceptos
ordenados, hash, origen, versionMetodo y resolucionDinamica=false. Los conceptos del
filtro deben coincidir exactamente con `grupo.conceptos`; use conceptosExcluidos para
exclusiones. Desvincular grupo conserva ids manuales sin acreditar origen de grupo.

`modelosSave` reconsulta Oracle y compara hash: recapture si hubo cambios, incluso
si el cliente recalculo un hash falso. La revision y sus corridas usan la captura
congelada; no cambian silenciosamente al editar el catalogo. Fuentes actuales manuales
sin revision configurada tambien revalidan antes de simular. Grupo vacio, mas de 100
asociaciones, ids/conceptos duplicados o empresa nula/ajena fallan con isValid=false;
no se entrega un subconjunto truncado ni se asigna empresa implicita.

Capacidades: gruposAcumulacion=true, versionGruposAcumulacion=GRUPOS_ACUMULACION_V1.
Proveedor modelo V5, fuentes actuales V6 y recalc historico V3; formato 7 conserva
lectores/hash de proveedores V1..V4, que no admiten grupos nuevos. Cliente antiguo
no puede editar silenciosamente esta configuracion. Respuestas siguen ResultDto,
HTTP 200 para resultados del handler; autenticar/permisos por controlador existente.
No implica publicacion de reglas, certificacion de paridad o aplicacion de movimientos.

## 31. Variables/condiciones tipadas, formato 8

`variablesSave` admite Tipo NUMERO/FECHA/BOOLEANO/TEXTO; tipos no numericos exigen
Unidad igual al Tipo y Origen ENTRADA/PARAMETRO. CALCULADA solo NUMERO. Mismos endpoint,
permisos, id/version y ResultDto; lista conserva paginacion. No hay endpoint SQL libre.

Valores siguen siendo strings, por ejemplo fecha "2024-02-29", flag "true" y texto
"001". No enviar JSON booleano para un campo Valor string. FECHA es yyyy-MM-dd desde
1900, sin hora/zona; BOOLEANO true/false exactos; TEXTO 1..100 letras/numeros, espacios
internos o _ . / -, ordinal y sensible a mayusculas. Nulo/vacio/tipo incompatible falla.
NUMERO conserva precision decimal y conversion de porcentaje solo por funciones propias.

```json
{
  "operador": "TODAS",
  "condiciones": [
    {"operador":"MAYOR_IGUAL","variable":"FECHA","unidad":"FECHA","valores":["2024-02-29"]},
    {"operador":"IGUAL","variable":"FLAG","unidad":"BOOLEANO","valores":["true"]},
    {"operador":"EN","variable":"SIGLAS","unidad":"TEXTO","valores":["E","AN"]}
  ]
}
```

Condiciones V2 conservan limites y alternativas obligatorias. BOOLEANO/TEXTO solo
IGUAL/DISTINTO/EN; fechas admiten orden/ENTRE inclusivo. Valores de igual tipo al
comparar variables; no coerces fecha/string/bool a NUMERO. UnidadesValidate recibe
`variables[].tipo` opcional, NUMERO por defecto. Tipos no numericos no tienen dimension
aritmetica y son rechazados si aparecen en una formula monetaria.

FormatosRevision incluye 8; capacidades variablesTipadas/versionVariablesTipadas=
VALORES_TIPADOS_V1, versionCondiciones=CONDICIONES_TIPADAS_V2. `modelosSave` exige unidades
validadas/cliente V2 para modelos tipados; proveedores modelo V6, fuentes actuales V7.
Lectores preservan revisiones numericas previas/condiciones V1 sin cambiar sus hashes.
Parametros simples pueden ser tipados; tablas de tramos siguen siendo numericas.

Fuentes: FECHA_NOMINA_CONTEXTO, FECHA_INICIO_RELACION, TIENE_PRECIERRE_CONTEXTO,
SIGLAS_TIPO_NOMINA_ACTUAL, con unidades FECHA/FECHA/BOOLEANO/TEXTO respectivamente.
Sin default de inicio de relacion; flag precierre no autoriza ejecutar. Historico solo
incorpora fecha del periodo cerrado, no rellena siglas/precierre/ingreso actuales.
En corridas ACTUAL, valores tipados deben coincidir con la fuente congelada.

Lote/comparacion manual tipados agregan `versionValores:"VALORES_TIPADOS_V1"`;
cuando falta, conserva perfil numerico/canonizacion previos. VersionLote y Comparacion
V2; recalculo historico V4. `modelosSimularVersion`/`modelosSimularVigente` resuelven tipo
desde la revision. Mismos limites/ResultDto/paginacion existentes, sin nuevos campos
de empresa. Casos invalidos rechazan el lote completo y no entregan un resultado parcial.

## 32. Validacion persistida y estados de revision

Flag de ambiente `RhMotorNomina:VersionesEnabled=false` por defecto. Instalar
`05_RH_NOM_ESTADOS.sql` y habilitar en QA antes de usar. Capacidades anuncian
`estadosRevision` y `versionEstadosRevision=VALIDACION_REVISION_V1`. Habilitar el
workflow no habilita liquidacion ni certifica pruebas.

POST `api/RhMotorNomina/`:

| Ruta | Body | Permiso |
| --- | --- | --- |
| modelosEstadoGet | ModeloId, Version | rh.nomina.configuracion.leer |
| modelosValidarRevision | ModeloId, Version, HashModeloEsperado, VersionEstadoEsperada, IdCorridas[], Motivo | rh.nomina.validar |
| modelosPublicarRevision | ModeloId, Version, HashModeloEsperado, VersionEstadoEsperada, HashValidacionEsperado, Desde, Hasta?, Motivo | rh.nomina.publicar |
| modelosRetirarRevision | ModeloId, Version, HashModeloEsperado, VersionEstadoEsperada, Motivo | rh.nomina.publicar |
| modelosAuditoriaEstados | ModeloId, Version, PageNumber=1, PageSize=20 | rh.nomina.configuracion.leer |

Actor SIS/JWT y empresa de settings; no enviarlos. DTOs de mutacion rechazan campos
no registrados, incluidos publicable/paridadCertificada/actor/evidencia arbitraria.
Motivo 10..500 caracteres; hashes SHA-256 exactos; version de estado inicialmente 0.
IdCorridas 1..20 positivas/distintas, propias del validador y completadas para mismo
modelo/revision/hash. Cada documento/resultado se verifica en servidor. Un fallo
no deja estado o auditoria parcial.

Respuesta `ResultDto<EstadoRevision>`: modeloId, version, hashModelo, estado
BORRADOR/VALIDADO/PUBLICADO/RETIRADO, versionEstado, autorConfiguracion, autorValidacion,
autorPublicacion, fechas administrativas, desde/hasta, evidencia?, motivo, publicable,
movimientosAplicados=false. Evidencia contiene referencias de corridas/hashes, nivel,
puertasPendientes, hash, versionMetodo y banderas; no duplica salarios/entradas.
Los hashes y el contador del estado deben volver a enviarse al mutar.

La validacion actual es TECNICA: publica `publicable=false` porque comparaciones
existentes no certifican paridad integral y no hay adaptador transaccional habilitado.
La UI muestra esas puertas, no lo representa como nomina conciliada. Publicar exige
validacion integral publicable, publicador distinto del configurador y fechas ISO
inclusivas sin solapamiento por empresa/tipo/escenario, incluso entre modelos distintos.
El validador puede aprobar si es distinto del configurador; no se exigen tres personas.
No hay flag cliente/setting que convierta evidencia tecnica en certificacion.

Retirar solo VALIDADO/PUBLICADO; conserva modelo, pruebas y auditoria, no revierte
movimientos. RETIRADO no se revalida/reactiva; crear otra revision. Las simulaciones
explicitas/replay conservan acceso segun sus permisos, incluso de revisiones retiradas.
ModelosVersionGet informa estado actual con workflow habilitado; snapshots anteriores
no se reescriben. Ningun DTO Estado enviado por cliente autoriza ejecucion.

Auditoria es append-only, orden descendente por IdEvento, campos accion/estados/actor/
fecha/motivo/hashValidacion. ResultDto con page, totalPage y cantidadRegistros; pagina
1..100000, size 1..100. Conflictos de hash/version/estado devolven isValid=false; cliente
recarga. Para leer una nueva pagina, usar numero basado en 1; sin exitos parciales.


### Snapshot de cargo V2 y recalc historico V5

`POST api/RhMotorNomina/historicoCargoGet` conserva el request existente
`{codigoTipoNomina,codigoPeriodo,fechaNomina,codigoPersona,codigoRelacionCargo}`.
En `data` agrega `frecuenciaPagoId: number|null`, `fechaIngreso: string|null`
y `versionFuente: "CARGO_SNAPSHOT_V2"`. Los documentos previos V1 siguen legibles.
Los campos nuevos nulos se omiten; no hay paginacion en esta lectura individual.
`fechaNomina` mantiene `yyyy-MM-ddTHH:mm:ss` sin zona, con hora Oracle exacta.

Desde el perfil `RECALCULO_HISTORICO_V5` (conservado en V6) se permite reconstruir la frecuencia de una fuente
`FRECUENCIA_PAGO_ACTUAL` desde el snapshot cerrado. Valores nulos/no positivos
producen `isValid:false`; no se sustituyen por datos actuales. `historicoRecalcular`
conserva request, empresa desde settings y ausencia de entradas manuales.
Ejemplo de campos nuevos: `{"frecuenciaPagoId":1271,"fechaIngreso":"2020-02-29T00:00:00","versionFuente":"CARGO_SNAPSHOT_V2"}`.
La fuente de ingreso del snapshot no reemplaza automaticamente FECHA_INI actual.


### Fuentes personales actuales V8 / modelo V7

Se conservan `POST api/RhMotorNomina/simularContextoActual`, request y permisos;
no se solicita empresa al cliente. Tres proveedores nuevos del catalogo:
`TITULO_EDUC_ULTIMO_REG`, `TITULO_EDUC_FECHA_NOMINA` (NUMERO),
`FECHA_INGRESO_ADMIN_UNICO` (FECHA). Se asignan con el binding existente:
`{"codigoVariable":"TITULO","proveedor":"TITULO_EDUC_FECHA_NOMINA"}`.
Requieren perfil FUENTES_MODELO_V7 para guardar; lectores admiten versiones previas.

En `data.entradas[]` se agrega `datosPersonales` opcional, omitido cuando es nulo:
`{documento:{formato:1,contexto:{empresa,tipo,periodo,fecha,persona,relacion},proveedor,
filas:[{id,empresa,fecha,titulo,estado}]},elegidoId,valor,hash,versionMetodo,
estadoHistoricoVerificado:false}`. No hay paginacion ni truncamiento: mas de 200
registros bloquea. Ausencia, empate, empresa no explicita y titulo invalido devuelven
isValid false con mensaje especifico. No se proporciona valor cero por defecto.
El proveedor de titulo entrega id, no porcentaje; usar parametros/tramos por titulo.
`historicoRecalcular` mantiene rechazadas las fuentes de educacion sin equivalente historico.
FECHA_INGRESO_ADMIN_UNICO puede resolverse desde FECHA_INGRESO_CARGO_SNAPSHOT conforme
al campo conservado por el cierre, sin leer administrativos actuales.


### Acumuladores de resultados generados

Capacidades agrega `acumuladoresSalidas:true`,
`versionAcumuladoresSalidas:"SALIDAS_CORRIDA_V1"` y `tiposNominaManuales:[19]` en QA.
`modelosSave` conserva DTO/formato y permite `SUMA_SALIDAS([A],[B])` como expresion.
Los sumandos deben ser CALCULADA/DINERO distintos, con productor en la revision.
Se exige validacion de unidades; no se acepta origen ENTRADA/PARAMETRO ni literales.
Rango 1..50 sumandos. Ciclos, falta de productor, precision/desbordamiento o intento
de evaluar salidas no generadas devuelven isValid false sin resultados parciales.
Simular revision/vigente/lote/worker usa el mismo grafo. La validacion de formula
suelta no sustituye la evaluacion del modelo. No hay nuevo endpoint ni paginacion.
La traza incluye operacion SUMA_SALIDAS y operandos; dependencias identifican salidas.

La publicacion de un tipo configurado como manual devuelve isValid false, mensaje
"El tipo conserva sus importes manuales; no se permite publicacion ni aplicacion
automatica." La empresa sigue viniendo de settings, sin override del cliente.
La politica no afecta la lectura ni simulaciones diagnosticas sin movimientos.


### Fuentes V9, modelos V8 y captura de grupos V2

`FuenteActualBinding` agrega `valoresPermitidos?: string[]|null`. Ejemplo:
`{"codigoVariable":"SUPL","proveedor":"EXTRA1_RELACION_ACTUAL","valorSiNulo":"0","valoresPermitidos":["0"]}`.
Rango 1..50 valores canonicos unicos; null omite la restriccion. Fuera de alcance:
VALOR_FUENTE_NO_PERMITIDO antes del calculo. Aplica tambien a simulacion manual y
worker. Los acumuladores admiten E/F/V de forma explicita; los destinos siguen F/V.
Perfil de cliente requerido FUENTES_MODELO_V8, fuentes actuales FUENTES_ACTUALES_V9.

`gruposAcumulacionCapturar` conserva ruta/metodo y agrega `politicaDatos:null` para
captura estricta o `LEGACY_OBSERVADO` para asociaciones con referencia fisica.
El resultado agrega politicaDatos y filas[].referenciaFila (ROWID); id/empresa pueden
ser nulos en esa politica y se conservan originales. Se exige concepto unico de la
empresa/tipo. Grupos estrictos mantienen versionMetodo V1/hash; legacy usa V2.
Los lectores admiten ambos; capacidades anuncia GRUPOS_ACUMULACION_V2. Sin paginacion,
maximo 100 asociaciones, sin truncar. Actualizar el SP propio antes de usar V2.
La traza del acumulador agrega multiplicidad opcional; monto es el original y total
conserva el efecto de las asociaciones repetidas. No se modifican datos de RH.

## Fuente historica del mismo mes y perfiles V9/V10

La API anuncia `versionFuentesModelo: FUENTES_MODELO_V9` y
`versionFuentesActuales: FUENTES_ACTUALES_V10`. No agrega rutas; usa el guardado de
revision y la simulacion de contexto actual existentes, con sus permisos actuales.
Actualizar el cliente antes de editar las nuevas revisiones. Las revisiones V1..V8
siguen siendo legibles; la nueva ventana no se acepta bajo un perfil anterior.

Una ENTRADA DINERO puede declarar:

```json
{
  "codigoVariable": "ANTIGUEDAD_MES",
  "proveedor": "ACUMULADOR_HIST_MES_NOMINA",
  "acumuladorHistorico": {
    "mesesAnteriores": 0,
    "ventana": "MES_NOMINA",
    "filtros": {
      "conceptos": [100],
      "estados": ["A"],
      "tiposMovimiento": ["V"],
      "politicaFrecuencias": "TODAS",
      "frecuencias": [],
      "sinFilas": "CERO_EXPLICITO"
    }
  }
}
```

El concepto del ejemplo es ilustrativo: seleccionar IDs reales de la empresa/tipo.
Empresa proviene de settings, no del frontend. La ventana es el mes calendario de
la fecha del contexto: desde incluido, primer dia del mes siguiente excluido.
Excluye el periodo objetivo; exige periodo unico cerrado para cada fila capturada.
Puede incluir otro periodo cerrado con fecha nominal posterior dentro del mismo
mes. No prueba que esa fila existiera al calcular una nomina historica original.
El recalculo historico no admite este proveedor como equivalencia automatica.

La respuesta conserva la forma de `acumulacionHistorica`, con filas, filtros,
`desde`, `hastaExclusiva`, suma y hash; `origen: RH_HISTORICO_NOMINA_MES` y
`versionMetodo: ACUMULADORES_MES_NOMINA_V1`. El worker revalida evidencia, contexto,
ventana y valor. Una evidencia ausente/alterada o una ventana incompatible es error
explicito, nunca cero implicito ni certificacion financiera.

El proveedor anterior `ACUMULADOR_HIST_MESES_ANT` conserva 1..24 meses anteriores,
sin `ventana`; su JSON y hash existentes se conservan. No usar mesesAnteriores=0
con el proveedor anterior ni MES_NOMINA con mesesAnteriores distinto de cero.


Referencia temporal integral: lector interno RhNomReferenciaIntegral con SP propio
SP_RH_NOM_REF_INT. No agrega ruta, binding ni capacidad publica todavia; no enviar
un proveedor nuevo desde el frontend por la existencia del helper. Se conserva
contexto, reloj Oracle y candidatos de precierre con huella; candidatos duplicados
se rechazan. No identifica por si mismo entradas historicas originales.

## Agregados PH registrados (modelo V10, fuentes actuales V11)

Se mantienen las rutas de guardar revision, simular contexto y crear/consultar
corridas. Permisos existentes: configuracion, fuentes de datos y simulacion.
Actualizar capacidades antes de enviar estos campos; un perfil anterior no puede
editar/agregar las nuevas definiciones. Lectores anteriores V1..V9 se conservan.

Proveedores DINERO: PH_SI_ACT_EXCL, PH_SI_HIST_TOTAL, PH_SI_HIST_MIN,
PH_SI_HIST_MAX, PH_SI_ACT_SUELDO, PH_SI_ACT_OTROS, PH_SI_HEV_SUELDO,
PH_SI_HEV_OTROS y PH_AF_ACT_SUELDO. Cada binding incluye `agregadosPh`:

```json
{
  "codigoVariable": "MIG_PH_HMIN",
  "proveedor": "PH_SI_HIST_MIN",
  "agregadosPh": {
    "grupoSi": null,
    "conceptosSueldoSi": [],
    "conceptosSueldoAf": [],
    "frecuenciasExcluidas": [],
    "politicaNulos": "CERO_NVL_EXPLICITO"
  }
}
```

El ejemplo declara conjuntos vacios y por tanto sumas vacias cero, no un grupo SI
real. Para SI, capturar un grupo con codigo SI de la empresa/tipo y los ids de
conceptos SUELDO de ese conjunto. Los extras de la asociacion no son los extras
del concepto: no inferirlos desde `grupoSi.filas[].extra1`. Declarar frecuencias
excluidas positivas si se selecciona SI. AF usa su conjunto de conceptos explicito.
No enviar empresa: proviene de settings. Hasta 100 conceptos combinados SI/AF.

El historico toma el mes de la referencia temporal capturada: ultimo periodo
abierto con precierre, o reloj Oracle si no existe candidato. Multiples candidatos
son error. Excluye el periodo objetivo y exige periodos historicos unicos cerrados.
No anclarlo silenciosamente al mes nominal ni usar el reloj del navegador/web.

Actual SI incluye F/V; la variante EXCL omite frecuencias declaradas y frecuencia
nula. Historico SI completo conserva tipos/estados; HEV restringe E/V. AF no agrega
filtro de estado/tipo/frecuencia. Conservar multiplicidad de asociaciones SI.
MIN/MAX operan sobre importes candidatos, con cero explicito para monto nulo y
conjunto vacio. No incorporan tasas, factores de sueldo ni decisiones de rama:
esas operaciones quedan en las formulas versionadas.

Respuesta: cada entrada entrega `agregadosPh` con documento (fuente, definicion,
referencia y filas), nueve valores string, hash, version y bandera historica false.
El handler comparte una lectura por definicion identica; hasta cuatro definiciones
y 5000 filas globales, 2000 por origen/definicion. El worker verifica nuevamente
contexto, hash, definicion de revision, proveedor y valor. Evidencia ausente,
alterada o de otra definicion se rechaza. No existe equivalente historico automatico.

La receta PH actual usa estas fuentes, grupo PH, sueldo de relacion, control de
omision y siglas. Mantiene ROUND mensual y TRUNC del periodo. Para ambiguedad C/AN
usa sueldo vigente de relacion por decision del usuario; E/AF/O conservan sus ramas.
El comparador actual, la traza y la simulacion no certifican origen historico ni
habilitan aplicacion financiera.


## Consolidacion diagnostica de corridas

POST `api/RhMotorNomina/corridasConsolidarDiagnostico`.
JWT SIS, permiso `rh.nomina.simular` y `rh.nomina.fuentes.datos`; empresa desde
settings:EmpresaConfig, actor autenticado. Sin token de analisis ni actor del cliente.
Capacidades: consolidacionDiagnostica=true, versionConsolidacion=CONSOLIDACION_DIAGNOSTICA_V1.
Solicitud: `{"corridas":[101,102]}`; 1..16 IDs positivos distintos. Sin paginacion.
No se reciben importes, conceptos, cargos, hashes ni politicas de suma del cliente.

ResultDto.data: versionMetodo, codigoEmpresa, codigoTipoNomina, codigoPeriodo,
fechaNomina, codigoPresupuesto, codigoPersona, escenario, tieneConflictos, destinos.
Cada destino contiene codigoConcepto, frecuenciaId, tipoMovimiento, estado y origenes.
Origen: idRun, modeloId, version, hashDatos, hashModelo, codigoRelacionCargo,
hashFuente y partida (contrato RhNomPartidaPropuesta existente).

Se requieren corridas ACTUAL/COMPLETADO del actor y empresa. Se verifica su entrada
inmutable y se recalcula con el motor; no se usa el resultado guardado como entrada.
Contexto comun: persona/tipo/periodo/fecha/presupuesto/escenario. Un mismo cargo
requiere contexto fuente congelado identico. Un snapshot alterado, distinto actor,
contexto mezclado o corrida no terminal devuelve isValid=false con mensaje en espanol.

Los estados de destino son DESTINO_UNICO, CONFLICTO_DESTINO_REPETIDO y
CONFLICTO_CARGOS_SIN_SEGMENTOS. Un conflicto es un diagnostico valido (isValid=true,
tieneConflictos=true): conserva todas las procedencias, incluso si los importes son
iguales o las acciones conservan existentes. No suma sueldos, no deduplica importes
ni elige una primera fila. No infiere segmentos ni prorrateos de fechas del cargo.

aplicacionDisponible=false, movimientosAplicados=false y
paridadHistoricaCertificada=false siempre, tambien con destinos unicos. Este lector
no consulta/valida el periodo actual para aplicacion, no bloquea movimientos y no
habilita el adaptador financiero. No hay escrituras Oracle ni objetos SQL nuevos.


## Recalculo historico V6: bases archivadas del mismo periodo

Ruta y solicitud `historicoRecalcular` conservadas, sin importes/entradas manuales
en el request. Capacidades: versionRecalculoHistorico=RECALCULO_HISTORICO_V6.
El proveedor guardado ACUMULADOR_MOV_ACTUAL se resuelve ahora como
BASE_PERIODO_HISTORICO desde RH_HISTORICO_NOMINA del periodo cerrado exacto,
con el cargo de RH_HISTORICO_PERSONAL_CARGO. No consulta RH_MOV_NOMINA.
Requiere reglas de concepto en el modelo: sus salidas se excluyen en SQL antes
de leer importes y se validan de nuevo en C#. No se aceptan exclusiones del cliente.

Cada entrada resuelta incorpora basePeriodo (opcional, omitido si null): documento
(formato,cargo,filtros,salidasModelo,partidas), filas (traza), total, hash,
versionMetodo=BASE_PERIODO_HISTORICO_V1 y origen=RH.RH_HISTORICO_NOMINA_PERIODO.
clasificacionHistoricaVerificada, incluyeSalidasModelo y paridadCertificada son false.
El documento coincide con cargo/filtros/salidas del modelo; capturas distintas se
rechazan. Los montos esperados de las salidas se leen solo despues del calculo,
si Comparar=true, conservando HashReferenciaEsperado y la politica existente.

Hasta 2000 filas por base y 5000 entre bases/acumuladores por contexto. Nulos incluidos,
duplicados, filas ajenas, contexto no cerrado o falta de snapshot unico son errores;
no se entregan sumas truncadas ni se usan ceros salvo politica explicita sin filas.
Los grupos congelados siguen siendo metadata del modelo, sin atribucion automatica
de clasificacion original. Siglas, suplencia, controles o proveedores sin equivalente
siguen rechazados; esta ampliacion no habilita lecturas actuales como reemplazo.
No aplica movimientos ni certifica por si misma una nomina completa.

La traza de base selecciona claves, frecuencia, tipo, estado e importe. El campo
Complemento del DTO compartido no se selecciona en este lector y no debe
interpretarse como una comprobacion de que el complemento original era nulo.

## Informe previo de dependencias historicas

POST `api/RhMotorNomina/historicoDependencias`, body `{"modeloId":1,"version":47}`.
Sin paginacion, importes, persona, periodo ni empresa en el request. JWT SIS, permisos
rh.nomina.historico.datos y rh.nomina.configuracion.leer. Empresa desde settings.
Capacidades: dependenciasHistoricas=true,
versionDependenciasHistoricas=DEPENDENCIAS_HISTORICAS_V1.

ResultDto.data contiene modeloId, version, hashModelo, codigoTipoNomina, escenario,
versionMetodo, versionRecalculo, tieneBloqueos, bloqueosModelo y entradas. Cada entrada:
codigoVariable, proveedorModelo, compatibleDiagnostico, proveedorHistorico (nullable),
estado y mensaje. Un bloqueo de compatibilidad no invalida el informe: isValid=true,
tieneBloqueos=true. Revision inexistente/ajena o definicion corrupta son errores.

La validacion es la misma utilizada por historicoRecalcular; lista todos los bloqueos
sin cambiar proveedores, valores por defecto, politica de vigencia o revision. Estado
REQUIERE_LECTURA_HISTORICA significa equivalente registrado para diagnostico, sin haber
comprobado datos del periodo. VIGENCIA_HISTORICA_REQUERIDA, SALIDAS_BASE_REQUERIDAS y
FUENTE_SIN_EQUIVALENTE_HISTORICO conservan el motivo de rechazo del motor.

datosHistoricosLeidos, paridadCertificada, aplicacionDisponible y movimientosAplicados
son false. No se consultan datos de nomina ni se reemplaza una fuente por la actual.
No equivale a una validacion/publicacion operativa ni certifica resultados originales.


FECHA_INGRESO_ADMIN_UNICO: equivalente de diagnostico registrado en V6 como
FECHA_INGRESO_CARGO_SNAPSHOT. Reutiliza RH_HISTORICO_PERSONAL_CARGO.FECHA_INGRESO,
cuya procedencia de RH_ADMINISTRATIVOS.FECHA_INGRESO esta verificada en la vista y
el cierre. La entrada conserva ProveedorModelo, ProveedorUsado, evidencia de columna
y hash del cargo. Nulo, zona no Oracle, fecha anterior a 1900 o posterior al contexto
son errores. No sustituye FECHA_INI ni valida automaticamente antiguedad/procedencia
de todas las otras entradas. No cambia request, version V6 ni contratos anteriores.

## Plan financiero previo (solo lectura)

POST `api/RhMotorNomina/corridasPlanFinanciero`.
Permisos: `rh.nomina.simular` y `rh.nomina.fuentes.datos`; JWT SIS valido.
Empresa y actor se obtienen del servidor/sesion. Request maximo 16 KiB:

```json
{"corridas":[48,49,50]}
```

Acepta de 1 a 16 IDs positivos distintos, propios, ACTUAL/COMPLETADO, del mismo
contexto empresa/persona/tipo/periodo/fecha/presupuesto/escenario. Reproduce las
entradas congeladas de cada corrida antes de preparar destinos; el cliente no
puede enviar importes, imagenes, empresa, actor ni propiedad de movimientos.
Sin paginacion: maximo 100 destinos y 2000 movimientos por contexto. Si se excede
el limite, retorna IsValid=false; nunca entrega un plan truncado como valido.

ResultDto con `data.consolidacion`, `data.plan.pasos`, `hashMovimientos` y
`cantidadMovimientos`. Cada paso contiene concepto, frecuencia, tipo, accion,
`antes` (imagen completa de las 17 columnas), `montoNuevo` como texto decimal,
`idAplicacionPropietaria` y `bloqueo`. Imagenes/importes existentes son datos RH
protegidos; no se exponen movimientos de otra empresa. El hash ordena por ID,
incluye extras/auditoria y conserva MONTO Oracle como texto sin redondearlo.

Ejemplo abreviado de respuesta de negocio valida con bloqueo:

```json
{
  "isValid":true,
  "message":"Plan previo con bloqueos. No se modificaron movimientos.",
  "data":{
    "cantidadMovimientos":11,
    "propiedadRegistrada":false,
    "planPersistido":false,
    "paridadCertificada":false,
    "aplicacionDisponible":false,
    "plan":{
      "versionMetodo":"PLAN_FINANCIERO_PREVIO_V1",
      "tieneBloqueos":true,
      "aplicacionDisponible":false,
      "movimientosAplicados":false,
      "datosRevalidadosEnTransaccion":false,
      "pasos":[{
        "codigoConcepto":1656,"frecuenciaId":20,"tipo":"F",
        "accion":"BLOQUEADO","montoNuevo":null,
        "idAplicacionPropietaria":null,"bloqueo":"MOVIMIENTO_NO_PROPIO"
      }]
    }
  }
}
```

El ejemplo omite `consolidacion`, hash e imagenes para brevedad. IsValid=true
indica que se preparo el diagnostico; no significa autorizacion de pago.
Capacidades: `planFinancieroPrevio=true`,
`versionPlanFinanciero=PLAN_FINANCIERO_PREVIO_V1`.

Sin ledger transaccional todas las filas existentes se consideran no propias:
no se adoptan por concepto automatico, actor -1 ni coincidencia de importe.
Una propuesta ausente puede mostrar PROPONER_CREACION, sin asignar ID ni escribir.
Varios cargos se bloquean mientras no exista politica explicita de segmentos.
El indice unico U2 no incluye empresa ni frecuencia: cualquier movimiento de
otro ambito para la misma persona/tipo bloquea conservadoramente todo el plan,
sin devolver sus datos (`INDICE_UNICO_OTRO_AMBITO`).

Este endpoint no persiste planes, no registra propiedad, no aplica ni revierte,
no bloquea filas y no certifica paridad. Las imagenes deben releerse/bloquearse
junto con periodo, vigencia, evidencias y fuentes dentro de la futura transaccion.
En Corridas, seleccionar filas y usar **Revisar movimientos existentes**.

## Archivo inmutable de planes previos

Capacidades: `planesFinancierosGuardados=true`,
`versionPlanesGuardados=PLAN_GUARDADO_V1`. Las tres operaciones requieren JWT SIS,
`rh.nomina.simular` y `rh.nomina.fuentes.datos`. Empresa y actor son del servidor;
no se aceptan imagenes, importes, propiedad o permisos enviados por el cliente.

### Guardar

POST `api/RhMotorNomina/planesFinancierosCrear`, maximo 16 KiB:

```json
{"claveSolicitud":"REVISION:2026-11-20:1","corridas":[48,49,50]}
```

Clave literal de 1..80 caracteres `[A-Za-z0-9:_-]`; no se recorta ni convierte a
mayusculas. Corridas: mismos requisitos de corridasPlanFinanciero, 1..16 IDs
positivos distintos. La primera solicitud reproduce sus corridas, toma una nueva
captura Oracle y guarda un documento inmutable. No guarda el resultado recibido
del navegador ni promete que coincida con una consulta previa de movimientos.

La misma clave/empresa/actor y el mismo conjunto de corridas (orden indiferente)
recuperan exactamente el plan original, sin recapturar. Si cambia el conjunto,
retorna IsValid=false. Un mutex Oracle y UNIQUE empresa/actor/clave impiden duplicar
registros concurrentes. Cuota de 500 planes por empresa/actor; los reintentos de
planes existentes funcionan aun alcanzada la cuota. Documento hasta 4 MiB; traza
de respuesta limitada antes del INSERT para evitar guardar un resultado ilegible.
No hay UPDATE/DELETE de planes; trigger rechaza ambas operaciones.

Respuesta `ResultDto<PlanGuardado>`: `data.cabecera` con `idPlan`, `codigoEmpresa`,
`actor`, `claveSolicitud`, `hashSolicitud`, `hashDocumento`, `fechaIns` (reloj BD).
`data.diagnostico` conserva consolidacion, plan, hash/cantidad de movimientos y
advertencia de captura archivada. `data.planPersistido` y
`data.diagnostico.planPersistido` son true. `movimientosAplicados`,
`propiedadRegistrada`, `aplicacionDisponible` y `paridadCertificada` siguen false.
El diagnostico anidado tiene `aplicacionDisponible=false`, igual que `plan`.
La persistencia de un plan NO es una aplicacion ni un ledger de propiedad.

### Consultar

POST `api/RhMotorNomina/planesFinancierosDetalle`, maximo 4 KiB:

```json
{"idPlan":1}
```

Solo el actor propietario en la empresa configurada puede recuperarlo. Lee CLOB
por fragmentos, verifica orden, longitud, hash completo, cabecera, solicitud,
version, origenes/corridas y alcance. Reconstruye el plan con sus imagenes
archivadas; no toma movimientos actuales ni reejecuta el paquete antiguo.
No certifica vigencia ni sustituye la revalidacion transaccional futura.

### Listar

POST `api/RhMotorNomina/planesFinancierosGetAll`, maximo 4 KiB:

```json
{"pageNumber":1,"pageSize":10}
```

Pagina desde 1 (maximo 100000), tamanio 1..100; `searchText` no vacio se rechaza.
Devuelve `ResultDto<List<PlanCabecera>>` con `page`, `totalPage`,
`cantidadRegistros`, orden ID descendente y solo planes propios. El frontend
convierte la pagina de DataGrid desde cero. No devuelve imagenes en el listado.
En Corridas aparece **Planes previos guardados**, con referencia, guardar captura,
listado paginado y consulta del detalle. Guardar no habilita pagos.

## Consulta del ledger financiero

POST `api/RhMotorNomina/planesLibroFinanciero`, maximo 4 KiB:

```json
{"idPlan":1}
```

Requiere JWT SIS, `rh.nomina.simular` y `rh.nomina.fuentes.datos`. Primero recupera
el plan propio en la empresa configurada; de el obtiene tipo/persona. No recibe
empresa, persona, actor, imagenes o propiedad del navegador. Consulta la cadena
completa del contexto empresa/tipo/persona, incluyendo eventos de otros periodos
que afecten a la propiedad de los mismos IDs fisicos. No representa una corrida.

ResultDto con `data.eventos`, `ultimoEvento`, `ultimoHash` y `propiedades`:
cada propiedad indica `idAplicacion` (ID del ultimo evento que genero su imagen),
`ultimaImagen` completa de movimiento y `hashUltimaImagen`. El ID puede referirse
a una reversion que haya restaurado la imagen, no solo a una aplicacion inicial.
`versionMetodo=LIBRO_FINANCIERO_V1`, `datosRevalidadosEnTransaccion=false`,
`aplicacionDisponible=false`, `paridadCertificada=false`.

Ejemplo observado en QA, sin aplicaciones reales:

```json
{"isValid":true,"data":{"propiedades":[],"ultimoEvento":null,"ultimoHash":null,
 "eventos":0,"versionMetodo":"LIBRO_FINANCIERO_V1",
 "datosRevalidadosEnTransaccion":false,"aplicacionDisponible":false,"paridadCertificada":false}}
```

Capacidades: `libroFinanciero=true`, `versionLibroFinanciero=LIBRO_FINANCIERO_V1`.
En el detalle de un plan guardado: **Consultar aplicaciones registradas**.
Guardar un plan no crea eventos de aplicacion ni otorga propiedad.

El lector verifica hashes de documento y evento, padre/orden de la cadena,
cabecera/plan y alcance, antes de reconstruir propiedad. Solo F/V y frecuencia
positiva pueden ser propios del motor; no se adoptan movimientos manuales E.
Rechaza cualquier historial incompleto o incompatible con el indice unico fisico.
Limites: 200 eventos (sentinel201), 100 cambios/evento, 512 KiB/documento,
8 MiB/cadena y 2000 movimientos propios; sin paginacion ni propiedad parcial.
El limite de respuesta API sigue siendo 2 MiB. Una cadena fuera de limites requiere
un futuro mecanismo de checkpoint validado; no se omiten eventos antiguos.

Esta entrega crea almacenamiento y lectores, sin procedimiento de registro ni
endpoint para insertar eventos confirmados. Los casos de aplicacion/reversion
se prueban en memoria; no se insertan como hechos financieros ficticios en QA.
El adaptador pendiente debera bloquear/revalidar fuentes, periodo, publicacion y
movimientos, ejecutar DML y registrar el evento en la misma transaccion. La
consulta actual no prueba que esos pasos hayan ocurrido ni habilita pagos.

## Adaptador transaccional (ejecucion cerrada)

POST `api/RhMotorNomina/planesEjecutar`, maximo 4 KiB. JWT SIS y
`rh.nomina.fuentes.datos`, mas `rh.nomina.ejecutar` para aplicar o
`rh.nomina.revertir` cuando `idRevertido` no es nulo. Campos desconocidos se rechazan.

```json
{"idPlan":5,"hashPlanEsperado":"AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
 "claveSolicitud":"LIQ:2026-11:1","motivo":"Liquidacion revisada por el operador"}
```

Para revertir, mismo contrato con `idRevertido` igual al evento de aplicacion.
Sustituya el hash ilustrativo por el hash SHA256 real de la cabecera. Plan positivo, clave literal `[A-Za-z0-9:_-]` de 1..80
y motivo recortado de 10..500 caracteres. Empresa/actor, importes, imagenes,
publicacion y habilitacion nunca proceden del cliente. Solo planes propios.
Primera version: una relacion de cargo y una revision de modelo con todas sus
reglas; planes de varias revisiones/cargos se rechazan, sin sumarlos.

La ejecucion sigue cerrada en QA. Requiere simultaneamente configuracion servidor
`RhMotorNomina:AplicacionEnabled`, control RH_NOM_FIN_CTL con ACTIVO=1 y
LEGACY_RETIRADO=1, periodo abierto/presupuesto actual y, al aplicar, revision
integral publicada/vigente con pruebas reproducibles. Instalar no habilita ninguna
empresa. Una bandera de configuracion o un JSON del cliente no bastan.
Capacidades informan `adaptadorTransaccional=true`,
`versionAdaptadorTransaccional=TRANSACCION_FINANCIERA_V1`, `aplicacionDisponible=false`.
No se agrega un boton de aplicar al frontend mientras falta integracion positiva.

El servidor toma bloqueos globales conservadores NOWAIT, recaptura fuentes en la
misma conexion/transaccion, reproduce pruebas de publicacion y corridas, compara
hashes y catalogo, valida propiedad y luego escribe. Creacion: STATUS=A,
complemento/extras nulos y F/V/frecuencia del modelo; actualizacion: solo monto y
auditoria. No adopta filas legacy ni movimientos E. Lee de nuevo todas las imagenes
del contexto, protege filas no tocadas y registra evento+solicitud antes del commit.
Cualquier fallo previo a confirmar produce rollback. Deadline de 30 segundos.

Respuesta exitosa futura `ResultDto<ReciboFinanciero>`: `idEvento`, `idPlan`,
`hashSolicitud`, `hashEvento`, `fechaBd`, `movimientosAplicados=true`,
`eventoConfirmado=true`. Es un recibo de la operacion original; reintentar la misma
clave/cuerpo devuelve ese recibo sin nuevos cambios, incluso si luego se deshabilita
la ejecucion. Cambiar plan, hash, motivo o evento revertido bajo la misma clave se
rechaza. Si se pierde la respuesta durante commit, consultar/reintentar con la
misma clave; no crear otra solicitud para resolver una confirmacion incierta.

Reversion: ultima aplicacion activa del mismo plan/contexto, todas sus partidas,
imagen propia actual intacta y periodo aun abierto. Puede revertir reglas retiradas
sin volver a publicarlas; no recalcula formulas para deshacer. Auditoria nueva con
actor/reloj BD. Detecta IDs ocupados y claves unicas conflictivas. El mismo recibo
confirma la operacion inversa; el ledger identifica su accion y evento original.

Limites existentes del ledger se comprueban antes de escribir y antes de registrar.
Al aplicar se reserva espacio para un inverso inmediato (un evento y hasta 512 KiB),
con 16 KiB de margen por documento para auditoria/motivo inversos. No garantiza
una cadena ilimitada de deshacer: para exceder esos limites falta checkpoint validado.
La secuencia propia de movimientos salta hasta 1000 IDs ocupados bajo bloqueo;
no usa MAX+1 en runtime y puede dejar huecos al fallar o hacer rollback.

La integracion positiva con aplicaciones financieras reales continua pendiente.
En QA se probaron puertas cerradas, bloqueos/release y recaptura; no se fabricaron
publicaciones integrales ni eventos financieros para sortearlas. No hay paridad
integral certificada ni habilitacion operativa de este endpoint.

## Universo completo de una nomina historica (2026-11-21)

POST `/api/RhMotorNomina/historicoPoblacion`. JWT SIS y permiso
`rh.nomina.historico.datos`. Empresa desde `settings:EmpresaConfig`; propiedades
no declaradas se rechazan. Capacidades: `poblacionHistorica=true`,
`versionPoblacionHistorica=POBLACION_HISTORICA_V1`.

```json
{"codigoTipoNomina":10,"codigoPeriodo":5933,"fechaNomina":"2025-10-30T00:00:00"}
```

`ResultDto<PoblacionHistorica>` conserva `isValid/message/data`. `data` contiene
`fuente` (contexto cerrado y contadores), `personas`, `filasCargo`, `partidas`,
`candidatosUnCargo`, `personasConBloqueos`, `hashPoblacion`, `versionMetodo` y
`poblacionCompleta=true`. `entradasOriginalesVerificadas`, `paridadCertificada` y
`movimientosAplicados` son false. Cada persona contiene:

```json
{"archivo":{"codigoPersona":123,"filasCargo":1,"relaciones":1,
"relacionesInvalidas":0,"codigoRelacionCargo":456,"partidas":7},
"bloqueos":[],"candidatoUnCargo":true}
```

Incluye la union de personas de ambos historicos; no omite personas con partidas
sin cargo, cargos sin partidas, IDs invalidos, snapshots repetidos o multiples
cargos. Relacion unica solo si existe una fila con relacion positiva entera; no
se elige MIN/MAX entre varios cargos. `candidatoUnCargo` es solo elegibilidad
estructural, no disponibilidad de todas las entradas ni resultado calculado.
Una persona sin partidas queda identificada para revisar el alcance; no se infiere
que haya que crearle pagos. Las personas y relaciones son identificadores sensibles:
no mostrar estos datos a usuarios sin el permiso de datos historicos.

Sin paginacion: hasta 5000 grupos completos ordenados por persona, NULL primero.
Si hay un grupo 5001 se rechaza toda la respuesta, sin entregar una muestra como
universo completo. No se leen nombres, cuentas, cedulas, importes ni tablas actuales.
Fecha/hora exactas, periodo unico y cerrado; no se trunca la fecha Oracle. Un cambio
en los contadores entre contexto y lectura rechaza la captura y pide reintentar.
El hash identifica la captura, no prueba que el archivo conserve entradas originales.
No acepta listas del cliente para afirmar que una nomina fue comparada completa.
La matriz existente de casos individuales sigue siendo diagnostica.

### Referencias de nomina especial

Las referencias historicas conservan `tipoMovimiento="E"`, ademas de F/V, segun
el flujo de nomina especial observado en PROCESS_PRENOMINA de Oracle Forms. La
integridad estructural de una referencia E no implica que exista una receta para
ese concepto. El comparador conserva E como clave distinta: un resultado F no
coincide con una partida E de igual concepto/frecuencia/importe. Sin regla migrada,
la partida E aparece en `sinAsociar` y la salida F en `SIN_PARTIDA_REFERENCIA`.
No se infiere ni convierte el tipo por monto o por nombre de concepto. Tipos
historicos desconocidos siguen observados como invalidos. Las asociaciones de
salida del motor y la aplicacion financiera siguen limitadas a F/V.
`historicoContexto.clavesInvalidas` ya no considera E como tipo desconocido.
No se cambia el formato ni el hash de las referencias F/V existentes.


## Ausencia explicita de educacion - perfiles V11/V12

`versionFuentesModelo: FUENTES_MODELO_V11` y
`versionFuentesActuales: FUENTES_ACTUALES_V12`. Las rutas, POST y envoltura
ResultDto siguen iguales. `modelosGuardarRevision` acepta bindings con
`proveedor: TITULO_EDUC_ULTIMO_O_SIN_REG`; exige versionFuentesCliente V11.
`simularContextoActual` y `corridasCrear` usan esa politica solo si esta fijada
por la revision. No se agrega empresa al cuerpo del frontend.

El nuevo proveedor devuelve el ultimo titulo finalizado unico; si no hay educacion
finalizada, captura `valor: "0"` y `datosPersonales.elegidoId: 0` como ausencia
explicita. No es un titulo real ni acredita porcentaje. La formula/tramo debe
declarar expresamente el tratamiento de esa ausencia; para la receta profesional
NO_APLICA_EXPLICITO incluye tramo `[0,1)` valor0. Un titulo real nulo/cero,
un empate o empresa desconocida sigue produciendo IsValid=false.
El proveedor estricto TITULO_EDUC_ULTIMO_REG conserva su error cuando faltan datos.
La evidencia y hash incluyen las filas consultadas, incluso vacias.

Ejemplo de binding dentro de fuentesActuales:
```json
{"codigoVariable":"MIG_PR_TIT","proveedor":"TITULO_EDUC_ULTIMO_O_SIN_REG"}
```

La politica usa el procedimiento de lectura existente, sin nuevos objetos Oracle.
El worker comprueba que proveedor de entrada, evidencia y revision sean iguales;
no permite introducir una ausencia explicita en un modelo que exige titulo.
Los snapshots V10 siguen legibles con hashes inalterados; no admiten el proveedor
nuevo. No hay cambios de paginacion. El frontend rechaza una API anterior al guardar
o simular con este perfil. La huella del adaptador financiero sigue fijada en
V11/V10 y bloquea el perfil nuevo hasta una revision transaccional explicita.


## Decisiones de control posteriores - formato 9

Las rutas POST existentes conservan ResultDto y sus reglas de paginacion.
`capacidades` anuncia formato9, accionesControl=true y
versionAccionesControl=ACCIONES_CONTROL_V1. `modelosGuardarRevision` acepta
accionesControl y versionAccionesCliente. Solo RECALCULA_VARIACIONES, con unidades
verificadas y fuentes versionadas de CONTROL_OMITIR_CONCEPTO para el mismo concepto.
No requiere codigoEmpresa del frontend. Maximo veinte decisiones por revision.

Ejemplo de campos adicionales del cuerpo de guardado:
```json
{
  "accionesControl":[{
    "clave":"RETIRAR_SS","codigoConcepto":100,
    "hashConceptoEsperado":"SHA256_DEL_CATALOGO",
    "codigoVariableControl":"CTL_SS","tipoMovimiento":"F","valorRetiro":"1"
  }],
  "versionAccionesCliente":"ACCIONES_CONTROL_V1"
}
```

El hash debe ser el SHA-256 real del concepto elegible; el ejemplo es descriptivo.
La variable de control debe ser ENTRADA/NUMERO/NUMERO y capturarse para ese concepto.
No se permite destino monetario y decision para el mismo concepto. El modelo
rechaza catalogos cambiados, destinos duplicados, conceptos manuales o proveedores
ajenos. ValorRetiro explicito0/1; la otra alternativa conserva el movimiento.
Un cliente sin version de acciones no puede editar una revision que las contiene.
Snapshots anteriores siguen legibles con hashes inalterados; omiten los campos nuevos.

`modelosSimularVigente`, `simularContextoActual` y el resultado de `corridasDetalle`
incluyen decisiones en `simulacion.accionesControl` (en la simulacion vigente,
accionesControl esta en data). Cada decision contiene clave, codigoConcepto,
frecuenciaId, tipoMovimiento, codigoVariableControl, valorControl y accion
RETIRAR_GENERADO/CONSERVAR_EXISTENTE. No contiene un importe calculado ni propuesto.
Las partidas monetarias siguen separadas en proyeccionConceptos.

Estas decisiones no cambian formulas ni excluyen el sueldo de las bases. El worker
verifica evidencia, concepto y politica del control contra la revision. La UI
conserva las acciones capturadas al guardar y muestra sus decisiones por separado.
El editor visual de nuevas acciones y la integracion de las decisiones al plan
financiero siguen pendientes. La consolidacion rechaza modelos con acciones en vez
de preparar planes parciales (ACCIONES_CONTROL_SIN_PLAN). La validacion registra
esa puerta pendiente y no habilita liquidacion. La reconstruccion historica tampoco
ignora acciones: requiere una fuente de controles historicos aun no disponible.
El endpoint de simulacion simple de formulas indica usar vigente/contexto actual
cuando la revision contiene acciones para no ocultar sus decisiones.


## Plan previo con decisiones de control

Actualizacion2026-11-21: ya se incluyen decisiones en POST corridasConsolidarDiagnostico,
corridasPlanFinanciero y planesFinancierosCrear/Detalle. Metodos, solicitudes
(claveSolicitud/corridas o idPlan), paginacion, permisos y ResultDto siguen iguales.
Capacidades anuncia planesDecisionesControl=true y
versionPlanesDecisionesControl=PLAN_DECISIONES_CONTROL_V1. No habilita aplicacion.

El consolidado agrega versionDecisionesControl solo cuando hay decisiones. Cada
origen contiene partida monetaria o decisionControl, exclusivamente. En un origen
de control partida es null, decisionControl tiene el formato descrito en la seccion
anterior y no contiene un monto. Por ejemplo:
```json
{"partida":null,"decisionControl":{"clave":"SS_RET","codigoConcepto":716,
"frecuenciaId":1321,"tipoMovimiento":"F","codigoVariableControl":"CTL_SS",
"valorControl":"1","accion":"RETIRAR_GENERADO"}}
```

El paso de retiro tiene montoNuevo=null; solo propone retirar si existe propiedad
acreditada y la imagen actual coincide. Sin propiedad devuelve MOVIMIENTO_NO_PROPIO;
si cambio cualquier dato devuelve MOVIMIENTO_MODIFICADO_POSTERIORMENTE. Sin fila:
SIN_MOVIMIENTO. Conservar mantiene la fila sin adoptar propiedad. El plan archivado
no acredita propiedad ni autoriza liquidacion. Documentos anteriores conservan sus
campos, hashes y solicitudes idempotentes, sin la extension nueva.

Esta actualizacion sustituye el bloqueo ACCIONES_CONTROL_SIN_PLAN por
ACCIONES_CONTROL_SIN_VALIDACION_TRANSACCIONAL. La validacion sigue siendo tecnica
sin paridad historica/aceptacion transaccional completa. La aplicacion no se habilita.


## Editor visual de decisiones - seguimiento

La vista de modelos ahora edita accionesControl en su borrador: carga las acciones
capturadas, agrega destinos F/V sin repetirlos, cambia clave/control/valor de retiro
y permite quitarlos. La seleccion usa las fuentes CONTROL_OMITIR_CONCEPTO de
entradas NUMERO/NUMERO y conserva la vinculacion al concepto de la fuente. Busca
metadatos del catalogo por tipo con la paginacion existente y permite confirmarlos.
No permite agregar una decision si sus dos destinos F/V ya estan definidos o el
concepto tiene salida monetaria. Un conflicto creado al editar se muestra y el
backend lo vuelve a validar.

Al guardar se envia el borrador editado, no se vuelve a copiar la captura anterior.
La eliminacion de la ultima accion conserva versionAccionesCliente cuando la
revision cargada ya las tenia. El backend sigue rechazando clientes que omiten
esa version al editar revisiones con acciones. No hay rutas, permisos o settings
nuevos; requiere formato9/capacidad accionesControl y unidades verificadas en
RECALCULA_VARIACIONES. Guardar crea una revision; no publica ni aplica movimientos.
La prueba con sesion SIS real sigue pendiente; build/preview son verificaciones
tecnicas diferentes de la aceptacion funcional positiva.


## Revalidacion transaccional de acciones y reversion por imagen

La aplicacion revalida el catalogo de todos los destinos, incluidas las decisiones
de control: concepto automatico activo, empresa/tipo y frecuencia vigentes. La
ausencia o cambio invalida el plan; no se adopta propiedad de movimientos manuales.
No cambia ruta, request, respuesta ni permisos. El resultado de errores esperados
sigue siendo HTTP200 con isValid=false y message en espaniol.

La huella de proveedores fijada en V11/V10 mantiene bloqueadas las aplicaciones
nuevas del perfil actual. Una reversion restaura imagenes confirmadas del ledger
sin evaluar proveedores actuales y por eso no depende de esa huella. Sigue
requiriendo aplicacion habilitada, controles SQL, plan propio/hash correcto,
periodo/presupuesto y protecciones de cadena, imagen completa y concurrencia.
Un recibo confirmado puede recuperarse por su misma clave sin ejecutar cambios.

PruebasTransaccionControlQA.md documenta dos ensayos aislados de75 comprobaciones.
No certifica aplicacion integral ni autenticacion SIS positiva. La puerta
ACCIONES_CONTROL_SIN_VALIDACION_TRANSACCIONAL y los controles financieros reales
continuan cerrados.


## Plantillas compuestas con decisiones de control

El ensamblador interno de plantillas puede combinar bloques con acciones, usando
los mismos campos accionesControl/versionAccionesCliente del contrato de modelos.
No hay ruta nueva ni cambios en request/respuesta HTTP. Cada bloque se valida con
su ACK; el modelo combinado rechaza claves/destinos duplicados y conflictos entre
reglas monetarias y decisiones. No se deduplican acciones ni se escoge una por orden.
Sin acciones se conservan campos nulos y compatibilidad anterior. Las decisiones
no modifican el grafo monetario ni permiten eludir las puertas de aplicacion.


## Consulta de importes manuales actuales - CONSERVACION_MANUAL_V1

POST api/RhMotorNomina/manualMovimientos. JWT SIS y permisos rh.nomina.simular
y rh.nomina.fuentes.datos (o superusuario). Empresa desde settings:EmpresaConfig;
no se acepta un actor/empresa del cliente como autorizacion. Request hasta8KiB:

```json
{"codigoTipoNomina":19,"codigoPersona":123}
```

Requiere identificadores positivos y tipo incluido en RhMotorNomina:TiposNominaManuales.
El numero19 es solo ejemplo CE; el motor usa la configuracion, no un ID fijo.
No requiere modelo, formula, sueldo SS, periodo ni datos historicos. Consulta la
imagen actual de RH_MOV_NOMINA, no un archivo cerrado ni un calculo de prenomina.

Respuesta ResultDto<CapturaManual>, HTTP200 para errores esperados con isValid=false
y message en espaniol. Sin paginacion de servidor; hasta2000 imagenes completas por
empresa/tipo/persona. No trunca ni deduplica; el visor pagina localmente10/25/50.
Si supera el limite o2MiB de respuesta se rechaza la captura completa.

```json
{"isValid":true,"message":"Importes actuales consultados sin realizar calculo automatico ni modificar movimientos.","data":{"codigoEmpresa":13,"codigoTipoNomina":19,"codigoPersona":123,"hashCaptura":"HUELLA_SHA256","movimientos":[{"id":1,"codigoEmpresa":13,"codigoTipoNomina":19,"codigoPersona":123,"codigoConcepto":99,"frecuenciaId":20,"tipo":"F","monto":"123.45","estado":"A","complemento":null,"extra1":null,"extra2":null,"extra3":null,"usuarioIns":7,"fechaIns":"2025-01-01T00:00:00","usuarioUpd":null,"fechaUpd":null}],"hayAmbitoAjeno":false,"versionMetodo":"CONSERVACION_MANUAL_V1","origen":"RH_MOV_NOMINA_ACTUAL","tratamiento":"CONSERVAR_IMPORTES_EXISTENTES","calculoAutomaticoRealizado":false,"movimientosAplicados":false,"propiedadFinancieraAcreditada":false,"estadoHistoricoVerificado":false,"paridadHistoricaCertificada":false}}
```

Monto es texto NUMBER de Oracle, nunca float JS. Se conserva su signo y nulidad;
no convertir null en0 ni F/V/E entre si. No calcula totales ni interpreta el signo
como asignacion/deduccion. La huella incluye contexto, lista ordenada de tipos
manuales configurados, advertencia de ambito ajeno e imagenes completas ordenadas
por ID. No equivale a propiedad financiera ni firma de autorizacion.

capacidades agrega conservacionManual=true y versionConservacionManual; el cliente
debe verificar ambos y usar tiposNominaManuales. Version desconocida/capacidad
ausente deshabilita la consulta. Campos extra son compatibles con clientes previos.
La pestaña Importes manuales muestra tipo/persona de la respuesta, valores originales,
auditoria y huella; limpia el resultado al editar el contexto. Sin acciones de
aplicar/guardar, formula artificial, suma monetaria ni descarga automatica.


## Etapa interna de seleccion N/E (sin nueva operacion HTTP)

SELECCION_PRENOMINA_V1 es un componente de integracion en staging, no una ruta nueva
ni un calculo especial disponible en la UI. Define criterios explicitos de tipo,
personal y frecuencia, con imagen completa, motivos y multiplicidad del cursor.
No convierte resultados F a E ni acredita las fuentes anteriores al cierre.
SeleccionPrenominaEspecial.md documenta pruebas, alcance y pasos pendientes antes
de conectarlo al flujo completo. El frontend/contrato HTTP publicados no cambian.


## Totales de prenomina en staging (sin nueva operacion HTTP)

TOTALES_PRENOMINA_V1 integra seleccion N/E con sumas exactas por persona: asignaciones,
deduccionesFirmadas, neto y motivos. VALIDADO es estado aritmetico, sin autorizacion
de aplicacion. SIN_PARTIDAS tiene totales null; fuentes bloqueadas nunca entregan
un total parcial como pago. Neto negativo o agregados fuera de NUMBER38 bloquean
y conservan el valor exacto como texto. No convertir a float JS ni cambiar signo.
TotalesPrenomina.md documenta pruebas y limites. No agrega endpoint, cambia el
frontend publicado ni certifica modelos o paridad historica.


## Inventario E y procedencia (sin cambio HTTP)

MatrizEntradasEspeciales.md documenta53 pares tipo/concepto de la muestra especial
y las vias posibles de captura/calculo. AUTOMATICO o extras actuales no confirman
productor; usuario/fecha historicos se reasignan en la carga de prenomina. No se
autorrellena InputAmounts desde el esperado ni se habilita un modelo por nombre
del concepto. La matriz es documentacion no ejecutable, sin nuevos endpoints.


## Capsula interna de prenomina (sin cambio HTTP)

CAPSULA_PRENOMINA_V1 liga JSON canonico de contexto/imagenes/metadatos a una huella
para replay y postcondiciones de cambios ordinarios F/V. No es firma, prueba de
productor, periodo/presupuesto original ni propiedad; no debe aceptarse del cliente
como autorizacion. No habilita almacenamiento, endpoint o liquidacion.
CapsulaPrenomina.md documenta alcance/pruebas. La UI publicada no cambia.


## Archivo interno de capsulas (sin cambio HTTP)

ARCHIVO_CAPSULA_V1 persiste capsulas V1/V2 inmutables con contexto, actor/empresa,
clave de reintento y huellas. No se agrega ruta, request de cliente ni accion en
UI. No enviar JSON de capsula desde el frontend para autorizar una captura o pago.
La futura operacion de captura debe obtener actor/empresa y fuentes en servidor,
con permisos SIS y validaciones de contexto; todavia no se expone ese flujo.
El SDK tecnico de QA acredita almacenamiento/replay, no una sesion SIS autenticada.
Ver ArchivoCapsulaPrenomina.md y ArchivoCapsulaManifest.json en el requerimiento.


## Captura de prenomina desde servidor (publicada en QA 2026-11-22)

JWT normal con sesion SIS, no token OracleAnalysis. Requiere rh.nomina.simular Y
rh.nomina.fuentes.datos, o superusuario segun la seguridad existente. Empresa desde
settings:EmpresaConfig y actor de la identidad validada en SIS. Ninguno se acepta
en el request. No hay paginacion para estas tres operaciones.

| Ruta POST bajo /api/RhMotorNomina | Body | Respuesta data |
| --- | --- | --- |
| capturaPrenominaCrear | claveSolicitud, codigoTipoNomina, codigoPeriodo, codigoPersona; politicaPresupuesto/motivoPresupuesto opcionales | Resumen con cabecera del archivo, hashes, conteos, totales y contexto congelado |
| capturaPrenominaGet | idCapsula | Cabecera verificada del archivo propio |
| capturaPrenominaReproducir | idCapsula | Mismo resumen recalculado desde el archivo, sin leer movimientos actuales |

Crear no recibe JSON de capsula, importes, formulas, declaraciones manuales,
fecha, clase N/E, presupuesto numerico, empresa o actor. Los campos desconocidos
se rechazan con400; el servidor lee sus fuentes. Los bonos manuales se definen
solo en RhMotorNomina:BonosManualesE del setting servidor y quedan congelados.

Ejemplo de creacion con presupuesto coincidente; usar IDs reales de la empresa:

```json
{
  "claveSolicitud": "PRENOMINA:REVISION:001",
  "codigoTipoNomina": 10,
  "codigoPeriodo": 5000,
  "codigoPersona": 123,
  "politicaPresupuesto": "COINCIDIR_PERIODO"
}
```

La politica predeterminada COINCIDIR_PERIODO rechaza presupuestos distintos o
faltantes. Para diagnostico explicito con presupuesto actual del tipo se permite
TIPO_ACTUAL_EXPLICITO con motivo de10..500 caracteres cuando hay diferencia:

```json
{
  "claveSolicitud": "PRENOMINA:DIAGNOSTICO:001",
  "codigoTipoNomina": 10,
  "codigoPeriodo": 5000,
  "codigoPersona": 123,
  "politicaPresupuesto": "TIPO_ACTUAL_EXPLICITO",
  "motivoPresupuesto": "Analisis de fuentes actuales con presupuesto distinto al periodo; sin liquidacion."
}
```

Se conservan presupuesto del periodo (incluso0/null), presupuesto actual en
archivo.ambito.codigoPresupuesto, precierre, politica, motivo y aislamiento.
Una diferencia de presupuestos mantiene estado BLOQUEADO incluso con politica
explicita. El precierre permite leer para diagnostico, sin modificarlo ni conceder
permiso financiero. Un periodo con FECHA_CIERRE se rechaza para captura actual.
No se elige MAX de periodo ni se convierten datos actuales en entrada historica:
el ID del periodo es explicito, los movimientos son actuales.

Ejemplo resumido de respuesta de creacion/reproduccion (se omiten en este ejemplo
campos de cabecera/hashes, no son propiedades ausentes del contrato real):

```json
{
  "isValid": true,
  "message": "Captura del servidor archivada para diagnostico; no se calcularon formulas ni se modificaron movimientos.",
  "data": {
    "archivo": { "idCapsula": 12, "versionDocumento": "CAPSULA_PRENOMINA_V3" },
    "clase": "E",
    "estado": "BLOQUEADO",
    "hashSeleccion": "<SHA256>",
    "hashTotales": "<SHA256>",
    "movimientos": 11,
    "incluidos": 1,
    "excluidos": 10,
    "bloqueados": 0,
    "manualesDeclaradas": 1,
    "eIncluidosSinRegla": 0,
    "totales": [{ "codigoPersona": 123, "filasIncluidas": 1, "estado": "VALIDADO", "asignaciones": "100", "deduccionesFirmadas": "0", "neto": "100", "motivos": [] }],
    "recuperada": false,
    "periodoCapturado": {
      "metodo": "CAPTURA_SERVIDOR_V1",
      "clase": "E",
      "presupuestoPeriodo": 0,
      "fechaPreCierre": "2025-12-01T10:00:00",
      "aislamiento": "SERIALIZABLE",
      "politicaPresupuesto": "TIPO_ACTUAL_EXPLICITO",
      "motivoPresupuesto": "Diagnostico explicito; sin liquidacion."
    },
    "movimientosAplicados": false,
    "formulasNominaRecalculadas": false,
    "paridadHistoricaCertificada": false,
    "propiedadFinancieraAcreditada": false
  }
}
```

La cabecera completa contiene idCapsula, codigoEmpresa, actor, claveSolicitud,
hashSolicitud, hashDocumento, versionDocumento, ambito y fechaIns. ambito contiene
codigoPresupuesto/codigoPersona y periodo: codigoEmpresa, codigoTipoNomina,
codigoPeriodo, fechaNomina, numeroPeriodo. No es un payload que el cliente pueda
reenviar para autorizar aplicacion. Get/Reproducir reciben solo:

```json
{ "idCapsula": 12 }
```

IsValid=true indica que la captura/lectura funciono. estado BLOQUEADO, SIN_PARTIDAS
o DIAGNOSTICO no autoriza liquidacion. totales[].estado=VALIDADO solo valida la
aritmetica. eIncluidosSinRegla cuenta E seleccionados sin politica; manualesDeclaradas
cuenta E declarados entre todas las imagenes preservadas, no solo seleccionados.
Los E preservados pero excluidos de N no se activan por esta operacion.

Los importes son strings exactos con punto; null no se transforma en cero.
Sin partidas, asignaciones/deducciones/neto son null. No convertir a float JS.
Los motivos de neto/fuentes estan en totales[].motivos; presupuestos distintos y
E sin regla se identifican ademas con el contexto/conteos del resumen.

La clave admite1..80 caracteres ASCII alfanumericos, guion, dos puntos y guion
bajo. Repetirla con los mismos tipo/periodo/persona/politica/motivo recupera el
archivo original, aun si cambio la configuracion manual actual; no recaptura.
Cambiar alguno de esos valores exige otra clave. El motivo se normaliza con Trim.
Un archivo de otro actor/empresa no se devuelve. No usar archivo de otro flujo
como captura de servidor: el reintento exige contexto de periodo V3 congelado.

Validaciones de negocio devuelven Ok(ResultDto) con isValid=false y message en
espanol. JWT ausente/invalido:401; permiso insuficiente:403; campos desconocidos o
JSON invalido:400; exceso del body:413. Limites8KiB para Crear y4KiB para Get/Replay.
Hasta2000 filas por fuente y2MiB por capsula; sin truncado. La respuesta no incluye
el JSON completo ni todas las imagenes; el archivo permanece en Oracle.

La captura usa una transaccion SERIALIZABLE y rollback de lectura; despues guarda
el archivo en otra transaccion. No bloquea cambios futuros ni constituye
revalidacion para aplicar una nomina. Los journals/controles financieros no se
modifican. Modelo/worker/aplicacion e HTTP/UI positivo SIS siguen pendientes.

## Captura completa de modelo y calculo de prenomina

POST `api/RhMotorNomina/capturaPrenominaCrearParaModelo` (8KiB):

```json
{"claveSolicitud":"PRE:202611:01","modeloId":1,"version":71,"codigoPeriodo":5961,"codigoPersona":123,"codigoRelacionCargo":456,"politicaPresupuesto":"COINCIDIR_PERIODO","motivoPresupuesto":null}
```

Respuesta ResultDto con `data` ResumenCaptura: `archivo.idCapsula`,
`archivo.versionDocumento=CAPSULA_PRENOMINA_V4`, `archivo.hashDocumento`,
`archivo.ambito`, `estado`, `clase`, `incluidos`, `excluidos`, `bloqueados`,
`eIncluidosSinRegla`, `recuperada`. No paginada; usa campos comunes ResultDto.
Empresa/actor autenticados; tipo/fecha/presupuestos derivados por servidor.
Relacion positiva explicita; no se escoge un cargo arbitrario. Periodo cerrado,
fuentes ausentes o revision/contexto distinto devuelven IsValid=false y mensaje.
TIPO_ACTUAL_EXPLICITO exige motivo10..500; discrepancia de presupuesto permanece
BLOQUEADO aunque se permita el diagnostico. No habilita liquidacion.

POST `api/RhMotorNomina/corridasCrearDesdeCaptura` (8KiB):

```json
{"claveSolicitud":"PRE:202611:01:CALC","modeloId":1,"version":71,"idCapsula":10}
```

Devuelve ResultDto<Corrida>, modalidad CAPTURA_PRENOMINA. Necesita
CorridasEnabled y CorridasCapturaEnabled. Reintento exacto devuelve mismo ID;
clave reutilizada para otro modelo/revision/archivo falla. Permisos JWT/SIS
rh.nomina.simular + rh.nomina.fuentes.datos en ambas operaciones.

Consultar corridasDetalle con `idRun` y `modo=CAPTURA_PRENOMINA`.
`resultado.captura` incluye idCapsula,hashCapsula,estadoCaptura,entradas tipadas,
errores y simulacion nullable con parametros, resultados, proyecciones y acciones.
Formato2 de entrada conserva archivo/modelo. V4 solo admite revision exacta;
V3 admite un subconjunto de proveedores y devuelve error si falta evidencia.
La simulacion no relee Oracle. AptaParaAplicacion,MovimientosAplicados y
ParidadHistoricaCertificada son false. La modalidad no permite consolidacion
financiera. Fuentes ausentes no entregan calculos parciales.

UI: en Corridas, capturar entradas con clave/modelo/revision/periodo/persona/cargo;
revisar estado y luego Calcular con estas entradas. Los cambios de formulario
invalidan la seleccion previa. Seguir la corrida y abrir Detalle. El listado
conserva su paginacion existente; la captura individual no agrega paginacion.

Aclaratoria E/F vigente: los E declarados (incluidas retenciones1374/1375/1376 del
12) se conservan; sus F se calculan por formula separada. La misma identificacion
de concepto no convierte un E en F. La politica de servidor contiene53 pares
explicitamente revisados; conceptos futuros sin declaracion requieren revision.
El detalle de CAPTURA_PRENOMINA presenta parametros, decisiones y resultados en
los visores habituales. Los archivos previos mantienen su politica congelada.


## Presupuesto actual segun contrato de nomina - V5 (2026-11-23)

`capacidades` agrega `presupuestoTipoActualContrato` y
`versionContratoPresupuesto=PRESUPUESTO_TIPO_ACTUAL_V1`. El frontend muestra la
politica `CONTRATO_TIPO_ACTUAL` solo cuando la API declara ambas capacidades.
Requiere `RhMotorNomina:ContratoPresupuestoTipoActualEnabled`, false por defecto.
Usar esta politica en capturaPrenominaCrear/CrearParaModelo; simularContextoActual
la rechaza para exigir archivo de servidor. Motivo opcional, hasta500 caracteres.

Para el periodo no informado0/null o coincidente se usa el presupuesto actual
positivo del tipo/RRC. Otro positivo/negativo se rechaza; no se corrige la BD.
Respuesta de captura V5 agrega `presupuesto`:

```json
{"efectivo":19,"periodo":0,"origen":"RH_TIPOS_NOMINA.CODIGO_PRESUPUESTO_ACTUAL","estado":"PERIODO_NO_INFORMADO","referencia":"R57_FORMS_UTIL_GET_TIPO_NOMINA"}
```

`periodoCapturado.contratoPresupuesto` congela version/origen/referencia/huella.
El archivo conserva el valor0/null y la revision exacta del modelo. V1..V4 no
se reinterpretan. Nuevas capturas pueden quedar DIAGNOSTICO si no hay otros
bloqueos; `AptaParaAplicacion` sigue false. E pendientes, signos/netos/datos ambiguos
continuan bloqueando. El contrato no agrega paginacion ni nuevos permisos.

Piloto ordinario QA solicitado: tipo10, periodo5696, fecha2026-11-23,numero2.
La fecha/presupuesto se derivan en servidor; no agregarlos al body de captura.
El usuario autenticado crea sus propios archivos; los IDs del actor tecnico de
las pruebas SDK no conceden acceso en otra sesion.


## Cadena persistida de sueldo previo y recalculo - 2026-11-23

POST `api/RhMotorNomina/corridasCrearEnlaceSueldo`. Usa los permisos
`rh.nomina.simular` y `rh.nomina.fuentes.datos`, empresa de configuracion y actor
SIS autenticado. Requiere CorridasEnabled y CorridasCapturaEnabled. No recibe
empresa, actor, entradas manuales ni documentos de revision desde el frontend.

```json
{
  "ClaveSolicitud": "CAP_ORD_USUARIO:CADENA",
  "ModeloId": 1,
  "Version": 71,
  "IdCapsula": 151,
  "ModeloSueldoId": 1,
  "VersionSueldo": 15,
  "PoliticaEnlace": "SUELDO_PREVIO_A_RECALCULO"
}
```

Los IDs/revisiones son ejemplos del modelo tecnico QA; no son reglas operativas.
IdCapsula debe proceder de una captura propia del mismo usuario. El archivo151
usado por el ensayo SDK no esta disponible a otros actores SIS. Primero crear la
captura propia mediante capturaPrenominaCrearParaModelo con el modelo posterior.

Responde `ResultDto<Corrida>` con isValid/data/message, sin paginacion adicional.
La corrida conserva modo CAPTURA_PRENOMINA y snapshot formato3. Se congelan la
revision de recalculo, la revision de sueldo y el archivo original. La validacion
de fuentes ocurre antes de encolar; las formulas se ejecutan en el worker.
Misma clave/cuerpo recupera el mismo IdRun. Cambiar captura, revisiones o politica
con la misma clave devuelve CLAVE_REUTILIZADA. Se rechazan IDs no positivos,
actores/empresas ajenos, politica desconocida, captura E, fuentes insuficientes y
hashes incompatibles. Los errores de dominio usan isValid=false conforme al slice.

Consultar con corridasGet/corridasDetalle, modo CAPTURA_PRENOMINA. Cancelacion,
listado paginado y propiedad de corridas mantienen su contrato. El resultado
agrega `enlaceSueldo`, separado de `captura` y `actual`:

```json
{
  "modo": "CAPTURA_PRENOMINA",
  "isValid": true,
  "enlaceSueldo": {
    "versionMetodo": "ENLACE_SUELDO_CAPTURADO_V1",
    "politica": "SUELDO_PREVIO_A_RECALCULO",
    "sueldo": { "idCapsula": 151, "simulacion": {} },
    "idMovimientoReferencia": 123456,
    "montoOriginal": "200",
    "montoDerivado": "250",
    "entradas": [{ "codigoVariable": "BASE", "proveedor": "ACUMULADOR_MOV_ACTUAL", "valorOriginal": "200", "valorDerivado": "250", "esNuevaCapturaOracle": false }],
    "recalculo": {},
    "movimientosAplicados": false,
    "aptaParaAplicacion": false
  },
  "movimientosAplicados": false,
  "paridadCertificada": false
}
```

Los objetos simulacion/recalculo conservan parametros, resultado del grafo,
proyecciones y decisiones de control. El ejemplo abrevia esas estructuras.
Las entradas derivadas incluyen evidencia original/derivada de acumuladores y PH;
no son nuevas capturas Oracle. No sumar las dos proyecciones como neto aceptado.
Sin F unico compatible, el trabajo queda ERROR con el mensaje de dominio; no crea
filas artificiales ni borra movimientos. La consolidacion financiera sigue
aceptando exclusivamente ACTUAL; esta cadena diagnostica no es aplicable.

Capacidades agrega corridasEnlaceSueldo y versionCorridasEnlaceSueldo
CORRIDA_ENLACE_SUELDO_V1. La UI muestra la opcion solo con esa capacidad/version.
En la captura, seleccionar Calcular sueldo previo al recalculo e indicar modelo y
revision de sueldo; revisar la captura y encolar. El detalle muestra las etapas y
los valores anteriores/derivados. Las claves CALC y CADENA son distintas para no
reutilizar una solicitud de otro flujo. Cambiar campos invalida la captura visible.
Formatos1/2 anteriores omiten enlaceSueldo y conservan su lectura y hashes.
Limites: cuerpo8192 bytes, entrada4MiB y resultado2MiB; no se truncan trazas.


### Lectura completa de documentos de corrida

Instalar Sql/11_RH_NOM_RUN_DOC.sql como RH antes de desplegar el lector nuevo.
INSTALL_RH_NOM_CFG.sql ya lo incluye. SP_RH_NOM_RUN_DOC devuelve un CLOB unico
filtrado por empresa/actor/ID y tipo ENTRADA/RESULTADO. El paquete lector por
fragmentos permanece disponible para clientes anteriores. No hay cambios de
payload HTTP ni de hashes/formato por esta optimizacion.

Se valida longitud antes de cargar, longitud/bytes UTF8 y hash contra contenido y
cabecera al terminar. Resultado pendiente nulo se conserva como null. Un actor o
empresa ajenos no reciben filas. QA verifico documentos formato1/2/3 y sus replay
exactos, ademas del rechazo de alcance ajeno. Las mediciones del worker se guardan
solo en logs de corridas lentas, sin datos personales ni tiempos en snapshots o
resultados. No cambian su determinismo ni habilitan aplicacion financiera.

### Prenomina ordinaria completa por periodo (QA)

Capacidades: `prenominaPeriodo`, `versionPrenominaPeriodo=LOTE_PRENOMINA_V1`
y `prenominaTemporal`. Flags independientes `RhMotorNomina:PrenominaEnabled`
y `RhMotorNomina:PrenominaTemporalEnabled`, deshabilitados por defecto.
No habilitan `AplicacionEnabled`, pagos, precierre ni cierre.
Instalar SP_RH_NOM_PRE_FUE.sql, SP_RH_NOM_CAP_DOC.sql, 12_RH_NOM_PRE_LOT.sql
 y 13_RH_NOM_PRE_TMP.sql; incluidos en INSTALL_RH_NOM_CFG.sql.

Todas las rutas son POST bajo `/api/RhMotorNomina`, con JWT/SIS real, empresa de
settings:EmpresaConfig y actor obtenido en el servidor. El token OracleAnalysis
no autentica este flujo. Respuesta `ResultDto<T>` habitual, errores esperados
`isValid=false`. Sin paginacion de servidor: maximo 2000 personas; la UI pagina
localmente, sin truncar personas. El cuerpo de creacion tiene limite 16 KiB.

| Ruta | Request | Permisos adicionales a fuentes.datos |
| --- | --- | --- |
| prenominaCrear | Solicitud siguiente | rh.nomina.simular, rh.nomina.configuracion.editar |
| prenominaConsultar | {"idLote":2} | rh.nomina.simular |
| prenominaAvanzar | {"idLote":2,"versionEsperada":1} | rh.nomina.simular, rh.nomina.configuracion.editar |
| prenominaCancelar | {"idLote":2,"versionEsperada":1} | rh.nomina.simular, rh.nomina.configuracion.editar |
| prenominaDetallePersona | {"idLote":2,"codigoPersona":123} | rh.nomina.simular |
| prenominaGuardarTemporal | {"idLote":2,"versionEsperada":265} | rh.nomina.ejecutar |
| prenominaRevertirTemporal | {"idLote":2,"versionEsperada":267} | rh.nomina.revertir |

El nombre completo del permiso de datos es `rh.nomina.fuentes.datos`.
Avanzar es una operacion recuperable por el worker; la pantalla solo consulta,
no mantiene abierto el calculo mediante requests. No envia montos ni personas
para construir la poblacion. Consultar otro actor/empresa no devuelve el lote.

Ejemplo de creacion del piloto QA (revisiones tecnicas, no modelo publicado):

```json
{"claveSolicitud":"PRE_EMPLEADOS_20261123_01","modeloId":1,"version":71,
 "modeloSueldoId":1,"versionSueldo":15,"codigoPeriodo":5696,
 "retirosPrevios":[
  {"campo":"CODIGO_CONCEPTO","valor":"761","tipos":["F","V","E"]},
  {"campo":"CODIGO_CONCEPTO","valor":"1137","tipos":["F","V","E"]},
  {"campo":"CODIGO_CONCEPTO","valor":"1640","tipos":["F","V","E"]},
  {"campo":"CODIGO_CONCEPTO","valor":"1811","tipos":["F","V","E"]},
  {"campo":"PADRE_MODULO","valor":"DL","tipos":["F"],"tipoConcepto":"D"}
 ]}
```

Los selectores son datos versionados con la solicitud, no ramas por ID en el
motor. Admiten CODIGO_CONCEPTO, EXTRA1 o PADRE_MODULO, tipo A/D opcional y tipos
F/V/E explicitos; hasta 20 selectores. El catalogo del tipo determina los retiros;
un concepto de otro tipo no se toca. La UI permite editar los IDs; los valores
iniciales reflejan el caller revisado, sin generalizar ANTIGUEDAD a otros IDs.

`data` contiene `idLote`, `version`, `hashSolicitud`, `documento`. El documento
conserva solicitud, ambas revisiones, contexto completo, casos y estado. Cada
caso muestra persona/relacion/nombre, estado/motivo, IDs de captura/corrida,
hash de proyeccion, asignaciones/deducciones/neto como strings y cantidad de
filas. Los totales globales son strings exactos y permanecen null mientras
existan casos pendientes/errores. Se distinguen excluidos y cargos vacantes.
El detalle reproduce filas conservadas/calculadas, seleccion/frecuencia y neto.

Estados: CALCULANDO -> LISTA o BLOQUEADA; LISTA -> GUARDANDO -> GUARDADA o
TEMP_ERROR. CANCELADA y REVERTIDA son terminales. La UI consulta cada 3 segundos
solo mientras calcula/guarda; una ejecucion puede recuperarse por numero.
Clave repetida recupera el mismo lote sin recapturar; otro request con esa clave
se rechaza. Avance/cancelacion/escritura usan version esperada y exclusion mutua.

Orden: sueldo; PH previa a retiros; retiros configurados; bases posteriores;
recalculo por grafo; omisiones finales; seleccion F/V y totales. Solo personal A
se recalcula; I/R elegible conserva movimientos. Relaciones multiples, destinos
repetidos, metadata faltante, errores de formulas o neto negativo bloquean el
lote completo. No se reemplazan por ceros. No se llama al paquete legacy.

Guardar es asincrono y atomico. Antes de escribir, el servidor reproduce y
verifica las huellas de las 132 proyecciones del piloto y conserva los bloqueos
NOWAIT. Las fuentes se comparan en otra conexion RH, bajo un snapshot SERIALIZABLE
de solo lectura. Ambas conexiones deben coincidir en base, servidor, usuario y
servicio; un contexto distinto se rechaza antes de escribir. La transaccion de
escritura usa READ COMMITTED con los locks existentes, temporal exclusivo y
version esperada; confirma filas, control y periodo juntos. Compara
periodo/poblacion/catalogo/frecuencias, movimientos y entradas
originales actuales. Cambios requieren crear una ejecucion nueva. No reemplaza
un RH_TMP_NOMINA existente ni filas de otro lote. Reserva nuevas identidades
con la secuencia existente, conservando NUMBER38 y metadatos de movimientos
conservados. RH_MOV_NOMINA permanece intacta. La fecha/usuario de prenomina y
el journal se confirman en la misma transaccion que el temporal.

La reversion exige mismo actor/lote y huella del temporal intacta; restaura el
temporal vacio y la fecha/usuario previos. Si un consumidor cambio las filas,
no las borra. Un trigger bloquea precierre/cierre exclusivamente en periodos
controlados por este temporal QA hasta su reversion. Esta entrega no certifica
paridad de las 138 muestras ni habilita liquidacion financiera.

### Recuperacion de fallos tecnicos del lote

`POST api/RhMotorNomina/prenominaReintentar`, body
`{"idLote":3,"versionEsperada":266}`, devuelve `ResultDto` con el mismo contrato
de lote. Requiere sesion SIS y permisos simular, fuentes.datos y config.editar;
la capacidad `prenominaReintentos=true`, version `REINTENTO_PRENOMINA_V1`,
controla el boton Reintentar fallo tecnico. No lleva empresa en el request.

Solo admite BLOQUEADA con un unico caso ERROR por fallo tecnico y sin resultado
guardado. Copia los bytes y hash de la captura archivada; no recaptura fuentes,
no recalcula los casos ya completados ni oculta la corrida fallida. La nueva
clave `PRE_<lote>_<persona>_RETRY_<corridaAnterior>` conserva la trazabilidad.
Controla actor, empresa, version esperada, revisiones y captura inmutables.
Los errores de negocio o los datos cambiados requieren corregir el contexto
y crear otra ejecucion. La UI conserva paginacion local y consulta cada 3 s.

Los avisos transitorios de falta de espacio se presentan en ErrorTemporal
sin alterar los documentos originales ni sus hashes; el avance confirmado
limpia el aviso. El cambio de dia de referencia Oracle es material: un lote
anterior puede consultarse pero no guardarse contra fuentes de otro dia.
