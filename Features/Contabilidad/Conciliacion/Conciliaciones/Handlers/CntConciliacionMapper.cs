using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

internal static class CntConciliacionMapper
{
    public static CntConciliacionResponse Map(IDataReader reader) =>
        new(
            reader.SafeGetInt32("CODIGO_CONCILIACION"),
            reader.SafeGetInt32("CODIGO_PERIODO"),
            reader.SafeGetString("NOMBRE_PERIODO"),
            reader.SafeGetInt32("ANO_PERIODO"),
            reader.SafeGetInt32("NUMERO_PERIODO"),
            reader.SafeGetInt32("CODIGO_CUENTA_BANCO"),
            reader.SafeGetInt32("CODIGO_BANCO"),
            reader.SafeGetString("BANCO"),
            reader.SafeGetString("NO_CUENTA"),
            reader.SafeGetString("DENOMINACION_FUNCIONAL"),
            reader.SafeGetDecimal("SALDO_BANCO"),
            reader.SafeGetDecimal("SALDO_LIBRO"),
            CntDb.SafeGetNullableDate(reader, "FECHA_PRECIERRE"),
            CntDb.SafeGetNullableDate(reader, "FECHA_CIERRE"),
            reader.SafeGetString("ESTADO"),
            CntDb.SafeGetNullableInt(reader, "CODIGO_EMPRESA"));
}
