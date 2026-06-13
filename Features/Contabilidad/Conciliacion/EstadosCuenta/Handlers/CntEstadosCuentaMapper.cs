using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

internal static class CntEstadosCuentaMapper
{
    public static CntEstadoCuentaResponse Map(IDataReader reader) =>
        new(
            reader.SafeGetInt32("CODIGO_ESTADO_CUENTA"),
            reader.SafeGetInt32("CODIGO_CUENTA_BANCO"),
            reader.SafeGetString("NO_CUENTA"),
            reader.SafeGetInt32("CODIGO_BANCO"),
            reader.SafeGetString("BANCO"),
            reader.SafeGetString("NUMERO_ESTADO_CUENTA"),
            CntDb.SafeGetDate(reader, "FECHA_DESDE"),
            CntDb.SafeGetDate(reader, "FECHA_HASTA"),
            reader.SafeGetDecimal("SALDO_INICIAL"),
            reader.SafeGetDecimal("SALDO_FINAL"),
            reader.SafeGetInt32("CANTIDAD_MOVIMIENTOS"),
            reader.SafeGetDecimal("MONTO_MOVIMIENTOS"),
            CntDb.SafeGetNullableInt(reader, "CODIGO_EMPRESA"));

    public static CntEstadoCuentaDetalleResponse MapDetalle(IDataReader reader) =>
        new(
            reader.SafeGetInt32("CODIGO_DETALLE_EDO_CTA"),
            reader.SafeGetInt32("CODIGO_ESTADO_CUENTA"),
            CntDb.SafeGetNullableInt(reader, "TIPO_TRANSACCION_ID"),
            reader.SafeGetString("TIPO_TRANSACCION"),
            reader.SafeGetString("NUMERO_TRANSACCION"),
            CntDb.SafeGetDate(reader, "FECHA_TRANSACCION"),
            reader.SafeGetString("DESCRIPCION"),
            reader.SafeGetDecimal("MONTO"),
            reader.SafeGetString("STATUS"),
            CntDb.SafeGetNullableInt(reader, "CODIGO_EMPRESA"));
}
