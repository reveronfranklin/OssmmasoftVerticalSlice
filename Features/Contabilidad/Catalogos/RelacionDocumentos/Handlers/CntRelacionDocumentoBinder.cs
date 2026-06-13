using Oracle.ManagedDataAccess.Client;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

internal static class CntRelacionDocumentoBinder
{
    public static void AddSaveParameters(OracleCommand cmd, CntRelacionDocumentoSaveCommand value, int empresa)
    {
        cmd.Parameters.Add("p_TIPO_DOC_ID", OracleDbType.Int32).Value = value.TipoDocumentoId;
        cmd.Parameters.Add("p_TIPO_TRANS_ID", OracleDbType.Int32).Value = value.TipoTransaccionId;
        cmd.Parameters.Add("p_EXTRA1", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.Extra1);
        cmd.Parameters.Add("p_EXTRA2", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.Extra2);
        cmd.Parameters.Add("p_EXTRA3", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.Extra3);
        cmd.Parameters.Add("p_USUARIO_ID", OracleDbType.Int32).Value = value.UsuarioId;
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
    }
}
