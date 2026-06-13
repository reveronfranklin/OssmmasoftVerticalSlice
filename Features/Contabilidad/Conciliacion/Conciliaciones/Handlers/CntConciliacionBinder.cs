using Oracle.ManagedDataAccess.Client;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

internal static class CntConciliacionBinder
{
    public static void AddGetParameters(OracleCommand cmd, CntConciliacionGetAllQuery value, int empresa)
    {
        cmd.Parameters.Add("p_CODIGO_PERIODO", OracleDbType.Int32).Value = CntDb.DbValue(value.CodigoPeriodo);
        cmd.Parameters.Add("p_CODIGO_BANCO", OracleDbType.Int32).Value = CntDb.DbValue(value.CodigoBanco);
        cmd.Parameters.Add("p_CODIGO_CUENTA_BANCO", OracleDbType.Int32).Value = CntDb.DbValue(value.CodigoCuentaBanco);
        cmd.Parameters.Add("p_ESTADO", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.Estado);
        cmd.Parameters.Add("p_SEARCH_TEXT", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.SearchText);
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
    }

    public static void AddCreateParameters(OracleCommand cmd, CntConciliacionCreateCommand value, int empresa)
    {
        cmd.Parameters.Add("p_CODIGO_PERIODO", OracleDbType.Int32).Value = value.CodigoPeriodo;
        cmd.Parameters.Add("p_CODIGO_CUENTA_BANCO", OracleDbType.Int32).Value = value.CodigoCuentaBanco;
        cmd.Parameters.Add("p_USUARIO_ID", OracleDbType.Int32).Value = value.UsuarioId;
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
    }

    public static void AddPrecloseParameters(OracleCommand cmd, CntConciliacionPrecloseCommand value, int empresa)
    {
        cmd.Parameters.Add("p_CODIGO_CONCILIACION", OracleDbType.Int32).Value = value.CodigoConciliacion;
        cmd.Parameters.Add("p_USUARIO_ID", OracleDbType.Int32).Value = value.UsuarioId;
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
    }

    public static void AddCloseParameters(OracleCommand cmd, CntConciliacionCloseCommand value, int empresa)
    {
        cmd.Parameters.Add("p_CODIGO_CONCILIACION", OracleDbType.Int32).Value = value.CodigoConciliacion;
        cmd.Parameters.Add("p_USUARIO_ID", OracleDbType.Int32).Value = value.UsuarioId;
        cmd.Parameters.Add("p_FORZAR_DIFERENCIA", OracleDbType.Int32).Value = value.ForzarDiferencia ? 1 : 0;
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
    }

    public static void AddReverseParameters(OracleCommand cmd, CntConciliacionReverseCommand value, int empresa)
    {
        cmd.Parameters.Add("p_CODIGO_CONCILIACION", OracleDbType.Int32).Value = value.CodigoConciliacion;
        cmd.Parameters.Add("p_USUARIO_ID", OracleDbType.Int32).Value = value.UsuarioId;
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
    }
}
