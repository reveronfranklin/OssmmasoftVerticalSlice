using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.FacturacionElectronica;

// El documento que una nota corrige, leido dentro de la transaccion de la
// emision. Trae lo que el Art. 23 obliga a referenciar -fecha, numero y monto- y
// lo que hace falta para validar: de quien es, de que tipo, en que moneda y
// cuanto saldo le queda.
public record NotaOrigenDatos(
    long Id,
    long EmisorId,
    string TipoDocumento,
    string Serie,
    string Numeracion,
    DateTime EmitidoEn,
    decimal TotalGeneral,
    string Moneda,
    decimal Saldo,
    string Estado);

// SQL del subdominio de notas de debito y credito. Fase 5.
public static class NotaDb
{
    // El documento original con su saldo, leido de la vista de estado.
    //
    // Sale de FED_V_DOCUMENTO_ESTADO y no de FED_DOCUMENTO porque hacen falta las
    // dos cosas: los datos del documento para la referencia del Art. 23 y el
    // saldo para la validacion de D-34. La vista ya las junta y no puede diferir
    // de sus origenes.
    public const string SqlOrigenParaNota = @"
        SELECT
            e.DOCUMENTO_ID, e.EMISOR_ID, e.TIPO_DOCUMENTO, e.NUMERACION,
            e.MONEDA, e.TOTAL_GENERAL, e.SALDO, e.ESTADO,
            d.SERIE, d.EMITIDO_EN
        FROM FED.FED_V_DOCUMENTO_ESTADO e
        JOIN FED.FED_DOCUMENTO d ON d.ID = e.DOCUMENTO_ID
        WHERE e.DOCUMENTO_ID = @documento_id;";

    // El vinculo y la instantanea del Art. 23. Va en la MISMA transaccion que el
    // INSERT del documento: una nota sin vinculo no debe poder existir ni un
    // instante, y el trigger diferido de 13_fed_nota.sql lo hace imposible.
    public const string SqlNotaInsert = @"
        INSERT INTO FED.FED_NOTA
            (DOCUMENTO_ID, DOCUMENTO_ORIGEN_ID, MOTIVO, ES_ANULACION,
             ORIGEN_NUMERACION, ORIGEN_FECHA_8D, ORIGEN_TOTAL, ORIGEN_MONEDA,
             USUARIO_INS)
        VALUES
            (@documento_id, @documento_origen_id, @motivo, @es_anulacion,
             @origen_numeracion, @origen_fecha_8d, @origen_total, @origen_moneda,
             @usuario_ins);";

    // Serializacion de las notas contra un mismo original (D-33).
    //
    // NO se usa SELECT ... FOR UPDATE, y no es una preferencia: sobre
    // FED_DOCUMENTO falla con "permission denied" para el rol de aplicacion.
    // PostgreSQL exige privilegio de escritura para tomar un bloqueo de fila, y
    // D-20 se lo quito a proposito para sostener el append-only del Art. 18.2.
    //
    // El advisory lock no exige privilegio de tabla, se libera solo al terminar
    // la transaccion -commit o rollback, sin codigo- y serializa exactamente lo
    // que hay que serializar: las notas sobre ESE documento. Apoyarse en el
    // bloqueo del contador del numero de control habria funcionado, pero ensancha
    // la ventana de serializacion de todas las emisiones del emisor, incluidas
    // las facturas, para resolver un problema de notas.
    //
    // El primer argumento es el namespace del modulo, para no colisionar con
    // ningun otro uso de advisory locks en el ERP. Una colision por truncamiento
    // solo produce espera adicional, nunca un resultado incorrecto.
    public const int AdvisoryNamespace = 32;

    public const string SqlBloquearOrigen = @"
        SELECT pg_advisory_xact_lock(@ns, @documento_id);";

    public static NotaOrigenDatos MapOrigen(IDataReader reader) => new(
        reader.SafeGetInt64("documento_id"),
        reader.SafeGetInt64("emisor_id"),
        reader.SafeGetString("tipo_documento"),
        reader.SafeGetString("serie"),
        reader.SafeGetString("numeracion"),
        reader.GetDateTime(reader.GetOrdinal("emitido_en")),
        reader.SafeGetDecimal("total_general"),
        reader.SafeGetString("moneda"),
        reader.SafeGetDecimal("saldo"),
        reader.SafeGetString("estado"));
}
