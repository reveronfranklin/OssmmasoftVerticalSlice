using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

internal static class CntBancoMapper
{
    public static CntBancoResponse Map(IDataReader reader) =>
        new(
            reader.SafeGetInt32("CODIGO_BANCO"),
            reader.SafeGetString("NOMBRE"),
            reader.SafeGetString("CODIGO_INTERBANCARIO"),
            CntDb.SafeGetNullableInt(reader, "CODIGO_EMPRESA"));
}
