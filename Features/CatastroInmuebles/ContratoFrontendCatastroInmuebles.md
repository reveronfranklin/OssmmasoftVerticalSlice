# Contrato frontend - Consulta de inmuebles

## Endpoint

- Metodo: `POST`
- Ruta: `/api/CatastroInmuebles/GetAll`
- Autenticacion: JWT mediante el interceptor normal de NextOssmasoft.

## Request

```json
{
  "pageSize": 10,
  "pageNumber": 1,
  "searchText": "001-01"
}
```

`pageSize` se limita en API al rango 1-100. `pageNumber` inicia en 1. `searchText` es opcional y busca por codigo catastral, nombre, numero de inmueble, codigo interno o codigo de contribuyente. El codigo de empresa se obtiene de `settings:EmpresaConfig` y no forma parte del request.

## Response exitosa

```json
{
  "data": [
    {
      "codigoInmueble": 123,
      "codigoCatastro": "001-01",
      "codigoContribuyente": 456,
      "nombreInmueble": "Casa principal",
      "numeroInmueble": "12",
      "area": 180,
      "valorInmueble": 250000,
      "valorTerreno": 100000,
      "valorConstruccion": 150000,
      "codigoParcela": 50,
      "codigoFicha": 90,
      "observacion": null
    }
  ],
  "isValid": true,
  "message": "success",
  "cantidadRegistros": 1,
  "page": 1,
  "totalPage": 1
}
```

## Errores

La API conserva HTTP 200 y devuelve `isValid: false` con un mensaje funcional o tecnico conciso, siguiendo el convenio actual del backend. El frontend debe mostrar el mensaje y conservar la posibilidad de reintentar.

## Detalle por identificador

- Metodo: `POST`
- Ruta: `/api/CatastroInmuebles/getById`

```json
{
  "codigoInmueble": 123
}
```

Devuelve un objeto con los campos generales de la consulta y la direccion principal confirmada en `CAT_DIRECCIONES`: codigos geograficos, vialidad, vivienda, unidad y complemento. Si no existe direccion, esos campos son `null`. Este incremento no devuelve aun caracteristicas, documentos, folio, roles o usos.

## Detalles relacionados

- Metodo: `POST`
- Ruta: `/api/CatastroInmuebles/getRelated`

```json
{
  "codigoInmueble": 123
}
```

La respuesta devuelve siempre las siguientes colecciones cuando `isValid` es `true`:

```json
{
  "data": {
    "caracteristicas": [],
    "documentosLegales": [],
    "foliosReales": [],
    "otrosDatos": [],
    "roles": [],
    "usosZonificacion": [],
    "multiusos": []
  },
  "isValid": true,
  "message": "success"
}
```

Las colecciones vacias se representan como `[]`, no como `null`. Los codigos de caracteristicas y roles se entregan sin descripcion hasta confirmar los catalogos RM utilizados por Forms. Documentos legales se vinculan mediante la ficha; las demas colecciones respetan sus claves originales de inmueble, contribuyente y direccion.

## Alcance de este incremento

Estos endpoints son de solo lectura. Contribuyentes, propietarios, movimientos y edicion se agregaran cuando la ingenieria inversa del formulario legado confirme sus reglas.
