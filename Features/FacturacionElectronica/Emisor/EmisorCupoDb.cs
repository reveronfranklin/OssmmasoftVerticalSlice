using Npgsql;

namespace OssmmasoftVerticalSlice.Features.FacturacionElectronica;

// Cupo de documentos por emisor (TM.4, D-54). Ver Sql/24_fed_emisor_cupo.sql
// para el modelo y el porque de CONSUMIDO_AL_CARGAR.
//
// Va en la subcarpeta y no en FacturacionElectronicaShared.cs por el mismo
// criterio que Factura/FacturaDb.cs: el SQL de un subdominio vive con su
// subdominio.

public record EmisorCupoCargaResponse(
    long Id,
    int Cantidad,
    string UsuarioIns,
    string FechaIns);

// CupoTotal y CupoDisponible en null = el emisor no tiene cupo: sin limite.
public record EmisorCupoResponse(
    long EmisorId,
    long? CupoTotal,
    long? CupoDisponible,
    List<EmisorCupoCargaResponse> Cargas);

public static class EmisorCupoDb
{
    // El tope de una carga, escrito para mensajes. Fijo y no con {valor:N0}: N0
    // toma la cultura del servidor, que hoy es en-US y escribe "99,999,999".
    public const string CantidadMaximaTexto = "99.999.999";

    // Posicion del ultimo numero asignado en la secuencia del emisor. Es la
    // misma cuenta en C# y en SQL; si una cambia, la otra tambien.
    public static long Consumido(string identificador, int secuencial) =>
        (long.Parse(identificador) * FacturacionElectronicaDb.SecuencialMaximo) + secuencial;

    // La misma cuenta, para usar en SQL sobre FED_EMISOR_CONTADOR con alias CT.
    public static readonly string SqlConsumido =
        $"(CAST(CT.IDENTIFICADOR AS BIGINT) * {FacturacionElectronicaDb.SecuencialMaximo} + CT.SECUENCIAL)";

    public const string SqlResumen = @"
        SELECT SUM(CANTIDAD) AS TOTAL, MIN(CONSUMIDO_AL_CARGAR) AS BASE
        FROM FED.FED_EMISOR_CUPO
        WHERE EMISOR_ID = @emisor_id;";

    public const string SqlInsertar = @"
        INSERT INTO FED.FED_EMISOR_CUPO (EMISOR_ID, CANTIDAD, CONSUMIDO_AL_CARGAR, USUARIO_INS)
        VALUES (@emisor_id, @cantidad, @consumido, @usuario_ins);";

    public const string SqlCargas = @"
        SELECT ID, CANTIDAD, USUARIO_INS, FECHA_INS
        FROM FED.FED_EMISOR_CUPO
        WHERE EMISOR_ID = @emisor_id
        ORDER BY ID DESC;";

    // TM.6. Las cargas en el orden en que se hicieron: es el orden en que se
    // consumen.
    public const string SqlCargasEnOrden = @"
        SELECT CANTIDAD, CONSUMIDO_AL_CARGAR
        FROM FED.FED_EMISOR_CUPO
        WHERE EMISOR_ID = @emisor_id
        ORDER BY ID;";

    // TM.6. Que lugar ocupa un numero de control dentro del cupo que lo cubrio:
    // "documento N de M". Las cargas se consumen en fila, una detras de otra, a
    // partir de la base del primer cupo:
    //
    //     carga 1 cubre (base,            base + C1]
    //     carga 2 cubre (base + C1,       base + C1 + C2]  ...
    //
    // Si se renueva antes de agotar, lo que quedaba de la carga anterior se usa
    // primero: esos documentos siguen siendo "16 de 20" y la carga nueva arranca
    // en 1. Devuelve null si el numero es anterior al primer cupo -no le toco
    // ninguno- o si el emisor no tiene cupo.
    //
    // N cuenta numeros de control, no documentos: una asignacion manual sin
    // documento (D-16) tambien ocupa un lugar, porque tambien descuenta.
    public static (long Numero, int Cantidad)? PosicionEnCupo(
        long consumido, IReadOnlyList<(int Cantidad, long ConsumidoAlCargar)> cargas)
    {
        if (cargas.Count == 0)
        {
            return null;
        }

        long inicio = cargas[0].ConsumidoAlCargar;

        foreach (var carga in cargas)
        {
            if (consumido <= inicio)
            {
                return null;
            }

            if (consumido <= inicio + carga.Cantidad)
            {
                return (consumido - inicio, carga.Cantidad);
            }

            inicio += carga.Cantidad;
        }

        return null;
    }

    public static long? Disponible(long? total, long? baseConsumo, long consumidoActual) =>
        total is null || baseConsumo is null ? null : total - (consumidoActual - baseConsumo);

    public static async Task<(long? Total, long? Base)> LeerResumenAsync(
        NpgsqlConnection cn, NpgsqlTransaction? tx, long emisorId)
    {
        using var cmd = new NpgsqlCommand(SqlResumen, cn, tx);
        cmd.Parameters.AddWithValue("emisor_id", emisorId);

        using var reader = await cmd.ExecuteReaderAsync();
        await reader.ReadAsync();

        return (LeerEnteroNulable(reader, "total"), LeerEnteroNulable(reader, "base"));
    }

    // Se llama DESPUES de bloquear la fila del contador (SqlContadorBloquear) y
    // con los valores que ese bloqueo devolvio. Por eso no hay carrera: dos
    // peticiones del mismo emisor pasan por aqui de a una, y la segunda ya ve el
    // numero que consumio la primera. Devuelve el motivo del rechazo, o null si
    // queda cupo o el emisor no tiene.
    public static async Task<string?> VerificarAsync(
        NpgsqlConnection cn, NpgsqlTransaction tx, long emisorId, string identificador, int secuencial)
    {
        var (total, baseConsumo) = await LeerResumenAsync(cn, tx, emisorId);
        long? disponible = Disponible(total, baseConsumo, Consumido(identificador, secuencial));

        if (disponible is null or > 0)
        {
            return null;
        }

        string usados = total == 1 ? "ya usó el único autorizado" : $"ya usó los {total} autorizados";

        return $"El emisor agotó su cupo de documentos: {usados}. "
               + "Debe renovarse el cupo para seguir asignando números de control.";
    }

    public static long? LeerEnteroNulable(System.Data.IDataReader reader, string columna)
    {
        int ordinal = reader.GetOrdinal(columna);

        return reader.IsDBNull(ordinal) ? null : Convert.ToInt64(reader.GetValue(ordinal));
    }
}
