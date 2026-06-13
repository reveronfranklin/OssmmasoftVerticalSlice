using Oracle.ManagedDataAccess.Client;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

internal static class CntAuxiliarCommandBinder
{
    public static void AddSaveParameters(OracleCommand cmd, CntAuxiliarSaveCommand value, int empresa)
    {
        cmd.Parameters.Add("p_CODIGO_MAYOR", OracleDbType.Int32).Value = value.CodigoMayor;
        cmd.Parameters.Add("p_SEGMENTO1", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.Segmento1);
        cmd.Parameters.Add("p_SEGMENTO2", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.Segmento2);
        cmd.Parameters.Add("p_SEGMENTO3", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.Segmento3);
        cmd.Parameters.Add("p_SEGMENTO4", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.Segmento4);
        cmd.Parameters.Add("p_SEGMENTO5", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.Segmento5);
        cmd.Parameters.Add("p_SEGMENTO6", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.Segmento6);
        cmd.Parameters.Add("p_SEGMENTO7", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.Segmento7);
        cmd.Parameters.Add("p_SEGMENTO8", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.Segmento8);
        cmd.Parameters.Add("p_SEGMENTO9", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.Segmento9);
        cmd.Parameters.Add("p_SEGMENTO10", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.Segmento10);
        cmd.Parameters.Add("p_DENOMINACION", OracleDbType.Varchar2).Value = value.Denominacion.Trim();
        cmd.Parameters.Add("p_DESCRIPCION", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.Descripcion);
        cmd.Parameters.Add("p_EXTRA1", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.Extra1);
        cmd.Parameters.Add("p_EXTRA2", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.Extra2);
        cmd.Parameters.Add("p_EXTRA3", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.Extra3);
        cmd.Parameters.Add("p_FECHA_FIN_VIGENCIA", OracleDbType.Date).Value = CntDb.DbValue(value.FechaFinVigencia);
        cmd.Parameters.Add("p_CODIGO_PROVEEDOR", OracleDbType.Int32).Value = CntDb.DbValue(value.CodigoProveedor);
        cmd.Parameters.Add("p_USUARIO_ID", OracleDbType.Int32).Value = value.UsuarioId;
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
    }
}
