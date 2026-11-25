using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace OssmmasoftVerticalSlice.Features.OracleAnalisis;

public sealed class OracleAnalisisOptions
{
    public bool Enabled { get; set; }
    public OracleAnalisisAccess ServiceAccess { get; set; } = new();
    public Dictionary<string, OracleAnalisisTarget> Targets { get; set; } = new();
    public int TimeoutSeconds { get; set; } = 15;
    public int MaxRows { get; set; } = 500;
    public int MaxResponseBytes { get; set; } = 2 * 1024 * 1024;
    public static OracleAnalisisOptions Read(IConfiguration config) =>
        config.GetSection("OracleAnalysis").Get<OracleAnalisisOptions>() ?? new();
}
public sealed class OracleAnalisisAccess
{
    public bool Enabled { get; set; }
    public string ClientId { get; set; } = "";
    public string Token { get; set; } = "";
    public DateTimeOffset? ExpiresAtUtc { get; set; }
    public List<string> AllowedAliases { get; set; } = [];
    public List<string> Permissions { get; set; } = [];
    public override string ToString() => "Credencial privada OracleAnalisis";
}
public sealed class OracleAnalisisTarget
{
    public string Schema { get; set; } = "";
    public string ExpectedDatabaseName { get; set; } = "";
    public string ExpectedServerHost { get; set; } = "";
    public string ExpectedSessionUser { get; set; } = "";
    // Explicit names; empty means sources are denied. No wildcard.
    public List<string> SourceObjects { get; set; } = [];
    public Dictionary<string, OracleAnalisisDataPolicy> DataObjects { get; set; } = new();
}
public static class OracleAnalisisRules
{
    public const string Scheme = "OracleAnalysisService";
    public const string Header = "X-Oracle-Analysis-Token";
    public const string Metadata = "oracle.analisis.metadatos";
    public const string Sources = "oracle.analisis.fuentes";
    private static readonly HashSet<string> Aliases = ["SIS", "RH", "PRE", "ADM", "CNT", "BM", "CAT", "RM", "MFO"];
    public static bool AliasAllowed(string alias, OracleAnalisisOptions options) =>
        Aliases.Contains(alias) && options.ServiceAccess.AllowedAliases.Contains(alias, StringComparer.Ordinal)
        && options.Targets.ContainsKey(alias);
    public static string Identifier(string? value)
    {
        if (value is null || !Regex.IsMatch(value, @"\A[A-Z][A-Z0-9_$#]{0,29}\z"))
            throw new OracleAnalisisValidation("IDENTIFICADOR_INVALIDO", "Use identificadores Oracle en mayusculas de hasta 30 caracteres.");
        return value;
    }
    public static bool ValidToken(OracleAnalisisOptions options, string? received, DateTimeOffset now)
    {
        var access = options.ServiceAccess;
        return options.Enabled && access.Enabled && !string.IsNullOrWhiteSpace(access.ClientId)
            && access.Token.Length >= 32 && access.ExpiresAtUtc > now
            && !string.IsNullOrWhiteSpace(received) && received.Length <= 4096
            && CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(access.Token)),
                SHA256.HashData(Encoding.UTF8.GetBytes(received)));
    }
}
public sealed class OracleAnalisisValidation(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}
