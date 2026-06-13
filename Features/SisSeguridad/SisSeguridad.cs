using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Features.Support;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OssmmasoftVerticalSlice.Features.SisSeguridad;

public record SisSegModuloResponse(int CodigoMod, string Codigo, string Nombre, string Icono, int Orden, bool Activo);
public record SisSegMenuResponse(int CodigoMenu, int CodigoMod, int? CodigoPadre, string Titulo, string Path, string Icono, int Orden, bool Activo);
public record SisSegPermResponse(int CodigoPerm, int CodigoMod, string Clave, string Nombre, string Descripcion, bool Activo);
public record SisSegUsrPermResponse(int CodigoPerm, int CodigoMod, string Clave, string Nombre, string Descripcion, bool Activo, string Tipo);
public record SisSegRolResponse(int CodigoRol, int CodigoMod, string Clave, string Nombre, string Descripcion, bool Activo);
public record SisSegRolPermResponse(int CodigoRol, int CodigoPerm);
public record SisSegRolMenuResponse(int CodigoRol, int CodigoMenu);
public record SisSegCatalogosResponse(List<SisSegModuloResponse> Modulos, List<SisSegMenuResponse> Menus, List<SisSegPermResponse> Permisos, List<SisSegRolResponse> Roles, List<SisSegRolPermResponse> RolPermisos, List<SisSegRolMenuResponse> RolMenus);
public record SisSegUsuarioQuery(int CodigoUsuario);
public record SisSegUsrPermCommand(int CodigoPerm, string Tipo, bool Activo = true);
public record SisSegUsrRolSaveCommand(int CodigoUsuario, List<int> Roles, List<SisSegUsrPermCommand> Permisos, int UsuarioUpd);
public record SisSegRolSaveCommand(int CodigoRol, int CodigoMod, string Clave, string Nombre, string Descripcion, bool Activo = true, int UsuarioUpd = 0);
public record SisSegMenuSaveCommand(int CodigoMenu, int CodigoMod, int? CodigoPadre, string Titulo, string Path, string Icono, int Orden, bool Activo = true, int UsuarioUpd = 0);
public record SisSegRolPermSaveCommand(int CodigoRol, List<int> Permisos, int UsuarioUpd = 0);
public record SisSegRolMenuSaveCommand(int CodigoRol, List<int> Menus, int UsuarioUpd = 0);
public record SisSegCacheCommand(int CodigoUsuario, string? CodigoModulo = null);
public record SisSegUsuarioResponse(int CodigoUsuario, string Usuario, string Login, bool IsSuperuser, List<SisSegRolResponse> Roles, List<SisSegPermResponse> Permisos, List<SisSegUsrPermResponse> Excepciones, JsonElement JsonMenu);
public record SisSegCacheResponse(int CodigoUsuario, List<string> ModulosActualizados, JsonElement JsonMenu);
public record SisSegMigQuery(string? CodigoModulo = null);
public record SisSegMigItemResponse(int CodigoUsuario, string Usuario, string Login, string Descripcion, bool IsSuperuser, bool JsonValido, List<string> Permisos, List<string> Rutas, List<string> RolesSugeridos, List<string> PermisosExcepcionSugeridos);
public record SisSegMigApplyCommand(int CodigoUsuario, string? CodigoModulo = null, int UsuarioUpd = 0);
public record SisSegMigBulkCommand(string? CodigoModulo = null, int UsuarioUpd = 0, bool Confirmar = false);
public record SisSegMigBulkResponse(int UsuariosProcesados, int RolesAplicados, int ExcepcionesAplicadas, List<string> Mensajes);
public record SisSegMigResumenQuery(string? CodigoModulo = null);
public record SisSegMigResumenResponse(int UsuariosLegacy, int UsuariosNormalizados, int RegistrosLegacy, int RolesNormalizados, int ExcepcionesNormalizadas, int JsonInvalidos, int Pendientes);
public record SisSegInstallStatusResponse(bool InstalacionCompleta, List<string> TablasFaltantes, string Mensaje);
public record SisSegCloneUserData(string Usuario, string Login, string Clave, decimal? Cedula = null, string? Email = null, bool RecibeEmail = true);
public record SisSegCloneUserCommand(int CodigoUsuarioOrigen, int? CodigoUsuarioDestino = null, SisSegCloneUserData? UsuarioDestino = null, bool SobrescribirAccesos = true, int UsuarioUpd = 0);
public record SisSegCloneUserResponse(int CodigoUsuarioOrigen, int CodigoUsuarioDestino, bool UsuarioDestinoCreado, int RolesCopiados, int ExcepcionesCopiadas, JsonElement JsonMenu);

[ApiController]
[Authorize]
[Route("api/SisSeguridad")]
public class SisSeguridadController(ConnectionDB connectionDB, IConfiguration config) : ControllerBase
{
    [HttpPost("getCatalogos")]
    public async Task<IActionResult> GetCatalogos()
    {
        var access = await RequireAdminAsync(User, Request);
        if (!access.IsValid)
        {
            return Ok(new ResultDto<SisSegCatalogosResponse>(null!) { Data = null, IsValid = false, Message = access.Message });
        }

        var handler = new SisSeguridadHandler(connectionDB, config);
        var result = await handler.GetCatalogosAsync();
        return Ok(result);
    }

    [HttpPost("getEstadoInstalacion")]
    public async Task<IActionResult> GetEstadoInstalacion()
    {
        var access = await RequireAdminAsync(User, Request);
        if (!access.IsValid)
        {
            return Ok(new ResultDto<SisSegInstallStatusResponse>(null!) { Data = null, IsValid = false, Message = access.Message });
        }

        var handler = new SisSeguridadHandler(connectionDB, config);
        var result = await handler.GetEstadoInstalacionAsync();
        return Ok(result);
    }

    [HttpPost("getUsuario")]
    public async Task<IActionResult> GetUsuario(SisSegUsuarioQuery value)
    {
        var access = await RequireAdminAsync(User, Request);
        if (!access.IsValid)
        {
            return Ok(new ResultDto<SisSegUsuarioResponse>(null!) { Data = null, IsValid = false, Message = access.Message });
        }

        var handler = new SisSeguridadHandler(connectionDB, config);
        var result = await handler.GetUsuarioAsync(value.CodigoUsuario);
        return Ok(result);
    }

    [HttpPost("saveUsuarioRoles")]
    public async Task<IActionResult> SaveUsuarioRoles(SisSegUsrRolSaveCommand value)
    {
        var access = await RequireAdminAsync(User, Request);
        if (!access.IsValid)
        {
            return Ok(new ResultDto<SisSegCacheResponse>(null!) { Data = null, IsValid = false, Message = access.Message });
        }

        var handler = new SisSeguridadHandler(connectionDB, config);
        var result = await handler.SaveUsuarioRolesAsync(value, access.Data);
        return Ok(result);
    }

    [HttpPost("clonarUsuario")]
    public async Task<IActionResult> ClonarUsuario(SisSegCloneUserCommand value)
    {
        var access = await RequireAdminAsync(User, Request);
        if (!access.IsValid)
        {
            return Ok(new ResultDto<SisSegCloneUserResponse>(null!) { Data = null, IsValid = false, Message = access.Message });
        }

        var handler = new SisSeguridadHandler(connectionDB, config);
        var result = await handler.CloneUsuarioAsync(value, access.Data);
        return Ok(result);
    }

    [HttpPost("saveRol")]
    public async Task<IActionResult> SaveRol(SisSegRolSaveCommand value)
    {
        var access = await RequireAdminAsync(User, Request);
        if (!access.IsValid)
        {
            return Ok(new ResultDto<SisSegRolResponse>(null!) { Data = null, IsValid = false, Message = access.Message });
        }

        var handler = new SisSeguridadHandler(connectionDB, config);
        var result = await handler.SaveRolAsync(value, access.Data);
        return Ok(result);
    }

    [HttpPost("saveMenu")]
    public async Task<IActionResult> SaveMenu(SisSegMenuSaveCommand value)
    {
        var access = await RequireAdminAsync(User, Request);
        if (!access.IsValid)
        {
            return Ok(new ResultDto<SisSegMenuResponse>(null!) { Data = null, IsValid = false, Message = access.Message });
        }

        var handler = new SisSeguridadHandler(connectionDB, config);
        var result = await handler.SaveMenuAsync(value, access.Data);
        return Ok(result);
    }

    [HttpPost("saveRolPermisos")]
    public async Task<IActionResult> SaveRolPermisos(SisSegRolPermSaveCommand value)
    {
        var access = await RequireAdminAsync(User, Request);
        if (!access.IsValid)
        {
            return Ok(new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = access.Message });
        }

        var handler = new SisSeguridadHandler(connectionDB, config);
        var result = await handler.SaveRolPermisosAsync(value, access.Data);
        return Ok(result);
    }

    [HttpPost("saveRolMenus")]
    public async Task<IActionResult> SaveRolMenus(SisSegRolMenuSaveCommand value)
    {
        var access = await RequireAdminAsync(User, Request);
        if (!access.IsValid)
        {
            return Ok(new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = access.Message });
        }

        var handler = new SisSeguridadHandler(connectionDB, config);
        var result = await handler.SaveRolMenusAsync(value, access.Data);
        return Ok(result);
    }

    [HttpPost("regenerarCache")]
    public async Task<IActionResult> RegenerarCache(SisSegCacheCommand value)
    {
        var access = await RequireAdminAsync(User, Request);
        if (!access.IsValid)
        {
            return Ok(new ResultDto<SisSegCacheResponse>(null!) { Data = null, IsValid = false, Message = access.Message });
        }

        var handler = new SisSeguridadHandler(connectionDB, config);
        var result = await handler.RegenerateCacheAsync(value.CodigoUsuario, value.CodigoModulo, access.Data);
        return Ok(result);
    }

    [HttpPost("getMigracionSugerida")]
    public async Task<IActionResult> GetMigracionSugerida(SisSegMigQuery value)
    {
        var access = await RequireAdminAsync(User, Request);
        if (!access.IsValid)
        {
            return Ok(new ResultDto<List<SisSegMigItemResponse>>(null!) { Data = null, IsValid = false, Message = access.Message });
        }

        var handler = new SisSeguridadHandler(connectionDB, config);
        var result = await handler.GetMigracionSugeridaAsync(value.CodigoModulo);
        return Ok(result);
    }

    [HttpPost("aplicarMigracionSugerida")]
    public async Task<IActionResult> AplicarMigracionSugerida(SisSegMigApplyCommand value)
    {
        var access = await RequireAdminAsync(User, Request);
        if (!access.IsValid)
        {
            return Ok(new ResultDto<SisSegCacheResponse>(null!) { Data = null, IsValid = false, Message = access.Message });
        }

        var handler = new SisSeguridadHandler(connectionDB, config);
        var result = await handler.ApplyMigracionSugeridaAsync(value, access.Data);
        return Ok(result);
    }

    [HttpPost("aplicarMigracionMasiva")]
    public async Task<IActionResult> AplicarMigracionMasiva(SisSegMigBulkCommand value)
    {
        var access = await RequireSuperuserAsync(User, Request);
        if (!access.IsValid)
        {
            return Ok(new ResultDto<SisSegMigBulkResponse>(null!) { Data = null, IsValid = false, Message = access.Message });
        }

        var handler = new SisSeguridadHandler(connectionDB, config);
        var result = await handler.ApplyMigracionMasivaAsync(value, access.Data);
        return Ok(result);
    }

    [HttpPost("getResumenMigracion")]
    public async Task<IActionResult> GetResumenMigracion(SisSegMigResumenQuery value)
    {
        var access = await RequireAdminAsync(User, Request);
        if (!access.IsValid)
        {
            return Ok(new ResultDto<SisSegMigResumenResponse>(null!) { Data = null, IsValid = false, Message = access.Message });
        }

        var handler = new SisSeguridadHandler(connectionDB, config);
        var result = await handler.GetResumenMigracionAsync(value.CodigoModulo);
        return Ok(result);
    }

    private async Task<ResultDto<int>> RequireAdminAsync(ClaimsPrincipal user, HttpRequest request)
    {
        var session = await SupportSecurity.ResolveAuthenticatedUserAsync(connectionDB, config, user, request);
        if (!session.IsValid)
        {
            return session;
        }

        if (!SupportDb.TryGetEmpresa(config, out int empresa, out string errorMessage))
        {
            return new ResultDto<int>(0) { IsValid = false, Message = errorMessage };
        }

        using var cn = connectionDB.GetSisConnection();
        await cn.OpenAsync();
        var usuario = await SisSeguridadHandler.ReadUsuarioForAuthAsync(cn, session.Data, empresa);
        if (usuario is not null && usuario.IsSuperuser)
        {
            return session;
        }

        var permissions = await SupportSecurity.GetPermissionsAsync(cn, session.Data, empresa);
        return SupportSecurity.HasAny(permissions, SupportSecurity.SupportUsersConfig)
            ? session
            : new ResultDto<int>(0) { IsValid = false, Message = $"El usuario no tiene el permiso requerido: {SupportSecurity.SupportUsersConfig}." };
    }

    private async Task<ResultDto<int>> RequireSuperuserAsync(ClaimsPrincipal user, HttpRequest request)
    {
        var access = await RequireAdminAsync(user, request);
        if (!access.IsValid)
        {
            return access;
        }

        if (!SupportDb.TryGetEmpresa(config, out int empresa, out string errorMessage))
        {
            return new ResultDto<int>(0) { IsValid = false, Message = errorMessage };
        }

        using var cn = connectionDB.GetSisConnection();
        await cn.OpenAsync();
        var target = await SisSeguridadHandler.ReadUsuarioForAuthAsync(cn, access.Data, empresa);
        return target is not null && target.IsSuperuser
            ? access
            : new ResultDto<int>(0) { IsValid = false, Message = "La migracion masiva requiere usuario superuser." };
    }
}

