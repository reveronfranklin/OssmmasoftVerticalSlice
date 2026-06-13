using Oracle.ManagedDataAccess.Client;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

internal static class CntPeriodoBinder
{
    public static void AddSaveParameters(OracleCommand cmd, CntPeriodoSaveCommand value, int empresa)
    {
        cmd.Parameters.Add("p_NOMBRE_PERIODO", OracleDbType.Varchar2).Value = value.NombrePeriodo.Trim();
        cmd.Parameters.Add("p_FECHA_DESDE", OracleDbType.Date).Value = value.FechaDesde;
        cmd.Parameters.Add("p_FECHA_HASTA", OracleDbType.Date).Value = value.FechaHasta;
        cmd.Parameters.Add("p_ANO_PERIODO", OracleDbType.Int32).Value = value.AnoPeriodo;
        cmd.Parameters.Add("p_NUM_PERIODO", OracleDbType.Int32).Value = value.NumeroPeriodo;
        cmd.Parameters.Add("p_CERRADO", OracleDbType.Int32).Value = value.Cerrado ? 1 : 0;
        cmd.Parameters.Add("p_EXTRA1", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.Extra1);
        cmd.Parameters.Add("p_EXTRA2", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.Extra2);
        cmd.Parameters.Add("p_EXTRA3", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.Extra3);
        cmd.Parameters.Add("p_USUARIO_ID", OracleDbType.Int32).Value = value.UsuarioId;
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
    }
}
