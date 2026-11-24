using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using OssmmasoftVerticalSlice.ContextDB;

namespace OssmmasoftVerticalSlice.Features.OracleAnalisis;

public record OracleAnalisisIdentity(string Database, string ServerHost, string SessionUser, string Schema);
public record OracleAnalisisRows(List<Dictionary<string, object?>> Filas, bool HasMore, bool Truncado, int Limite);

public sealed class OracleAnalisisDb(OracleConnection connection, OracleAnalisisOptions options,
    OracleAnalisisTarget target) : IDisposable
{
    public OracleAnalisisIdentity Identity { get; private set; } = new("", "", "", "");
    public OracleAnalisisTarget Target => target;
    public OracleAnalisisOptions Options => options;
    public static OracleConnection Connection(ConnectionDB db, string alias) => alias switch
    {
        "SIS" => db.GetSisConnection(), "RH" => db.GetRhConnection(), "PRE" => db.GetPresupuestoConnection(),
        "ADM" => db.GetAdmConnection(), "CNT" => db.GetCntConnection(), "BM" => db.GetBmConnection(),
        "CAT" => db.GetCatConnection(), "RM" => db.GetRmConnection(), "MFO" => db.GetMfoConnection(),
        _ => throw new OracleAnalisisValidation("CONEXION_NO_AUTORIZADA", "Conexion no autorizada.")
    };
    public async Task Open(CancellationToken ct)
    {
        OracleAnalisisRules.Identifier(target.Schema);
        if (string.IsNullOrWhiteSpace(target.ExpectedDatabaseName) || string.IsNullOrWhiteSpace(target.ExpectedServerHost)
            || string.IsNullOrWhiteSpace(target.ExpectedSessionUser))
            throw new OracleAnalisisValidation("DESTINO_SIN_CONFIGURAR", "Configure la identidad esperada del destino antes de consultar.");
        try { await connection.OpenAsync(ct); }
        catch (OracleException) { throw new OracleAnalisisValidation("CONEXION_NO_DISPONIBLE", "No se pudo abrir la conexion Oracle configurada."); }
        using var cmd = Command("""
            SELECT SYS_CONTEXT('USERENV','DB_NAME'), SYS_CONTEXT('USERENV','SERVER_HOST'),
                   SYS_CONTEXT('USERENV','SESSION_USER') FROM DUAL
            """);
        using var reader = await cmd.ExecuteReaderAsync(ct);
        if (!await reader.ReadAsync(ct)) throw new OracleAnalisisValidation("DESTINO_DESCONOCIDO", "No se pudo identificar el destino.");
        Identity = new(reader.GetString(0), reader.GetString(1), reader.GetString(2), target.Schema);
        if (!string.Equals(Identity.Database, target.ExpectedDatabaseName, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(Identity.ServerHost, target.ExpectedServerHost, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(Identity.SessionUser, target.ExpectedSessionUser, StringComparison.OrdinalIgnoreCase))
            throw new OracleAnalisisValidation("DESTINO_INESPERADO", "La identidad Oracle no coincide con el destino autorizado.");
    }
    public OracleCommand Command(string sql, params (string Name, object? Value)[] binds)
    {
        var cmd = new OracleCommand(sql, connection)
        {
            BindByName = true, CommandType = CommandType.Text,
            CommandTimeout = Math.Clamp(options.TimeoutSeconds, 1, 30), InitialLONGFetchSize = 16384
        };
        foreach (var (name, value) in binds)
        {
            var type = value is int ? OracleDbType.Int32 : OracleDbType.Varchar2;
            cmd.Parameters.Add(name, type).Value = value ?? DBNull.Value;
        }
        return cmd;
    }
    public async Task<OracleAnalisisRows> Read(string sql, int limit, CancellationToken ct, params (string Name, object? Value)[] binds)
    {
        limit = Math.Clamp(limit, 1, Math.Clamp(options.MaxRows, 1, 1000));
        // ROWNUM is compatible with Oracle 10g. Caller SQL is fixed and never client supplied.
        using var cmd = Command($"SELECT * FROM ({sql}) WHERE ROWNUM <= :oa_limit", binds);
        cmd.Parameters.Add("oa_limit", OracleDbType.Int32).Value = limit + 1;
        return await ReadRows(cmd, limit, ct);
    }
    public async Task<OracleAnalisisRows> ReadData(OracleAnalisisPlan plan, CancellationToken ct)
    {
        using var cmd = Command($"SELECT * FROM ({plan.Sql}) WHERE ROWNUM <= :oa_limit");
        foreach (var bind in plan.Binds) cmd.Parameters.Add(bind.Name, bind.Type).Value = bind.Value ?? DBNull.Value;
        cmd.Parameters.Add("oa_limit", OracleDbType.Int32).Value = plan.Limit + 1;
        return await ReadRows(cmd, plan.Limit, ct);
    }
    private async Task<OracleAnalisisRows> ReadRows(OracleCommand cmd, int limit, CancellationToken ct)
    {
        using var registration = ct.Register(() => { try { cmd.Cancel(); } catch { /* Disposal race. */ } });
        using var reader = await cmd.ExecuteReaderAsync(ct);
        var rows = new List<Dictionary<string, object?>>();
        var truncated = false;
        var bytes = 0;
        while (await reader.ReadAsync(ct))
        {
            if (rows.Count == limit) return new(rows, true, truncated, limit);
            var row = new Dictionary<string, object?>();
            for (var i = 0; i < reader.FieldCount; i++)
            {
                object? value = null;
                if (!reader.IsDBNull(i))
                {
                    var type = reader.GetDataTypeName(i).ToUpperInvariant();
                    if (type is "BLOB" or "RAW" or "LONG RAW" or "BFILE") { value = "[BINARIO OMITIDO]"; truncated = true; }
                    else if (type is "NUMBER" or "DECIMAL" or "INT16" or "INT32" or "INT64") value = reader.GetOracleDecimal(i).ToString();
                    else if (type == "CLOB")
                    {
                        using var clob = reader.GetOracleClob(i);
                        var chars = new char[(int)Math.Min(clob.Length, 16384)];
                        var count = clob.Read(chars, 0, chars.Length);
                        value = new string(chars, 0, count);
                        truncated |= clob.Length > count;
                    }
                    else
                    {
                        value = reader.GetValue(i);
                        if (value is DateTime date) value = date.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff", CultureInfo.InvariantCulture);
                        else if (value is not (string or bool or int or long or decimal or double)) value = Convert.ToString(value, CultureInfo.InvariantCulture);
                        if (value is string text && text.Length > 16384) { value = text[..16384]; truncated = true; }
                    }
                }
                row[reader.GetName(i)] = value;
            }
            bytes += JsonSerializer.SerializeToUtf8Bytes(row).Length;
            // Leave room for envelope and metadata.
            if (bytes > Math.Clamp(options.MaxResponseBytes, 32768, 2 * 1024 * 1024) - 8192)
                return new(rows, true, true, limit);
            rows.Add(row);
        }
        return new(rows, false, truncated, limit);
    }
    public void Dispose() => connection.Dispose();
}
