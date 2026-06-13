using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

internal static class CntSaldoMapper
{
    public static CntSaldoResponse Map(IDataReader reader) =>
        new(
            reader.SafeGetInt32("CODIGO_SALDO"),
            reader.SafeGetInt32("CODIGO_PERIODO"),
            reader.SafeGetString("NOMBRE_PERIODO"),
            reader.SafeGetInt32("CODIGO_MAYOR"),
            reader.SafeGetString("MAYOR"),
            reader.SafeGetInt32("CODIGO_AUXILIAR"),
            reader.SafeGetString("AUXILIAR"),
            reader.SafeGetDecimal("DEBITOS"),
            reader.SafeGetDecimal("CREDITOS"),
            reader.SafeGetDecimal("MONTO"),
            reader.SafeGetString("EXTRA1"),
            reader.SafeGetString("EXTRA2"),
            reader.SafeGetString("EXTRA3"),
            CntDb.SafeGetNullableInt(reader, "CODIGO_EMPRESA"));
}
