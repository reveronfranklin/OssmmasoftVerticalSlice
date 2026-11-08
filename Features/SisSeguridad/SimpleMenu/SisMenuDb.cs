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
        Add(cmd, "p_KIND", kind is "LEGACY" or "SOURCE" ? kind + "_META" : kind, OracleDbType.Varchar2); Add(cmd, "p_EMPRESA", company); Add(cmd, "p_USER", user);
        cmd.Parameters.Add("p_RESULT", OracleDbType.RefCursor, ParameterDirection.Output);
        var result = new List<Dictionary<string, object>>();
        var stage = "abrir cursor";
        try
        {
            using var r = await cmd.ExecuteReaderAsync();
            while (true)
            {
                stage = "leer fila";
                if (!await r.ReadAsync()) break;
                var row = new Dictionary<string, object>();
                for (var i = 0; i < r.FieldCount; i++)
                {
                    var column = r.GetName(i);
                    stage = "leer columna " + column;
                    if (r.IsDBNull(i)) { row[column] = ""; continue; }
                    var value = r.GetValue(i);
                    if (value is Oracle.ManagedDataAccess.Types.OracleClob clob)
                    {
                        using (clob) row[column] = clob.Value;
                    }
                    else row[column] = value;
                }
                result.Add(row);
            }
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Menu SIS READ_DATA/{kind} ({stage}): {ex.GetType().Name}", ex);
        }
        if (kind is "LEGACY" or "SOURCE")
            foreach (var row in result) row["JSON_MENU"] = await ReadJson(Number(row, "CODIGO_USUARIO_ROL"));
        return result;
    }
    private async Task<string> ReadJson(int id)
    {
        var text = new System.Text.StringBuilder();
        var offset = 1;
        int? length = null;
        try
        {
            while (true)
            {
                using var cmd = Command("READ_JSON");
                Add(cmd, "p_ID", id); Add(cmd, "p_OFFSET", offset);
                var part = cmd.Parameters.Add("p_TEXT", OracleDbType.Varchar2, 4000);
                part.Direction = ParameterDirection.Output;
                var total = Output(cmd, "p_TOTAL"); var next = Output(cmd, "p_NEXT");
                await cmd.ExecuteNonQueryAsync();
                var size = Int(total);
                if (length is not null && length != size) throw new InvalidOperationException("El menu cambio durante su lectura. Reintente.");
                length = size;
                if (offset > size) break;
                var value = part.Value;
                var chunk = value is null or DBNull ? "" : value is Oracle.ManagedDataAccess.Types.OracleString os ? (os.IsNull ? "" : os.Value) : value.ToString() ?? "";
                var following = Int(next);
                if (chunk.Length == 0 || following <= offset) throw new InvalidOperationException("Lectura incompleta del menu.");
                text.Append(chunk); offset = following;
            }
            return text.ToString();
        }
        catch (Exception ex) { throw new InvalidOperationException("Menu SIS READ_JSON (fragmentos): " + ex.GetType().Name, ex); }
    }
    internal async Task<int> Lock(int? expected)
    {
        using var cmd = Command("LOCK_REV"); Add(cmd, "p_EXPECTED", expected); var rev = Output(cmd, "p_REV");
        try { await cmd.ExecuteNonQueryAsync(); return Int(rev); }
        catch (Exception ex) { throw new InvalidOperationException("Menu SIS LOCK_REV: " + ex.GetType().Name, ex); }
    }
    internal async Task Execute(string op, params (string Name, object? Value, OracleDbType Type)[] values)
    {
        using var cmd = Command(op);
        foreach (var v in values) Add(cmd, v.Name, v.Value, v.Type);
        try { await cmd.ExecuteNonQueryAsync(); }
        catch (Exception ex) { throw new InvalidOperationException("Menu SIS " + op + ": " + ex.GetType().Name, ex); }
    }
    internal static (string, object?, OracleDbType) N(string n, int? v) => (n, v, OracleDbType.Int32);
    internal static (string, object?, OracleDbType) S(string n, string? v) => (n, v, OracleDbType.Varchar2);
    internal static (string, object?, OracleDbType) Clob(string n, string v) => (n, v, OracleDbType.Clob);
    internal static string Ids(IEnumerable<int> ids) => string.Join(',', ids.Distinct().Order());
    internal static int Number(Dictionary<string, object> r, string k) => r.TryGetValue(k, out var v) && v.ToString() != "" ? Convert.ToInt32(v) : 0;
    internal static string Text(Dictionary<string, object> r, string k) => r.GetValueOrDefault(k)?.ToString() ?? "";
}
