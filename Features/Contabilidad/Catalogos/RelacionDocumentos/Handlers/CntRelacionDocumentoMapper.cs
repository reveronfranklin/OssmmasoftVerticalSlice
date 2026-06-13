using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

internal static class CntRelacionDocumentoMapper
{
    public static CntRelacionDocumentoResponse Map(IDataReader reader) =>
        new(
            reader.SafeGetInt32("CODIGO_RELACION_DOCUMENTO"),
            reader.SafeGetInt32("TIPO_DOCUMENTO_ID"),
            reader.SafeGetString("TIPO_DOCUMENTO_CODIGO"),
            reader.SafeGetString("TIPO_DOCUMENTO"),
            reader.SafeGetInt32("TIPO_DOCUMENTO_TITULO_ID"),
            reader.SafeGetInt32("TIPO_TRANSACCION_ID"),
            reader.SafeGetString("TIPO_TRANSACCION_CODIGO"),
            reader.SafeGetString("TIPO_TRANSACCION"),
            reader.SafeGetInt32("TIPO_TRANSACCION_TITULO_ID"),
            reader.SafeGetString("EXTRA1"),
            reader.SafeGetString("EXTRA2"),
            reader.SafeGetString("EXTRA3"),
            CntDb.SafeGetNullableInt(reader, "CODIGO_EMPRESA"));
}