public class SisSeguridadHandler(ConnectionDB connectionDB, IConfiguration config)
{
    private static readonly string[] RequiredInstallTables =
    [
        "OSS_MENU",
        "OSS_MENU_PERM",
        "OSS_MOD",
        "OSS_PERM",
        "OSS_ROL",
        "OSS_ROL_MENU",
        "OSS_ROL_PERM",
        "OSS_SEG_AUD",
        "OSS_USR_PERM",
        "OSS_USR_ROL",
        "OSS_USUARIO_ROL"
    ];

    private static readonly string[] RequiredSaveTables =
    [
        "OSS_MENU",
        "OSS_MENU_PERM",
        "OSS_PERM",
        "OSS_ROL",
        "OSS_ROL_MENU",
        "OSS_ROL_PERM",
        "OSS_USR_PERM",
        "OSS_USR_ROL",
        "OSS_USUARIO_ROL"
    ];

    internal static Task<SisSegUserData?> ReadUsuarioForAuthAsync(OracleConnection cn, int codigoUsuario, int empresa) =>
        ReadUsuarioAsync(cn, codigoUsuario, empresa);

    public async Task<ResultDto<SisSegInstallStatusResponse>> GetEstadoInstalacionAsync()
    {
        using var cn = connectionDB.GetSisConnection();
        var open = await OpenAsync(cn, "estado de instalacion de seguridad");
        if (!open.IsValid)
        {
            return new ResultDto<SisSegInstallStatusResponse>(null!) { Data = null, IsValid = false, Message = open.Message };
        }

        try
        {
            var missingTables = await FindMissingSisTablesAsync(cn, RequiredInstallTables);
            var installed = missingTables.Count == 0;
            var message = installed
                ? "Instalacion de seguridad normalizada completa."
                : $"Faltan tablas requeridas en SIS: {string.Join(", ", missingTables)}.";
            var response = new SisSegInstallStatusResponse(installed, missingTables, message);

            return new ResultDto<SisSegInstallStatusResponse>(response) { Data = response, IsValid = true, Message = "Success" };
        }
        catch (Exception ex)
        {
            return new ResultDto<SisSegInstallStatusResponse>(null!) { Data = null, IsValid = false, Message = FormatOracleError(ex) };
        }
    }

    public async Task<ResultDto<SisSegCatalogosResponse>> GetCatalogosAsync()
    {
        using var cn = connectionDB.GetSisConnection();
        var open = await OpenAsync(cn, "catalogos de seguridad");
        if (!open.IsValid)
        {
            return new ResultDto<SisSegCatalogosResponse>(null!) { Data = null, IsValid = false, Message = open.Message };
        }

        try
        {
            var result = new SisSegCatalogosResponse(
                await ReadModulosAsync(cn),
                await ReadMenusAsync(cn),
                await ReadPermisosAsync(cn),
                await ReadRolesAsync(cn),
                await ReadRolPermisosAsync(cn),
                await ReadRolMenusAsync(cn)
            );

            return new ResultDto<SisSegCatalogosResponse>(result) { Data = result, IsValid = true, Message = "Success" };
        }
        catch (Exception ex)
        {
            return new ResultDto<SisSegCatalogosResponse>(null!) { Data = null, IsValid = false, Message = $"Error técnico: {ex.Message}" };
        }
    }

    public async Task<ResultDto<SisSegRolResponse>> SaveRolAsync(SisSegRolSaveCommand value, int usuarioSesion)
    {
        if (value.CodigoMod <= 0 || string.IsNullOrWhiteSpace(value.Clave) || string.IsNullOrWhiteSpace(value.Nombre))
        {
            return new ResultDto<SisSegRolResponse>(null!) { Data = null, IsValid = false, Message = "Modulo, clave y nombre del rol son requeridos." };
        }

        if (!SupportDb.TryGetEmpresa(config, out int empresa, out string empresaMessage))
        {
            return new ResultDto<SisSegRolResponse>(null!) { Data = null, IsValid = false, Message = empresaMessage };
        }

        int usuarioUpd = value.UsuarioUpd > 0 ? value.UsuarioUpd : usuarioSesion;
        using var cn = connectionDB.GetSisConnection();
        var open = await OpenAsync(cn, "guardar rol");
        if (!open.IsValid)
        {
            return new ResultDto<SisSegRolResponse>(null!) { Data = null, IsValid = false, Message = open.Message };
        }

        using var tx = cn.BeginTransaction();
        try
        {
            int codigoRol = await SaveRolCoreAsync(cn, tx, value, usuarioUpd);
            await InsertAuditAsync(cn, tx, null, empresa, "SAVE_ROL", $"Rol={codigoRol}; Clave={value.Clave}", usuarioUpd);
            tx.Commit();

            var response = new SisSegRolResponse(codigoRol, value.CodigoMod, value.Clave.Trim().ToUpperInvariant(), value.Nombre.Trim(), value.Descripcion?.Trim() ?? string.Empty, value.Activo);
            return new ResultDto<SisSegRolResponse>(response) { Data = response, IsValid = true, Message = "Rol guardado correctamente." };
        }
        catch (Exception ex)
        {
            tx.Rollback();
            return new ResultDto<SisSegRolResponse>(null!) { Data = null, IsValid = false, Message = $"Error técnico: {ex.Message}" };
        }
    }

    public async Task<ResultDto<SisSegMenuResponse>> SaveMenuAsync(SisSegMenuSaveCommand value, int usuarioSesion)
    {
        if (value.CodigoMod <= 0 || string.IsNullOrWhiteSpace(value.Titulo))
        {
            return new ResultDto<SisSegMenuResponse>(null!) { Data = null, IsValid = false, Message = "Modulo y titulo del menu son requeridos." };
        }

        if (!SupportDb.TryGetEmpresa(config, out int empresa, out string empresaMessage))
        {
            return new ResultDto<SisSegMenuResponse>(null!) { Data = null, IsValid = false, Message = empresaMessage };
        }

        int usuarioUpd = value.UsuarioUpd > 0 ? value.UsuarioUpd : usuarioSesion;
        using var cn = connectionDB.GetSisConnection();
        var open = await OpenAsync(cn, "guardar menu");
        if (!open.IsValid)
        {
            return new ResultDto<SisSegMenuResponse>(null!) { Data = null, IsValid = false, Message = open.Message };
        }

        using var tx = cn.BeginTransaction();
        try
        {
            int codigoMenu = await SaveMenuCoreAsync(cn, tx, value, usuarioUpd);
            await InsertAuditAsync(cn, tx, null, empresa, "SAVE_MENU", $"Menu={codigoMenu}; Titulo={value.Titulo}", usuarioUpd);
            tx.Commit();

            var response = new SisSegMenuResponse(codigoMenu, value.CodigoMod, value.CodigoPadre, value.Titulo.Trim(), value.Path?.Trim() ?? string.Empty, value.Icono?.Trim() ?? string.Empty, value.Orden, value.Activo);
            return new ResultDto<SisSegMenuResponse>(response) { Data = response, IsValid = true, Message = "Menu guardado correctamente." };
        }
        catch (Exception ex)
        {
            tx.Rollback();
            return new ResultDto<SisSegMenuResponse>(null!) { Data = null, IsValid = false, Message = $"Error técnico: {ex.Message}" };
        }
    }

    public async Task<ResultDto<string>> SaveRolPermisosAsync(SisSegRolPermSaveCommand value, int usuarioSesion)
    {
        return await SaveRolRelationAsync(value.CodigoRol, value.Permisos, value.UsuarioUpd, usuarioSesion, "permisos del rol", "SIS.OSS_ROL_PERM", "CODIGO_PERM", "SAVE_ROL_PERM");
    }

    public async Task<ResultDto<string>> SaveRolMenusAsync(SisSegRolMenuSaveCommand value, int usuarioSesion)
    {
        return await SaveRolRelationAsync(value.CodigoRol, value.Menus, value.UsuarioUpd, usuarioSesion, "menus del rol", "SIS.OSS_ROL_MENU", "CODIGO_MENU", "SAVE_ROL_MENU");
    }

    public async Task<ResultDto<SisSegUsuarioResponse>> GetUsuarioAsync(int codigoUsuario)
    {
        if (codigoUsuario <= 0)
        {
            return new ResultDto<SisSegUsuarioResponse>(null!) { Data = null, IsValid = false, Message = "CodigoUsuario es requerido." };
        }

        if (!SupportDb.TryGetEmpresa(config, out int empresa, out string empresaMessage))
        {
            return new ResultDto<SisSegUsuarioResponse>(null!) { Data = null, IsValid = false, Message = empresaMessage };
        }

        using var cn = connectionDB.GetSisConnection();
        var open = await OpenAsync(cn, "seguridad de usuario");
        if (!open.IsValid)
        {
            return new ResultDto<SisSegUsuarioResponse>(null!) { Data = null, IsValid = false, Message = open.Message };
        }

        try
        {
            var usuario = await ReadUsuarioAsync(cn, codigoUsuario, empresa);
            if (usuario is null)
            {
                return new ResultDto<SisSegUsuarioResponse>(null!) { Data = null, IsValid = false, Message = "No se encontró el usuario indicado." };
            }

            var roles = await ReadUsuarioRolesAsync(cn, codigoUsuario, empresa);
            var permisos = await ReadEffectivePermisosAsync(cn, codigoUsuario, empresa, usuario.IsSuperuser);
            var excepciones = await ReadUsuarioPermisosAsync(cn, codigoUsuario, empresa);
            var jsonMenu = await ReadLegacyJsonMenuAsync(cn, codigoUsuario);
            var response = new SisSegUsuarioResponse(
                codigoUsuario,
                usuario.Usuario,
                usuario.Login,
                usuario.IsSuperuser,
                roles,
                permisos,
                excepciones,
                ParseJson(jsonMenu)
            );

            return new ResultDto<SisSegUsuarioResponse>(response) { Data = response, IsValid = true, Message = "Success" };
        }
        catch (Exception ex)
        {
            return new ResultDto<SisSegUsuarioResponse>(null!) { Data = null, IsValid = false, Message = $"Error técnico: {ex.Message}" };
        }
    }

    public async Task<ResultDto<SisSegCacheResponse>> SaveUsuarioRolesAsync(SisSegUsrRolSaveCommand value, int usuarioSesion)
    {
        if (value.CodigoUsuario <= 0)
        {
            return new ResultDto<SisSegCacheResponse>(null!) { Data = null, IsValid = false, Message = "CodigoUsuario es requerido." };
        }

        if (!SupportDb.TryGetEmpresa(config, out int empresa, out string empresaMessage))
        {
            return new ResultDto<SisSegCacheResponse>(null!) { Data = null, IsValid = false, Message = empresaMessage };
        }

        using var cn = connectionDB.GetSisConnection();
        var open = await OpenAsync(cn, "guardar roles de usuario");
        if (!open.IsValid)
        {
            return new ResultDto<SisSegCacheResponse>(null!) { Data = null, IsValid = false, Message = open.Message };
        }

        var missingTables = await FindMissingSisTablesAsync(cn, RequiredSaveTables);
        if (missingTables.Count > 0)
        {
            return new ResultDto<SisSegCacheResponse>(null!)
            {
                Data = null,
                IsValid = false,
                Message = $"Faltan tablas requeridas en el esquema SIS: {string.Join(", ", missingTables)}. Ejecute Features/SisSeguridad/Sql/00_INSTALL_SIS_SEGURIDAD.sql conectado como SIS o cree/grante esas tablas en SIS."
            };
        }

        using var tx = cn.BeginTransaction();
        try
        {
            int usuarioUpd = value.UsuarioUpd > 0 ? value.UsuarioUpd : usuarioSesion;
            await SaveRolesAsync(cn, tx, value.CodigoUsuario, empresa, value.Roles.Distinct().ToList(), usuarioUpd);
            await SavePermisosAsync(cn, tx, value.CodigoUsuario, empresa, value.Permisos, usuarioUpd);
            await InsertAuditAsync(cn, tx, value.CodigoUsuario, empresa, "SAVE_USR_SEG", $"Roles={value.Roles.Count}; Permisos={value.Permisos.Count}", usuarioUpd);
            var response = await RegenerateCacheCoreAsync(cn, tx, value.CodigoUsuario, empresa, null);
            tx.Commit();

            return new ResultDto<SisSegCacheResponse>(response) { Data = response, IsValid = true, Message = "Seguridad de usuario guardada y cache regenerada." };
        }
        catch (Exception ex)
        {
            tx.Rollback();
            return new ResultDto<SisSegCacheResponse>(null!) { Data = null, IsValid = false, Message = FormatOracleError(ex) };
        }
    }

