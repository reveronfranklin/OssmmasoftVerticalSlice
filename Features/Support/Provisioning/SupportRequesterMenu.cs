using System.Text.Json;
using System.Text.Json.Nodes;

namespace OssmmasoftVerticalSlice.Features.Support;

// Used by the administrative provisioning tool. Does not change menu strategy.
public static class SupportRequesterMenu
{
    public static readonly string[] Permissions = ["soporte.tickets.crear","soporte.tickets.ver_propios","soporte.comentarios.crear"];
    public static readonly string[] Paths = ["/apps/soporte/tickets","/apps/soporte/notificaciones"];
    public static (JsonArray Menu,bool Repaired) Parse(string json)
    {
        try { return (JsonNode.Parse(json)!.AsArray(),false); }
        catch(JsonException)
        {
            // Recover only missing closing delimiters; never guess a truncated string or value.
            var text=json.TrimEnd();var stack=new Stack<char>();bool quoted=false,escape=false;
            foreach(var c in text)
            {
                if(quoted){if(escape)escape=false;else if(c=='\\')escape=true;else if(c=='"')quoted=false;continue;}
                if(c=='"'){quoted=true;continue;}
                if(c=='[')stack.Push(']');else if(c=='{')stack.Push('}');
                else if(c is ']' or '}'){if(stack.Count==0 || stack.Pop()!=c)throw new InvalidOperationException("JSON con estructura incompatible.");}
            }
            if(quoted || stack.Count==0 || text.Length==0 || text[^1] is ':' or ',')
                throw new InvalidOperationException("El JSON requiere revision manual: no se puede inferir informacion faltante.");
            var repaired=JsonNode.Parse(text+new string(stack.ToArray()))!.AsArray();
            return(repaired,true);
        }
    }
    public static IEnumerable<JsonObject> Nodes(JsonArray menu)
    {
        foreach(var node in menu.OfType<JsonObject>()){yield return node;if(node["children"] is JsonArray children)foreach(var child in Nodes(children))yield return child;}
    }
    public static void Ensure(IReadOnlyList<JsonArray> menus)
    {
        if(menus.Count==0)throw new ArgumentException("Se requiere al menos un arreglo de menu.");
        var all=menus.SelectMany(Nodes).ToList();
        var missing=Paths.Where(path=>!all.Any(x=>(x["path"]?.GetValue<string>()??"").TrimEnd('/')==path)).ToList();
        if(missing.Count==0)return;
        var group=all.FirstOrDefault(x=>string.Equals(x["title"]?.GetValue<string>(),"Soporte",StringComparison.OrdinalIgnoreCase));
        if(group is null)
        {
            var system=menus.SelectMany(x=>x.OfType<JsonObject>()).FirstOrDefault(x=>string.Equals(x["title"]?.GetValue<string>(),"Sistema",StringComparison.OrdinalIgnoreCase));
            if(system is null){system=new JsonObject{["title"]="Sistema",["icon"]="mdi:shield-account-outline",["children"]=new JsonArray()};menus[0].Add(system);}
            if(system["children"] is not JsonArray)system["children"]=new JsonArray();
            group=new JsonObject{["title"]="Soporte",["children"]=new JsonArray()};system["children"]!.AsArray().Add(group);
        }
        if(group["children"] is not JsonArray)group["children"]=new JsonArray();
        foreach(var path in missing)group["children"]!.AsArray().Add(new JsonObject {
            ["title"]=path==Paths[0]?"Tickets":"Notificaciones",["path"]=path,
            ["permissions"]=new JsonArray((path==Paths[0]?Permissions:[Permissions[1]]).Select(x=>(JsonNode?)JsonValue.Create(x)).ToArray()) });
    }
}
