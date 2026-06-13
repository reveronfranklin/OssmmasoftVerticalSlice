using Oracle.ManagedDataAccess.Client;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

internal static class CntAuxiliarPucBinder
{
    public static void AddSaveParameters(OracleCommand cmd, CntAuxiliarPucSaveCommand value, int empresa)
    {
        cmd.Parameters.Add("p_CODIGO_AUXILIAR", OracleDbType.Int32).Value = value.CodigoAuxiliar;
        cmd.Parameters.Add("p_CODIGO_PUC", OracleDbType.Int32).Value = value.CodigoPuc;
        cmd.Parameters.Add("p_TIPO_DOC_ID", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.TipoDocumentoId);
        cmd.Parameters.Add("p_USUARIO_ID", OracleDbType.Int32).Value = value.UsuarioId;
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
    }
}
