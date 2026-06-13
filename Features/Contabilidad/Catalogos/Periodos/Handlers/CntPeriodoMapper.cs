using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

internal static class CntPeriodoMapper
{
    public static CntPeriodoAdminResponse Map(IDataReader reader) =>
        new(
            reader.SafeGetInt32("CODIGO_PERIODO"),
            reader.SafeGetString("NOMBRE_PERIODO"),
            CntDb.SafeGetDate(reader, "FECHA_DESDE"),
            CntDb.SafeGetDate(reader, "FECHA_HASTA"),
            reader.SafeGetInt32("ANO_PERIODO"),
            reader.SafeGetInt32("NUMERO_PERIODO"),
            CntDb.SafeGetNullableDate(reader, "FECHA_CIERRE"),
            reader.SafeGetInt32("CERRADO") == 1,
            reader.SafeGetString("EXTRA1"),
            reader.SafeGetString("EXTRA2"),
            reader.SafeGetString("EXTRA3"),
            CntDb.SafeGetNullableInt(reader, "CODIGO_EMPRESA"));
}
