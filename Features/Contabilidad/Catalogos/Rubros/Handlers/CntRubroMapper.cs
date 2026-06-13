using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

internal static class CntRubroMapper
{
    public static CntRubroResponse Map(IDataReader reader) =>
        new(
            reader.SafeGetInt32("CODIGO_RUBRO"),
            reader.SafeGetString("NUMERO_RUBRO"),
            reader.SafeGetString("DENOMINACION"),
            reader.SafeGetString("DESCRIPCION"),
            reader.SafeGetString("EXTRA1"),
            reader.SafeGetString("EXTRA2"),
            reader.SafeGetString("EXTRA3"),
            CntDb.SafeGetNullableInt(reader, "CODIGO_EMPRESA"));
}
