using System.Data;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

internal static class CntBancoArchivoMapper
{
    public static CntBancoArchivoResponse Map(IDataReader reader) =>
        new(
            reader.SafeGetInt32("CODIGO_BANCO_ARCHIVO_CONTROL"),
            reader.SafeGetInt32("CODIGO_BANCO"),
            reader.SafeGetString("BANCO"),
            reader.SafeGetInt32("CODIGO_CUENTA_BANCO"),
            reader.SafeGetString("NO_CUENTA"),
            reader.SafeGetString("NOMBRE_ARCHIVO"),
            CntDb.SafeGetDate(reader, "FECHA_DESDE"),
            CntDb.SafeGetDate(reader, "FECHA_HASTA"),
            reader.SafeGetDecimal("SALDO_INICIAL"),
            reader.SafeGetDecimal("SALDO_FINAL"),
            CntDb.SafeGetNullableInt(reader, "CODIGO_ESTADO_CUENTA"),
            reader.SafeGetInt32("CONFIRMADO") == 1,
            reader.SafeGetInt32("CANTIDAD_MOVIMIENTOS"),
            reader.SafeGetDecimal("MONTO_MOVIMIENTOS"),
            CntDb.SafeGetNullableInt(reader, "CODIGO_EMPRESA"));
}
