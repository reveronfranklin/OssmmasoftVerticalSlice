using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

internal static class CntAuxiliarAdminMapper
{
    public static CntAuxiliarAdminResponse Map(IDataReader reader)
    {
        var fechaFin = CntDb.SafeGetNullableDate(reader, "FECHA_FIN_VIGENCIA");

        return new(
            reader.SafeGetInt32("CODIGO_AUXILIAR"),
            reader.SafeGetInt32("CODIGO_MAYOR"),
            reader.SafeGetString("NUMERO_MAYOR"),
            reader.SafeGetString("MAYOR"),
            reader.SafeGetString("SEGMENTO1"),
            reader.SafeGetString("SEGMENTO2"),
            reader.SafeGetString("SEGMENTO3"),
            reader.SafeGetString("SEGMENTO4"),
            reader.SafeGetString("SEGMENTO5"),
            reader.SafeGetString("SEGMENTO6"),
            reader.SafeGetString("SEGMENTO7"),
            reader.SafeGetString("SEGMENTO8"),
            reader.SafeGetString("SEGMENTO9"),
            reader.SafeGetString("SEGMENTO10"),
            reader.SafeGetString("DENOMINACION"),
            reader.SafeGetString("DESCRIPCION"),
            reader.SafeGetString("EXTRA1"),
            reader.SafeGetString("EXTRA2"),
            reader.SafeGetString("EXTRA3"),
            CntDb.SafeGetNullableInt(reader, "CODIGO_EMPRESA"),
            fechaFin,
            CntDb.SafeGetNullableInt(reader, "CODIGO_PROVEEDOR"),
            !fechaFin.HasValue || fechaFin.Value.Date >= DateTime.Today);
    }
}
