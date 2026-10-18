# Contrato frontend - RhFamiliares

Requerimiento 42. API vertical; las pantallas de Familiares ya utilizan
`ossmmasofApiVertical`. Parentesco, nivel educativo y estado de salud siguen
consultandose por el cliente legacy `ossmmasofApi`.

## Rutas y autorizacion

Todas las operaciones son POST, JSON y requieren Bearer valido (`Authorize`).
Se conserva el alcance de acceso autenticado del controlador legacy. Empresa
siempre desde `settings:EmpresaConfig`; nunca se recibe del frontend.
La auditoria de alta/edicion resuelve el login del token contra `SIS.SIS_USUARIOS`
para esa empresa y un usuario activo. No se acepta un usuario arbitrario del cliente.

| Ruta | Solicitud | Data de respuesta |
| --- | --- | --- |
| `/api/RhFamiliares/GetByPersona` | `{ "codigoPersona": 123 }` | Arreglo de familiares |
| `/api/RhFamiliares/Create` | Familiar de alta | Familiar guardado |
| `/api/RhFamiliares/Update` | Familiar de edicion | Familiar guardado |
| `/api/RhFamiliares/Delete` | `{ "codigoFamiliar": 456 }` | Mismo identificador |

No hay paginacion; se devuelve la lista completa de la persona dentro de la empresa.
`page`, `totalPage` y `cantidadRegistros` conservan el valor 0 de ResultDto, sin
representar metadatos de paginacion para esta consulta.

## Alta y edicion

Ejemplo de cuerpo completo (el identificador de estado es ilustrativo; usar uno
real del titulo 72):

```json
{
  "codigoPersona": 123,
  "codigoFamiliar": 456,
  "cedulaFamiliar": 12345678,
  "nacionalidad": "V",
  "nombre": "Maria",
  "apellido": "Perez",
  "fechaNacimientoString": "21/09/2020",
  "parienteId": 10,
  "sexo": "F",
  "nivelEducativo": 0,
  "grado": 0,
  "estadoSaludId": 7201
}
```

En Create se ignora codigoFamiliar y se genera la clave. `edad` no es un campo
persistible; se calcula desde la fecha. Se tolera el campo enviado por consumidores
anteriores. Fecha admite `dd/MM/yyyy`, `yyyy-MM-dd` e ISO con hora y zona.
La fecha del datepicker se conserva como fecha calendario; no se convierte a UTC.

Estado de salud:

- Create omitido o null: sin clasificacion.
- Update omitido: conserva el valor existente.
- Update null explicito: limpia la clasificacion.
- Numero entero de 1 a 99999: valida pertenencia al titulo 72.
- Valores cero, negativos, fraccionarios, cadenas, objetos y booleanos: invalidos.
- Un valor historico que ya no resuelva descripcion se conserva si no cambia;
  no se permite introducir ese valor en otro familiar.

Persona, familiar al editar y cedula deben ser positivos; nombre y apellido no
pueden estar vacios. Nacionalidad V/E, sexo M/F y parentesco existente.
Se conservan nivel educativo y grado sin introducir validaciones nuevas sobre
estos campos. La persona y el familiar deben pertenecer a la empresa configurada;
al editar se comprueba tambien la asociacion del familiar con la persona.

## Respuesta

Ejemplo de GetByPersona (campos de enlaces y totales adicionales de ResultDto
pueden aparecer con sus valores por defecto):

```json
{
  "isValid": true,
  "message": "success",
  "data": [{
    "codigoPersona": 123,
    "codigoFamiliar": 456,
    "cedulaFamiliar": 12345678,
    "nacionalidad": "V",
    "nombre": "Maria",
    "apellido": "Perez",
    "fechaNacimiento": "2020-09-21T00:00:00",
    "fechaNacimientoString": "21/09/2020",
    "fechaNacimientoObj": { "year": "2020", "month": "09", "day": "21" },
    "edad": "6 Años",
    "parienteId": 10,
    "parienteDescripcion": "Descripcion del parentesco",
    "sexo": "F",
    "nivelEducativo": 0,
    "grado": 0,
    "estadoSaludId": 7201,
    "estadoSaludDescripcion": "Descripcion del catalogo"
  }],
  "page": 0,
  "totalPage": 0,
  "cantidadRegistros": 0
}
```

Edad es dinamica; el ejemplo corresponde a octubre de 2026. Create/Update devuelven
un objeto en data, no un arreglo. Sin estado: `estadoSaludId: null` y descripcion
vacia; UI muestra "Sin especificar". Con ID sin descripcion: UI muestra
"Descripcion no disponible". Las uniones externas mantienen los historicos visibles.

Los errores esperados devuelven HTTP 200 con `isValid: false`, `message` en espanol
 y data null; GetByPersona puede devolver lista vacia. Ejemplo:

```json
{ "isValid": false, "message": "Estado de salud no pertenece al titulo 72", "data": null }
```

Sin autenticacion: 401. JSON mal formado puede producir 400 por ApiController.
No se confirman cambios de base ante validacion o excepcion: la operacion usa
transaccion y solo confirma si el procedimiento devuelve success/suscces.

## Catalogo existente

POST legacy `/api/RhDescriptivas/GetByTitulo`:

```json
{ "descripcionId": 0, "tituloId": 72 }
```

Devuelve un arreglo sin envoltura ResultDto y sin paginacion:

```json
[{ "id": 7201, "descripcion": "Descripcion del catalogo" }]
```

La lista real depende de la configuracion institucional. No se crean ni se
precargan etiquetas. En UI, carga/error/vacio son estados diferentes; el error
permite reintentar y conserva la seleccion. La descripcion viaja solo para consulta.

## Instalacion y reverso

1. Confirmar opciones de `RH.RH_DESCRIPTIVAS` para titulo 72 y esquema destino.
2. RH requiere permiso SELECT directo sobre `SIS.SIS_USUARIOS` para compilar
   el procedimiento de auditoria; un rol no sustituye ese grant. El DBA debe
   concederlo si no existe antes de ejecutar el instalador.
3. Ejecutar `@INSTALL_RH_FAM.sql` desde esta carpeta con SQL*Plus/SQLcl como RH.
   Verifica/agrega `ESTADO_SALUD_ID NUMBER(5,0)` y compila tres procedimientos.
   No modifica valores historicos ni agrega catalogos. Oracle DDL confirma
   implicitamente; revisar el esquema antes de instalar.
4. Confirmar procedimientos VALID y otorgar EXECUTE al usuario de conexion de la
   API si es distinto de RH, conforme al mecanismo habitual del ambiente.
5. Publicar backend y luego frontend. Mantener API legacy de catalogos disponible.
6. Reverso: restaurar consumidor/API anteriores conservando columna y datos.
   No eliminar automaticamente la columna ni las clasificaciones guardadas.

`SP_RH_FAM_SAVE` genera claves con bloqueo exclusivo de tabla siguiendo el patron
RH existente. Coordinar el cambio de consumidores: escrituras legacy con MAX+1
no participan de ese protocolo y requieren evitar altas simultaneas durante la
transicion. No se modifica la aplicacion legacy en este requerimiento.

Scripts y codigo no se han instalado ni ejecutado contra Oracle en esta entrega.
