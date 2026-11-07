using System.Security.Cryptography;
using System.Text;

namespace OssmmasoftVerticalSlice.Features.DatabaseDeployment;

public record DeploymentCompare(string Solicitud, List<string> Schemas, List<string> Types);
public record DeploymentId(string Id);
public record DeploymentObjectQuery(string Id, string Key);
public record DeploymentPrepare(string Id, List<string> Objects);
public record DeploymentIdentity(string DatabaseId, string DatabaseName, string Schema);
public record DeploymentConnectionResult(string Schema, string Ambiente, bool Configurado, bool Validado, DeploymentIdentity? Identidad, string Mensaje);
public record DeploymentAvailability(bool Habilitado, bool EjecucionHabilitada, List<string> Schemas, List<string> Types, string Mensaje);
public record DeploymentDependency(string Owner, string Name, string Type, bool Remote = false);
public record DeploymentColumn(string Name, string Type, int Length, int? Precision, int? Scale, string CharUsed, int CharLength, bool Nullable, string Default);
public record DeploymentObject(string Owner, string Type, string Name, string Status, string Ddl, List<DeploymentDependency> Dependencies, List<DeploymentColumn> Columns)
{
    public string Key => Owner + "|" + Type + "|" + Name;
    public string Hash => DeploymentRules.Hash(DeploymentRules.Normalize(Ddl, Type) + "|" + Status);
}
public record DeploymentDifference(string Key, string Schema, string Type, string Name, string Estado, string SourceStatus, string TargetStatus);
public record DeploymentPlanItem(string Key, string Action, bool Manual, string Reason, string Sql);
public record DeploymentPlan(string Id, string ComparisonId, string Hash, DateTime CreatedAt, List<DeploymentPlanItem> Items, string Script, string Version);
public sealed class DeploymentRecord
{
    public string Id { get; set; } = "";
    public int Actor { get; set; }
    public string RequestHash { get; set; } = "";
    public string Version { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string Status { get; set; } = "EN_COLA";
    public string Message { get; set; } = "";
    public List<string> Schemas { get; set; } = [];
    public List<string> Types { get; set; } = [];
    public List<DeploymentIdentity> SourceIdentities { get; set; } = [];
    public List<DeploymentIdentity> TargetIdentities { get; set; } = [];
    public List<DeploymentObject> Source { get; set; } = [];
    public List<DeploymentObject> Target { get; set; } = [];
    public List<DeploymentDifference> Differences { get; set; } = [];
    public List<DeploymentPlan> Plans { get; set; } = [];
}
public record DeploymentSummary(string Id, DateTime CreatedAt, string Estado, string Mensaje, List<string> Schemas, int Diferencias, List<DeploymentDifference> Items, List<DeploymentPlan> Planes, List<DeploymentIdentity> Pruebas, List<DeploymentIdentity> Produccion);

internal static partial class DeploymentRules
{
    internal static readonly HashSet<string> Excluded = new(["BMC", "RHC"], StringComparer.Ordinal);
    internal static readonly string[] KnownSchemas = ["RH", "SIS", "CNT", "ADM", "PRE", "RM", "CAT", "BM", "MFO"];
    internal static readonly string[] KnownTypes = ["TABLE", "VIEW", "PROCEDURE", "FUNCTION", "PACKAGE", "PACKAGE BODY", "TRIGGER", "INDEX", "SEQUENCE", "SYNONYM", "GRANT"];
    internal static bool ValidId(string id) => Guid.TryParseExact(id, "D", out var value) && value != Guid.Empty && id == value.ToString("D");
    internal static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));
    internal static bool SchemaAllowed(string value, IEnumerable<string> configured) => !Excluded.Contains(value) && KnownSchemas.Contains(value) && configured.Contains(value);
    internal static string Quote(string identifier) => "\"" + identifier.Replace("\"", "\"\"") + "\"";
    internal static DeploymentSummary Summary(DeploymentRecord value) => new(value.Id, value.CreatedAt, value.Status, value.Message, value.Schemas, value.Differences.Count(x=>x.Estado!="IGUAL"), value.Differences, value.Plans.Select(p => p with { Script = "", Items = [] }).ToList(), value.SourceIdentities, value.TargetIdentities);
    internal static List<DeploymentDifference> Compare(List<DeploymentObject> source, List<DeploymentObject> target)
    {
        var left = source.ToDictionary(x => x.Key); var right = target.ToDictionary(x => x.Key);
        return left.Keys.Union(right.Keys).Order(StringComparer.Ordinal).Select(key => {
            left.TryGetValue(key, out var s); right.TryGetValue(key, out var t); var item = s ?? t!;
            var state = (s?.Status.StartsWith("NO_ACCESIBLE") == true || t?.Status.StartsWith("NO_ACCESIBLE") == true) ? "NO_COMPARABLE" :
                s is null ? "SOLO_PRODUCCION" : !s.Status.StartsWith("VALID") || (t is not null && !t.Status.StartsWith("VALID")) ? "INVALIDO" :
                t is null ? "NUEVO" : s.Hash == t.Hash ? "IGUAL" : "MODIFICADO";
            return new DeploymentDifference(key, item.Owner, item.Type, item.Name, state, s?.Status ?? "", t?.Status ?? "");
        }).ToList();
    }
    // Tokenize only outside Oracle literals; preserve hints, quoted identifiers and alternative quoting.
    internal static string Normalize(string sql, string type)
    {
        var tokens = new List<string>();
        for (var i = 0; i < sql.Length;)
        {
            var c = sql[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }
            if (i + 1 < sql.Length && sql[i..(i+2)] == "--") { while(i < sql.Length && sql[i] != '\n') i++; continue; }
            if (i + 1 < sql.Length && sql[i..(i+2)] == "/*")
            {
                var end = sql.IndexOf("*/", i+2, StringComparison.Ordinal); end = end < 0 ? sql.Length : end+2;
                if (i+2 < sql.Length && sql[i+2] == '+') tokens.Add(sql[i..end]); i=end; continue;
            }
            var start = i;
            if ((c is 'q' or 'Q') && i+2 < sql.Length && sql[i+1] == '\'')
            {
                var close = sql[i+2] switch { '[' => ']', '{' => '}', '(' => ')', '<' => '>', var a => a };
                i+=3; while(i+1 < sql.Length && !(sql[i] == close && sql[i+1] == '\'')) i++;
                i=Math.Min(sql.Length, i+2); tokens.Add(sql[start..i]); continue;
            }
            if (c is '\'' or '"')
            {
                i++; while(i < sql.Length) { if(sql[i++] != c) continue; if(i < sql.Length && sql[i] == c) { i++; continue; } break; }
                tokens.Add(sql[start..i]); continue;
            }
            if (char.IsLetterOrDigit(c) || c is '_' or '$' or '#') { while(i < sql.Length && (char.IsLetterOrDigit(sql[i]) || sql[i] is '_' or '$' or '#')) i++; tokens.Add(sql[start..i].ToUpperInvariant()); }
            else { tokens.Add(c.ToString()); i++; }
        }
        if (type == "SEQUENCE")
            for(var i=0;i+2<tokens.Count;i++) if(tokens[i] == "START" && tokens[i+1] == "WITH")
            {
                var number=i+2;
                if(tokens[number] is "-" or "+")tokens.RemoveAt(number);
                if(number<tokens.Count && tokens[number].All(char.IsDigit))tokens[number]="<CONTADOR>";
            }
        return string.Join(' ', tokens);
    }
}
