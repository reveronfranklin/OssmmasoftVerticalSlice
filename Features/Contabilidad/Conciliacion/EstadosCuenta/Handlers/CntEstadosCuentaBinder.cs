using Oracle.ManagedDataAccess.Client;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

internal static class CntEstadosCuentaBinder
{
    public static void AddGetParameters(OracleCommand cmd, CntEstadosCuentaGetQuery value, int empresa)
    {
        cmd.Parameters.Add("p_CODIGO_BANCO", OracleDbType.Int32).Value = CntDb.DbValue(value.CodigoBanco);
        cmd.Parameters.Add("p_CODIGO_CUENTA_BANCO", OracleDbType.Int32).Value = CntDb.DbValue(value.CodigoCuentaBanco);
        cmd.Parameters.Add("p_FECHA_DESDE", OracleDbType.Date).Value = CntDb.DbValue(value.FechaDesde);
        cmd.Parameters.Add("p_FECHA_HASTA", OracleDbType.Date).Value = CntDb.DbValue(value.FechaHasta);
        cmd.Parameters.Add("p_SEARCH_TEXT", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.SearchText);
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
    }

    public static void AddDetailParameters(OracleCommand cmd, CntEstadoCuentaDetalleGetQuery value, int empresa)
    {
        cmd.Parameters.Add("p_CODIGO_ESTADO_CUENTA", OracleDbType.Int32).Value = value.CodigoEstadoCuenta;
        cmd.Parameters.Add("p_STATUS", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.Status);
        cmd.Parameters.Add("p_SEARCH_TEXT", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.SearchText);
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
    }
}
