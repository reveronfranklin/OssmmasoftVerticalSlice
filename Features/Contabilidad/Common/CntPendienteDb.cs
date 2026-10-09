using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

public static class CntMoney
{
    public static decimal Normalize(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
    public static bool IsBalanced(IEnumerable<decimal> amounts) => amounts.Sum(Normalize) == 0m;
}

internal static class CntPendienteDb
{
    public static async Task LockAsync(OracleConnection cn, OracleTransaction tx)
    {
        using var cmd = new OracleCommand("CNT.CNT_CMP_GUARD.LOCK_ALL", cn) { CommandType = CommandType.StoredProcedure, BindByName = true, Transaction = tx };
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task AllowWriteAsync(OracleConnection cn, OracleTransaction tx, int id, int empresa)
    {
        using var cmd = new OracleCommand("CNT.CNT_CMP_GUARD.ALLOW_WRITE", cn) { CommandType = CommandType.StoredProcedure, BindByName = true, Transaction = tx };
        cmd.Parameters.Add("p_cmp", OracleDbType.Int32).Value = id;
        cmd.Parameters.Add("p_empresa", OracleDbType.Int32).Value = empresa;
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task ValidatePeriodAsync(OracleConnection cn, OracleTransaction tx, int periodo, int empresa, DateTime fecha)
    {
        using var cmd = new OracleCommand(@"SELECT COUNT(*) FROM CNT.CNT_PERIODOS WHERE CODIGO_PERIODO = :periodo
            AND CODIGO_EMPRESA = :empresa AND FECHA_CIERRE IS NULL AND :fecha BETWEEN FECHA_DESDE AND FECHA_HASTA", cn) { BindByName = true, Transaction = tx };
        cmd.Parameters.Add("periodo", OracleDbType.Int32).Value = periodo;
        cmd.Parameters.Add("empresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("fecha", OracleDbType.Date).Value = fecha;
        if (Convert.ToInt32(await cmd.ExecuteScalarAsync()) != 1)
            throw new InvalidOperationException("No existe un periodo abierto para la fecha indicada.");
    }

    public static async Task CheckEditAsync(OracleConnection cn, OracleTransaction tx, int id, int empresa, int? version, int usuario, bool automatico, string operacion)
    {
        using var cmd = new OracleCommand("CNT.CNT_CMP_GUARD.CHECK_EDIT", cn) { CommandType = CommandType.StoredProcedure, BindByName = true, Transaction = tx };
        cmd.Parameters.Add("p_cmp", OracleDbType.Int32).Value = id;
        cmd.Parameters.Add("p_empresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_version", OracleDbType.Int32).Value = CntDb.DbValue(version);
        cmd.Parameters.Add("p_usuario", OracleDbType.Int32).Value = usuario;
        cmd.Parameters.Add("p_automatico", OracleDbType.Int32).Value = automatico ? 1 : 0;
        cmd.Parameters.Add("p_operacion", OracleDbType.Varchar2).Value = operacion;
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task FinishAsync(OracleConnection cn, OracleTransaction tx, int id, int empresa, int usuario)
    {
        using var cmd = new OracleCommand("CNT.CNT_CMP_GUARD.FINISH_EDIT", cn) { CommandType = CommandType.StoredProcedure, BindByName = true, Transaction = tx };
        cmd.Parameters.Add("p_cmp", OracleDbType.Int32).Value = id;
        cmd.Parameters.Add("p_empresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_usuario", OracleDbType.Int32).Value = usuario;
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task AuditAsync(OracleConnection cn, OracleTransaction tx, int id, int empresa, int usuario, string operacion)
    {
        using var cmd = new OracleCommand("CNT.CNT_CMP_GUARD.AUDIT_CMP", cn) { CommandType = CommandType.StoredProcedure, BindByName = true, Transaction = tx };
        cmd.Parameters.Add("p_cmp", OracleDbType.Int32).Value = id;
        cmd.Parameters.Add("p_empresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_usuario", OracleDbType.Int32).Value = usuario;
        cmd.Parameters.Add("p_operacion", OracleDbType.Varchar2).Value = operacion;
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task InvalidatePrecloseAsync(OracleConnection cn, OracleTransaction tx, int periodo, int empresa)
    {
        using var cmd = new OracleCommand(@"UPDATE CNT.CNT_PERIODOS SET FECHA_PRECIERRE = NULL, USUARIO_PRECIERRE = NULL
            WHERE CODIGO_PERIODO = :periodo AND CODIGO_EMPRESA = :empresa", cn) { BindByName = true, Transaction = tx };
        cmd.Parameters.Add("periodo", OracleDbType.Int32).Value = periodo;
        cmd.Parameters.Add("empresa", OracleDbType.Int32).Value = empresa;
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task<int> ResolveDetailAsync(OracleConnection cn, OracleTransaction tx, int detalle, int empresa)
    {
        using var cmd = new OracleCommand(@"SELECT CODIGO_COMPROBANTE FROM CNT.CNT_DETALLE_COMPROBANTE
            WHERE CODIGO_DETALLE_COMPROBANTE = :detalle AND CODIGO_EMPRESA = :empresa", cn) { BindByName = true, Transaction = tx };
        cmd.Parameters.Add("detalle", OracleDbType.Int32).Value = detalle;
        cmd.Parameters.Add("empresa", OracleDbType.Int32).Value = empresa;
        var value = await cmd.ExecuteScalarAsync();
        if (value is null || value == DBNull.Value) throw new InvalidOperationException("Detalle no encontrado.");
        return Convert.ToInt32(value);
    }
}
