# Contrato Frontend - ReportePersonal

## Endpoint

- Metodo: `POST`
- Ruta: `/api/ReportePersonal/GetAll`
- Stored procedure: `RH.SP_REPORTE_PERSONAL_GET_ALL`

## Request

```json
{
  "codigoTipoNomina": 2,
  "status": "A"
}
```

## Parametros

- `codigoTipoNomina`: opcional. Si se envia `null`, `0` o se omite, devuelve todos los tipos de nomina.
- `status`: opcional. Si se envia `null`, vacio o se omite, devuelve todos los estatus.

## Response

```json
{
  "data": [
    {
      "cedula": "12345678",
      "nombre": "APELLIDO NOMBRE",
      "fechaIngreso": "2024-01-15T00:00:00",
      "departamento": "01-01-00-00-00-000     DIRECCION",
      "codigo": "123",
      "cargo": "ANALISTA",
      "sueldo": 1000.0,
      "descripcionStatus": "Activo",
      "tipoNomina": "Empleado"
    }
  ],
  "isValid": true,
  "message": "Success",
  "cantidadRegistros": 1
}
```

## Campos

- `cedula`: cedula de la persona. Para vacantes retorna vacio/null desde BD y el backend lo serializa como cadena vacia.
- `nombre`: apellido y nombre de la persona; para vacantes retorna `VACANTE`.
- `fechaIngreso`: fecha de ingreso; para vacantes retorna `null`.
- `departamento`: estructura presupuestaria y denominacion.
- `codigo`: codigo del cargo.
- `cargo`: denominacion del cargo.
- `sueldo`: sueldo asignado al cargo.
- `descripcionStatus`: descripcion del estatus de personal.
- `tipoNomina`: tipo o siglas de nomina.

## Notas

- El codigo de empresa no se envia desde el frontend; el backend lo toma de `settings:EmpresaConfig` en `appsettings`.
- El endpoint no pagina; `cantidadRegistros` contiene el total devuelto por el stored procedure.
- Los datos se ordenan por `cargo`, siguiendo el `ORDER BY 6` del query original.
