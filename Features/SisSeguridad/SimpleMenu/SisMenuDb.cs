using System.Data;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.Features.Support;

namespace OssmmasoftVerticalSlice.Features.SisSeguridad;

internal sealed class SisMenuDb(OracleConnection cn, OracleTransaction tx)
{
    internal OracleCommand Command(string operation) => new("SIS.PKG_OSS_MENU_SIMPLE." + operation, cn)
        { CommandType = CommandType.StoredProcedure, BindByName = true, Transaction = tx };
    internal static void Add(OracleCommand cmd, string name, object? value, OracleDbType type = OracleDbType.Int32) =>
        cmd.Parameters.Add(name, type).Value = value ?? DBNull.Value;
    internal static OracleParameter Output(OracleCommand cmd, string name) => cmd.Parameters.Add(name, OracleDbType.Int32, ParameterDirection.Output);
    internal static int Int(OracleParameter p) => SupportDb.GetIntOutput(p);
    internal async Task<List<Dictionary<string, object>>> Read(string kind, int company = 0, int user = 0)
    {
        using var cmd = Command("READ_DATA");
        Add(cmd, "p_KIND", kind, OracleDbType.Varchar2); Add(cmd, "p_EMPRESA", company); Add(cmd, "p_USER", user);
        cmd.Parameters.Add("p_RESULT", OracleDbType.RefCursor, ParameterDirection.Output);
        var result = new List<Dictionary<string, object>>();
        using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            var row = new Dictionary<string, object>();
            for (var i = 0; i < r.FieldCount; i++) row[r.GetName(i)] = r.IsDBNull(i) ? "" : r.GetValue(i) is Oracle.ManagedDataAccess.Types.OracleClob clob ? clob.Value : r.GetValue(i);
            result.Add(row);
        }
        return result;
    }
    internal async Task<int> Lock(int? expected)
    {
        using var cmd = Command("LOCK_REV"); Add(cmd, "p_EXPECTED", expected); var rev = Output(cmd, "p_REV");
        await cmd.ExecuteNonQueryAsync(); return Int(rev);
    }
    internal async Task Execute(string op, params (string Name, object? Value, OracleDbType Type)[] values)
    {
        using var cmd = Command(op);
        foreach (var v in values) Add(cmd, v.Name, v.Value, v.Type);
        await cmd.ExecuteNonQueryAsync();
    }
    internal static (string, object?, OracleDbType) N(string n, int? v) => (n, v, OracleDbType.Int32);
    internal static (string, object?, OracleDbType) S(string n, string? v) => (n, v, OracleDbType.Varchar2);
    internal static (string, object?, OracleDbType) Clob(string n, string v) => (n, v, OracleDbType.Clob);
    internal static string Ids(IEnumerable<int> ids) => string.Join(',', ids.Distinct().Order());
    internal static int Number(Dictionary<string, object> r, string k) => r.TryGetValue(k, out var v) && v.ToString() != "" ? Convert.ToInt32(v) : 0;
    internal static string Text(Dictionary<string, object> r, string k) => r.GetValueOrDefault(k)?.ToString() ?? "";
}