    public async Task<ResultDto<SisSegCloneUserResponse>> CloneUsuarioAsync(SisSegCloneUserCommand value, int usuarioSesion)
    {
        if (value.CodigoUsuarioOrigen <= 0)
        {
            return new ResultDto<SisSegCloneUserResponse>(null!) { Data = null, IsValid = false, Message = "CodigoUsuarioOrigen es requerido." };
        }

        if (value.CodigoUsuarioDestino is null && value.UsuarioDestino is null)
        {
            return new ResultDto<SisSegCloneUserResponse>(null!) { Data = null, IsValid = false, Message = "Debe indicar CodigoUsuarioDestino o UsuarioDestino." };
        }

        if (value.CodigoUsuarioDestino.HasValue && value.CodigoUsuarioDestino.Value == value.CodigoUsuarioOrigen)
        {
            return new ResultDto<SisSegCloneUserResponse>(null!) { Data = null, IsValid = false, Message = "El usuario origen y destino no pueden ser el mismo." };
        }

        if (!SupportDb.TryGetEmpresa(config, out int empresa, out string empresaMessage))
        {
            return new ResultDto<SisSegCloneUserResponse>(null!) { Data = null, IsValid = false, Message = empresaMessage };
        }

        using var cn = connectionDB.GetSisConnection();
        var open = await OpenAsync(cn, "clonar seguridad de usuario");
        if (!open.IsValid)
        {
            return new ResultDto<SisSegCloneUserResponse>(null!) { Data = null, IsValid = false, Message = open.Message };
        }

        var missingTables = await FindMissingSisTablesAsync(cn, RequiredSaveTables);
        if (missingTables.Count > 0)
        {
            return new ResultDto<SisSegCloneUserResponse>(null!)
            {
                Data = null,
                IsValid = false,
                Message = $"Faltan tablas requeridas en el esquema SIS: {string.Join(", ", missingTables)}. Ejecute Features/SisSeguridad/Sql/00_INSTALL_SIS_SEGURIDAD.sql conectado como SIS o cree/grante esas tablas en SIS."
            };
        }

        using var tx = cn.BeginTransaction();
        try
        {
            var origen = await ReadUsuarioAsync(cn, value.CodigoUsuarioOrigen, empresa);
            if (origen is null)
            {
                tx.Rollback();
                return new ResultDto<SisSegCloneUserResponse>(null!) { Data = null, IsValid = false, Message = "No se encontró el usuario origen." };
            }

            int usuarioUpd = value.UsuarioUpd > 0 ? value.UsuarioUpd : usuarioSesion;
            bool usuarioCreado = false;
            int codigoUsuarioDestino;
            SisSegUserData? destino;

            if (value.CodigoUsuarioDestino.HasValue)
            {
                codigoUsuarioDestino = value.CodigoUsuarioDestino.Value;
                destino = await ReadUsuarioAsync(cn, codigoUsuarioDestino, empresa);
                if (destino is null)
                {
                    tx.Rollback();
                    return new ResultDto<SisSegCloneUserResponse>(null!) { Data = null, IsValid = false, Message = "No se encontró el usuario destino." };
                }
            }
            else
            {
                codigoUsuarioDestino = await CreateCloneTargetUserAsync(cn, tx, value.UsuarioDestino!, empresa, usuarioUpd);
                destino = await ReadUsuarioAsync(cn, codigoUsuarioDestino, empresa);
                usuarioCreado = true;
            }

            if (destino is null)
            {
                tx.Rollback();
                return new ResultDto<SisSegCloneUserResponse>(null!) { Data = null, IsValid = false, Message = "No se pudo preparar el usuario destino." };
            }

            var rolesOrigen = await ReadUsuarioRolesAsync(cn, value.CodigoUsuarioOrigen, empresa);
            var permisosOrigen = await ReadUsuarioPermisosAsync(cn, value.CodigoUsuarioOrigen, empresa);

            if (value.SobrescribirAccesos)
            {
                await SaveRolesAsync(cn, tx, codigoUsuarioDestino, empresa, rolesOrigen.Select(x => x.CodigoRol).Distinct().ToList(), usuarioUpd);
                await SavePermisosAsync(cn, tx, codigoUsuarioDestino, empresa, permisosOrigen.Select(x => new SisSegUsrPermCommand(x.CodigoPerm, x.Tipo, x.Activo)).ToList(), usuarioUpd);
            }
            else
            {
                foreach (var rol in rolesOrigen)
                {
                    await UpsertUserRoleAsync(cn, tx, codigoUsuarioDestino, empresa, rol.CodigoRol, usuarioUpd);
                }

                foreach (var permiso in permisosOrigen)
                {
                    await UpsertUserPermAsync(cn, tx, codigoUsuarioDestino, empresa, permiso.CodigoPerm, permiso.Tipo, usuarioUpd);
                }
            }

            var cache = await RegenerateCacheCoreAsync(cn, tx, codigoUsuarioDestino, empresa, null);
            await InsertAuditAsync(
                cn,
                tx,
                codigoUsuarioDestino,
                empresa,
                "CLONAR_USR",
                $"Origen={value.CodigoUsuarioOrigen}; Destino={codigoUsuarioDestino}; Creado={usuarioCreado}; Sobrescribir={value.SobrescribirAccesos}; Roles={rolesOrigen.Count}; Excepciones={permisosOrigen.Count}",
                usuarioUpd);

            tx.Commit();

            var response = new SisSegCloneUserResponse(
                value.CodigoUsuarioOrigen,
                codigoUsuarioDestino,
                usuarioCreado,
                rolesOrigen.Count,
                permisosOrigen.Count,
                cache.JsonMenu
            );

            return new ResultDto<SisSegCloneUserResponse>(response)
            {
                Data = response,
                IsValid = true,
                Message = usuarioCreado
                    ? "Usuario creado, seguridad clonada y cache regenerada."
                    : "Seguridad clonada y cache regenerada."
            };
        }
        catch (Exception ex)
        {
            tx.Rollback();
            return new ResultDto<SisSegCloneUserResponse>(null!) { Data = null, IsValid = false, Message = FormatOracleError(ex) };
        }
    }

    public async Task<ResultDto<SisSegCacheResponse>> RegenerateCacheAsync(int codigoUsuario, string? codigoModulo, int usuarioAccion)
    {
        if (codigoUsuario <= 0)
        {
            return new ResultDto<SisSegCacheResponse>(null!) { Data = null, IsValid = false, Message = "CodigoUsuario es requerido." };
        }

        if (!SupportDb.TryGetEmpresa(config, out int empresa, out string empresaMessage))
        {
            return new ResultDto<SisSegCacheResponse>(null!) { Data = null, IsValid = false, Message = empresaMessage };
        }

        using var cn = connectionDB.GetSisConnection();
        var open = await OpenAsync(cn, "regenerar cache de menu");
        if (!open.IsValid)
        {
            return new ResultDto<SisSegCacheResponse>(null!) { Data = null, IsValid = false, Message = open.Message };
        }

        using var tx = cn.BeginTransaction();
        try
        {
            var response = await RegenerateCacheCoreAsync(cn, tx, codigoUsuario, empresa, codigoModulo);
            await InsertAuditAsync(cn, tx, codigoUsuario, empresa, "REGEN_CACHE", $"Modulo={codigoModulo ?? "TODOS"}", usuarioAccion);
            tx.Commit();
            return new ResultDto<SisSegCacheResponse>(response) { Data = response, IsValid = true, Message = "Cache de menu regenerada." };
        }
        catch (Exception ex)
        {
            tx.Rollback();
            return new ResultDto<SisSegCacheResponse>(null!) { Data = null, IsValid = false, Message = $"Error técnico: {ex.Message}" };
        }
    }

    public async Task<ResultDto<List<SisSegMigItemResponse>>> GetMigracionSugeridaAsync(string? codigoModulo)
    {
        if (!SupportDb.TryGetEmpresa(config, out int empresa, out string empresaMessage))
        {
            return new ResultDto<List<SisSegMigItemResponse>>(null!) { Data = null, IsValid = false, Message = empresaMessage };
        }

        using var cn = connectionDB.GetSisConnection();
        var open = await OpenAsync(cn, "migracion sugerida de seguridad");
        if (!open.IsValid)
        {
            return new ResultDto<List<SisSegMigItemResponse>>(null!) { Data = null, IsValid = false, Message = open.Message };
        }

        try
        {
            var list = await ReadMigrationItemsAsync(cn, empresa, codigoModulo);

            return new ResultDto<List<SisSegMigItemResponse>>(list)
            {
                Data = list,
                IsValid = true,
                Message = "Success",
                CantidadRegistros = list.Count
            };
        }
        catch (Exception ex)
        {
            return new ResultDto<List<SisSegMigItemResponse>>(null!) { Data = null, IsValid = false, Message = $"Error técnico: {ex.Message}" };
        }
    }

    public async Task<ResultDto<SisSegCacheResponse>> ApplyMigracionSugeridaAsync(SisSegMigApplyCommand value, int usuarioSesion)
    {
        if (value.CodigoUsuario <= 0)
        {
            return new ResultDto<SisSegCacheResponse>(null!) { Data = null, IsValid = false, Message = "CodigoUsuario es requerido." };
        }

        if (!SupportDb.TryGetEmpresa(config, out int empresa, out string empresaMessage))
        {
            return new ResultDto<SisSegCacheResponse>(null!) { Data = null, IsValid = false, Message = empresaMessage };
        }

        using var cn = connectionDB.GetSisConnection();
        var open = await OpenAsync(cn, "aplicar migracion sugerida de seguridad");
        if (!open.IsValid)
        {
            return new ResultDto<SisSegCacheResponse>(null!) { Data = null, IsValid = false, Message = open.Message };
        }

        using var tx = cn.BeginTransaction();
        try
        {
            var usuario = await ReadUsuarioAsync(cn, value.CodigoUsuario, empresa);
            if (usuario is null)
            {
                tx.Rollback();
                return new ResultDto<SisSegCacheResponse>(null!) { Data = null, IsValid = false, Message = "No se encontró el usuario indicado." };
            }

            var migrationItems = await ReadMigrationItemsAsync(cn, empresa, value.CodigoModulo, value.CodigoUsuario);
            var roles = migrationItems
                .Where(x => x.JsonValido)
                .SelectMany(x => x.RolesSugeridos)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();
            var exceptions = migrationItems
                .Where(x => x.JsonValido)
                .SelectMany(x => x.PermisosExcepcionSugeridos)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(x => x)
                .ToList();

            if (roles.Count == 0 && exceptions.Count == 0 && !usuario.IsSuperuser)
            {
                tx.Rollback();
                return new ResultDto<SisSegCacheResponse>(null!) { Data = null, IsValid = false, Message = "No se encontraron roles sugeridos para aplicar." };
            }

            int usuarioUpd = value.UsuarioUpd > 0 ? value.UsuarioUpd : usuarioSesion;
            foreach (var roleKey in roles)
            {
                await UpsertUserRoleByKeyAsync(cn, tx, value.CodigoUsuario, empresa, roleKey, usuarioUpd);
            }

            foreach (var permissionKey in exceptions)
            {
                await UpsertUserPermByKeyAsync(cn, tx, value.CodigoUsuario, empresa, permissionKey, "ALLOW", usuarioUpd);
            }

            var response = await RegenerateCacheCoreAsync(cn, tx, value.CodigoUsuario, empresa, CacheModuleForLegacy(value.CodigoModulo));
            await InsertAuditAsync(cn, tx, value.CodigoUsuario, empresa, "MIGRAR_USR", $"Roles={roles.Count}; Excepciones={exceptions.Count}; Modulo={value.CodigoModulo ?? "TODOS"}", usuarioUpd);
            tx.Commit();

            return new ResultDto<SisSegCacheResponse>(response)
            {
                Data = response,
                IsValid = true,
                Message = $"Migracion sugerida aplicada. Roles: {string.Join(", ", roles)}. Excepciones: {exceptions.Count}."
            };
        }
        catch (Exception ex)
        {
            tx.Rollback();
            return new ResultDto<SisSegCacheResponse>(null!) { Data = null, IsValid = false, Message = $"Error técnico: {ex.Message}" };
        }
    }

    public async Task<ResultDto<SisSegMigBulkResponse>> ApplyMigracionMasivaAsync(SisSegMigBulkCommand value, int usuarioSesion)
    {
        if (!value.Confirmar)
        {
            return new ResultDto<SisSegMigBulkResponse>(null!)
            {
                Data = null,
                IsValid = false,
                Message = "Debe enviar confirmar = true para aplicar migracion masiva."
            };
        }

        if (!SupportDb.TryGetEmpresa(config, out int empresa, out string empresaMessage))
        {
            return new ResultDto<SisSegMigBulkResponse>(null!) { Data = null, IsValid = false, Message = empresaMessage };
        }

        using var cn = connectionDB.GetSisConnection();
        var open = await OpenAsync(cn, "aplicar migracion masiva de seguridad");
        if (!open.IsValid)
        {
            return new ResultDto<SisSegMigBulkResponse>(null!) { Data = null, IsValid = false, Message = open.Message };
        }

        using var tx = cn.BeginTransaction();
        try
        {
            int usuarioUpd = value.UsuarioUpd > 0 ? value.UsuarioUpd : usuarioSesion;
            var items = await ReadMigrationItemsAsync(cn, empresa, value.CodigoModulo);
            int users = 0;
            int rolesApplied = 0;
            int exceptionsApplied = 0;
            var messages = new List<string>();

            foreach (var userGroup in items.Where(x => x.JsonValido).GroupBy(x => x.CodigoUsuario))
            {
                var usuario = await ReadUsuarioAsync(cn, userGroup.Key, empresa);
                if (usuario is null)
                {
                    messages.Add($"Usuario {userGroup.Key}: no encontrado.");
                    continue;
                }

                var roles = userGroup
                    .SelectMany(x => x.RolesSugeridos)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x)
                    .ToList();
                var exceptions = userGroup
                    .SelectMany(x => x.PermisosExcepcionSugeridos)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x)
                    .ToList();

                if (roles.Count == 0 && exceptions.Count == 0 && !usuario.IsSuperuser)
                {
                    messages.Add($"Usuario {userGroup.Key}: sin roles ni excepciones sugeridas.");
                    continue;
                }

                foreach (var roleKey in roles)
                {
                    await UpsertUserRoleByKeyAsync(cn, tx, userGroup.Key, empresa, roleKey, usuarioUpd);
                    rolesApplied++;
                }

                foreach (var permissionKey in exceptions)
                {
                    await UpsertUserPermByKeyAsync(cn, tx, userGroup.Key, empresa, permissionKey, "ALLOW", usuarioUpd);
                    exceptionsApplied++;
                }

                await RegenerateCacheCoreAsync(cn, tx, userGroup.Key, empresa, CacheModuleForLegacy(value.CodigoModulo));
                await InsertAuditAsync(cn, tx, userGroup.Key, empresa, "MIGRAR_MASIVA", $"Roles={roles.Count}; Excepciones={exceptions.Count}; Modulo={value.CodigoModulo ?? "TODOS"}", usuarioUpd);
                users++;
            }

