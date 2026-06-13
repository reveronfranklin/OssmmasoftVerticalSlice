using Oracle.ManagedDataAccess.Client;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

internal static class CntConciliacionMatchingBinder
{
    public static void AddMatchParameters(OracleCommand cmd, CntConciliacionMatchCommand value, int empresa)
    {
        cmd.Parameters.Add("p_CODIGO_CONCILIACION", OracleDbType.Int32).Value = value.CodigoConciliacion;
        cmd.Parameters.Add("p_CODIGO_DETALLE_EDO_CTA", OracleDbType.Int32).Value = CntDb.DbValue(value.CodigoDetalleEdoCta);
        cmd.Parameters.Add("p_CODIGO_DETALLE_LIBRO", OracleDbType.Int32).Value = CntDb.DbValue(value.CodigoDetalleLibro);
        cmd.Parameters.Add("p_USUARIO_ID", OracleDbType.Int32).Value = value.UsuarioId;
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
    }

    public static void AddUnmatchParameters(OracleCommand cmd, CntConciliacionUnmatchCommand value, int empresa)
    {
        cmd.Parameters.Add("p_CODIGO_TMP", OracleDbType.Int32).Value = value.CodigoTmpConciliacion;
        cmd.Parameters.Add("p_USUARIO_ID", OracleDbType.Int32).Value = value.UsuarioId;
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
    }

    public static void AddMatchMultiParameters(OracleCommand cmd, CntConciliacionMatchMultiCommand value, int empresa)
    {
        cmd.Parameters.Add("p_CODIGO_CONCILIACION", OracleDbType.Int32).Value = value.CodigoConciliacion;
        cmd.Parameters.Add("p_CODIGOS_EDO_CTA", OracleDbType.Varchar2).Value = CntDb.StringDbValue(string.Join(",", value.CodigosDetalleEdoCta ?? []));
        cmd.Parameters.Add("p_CODIGOS_LIBRO", OracleDbType.Varchar2).Value = CntDb.StringDbValue(string.Join(",", value.CodigosDetalleLibro ?? []));
        cmd.Parameters.Add("p_USUARIO_ID", OracleDbType.Int32).Value = value.UsuarioId;
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
    }

    public static void AddSuggestionParameters(OracleCommand cmd, CntConciliacionSuggestionGetQuery value, int empresa)
    {
        cmd.Parameters.Add("p_CODIGO_CONCILIACION", OracleDbType.Int32).Value = value.CodigoConciliacion;
        cmd.Parameters.Add("p_TOLERANCIA_DIAS", OracleDbType.Int32).Value = Math.Max(value.ToleranciaDias, 0);
        cmd.Parameters.Add("p_TOLERANCIA_MONTO", OracleDbType.Decimal).Value = Math.Max(value.ToleranciaMonto, 0);
        cmd.Parameters.Add("p_MAX_ROWS", OracleDbType.Int32).Value = value.MaxRows <= 0 ? 100 : value.MaxRows;
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
    }
}
