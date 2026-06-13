using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

internal static class CntCierreContableMapper
{
    public static CntCierrePeriodoResponse MapPeriodo(IDataReader reader) =>
        new(
            reader.SafeGetInt32("CODIGO_PERIODO"),
            reader.SafeGetString("NOMBRE_PERIODO"),
            CntDb.SafeGetDate(reader, "FECHA_DESDE"),
            CntDb.SafeGetDate(reader, "FECHA_HASTA"),
            reader.SafeGetInt32("ANO_PERIODO"),
            reader.SafeGetInt32("NUMERO_PERIODO"),
            CntDb.SafeGetNullableDate(reader, "FECHA_PRECIERRE"),
            CntDb.SafeGetNullableInt(reader, "USUARIO_PRECIERRE"),
            CntDb.SafeGetNullableDate(reader, "FECHA_CIERRE"),
            CntDb.SafeGetNullableInt(reader, "USUARIO_CIERRE"),
            reader.SafeGetString("ESTADO"),
            reader.SafeGetInt32("CANT_TMP_SALDOS"),
            reader.SafeGetInt32("CANT_TMP_ANALITICO"),
            reader.SafeGetInt32("CANT_SALDOS"),
            reader.SafeGetInt32("CANT_HIST_ANALITICO"),
            reader.SafeGetInt32("CANT_MODIFICACIONES"),
            CntDb.SafeGetNullableInt(reader, "CODIGO_EMPRESA"));
}