            tx.Commit();
            var response = new SisSegMigBulkResponse(users, rolesApplied, exceptionsApplied, messages);
            return new ResultDto<SisSegMigBulkResponse>(response)
            {
                Data = response,
                IsValid = true,
                Message = "Migracion masiva aplicada."
            };
        }
        catch (Exception ex)
        {
            tx.Rollback();
            return new ResultDto<SisSegMigBulkResponse>(null!) { Data = null, IsValid = false, Message = $"Error técnico: {ex.Message}" };
        }
    }

    public async Task<ResultDto<SisSegMigResumenResponse>> GetResumenMigracionAsync(string? codigoModulo)
    {
        if (!SupportDb.TryGetEmpresa(config, out int empresa, out string empresaMessage))
        {
            return new ResultDto<SisSegMigResumenResponse>(null!) { Data = null, IsValid = false, Message = empresaMessage };
        }

        using var cn = connectionDB.GetSisConnection();
        var open = await OpenAsync(cn, "resumen de migracion de seguridad");
        if (!open.IsValid)
        {
            return new ResultDto<SisSegMigResumenResponse>(null!) { Data = null, IsValid = false, Message = open.Message };
        }

        try
        {
            var items = await ReadMigrationItemsAsync(cn, empresa, codigoModulo);
            var legacyUsers = items.Select(x => x.CodigoUsuario).Distinct().Count();
            var invalid = items.Count(x => !x.JsonValido);
            var normalized = await ReadNormalizedSummaryAsync(cn, empresa, codigoModulo);
            var pending = items
                .Where(x => x.JsonValido)
                .Select(x => x.CodigoUsuario)
                .Distinct()
                .Count() - normalized.UsuariosNormalizados;

            var response = new SisSegMigResumenResponse(
                legacyUsers,
                normalized.UsuariosNormalizados,
                items.Count,
                normalized.RolesNormalizados,
                normalized.ExcepcionesNormalizadas,
                invalid,
                Math.Max(pending, 0)
            );

            return new ResultDto<SisSegMigResumenResponse>(response) { Data = response, IsValid = true, Message = "Success" };
        }
        catch (Exception ex)
        {
            return new ResultDto<SisSegMigResumenResponse>(null!) { Data = null, IsValid = false, Message = $"Error técnico: {ex.Message}" };
        }
    }

    private static async Task<ResultDto<string>> OpenAsync(OracleConnection cn, string contexto)
    {
        try
        {
            await cn.OpenAsync();
            return new ResultDto<string>("OK") { IsValid = true, Message = "Success" };
        }
        catch (Exception ex)
        {
            return new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = $"Error técnico al abrir conexión SIS para {contexto}: {ex.Message}" };
        }
    }

    private static async Task<List<SisSegModuloResponse>> ReadModulosAsync(OracleConnection cn)
    {
        using var cmd = new OracleCommand(@"
            SELECT CODIGO_MOD, CODIGO, NOMBRE, NVL(ICONO, '') ICONO, ORDEN, ACTIVO
              FROM SIS.OSS_MOD
             ORDER BY ORDEN, NOMBRE", cn)
        {
            BindByName = true
        };

        var list = new List<SisSegModuloResponse>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new SisSegModuloResponse(
                reader.SafeGetInt32("CODIGO_MOD"),
                reader.SafeGetString("CODIGO"),
                reader.SafeGetString("NOMBRE"),
                reader.SafeGetString("ICONO"),
                reader.SafeGetInt32("ORDEN"),
                reader.SafeGetInt32("ACTIVO") == 1
            ));
        }

        return list;
    }

    private static async Task<List<SisSegMenuResponse>> ReadMenusAsync(OracleConnection cn)
    {
        using var cmd = new OracleCommand(@"
            SELECT CODIGO_MENU, CODIGO_MOD, CODIGO_PADRE, TITULO, NVL(PATH, '') PATH, NVL(ICONO, '') ICONO, ORDEN, ACTIVO
              FROM SIS.OSS_MENU
             ORDER BY CODIGO_PADRE NULLS FIRST, ORDEN, TITULO", cn)
        {
            BindByName = true
        };

        var list = new List<SisSegMenuResponse>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new SisSegMenuResponse(
                reader.SafeGetInt32("CODIGO_MENU"),
                reader.SafeGetInt32("CODIGO_MOD"),
                NullableInt(reader, "CODIGO_PADRE"),
                reader.SafeGetString("TITULO"),
                reader.SafeGetString("PATH"),
                reader.SafeGetString("ICONO"),
                reader.SafeGetInt32("ORDEN"),
                reader.SafeGetInt32("ACTIVO") == 1
            ));
        }

        return list;
    }

    private static async Task<List<SisSegPermResponse>> ReadPermisosAsync(OracleConnection cn)
    {
        using var cmd = new OracleCommand(@"
            SELECT CODIGO_PERM, CODIGO_MOD, CLAVE, NOMBRE, NVL(DESCRIPCION, '') DESCRIPCION, ACTIVO
              FROM SIS.OSS_PERM
             ORDER BY CLAVE", cn)
        {
            BindByName = true
        };

        return await ReadPermisosFromCommandAsync(cmd);
    }

    private static async Task<List<SisSegRolResponse>> ReadRolesAsync(OracleConnection cn)
    {
        using var cmd = new OracleCommand(@"
            SELECT CODIGO_ROL, CODIGO_MOD, CLAVE, NOMBRE, NVL(DESCRIPCION, '') DESCRIPCION, ACTIVO
              FROM SIS.OSS_ROL
             ORDER BY NOMBRE", cn)
        {
            BindByName = true
        };

        var list = new List<SisSegRolResponse>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(MapRol(reader));
        }

        return list;
    }

    private static async Task<List<SisSegRolPermResponse>> ReadRolPermisosAsync(OracleConnection cn)
    {
        using var cmd = new OracleCommand(@"
            SELECT CODIGO_ROL, CODIGO_PERM
              FROM SIS.OSS_ROL_PERM
             ORDER BY CODIGO_ROL, CODIGO_PERM", cn)
        {
            BindByName = true
        };

        var list = new List<SisSegRolPermResponse>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new SisSegRolPermResponse(reader.SafeGetInt32("CODIGO_ROL"), reader.SafeGetInt32("CODIGO_PERM")));
        }

        return list;
    }

    private static async Task<List<SisSegRolMenuResponse>> ReadRolMenusAsync(OracleConnection cn)
    {
        using var cmd = new OracleCommand(@"
            SELECT CODIGO_ROL, CODIGO_MENU
              FROM SIS.OSS_ROL_MENU
             ORDER BY CODIGO_ROL, CODIGO_MENU", cn)
        {
            BindByName = true
        };

        var list = new List<SisSegRolMenuResponse>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new SisSegRolMenuResponse(reader.SafeGetInt32("CODIGO_ROL"), reader.SafeGetInt32("CODIGO_MENU")));
        }

        return list;
    }

    private static async Task<SisSegUserData?> ReadUsuarioAsync(OracleConnection cn, int codigoUsuario, int empresa)
    {
        using var cmd = new OracleCommand(@"
            SELECT CODIGO_USUARIO, USUARIO, LOGIN, NVL(IS_SUPERUSER, 0) IS_SUPERUSER
              FROM SIS.SIS_USUARIOS
             WHERE CODIGO_USUARIO = :p_CODIGO_USUARIO
               AND CODIGO_EMPRESA = :p_CODIGO_EMPRESA", cn)
        {
            BindByName = true
        };
        cmd.Parameters.Add("p_CODIGO_USUARIO", OracleDbType.Int32).Value = codigoUsuario;
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;

        using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new SisSegUserData(
            reader.SafeGetInt32("CODIGO_USUARIO"),
            reader.SafeGetString("USUARIO"),
            reader.SafeGetString("LOGIN"),
            reader.SafeGetInt32("IS_SUPERUSER") == 1
        );
    }

    private static async Task<List<SisSegRolResponse>> ReadUsuarioRolesAsync(OracleConnection cn, int codigoUsuario, int empresa)
    {
        using var cmd = new OracleCommand(@"
            SELECT r.CODIGO_ROL, r.CODIGO_MOD, r.CLAVE, r.NOMBRE, NVL(r.DESCRIPCION, '') DESCRIPCION, r.ACTIVO
              FROM SIS.OSS_USR_ROL ur
              JOIN SIS.OSS_ROL r ON r.CODIGO_ROL = ur.CODIGO_ROL
             WHERE ur.CODIGO_USUARIO = :p_CODIGO_USUARIO
               AND ur.CODIGO_EMPRESA = :p_CODIGO_EMPRESA
               AND ur.ACTIVO = 1
             ORDER BY r.NOMBRE", cn)
        {
            BindByName = true
        };
        cmd.Parameters.Add("p_CODIGO_USUARIO", OracleDbType.Int32).Value = codigoUsuario;
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;

        var list = new List<SisSegRolResponse>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(MapRol(reader));
        }

        return list;
    }

    private static async Task<List<SisSegUsrPermResponse>> ReadUsuarioPermisosAsync(OracleConnection cn, int codigoUsuario, int empresa)
    {
        using var cmd = new OracleCommand(@"
            SELECT p.CODIGO_PERM, p.CODIGO_MOD, p.CLAVE, p.NOMBRE, NVL(p.DESCRIPCION, '') DESCRIPCION, up.ACTIVO, up.TIPO
              FROM SIS.OSS_USR_PERM up
              JOIN SIS.OSS_PERM p ON p.CODIGO_PERM = up.CODIGO_PERM
             WHERE up.CODIGO_USUARIO = :p_CODIGO_USUARIO
               AND up.CODIGO_EMPRESA = :p_CODIGO_EMPRESA
               AND up.ACTIVO = 1
             ORDER BY p.CLAVE", cn)
        {
            BindByName = true
        };
        cmd.Parameters.Add("p_CODIGO_USUARIO", OracleDbType.Int32).Value = codigoUsuario;
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;

        var list = new List<SisSegUsrPermResponse>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new SisSegUsrPermResponse(
                reader.SafeGetInt32("CODIGO_PERM"),
                reader.SafeGetInt32("CODIGO_MOD"),
                reader.SafeGetString("CLAVE"),
                reader.SafeGetString("NOMBRE"),
                reader.SafeGetString("DESCRIPCION"),
                reader.SafeGetInt32("ACTIVO") == 1,
                reader.SafeGetString("TIPO")
            ));
        }

        return list;
    }

    private static async Task<List<SisSegPermResponse>> ReadEffectivePermisosAsync(OracleConnection cn, int codigoUsuario, int empresa, bool isSuperuser)
    {
        if (isSuperuser)
        {
            using var allCmd = new OracleCommand(@"
                SELECT CODIGO_PERM, CODIGO_MOD, CLAVE, NOMBRE, NVL(DESCRIPCION, '') DESCRIPCION, ACTIVO
                  FROM SIS.OSS_PERM
                 WHERE ACTIVO = 1
                 ORDER BY CLAVE", cn)
            {
                BindByName = true
            };
            return await ReadPermisosFromCommandAsync(allCmd);
        }

        using var cmd = new OracleCommand(@"
            SELECT DISTINCT p.CODIGO_PERM, p.CODIGO_MOD, p.CLAVE, p.NOMBRE, NVL(p.DESCRIPCION, '') DESCRIPCION, p.ACTIVO
              FROM SIS.OSS_PERM p
             WHERE p.ACTIVO = 1
               AND (
                   EXISTS (
                       SELECT 1
                         FROM SIS.OSS_USR_ROL ur
                         JOIN SIS.OSS_ROL_PERM rp ON rp.CODIGO_ROL = ur.CODIGO_ROL
                        WHERE ur.CODIGO_USUARIO = :p_CODIGO_USUARIO
                          AND ur.CODIGO_EMPRESA = :p_CODIGO_EMPRESA
                          AND ur.ACTIVO = 1
                          AND rp.CODIGO_PERM = p.CODIGO_PERM
                   )
                   OR EXISTS (
                       SELECT 1
                         FROM SIS.OSS_USR_PERM up
                        WHERE up.CODIGO_USUARIO = :p_CODIGO_USUARIO
                          AND up.CODIGO_EMPRESA = :p_CODIGO_EMPRESA
                          AND up.ACTIVO = 1
                          AND up.TIPO = 'ALLOW'
                          AND up.CODIGO_PERM = p.CODIGO_PERM
                   )
               )
               AND NOT EXISTS (
                   SELECT 1
                     FROM SIS.OSS_USR_PERM up
                    WHERE up.CODIGO_USUARIO = :p_CODIGO_USUARIO
                      AND up.CODIGO_EMPRESA = :p_CODIGO_EMPRESA
                      AND up.ACTIVO = 1
                      AND up.TIPO = 'DENY'
                      AND up.CODIGO_PERM = p.CODIGO_PERM
               )
             ORDER BY p.CLAVE", cn)
        {
            BindByName = true
        };
        cmd.Parameters.Add("p_CODIGO_USUARIO", OracleDbType.Int32).Value = codigoUsuario;
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;

        return await ReadPermisosFromCommandAsync(cmd);
    }

    private static async Task<List<SisSegPermResponse>> ReadPermisosFromCommandAsync(OracleCommand cmd)
    {
        var list = new List<SisSegPermResponse>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new SisSegPermResponse(
                reader.SafeGetInt32("CODIGO_PERM"),
                reader.SafeGetInt32("CODIGO_MOD"),
                reader.SafeGetString("CLAVE"),
                reader.SafeGetString("NOMBRE"),
                reader.SafeGetString("DESCRIPCION"),
                reader.SafeGetInt32("ACTIVO") == 1
            ));
        }

        return list;
    }

    private static async Task<string> ReadLegacyJsonMenuAsync(OracleConnection cn, int codigoUsuario)
    {
        using var cmd = new OracleCommand(@"
            SELECT JSON_MENU
              FROM SIS.OSS_USUARIO_ROL
             WHERE CODIGO_USUARIO = :p_CODIGO_USUARIO
             ORDER BY CODIGO_USUARIO_ROL", cn)
        {
            BindByName = true
        };
        cmd.Parameters.Add("p_CODIGO_USUARIO", OracleDbType.Int32).Value = codigoUsuario;

        var root = new JsonArray();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var json = ReadClob(reader, "JSON_MENU");
            var parsed = ParseJsonNode(json);
            if (parsed is JsonArray array)
            {
                foreach (var item in array)
                {
                    root.Add(item?.DeepClone());
                }
            }
            else if (parsed is not null)
            {
                root.Add(parsed.DeepClone());
            }
        }

        return root.ToJsonString();
    }

    private async Task<ResultDto<string>> SaveRolRelationAsync(int codigoRol, List<int> items, int usuarioUpdRequest, int usuarioSesion, string contexto, string tableName, string columnName, string auditAction)
    {
        if (codigoRol <= 0)
        {
            return new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = "CodigoRol es requerido." };
        }

        if (!SupportDb.TryGetEmpresa(config, out int empresa, out string empresaMessage))
        {
            return new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = empresaMessage };
        }

        int usuarioUpd = usuarioUpdRequest > 0 ? usuarioUpdRequest : usuarioSesion;
        using var cn = connectionDB.GetSisConnection();
        var open = await OpenAsync(cn, contexto);
        if (!open.IsValid)
        {
            return new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = open.Message };
        }

        using var tx = cn.BeginTransaction();
        try
        {
            using (var delete = new OracleCommand($"DELETE FROM {tableName} WHERE CODIGO_ROL = :p_CODIGO_ROL", cn)
            {
                BindByName = true,
                Transaction = tx
            })
            {
                delete.Parameters.Add("p_CODIGO_ROL", OracleDbType.Int32).Value = codigoRol;
                await delete.ExecuteNonQueryAsync();
            }

            foreach (int item in items.Where(x => x > 0).Distinct())
            {
                using var insert = new OracleCommand($@"
                    INSERT INTO {tableName} (CODIGO_ROL, {columnName}, USUARIO_INS, FECHA_INS)
                    VALUES (:p_CODIGO_ROL, :p_ITEM, :p_USUARIO_INS, SYSDATE)", cn)
                {
                    BindByName = true,
                    Transaction = tx
                };
                insert.Parameters.Add("p_CODIGO_ROL", OracleDbType.Int32).Value = codigoRol;
                insert.Parameters.Add("p_ITEM", OracleDbType.Int32).Value = item;
                insert.Parameters.Add("p_USUARIO_INS", OracleDbType.Int32).Value = usuarioUpd;
                await insert.ExecuteNonQueryAsync();
            }

            await InsertAuditAsync(cn, tx, null, empresa, auditAction, $"Rol={codigoRol}; Items={items.Count}", usuarioUpd);
            tx.Commit();

            return new ResultDto<string>("OK") { Data = "OK", IsValid = true, Message = "Relaciones del rol guardadas correctamente." };
        }
        catch (Exception ex)
        {
            tx.Rollback();
            return new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = $"Error técnico: {ex.Message}" };
        }
    }

    private static async Task<int> SaveRolCoreAsync(OracleConnection cn, OracleTransaction tx, SisSegRolSaveCommand value, int usuarioUpd)
    {
        int codigoRol = value.CodigoRol > 0 ? value.CodigoRol : await NextNumberAsync(cn, tx, "SIS.OSS_ROL", "CODIGO_ROL");
        using var cmd = new OracleCommand(@"
            MERGE INTO SIS.OSS_ROL t
            USING (
                SELECT :p_CODIGO_ROL CODIGO_ROL,
                       :p_CODIGO_MOD CODIGO_MOD,
                       :p_CLAVE CLAVE,
                       :p_NOMBRE NOMBRE,
                       :p_DESCRIPCION DESCRIPCION,
                       :p_ACTIVO ACTIVO
                  FROM dual
            ) s
               ON (t.CODIGO_ROL = s.CODIGO_ROL)
             WHEN MATCHED THEN UPDATE SET CODIGO_MOD = s.CODIGO_MOD, CLAVE = s.CLAVE, NOMBRE = s.NOMBRE, DESCRIPCION = s.DESCRIPCION, ACTIVO = s.ACTIVO, USUARIO_UPD = :p_USUARIO_UPD, FECHA_UPD = SYSDATE
             WHEN NOT MATCHED THEN INSERT (CODIGO_ROL, CODIGO_MOD, CLAVE, NOMBRE, DESCRIPCION, ACTIVO, USUARIO_INS, FECHA_INS)
               VALUES (s.CODIGO_ROL, s.CODIGO_MOD, s.CLAVE, s.NOMBRE, s.DESCRIPCION, s.ACTIVO, :p_USUARIO_UPD, SYSDATE)", cn)
        {
            BindByName = true,
            Transaction = tx
        };
        cmd.Parameters.Add("p_CODIGO_ROL", OracleDbType.Int32).Value = codigoRol;
        cmd.Parameters.Add("p_CODIGO_MOD", OracleDbType.Int32).Value = value.CodigoMod;
        cmd.Parameters.Add("p_CLAVE", OracleDbType.Varchar2).Value = value.Clave.Trim().ToUpperInvariant();
        cmd.Parameters.Add("p_NOMBRE", OracleDbType.Varchar2).Value = value.Nombre.Trim();
        cmd.Parameters.Add("p_DESCRIPCION", OracleDbType.Varchar2).Value = DbValue(value.Descripcion);
        cmd.Parameters.Add("p_ACTIVO", OracleDbType.Int32).Value = value.Activo ? 1 : 0;
        cmd.Parameters.Add("p_USUARIO_UPD", OracleDbType.Int32).Value = usuarioUpd;
        await cmd.ExecuteNonQueryAsync();

        return codigoRol;
    }

    private static async Task<int> SaveMenuCoreAsync(OracleConnection cn, OracleTransaction tx, SisSegMenuSaveCommand value, int usuarioUpd)
    {
        int codigoMenu = value.CodigoMenu > 0 ? value.CodigoMenu : await NextNumberAsync(cn, tx, "SIS.OSS_MENU", "CODIGO_MENU");
        using var cmd = new OracleCommand(@"
            MERGE INTO SIS.OSS_MENU t
            USING (
                SELECT :p_CODIGO_MENU CODIGO_MENU,
                       :p_CODIGO_MOD CODIGO_MOD,
                       :p_CODIGO_PADRE CODIGO_PADRE,
                       :p_TITULO TITULO,
                       :p_PATH PATH,
                       :p_ICONO ICONO,
                       :p_ORDEN ORDEN,
                       :p_ACTIVO ACTIVO
                  FROM dual
            ) s
               ON (t.CODIGO_MENU = s.CODIGO_MENU)
             WHEN MATCHED THEN UPDATE SET CODIGO_MOD = s.CODIGO_MOD, CODIGO_PADRE = s.CODIGO_PADRE, TITULO = s.TITULO, PATH = s.PATH, ICONO = s.ICONO, ORDEN = s.ORDEN, ACTIVO = s.ACTIVO, USUARIO_UPD = :p_USUARIO_UPD, FECHA_UPD = SYSDATE
             WHEN NOT MATCHED THEN INSERT (CODIGO_MENU, CODIGO_MOD, CODIGO_PADRE, TITULO, PATH, ICONO, ORDEN, ACTIVO, USUARIO_INS, FECHA_INS)
               VALUES (s.CODIGO_MENU, s.CODIGO_MOD, s.CODIGO_PADRE, s.TITULO, s.PATH, s.ICONO, s.ORDEN, s.ACTIVO, :p_USUARIO_UPD, SYSDATE)", cn)
        {
            BindByName = true,
            Transaction = tx
        };
        cmd.Parameters.Add("p_CODIGO_MENU", OracleDbType.Int32).Value = codigoMenu;
        cmd.Parameters.Add("p_CODIGO_MOD", OracleDbType.Int32).Value = value.CodigoMod;
        cmd.Parameters.Add("p_CODIGO_PADRE", OracleDbType.Int32).Value = DbValue(value.CodigoPadre);
        cmd.Parameters.Add("p_TITULO", OracleDbType.Varchar2).Value = value.Titulo.Trim();
        cmd.Parameters.Add("p_PATH", OracleDbType.Varchar2).Value = DbValue(value.Path);
        cmd.Parameters.Add("p_ICONO", OracleDbType.Varchar2).Value = DbValue(value.Icono);
        cmd.Parameters.Add("p_ORDEN", OracleDbType.Int32).Value = value.Orden;
        cmd.Parameters.Add("p_ACTIVO", OracleDbType.Int32).Value = value.Activo ? 1 : 0;
        cmd.Parameters.Add("p_USUARIO_UPD", OracleDbType.Int32).Value = usuarioUpd;
        await cmd.ExecuteNonQueryAsync();

        return codigoMenu;
    }

    private static async Task SaveRolesAsync(OracleConnection cn, OracleTransaction tx, int codigoUsuario, int empresa, List<int> roles, int usuarioUpd)
    {
        using (var deactivate = new OracleCommand(@"
            UPDATE SIS.OSS_USR_ROL
               SET ACTIVO = 0,
                   USUARIO_UPD = :p_USUARIO_UPD,
                   FECHA_UPD = SYSDATE
             WHERE CODIGO_USUARIO = :p_CODIGO_USUARIO
               AND CODIGO_EMPRESA = :p_CODIGO_EMPRESA", cn)
        {
            BindByName = true,
            Transaction = tx
        })
        {
            deactivate.Parameters.Add("p_USUARIO_UPD", OracleDbType.Int32).Value = usuarioUpd;
            deactivate.Parameters.Add("p_CODIGO_USUARIO", OracleDbType.Int32).Value = codigoUsuario;
            deactivate.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
            await deactivate.ExecuteNonQueryAsync();
        }

        foreach (int codigoRol in roles.Where(x => x > 0))
        {
            using var cmd = new OracleCommand(@"
                MERGE INTO SIS.OSS_USR_ROL t
                USING (
                    SELECT :p_CODIGO_USUARIO CODIGO_USUARIO,
                           :p_CODIGO_ROL CODIGO_ROL,
                           :p_CODIGO_EMPRESA CODIGO_EMPRESA
                      FROM dual
                ) s
                   ON (t.CODIGO_USUARIO = s.CODIGO_USUARIO AND t.CODIGO_ROL = s.CODIGO_ROL AND t.CODIGO_EMPRESA = s.CODIGO_EMPRESA)
                 WHEN MATCHED THEN UPDATE SET ACTIVO = 1, USUARIO_UPD = :p_USUARIO_UPD, FECHA_UPD = SYSDATE
                 WHEN NOT MATCHED THEN INSERT (CODIGO_USR_ROL, CODIGO_USUARIO, CODIGO_ROL, CODIGO_EMPRESA, ACTIVO, USUARIO_INS, FECHA_INS)
                   VALUES ((SELECT NVL(MAX(CODIGO_USR_ROL), 0) + 1 FROM SIS.OSS_USR_ROL), s.CODIGO_USUARIO, s.CODIGO_ROL, s.CODIGO_EMPRESA, 1, :p_USUARIO_UPD, SYSDATE)", cn)
            {
                BindByName = true,
                Transaction = tx
            };
            cmd.Parameters.Add("p_CODIGO_USUARIO", OracleDbType.Int32).Value = codigoUsuario;
            cmd.Parameters.Add("p_CODIGO_ROL", OracleDbType.Int32).Value = codigoRol;
            cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
            cmd.Parameters.Add("p_USUARIO_UPD", OracleDbType.Int32).Value = usuarioUpd;
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private static async Task SavePermisosAsync(OracleConnection cn, OracleTransaction tx, int codigoUsuario, int empresa, List<SisSegUsrPermCommand> permisos, int usuarioUpd)
    {
        using (var deactivate = new OracleCommand(@"
            UPDATE SIS.OSS_USR_PERM
               SET ACTIVO = 0,
                   USUARIO_UPD = :p_USUARIO_UPD,
                   FECHA_UPD = SYSDATE
             WHERE CODIGO_USUARIO = :p_CODIGO_USUARIO
               AND CODIGO_EMPRESA = :p_CODIGO_EMPRESA", cn)
        {
            BindByName = true,
            Transaction = tx
        })
        {
            deactivate.Parameters.Add("p_USUARIO_UPD", OracleDbType.Int32).Value = usuarioUpd;
            deactivate.Parameters.Add("p_CODIGO_USUARIO", OracleDbType.Int32).Value = codigoUsuario;
            deactivate.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
            await deactivate.ExecuteNonQueryAsync();
        }

        foreach (var permiso in permisos.Where(x => x.CodigoPerm > 0))
        {
            var tipo = string.Equals(permiso.Tipo, "DENY", StringComparison.OrdinalIgnoreCase) ? "DENY" : "ALLOW";
            using var cmd = new OracleCommand(@"
                MERGE INTO SIS.OSS_USR_PERM t
                USING (
                    SELECT :p_CODIGO_USUARIO CODIGO_USUARIO,
                           :p_CODIGO_PERM CODIGO_PERM,
                           :p_CODIGO_EMPRESA CODIGO_EMPRESA
                      FROM dual
                ) s
                   ON (t.CODIGO_USUARIO = s.CODIGO_USUARIO AND t.CODIGO_PERM = s.CODIGO_PERM AND t.CODIGO_EMPRESA = s.CODIGO_EMPRESA)
                 WHEN MATCHED THEN UPDATE SET TIPO = :p_TIPO, ACTIVO = :p_ACTIVO, USUARIO_UPD = :p_USUARIO_UPD, FECHA_UPD = SYSDATE
                 WHEN NOT MATCHED THEN INSERT (CODIGO_USR_PERM, CODIGO_USUARIO, CODIGO_PERM, CODIGO_EMPRESA, TIPO, ACTIVO, USUARIO_INS, FECHA_INS)
                   VALUES ((SELECT NVL(MAX(CODIGO_USR_PERM), 0) + 1 FROM SIS.OSS_USR_PERM), s.CODIGO_USUARIO, s.CODIGO_PERM, s.CODIGO_EMPRESA, :p_TIPO, :p_ACTIVO, :p_USUARIO_UPD, SYSDATE)", cn)
            {
                BindByName = true,
                Transaction = tx
            };
            cmd.Parameters.Add("p_CODIGO_USUARIO", OracleDbType.Int32).Value = codigoUsuario;
            cmd.Parameters.Add("p_CODIGO_PERM", OracleDbType.Int32).Value = permiso.CodigoPerm;
            cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
            cmd.Parameters.Add("p_TIPO", OracleDbType.Varchar2).Value = tipo;
            cmd.Parameters.Add("p_ACTIVO", OracleDbType.Int32).Value = permiso.Activo ? 1 : 0;
            cmd.Parameters.Add("p_USUARIO_UPD", OracleDbType.Int32).Value = usuarioUpd;
            await cmd.ExecuteNonQueryAsync();
        }
    }

    private static async Task<int> CreateCloneTargetUserAsync(OracleConnection cn, OracleTransaction tx, SisSegCloneUserData value, int empresa, int usuarioUpd)
    {
        if (string.IsNullOrWhiteSpace(value.Usuario) || string.IsNullOrWhiteSpace(value.Login) || string.IsNullOrWhiteSpace(value.Clave))
        {
            throw new InvalidOperationException("UsuarioDestino requiere usuario, login y clave.");
        }

        int codigoUsuario = await NextNumberAsync(cn, tx, "SIS.SIS_USUARIOS", "CODIGO_USUARIO");
        using var cmd = new OracleCommand(@"
            INSERT INTO SIS.SIS_USUARIOS (
                CODIGO_USUARIO, USUARIO, LOGIN, PASSWORD, CEDULA, STATUS,
                EMAIL, RECIBE_EMAIL, ES_ANALISTA_SOPORTE, ES_ANALISTA_CNT,
                ES_ADMIN_CNT, IS_SUPERUSER, USUARIO_INS, FECHA_INS, CODIGO_EMPRESA
            ) VALUES (
                :p_CODIGO_USUARIO, :p_USUARIO, :p_LOGIN, SIS.SIS_ENCRYPTED(:p_CLAVE), :p_CEDULA, 'A',
                :p_EMAIL, :p_RECIBE_EMAIL, 0, 0,
                0, 0, :p_USUARIO_INS, SYSDATE, :p_CODIGO_EMPRESA
            )", cn)
        {
            BindByName = true,
            Transaction = tx
        };
        cmd.Parameters.Add("p_CODIGO_USUARIO", OracleDbType.Int32).Value = codigoUsuario;
        cmd.Parameters.Add("p_USUARIO", OracleDbType.Varchar2).Value = value.Usuario.Trim();
        cmd.Parameters.Add("p_LOGIN", OracleDbType.Varchar2).Value = value.Login.Trim().ToUpperInvariant();
        cmd.Parameters.Add("p_CLAVE", OracleDbType.Varchar2).Value = value.Clave;
        cmd.Parameters.Add("p_CEDULA", OracleDbType.Decimal).Value = value.Cedula.HasValue ? value.Cedula.Value : DBNull.Value;
        cmd.Parameters.Add("p_EMAIL", OracleDbType.Varchar2).Value = DbValue(value.Email);
        cmd.Parameters.Add("p_RECIBE_EMAIL", OracleDbType.Int32).Value = value.RecibeEmail ? 1 : 0;
        cmd.Parameters.Add("p_USUARIO_INS", OracleDbType.Int32).Value = usuarioUpd;
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
        await cmd.ExecuteNonQueryAsync();

        return codigoUsuario;
    }

    private static async Task UpsertUserRoleAsync(OracleConnection cn, OracleTransaction tx, int codigoUsuario, int empresa, int codigoRol, int usuarioUpd)
    {
        using var cmd = new OracleCommand(@"
            MERGE INTO SIS.OSS_USR_ROL t
            USING (
                SELECT :p_CODIGO_USUARIO CODIGO_USUARIO,
                       :p_CODIGO_ROL CODIGO_ROL,
                       :p_CODIGO_EMPRESA CODIGO_EMPRESA
                  FROM dual
            ) s
               ON (t.CODIGO_USUARIO = s.CODIGO_USUARIO AND t.CODIGO_ROL = s.CODIGO_ROL AND t.CODIGO_EMPRESA = s.CODIGO_EMPRESA)
             WHEN MATCHED THEN UPDATE SET ACTIVO = 1, USUARIO_UPD = :p_USUARIO_UPD, FECHA_UPD = SYSDATE
             WHEN NOT MATCHED THEN INSERT (CODIGO_USR_ROL, CODIGO_USUARIO, CODIGO_ROL, CODIGO_EMPRESA, ACTIVO, USUARIO_INS, FECHA_INS)
               VALUES ((SELECT NVL(MAX(CODIGO_USR_ROL), 0) + 1 FROM SIS.OSS_USR_ROL), s.CODIGO_USUARIO, s.CODIGO_ROL, s.CODIGO_EMPRESA, 1, :p_USUARIO_UPD, SYSDATE)", cn)
        {
            BindByName = true,
            Transaction = tx
        };
        cmd.Parameters.Add("p_CODIGO_USUARIO", OracleDbType.Int32).Value = codigoUsuario;
        cmd.Parameters.Add("p_CODIGO_ROL", OracleDbType.Int32).Value = codigoRol;
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_USUARIO_UPD", OracleDbType.Int32).Value = usuarioUpd;
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task UpsertUserPermAsync(OracleConnection cn, OracleTransaction tx, int codigoUsuario, int empresa, int codigoPerm, string tipo, int usuarioUpd)
    {
        using var cmd = new OracleCommand(@"
            MERGE INTO SIS.OSS_USR_PERM t
            USING (
                SELECT :p_CODIGO_USUARIO CODIGO_USUARIO,
                       :p_CODIGO_PERM CODIGO_PERM,
                       :p_CODIGO_EMPRESA CODIGO_EMPRESA
                  FROM dual
            ) s
               ON (t.CODIGO_USUARIO = s.CODIGO_USUARIO AND t.CODIGO_PERM = s.CODIGO_PERM AND t.CODIGO_EMPRESA = s.CODIGO_EMPRESA)
             WHEN MATCHED THEN UPDATE SET TIPO = :p_TIPO, ACTIVO = 1, USUARIO_UPD = :p_USUARIO_UPD, FECHA_UPD = SYSDATE
             WHEN NOT MATCHED THEN INSERT (CODIGO_USR_PERM, CODIGO_USUARIO, CODIGO_PERM, CODIGO_EMPRESA, TIPO, ACTIVO, USUARIO_INS, FECHA_INS)
               VALUES ((SELECT NVL(MAX(CODIGO_USR_PERM), 0) + 1 FROM SIS.OSS_USR_PERM), s.CODIGO_USUARIO, s.CODIGO_PERM, s.CODIGO_EMPRESA, :p_TIPO, 1, :p_USUARIO_UPD, SYSDATE)", cn)
        {
            BindByName = true,
            Transaction = tx
        };
        cmd.Parameters.Add("p_CODIGO_USUARIO", OracleDbType.Int32).Value = codigoUsuario;
        cmd.Parameters.Add("p_CODIGO_PERM", OracleDbType.Int32).Value = codigoPerm;
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_TIPO", OracleDbType.Varchar2).Value = string.Equals(tipo, "DENY", StringComparison.OrdinalIgnoreCase) ? "DENY" : "ALLOW";
        cmd.Parameters.Add("p_USUARIO_UPD", OracleDbType.Int32).Value = usuarioUpd;
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task<List<SisSegMigItemResponse>> ReadMigrationItemsAsync(OracleConnection cn, int empresa, string? codigoModulo, int? codigoUsuario = null)
    {
        var rolePermissions = await ReadRolePermissionMapAsync(cn);
        using var cmd = new OracleCommand(@"
            SELECT r.CODIGO_USUARIO,
                   NVL(u.USUARIO, r.USUARIO) USUARIO,
                   NVL(u.LOGIN, r.USUARIO) LOGIN,
                   NVL(u.IS_SUPERUSER, 0) IS_SUPERUSER,
                   NVL(r.DESCRIPCION, '') DESCRIPCION,
                   r.JSON_MENU
              FROM SIS.OSS_USUARIO_ROL r
              LEFT JOIN SIS.SIS_USUARIOS u ON u.CODIGO_USUARIO = r.CODIGO_USUARIO
             WHERE (:p_CODIGO_USUARIO IS NULL OR r.CODIGO_USUARIO = :p_CODIGO_USUARIO)
               AND (:p_CODIGO_MODULO IS NULL OR UPPER(TRIM(r.DESCRIPCION)) = UPPER(TRIM(:p_CODIGO_MODULO)))
               AND (u.CODIGO_EMPRESA IS NULL OR u.CODIGO_EMPRESA = :p_CODIGO_EMPRESA)
             ORDER BY r.CODIGO_USUARIO, r.DESCRIPCION", cn)
        {
            BindByName = true
        };
        cmd.Parameters.Add("p_CODIGO_USUARIO", OracleDbType.Int32).Value = codigoUsuario.HasValue ? codigoUsuario.Value : DBNull.Value;
        cmd.Parameters.Add("p_CODIGO_MODULO", OracleDbType.Varchar2).Value = string.IsNullOrWhiteSpace(codigoModulo) ? DBNull.Value : codigoModulo.Trim().ToUpperInvariant();
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;

        var list = new List<SisSegMigItemResponse>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var descripcion = reader.SafeGetString("DESCRIPCION").Trim().ToUpperInvariant();
            var json = ReadClob(reader, "JSON_MENU");
            var permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool valid = TryExtractJsonMenu(json, permissions, paths);
            var roles = SuggestRoles(descripcion, permissions, paths);
            var exceptions = GetPermissionExceptions(permissions, roles, rolePermissions);

            list.Add(new SisSegMigItemResponse(
                reader.SafeGetInt32("CODIGO_USUARIO"),
                reader.SafeGetString("USUARIO"),
                reader.SafeGetString("LOGIN"),
                descripcion,
                reader.SafeGetInt32("IS_SUPERUSER") == 1,
                valid,
                permissions.OrderBy(x => x).ToList(),
                paths.OrderBy(x => x).ToList(),
                roles,
                exceptions
            ));
        }

        return list;
    }

    private static async Task<Dictionary<string, HashSet<string>>> ReadRolePermissionMapAsync(OracleConnection cn)
    {
        using var cmd = new OracleCommand(@"
            SELECT r.CLAVE ROLE_KEY, p.CLAVE PERM_KEY
              FROM SIS.OSS_ROL r
              JOIN SIS.OSS_ROL_PERM rp ON rp.CODIGO_ROL = r.CODIGO_ROL
              JOIN SIS.OSS_PERM p ON p.CODIGO_PERM = rp.CODIGO_PERM
             WHERE r.ACTIVO = 1
               AND p.ACTIVO = 1", cn)
        {
            BindByName = true
        };

        var map = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var role = reader.SafeGetString("ROLE_KEY");
            if (!map.TryGetValue(role, out var permissions))
            {
                permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                map[role] = permissions;
            }

            permissions.Add(reader.SafeGetString("PERM_KEY"));
        }

        return map;
    }

    private static List<string> GetPermissionExceptions(ISet<string> legacyPermissions, List<string> roles, Dictionary<string, HashSet<string>> rolePermissions)
    {
        var covered = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var role in roles)
        {
            if (rolePermissions.TryGetValue(role, out var permissions))
            {
                covered.UnionWith(permissions);
            }
        }

        return legacyPermissions
            .Where(x => !covered.Contains(x))
            .OrderBy(x => x)
            .ToList();
    }

    private static async Task<List<string>> ReadSuggestedRoleKeysAsync(OracleConnection cn, int codigoUsuario, int empresa, string? codigoModulo)
    {
        using var cmd = new OracleCommand(@"
            SELECT NVL(r.DESCRIPCION, '') DESCRIPCION,
                   r.JSON_MENU
              FROM SIS.OSS_USUARIO_ROL r
              LEFT JOIN SIS.SIS_USUARIOS u ON u.CODIGO_USUARIO = r.CODIGO_USUARIO
             WHERE r.CODIGO_USUARIO = :p_CODIGO_USUARIO
               AND (:p_CODIGO_MODULO IS NULL OR UPPER(TRIM(r.DESCRIPCION)) = UPPER(TRIM(:p_CODIGO_MODULO)))
               AND (u.CODIGO_EMPRESA IS NULL OR u.CODIGO_EMPRESA = :p_CODIGO_EMPRESA)", cn)
        {
            BindByName = true
        };
        cmd.Parameters.Add("p_CODIGO_USUARIO", OracleDbType.Int32).Value = codigoUsuario;
        cmd.Parameters.Add("p_CODIGO_MODULO", OracleDbType.Varchar2).Value = string.IsNullOrWhiteSpace(codigoModulo) ? DBNull.Value : codigoModulo.Trim().ToUpperInvariant();
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;

        var roles = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (!TryExtractJsonMenu(ReadClob(reader, "JSON_MENU"), permissions, paths))
            {
                continue;
            }

            foreach (var role in SuggestRoles(reader.SafeGetString("DESCRIPCION").Trim().ToUpperInvariant(), permissions, paths))
            {
                roles.Add(role);
            }
        }

        return roles.OrderBy(x => x).ToList();
    }

    private static async Task UpsertUserRoleByKeyAsync(OracleConnection cn, OracleTransaction tx, int codigoUsuario, int empresa, string roleKey, int usuarioUpd)
    {
        using var cmd = new OracleCommand(@"
            MERGE INTO SIS.OSS_USR_ROL t
            USING (
                SELECT :p_CODIGO_USUARIO CODIGO_USUARIO,
                       r.CODIGO_ROL CODIGO_ROL,
                       :p_CODIGO_EMPRESA CODIGO_EMPRESA
                  FROM SIS.OSS_ROL r
                 WHERE UPPER(TRIM(r.CLAVE)) = UPPER(TRIM(:p_ROLE_KEY))
            ) s
               ON (t.CODIGO_USUARIO = s.CODIGO_USUARIO AND t.CODIGO_ROL = s.CODIGO_ROL AND t.CODIGO_EMPRESA = s.CODIGO_EMPRESA)
             WHEN MATCHED THEN UPDATE SET ACTIVO = 1, USUARIO_UPD = :p_USUARIO_UPD, FECHA_UPD = SYSDATE
             WHEN NOT MATCHED THEN INSERT (CODIGO_USR_ROL, CODIGO_USUARIO, CODIGO_ROL, CODIGO_EMPRESA, ACTIVO, USUARIO_INS, FECHA_INS)
               VALUES ((SELECT NVL(MAX(CODIGO_USR_ROL), 0) + 1 FROM SIS.OSS_USR_ROL), s.CODIGO_USUARIO, s.CODIGO_ROL, s.CODIGO_EMPRESA, 1, :p_USUARIO_UPD, SYSDATE)", cn)
        {
            BindByName = true,
            Transaction = tx
        };
        cmd.Parameters.Add("p_CODIGO_USUARIO", OracleDbType.Int32).Value = codigoUsuario;
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_ROLE_KEY", OracleDbType.Varchar2).Value = roleKey;
        cmd.Parameters.Add("p_USUARIO_UPD", OracleDbType.Int32).Value = usuarioUpd;
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task UpsertUserPermByKeyAsync(OracleConnection cn, OracleTransaction tx, int codigoUsuario, int empresa, string permissionKey, string tipo, int usuarioUpd)
    {
        using var cmd = new OracleCommand(@"
            MERGE INTO SIS.OSS_USR_PERM t
            USING (
                SELECT :p_CODIGO_USUARIO CODIGO_USUARIO,
                       p.CODIGO_PERM CODIGO_PERM,
                       :p_CODIGO_EMPRESA CODIGO_EMPRESA
                  FROM SIS.OSS_PERM p
                 WHERE UPPER(TRIM(p.CLAVE)) = UPPER(TRIM(:p_PERM_KEY))
            ) s
               ON (t.CODIGO_USUARIO = s.CODIGO_USUARIO AND t.CODIGO_PERM = s.CODIGO_PERM AND t.CODIGO_EMPRESA = s.CODIGO_EMPRESA)
             WHEN MATCHED THEN UPDATE SET TIPO = :p_TIPO, ACTIVO = 1, USUARIO_UPD = :p_USUARIO_UPD, FECHA_UPD = SYSDATE
             WHEN NOT MATCHED THEN INSERT (CODIGO_USR_PERM, CODIGO_USUARIO, CODIGO_PERM, CODIGO_EMPRESA, TIPO, ACTIVO, USUARIO_INS, FECHA_INS)
               VALUES ((SELECT NVL(MAX(CODIGO_USR_PERM), 0) + 1 FROM SIS.OSS_USR_PERM), s.CODIGO_USUARIO, s.CODIGO_PERM, s.CODIGO_EMPRESA, :p_TIPO, 1, :p_USUARIO_UPD, SYSDATE)", cn)
        {
            BindByName = true,
            Transaction = tx
        };
        cmd.Parameters.Add("p_CODIGO_USUARIO", OracleDbType.Int32).Value = codigoUsuario;
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_PERM_KEY", OracleDbType.Varchar2).Value = permissionKey;
        cmd.Parameters.Add("p_TIPO", OracleDbType.Varchar2).Value = string.Equals(tipo, "DENY", StringComparison.OrdinalIgnoreCase) ? "DENY" : "ALLOW";
        cmd.Parameters.Add("p_USUARIO_UPD", OracleDbType.Int32).Value = usuarioUpd;
        await cmd.ExecuteNonQueryAsync();
    }

    private static async Task InsertAuditAsync(OracleConnection cn, OracleTransaction tx, int? codigoUsuario, int empresa, string accion, string detalle, int usuarioAccion)
    {
        try
        {
            using var cmd = new OracleCommand(@"
                INSERT INTO SIS.OSS_SEG_AUD (
                    CODIGO_AUD,
                    CODIGO_USUARIO,
                    CODIGO_EMPRESA,
                    ACCION,
                    DETALLE,
                    USUARIO_ACCION,
                    FECHA_ACCION
                ) VALUES (
                    (SELECT NVL(MAX(CODIGO_AUD), 0) + 1 FROM SIS.OSS_SEG_AUD),
                    :p_CODIGO_USUARIO,
                    :p_CODIGO_EMPRESA,
                    :p_ACCION,
                    :p_DETALLE,
                    :p_USUARIO_ACCION,
                    SYSDATE
                )", cn)
            {
                BindByName = true,
                Transaction = tx
            };
            cmd.Parameters.Add("p_CODIGO_USUARIO", OracleDbType.Int32).Value = codigoUsuario.HasValue ? codigoUsuario.Value : DBNull.Value;
            cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
            cmd.Parameters.Add("p_ACCION", OracleDbType.Varchar2).Value = accion;
            cmd.Parameters.Add("p_DETALLE", OracleDbType.Varchar2).Value = detalle.Length > 1000 ? detalle[..1000] : detalle;
            cmd.Parameters.Add("p_USUARIO_ACCION", OracleDbType.Int32).Value = usuarioAccion;
            await cmd.ExecuteNonQueryAsync();
        }
        catch (OracleException ex) when (ex.Number == 942)
        {
            // La auditoria no debe impedir guardar seguridad si el ambiente aun no tiene OSS_SEG_AUD.
        }
    }

    private static async Task<List<string>> FindMissingSisTablesAsync(OracleConnection cn, IReadOnlyCollection<string> tables)
    {
        using var cmd = new OracleCommand(@"
            SELECT TABLE_NAME
              FROM ALL_TABLES
             WHERE OWNER = 'SIS'
               AND TABLE_NAME IN (" + string.Join(", ", tables.Select((_, index) => $":p_TABLE_{index}")) + ")", cn)
        {
            BindByName = true
        };

        int i = 0;
        foreach (var table in tables)
        {
            cmd.Parameters.Add($"p_TABLE_{i}", OracleDbType.Varchar2).Value = table;
            i++;
        }

        var found = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            found.Add(reader.SafeGetString("TABLE_NAME"));
        }

        return tables.Where(table => !found.Contains(table)).OrderBy(x => x).ToList();
    }

    private static async Task<SisSegNormSummary> ReadNormalizedSummaryAsync(OracleConnection cn, int empresa, string? codigoModulo)
    {
        using var cmd = new OracleCommand(@"
            SELECT COUNT(DISTINCT ur.CODIGO_USUARIO) USUARIOS_NORMALIZADOS,
                   COUNT(1) ROLES_NORMALIZADOS,
                   (
                     SELECT COUNT(1)
                       FROM SIS.OSS_USR_PERM up
                       JOIN SIS.OSS_PERM p ON p.CODIGO_PERM = up.CODIGO_PERM
                       JOIN SIS.OSS_MOD m ON m.CODIGO_MOD = p.CODIGO_MOD
                      WHERE up.CODIGO_EMPRESA = :p_CODIGO_EMPRESA
                        AND up.ACTIVO = 1
                        AND (:p_CODIGO_MODULO IS NULL OR m.CODIGO = :p_CODIGO_MODULO)
                   ) EXCEPCIONES_NORMALIZADAS
              FROM SIS.OSS_USR_ROL ur
              JOIN SIS.OSS_ROL r ON r.CODIGO_ROL = ur.CODIGO_ROL
              JOIN SIS.OSS_MOD m ON m.CODIGO_MOD = r.CODIGO_MOD
             WHERE ur.CODIGO_EMPRESA = :p_CODIGO_EMPRESA
               AND ur.ACTIVO = 1
               AND (:p_CODIGO_MODULO IS NULL OR m.CODIGO = :p_CODIGO_MODULO)", cn)
        {
            BindByName = true
        };
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_CODIGO_MODULO", OracleDbType.Varchar2).Value = string.IsNullOrWhiteSpace(codigoModulo) ? DBNull.Value : codigoModulo.Trim().ToUpperInvariant();

        using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return new SisSegNormSummary(0, 0, 0);
        }

        return new SisSegNormSummary(
            reader.SafeGetInt32("USUARIOS_NORMALIZADOS"),
            reader.SafeGetInt32("ROLES_NORMALIZADOS"),
            reader.SafeGetInt32("EXCEPCIONES_NORMALIZADAS")
        );
    }


    private static string? CacheModuleForLegacy(string? codigoModulo)
    {
        if (string.IsNullOrWhiteSpace(codigoModulo))
        {
            return null;
        }

        var normalized = codigoModulo.Trim().ToUpperInvariant();
        return normalized == "SIS" ? null : normalized;
    }

    private static async Task<SisSegCacheResponse> RegenerateCacheCoreAsync(OracleConnection cn, OracleTransaction tx, int codigoUsuario, int empresa, string? codigoModulo)
    {
        var usuario = await ReadUsuarioAsync(cn, codigoUsuario, empresa)
            ?? throw new InvalidOperationException("No se encontró el usuario indicado.");

        var menus = await ReadEffectiveMenusAsync(cn, tx, codigoUsuario, empresa, usuario.IsSuperuser, codigoModulo);
        var menuPerms = await ReadMenuPermisosAsync(cn, tx, codigoUsuario, empresa, usuario.IsSuperuser);
        var groupedRoots = BuildMenuGroups(menus, menuPerms);
        var updated = new List<string>();
        var fullMenu = new JsonArray();

        foreach (var group in groupedRoots)
        {
            var json = group.Roots.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
            await UpsertLegacyCacheAsync(cn, tx, usuario, group.CodigoCache, json);
            updated.Add(group.CodigoCache);

            foreach (var item in group.Roots)
            {
                fullMenu.Add(item?.DeepClone());
            }
        }

        return new SisSegCacheResponse(codigoUsuario, updated, ParseJson(fullMenu.ToJsonString()));
    }

    private static async Task<List<SisSegMenuData>> ReadEffectiveMenusAsync(OracleConnection cn, OracleTransaction tx, int codigoUsuario, int empresa, bool isSuperuser, string? codigoModulo)
    {
        var sql = isSuperuser
            ? @"
                SELECT m.CODIGO_MENU, m.CODIGO_MOD, mo.CODIGO CODIGO_MOD_TXT, m.CODIGO_PADRE, m.TITULO,
                       NVL(m.PATH, '') PATH, NVL(m.ICONO, '') ICONO, m.ORDEN
                  FROM SIS.OSS_MENU m
                  JOIN SIS.OSS_MOD mo ON mo.CODIGO_MOD = m.CODIGO_MOD
                 WHERE m.ACTIVO = 1
                   AND mo.ACTIVO = 1
                   AND (:p_CODIGO_MODULO IS NULL OR mo.CODIGO = :p_CODIGO_MODULO)
                 ORDER BY NVL(m.CODIGO_PADRE, 0), m.ORDEN, m.TITULO"
            : @"
                SELECT DISTINCT m.CODIGO_MENU, m.CODIGO_MOD, mo.CODIGO CODIGO_MOD_TXT, m.CODIGO_PADRE, m.TITULO,
                       NVL(m.PATH, '') PATH, NVL(m.ICONO, '') ICONO, m.ORDEN
                  FROM SIS.OSS_MENU m
                  JOIN SIS.OSS_MOD mo ON mo.CODIGO_MOD = m.CODIGO_MOD
                 WHERE m.ACTIVO = 1
                   AND mo.ACTIVO = 1
                   AND (:p_CODIGO_MODULO IS NULL OR mo.CODIGO = :p_CODIGO_MODULO OR m.CODIGO_PADRE IS NULL)
                   AND EXISTS (
                       SELECT 1
                         FROM SIS.OSS_USR_ROL ur
                         JOIN SIS.OSS_ROL_MENU rm ON rm.CODIGO_ROL = ur.CODIGO_ROL
                        WHERE ur.CODIGO_USUARIO = :p_CODIGO_USUARIO
                          AND ur.CODIGO_EMPRESA = :p_CODIGO_EMPRESA
                          AND ur.ACTIVO = 1
                          AND rm.CODIGO_MENU = m.CODIGO_MENU
                   )
                 ORDER BY NVL(m.CODIGO_PADRE, 0), m.ORDEN, m.TITULO";

        using var cmd = new OracleCommand(sql, cn)
        {
            BindByName = true,
            Transaction = tx
        };
        cmd.Parameters.Add("p_CODIGO_MODULO", OracleDbType.Varchar2).Value = string.IsNullOrWhiteSpace(codigoModulo) ? DBNull.Value : codigoModulo.Trim().ToUpperInvariant();
        if (!isSuperuser)
        {
            cmd.Parameters.Add("p_CODIGO_USUARIO", OracleDbType.Int32).Value = codigoUsuario;
            cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
        }

        var list = new List<SisSegMenuData>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            list.Add(new SisSegMenuData(
                reader.SafeGetInt32("CODIGO_MENU"),
                reader.SafeGetInt32("CODIGO_MOD"),
                reader.SafeGetString("CODIGO_MOD_TXT"),
                NullableInt(reader, "CODIGO_PADRE"),
                reader.SafeGetString("TITULO"),
                reader.SafeGetString("PATH"),
                reader.SafeGetString("ICONO"),
                reader.SafeGetInt32("ORDEN")
            ));
        }

        return list;
    }

    private static async Task<Dictionary<int, List<string>>> ReadMenuPermisosAsync(OracleConnection cn, OracleTransaction tx, int codigoUsuario, int empresa, bool isSuperuser)
    {
        var sql = isSuperuser
            ? @"
                SELECT mp.CODIGO_MENU, p.CLAVE
                  FROM SIS.OSS_MENU_PERM mp
                  JOIN SIS.OSS_PERM p ON p.CODIGO_PERM = mp.CODIGO_PERM
                 WHERE p.ACTIVO = 1
                 ORDER BY mp.CODIGO_MENU, p.CLAVE"
            : @"
                SELECT mp.CODIGO_MENU, p.CLAVE
                  FROM SIS.OSS_MENU_PERM mp
                  JOIN SIS.OSS_PERM p ON p.CODIGO_PERM = mp.CODIGO_PERM
                 WHERE p.ACTIVO = 1
                   AND (
                       EXISTS (
                           SELECT 1
                             FROM SIS.OSS_USR_ROL ur
                             JOIN SIS.OSS_ROL_PERM rp ON rp.CODIGO_ROL = ur.CODIGO_ROL
                            WHERE ur.CODIGO_USUARIO = :p_CODIGO_USUARIO
                              AND ur.CODIGO_EMPRESA = :p_CODIGO_EMPRESA
                              AND ur.ACTIVO = 1
                              AND rp.CODIGO_PERM = p.CODIGO_PERM
                       )
                       OR EXISTS (
                           SELECT 1
                             FROM SIS.OSS_USR_PERM up
                            WHERE up.CODIGO_USUARIO = :p_CODIGO_USUARIO
                              AND up.CODIGO_EMPRESA = :p_CODIGO_EMPRESA
                              AND up.ACTIVO = 1
                              AND up.TIPO = 'ALLOW'
                              AND up.CODIGO_PERM = p.CODIGO_PERM
                       )
                   )
                   AND NOT EXISTS (
                       SELECT 1
                         FROM SIS.OSS_USR_PERM up
                        WHERE up.CODIGO_USUARIO = :p_CODIGO_USUARIO
                          AND up.CODIGO_EMPRESA = :p_CODIGO_EMPRESA
                          AND up.ACTIVO = 1
                          AND up.TIPO = 'DENY'
                          AND up.CODIGO_PERM = p.CODIGO_PERM
                   )
                 ORDER BY mp.CODIGO_MENU, p.CLAVE";

        using var cmd = new OracleCommand(sql, cn)
        {
            BindByName = true,
            Transaction = tx
        };
        if (!isSuperuser)
        {
            cmd.Parameters.Add("p_CODIGO_USUARIO", OracleDbType.Int32).Value = codigoUsuario;
            cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
        }

        var map = new Dictionary<int, List<string>>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            int menuId = reader.SafeGetInt32("CODIGO_MENU");
            if (!map.TryGetValue(menuId, out var list))
            {
                list = new List<string>();
                map[menuId] = list;
            }

            list.Add(reader.SafeGetString("CLAVE"));
        }

        return map;
    }

    private static List<SisSegMenuGroup> BuildMenuGroups(List<SisSegMenuData> menus, Dictionary<int, List<string>> menuPerms)
    {
        var byParent = menus
            .GroupBy(x => x.CodigoPadre ?? 0)
            .ToDictionary(x => x.Key, x => x.OrderBy(y => y.Orden).ThenBy(y => y.Titulo).ToList());

        var groups = new List<SisSegMenuGroup>();
        var roots = byParent.GetValueOrDefault(0) ?? new List<SisSegMenuData>();
        foreach (var moduleGroup in roots.GroupBy(x => x.CodigoModTexto))
        {
            var array = new JsonArray(
                moduleGroup
                    .OrderBy(x => x.Orden)
                    .ThenBy(x => x.Titulo)
                    .Select(x => BuildNode(x, byParent, menuPerms))
                    .ToArray<JsonNode?>()
            );

            groups.Add(new SisSegMenuGroup(moduleGroup.Key, array));
        }

        return groups;
    }

    private static JsonObject BuildNode(SisSegMenuData menu, Dictionary<int, List<SisSegMenuData>> byParent, Dictionary<int, List<string>> menuPerms)
    {
        var node = new JsonObject
        {
            ["title"] = menu.Titulo
        };

        if (!string.IsNullOrWhiteSpace(menu.Icono))
        {
            node["icon"] = menu.Icono;
        }

        if (!string.IsNullOrWhiteSpace(menu.Path))
        {
            node["path"] = menu.Path;
        }

        if (menuPerms.TryGetValue(menu.CodigoMenu, out var permissions) && permissions.Count > 0)
        {
            node["permissions"] = new JsonArray(permissions.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).Select(x => JsonValue.Create(x)).ToArray<JsonNode?>());
        }

        if (byParent.TryGetValue(menu.CodigoMenu, out var children) && children.Count > 0)
        {
            node["children"] = new JsonArray(children.Select(x => BuildNode(x, byParent, menuPerms)).ToArray<JsonNode?>());
        }

        return node;
    }

    private static async Task UpsertLegacyCacheAsync(OracleConnection cn, OracleTransaction tx, SisSegUserData usuario, string descripcion, string jsonMenu)
    {
        using var cmd = new OracleCommand(@"
            MERGE INTO SIS.OSS_USUARIO_ROL t
            USING (
                SELECT :p_CODIGO_USUARIO CODIGO_USUARIO,
                       :p_USUARIO USUARIO,
                       :p_DESCRIPCION DESCRIPCION,
                       :p_JSON_MENU JSON_MENU
                  FROM dual
            ) s
               ON (t.CODIGO_USUARIO = s.CODIGO_USUARIO AND UPPER(TRIM(t.DESCRIPCION)) = UPPER(TRIM(s.DESCRIPCION)))
             WHEN MATCHED THEN UPDATE SET USUARIO = s.USUARIO, JSON_MENU = s.JSON_MENU
             WHEN NOT MATCHED THEN INSERT (CODIGO_USUARIO_ROL, USUARIO, CODIGO_USUARIO, DESCRIPCION, JSON_MENU)
               VALUES ((SELECT NVL(MAX(CODIGO_USUARIO_ROL), 0) + 1 FROM SIS.OSS_USUARIO_ROL), s.USUARIO, s.CODIGO_USUARIO, s.DESCRIPCION, s.JSON_MENU)", cn)
        {
            BindByName = true,
            Transaction = tx
        };
        cmd.Parameters.Add("p_CODIGO_USUARIO", OracleDbType.Int32).Value = usuario.CodigoUsuario;
        cmd.Parameters.Add("p_USUARIO", OracleDbType.Varchar2).Value = usuario.Login;
        cmd.Parameters.Add("p_DESCRIPCION", OracleDbType.Varchar2).Value = descripcion;
        cmd.Parameters.Add("p_JSON_MENU", OracleDbType.Clob).Value = jsonMenu;
        await cmd.ExecuteNonQueryAsync();
    }

    private static SisSegRolResponse MapRol(IDataReader reader)
    {
        return new SisSegRolResponse(
            reader.SafeGetInt32("CODIGO_ROL"),
            reader.SafeGetInt32("CODIGO_MOD"),
            reader.SafeGetString("CLAVE"),
            reader.SafeGetString("NOMBRE"),
            reader.SafeGetString("DESCRIPCION"),
            reader.SafeGetInt32("ACTIVO") == 1
        );
    }

    private static int? NullableInt(IDataReader reader, string columnName)
    {
        int ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.SafeGetInt32(columnName);
    }

    private static string ReadClob(IDataReader reader, string columnName)
    {
        int ordinal = reader.GetOrdinal(columnName);
        if (reader.IsDBNull(ordinal))
        {
            return string.Empty;
        }

        if (reader is OracleDataReader oracleReader)
        {
            using OracleClob clob = oracleReader.GetOracleClob(ordinal);
            return clob.IsNull ? string.Empty : clob.Value;
        }

        return reader.GetValue(ordinal).ToString() ?? string.Empty;
    }

    private static JsonElement ParseJson(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "[]" : json);
            return doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            using var doc = JsonDocument.Parse("[]");
            return doc.RootElement.Clone();
        }
    }

    private static JsonNode? ParseJsonNode(string json)
    {
        try
        {
            return JsonNode.Parse(string.IsNullOrWhiteSpace(json) ? "[]" : json);
        }
        catch (JsonException)
        {
            return new JsonArray();
        }
    }

    private static bool TryExtractJsonMenu(string json, ISet<string> permissions, ISet<string> paths)
    {
        try
        {
            var node = JsonNode.Parse(string.IsNullOrWhiteSpace(json) ? "[]" : json);
            ExtractJsonMenu(node, permissions, paths);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static void ExtractJsonMenu(JsonNode? node, ISet<string> permissions, ISet<string> paths)
    {
        if (node is JsonArray array)
        {
            foreach (var item in array)
            {
                ExtractJsonMenu(item, permissions, paths);
            }

            return;
        }

        if (node is not JsonObject obj)
        {
            return;
        }

        if (obj["path"] is JsonValue pathValue && pathValue.TryGetValue<string>(out var path) && !string.IsNullOrWhiteSpace(path))
        {
            paths.Add(path);
        }

        if (obj["permissions"] is JsonArray permArray)
        {
            foreach (var item in permArray)
            {
                if (item is JsonValue value && value.TryGetValue<string>(out var permission) && !string.IsNullOrWhiteSpace(permission))
                {
                    permissions.Add(permission);
                }
            }
        }

        ExtractJsonMenu(obj["children"], permissions, paths);
    }

    private static List<string> SuggestRoles(string descripcion, ISet<string> permissions, ISet<string> paths)
    {
        var roles = new List<string>();

        if (permissions.Contains("soporte.tickets.ver_todos") || permissions.Contains("soporte.catalogos.admin"))
        {
            roles.Add("SOPORTE_ADMIN");
        }
        else if (permissions.Contains("soporte.tickets.ver_asignados") || permissions.Contains("soporte.tickets.asignar"))
        {
            roles.Add("SOPORTE_AGENTE");
        }
        else if (permissions.Contains("soporte.tickets.ver_propios") || permissions.Contains("soporte.tickets.crear") || paths.Any(x => x.Contains("/apps/soporte/", StringComparison.OrdinalIgnoreCase)))
        {
            roles.Add("SOPORTE_USUARIO");
        }

        if (permissions.Contains("contabilidad.catalogos.admin")
            || permissions.Contains("contabilidad.comprobantes.eliminar")
            || permissions.Contains("contabilidad.cierre.cierre")
            || permissions.Contains("contabilidad.conciliacion.admin"))
        {
            roles.Add("CNT_ADMIN");
        }
        else if (permissions.Contains("contabilidad.comprobantes.editar")
            || permissions.Contains("contabilidad.conciliacion.importar")
            || permissions.Contains("contabilidad.cierre.precierre"))
        {
            roles.Add("CNT_ANALISTA");
        }
        else if (permissions.Contains("contabilidad.comprobantes.ver") || paths.Any(x => x.Contains("/apps/cnt/", StringComparison.OrdinalIgnoreCase)))
        {
            roles.Add("CNT_USUARIO");
        }

        if (descripcion == "RH" || paths.Any(x => x.Contains("/apps/rh/", StringComparison.OrdinalIgnoreCase)))
        {
            roles.Add("RH_USUARIO");
        }

        if (descripcion == "PRE" || paths.Any(x => x.Contains("/apps/presupuesto/", StringComparison.OrdinalIgnoreCase) || x.Contains("/dashboards/presupuesto", StringComparison.OrdinalIgnoreCase)))
        {
            roles.Add("PRE_USUARIO");
        }

        if (descripcion == "ADM" || paths.Any(x => x.Contains("/apps/adm/", StringComparison.OrdinalIgnoreCase)))
        {
            roles.Add("ADM_USUARIO");
        }

        return roles.Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
    }

    private static object DbValue(string? value) =>
        string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();

    private static object DbValue(int? value) =>
        value.HasValue ? value.Value : DBNull.Value;

    private static async Task<int> NextNumberAsync(OracleConnection cn, OracleTransaction tx, string tableName, string columnName)
    {
        using var cmd = new OracleCommand($"SELECT NVL(MAX({columnName}), 0) + 1 FROM {tableName}", cn)
        {
            BindByName = true,
            Transaction = tx
        };
        var value = await cmd.ExecuteScalarAsync();

        return Convert.ToInt32(value);
    }

    private static string FormatOracleError(Exception ex)
    {
        if (ex is OracleException oracle && oracle.Number == 942)
        {
            return "Error técnico: falta una tabla o vista requerida en SIS para seguridad normalizada. Verifique que existan SIS.OSS_USR_ROL, SIS.OSS_USR_PERM, SIS.OSS_USUARIO_ROL y las tablas OSS_* del script Features/SisSeguridad/Sql/00_INSTALL_SIS_SEGURIDAD.sql.";
        }

        return $"Error técnico: {ex.Message}";
    }

    internal sealed record SisSegUserData(int CodigoUsuario, string Usuario, string Login, bool IsSuperuser);
    private sealed record SisSegNormSummary(int UsuariosNormalizados, int RolesNormalizados, int ExcepcionesNormalizadas);
    private sealed record SisSegMenuData(int CodigoMenu, int CodigoMod, string CodigoModTexto, int? CodigoPadre, string Titulo, string Path, string Icono, int Orden);
    private sealed record SisSegMenuGroup(string CodigoCache, JsonArray Roots);
}
