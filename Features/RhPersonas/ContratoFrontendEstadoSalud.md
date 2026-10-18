# Estado de salud de Personas - contrato e instalacion

Requerimiento 45. Fecha: 2026-10-18.
Estado: implementado en codigo; instalacion Oracle y pruebas integrales pendientes.

## Integracion y rutas

Se amplia el mantenimiento existente de Personas en la API legacy. No se migra
el CRUD completo ni se introduce otro endpoint de guardado. El cliente sigue
siendo ossmmasofApi para Personas y descriptivas. La API vertical contiene los
scripts RH y este contrato; no se agregan endpoints ejecutables en ella.

RhPersonaController ahora requiere Bearer mediante Authorize, incluidas consultas
y mantenimiento. Las pantallas existentes ya envian el token. Los consumidores
que antes llamaban sin autenticar deben enviar sus credenciales vigentes.
Create/Update tambien rechazan usuarios sin identidad SIS resuelta para auditoria.
No se agrego un sistema nuevo de permisos RH ni se presume autorizacion granular
que no existe en el mantenimiento actual.

| Metodo/ruta | Contrato |
| --- | --- |
| POST `/api/RhPersona/GetPersona` | `{ "codigoPersona": 123 }`, devuelve PersonasDto sin envoltura |
| POST `/api/RhPersona/Create` | DTO completo existente mas estadoSaludId opcional; ResultDto<PersonasDto> |
| POST `/api/RhPersona/Update` | DTO completo existente mas estadoSaludId opcional; ResultDto<PersonasDto> |
| GET `/api/RhPersona/GetAll` | ResultDto con lista simple; incluye ID/descripcion de salud |
| GET `/api/RhPersona/GetAllSimple` | Mismo enriquecimiento de la lista simple |
| POST `/api/RhDescriptivas/GetByTitulo` | `{ "descripcionId": 0, "tituloId": 72 }`, arreglo id/descripcion |

No se agrega paginacion a estas operaciones ni se interpretan valores por defecto
de ResultDto como un paginado real. GetPersona conserva su respuesta sin wrapper;
una consulta fallida no debe confundirse con un trabajador sin clasificacion.
Empresa procede de settings:EmpresaConfig en servidor; no se acepta override
en el cuerpo. Los lookups de persona/cedula y la lista se acotan a esa empresa.

## Campo nuevo y compatibilidad

Solicitud C#: RhPersonaUpdateDto.EstadoSaludId, JsonElement para diferenciar
propiedad ausente de null explicito. Valor almacenado: nullable int/NUMBER(5,0).

- Create omitido o null: sin clasificacion.
- Update omitido: conserva el valor; la propiedad EF no se marca como modificada,
  evitando sobrescribir una clasificacion concurrente desde un cliente anterior.
- Update null explicito: limpia el valor.
- Entero positivo de 1 a 99999: exige descriptiva existente del titulo 72.
- Cadenas, objetos, arreglos, booleanos, fracciones, cero o fuera de rango: rechazo.
- Valor historico positivo sin descripcion: se conserva si no cambia; no se puede
  introducir como una nueva clasificacion. Los historicos numericamente invalidos
  pueden conservarse omitiendo el campo, nunca como nueva seleccion.

El formulario de modificacion omite salud mientras el usuario no cambie el selector.
Alta envia su seleccion o null. El selector no admite texto libre ni clasifica por
defecto. Mantiene carga/error/vacio, busqueda, seleccion conocida y reintento.
La etiqueta junto a Detalle muestra descripcion, Sin especificar para null o
Descripcion no disponible cuando existe ID sin descripcion del titulo 72.
Familiares conserva su dato independiente.

Ejemplo de cuerpo completo de edicion; los IDs son ilustrativos y deben sustituirse
por valores reales de los catalogos del ambiente:

