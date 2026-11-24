# Integracion de configuracion en QA

ConfiguracionQA.cs.txt es el runner utilizado para la instalacion inicial y pruebas.
Se copia como Program.cs a un proyecto de consola temporal net10.0 en QA, con
FrameworkReference Microsoft.AspNetCore.App y referencias al DLL nuevo del backend,
Oracle.ManagedDataAccess.dll y Microsoft.Data.SqlClient.dll del release vigente.
No inicia el host ASP.NET ni sus workers, ni publica la API.

Argumentos: --qa-install-test <directorio SQL> <directorio resultados privado>.
Usa exclusivamente /srv/ossmmasoft-pruebas/shared/config/vertical.json, comprueba
identidades SIS/RH contra el destino verificado y exige empresa 13. Validar tambien
la identidad real del destino antes de copiar/ejecutar (ver evidencia R57 QA).
Nunca copiar el settings a la carpeta de pruebas ni imprimir cadenas de conexion.

El runner instala los tres scripts y genera datos sinteticos etiquetados en el
catalogo SIS y tablas nuevas RH, sin ejecutar el paquete antiguo. Reserva un hueco
de ID de modelo bajo bloqueo corto, no MAX+1 ni IDs grandes; excluye modelos ya
referenciados por conceptos. Usa un actor numerico sintetico no existente en SIS,
que identifica fixtures, no una identidad de usuario real ni una prueba JWT.

Se conservan fixtures para inspeccion; el trigger impide borrar/editar revisiones.
No eliminar/desactivar ese trigger para limpiar a ciegas. installation.json permite
reanudar una instalacion inicial propia interrumpida; si existen objetos sin ese
recibo, revisar antes de reemplazarlos. DDL Oracle tiene commits implicitos.

result.json/run.log registran comprobaciones sin credenciales ni salarios reales.
El estado vigente de QA y la prueba adicional de guardado SQL con referencias viejas
estan en Requerimientos/57 - Motor Parametrizable De Calculo De Nomina/ValidacionConfiguracionQA.md.
Las pruebas directas no certifican autenticacion/permisos HTTP ni nominas historicas.

## Contexto historico de solo lectura

FuentesQA.cs.txt instala exclusivamente SP_RH_NOM_CTX_HIST y prueba contextos
agregados; no crea fixtures ni inicia la API. Proyecto temporal net10.0 con referencia
Oracle.ManagedDataAccess.dll del release. Argumento: ruta del script SQL. TZ del
proceso America/Caracas, config privado original sin copiar. Verifica identidad RH,
empresa 13 y exige nombre nuevo no existente antes de instalar; no es para repetir
sobre un objeto ya instalado. No quitar esa proteccion para reemplazar objetos ajenos.

28 comprobaciones y 18 contextos de nueve tipos. Resultado agregado sin importes
ni IDs personales en ResultadoFuentesQA.json. El paquete anterior se verifica por
hash sin ejecutarlo. Para reinspeccion usar consultas de lectura, no reinstalacion.

## Snapshot de cargo individual

CargoSnapshotQA.cs.txt instala unicamente SP_RH_NOM_CAR_HIST. Proyecto net10.0
con Oracle.ManagedDataAccess.dll vigente. Argumentos: script SQL y JSON de los
contextos agregados ya verificados. Comprueba identidad y nombre de objeto nuevo.
Lee una muestra explicita por contexto solo en memoria; no imprime ni persiste
sueldos/IDs. 65 checks, 18 contextos de nueve tipos. No es runner repetible sobre
objeto ya instalado ni certifica paridad/JWT. Evidencia en ResultadoCargoSnapshotQA.json.

## Referencia de partidas

ReferenciaQA.cs.txt instala SP_RH_NOM_REF_HIST (nombre nuevo) y compara las filas de
18 muestras contra su fuente, solo en memoria. Mismos requisitos de proyecto/config
privada/TZ que CargoSnapshotQA; argumentos SQL y JSON de contextos agregados.
La seleccion de persona es muestreo del runner, no un comportamiento del endpoint.
No imprime documentos/IDs/importes; evidencia en ResultadoReferenciaQA.json.
192 checks y 82 partidas leidas; NLS decimal coma solo en esa sesion. No certifica
paridad ni JWT. No repetir sobre objeto instalado ni modificar triggers para pruebas.

## Auditoria de conceptos

ConceptoAuditoriaQA.cs.txt instala SP_RH_NOM_CON_HIST y verifica todos los conceptos
positivos de los 18 contextos, con limite explicito de 500 combinaciones. Mismo
proyecto/config/TZ que los runners anteriores; argumentos SQL y contextos JSON.
596 checks sobre 117 combinaciones. No imprime imagenes, IDs personales ni importes.
ResultadoConceptosQA.json conserva conteos y checks. 116 sin evento posterior y una
imagen posterior unica; no son candidatos certificados ni prueba de paridad.
No repetir instalacion sobre nombre existente ni cambiar auditorias para fabricar casos.

## Publicacion acumulada

ModuloPreflightQA.cs.txt verifica identidad/objetos sin escribir. ModuloIntegracionQA.cs.txt
se ejecuta contra el DLL nuevo, con FrameworkReference Microsoft.AspNetCore.App y
referencias Oracle, Microsoft.Data.SqlClient y API vigentes. No inicia el host ni workers.
Usa el fixture Q57_0523F8B5 (modelo 1) y exige revision inicial 3; agrega revision 4,
por lo que NO es repetible a ciegas. Actor sintetico, no usuario autenticado. Comprueba
unidades/vigencias/CLOB, legacy intacto y lecturas de handlers sin exportar importes.
22 preflight/27 checks positivos; no certifican JWT HTTP ni paridad. Ver PublicacionQA.md.
