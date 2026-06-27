# Pruebas en ambiente de prueba - Bienes Municipales

## Objetivo

Validar el modulo BM publicado contra Oracle de prueba, separando fallas de:

- Menu SIS.
- Conexion `DefaultConnectionBM`.
- Conexion `DefaultConnectionBMC`.
- Grants/permisos de objetos BM, BMC y PRE.
- Endpoints y pantallas frontend.

## Orden recomendado

1. Instalar/actualizar objetos SQL de la feature:
   - `Sql/00_INSTALL_BM_ENT1.sql`
   - `Sql/01_INSTALL_BM_ENT2.sql`
   - `Sql/02_INSTALL_BM_ENT3.sql`
   - `Sql/03_INSTALL_BM_ENT4.sql`
   - `Sql/04_INSTALL_BM_ENT5.sql`
   - `Sql/05_INSTALL_BM_ENT6.sql`
   - `Sql/09_INSTALL_BM_ENT7.sql`
   - `Sql/10_INSTALL_BM_ENT8.sql`
2. Instalar/actualizar menu:
   - `Sql/06_SEED_BM_MENU.sql`
3. Ejecutar grants runtime si el usuario de conexion no es el owner:
   - `Sql/08_GRANT_BM_RUNTIME.sql`
4. Validar objetos/grants/menu desde los usuarios de conexion:
   - `Sql/07_VAL_BM_GRANTS.sql`
5. Publicar backend y configurar:
   - `ConnectionStrings:DefaultConnectionBM`
   - `ConnectionStrings:DefaultConnectionBMC`
   - `settings:EmpresaConfig`
   - `settings:BmFiles`
6. Ejecutar smoke tests HTTP:
   - `BmSmokeTests.http`
7. Probar pantallas frontend desde el menu.

## Lectura rapida de fallas

- Si fallan `Bm1`, `BmBienes`, `BmUbicaciones`, `BmMovBienes`, `BmReportes/Placa` o `ReporteBm1`, revisar `DefaultConnectionBM`.
- Si fallan `BmConteo`, `BmConteoDetalle`, `BmConteoHistorico`, `BmReportes/ConteoDiferencias` o `BmReportes/ConteoHistorico`, revisar `DefaultConnectionBMC`.
- Si fallan `BmProcesosMasivos` o los reportes de lote/ficha/solicitudes/procesos masivos, validar que se haya ejecutado `Sql/09_INSTALL_BM_ENT7.sql` y `Sql/10_INSTALL_BM_ENT8.sql`.
- Si `BMC.BM_P_CONTEO` falla al abrir conteo, validar que BMC pueda leer `BM.BM_V_BM1`.
- Si BM1 falla con error de paquete/funcion, validar `BM.BM_PKG_UTIL`.
- Si ubicaciones o BM1 fallan al resolver ICP, validar acceso a `PRE.PRE_INDICE_CAT_PRG`.
- Si el menu no aparece, validar `SIS.OSS_MENU`, `SIS.OSS_MENU_PERM`, `SIS.OSS_ROL_MENU` y que el usuario tenga rol `BM_MENU` o `BM_USUARIO`.

## Pantallas esperadas

- `/apps/Bm/Bm1`
- `/apps/Bm/BmContar`
- `/apps/Bm/BmConteo`
- `/apps/Bm/BmConteoDetalle`
- `/apps/Bm/BmConteoDetalleCompare`
- `/apps/Bm/BmConteoHistorico`
- `/apps/Bm/BmPlacasCuarentena`
- `/apps/Bm/ReporteBM1`
- `/apps/Bm/BmBienes`
- `/apps/Bm/BmCatalogos`
- `/apps/Bm/BmUbicaciones`
- `/apps/Bm/BmMovimientos`
- `/apps/Bm/BmProcesosMasivos`
- `/apps/Bm/BmReportes`

## Criterio minimo de aprobacion

- El menu muestra todas las opciones BM.
- `Bm1/GetListICP` responde `isValid = true`.
- `BmBienes/GetAll` responde `isValid = true`.
- `BmConteo/GetAll` responde `isValid = true`.
- Se puede abrir un conteo y el detalle se pobla desde `BM.BM_V_BM1`.
- Se puede consultar diferencias e historico de conteos.
- Se puede previsualizar un proceso masivo y ver rechazos/errores por placa.
- Los reportes PDF se muestran en preview y Excel descarga archivo.
