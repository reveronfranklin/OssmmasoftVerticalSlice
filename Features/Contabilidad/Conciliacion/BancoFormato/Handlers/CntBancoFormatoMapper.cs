using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

internal static class CntBancoFormatoMapper
{
    public static CntBancoFormatoResponse Map(IDataReader reader) =>
        new(
            reader.SafeGetInt32("CODIGO_FORMATO"),
            reader.SafeGetInt32("CODIGO_BANCO"),
            reader.SafeGetString("BANCO"),
            CntDb.SafeGetNullableInt(reader, "CODIGO_CUENTA_BANCO"),
            reader.SafeGetString("CUENTA"),
            reader.SafeGetString("NOMBRE_FORMATO"),
            reader.SafeGetString("TIPO_FORMATO"),
            reader.SafeGetString("DELIMITADOR"),
            reader.SafeGetInt32("TIENE_ENCABEZADO") == 1,
            reader.SafeGetInt32("FILA_INICIO"),
            reader.SafeGetString("HOJA_EXCEL"),
            reader["MAPEO_JSON"]?.ToString() ?? string.Empty,
            reader["REGLAS_JSON"]?.ToString() ?? string.Empty,
            reader.SafeGetInt32("ACTIVO") == 1,
            reader.SafeGetInt32("CODIGO_EMPRESA"));
}

