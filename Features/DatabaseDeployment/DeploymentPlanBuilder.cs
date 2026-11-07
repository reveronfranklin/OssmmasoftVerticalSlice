using System.Text;

namespace OssmmasoftVerticalSlice.Features.DatabaseDeployment;

internal static class DeploymentPlanBuilder
{
    internal static DeploymentPlan Build(DeploymentRecord record,List<string> selected,string version)
    {
        if(record.Status!="COMPLETO" || selected.Count==0 || selected.Count>500 || selected.Distinct().Count()!=selected.Count)
            throw new InvalidOperationException("Seleccione entre 1 y 500 diferencias de una comparacion completa.");
        var source=record.Source.ToDictionary(x=>x.Key);var target=record.Target.ToDictionary(x=>x.Key);
        var differences=record.Differences.ToDictionary(x=>x.Key);
        foreach(var key in selected)
            if(!differences.TryGetValue(key,out var diff) || diff.Estado is "IGUAL" or "SOLO_PRODUCCION" ||
                !source.TryGetValue(key,out var obj) || DeploymentRules.Excluded.Contains(obj.Owner) || string.IsNullOrWhiteSpace(obj.Ddl) || obj.Status.StartsWith("NO_ACCESIBLE"))
                throw new InvalidOperationException("La seleccion contiene objetos no publicables o excluidos.");
        var ordered=new List<string>();var state=new Dictionary<string,int>();var cycle=new HashSet<string>();
        void Visit(string key)
        {
            if(state.TryGetValue(key,out var flag)){if(flag==1)cycle.Add(key);return;}
            state[key]=1;
            foreach(var dep in source[key].Dependencies)
            {
                var dependency=dep.Owner+"|"+dep.Type+"|"+dep.Name;
                if(selected.Contains(dependency) && dependency!=key)Visit(dependency);
            }
            if(source[key].Type=="PACKAGE BODY")
            {var spec=source[key].Owner+"|PACKAGE|"+source[key].Name;if(selected.Contains(spec))Visit(spec);}
            state[key]=2;ordered.Add(key);
        }
        foreach(var key in selected.Order(StringComparer.Ordinal))Visit(key);
        var items=new List<DeploymentPlanItem>();
        foreach(var key in ordered)
        {
            var obj=source[key];var manual=false;var reasons=new List<string>();
            if(Encoding.UTF8.GetByteCount(obj.Owner)>30 || Encoding.UTF8.GetByteCount(obj.Name)>30)
            {manual=true;reasons.Add("Identificador supera 30 bytes; revisar compatibilidad con Oracle 10g.");}
            if(!obj.Status.StartsWith("VALID",StringComparison.Ordinal)){manual=true;reasons.Add("Objeto de pruebas invalido.");}
            if(cycle.Count>0){manual=true;reasons.Add("La seleccion contiene dependencias ciclicas; revisar el orden completo.");}
            if(obj.Dependencies.Any(d=>DeploymentRules.Excluded.Contains(d.Owner) || d.Remote))
            {manual=true;reasons.Add("Dependencia de esquema excluido o enlace remoto; no se consulta ni modifica su destino.");}
            foreach(var dep in obj.Dependencies.Where(d=>DeploymentRules.KnownSchemas.Contains(d.Owner)))
            {
                var depKey=dep.Owner+"|"+dep.Type+"|"+dep.Name;
                if(depKey==key || selected.Contains(depKey))continue;
                if(!target.TryGetValue(depKey,out var available) || !available.Status.StartsWith("VALID",StringComparison.Ordinal))
                {manual=true;reasons.Add("Dependencia fuera de la comparacion, no seleccionada o no valida: "+dep.Owner+"."+dep.Name+" ("+dep.Type+").");}
            }
            var sql=obj.Ddl.Trim();var action=differences[key].Estado=="NUEVO"?"CREAR":"REEMPLAZAR";
            if(obj.Type=="TABLE")
            {
                manual=true;
                if(target.TryGetValue(key,out var previous))
                {
                    action="REVISAR_ESTRUCTURA";
                    var additions=obj.Columns.Where(c=>!previous.Columns.Any(p=>p.Name==c.Name)).ToList();
                    var safe=additions.Where(c=>c.Nullable && string.IsNullOrWhiteSpace(c.Default) && ColumnType(c) is not null).ToList();
                    sql=string.Join('\n',safe.Select(c=>"ALTER TABLE "+DeploymentRules.Quote(obj.Owner)+"."+DeploymentRules.Quote(obj.Name)+" ADD ("+DeploymentRules.Quote(c.Name)+" "+ColumnType(c)+");"));
                    reasons.Add(safe.Count>0?"Candidatas: columnas nuevas anulables sin default. Las demas diferencias no se convierten automaticamente en ALTER.":"Cambio estructural requiere migracion manual; revisar ambas definiciones.");
                }
                else reasons.Add("Creacion de tabla: revisar claves, referencias, defaults y dependencias antes de ejecutar fuera de la aplicacion.");
            }
            else if(obj.Type is "TRIGGER" or "INDEX" or "GRANT" or "SEQUENCE" or "SYNONYM")
            {
                manual=true;reasons.Add("Requiere revision especifica de efectos, permisos o definicion fisica.");
                if(obj.Type=="SEQUENCE" && action=="REEMPLAZAR") { sql="";reasons.Add("No se genera reinicio ni reemplazo de secuencias existentes de produccion."); }
                if(obj.Type=="INDEX" && action=="REEMPLAZAR") { sql="";reasons.Add("No se genera DROP/CREATE automatico de indices existentes."); }
            }
            if(obj.Type is "PACKAGE" or "PACKAGE BODY" or "PROCEDURE" or "FUNCTION" or "VIEW")
            {
                if(!sql.StartsWith("CREATE",StringComparison.OrdinalIgnoreCase)) {manual=true;reasons.Add("DDL no reconocido como definicion CREATE.");}
            }
            items.Add(new(key,action,manual,string.Join(' ',reasons.Distinct()),sql));
        }
        var script=new StringBuilder("-- R54: plan de revision. La aplicacion NO ejecuta este archivo.\n-- Referencia: pruebas -> produccion. Revisar destinos, dependencias y respaldos.\n-- Los candidatos MANUALES se incluyen comentados; no representan una migracion completa.\n");
        foreach(var id in record.TargetIdentities)script.AppendLine("-- Produccion: DBID="+id.DatabaseId+"; esquema="+id.Schema);
        foreach(var item in items)
        {
            script.AppendLine("\n-- "+item.Key+"; "+item.Action+(item.Manual?"; MANUAL":"; REVISAR"));
            if(item.Reason.Length>0)script.AppendLine("-- "+item.Reason.Replace('\n',' '));
            if(item.Manual)foreach(var line in item.Sql.Split('\n'))script.AppendLine("-- "+line);
            else
            {
                script.AppendLine(item.Sql);
                var type=source[item.Key].Type;
                if(type is "PACKAGE" or "PACKAGE BODY" or "PROCEDURE" or "FUNCTION")script.AppendLine("/");
                else if(!item.Sql.TrimEnd().EndsWith(';'))script.AppendLine(";");
            }
        }
        var value=script.ToString();var hash=DeploymentRules.Hash(version+"|"+record.RequestHash+"|"+string.Join('|',record.Source.Select(o=>o.Hash))+"|"+string.Join('|',record.Target.Select(o=>o.Hash))+"|"+value);
        return new(Guid.NewGuid().ToString("D"),record.Id,hash,DateTime.UtcNow,items,value,version);
    }
    private static string? ColumnType(DeploymentColumn c) => c.Type switch {
        "VARCHAR2" or "CHAR" => c.Type+"("+(c.CharUsed=="C"?c.CharLength:c.Length)+(c.CharUsed=="C"?" CHAR":" BYTE")+")",
        "NVARCHAR2" or "NCHAR" => c.Type+"("+c.CharLength+")",
        "NUMBER" => c.Precision is null ? c.Scale is null?"NUMBER":"NUMBER(*,"+c.Scale+")" : "NUMBER("+c.Precision+(c.Scale is null?"":","+c.Scale)+")",
        "DATE" or "CLOB" or "NCLOB" or "BLOB" or "BINARY_FLOAT" or "BINARY_DOUBLE" => c.Type,
        "RAW" => "RAW("+c.Length+")", _=>null };
}
