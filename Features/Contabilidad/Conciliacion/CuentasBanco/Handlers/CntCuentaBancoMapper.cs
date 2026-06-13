using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

internal static class CntCuentaBancoMapper
{
    public static CntCuentaBancoResponse Map(IDataReader reader) =>
        new(
            reader.SafeGetInt32("CODIGO_CUENTA_BANCO"),
            reader.SafeGetInt32("CODIGO_BANCO"),
            reader.SafeGetString("BANCO"),
            CntDb.SafeGetNullableInt(reader, "TIPO_CUENTA_ID"),
            reader.SafeGetString("NO_CUENTA"),
            reader.SafeGetString("FORMATO_MASCARA"),
            CntDb.SafeGetNullableInt(reader, "DENOMINACION_FUNCIONAL_ID"),
            reader.SafeGetString("DENOMINACION_FUNCIONAL"),
            reader.SafeGetString("CODIGO"),
            reader.SafeGetInt32("PRINCIPAL") == 1,
            reader.SafeGetInt32("RECAUDADORA") == 1,
            CntDb.SafeGetNullableInt(reader, "CODIGO_MAYOR"),
            reader.SafeGetString("MAYOR"),
            CntDb.SafeGetNullableInt(reader, "CODIGO_AUXILIAR"),
            reader.SafeGetString("AUXILIAR"),
            reader.SafeGetString("SEARCH_TEXT"),
            CntDb.SafeGetNullableInt(reader, "CODIGO_EMPRESA"));
}
