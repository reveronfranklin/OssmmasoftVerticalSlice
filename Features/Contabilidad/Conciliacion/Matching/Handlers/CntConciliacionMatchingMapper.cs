using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

internal static class CntConciliacionMatchingMapper
{
    public static CntConciliacionSuggestionResponse MapSuggestion(IDataReader reader) =>
        new(
            reader.SafeGetInt32("CODIGO_DETALLE_EDO_CTA"),
            reader.SafeGetInt32("CODIGO_DETALLE_LIBRO"),
            CntDb.SafeGetDate(reader, "BANCO_FECHA"),
            CntDb.SafeGetDate(reader, "FECHA_LIBRO"),
            reader.SafeGetString("NUMERO_TRANSACCION"),
            reader.SafeGetString("NUMERO_DOCUMENTO"),
            reader.SafeGetString("BANCO_DESCRIPCION"),
            reader.SafeGetString("LIBRO_DESCRIPCION"),
            reader.SafeGetDecimal("BANCO_MONTO"),
            reader.SafeGetDecimal("LIBRO_MONTO"),
            reader.SafeGetDecimal("DIFERENCIA_MONTO"),
            reader.SafeGetInt32("DIFERENCIA_DIAS"),
            reader.SafeGetInt32("MATCH_MONTO") == 1,
            reader.SafeGetInt32("MATCH_NUMERO") == 1,
            reader.SafeGetInt32("MATCH_FECHA") == 1,
            reader.SafeGetInt32("SCORE"),
            reader.SafeGetString("MOTIVOS"),
            CntDb.SafeGetNullableInt(reader, "CODIGO_EMPRESA"));
}