```json
{
  "codigoPersona": 123,
  "cedula": 12345678,
  "nombre": "Maria",
  "apellido": "Perez",
  "nacionalidad": "V",
  "sexo": "F",
  "edad": 36,
  "fechaNacimiento": "21/09/1990",
  "paisNacimientoId": 1,
  "estadoNacimientoId": 1,
  "numeroGacetaNacional": "",
  "fechaGacetaNacional": "",
  "estadoCivilId": 10,
  "estatura": 1.65,
  "peso": 60,
  "manoHabil": "D",
  "status": "A",
  "identificacionId": 11,
  "numeroIdentificacion": 12345678,
  "data": "",
  "nombreArchivo": "",
  "estadoSaludId": 7201
}
```

El cambio no redefine los demas campos: mantienen sus validaciones/formatos y
comportamiento anteriores, incluida la gestion separada de imagenes existente.

Create/Update exitoso, respuesta abreviada:

```json
{
  "isValid": true,
  "message": "",
  "data": {
    "codigoPersona": 123,
    "estadoSaludId": 7201,
    "estadoSaludDescripcion": "Descripcion real del catalogo"
  }
}
```

La respuesta contiene tambien los demas campos de PersonasDto. Un error de
validacion devuelve HTTP 200 con isValid false y message en espanol, por ejemplo:

```json
{ "isValid": false, "message": "Estado de salud no pertenece al titulo 72.", "data": null }
```

Un rechazo de Oracle puede incluir Error tecnico/ORA y pila; no depender del
texto exacto para determinar exito. Sin autenticacion: 401. JSON mal formado:
puede responder 400. Ante respuesta incierta, el formulario bloquea otro guardado
 y pide consultar el trabajador antes de repetir; no se anuncian exitos falsos.

## Persistencia y reglas Oracle

Se amplia la entidad RH_PERSONAS que ya utiliza el mantenimiento legacy, sin
agregar ORM, repositorios ni otra arquitectura. El estado se persiste junto con
los datos generales en el mismo SaveChanges existente. No hay una segunda llamada
HTTP para salud ni un guardado independiente despues de crear la persona.

RH.TR_RH_PER_SALUD llama RH.SP_RH_PER_SALUD_VAL en la misma sentencia para cada
clasificacion nueva/cambiada. Un rechazo revierte esa sentencia y el guardado;
no cambia los datos generales dejando el estado rechazado. El trigger permite
limpiar y conservar historicos sin cambio, sin desactivar validacion de nuevas
selecciones. No usa transaccion autonoma ni COMMIT.

La API valida tambien la seleccion antes de persistir y respeta el resultado
real del repositorio; no devuelve exito si Update fallo. La auditoria existente
del trabajador se conserva. No se cambian los datos de salud de Familiares.

## Instalacion y publicacion

1. Guardar el DDL vigente y comprobar opciones del titulo 72 en el ambiente.
2. Desde esta carpeta, como RH o con privilegios equivalentes, ejecutar:

   ```sql
   @INSTALL_RH_PER_SALUD.sql
   SHOW ERRORS PROCEDURE RH.SP_RH_PER_SALUD_VAL;
   SHOW ERRORS TRIGGER RH.TR_RH_PER_SALUD;
   ```

3. El instalador agrega/verifica ESTADO_SALUD_ID NUMBER(5,0) nullable y crea
   procedimiento/trigger. Detiene incompatibilidades; no reclasifica historicos.
   Oracle DDL confirma implicitamente: no considerar la instalacion una
   transaccion reversible automaticamente.
4. Verificar objetos VALID y trigger ENABLED. Si el servicio consulta bases RH
   distintas mediante contextos compartidos del legacy, aplicar la columna en
   esos esquemas antes de publicar: la entidad se usa tambien desde esos contextos.
5. Publicar primero la API legacy de Ossmmsoft_convertidor-main y luego frontend
   de NextOssmasoft. La API vertical no necesita endpoints nuevos para este campo.
   El push no ejecuta los scripts Oracle automaticamente.
6. Reverso: recuperar aplicaciones previas conservando columna, datos y regla de
   integridad. No eliminar automaticamente clasificaciones ni la columna.

Identificadores contados: ESTADO_SALUD_ID (15), SP_RH_PER_SALUD_VAL (19),
TR_RH_PER_SALUD (15), RH_PERSONAS (11). Compatibles con Oracle 10g.

No se ejecuto SQL ni se publico codigo durante esta implementacion.
