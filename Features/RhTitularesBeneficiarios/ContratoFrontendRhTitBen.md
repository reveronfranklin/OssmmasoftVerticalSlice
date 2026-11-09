# Titular-Beneficiarios - R55

POST `/api/RhTitularesBeneficiarios/GetByTipoNomina`, autenticacion Bearer.
El frontend usa ossmmasofApiVertical, reemplazando la llamada de este listado a
la API anterior. No cambia el mantenimiento de familiares ni direcciones.

## Solicitud y validacion

```json
{ "codigoTipoNomina": [{ "codigoTipoNomina": 20 }] }
```

De 1 a 300 codigos positivos distintos; empresa desde settings:EmpresaConfig,
sin override del cliente. Sin seleccion el frontend limpia la tabla sin consultar.
La consulta relaciona al trabajador mediante CODIGO_PERSONA y se limita a la
empresa configurada; la cedula se utiliza solamente como dato de presentacion.

## Respuesta

Conserva campos escalares del listado anterior: identificacion, nomina,
parentesco, fechas, edad, antiguedad, contactos del titular y datos bancarios.
Fragmento de ejemplo, omitiendo los demas campos existentes:

```json
{
  "isValid": true, "message": "", "page": 1, "totalPage": 1,
  "cantidadRegistros": 1,
  "data": [{
    "id": 1, "codigoPersona": 100, "codigoFamiliar": 460,
    "cedulaTitular": "V-10000000", "cedulaBeneficiario": "V-20000000",
    "emailFamiliar": "ana@example.com", "telefonoFamiliar": "+58 0412 0001234",
    "estadoDireccion": "disponible",
    "direccionTrabajador": "HABITACION | Calle 1 | Venezuela",
    "direccionSeleccionada": {
      "codigoDireccion": 1, "principal": true, "tipoDireccion": "HABITACION",
      "vialidad": null, "complementoDir": "Calle 1", "pais": "Venezuela",
      "estado": null, "municipio": null, "ciudad": null, "parroquia": null,
      "sector": null, "urbanizacion": null, "tipoVivienda": null,
      "vivienda": null, "tipoNivel": null, "nivel": null, "nroVivienda": null,
      "tenencia": null, "codigoPostal": null,
      "resumen": "HABITACION | Calle 1 | Venezuela"
    },
    "searchText": "texto que incluye contactos y direcciones"
  }]
}
```

Cada fila tambien devuelve direccionesTrabajador: array con todas las direcciones
con la misma estructura de direccionSeleccionada, incluida la seleccionada.
Campos ausentes son null. estadoDireccion: disponible, sin informacion, por definir.
Se selecciona la unica principal o, sin principal, la unica direccion registrada.
Multiples principales o multiples direcciones sin principal dejan seleccion null;
el dialogo de detalle permite consultar todas sin multiplicar filas.

El familiar se relaciona por CODIGO_PERSONA del trabajador y CODIGO_FAMILIAR.
La vista RH_V_TIT_BEN_KEYS expone ambas claves desde las tablas originales.
CODIGO_FAMILIAR es null para titulares: esas filas no reciben contacto de familiar.
No se utilizan cedula, nombre, fecha ni parentesco para localizar contactos.
Esto admite cedulas mostradas como MENOR o sin guion y parentescos que incluyen
estado de salud. Ante claves duplicadas no se asigna contacto. Los contactos del
titular (deEmail, nroTelefono, nroArea) se conservan separados.

Errores esperados: isValid=false, data=null, message en espanol. La pantalla
muestra el error, deshabilita Excel y descarta datos anteriores. Se cancelan las
solicitudes obsoletas al cambiar nomina.

## Paginacion, columnas, busqueda y Excel

La API devuelve el conjunto de nominas seleccionado en una pagina logica. La
paginacion visual (25, 50, 100), busqueda y ordenamiento se aplican al conjunto.
Buscar incluye contactos y todas las direcciones, aunque esten ocultos.

Inicialmente se muestran las diez columnas anteriores. Columnas permite buscar,
mostrar y ocultar contactos, direccion compuesta, cada componente, estado y detalle.
La seleccion permanece al filtrar, paginar y recargar datos en la misma instancia
de la vista; no se persiste entre sesiones. El detalle abre un dialogo de lectura.
Textos largos tienen tooltip; ausencias muestran Sin informacion.

Excel conserva el alcance anterior: todos los registros de las nominas
seleccionadas, independientemente de pagina y busqueda visual. Siempre incluye
campos actuales y adicionales, sin depender de la visibilidad. Los contactos y
telefonos se exportan como texto, valores ausentes como celdas vacias. No se
exportan objetos ni arrays como [object Object]. Se conservan los encabezados
anteriores y se agregan etiquetas en espanol para los campos nuevos. Se elimina
la repeticion de anosAntiguedadVaca y se sustituye la clave inexistente antiguedad
por antiguedadCmc y antiguedadOtros.

Orden completo de encabezados: excelFields y additionalColumns en
NextOssmasoft/src/rh/persona/beneficiarios/components/beneficiariosColumns.ts.

## Instalacion y validacion

1. Requiere R47 instalado: RH_FAMILIARES.EMAIL y NUMERO_COMUNICACION.
2. RH necesita SELECT directo sobre SIS.SIS_UBICACION_NACIONAL. La ficha utiliza
   EXTRA1 para las descripciones geograficas.
3. Ejecutar como RH @INSTALL_R55_TIT_BEN.sql desde esta carpeta: crea la vista
   especifica RH_V_TIT_BEN_KEYS y actualiza SP_RH_TIT_BEN_GET. Revisar SHOW ERRORS
   y estado VALID de ambos objetos. No se recrean tablas ni se cambia la vista
   original RH_V_TITULAR_BENEFICIARIOS.
4. Conceder EXECUTE al usuario de la API si se conecta con otro usuario, segun
   permisos habituales del ambiente.
5. Publicar frontend y backend juntos. Validar contactos de dos familiares,
   datos faltantes, direccion principal y casos ambiguos con datos reales.

SP_RH_TIT_BEN_GET y RH_V_TIT_BEN_KEYS tienen 17 caracteres; SQL compatible
Oracle 10g. La definicion original se leyo desde pruebas (XE / RH) mediante el
lector de metadatos del comparador el 2026-11-09. Se confirmo que no expone claves,
que concatena parentesco y estado de salud, y que usa otro formato de cedula para
familiares. La vista especifica conserva esa consulta de negocio y agrega las
claves desde A.CODIGO_PERSONA y RF.CODIGO_FAMILIAR, sin cruces por textos.
No se ha instalado DDL ni publicado esta correccion. No se consulto produccion.

La consulta propuesta se valido en Oracle de pruebas como CTE con WHERE 1=0:
36 columnas, incluidas las dos claves, sin obtener registros ni instalar DDL.
