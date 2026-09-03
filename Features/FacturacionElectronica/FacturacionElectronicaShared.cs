namespace OssmmasoftVerticalSlice.Features.FacturacionElectronica;

// Piezas comunes del modulo de Facturacion Electronica (requerimiento 32).
//
// Unico modulo del proyecto sobre PostgreSQL: usa GetFedConnection y el schema
// FED. El resto del backend trabaja contra Oracle con stored procedures.
public static class FacturacionElectronicaDb
{
    // D-13: el SQL del modulo vive aqui, parametrizado y nunca interpolado. Los
    // archivos de Sql/ son solo DDL. Es la traduccion a PostgreSQL del
    // antipatron "SQL inline en el handler" que marca el estandar del proyecto.
    public const string SqlHealth =
        "SELECT current_database() || ' / ' || current_user || ' / schema ' || current_schema();";

    // Mensaje de exito del proyecto. El typo es contrato con el frontend y no se
    // corrige: lo declara el estandar y lo consumen los modulos existentes.
    public const string MensajeExito = "suscces";
}
