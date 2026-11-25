# Instalacion de la relacion de compromisos con orden de pago y cheques

Requerimiento 17. Feature implementada con fechas obligatorias y proveedores opcionales. El contrato de datos y las diferencias frente al RDF estan en [ContratoFrontendReporteRelacionCompOpPagos.md](ContratoFrontendReporteRelacionCompOpPagos.md).

## Instalar en Oracle

1. Ejecutar `GRANTS_ADM_REP_COP.sql` conectado como ADM. Son permisos SELECT directos para que PRE pueda compilar procedimientos con referencias a ADM.
2. Ejecutar `INSTALL_REP_COP.sql` conectado como PRE. Deben quedar VALID los cuatro procedimientos y USER_ERRORS sin filas para ellos. Si DefaultConnectionPRE utiliza otro usuario, otorgarle EXECUTE sobre los cuatro procedimientos desde PRE.
3. Desplegar el backend que incluye `REPORTE_REL_COP_PDF` y el catalogo `ADM_PROVEEDORES_COP`.
4. Con el nucleo, definicion, reportes y permisos MFO previamente instalados, ejecutar como MFO:

```sql
@Features/MotorFormularios/Sql/23_MFO_SEMILLA_REL_COP.sql <codigo_empresa>
```

Sustituir `<codigo_empresa>` por el valor de `settings:EmpresaConfig` del ambiente. La semilla crea y publica `REP_REL_COP` con exactamente tres campos y cinco mapeos (tres de entrada, empresa y usuario de sistema). Una segunda ejecucion conserva el formulario existente. Si el alias pertenece a otra empresa, aborta. No sobrescribe personalizaciones ni concede permisos generales.

5. Asignar los permisos del formulario y del reporte al rol/usuario destinatario mediante la administracion existente del motor.
6. Abrir `/apps/mfo/reporte/REP_REL_COP`. Validar primero solo con fechas y luego con varios proveedores; el PDF debe mostrarse dentro del visor existente.

La semilla es una instalacion independiente, igual que las semillas recientes de reportes; no se ejecuta automaticamente al iniciar el API ni se agrega al instalador inicial del nucleo MFO.

## Verificar datos

Comparar contra el mismo corte de Oracle del reporte legado, con un conjunto que incluya varias partidas por compromiso, varias ordenes, varios cheques, anulaciones y compromisos sin hijos. Los importes de hijos son imputaciones a la partida y deben conciliarse por su clave compuesta.

Confirmar especialmente que las imputaciones historicas tienen `ADM_PUC_ORDEN_PAGO.CODIGO_PUC_COMPROMISO` informado y correcto. La implementacion exige ese enlace real para evitar cruces de identificadores; no afirmar paridad con el legado sin comprobar las filas que el enlace excluye.

El reporte incluye compromisos de todos los presupuestos de la empresa presentes en el periodo; conserva el presupuesto en cada clave. Las fechas filtran compromisos y no recortan independientemente los hijos. No se incluyen asignaciones/modificaciones porque las ramas originales requieren un PUC individual que no forma parte de la entrada acordada.

Las tres consultas corren en una transaccion SERIALIZABLE para leer un mismo snapshot. Se propaga cancelacion y timeout a ODP.NET; si el snapshot ya no esta disponible, el API devuelve error sin generar un PDF con datos mezclados.

La semilla limita a 10000 filas contando bases, ordenes y cheques; al excederlas se devuelve ERROR para refinar filtros, sin documento parcial. El timeout inicial es de 240 segundos y debe ajustarse con las mediciones del ambiente.

## Verificaciones locales

Desde la raiz del workspace:

```sh
dotnet build OssmmasoftVerticalSlice/OssmmasoftVerticalSlice.csproj --no-restore
dotnet test OssmmasoftVerticalSlice.Tests/OssmmasoftVerticalSlice.Tests.csproj --no-restore --filter FullyQualifiedName~CopReporteTests
bash OssmmasoftVerticalSlice/scripts/check-oracle-identifiers.sh OssmmasoftVerticalSlice/Features/ReporteRelacionCompOpPagos
bash OssmmasoftVerticalSlice/scripts/check-oracle-identifiers.sh OssmmasoftVerticalSlice/Features/MotorFormularios/Sql/23_MFO_SEMILLA_REL_COP.sql
```

La variable opcional `COP_PREVIEW_DIR` en las pruebas guarda el PDF sintetico de paginacion en la carpeta indicada. Las pruebas no necesitan Oracle. El proyecto conserva advertencias previas; una advertencia NU1900 indica que no se pudo consultar la auditoria de NuGet, no una validacion de vulnerabilidades satisfactoria.

## Estado de validacion

Compilacion verificada; 10 pruebas nuevas del reporte y 75 pruebas en la ejecucion conjunta con MotorFormularios superadas. Se reviso el PDF sintetico renderizado, incluidas las continuaciones y el cierre. Los scripts cumplen el control de identificadores de Oracle (30 caracteres maximo). Desplegado en QA el 2026-11-25: cuatro procedimientos VALID, formulario/reporte 41 publicados, CJHONNY con VER/EXPORTAR y ejecucion especifica. Se verificaron consulta, catalogo, filtros opcionales/multiples y PDF real a traves del motor. Evidencias en `Requerimientos/17 - RelacionCompromisoOrdenPagoPagos/DespliegueQA/`. La comparacion financiera contra el corte original sigue pendiente: QA tiene datos distintos, el PDF fuente se pudo renderizar para comparar el diseno tras corregir CreatorDate en una copia temporal, sin modificar el original.

Para retirar el acceso, inactivar el reporte/formulario nuevo conservando las ejecuciones de bitacora. El reporte independiente del requerimiento 25 permanece sin cambios.
