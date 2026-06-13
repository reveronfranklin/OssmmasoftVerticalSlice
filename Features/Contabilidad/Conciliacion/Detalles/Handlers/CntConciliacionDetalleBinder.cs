using Oracle.ManagedDataAccess.Client;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

internal static class CntConciliacionDetalleBinder
{
    public static void AddMovimientoParameters(OracleCommand cmd, CntConciliacionDetalleGetQuery value, int empresa)
    {
        cmd.Parameters.Add("p_CODIGO_CONCILIACION", OracleDbType.Int32).Value = value.CodigoConciliacion;
        cmd.Parameters.Add("p_SOLO_PENDIENTES", OracleDbType.Int32).Value = value.SoloPendientes ? 1 : 0;
        cmd.Parameters.Add("p_SEARCH_TEXT", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.SearchText);
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
    }

    public static void AddTemporalParameters(OracleCommand cmd, CntConciliacionTemporalGetQuery value, int empresa)
    {
        cmd.Parameters.Add("p_CODIGO_CONCILIACION", OracleDbType.Int32).Value = value.CodigoConciliacion;
        cmd.Parameters.Add("p_SEARCH_TEXT", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.SearchText);
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
    }
}
