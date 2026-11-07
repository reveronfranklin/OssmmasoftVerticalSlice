using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Features.Support;
using OssmmasoftVerticalSlice.Helpers;
using System.Text.Json;
using System.Text.Json.Nodes;
using static OssmmasoftVerticalSlice.Features.SisSeguridad.SisMenuDb;

namespace OssmmasoftVerticalSlice.Features.SisSeguridad;

public record SisMenuRoleSave(int CodigoRol, int CodigoMod, string Clave, string Nombre, string Descripcion, bool Activo, List<int> Menus, int VersionEsperada, List<int>? Permisos = null);
public record SisMenuNodeSave(int CodigoMenu, int CodigoMod, int? CodigoPadre, string Titulo, string Path, string Icono, int Orden, bool Activo, int VersionEsperada);
public record SisMenuUserSave(int CodigoUsuario, List<int> Roles, int VersionEsperada);
public record SisMenuMigration(int CodigoUsuario, string HashOrigen = "", int VersionEsperada = 0);
public record SisMenuHistoryQuery(int CodigoUsuario, int Page = 1, int PageSize = 10);
public record SisMenuHistoryRow(int CodigoMigracion, string HashOrigen, string Fecha, int UsuarioActor, int Version);
public record SisMenuMode(int CodigoUsuario, bool Vertical, int VersionEsperada);
public record SisMenuSnapshot(int Version, List<SisMenuNode> Menus, List<SisMenuRole> Roles, List<SisMenuGrant> Asignaciones,
    List<SisMenuPerm> Permisos, List<SisMenuRolePerm> RolPermisos, List<SisMenuNodePerm> MenuPermisos, Dictionary<int,int> UsuariosPorRol, List<SisSegModuloResponse> Modulos);
public record SisMenuSession(int CodigoUsuario, string Estrategia, int Version, JsonArray Menu, List<SisMenuOrigin> Origenes, List<int> Roles);
public record SisMenuAnalysis(int CodigoUsuario, int Version, string HashOrigen, bool PuedeMigrar, string Mensaje, JsonArray MenuAntes, int Opciones, List<string> Permisos);

public partial class SisSeguridadController
{
    // These endpoints resolve identity from the signed JWT, never from a client supplied login or refresh token.
    private async Task<ResultDto<int>> SimpleIdentity(bool admin)
    {
        try
        {
            if (!SupportDb.TryGetEmpresa(config, out var company, out var error) || company <= 0) return SimpleFail<int>(error);
            using var cn = connectionDB.GetSisConnection(); await cn.OpenAsync();
            var id = await SupportSecurity.GetAuthenticatedUserIdAsync(cn, User, company, null);
            if (id <= 0) return SimpleFail<int>("Sesion no valida.");
            if (admin)
            {
                var user = await SisSeguridadHandler.ReadUsuarioForAuthAsync(cn, id, company);
                var perms = await SupportSecurity.GetPermissionsAsync(cn, id, company);
                if (user?.IsSuperuser != true && !SupportSecurity.HasAny(perms, SupportSecurity.SupportUsersConfig))
                    return SimpleFail<int>("No tiene permiso para administrar seguridad.");
            }
            return new(id) { IsValid = true };
        }
        catch (Exception ex) { return SimpleFail<int>($"No se pudo validar la sesion: {ex.Message}"); }
    }
    private static ResultDto<T> SimpleFail<T>(string message) => new(default!) { IsValid = false, Message = message };
    private async Task<IActionResult> SimpleRun<T>(bool admin, Func<SisMenuSimpleHandler, int, Task<ResultDto<T>>> run)
    {
        var access = await SimpleIdentity(admin);
        return Ok(access.IsValid ? await run(new(connectionDB, config), access.Data) : SimpleFail<T>(access.Message));
    }
    [HttpPost("getMenuSimple")] public Task<IActionResult> SimpleCatalog() => SimpleRun(true, (h, _) => h.Catalog());
    [HttpPost("getMiMenu")] public Task<IActionResult> MyMenu() => SimpleRun(false, (h, id) => h.Session(id));
    [HttpPost("simularMenuUsuario")] public Task<IActionResult> SimpleSimulate(SisSegUsuarioQuery value) => SimpleRun(true, (h, _) => h.Session(value.CodigoUsuario));
    [HttpPost("saveOpcionSimple")] public Task<IActionResult> SimpleSaveNode(SisMenuNodeSave value) => SimpleRun(true, (h, id) => h.SaveNode(value,id));
    [HttpPost("saveMenuRolSimple")] public Task<IActionResult> SimpleSaveRole(SisMenuRoleSave value) => SimpleRun(true, (h, id) => h.SaveRole(value, id));
    [HttpPost("saveRolesUsuarioSimple")] public Task<IActionResult> SimpleSaveUser(SisMenuUserSave value) => SimpleRun(true, (h, id) => h.SaveUser(value, id));
    [HttpPost("analizarMigracionMenu")] public Task<IActionResult> SimpleAnalyze(SisMenuMigration value) => SimpleRun(true, (h, _) => h.Analyze(value.CodigoUsuario));
    [HttpPost("aplicarMigracionMenu")] public Task<IActionResult> SimpleMigrate(SisMenuMigration value) => SimpleRun(true, (h, id) => h.Migrate(value, id));
    [HttpPost("getMigracionMenu")] public Task<IActionResult> SimpleHistory(SisMenuHistoryQuery value) => SimpleRun(true, (h, _) => h.History(value));
    [HttpPost("setEstrategiaMenu")] public Task<IActionResult> SimpleMode(SisMenuMode value) => SimpleRun(true, (h, id) => h.SetMode(value, id));
}

