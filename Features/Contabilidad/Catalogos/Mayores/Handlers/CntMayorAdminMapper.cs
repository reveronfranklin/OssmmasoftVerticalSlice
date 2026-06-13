using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

internal static class CntMayorAdminMapper
{
    public static CntMayorAdminResponse Map(IDataReader reader) =>
        new(
            reader.SafeGetInt32("CODIGO_MAYOR"),
            reader.SafeGetString("NUMERO_MAYOR"),
            reader.SafeGetString("DENOMINACION"),
            reader.SafeGetString("DESCRIPCION"),
            CntDb.SafeGetNullableInt(reader, "CODIGO_BALANCE"),
            reader.SafeGetString("NUMERO_BALANCE"),
            reader.SafeGetString("BALANCE"),
            CntDb.SafeGetNullableInt(reader, "CODIGO_RUBRO"),
            reader.SafeGetString("NUMERO_RUBRO"),
            reader.SafeGetString("RUBRO"),
            reader.SafeGetString("COLUMNA_BALANCE"),
            reader.SafeGetString("EXTRA1"),
            reader.SafeGetString("EXTRA2"),
            reader.SafeGetString("EXTRA3"),
            CntDb.SafeGetNullableInt(reader, "CODIGO_EMPRESA"));
}
