using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

internal static class CntAuxiliarPucMapper
{
    public static CntAuxiliarPucResponse Map(IDataReader reader) =>
        new(
            reader.SafeGetInt32("CODIGO_AUXILIAR_PUC"),
            reader.SafeGetInt32("CODIGO_AUXILIAR"),
            reader.SafeGetString("AUXILIAR"),
            reader.SafeGetInt32("CODIGO_MAYOR"),
            reader.SafeGetString("MAYOR"),
            reader.SafeGetInt32("CODIGO_PUC"),
            reader.SafeGetString("TIPO_DOCUMENTO_ID"),
            CntDb.SafeGetNullableInt(reader, "CODIGO_EMPRESA"));
}
