using Oracle.ManagedDataAccess.Client;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

internal static class CntSaldoBinder
{
    public static void AddSaveParameters(OracleCommand cmd, CntSaldoSaveCommand value, int empresa)
    {
        cmd.Parameters.Add("p_CODIGO_PERIODO", OracleDbType.Int32).Value = value.CodigoPeriodo;
        cmd.Parameters.Add("p_CODIGO_MAYOR", OracleDbType.Int32).Value = value.CodigoMayor;
        cmd.Parameters.Add("p_CODIGO_AUX", OracleDbType.Int32).Value = value.CodigoAuxiliar;
        cmd.Parameters.Add("p_DEBITOS", OracleDbType.Decimal).Value = value.Debitos;
        cmd.Parameters.Add("p_CREDITOS", OracleDbType.Decimal).Value = value.Creditos;
        cmd.Parameters.Add("p_EXTRA1", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.Extra1);
        cmd.Parameters.Add("p_EXTRA2", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.Extra2);
        cmd.Parameters.Add("p_EXTRA3", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.Extra3);
        cmd.Parameters.Add("p_USUARIO_ID", OracleDbType.Int32).Value = value.UsuarioId;
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
    }
}
