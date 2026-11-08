using System.Data;
using Microsoft.Extensions.Logging;
using Oracle.ManagedDataAccess.Client;

namespace OssmmasoftVerticalSlice.Features.DatabaseDeployment;

public sealed class DeploymentOptions
{
    public bool Enabled { get; set; }
    public bool ExecutionEnabled { get; set; } // Phase 1 never uses an execution identity, regardless of this flag.
    public string StoragePath { get; set; } = "";
    public string Version { get; set; } = "";
    public string BuildVersion()
    {
        if(!string.IsNullOrWhiteSpace(Version))return Version;
        var file=Path.Combine(AppContext.BaseDirectory,"DEPLOY_COMMIT");
        if(File.Exists(file))
        {
            var value=File.ReadAllText(file).Trim();
            if(value.Length is >=7 and <=40 && value.All(char.IsAsciiHexDigit))return value;
        }
        return typeof(DeploymentOptions).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute),false).OfType<System.Reflection.AssemblyInformationalVersionAttribute>().FirstOrDefault()?.InformationalVersion ?? "Sin version";
    }
    public int MaxObjects { get; set; } = 3000;
    public int MaxDefinitionChars { get; set; } = 1000000;
    public int MaxTotalDefinitionChars { get; set; } = 32000000;
    public List<string> AllowedSchemas { get; set; } = ["RH", "SIS", "CNT", "ADM", "PRE", "RM", "CAT", "BM", "MFO"];
    public DeploymentProfile Pruebas { get; set; } = new();
    public DeploymentProfile ProduccionLectura { get; set; } = new();
    public static DeploymentOptions Read(IConfiguration config)
    {
        var result=config.GetSection("DatabaseDeployment").Get<DeploymentOptions>() ?? new();
        var path=config["DatabaseDeployment:PrivateConfigPath"];
        if(!string.IsNullOrWhiteSpace(path))
        {
            if(!Path.IsPathFullyQualified(path))throw new InvalidOperationException("PrivateConfigPath debe ser una ruta absoluta del servidor.");
            // Bind only this feature; never merge environment files into business configuration.
            var privateConfig=new ConfigurationBuilder().AddJsonFile(path,false,false).Build();
            privateConfig.GetSection("DatabaseDeployment").Bind(result);
        }
        return result;
    }
}
public sealed class DeploymentProfile
{
    public string ExpectedDatabaseId { get; set; } = "";
    public Dictionary<string,string> ExpectedDatabaseIds { get; set; } = new();
    public string Expected(string schema) => ExpectedDatabaseIds.GetValueOrDefault(schema,ExpectedDatabaseId).Trim();
    public Dictionary<string,string> ConnectionStrings { get; set; } = new();
    public override string ToString() => "Perfil privado de metadatos";
}
public class DeploymentMetadata(IConfiguration config, ILogger<DeploymentMetadata>? logger = null)
{
    private static OracleCommand Query(OracleConnection cn, string text, string? owner = null)
    {
        var cmd = new OracleCommand(text, cn) { BindByName = true, CommandTimeout = 30, InitialLONGFetchSize = -1 };
        if (owner is not null) cmd.Parameters.Add("owner", OracleDbType.Varchar2).Value = owner;
        return cmd;
    }
    // No CLOB locator crosses the wire: Oracle returns only bounded VARCHAR2 and numbers.
    // Re-evaluate metadata per fragment; fail if its length changes during capture.
    private static async Task<string> ReadDefinition(OracleConnection cn,string schema,string type,string name,int limit,CancellationToken ct)
    {
        const string sql="""
            DECLARE
              v_ddl CLOB;
              v_part VARCHAR2(4000);
            BEGIN
              v_ddl:=DBMS_METADATA.GET_DDL(:type,:name,:owner);
              :total:=NVL(DBMS_LOB.GETLENGTH(v_ddl),0);
              IF :total<=:max_chars THEN
                v_part:=DBMS_LOB.SUBSTR(v_ddl,1000,:offset_chars);
                :part:=v_part;
                :next_chars:=:offset_chars+NVL(LENGTH(v_part),0);
              ELSE
                :part:=NULL;
                :next_chars:=:offset_chars;
              END IF;
              IF DBMS_LOB.ISTEMPORARY(v_ddl)=1 THEN DBMS_LOB.FREETEMPORARY(v_ddl); END IF;
            EXCEPTION WHEN OTHERS THEN
              IF DBMS_LOB.ISTEMPORARY(v_ddl)=1 THEN DBMS_LOB.FREETEMPORARY(v_ddl); END IF;
              RAISE;
            END;
            """;
        var text=new System.Text.StringBuilder();var offset=1;int? length=null;
        while(true)
        {
            ct.ThrowIfCancellationRequested();
            using var cmd=Query(cn,sql,schema);
            cmd.Parameters.Add("type",OracleDbType.Varchar2).Value=type;
            cmd.Parameters.Add("name",OracleDbType.Varchar2).Value=name;
            cmd.Parameters.Add("max_chars",OracleDbType.Int32).Value=limit;
            cmd.Parameters.Add("offset_chars",OracleDbType.Int32).Value=offset;
            var total=cmd.Parameters.Add("total",OracleDbType.Int32,ParameterDirection.Output);
            var next=cmd.Parameters.Add("next_chars",OracleDbType.Int32,ParameterDirection.Output);
            var part=cmd.Parameters.Add("part",OracleDbType.Varchar2,4000);part.Direction=ParameterDirection.Output;
            await cmd.ExecuteNonQueryAsync(ct);
            var size=int.Parse(total.Value.ToString()!,System.Globalization.CultureInfo.InvariantCulture);
            if(size>limit)throw new InvalidOperationException("Definicion demasiado grande. Compare menos objetos.");
            if(length is not null && length!=size)throw new InvalidOperationException("La definicion cambio durante su lectura. Inicie otra comparacion.");
            length=size;
            if(size==0)throw new InvalidOperationException("Definicion no disponible.");
            var value=part.Value;
            var chunk=value is Oracle.ManagedDataAccess.Types.OracleString os?(os.IsNull?"":os.Value):value?.ToString()??"";
            var following=int.Parse(next.Value.ToString()!,System.Globalization.CultureInfo.InvariantCulture);
            if(chunk.Length==0 || following<=offset || following>size+1)throw new InvalidOperationException("Lectura incompleta de la definicion. Inicie otra comparacion.");
            text.Append(chunk);offset=following;
            if(offset>size)break;
        }
        return text.ToString();
    }
    private static string Text(OracleDataReader r,string name) => r.IsDBNull(r.GetOrdinal(name)) ? "" : r.GetValue(r.GetOrdinal(name)).ToString() ?? "";
    private static int Num(OracleDataReader r,string name) => r.IsDBNull(r.GetOrdinal(name)) ? 0 : Convert.ToInt32(r.GetValue(r.GetOrdinal(name)));
    private static int? Opt(OracleDataReader r,string name) => r.IsDBNull(r.GetOrdinal(name)) ? null : Num(r,name);
    private static void ValidateSchema(DeploymentOptions options, string schema)
    {
        if(!DeploymentRules.SchemaAllowed(schema,options.AllowedSchemas)) throw new InvalidOperationException("Esquema excluido o no autorizado.");
    }
    private static async Task<DeploymentIdentity> Identity(OracleConnection cn,string schema,CancellationToken ct)
    {
        using var q=Query(cn,"SELECT TO_CHAR(DBID) DATABASE_ID, NAME DATABASE_NAME FROM V$DATABASE");
        using var r=await q.ExecuteReaderAsync(ct);
        if(!await r.ReadAsync(ct)) throw new InvalidOperationException("No se pudo identificar la base.");
        return new(Text(r,"DATABASE_ID"),Text(r,"DATABASE_NAME"),schema);
    }
    private static async Task<string> Visibility(OracleConnection cn,string schema,CancellationToken ct)
    {
        using(var q=Query(cn,"SELECT SYS_CONTEXT('USERENV','SESSION_USER') FROM DUAL"))
            if(string.Equals((await q.ExecuteScalarAsync(ct))?.ToString(),schema,StringComparison.Ordinal)) return "ALL_";
        using var rights=Query(cn,"SELECT (SELECT COUNT(*) FROM SESSION_ROLES WHERE ROLE='SELECT_CATALOG_ROLE') + (SELECT COUNT(*) FROM SESSION_PRIVS WHERE PRIVILEGE='SELECT ANY DICTIONARY') FROM DUAL");
        if(Convert.ToInt32(await rights.ExecuteScalarAsync(ct))>0) return "DBA_";
        throw new InvalidOperationException("La cuenta no permite inventariar completamente el esquema. Configure acceso a metadatos con el DBA.");
    }
    private async Task<(OracleConnection Connection,DeploymentIdentity Id,string Prefix)> Open(string schema,bool source,bool requireExpected,CancellationToken ct)
    {
        var options=DeploymentOptions.Read(config); ValidateSchema(options,schema);
        var profile=source?options.Pruebas:options.ProduccionLectura;
        if(!profile.ConnectionStrings.TryGetValue(schema,out var value) || string.IsNullOrWhiteSpace(value)) throw new InvalidOperationException("Perfil de lectura no configurado.");
        var cn=new OracleConnection(value);
        var stage="abrir conexion";
        try
        {
            await cn.OpenAsync(ct);
            stage="consultar identidad V$DATABASE";
            var identity=await Identity(cn,schema,ct);
            if(requireExpected && (string.IsNullOrWhiteSpace(profile.Expected(schema)) || identity.DatabaseId!=profile.Expected(schema)))
                throw new InvalidOperationException("Configure o corrija ExpectedDatabaseId con la identidad verificada del ambiente.");
            stage="verificar usuario y visibilidad del diccionario";
            var prefix=await Visibility(cn,schema,ct);
            stage="consultar inventario "+prefix+"OBJECTS";
            using(var owner=Query(cn,$"SELECT COUNT(*) FROM {prefix}OBJECTS WHERE OWNER=:owner",schema))
                if(Convert.ToInt32(await owner.ExecuteScalarAsync(ct))==0) throw new InvalidOperationException("Esquema sin objetos visibles; no se interpretara como una base vacia.");
            return (cn,identity,prefix);
        }
        catch(OracleException ex)
        {
            cn.Dispose();
            var reference=Guid.NewGuid().ToString("N");
            // Do not log connection strings, exception messages or parameter values.
            logger?.LogError("Comparador Oracle {Reference}; esquema {Schema}; origen {Source}; etapa {Stage}; codigo {Code}; tipo {Type}; pila {Stack}; causa {InnerType}; pila causa {InnerStack}",
                reference,schema,source,stage,ex.Number,ex.GetType().FullName,ex.StackTrace,ex.InnerException?.GetType().FullName,ex.InnerException?.StackTrace);
            var reason=ex.Number switch
            {
                1017 => "Credenciales rechazadas. Revise usuario y clave del perfil de este ambiente.",
                942 or 1031 => "Objeto no visible o permiso insuficiente en esa operacion; verifique la cuenta y base efectivas.",
                50000 => "Fallo del proveedor Oracle; este codigo no demuestra falta de permisos.",
                _ => "Fallo de Oracle en esta operacion."
            };
            var protocol=ex.Message.Contains("TTC",StringComparison.OrdinalIgnoreCase)?" Se detecto error de protocolo TTC.":"";
            throw new InvalidOperationException($"Fallo al {stage} (ORA-{ex.Number:D5}). {reason}{protocol} Referencia: {reference}",ex);
        }
        catch { cn.Dispose(); throw; }
    }
    public async Task<DeploymentConnectionResult> Check(string schema,bool source,CancellationToken ct)
    {
        var label=source?"Pruebas":"Produccion"; DeploymentIdentity? identity=null;
        try
        {
            var opened=await Open(schema,source,false,ct); using var cn=opened.Connection; identity=opened.Id;
            var profile=source?DeploymentOptions.Read(config).Pruebas:DeploymentOptions.Read(config).ProduccionLectura;
            return new(schema,label,true,identity.DatabaseId==profile.Expected(schema),identity,
                string.IsNullOrWhiteSpace(profile.Expected(schema))?"Identidad descubierta. Configure ExpectedDatabaseId antes de comparar.":
                identity.DatabaseId!=profile.Expected(schema)?"La identidad no coincide con ExpectedDatabaseId. Revise el destino antes de comparar.":"Identidad y acceso al inventario verificados.");
        }
        catch(OracleException ex) { return new(schema,label,false,false,identity,$"Fallo de Oracle (ORA-{ex.Number:D5}); no implica necesariamente falta de permisos."); }
        catch(InvalidOperationException ex) { return new(schema,label,false,false,identity,ex.Message); }
    }
    public async Task<(DeploymentIdentity Identity,List<DeploymentObject> Objects)> Capture(string schema,bool source,List<string> types,CancellationToken ct)
    {
        var stage="validar configuracion";
        try { return await CaptureCore(schema,source,types,ct,value=>stage=value); }
        catch(Exception ex) when (ex is not OperationCanceledException && ex is not InvalidOperationException)
        {
            var reference=Guid.NewGuid().ToString("N");
            logger?.LogError("Captura {Reference}; esquema {Schema}; origen {Source}; etapa {Stage}; tipo {Type}; pila {Stack}; causa {InnerType}; pila causa {InnerStack}",
                reference,schema,source,stage,ex.GetType().FullName,ex.StackTrace,ex.InnerException?.GetType().FullName,ex.InnerException?.StackTrace);
            var detail=ex is OracleException oracle?$"ORA-{oracle.Number:D5}":ex.GetType().Name;
            var protocol=ex.Message.Contains("TTC",StringComparison.OrdinalIgnoreCase)?" Error de protocolo TTC.":"";
            throw new InvalidOperationException($"Fallo al capturar {(source?"Pruebas":"Produccion")}/{schema}: {stage} ({detail}).{protocol} Referencia: {reference}",ex);
        }
    }
    private async Task<(DeploymentIdentity Identity,List<DeploymentObject> Objects)> CaptureCore(string schema,bool source,List<string> types,CancellationToken ct,Action<string> stage)
    {
        var options=DeploymentOptions.Read(config); ValidateSchema(options,schema);
        if(types.Count==0 || types.Any(x=>!DeploymentRules.KnownTypes.Contains(x))) throw new InvalidOperationException("Tipo de objeto no permitido.");
        stage("abrir conexion e identificar destino");
        var opened=await Open(schema,source,true,ct); using var cn=opened.Connection; var prefix=opened.Prefix;
        stage("configurar transformaciones DBMS_METADATA");
        using(var transforms=Query(cn,"BEGIN DBMS_METADATA.SET_TRANSFORM_PARAM(DBMS_METADATA.SESSION_TRANSFORM,'SEGMENT_ATTRIBUTES',FALSE); DBMS_METADATA.SET_TRANSFORM_PARAM(DBMS_METADATA.SESSION_TRANSFORM,'STORAGE',FALSE); DBMS_METADATA.SET_TRANSFORM_PARAM(DBMS_METADATA.SESSION_TRANSFORM,'SQLTERMINATOR',TRUE); END;")) await transforms.ExecuteNonQueryAsync(ct);
        stage("leer inventario de objetos");
        var inventory=new List<(string Name,string Type,string Status)>();
        using(var q=Query(cn,$"SELECT OBJECT_NAME,OBJECT_TYPE,STATUS FROM {prefix}OBJECTS WHERE OWNER=:owner AND SUBOBJECT_NAME IS NULL AND GENERATED='N' ORDER BY OBJECT_TYPE,OBJECT_NAME",schema))
        using(var r=await q.ExecuteReaderAsync(ct)) while(await r.ReadAsync(ct))
            if(types.Contains(Text(r,"OBJECT_TYPE"))) { inventory.Add((Text(r,"OBJECT_NAME"),Text(r,"OBJECT_TYPE"),Text(r,"STATUS"))); if(inventory.Count>Math.Clamp(options.MaxObjects,1,5000)) throw new InvalidOperationException("Inventario excede MaxObjects. Compare menos tipos de objetos."); }
        stage("leer dependencias");
        var deps=new Dictionary<string,List<DeploymentDependency>>();
        using(var q=Query(cn,$"SELECT NAME,TYPE,REFERENCED_OWNER,REFERENCED_NAME,REFERENCED_TYPE,REFERENCED_LINK_NAME FROM {prefix}DEPENDENCIES WHERE OWNER=:owner",schema))
        using(var r=await q.ExecuteReaderAsync(ct)) while(await r.ReadAsync(ct))
        {
            var key=Text(r,"TYPE")+"|"+Text(r,"NAME"); if(!deps.TryGetValue(key,out var list)) deps[key]=list=[];
            list.Add(new(Text(r,"REFERENCED_OWNER"),Text(r,"REFERENCED_NAME"),Text(r,"REFERENCED_TYPE"),Text(r,"REFERENCED_LINK_NAME").Length>0));
        }
        stage("leer columnas");
        var columns=new Dictionary<string,List<DeploymentColumn>>();
        if(types.Contains("TABLE"))
        {
            using var q=Query(cn,$"SELECT TABLE_NAME,COLUMN_NAME,DATA_TYPE,DATA_LENGTH,DATA_PRECISION,DATA_SCALE,CHAR_USED,CHAR_LENGTH,NULLABLE,DATA_DEFAULT FROM {prefix}TAB_COLUMNS WHERE OWNER=:owner ORDER BY TABLE_NAME,COLUMN_ID",schema);
            using var r=await q.ExecuteReaderAsync(ct); while(await r.ReadAsync(ct))
            {
                var name=Text(r,"TABLE_NAME"); if(!columns.TryGetValue(name,out var list)) columns[name]=list=[];
                list.Add(new(Text(r,"COLUMN_NAME"),Text(r,"DATA_TYPE"),Num(r,"DATA_LENGTH"),Opt(r,"DATA_PRECISION"),Opt(r,"DATA_SCALE"),Text(r,"CHAR_USED"),Num(r,"CHAR_LENGTH"),Text(r,"NULLABLE")=="Y",Text(r,"DATA_DEFAULT").Trim()));
            }
        }
        stage("leer estado de triggers");
        var triggers=new Dictionary<string,string>();
        if(types.Contains("TRIGGER"))
        {
            using var q=Query(cn,$"SELECT TRIGGER_NAME,STATUS FROM {prefix}TRIGGERS WHERE OWNER=:owner",schema);
            using var r=await q.ExecuteReaderAsync(ct); while(await r.ReadAsync(ct)) triggers[Text(r,"TRIGGER_NAME")]=Text(r,"STATUS");
        }
        var result=new List<DeploymentObject>(); var total=0;
        foreach(var item in inventory)
        {
            stage("leer definicion "+item.Type+" "+item.Name);
            ct.ThrowIfCancellationRequested(); var type=item.Type switch { "PACKAGE"=>"PACKAGE_SPEC", "PACKAGE BODY"=>"PACKAGE_BODY", var t=>t }; string ddl; var status=item.Status;
            try
            {
                ddl=await ReadDefinition(cn,schema,type,item.Name,Math.Clamp(options.MaxDefinitionChars,1000,2000000),ct);
            }
            catch(OracleException ex) { ddl="";status=$"NO_ACCESIBLE_ORA_{ex.Number:D5}"; }
            total+=ddl.Length; if(total>Math.Clamp(options.MaxTotalDefinitionChars,10000,32000000)) throw new InvalidOperationException("Metadatos exceden el limite configurado. Compare menos tipos.");
            if(item.Type=="TRIGGER" && triggers.TryGetValue(item.Name,out var enabled)) status+="/"+enabled;
            result.Add(new(schema,item.Type,item.Name,status,ddl,deps.GetValueOrDefault(item.Type+"|"+item.Name,[]),columns.GetValueOrDefault(item.Name,[])));
        }
        if(types.Contains("GRANT"))
        {
            stage("leer permisos de objetos");
            using var q=Query(cn,$"SELECT TABLE_NAME,GRANTEE,PRIVILEGE,GRANTABLE FROM {prefix}TAB_PRIVS WHERE "+(prefix=="ALL_"?"TABLE_SCHEMA":"OWNER")+"=:owner ORDER BY TABLE_NAME,GRANTEE,PRIVILEGE",schema);
            using var r=await q.ExecuteReaderAsync(ct); var grouped=new Dictionary<string,List<string>>();
            while(await r.ReadAsync(ct))
            {
                var name=Text(r,"TABLE_NAME")+" -> "+Text(r,"GRANTEE"); if(!grouped.TryGetValue(name,out var list))grouped[name]=list=[];
                list.Add("GRANT "+Text(r,"PRIVILEGE")+" ON "+DeploymentRules.Quote(schema)+"."+DeploymentRules.Quote(Text(r,"TABLE_NAME"))+" TO "+DeploymentRules.Quote(Text(r,"GRANTEE"))+(Text(r,"GRANTABLE")=="YES"?" WITH GRANT OPTION":"")+";");
            }
            foreach(var (name,list) in grouped) result.Add(new(schema,"GRANT",name,"VALID",string.Join('\n',list),[],[]));
        }
        if(result.Count>Math.Clamp(options.MaxObjects,1,5000) || result.Sum(x=>x.Ddl.Length)>Math.Clamp(options.MaxTotalDefinitionChars,10000,32000000))
            throw new InvalidOperationException("Inventario o definiciones exceden los limites configurados.");
        return (opened.Id,result);
    }
}
