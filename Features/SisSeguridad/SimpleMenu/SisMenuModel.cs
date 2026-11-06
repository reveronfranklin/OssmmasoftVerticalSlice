using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OssmmasoftVerticalSlice.Features.SisSeguridad;

public record SisMenuNode(int Id, int Mod, int? Parent, string Title, string Path, string Icon, int Order, bool Active);
public record SisMenuRole(int Id, int Mod, string Key, string Name, string Description, bool Active);
public record SisMenuGrant(int Role, int Menu);
public record SisMenuPerm(int Id, string Key);
public record SisMenuRolePerm(int Role, int Perm);
public record SisMenuNodePerm(int Menu, int Perm);
public record SisMenuSource(int Id, string Module, string Json, int UserId = 0, string Login = "");
public record SisMenuOrigin(int CodigoMenu, List<int> Roles);
public record SisMenuResolved(JsonArray Menu, List<SisMenuOrigin> Origenes);
public record SisMenuLegacyNode(string Module, string Title, string Path, string Icon, int Order, List<string> Permissions, List<SisMenuLegacyNode> Children);

// Shared by migration verification, session rendering and administrative simulation.
public static class SisMenuModel
{
    public const string ReportsPath = "/apps/mfo/formularios";
    public static string Hash(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)));

    public static SisMenuResolved Resolve(IReadOnlyList<SisMenuNode> nodes, IReadOnlyList<SisMenuRole> roles,
        IReadOnlyList<SisMenuGrant> grants, IReadOnlyList<int> assigned,
        IReadOnlyList<SisMenuPerm> permissions, IReadOnlyList<SisMenuRolePerm> rolePermissions,
        IReadOnlyList<SisMenuNodePerm> nodePermissions)
    {
        var byId = nodes.ToDictionary(x => x.Id);
        foreach (var node in nodes)
        {
            var visited = new HashSet<int>();
            for (var current = node; current is not null; current = current.Parent is int p ? byId.GetValueOrDefault(p) : null)
                if (!visited.Add(current.Id)) throw new InvalidOperationException("El catalogo contiene un ciclo.");
        }
        bool Available(SisMenuNode node) => node.Active && (node.Parent is not int p || byId.TryGetValue(p, out var parent) && Available(parent));
        var activeRoles = roles.Where(x => x.Active && assigned.Contains(x.Id)).Select(x => x.Id).ToHashSet();
        var origins = grants.Where(x => activeRoles.Contains(x.Role) && byId.TryGetValue(x.Menu, out var n) && Available(n))
            .GroupBy(x => x.Menu).ToDictionary(x => x.Key, x => x.Select(v => v.Role).Distinct().Order().ToList());
        var selected = origins.Keys.ToHashSet();
        foreach (var id in selected.ToList())
            for (var p = byId[id].Parent; p is int parent; p = byId[parent].Parent) selected.Add(parent);
        var permIds = rolePermissions.Where(x => activeRoles.Contains(x.Role)).Select(x => x.Perm).ToHashSet();
        var permKeys = permissions.Where(x => permIds.Contains(x.Id)).ToDictionary(x => x.Id, x => x.Key);
        // Profiles can retain a different ordering of the same option. Merge equivalent
        // presentation nodes when roles are combined, preserving all permission sources.
        var keys = new Dictionary<int,string>();
        string Key(SisMenuNode node) => keys.TryGetValue(node.Id,out var key) ? key : keys[node.Id] = Hash(JsonSerializer.Serialize(new {
            node.Mod, Parent=node.Parent is int p ? Key(byId[p]) : "", node.Title,node.Path,node.Icon }));
        var classes=nodes.Where(x=>selected.Contains(x.Id)).GroupBy(Key).ToDictionary(x=>x.Key,x=>x.OrderBy(n=>n.Order).ThenBy(n=>n.Id).ToList());
        JsonObject Build(List<SisMenuNode> variants)
        {
            var node=variants[0]; var direct=variants.Where(x=>origins.ContainsKey(x.Id)).Select(x=>x.Id).ToHashSet();
            var result=new JsonObject { ["title"]=node.Title };
            if(node.Icon.Length>0) result["icon"]=node.Icon;
            if(direct.Count>0 && node.Path.Length>0) result["path"]=node.Path;
            var granted=nodePermissions.Where(x=>direct.Contains(x.Menu) && permKeys.ContainsKey(x.Perm))
                .Select(x=>permKeys[x.Perm]).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
            if(granted.Length>0) result["permissions"]=new JsonArray(granted.Select(x=>JsonValue.Create(x)).ToArray<JsonNode?>());
            var parentKey=Key(node);
            var children=classes.Values.Where(x=>x[0].Parent is int p && Key(byId[p])==parentKey)
                .OrderBy(x=>x[0].Order).ThenBy(x=>x[0].Id).Select(Build).ToArray<JsonNode?>();
            if(children.Length>0) result["children"]=new JsonArray(children);
            return result;
        }
        return new(new JsonArray(classes.Values.Where(x=>x[0].Parent is null).OrderBy(x=>x[0].Order).ThenBy(x=>x[0].Id).Select(Build).ToArray<JsonNode?>()),
            origins.Select(x=>new SisMenuOrigin(x.Key,x.Value)).ToList());
    }

    public static bool IsInternalPath(string path) => path.Length == 0 ||
        (path.StartsWith("/apps/", StringComparison.Ordinal) || path.StartsWith("/dashboards/", StringComparison.Ordinal)) && !path.Contains("//") && !path.Contains('\\') && !path.Contains("..") && !path.Contains('?') && !path.Contains('#');

    public static List<SisMenuLegacyNode> ParseLegacy(IReadOnlyList<SisMenuSource> sources, ISet<string> knownRoutes)
    {
        var total = 0;
        SisMenuLegacyNode Parse(JsonElement item, string module, int order, int depth)
        {
            if (++total > 10000 || depth > 30) throw new InvalidOperationException("Menu demasiado grande o profundo.");
            if (item.ValueKind != JsonValueKind.Object) throw new InvalidOperationException("Nodo de menu invalido.");
            foreach (var prop in item.EnumerateObject())
                if (prop.Name is not ("title" or "path" or "icon" or "children" or "permissions" or "action" or "subject" or "openInNewTab"))
                    throw new InvalidOperationException($"Propiedad no soportada: {prop.Name}.");
            string Text(string key) => !item.TryGetProperty(key, out var value) || value.ValueKind == JsonValueKind.Null ? "" :
                value.ValueKind == JsonValueKind.String ? value.GetString()! : throw new InvalidOperationException($"{key} debe ser texto.");
            var title = Text("title"); var path = Text("path"); var icon = Text("icon");
            if (string.IsNullOrWhiteSpace(title) || title.Length > 120)
                throw new InvalidOperationException($"Titulo vacio o mayor de 120 caracteres: {title}.");
            if (path.Length > 300 || !IsInternalPath(path))
                throw new InvalidOperationException($"Ruta interna invalida en '{title}': {path}.");
            if (icon.Length > 100)
                throw new InvalidOperationException($"Icono mayor de 100 caracteres en '{title}'.");
            if (path.Length > 0 && !knownRoutes.Contains(path.TrimEnd('/')))
                throw new InvalidOperationException($"Ruta sin validar en el catalogo: {path}.");
            var perms = new List<string>();
            if (item.TryGetProperty("permissions", out var values))
            {
                if (values.ValueKind != JsonValueKind.Array) throw new InvalidOperationException("permissions debe ser un arreglo.");
                foreach (var value in values.EnumerateArray())
                {
                    if (value.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()) || value.GetString()!.Length > 120)
                        throw new InvalidOperationException("Clave de permiso invalida.");
                    perms.Add(value.GetString()!);
                }
            }
            var children = new List<SisMenuLegacyNode>();
            if (item.TryGetProperty("children", out var nested))
            {
                if (nested.ValueKind != JsonValueKind.Array) throw new InvalidOperationException("children debe ser un arreglo.");
                foreach (var child in nested.EnumerateArray()) children.Add(Parse(child, module, children.Count, depth + 1));
            }
            if (path.Length == 0 && children.Count == 0) throw new InvalidOperationException($"Grupo vacio: {title}.");
            return new(module, title, path, icon, order, perms.Distinct().Order(StringComparer.Ordinal).ToList(), children);
        }
        var roots = new List<SisMenuLegacyNode>();
        foreach (var source in sources)
        {
            var module = source.Module.Trim().ToUpperInvariant();
            if (module.Length is 0 or > 10 || module.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_'))
                throw new InvalidOperationException($"Modulo legacy no reconocido: {source.Module}.");
            using var doc = JsonDocument.Parse(source.Json);
            if (doc.RootElement.ValueKind != JsonValueKind.Array) throw new InvalidOperationException("JSON_MENU debe ser un arreglo.");
            foreach (var item in doc.RootElement.EnumerateArray()) roots.Add(Parse(item, module, roots.Count, 0));
        }
        // Mirrors withReportes: remove existing report links and append the common entry.
        List<SisMenuLegacyNode> RemoveReports(List<SisMenuLegacyNode> list) => list.Where(x => x.Path.TrimEnd('/') != ReportsPath)
            .Select(x => x with { Children = RemoveReports(x.Children) }).ToList();
        roots = RemoveReports(roots);
        IEnumerable<SisMenuLegacyNode> Walk(IEnumerable<SisMenuLegacyNode> list)
        {
            foreach(var item in list) { yield return item; foreach(var child in Walk(item.Children)) yield return child; }
        }
        var duplicate=Walk(roots).Where(x=>x.Path.Length>0).GroupBy(x=>x.Path,StringComparer.Ordinal).FirstOrDefault(x=>x.Count()>1);
        if(duplicate is not null) throw new InvalidOperationException($"Ruta duplicada en el origen: {duplicate.Key}. Conciliar antes de migrar.");
        roots.Add(new("SIS", "Reportes", ReportsPath, "mdi:shield-account-outline", roots.Count, [], []));
        return roots;
    }

    public static JsonArray LegacyTree(IEnumerable<SisMenuLegacyNode> nodes) => new(nodes.Select(x =>
    {
        var obj = new JsonObject { ["title"] = x.Title };
        if (x.Icon.Length > 0) obj["icon"] = x.Icon;
        if (x.Path.Length > 0) obj["path"] = x.Path;
        if (x.Permissions.Count > 0) obj["permissions"] = new JsonArray(x.Permissions.Select(p => JsonValue.Create(p)).ToArray<JsonNode?>());
        if (x.Children.Count > 0) obj["children"] = LegacyTree(x.Children);
        return (JsonNode)obj;
    }).ToArray());
}
