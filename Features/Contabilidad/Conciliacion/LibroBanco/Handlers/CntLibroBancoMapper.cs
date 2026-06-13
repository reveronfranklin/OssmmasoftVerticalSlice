using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

internal static class CntLibroBancoMapper
{
    public static CntLibroBancoResponse Map(IDataReader reader) =>
        new(
            reader.SafeGetInt32("CODIGO_LIBRO"),
            reader.SafeGetInt32("CODIGO_CUENTA_BANCO"),
            reader.SafeGetString("NO_CUENTA"),
            reader.SafeGetInt32("CODIGO_BANCO"),
            reader.SafeGetString("BANCO"),
            CntDb.SafeGetDate(reader, "FECHA_LIBRO"),
            reader.SafeGetString("STATUS"),
            reader.SafeGetInt32("CANTIDAD_MOVIMIENTOS"),
            reader.SafeGetDecimal("MONTO_MOVIMIENTOS"),
            CntDb.SafeGetNullableInt(reader, "CODIGO_EMPRESA"));

    public static CntLibroBancoDetalleResponse MapDetalle(IDataReader reader) =>
        new(
            reader.SafeGetInt32("CODIGO_DETALLE_LIBRO"),
            reader.SafeGetInt32("CODIGO_LIBRO"),
            reader.SafeGetInt32("TIPO_DOCUMENTO_ID"),
            reader.SafeGetString("TIPO_DOCUMENTO"),
            CntDb.SafeGetNullableInt(reader, "CODIGO_CHEQUE"),
            CntDb.SafeGetNullableInt(reader, "CODIGO_IDENTIFICADOR"),
            CntDb.SafeGetNullableInt(reader, "ORIGEN_ID"),
            reader.SafeGetString("NUMERO_DOCUMENTO"),
            reader.SafeGetString("DESCRIPCION"),
            reader.SafeGetDecimal("MONTO"),
            reader.SafeGetString("STATUS"),
            CntDb.SafeGetNullableInt(reader, "CODIGO_EMPRESA"));
}