public sealed class SisMenuSimpleHandler(ConnectionDB connectionDB, IConfiguration config)
{
    private static ResultDto<T> Ok<T>(T data) => new(data) { IsValid = true, Message = "OK" };
    private async Task<ResultDto<T>> Run<T>(Func<SisMenuDb, OracleConnection, int, Task<T>> action, int? expected = null, bool write = false)
    {
        if (!SupportDb.TryGetEmpresa(config, out var company, out var error) || company <= 0)
            return new(default!) { IsValid = false, Message = error.Length > 0 ? error : "Empresa invalida." };
        using var cn = connectionDB.GetSisConnection();
        try { await cn.OpenAsync(); }
        catch (Exception ex) { return new(default!) { IsValid = false, Message = $"No se pudo conectar con SIS: {ex.Message}" }; }
        using var tx = cn.BeginTransaction();
        try
        {
            var db = new SisMenuDb(cn, tx);
            await db.Lock(expected); // Consistent catalog/preview and serialized writes; no read cache beyond a transaction.
            var result = await action(db, cn, company);
            await db.Execute("RESET_WRITE");
            if (write) tx.Commit(); else tx.Rollback();
            return Ok(result);
        }
        catch (Exception ex)
        {
            try { await new SisMenuDb(cn, tx).Execute("RESET_WRITE"); } catch { /* Preserve original failure. */ }
            tx.Rollback();
            return new(default!) { IsValid = false, Message = ex is OracleException ? $"Operacion SIS: {ex.Message}" : ex.Message };
        }
    }
    private static async Task<SisMenuSnapshot> Snapshot(SisMenuDb db)
    {
        var mods = (await db.Read("MODS")).Where(x => Number(x,"ACTIVO") == 1).Select(x => Number(x,"CODIGO_MOD")).ToHashSet();
        var menus = (await db.Read("MENUS")).Select(x => new SisMenuNode(Number(x,"CODIGO_MENU"),Number(x,"CODIGO_MOD"),
            Number(x,"CODIGO_PADRE") is >0 and var p ? p : null,Text(x,"TITULO"),Text(x,"PATH"),Text(x,"ICONO"),Number(x,"ORDEN"),Number(x,"ACTIVO")==1 && mods.Contains(Number(x,"CODIGO_MOD")))).ToList();
        var roles = (await db.Read("ROLES")).Select(x => new SisMenuRole(Number(x,"CODIGO_ROL"),Number(x,"CODIGO_MOD"),Text(x,"CLAVE"),Text(x,"NOMBRE"),Text(x,"DESCRIPCION"),Number(x,"ACTIVO")==1 && mods.Contains(Number(x,"CODIGO_MOD")))).ToList();
        return new(Number((await db.Read("REV"))[0],"VERSION"),menus,roles,
            (await db.Read("GRANTS")).Select(x=>new SisMenuGrant(Number(x,"CODIGO_ROL"),Number(x,"CODIGO_MENU"))).ToList(),
            (await db.Read("PERMS")).Select(x=>new SisMenuPerm(Number(x,"CODIGO_PERM"),Text(x,"CLAVE"))).ToList(),
            (await db.Read("RPERMS")).Select(x=>new SisMenuRolePerm(Number(x,"CODIGO_ROL"),Number(x,"CODIGO_PERM"))).ToList(),
            (await db.Read("MPERMS")).Select(x=>new SisMenuNodePerm(Number(x,"CODIGO_MENU"),Number(x,"CODIGO_PERM"))).ToList(),
            (await db.Read("COUNTS")).ToDictionary(x=>Number(x,"CODIGO_ROL"),x=>Number(x,"TOTAL")),
            (await db.Read("MODS")).Select(x=>new SisSegModuloResponse(Number(x,"CODIGO_MOD"),Text(x,"CODIGO"),Text(x,"NOMBRE"),Text(x,"ICONO"),Number(x,"ORDEN"),Number(x,"ACTIVO")==1)).ToList());
    }
    public Task<ResultDto<SisMenuSnapshot>> Catalog() => Run((db, _, _) => Snapshot(db));
    private static SisMenuResolved Resolve(SisMenuSnapshot s, List<int> assigned) => SisMenuModel.Resolve(s.Menus,s.Roles,s.Asignaciones,assigned,s.Permisos,s.RolPermisos,s.MenuPermisos);
    private static async Task<List<int>> UserRoles(SisMenuDb db,int company,int user) => (await db.Read("UR",company,user)).Select(x=>Number(x,"CODIGO_ROL")).ToList();
    private static async Task<bool> Vertical(SisMenuDb db,int company,int user) => (await db.Read("CTL",company,user)).Any(x=>Number(x,"ACTIVO")==1);
    private static async Task<Dictionary<string,object>> User(SisMenuDb db,int company,int user) =>
        (await db.Read("USER",company,user)).SingleOrDefault() ?? throw new InvalidOperationException("Usuario inexistente en la empresa.");
    private static async Task<List<SisMenuSource>> Sources(SisMenuDb db,int company,int user,string kind="LEGACY") =>
        (await db.Read(kind,company,user)).Select(x=>new SisMenuSource(Number(x,"CODIGO_USUARIO_ROL"),Text(x,"DESCRIPCION"),Text(x,"JSON_MENU"),Number(x,"CODIGO_USUARIO"),Text(x,"USUARIO"))).ToList();
    public Task<ResultDto<SisMenuSession>> Session(int user) => Run<SisMenuSession>(async (db,_,company) =>
    {
        var account=await User(db,company,user); var s=await Snapshot(db); var roles=await UserRoles(db,company,user);
        if (Text(account,"STATUS") is not ("" or "1" or "A")) return new(user,"inactivo",s.Version,[],[],roles);
        if (await Vertical(db,company,user))
        {
            var resolved=Resolve(s,roles); return new(user,"vertical",s.Version,resolved.Menu,resolved.Origenes,roles);
        }
        var legacy=new JsonArray();
        foreach(var source in await Sources(db,company,user))
        {
            var array=JsonNode.Parse(source.Json) as JsonArray ?? throw new InvalidOperationException("JSON_MENU invalido. Revise la migracion.");
            foreach(var node in array) legacy.Add(node?.DeepClone());
        }
        return new(user,(await db.Read("MIG",company,user)).Count>0?"compatibilidad":"legacy",s.Version,legacy,[],roles);
    });
    private static async Task Finish(SisMenuDb db,int company,int? user,int actor,string action,string detail) =>
        await db.Execute("FINISH",N("p_EMPRESA",company),N("p_USER",user),N("p_ACTOR",actor),S("p_ACTION",action),Clob("p_DETAIL",detail));
    private static async Task<int> StoreRole(SisMenuDb db,SisMenuRoleSave value,int actor,List<int>? permissions=null)
    {
        if (string.IsNullOrWhiteSpace(value.Clave)||value.Clave.Length>60||string.IsNullOrWhiteSpace(value.Nombre)||value.Nombre.Length>120||value.Descripcion.Length>300)
            throw new InvalidOperationException("Clave, nombre o descripcion del rol invalidos.");
        using var cmd=db.Command("SAVE_ROLE");
        var id=cmd.Parameters.Add("p_ID",OracleDbType.Int32); id.Direction=System.Data.ParameterDirection.InputOutput; id.Value=value.CodigoRol;
        Add(cmd,"p_MOD",value.CodigoMod); Add(cmd,"p_KEY",value.Clave.Trim(),OracleDbType.Varchar2); Add(cmd,"p_NAME",value.Nombre.Trim(),OracleDbType.Varchar2);
        Add(cmd,"p_DESC",value.Descripcion,OracleDbType.Varchar2); Add(cmd,"p_ACTIVE",value.Activo?1:0);
        Add(cmd,"p_MENUS",Ids(value.Menus),OracleDbType.Clob); Add(cmd,"p_PERMS",permissions is null?null:Ids(permissions),OracleDbType.Clob);
        Add(cmd,"p_SET_PERMS",permissions is null?0:1); Add(cmd,"p_ACTOR",actor); await cmd.ExecuteNonQueryAsync(); return Int(id);
    }
    public Task<ResultDto<int>> SaveRole(SisMenuRoleSave value,int actor) => Run(async(db,_,company)=>
    {
        var before=await Snapshot(db);
        if(value.Menus is null||value.Menus.Any(x=>x<=0)) throw new InvalidOperationException("Seleccion invalida.");
        // Resolve validates catalog cycles and ancestry before writing.
        var trial=before with { Roles=[new(value.CodigoRol,value.CodigoMod,value.Clave,value.Nombre,value.Descripcion,true)],Asignaciones=value.Menus.Distinct().Select(x=>new SisMenuGrant(value.CodigoRol,x)).ToList() };
        var resolvedTrial=Resolve(trial,[value.CodigoRol]);
        if(value.Menus.Except(resolvedTrial.Origenes.Select(x=>x.CodigoMenu)).Any()) throw new InvalidOperationException("Una opcion tiene un padre inexistente o inactivo.");
        foreach(var id in value.Menus) if(!before.Menus.Any(x=>x.Id==id && x.Active)) throw new InvalidOperationException("Opcion inactiva o inexistente.");
        var idRole=await StoreRole(db,value,actor,value.Permisos?.Distinct().ToList());
        var after=await Snapshot(db);
        // Update every migrated user of this shared role, including other configured companies, atomically.
        foreach(var row in await db.Read("AFFECTED"))
        {
            var u=Number(row,"CODIGO_USUARIO"); var c=Number(row,"CODIGO_EMPRESA");
            if((await db.Read("MIG",c,u)).Count>0 && (await UserRoles(db,c,u)).Contains(idRole)) await Cache(db,after,c,u);
        }
        await Finish(db,company,null,actor,"MENU_ROL",JsonSerializer.Serialize(new { Rol=idRole, Antes=before.Roles.FirstOrDefault(x=>x.Id==idRole), AntesMenus=before.Asignaciones.Where(x=>x.Role==idRole), AntesPermisos=before.RolPermisos.Where(x=>x.Role==idRole), Despues=value }));
        return idRole;
    },value.VersionEsperada,true);
    public Task<ResultDto<int>> SaveNode(SisMenuNodeSave value,int actor) => Run(async(db,_,company)=>
    {
        var before=await Snapshot(db);
        if(value.Titulo.Length is 0 or >120 || value.Icono.Length>100 || value.Path.Length>300 || !SisMenuModel.IsInternalPath(value.Path)) throw new InvalidOperationException("Opcion invalida.");
        if(value.Path.Length>0 && !(JsonSerializer.Deserialize<string[]>(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"Assets","MenuRoutes.json"))) ?? []).Contains(value.Path.TrimEnd('/')))
            throw new InvalidOperationException("Ruta fuera del inventario del frontend.");
        if(value.CodigoPadre is int parent && !before.Menus.Any(x=>x.Id==parent && x.Mod==value.CodigoMod)) throw new InvalidOperationException("El padre debe existir en el mismo modulo.");
        if(before.Menus.Any(x=>x.Parent==value.CodigoMenu && x.Mod!=value.CodigoMod)) throw new InvalidOperationException("No se puede cambiar el modulo de un grupo con hijos de otro modulo.");
        var replacement=new SisMenuNode(value.CodigoMenu>0?value.CodigoMenu:-1,value.CodigoMod,value.CodigoPadre,value.Titulo,value.Path,value.Icono,value.Orden,value.Activo);
        var trial=before with { Menus=before.Menus.Where(x=>x.Id!=value.CodigoMenu).Append(replacement).ToList() };
        Resolve(trial,[]);
        using var cmd=db.Command("SAVE_NODE");
        var id=cmd.Parameters.Add("p_ID",OracleDbType.Int32); id.Direction=System.Data.ParameterDirection.InputOutput; id.Value=value.CodigoMenu;
        Add(cmd,"p_MOD",value.CodigoMod); Add(cmd,"p_PARENT",value.CodigoPadre); Add(cmd,"p_TITLE",value.Titulo,OracleDbType.Varchar2);
        Add(cmd,"p_PATH",value.Path,OracleDbType.Varchar2); Add(cmd,"p_ICON",value.Icono,OracleDbType.Varchar2); Add(cmd,"p_ORDER",value.Orden); Add(cmd,"p_ACTIVE",value.Activo?1:0); Add(cmd,"p_ACTOR",actor);
        await cmd.ExecuteNonQueryAsync(); var saved=Int(id); var after=await Snapshot(db);
        foreach(var row in await db.Read("AFFECTED"))
        {
            var user=Number(row,"CODIGO_USUARIO"); var c=Number(row,"CODIGO_EMPRESA");
            if((await db.Read("MIG",c,user)).Count>0) await Cache(db,after,c,user);
        }
        await Finish(db,company,null,actor,"MENU_CATALOGO",$"Menu={saved}; Antes={JsonSerializer.Serialize(before.Menus.FirstOrDefault(x=>x.Id==saved))}; Despues={JsonSerializer.Serialize(replacement)}");
        return saved;
    },value.VersionEsperada,true);
    public Task<ResultDto<int>> SaveUser(SisMenuUserSave value,int actor) => Run(async(db,_,company)=>
    {
        await User(db,company,value.CodigoUsuario);
        if((await db.Read("MIG",company,value.CodigoUsuario)).Count==0) throw new InvalidOperationException("Migre y compare el usuario antes de asignarle roles desde esta pantalla.");
        if(value.Roles is null||value.Roles.Any(x=>x<=0)) throw new InvalidOperationException("Roles invalidos.");
        var before=await UserRoles(db,company,value.CodigoUsuario);
        await db.Execute("SAVE_USER",N("p_USER",value.CodigoUsuario),N("p_EMPRESA",company),Clob("p_ROLES",Ids(value.Roles)),N("p_ACTOR",actor));
        await Cache(db,await Snapshot(db),company,value.CodigoUsuario);
        await Finish(db,company,value.CodigoUsuario,actor,"MENU_USR_ROL",$"Antes={Ids(before)}; Despues={Ids(value.Roles)}"); return value.CodigoUsuario;
    },value.VersionEsperada,true);
    private static async Task Cache(SisMenuDb db,SisMenuSnapshot s,int company,int user)
    {
        var resolved=Resolve(s,await UserRoles(db,company,user));
        await db.Execute("CLEAR_CACHE",N("p_USER",user),N("p_EMPRESA",company));
        // All stale legacy rows are removed, including modules which now have zero options.

        var modules=await db.Read("MODS");
        foreach(var mod in modules.OrderBy(m=>s.Menus.Where(n=>n.Parent is null && n.Mod==Number(m,"CODIGO_MOD")).Select(n=>n.Order).DefaultIfEmpty(int.MaxValue).Min()))
        {
            var modId=Number(mod,"CODIGO_MOD"); var modNodes=s.Menus.Where(x=>x.Mod==modId).Select(x=>x.Id).ToHashSet();
            var selected=(await UserRoles(db,company,user));
            var grants=s.Asignaciones.Where(x=>modNodes.Contains(x.Menu)).ToList();
            var group=SisMenuModel.Resolve(s.Menus,s.Roles,grants,selected,s.Permisos,s.RolPermisos,s.MenuPermisos).Menu;
            if(group.Count>0) await db.Execute("WRITE_CACHE",N("p_USER",user),N("p_EMPRESA",company),S("p_MOD",Text(mod,"CODIGO")),Clob("p_JSON",group.ToJsonString()));
        }
        // Always write a well-formed empty cache when the user loses every option.
        if(resolved.Menu.Count==0) await db.Execute("WRITE_CACHE",N("p_USER",user),N("p_EMPRESA",company),S("p_MOD","SIS"),Clob("p_JSON","[]"));
    }
    private async Task<(SisMenuAnalysis Analysis,List<SisMenuLegacyNode> Tree,List<SisMenuSource> Sources,List<string> Effective)> AnalyzeCore(SisMenuDb db,OracleConnection cn,int company,int user)
    {
        var account=await User(db,company,user); var s=await Snapshot(db); var sources=await Sources(db,company,user,"SOURCE");
        var normalized=(await UserRoles(db,company,user)).Order().ToList(); var exceptions=(await db.Read("UP",company,user)).OrderBy(x=>Number(x,"CODIGO_PERM")).ThenBy(x=>Text(x,"TIPO")).ToList();
        var effective=(await SupportSecurity.GetPermissionsAsync(cn,user,company)).Permissions.Order(StringComparer.Ordinal).ToList();
        var hash=SisMenuModel.Hash(JsonSerializer.Serialize(new { sources,normalized,exceptions,effective,status=Text(account,"STATUS"),super=Text(account,"IS_SUPERUSER") }));
        try
        {
            if((await db.Read("MIG",company,user)).Count>0) throw new InvalidOperationException("Usuario ya migrado. Sus roles se administran en la pantalla Roles por usuario.");
            var observed=await Sources(db,company,user);
            if(sources.Any(x=>x.UserId!=user || !string.Equals(x.Login.Trim(),Text(account,"LOGIN").Trim(),StringComparison.OrdinalIgnoreCase)) || !sources.Select(x=>x.Id).ToHashSet().SetEquals(observed.Select(x=>x.Id)))
                throw new InvalidOperationException("Conflicto entre CODIGO_USUARIO y login legacy. Conciliar antes de migrar.");
            var routesFile=Path.Combine(AppContext.BaseDirectory,"Assets","MenuRoutes.json");
            if(!File.Exists(routesFile)) throw new InvalidOperationException("Falta el inventario de rutas publicado.");
            var known=(JsonSerializer.Deserialize<string[]>(await File.ReadAllTextAsync(routesFile)) ?? []).ToHashSet(StringComparer.Ordinal);
            var tree=SisMenuModel.ParseLegacy(sources,known);
            var flat=Flatten(tree).ToList();
            return (new(user,s.Version,hash,true,"Perfil exacto preparado. No se aplicaron cambios.",SisMenuModel.LegacyTree(tree),flat.Count,effective),tree,sources,effective);
        }
        catch(Exception ex) when(ex is JsonException or InvalidOperationException)
        {
            return(new(user,s.Version,hash,false,ex.Message,[],0,effective),[],sources,effective);
        }
    }
    private static IEnumerable<SisMenuLegacyNode> Flatten(IEnumerable<SisMenuLegacyNode> nodes)
    {
        foreach(var node in nodes) { yield return node; foreach(var child in Flatten(node.Children)) yield return child; }
    }
    public Task<ResultDto<SisMenuAnalysis>> Analyze(int user) => Run(async(db,cn,company)=>(await AnalyzeCore(db,cn,company,user)).Analysis);
    public Task<ResultDto<SisMenuSession>> Migrate(SisMenuMigration value,int actor) => Run<SisMenuSession>(async(db,cn,company)=>
    {
        var source=await AnalyzeCore(db,cn,company,value.CodigoUsuario);
        if(!source.Analysis.PuedeMigrar) throw new InvalidOperationException(source.Analysis.Mensaje);
        if(value.HashOrigen!=source.Analysis.HashOrigen) throw new InvalidOperationException("El origen cambio. Analice nuevamente antes de aplicar.");
        var oldRoles=await UserRoles(db,company,value.CodigoUsuario); var oldPerms=await db.Read("UP",company,value.CodigoUsuario);
        await db.Execute("BACKUP_USER",N("p_USER",value.CodigoUsuario),N("p_EMPRESA",company),S("p_HASH",value.HashOrigen),
            Clob("p_SOURCE",JsonSerializer.Serialize(new { source.Sources,oldRoles,oldPerms,source.Effective,source.Analysis.MenuAntes })),N("p_ACTOR",actor));
        var menuIds=new List<int>(); var permIds=new List<int>();
        var moduleIds=(await db.Read("MODS")).ToDictionary(x=>Text(x,"CODIGO"),x=>Number(x,"CODIGO_MOD"));
        async Task AddNodes(IEnumerable<SisMenuLegacyNode> nodes,int? parent,string parentKey)
        {
            foreach(var node in nodes)
            {
                var key=SisMenuModel.Hash(JsonSerializer.Serialize(new { parentKey,node.Module,node.Title,node.Path,node.Icon,node.Order,node.Permissions }));
                using var cmd=db.Command("MAP_NODE");
                Add(cmd,"p_HASH",key,OracleDbType.Varchar2); Add(cmd,"p_MOD",node.Module,OracleDbType.Varchar2); Add(cmd,"p_PARENT",parent);
                Add(cmd,"p_TITLE",node.Title,OracleDbType.Varchar2); Add(cmd,"p_PATH",node.Path,OracleDbType.Varchar2); Add(cmd,"p_ICON",node.Icon,OracleDbType.Varchar2);
                Add(cmd,"p_ORDER",node.Order); var output=Output(cmd,"p_ID"); await cmd.ExecuteNonQueryAsync(); var id=Int(output); menuIds.Add(id);
                if(!moduleIds.ContainsKey(node.Module)) moduleIds[node.Module]=Number((await db.Read("MODS")).Single(x=>Text(x,"CODIGO")==node.Module),"CODIGO_MOD");
                var module=moduleIds[node.Module];
                foreach(var permission in node.Permissions)
                {
                    var perm=await MapPermission(db,permission,module); permIds.Add(perm);
                    await db.Execute("LINK_PERM",N("p_MENU",id),N("p_PERM",perm));
                }
                await AddNodes(node.Children,id,key);
            }
        }
        await AddNodes(source.Tree,null,"");
        var catalog=await Snapshot(db); var firstModule=catalog.Menus.Single(x=>x.Id==menuIds[0]).Mod;
        foreach(var key in source.Effective) permIds.Add(await MapPermission(db,key,firstModule));
        var profile=SisMenuModel.Hash(JsonSerializer.Serialize(new { menu=source.Analysis.MenuAntes,source.Effective }));
        var roleKey="MIG_"+profile[..40]; var existing=catalog.Roles.SingleOrDefault(x=>x.Key==roleKey);
        int role;
        if(existing is not null)
        {
            if(!existing.Active || !catalog.Asignaciones.Where(x=>x.Role==existing.Id).Select(x=>x.Menu).ToHashSet().SetEquals(menuIds) ||
                !catalog.RolPermisos.Where(x=>x.Role==existing.Id).Select(x=>x.Perm).ToHashSet().SetEquals(permIds))
                throw new InvalidOperationException("El perfil compartido fue editado. Revise sus opciones antes de reutilizarlo.");
            role=existing.Id;
        }
        else role=await StoreRole(db,new(0,firstModule,roleKey,$"Perfil migrado {profile[..10]}","Perfil exacto compartido. Puede renombrarse desde Opciones por rol.",true,menuIds,0),actor,permIds.Distinct().ToList());
        await db.Execute("SAVE_USER",N("p_USER",value.CodigoUsuario),N("p_EMPRESA",company),Clob("p_ROLES",Ids([role])),N("p_ACTOR",actor));
        await db.Execute("CLEAR_PERMS",N("p_USER",value.CodigoUsuario),N("p_EMPRESA",company),N("p_ACTOR",actor));
        catalog=await Snapshot(db); var result=Resolve(catalog,[role]);
        if(!JsonNode.DeepEquals(result.Menu,source.Analysis.MenuAntes)) throw new InvalidOperationException("La comparacion detecto diferencias de menu. No se aplico la migracion.");
        await Cache(db,catalog,company,value.CodigoUsuario);
        var after=(await SupportSecurity.GetPermissionsAsync(cn,value.CodigoUsuario,company)).Permissions.ToHashSet(StringComparer.OrdinalIgnoreCase);
        if(!after.SetEquals(source.Effective)) throw new InvalidOperationException("La comparacion detecto diferencias de permisos. No se aplico la migracion.");
        await db.Execute("SET_MODE",N("p_USER",value.CodigoUsuario),N("p_EMPRESA",company),N("p_ACTIVE",1));
        await Finish(db,company,value.CodigoUsuario,actor,"MENU_MIGRAR",$"Hash={value.HashOrigen}; Rol={role}; Opciones={menuIds.Count}; RolesAntes={Ids(oldRoles)}");
        return new(value.CodigoUsuario,"vertical",source.Analysis.Version+1,result.Menu,result.Origenes,[role]);
    },value.VersionEsperada,true);
    private static async Task<int> MapPermission(SisMenuDb db,string key,int module)
    {
        using var cmd=db.Command("MAP_PERM"); Add(cmd,"p_KEY",key,OracleDbType.Varchar2); Add(cmd,"p_MOD",module); var id=Output(cmd,"p_ID");
        await cmd.ExecuteNonQueryAsync(); return Int(id);
    }
    public async Task<ResultDto<List<SisMenuHistoryRow>>> History(SisMenuHistoryQuery value)
    {
        var result=await Run(async(db,_,company)=>
        {
            await User(db,company,value.CodigoUsuario);
            return (await db.Read("MIG",company,value.CodigoUsuario)).Select(x=>new SisMenuHistoryRow(Number(x,"CODIGO_MIG"),Text(x,"HUELLA"),Text(x,"FECHA_INS"),Number(x,"USUARIO_INS"),Number(x,"VERSION"))).ToList();
        });
        if(!result.IsValid) return result;
        var size=Math.Clamp(value.PageSize,1,100); var page=Math.Max(1,value.Page); var all=result.Data!;
        result.Data=all.Skip((page-1)*size).Take(size).ToList(); result.Page=page; result.TotalPage=(int)Math.Ceiling(all.Count/(double)size); result.CantidadRegistros=all.Count;
        return result;
    }
    public Task<ResultDto<int>> SetMode(SisMenuMode value,int actor) => Run(async(db,_,company)=>
    {
        await User(db,company,value.CodigoUsuario);
        if(!(await db.Read("MIG",company,value.CodigoUsuario)).Any()) throw new InvalidOperationException("El usuario no tiene una migracion validada.");
        // Refresh compatibility JSON from current roles; reversing never restores stale access.
        await Cache(db,await Snapshot(db),company,value.CodigoUsuario);
        await db.Execute("SET_MODE",N("p_USER",value.CodigoUsuario),N("p_EMPRESA",company),N("p_ACTIVE",value.Vertical?1:0));
        await Finish(db,company,value.CodigoUsuario,actor,"MENU_ESTRATEGIA",$"Vertical={value.Vertical}"); return value.CodigoUsuario;
    },value.VersionEsperada,true);
}
