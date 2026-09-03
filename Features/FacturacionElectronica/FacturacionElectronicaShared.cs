using System.Data;
using Npgsql;

namespace OssmmasoftVerticalSlice.Features.FacturacionElectronica;

// Respuesta de un emisor. Va aqui y no en el archivo de operacion porque la
// comparten GetAll, GetById y las que vengan despues.
public record EmisorResponse(
    long Id,
    string Rif,
    string RazonSocial,
    string DomicilioFiscal,
    string Correo,
    string Estado,
    string RifVerificadoEl,
    string RifVerificadoEstado,
    string UsuarioIns,
    string FechaIns,
    string UsuarioUpd,
    string FechaUpd);

// Piezas comunes del modulo de Facturacion Electronica (requerimiento 32).
//
// Unico modulo del proyecto sobre PostgreSQL: usa GetFedConnection y el schema
// FED. El resto del backend trabaja contra Oracle con stored procedures.
public static class FacturacionElectronicaDb
{
    // Mensaje de exito del proyecto. El typo es contrato con el frontend y no se
    // corrige: lo declara el estandar y lo consumen los modulos existentes.
    public const string MensajeExito = "suscces";

    // -----------------------------------------------------------------------
    // SQL del modulo. D-13: vive aqui, parametrizado y nunca interpolado. Los
    // archivos de Sql/ son solo DDL. Es la traduccion a PostgreSQL del
    // antipatron "SQL inline en el handler" que marca el estandar.
    //
    // Se usa LIMIT/OFFSET y no ROW_NUMBER(). El estandar prohibe OFFSET porque
    // Oracle 10g no lo soporta; en PostgreSQL es lo idiomatico y trasladar el
    // rodeo de Oracle seria copiar la solucion sin el problema.
    // -----------------------------------------------------------------------

    public const string SqlHealth =
        "SELECT current_database() || ' / ' || current_user || ' / schema ' || current_schema();";

    private const string ColumnasEmisor = @"
        ID, RIF, RAZON_SOCIAL, DOMICILIO_FISCAL, CORREO, ESTADO,
        RIF_VERIFICADO_EL, RIF_VERIFICADO_ESTADO,
        USUARIO_INS, FECHA_INS, USUARIO_UPD, FECHA_UPD";

    public const string SqlEmisorCreate = @"
        INSERT INTO FED.FED_EMISOR
            (RIF, RAZON_SOCIAL, DOMICILIO_FISCAL, CORREO, ESTADO, USUARIO_INS)
        VALUES
            (@rif, @razon_social, @domicilio_fiscal, @correo, @estado, @usuario_ins)
        RETURNING ID;";

    public static readonly string SqlEmisorGetAll = $@"
        SELECT {ColumnasEmisor}, COUNT(*) OVER() AS TOTAL_REGISTROS
        FROM FED.FED_EMISOR
        WHERE (@search = '' OR RIF ILIKE @like OR RAZON_SOCIAL ILIKE @like)
        ORDER BY RAZON_SOCIAL
        LIMIT @page_size OFFSET @row_offset;";

    public static readonly string SqlEmisorGetById = $@"
        SELECT {ColumnasEmisor}
        FROM FED.FED_EMISOR
        WHERE ID = @id;";

    // El RIF no se actualiza a proposito. El Articulo 30 ata la secuencia de
    // numero de control al RIF del emisor, asi que cambiarlo rompe la unicidad
    // por emisor de todo lo ya asignado. Si el negocio necesita corregirlo, se
    // desactiva el emisor y se da de alta el correcto.
    public const string SqlEmisorUpdate = @"
        UPDATE FED.FED_EMISOR SET
            RAZON_SOCIAL          = @razon_social,
            DOMICILIO_FISCAL      = @domicilio_fiscal,
            CORREO                = @correo,
            ESTADO                = @estado,
            RIF_VERIFICADO_EL     = @rif_verificado_el,
            RIF_VERIFICADO_ESTADO = @rif_verificado_estado,
            USUARIO_UPD           = @usuario_upd,
            FECHA_UPD             = now()
        WHERE ID = @id;";

    // -----------------------------------------------------------------------
    // Mapeo y normalizacion
    // -----------------------------------------------------------------------

    // Las extensiones SafeGet* de helper/ sirven aqui: son sobre IDataReader y
    // NpgsqlDataReader lo implementa. Falta una para fechas, y como solo la usa
    // esta feature vive aqui y no en helper/, como manda el estandar.
    private static string SafeGetFecha(IDataReader reader, string columna, string formato)
    {
        int ordinal = reader.GetOrdinal(columna);

        return reader.IsDBNull(ordinal)
            ? string.Empty
            : Convert.ToDateTime(reader.GetValue(ordinal)).ToString(formato);
    }

    public static EmisorResponse MapEmisor(IDataReader reader) => new(
        reader.SafeGetInt64("id"),
        reader.SafeGetString("rif"),
        reader.SafeGetString("razon_social"),
        reader.SafeGetString("domicilio_fiscal"),
        reader.SafeGetString("correo"),
        reader.SafeGetString("estado"),
        SafeGetFecha(reader, "rif_verificado_el", "dd/MM/yyyy"),
        reader.SafeGetString("rif_verificado_estado"),
        reader.SafeGetString("usuario_ins"),
        SafeGetFecha(reader, "fecha_ins", "dd/MM/yyyy HH:mm"),
        reader.SafeGetString("usuario_upd"),
        SafeGetFecha(reader, "fecha_upd", "dd/MM/yyyy HH:mm"));

    // Nulos via helper y no con "?? DBNull.Value" inline, como pide el estandar.
    public static object DbValue(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? DBNull.Value : valor.Trim();

    public static object DbValueFecha(DateTime? valor) =>
        valor.HasValue ? valor.Value.Date : (object)DBNull.Value;

    // Codigo SQLSTATE de violacion de unicidad en PostgreSQL. La defensa contra
    // el RIF duplicado es el UNIQUE de la tabla, no un SELECT previo: entre la
    // consulta y el insert cabe otra peticion.
    public const string SqlStateUnico = "23505";

    public static bool EsRifDuplicado(NpgsqlException ex) =>
        ex is PostgresException pg && pg.SqlState == SqlStateUnico;
}
