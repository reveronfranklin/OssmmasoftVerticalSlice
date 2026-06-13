using Oracle.ManagedDataAccess.Client;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

internal static class CntBancoArchivoBinder
{
    public static void AddGetParameters(OracleCommand cmd, CntBancoArchivoGetQuery value, int empresa)
    {
        cmd.Parameters.Add("p_CODIGO_BANCO", OracleDbType.Int32).Value = CntDb.DbValue(value.CodigoBanco);
        cmd.Parameters.Add("p_CODIGO_CUENTA_BANCO", OracleDbType.Int32).Value = CntDb.DbValue(value.CodigoCuentaBanco);
        cmd.Parameters.Add("p_SEARCH_TEXT", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.SearchText);
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
    }

    public static void AddCreateControlParameters(OracleCommand cmd, CntBancoArchivoControlCreateCommand value, int empresa)
    {
        cmd.Parameters.Add("p_CODIGO_BANCO", OracleDbType.Int32).Value = value.CodigoBanco;
        cmd.Parameters.Add("p_CODIGO_CUENTA_BANCO", OracleDbType.Int32).Value = value.CodigoCuentaBanco;
        cmd.Parameters.Add("p_NOMBRE_ARCHIVO", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.NombreArchivo);
        cmd.Parameters.Add("p_FECHA_DESDE", OracleDbType.Date).Value = value.FechaDesde;
        cmd.Parameters.Add("p_FECHA_HASTA", OracleDbType.Date).Value = value.FechaHasta;
        cmd.Parameters.Add("p_SALDO_INICIAL", OracleDbType.Decimal).Value = value.SaldoInicial;
        cmd.Parameters.Add("p_SALDO_FINAL", OracleDbType.Decimal).Value = value.SaldoFinal;
        cmd.Parameters.Add("p_USUARIO_ID", OracleDbType.Int32).Value = value.UsuarioId;
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
    }

    public static void AddCreateDetailParameters(OracleCommand cmd, CntBancoArchivoDetalleCreateCommand value, int empresa)
    {
        cmd.Parameters.Add("p_CODIGO_BANCO_ARCHIVO_CONTROL", OracleDbType.Int32).Value = value.CodigoBancoArchivoControl;
        cmd.Parameters.Add("p_FECHA_TRANSACCION", OracleDbType.Date).Value = value.FechaTransaccion;
        cmd.Parameters.Add("p_NUMERO_TRANSACCION", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.NumeroTransaccion);
        cmd.Parameters.Add("p_TIPO_TRANSACCION_ID", OracleDbType.Int32).Value = value.TipoTransaccionId;
        cmd.Parameters.Add("p_TIPO_TRANSACCION", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.TipoTransaccion);
        cmd.Parameters.Add("p_DESCRIPCION_TRANSACCION", OracleDbType.Varchar2).Value = CntDb.StringDbValue(value.DescripcionTransaccion);
        cmd.Parameters.Add("p_MONTO_TRANSACCION", OracleDbType.Decimal).Value = value.MontoTransaccion;
        cmd.Parameters.Add("p_USUARIO_ID", OracleDbType.Int32).Value = value.UsuarioId;
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
    }

    public static void AddConfirmParameters(OracleCommand cmd, CntBancoArchivoConfirmCommand value, int empresa)
    {
        cmd.Parameters.Add("p_CODIGO_BANCO_ARCHIVO_CONTROL", OracleDbType.Int32).Value = value.CodigoBancoArchivoControl;
        cmd.Parameters.Add("p_USUARIO_ID", OracleDbType.Int32).Value = value.UsuarioId;
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
    }
}
