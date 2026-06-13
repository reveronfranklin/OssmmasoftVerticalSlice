using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

internal static class CntConciliacionDetalleMapper
{
    public static CntConciliacionBancoMovimientoResponse MapBanco(IDataReader reader) =>
        new(
            reader.SafeGetInt32("CODIGO_DETALLE_EDO_CTA"),
            reader.SafeGetInt32("CODIGO_ESTADO_CUENTA"),
            reader.SafeGetString("NUMERO_ESTADO_CUENTA"),
            CntDb.SafeGetNullableInt(reader, "TIPO_TRANSACCION_ID"),
            reader.SafeGetString("TIPO_TRANSACCION"),
            reader.SafeGetString("NUMERO_TRANSACCION"),
            CntDb.SafeGetDate(reader, "FECHA_TRANSACCION"),
            reader.SafeGetString("DESCRIPCION"),
            reader.SafeGetDecimal("MONTO"),
            reader.SafeGetString("STATUS"),
            CntDb.SafeGetNullableInt(reader, "CODIGO_TMP_CONCILIACION"),
            reader.SafeGetInt32("EN_TEMPORAL") == 1,
            CntDb.SafeGetNullableInt(reader, "CODIGO_EMPRESA"));

    public static CntConciliacionLibroMovimientoResponse MapLibro(IDataReader reader) =>
        new(
            reader.SafeGetInt32("CODIGO_DETALLE_LIBRO"),
            reader.SafeGetInt32("CODIGO_LIBRO"),
            CntDb.SafeGetDate(reader, "FECHA_LIBRO"),
            reader.SafeGetInt32("TIPO_DOCUMENTO_ID"),
            reader.SafeGetString("TIPO_DOCUMENTO"),
            CntDb.SafeGetNullableInt(reader, "CODIGO_CHEQUE"),
            CntDb.SafeGetNullableInt(reader, "CODIGO_IDENTIFICADOR"),
            CntDb.SafeGetNullableInt(reader, "ORIGEN_ID"),
            reader.SafeGetString("NUMERO_DOCUMENTO"),
            reader.SafeGetString("DESCRIPCION"),
            reader.SafeGetDecimal("MONTO"),
            reader.SafeGetString("STATUS"),
            CntDb.SafeGetNullableInt(reader, "CODIGO_TMP_CONCILIACION"),
            reader.SafeGetInt32("EN_TEMPORAL") == 1,
            CntDb.SafeGetNullableInt(reader, "CODIGO_EMPRESA"));

    public static CntConciliacionTemporalResponse MapTemporal(IDataReader reader) =>
        new(
            reader.SafeGetInt32("CODIGO_TMP_CONCILIACION"),
            reader.SafeGetInt32("CODIGO_CONCILIACION"),
            reader.SafeGetInt32("CODIGO_PERIODO"),
            reader.SafeGetInt32("CODIGO_CUENTA_BANCO"),
            CntDb.SafeGetNullableInt(reader, "CODIGO_DETALLE_LIBRO"),
            CntDb.SafeGetNullableInt(reader, "CODIGO_DETALLE_EDO_CTA"),
            CntDb.SafeGetDate(reader, "FECHA"),
            reader.SafeGetString("NUMERO"),
            reader.SafeGetDecimal("MONTO"),
            reader.SafeGetString("TIPO"),
            reader.SafeGetString("BANCO_DESCRIPCION"),
            reader.SafeGetString("LIBRO_DESCRIPCION"),
            CntDb.SafeGetNullableInt(reader, "CODIGO_EMPRESA"));
}
