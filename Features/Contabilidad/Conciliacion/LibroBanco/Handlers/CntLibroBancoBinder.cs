using Oracle.ManagedDataAccess.Client;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

internal static class CntLibroBancoBinder
{
    public static void AddGetParameters(OracleCommand cmd, CntLibroBancoGetQuery value, int empresa)
    {
        cmd.Parameters.Add("p_CODIGO_BANCO", OracleDbType.Int32).Value = CntDb.DbValue(value.CodigoBanco);
        cmd.Parameters.Add("p_CODIGO_CUENTA_BANCO", OracleDbType.Int32).Value = CntDb.DbValue(value.CodigoCuentaBanco);
        cmd.Parameters.Add("p_FECHA_DESDE", OracleDbType.Date).Value = CntDb.DbValue(value.FechaDesde);
        cmd.Parameters.Add("p_FECHA_HASTA", OracleDbType.Date).Value = CntDb.DbValue(value.FechaHasta);
        cmd.Parameters.Add("p_STATUS", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.Status);
        cmd.Parameters.Add("p_SEARCH_TEXT", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.SearchText);
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
    }

    public static void AddDetailParameters(OracleCommand cmd, CntLibroBancoDetalleGetQuery value, int empresa)
    {
        cmd.Parameters.Add("p_CODIGO_LIBRO", OracleDbType.Int32).Value = value.CodigoLibro;
        cmd.Parameters.Add("p_STATUS", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.Status);
        cmd.Parameters.Add("p_SEARCH_TEXT", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.SearchText);
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
    }

    public static void AddGenerateParameters(OracleCommand cmd, CntLibroBancoGenerateCommand value, int empresa)
    {
        cmd.Parameters.Add("p_CODIGO_CUENTA_BANCO", OracleDbType.Int32).Value = value.CodigoCuentaBanco;
        cmd.Parameters.Add("p_FECHA_DESDE", OracleDbType.Date).Value = value.FechaDesde;
        cmd.Parameters.Add("p_FECHA_HASTA", OracleDbType.Date).Value = value.FechaHasta;
        cmd.Parameters.Add("p_USUARIO_ID", OracleDbType.Int32).Value = value.UsuarioId;
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
    }
}
