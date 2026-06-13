using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using Oracle.ManagedDataAccess.Types;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Features.Support;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace OssmmasoftVerticalSlice.Features.SisUsuarios;

public record SisUsuarioResponse(int CodigoUsuario, string Usuario, string Login, decimal? Cedula, string Status, string Email, bool RecibeEmail, bool EsAnalistaSoporte, bool EsAnalistaCnt, bool EsAdminCnt, bool IsSuperuser, int CodigoEmpresa);
public record SisUsuarioGetAllQuery(int PageSize = 10, int PageNumber = 1, string SearchText = "", bool SoloActivos = false);
public record SisUsuarioGetByIdQuery(int CodigoUsuario);
public record SisUsuarioCreateCommand(string Usuario, string Login, string Clave, decimal? Cedula, string? Email, bool RecibeEmail, bool EsAnalistaSoporte, bool EsAnalistaCnt, bool EsAdminCnt, bool IsSuperuser, int UsuarioIns);
public record SisUsuarioUpdateCommand(int CodigoUsuario, string Usuario, string Login, decimal? Cedula, string Status, string? Email, bool RecibeEmail, bool EsAnalistaSoporte, bool EsAnalistaCnt, bool EsAdminCnt, bool IsSuperuser, int UsuarioUpd);
public record SisUsuarioUpdateEmailCommand(int CodigoUsuario, string? Email, bool RecibeEmail, bool EsAnalistaSoporte, bool EsAnalistaCnt, bool EsAdminCnt, bool IsSuperuser, int UsuarioUpd);
public record SisUsuarioUpdatePasswordCommand(int CodigoUsuario, string Clave, int UsuarioUpd);
public record SisUsuarioApplySupportPermissionsCommand(int CodigoUsuario, int UsuarioUpd);
public record SisUsuarioApplySupportPermissionsResponse(int CodigoUsuario, string Perfil, int CodigoUsuarioRol, bool Created);
public record SisUsuarioApplyCntPermissionsCommand(int CodigoUsuario, int UsuarioUpd);
public record SisUsuarioApplyCntPermissionsResponse(int CodigoUsuario, string Perfil, int CodigoUsuarioRol, bool Created);

[ApiController]
[Authorize]
[Route("api/SisUsuarios")]
public class SisUsuariosController(ConnectionDB connectionDB, IConfiguration config) : ControllerBase
{
    private const string SisRoleDescription = "SIS";
    private const string CntRoleDescription = "CNT";

    [HttpPost("GetAll")]
    public async Task<IActionResult> GetAll(SisUsuarioGetAllQuery value)
    {
        var access = await RequirePermissionAsync(SupportSecurity.SupportUsersConfig);
        if (!access.IsValid)
        {
            return Ok(new ResultDto<List<SisUsuarioResponse>>(null!) { IsValid = false, Message = access.Message });
        }

        if (!SupportDb.TryGetEmpresa(config, out int empresa, out string errorMessage))
        {
            return Ok(new ResultDto<List<SisUsuarioResponse>>(null!) { IsValid = false, Message = errorMessage });
        }

        int pageSize = value.PageSize <= 0 ? 10 : value.PageSize;
        int pageNumber = value.PageNumber <= 0 ? 1 : value.PageNumber;
        using var cn = connectionDB.GetSisConnection();
        await cn.OpenAsync();
        using var cmd = new OracleCommand("SIS.SP_SIS_USR_GET_ALL", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
        cmd.Parameters.Add("p_PageSize", OracleDbType.Int32).Value = pageSize;
        cmd.Parameters.Add("p_PageNumber", OracleDbType.Int32).Value = pageNumber;
        cmd.Parameters.Add("p_SearchText", OracleDbType.Varchar2).Value = SupportDb.StringDbValue(value.SearchText);
        cmd.Parameters.Add("p_SOLO_ACTIVOS", OracleDbType.Int32).Value = value.SoloActivos ? 1 : 0;
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
        var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
        var pTotalRecords = cmd.Parameters.Add("p_TotalRecords", OracleDbType.Int32, ParameterDirection.Output);
        var pTotalPages = cmd.Parameters.Add("p_TotalPages", OracleDbType.Int32, ParameterDirection.Output);

        var list = new List<SisUsuarioResponse>();
        using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                list.Add(MapUsuario(reader));
            }
        }

        var message = SupportDb.GetMessage(pMessage);
        var isSuccess = SupportDb.IsSuccessMessage(message);
        return Ok(new ResultDto<List<SisUsuarioResponse>>(list)
        {
            Data = isSuccess ? list : null,
            IsValid = isSuccess,
            Message = message,
            Page = pageNumber,
            TotalPage = SupportDb.GetIntOutput(pTotalPages),
            CantidadRegistros = SupportDb.GetIntOutput(pTotalRecords)
        });
    }

    [HttpPost("getById")]
    public async Task<IActionResult> GetById(SisUsuarioGetByIdQuery value)
    {
        var access = await RequirePermissionOrSelfAsync(value.CodigoUsuario, SupportSecurity.SupportUsersConfig);
        if (!access.IsValid)
        {
            return Ok(new ResultDto<SisUsuarioResponse>(null!) { IsValid = false, Message = access.Message });
        }

        return Ok(await ExecuteSingleAsync("SIS.SP_SIS_USR_GET_ID", cmd =>
        {
            cmd.Parameters.Add("p_CODIGO_USUARIO", OracleDbType.Int32).Value = value.CodigoUsuario;
        }));
    }

    [HttpPost("getSupportUsers")]
    public async Task<IActionResult> GetSupportUsers(SisUsuarioGetAllQuery value)
    {
        var access = await RequirePermissionAsync(SupportSecurity.SupportUsersConfig, SupportSecurity.TicketAssign, SupportSecurity.TicketViewAll);
        if (!access.IsValid)
        {
            return Ok(new ResultDto<List<SisUsuarioResponse>>(null!) { IsValid = false, Message = access.Message });
        }

        return Ok(await ExecuteListAsync("SIS.SP_SIS_USR_GET_SUPP", value));
    }

    [HttpPost("create")]
    public async Task<IActionResult> Create(SisUsuarioCreateCommand value)
    {
        var access = await RequirePermissionAsync(SupportSecurity.SupportUsersConfig);
        if (!access.IsValid)
        {
            return Ok(new ResultDto<int>(0) { IsValid = false, Message = access.Message });
        }

        var result = await ExecuteScalarAsync("SIS.SP_SIS_USR_INS", cmd =>
        {
            cmd.Parameters.Add("p_USUARIO", OracleDbType.Varchar2).Value = SupportDb.StringDbValue(value.Usuario);
            cmd.Parameters.Add("p_LOGIN", OracleDbType.Varchar2).Value = SupportDb.StringDbValue(value.Login);
            cmd.Parameters.Add("p_CLAVE", OracleDbType.Varchar2).Value = SupportDb.StringDbValue(value.Clave);
            cmd.Parameters.Add("p_CEDULA", OracleDbType.Decimal).Value = SupportDb.DbValue(value.Cedula);
            cmd.Parameters.Add("p_EMAIL", OracleDbType.Varchar2).Value = SupportDb.StringDbValue(value.Email);
            cmd.Parameters.Add("p_RECIBE_EMAIL", OracleDbType.Int32).Value = value.RecibeEmail ? 1 : 0;
            cmd.Parameters.Add("p_ES_ANALISTA_SOPORTE", OracleDbType.Int32).Value = value.EsAnalistaSoporte ? 1 : 0;
            cmd.Parameters.Add("p_ES_ANALISTA_CNT", OracleDbType.Int32).Value = value.EsAnalistaCnt ? 1 : 0;
            cmd.Parameters.Add("p_ES_ADMIN_CNT", OracleDbType.Int32).Value = value.EsAdminCnt ? 1 : 0;
            cmd.Parameters.Add("p_IS_SUPERUSER", OracleDbType.Int32).Value = value.IsSuperuser ? 1 : 0;
            cmd.Parameters.Add("p_USUARIO_INS", OracleDbType.Int32).Value = access.Data;
        }, "p_CODIGO_USUARIO_OUT");
        return Ok(result);
    }

    [HttpPost("update")]
    public async Task<IActionResult> Update(SisUsuarioUpdateCommand value)
    {
        var access = await RequirePermissionAsync(SupportSecurity.SupportUsersConfig);
        if (!access.IsValid)
        {
            return Ok(new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = access.Message });
        }

        var result = await ExecuteMessageAsync("SIS.SP_SIS_USR_UPD", cmd =>
        {
            cmd.Parameters.Add("p_CODIGO_USUARIO", OracleDbType.Int32).Value = value.CodigoUsuario;
            cmd.Parameters.Add("p_USUARIO", OracleDbType.Varchar2).Value = SupportDb.StringDbValue(value.Usuario);
            cmd.Parameters.Add("p_LOGIN", OracleDbType.Varchar2).Value = SupportDb.StringDbValue(value.Login);
            cmd.Parameters.Add("p_CEDULA", OracleDbType.Decimal).Value = SupportDb.DbValue(value.Cedula);
            cmd.Parameters.Add("p_STATUS", OracleDbType.Varchar2).Value = SupportDb.StringDbValue(value.Status);
            cmd.Parameters.Add("p_EMAIL", OracleDbType.Varchar2).Value = SupportDb.StringDbValue(value.Email);
            cmd.Parameters.Add("p_RECIBE_EMAIL", OracleDbType.Int32).Value = value.RecibeEmail ? 1 : 0;
            cmd.Parameters.Add("p_ES_ANALISTA_SOPORTE", OracleDbType.Int32).Value = value.EsAnalistaSoporte ? 1 : 0;
            cmd.Parameters.Add("p_ES_ANALISTA_CNT", OracleDbType.Int32).Value = value.EsAnalistaCnt ? 1 : 0;
            cmd.Parameters.Add("p_ES_ADMIN_CNT", OracleDbType.Int32).Value = value.EsAdminCnt ? 1 : 0;
            cmd.Parameters.Add("p_IS_SUPERUSER", OracleDbType.Int32).Value = value.IsSuperuser ? 1 : 0;
            cmd.Parameters.Add("p_USUARIO_UPD", OracleDbType.Int32).Value = access.Data;
        });
        return Ok(result);
    }

    [HttpPost("updateEmail")]
    public async Task<IActionResult> UpdateEmail(SisUsuarioUpdateEmailCommand value)
    {
        var access = await RequirePermissionOrSelfAsync(value.CodigoUsuario, SupportSecurity.SupportUsersConfig);
        if (!access.IsValid)
        {
            return Ok(new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = access.Message });
        }

        var result = await ExecuteMessageAsync("SIS.SP_SIS_USR_EMAIL_UPD", cmd =>
        {
            cmd.Parameters.Add("p_CODIGO_USUARIO", OracleDbType.Int32).Value = value.CodigoUsuario;
            cmd.Parameters.Add("p_EMAIL", OracleDbType.Varchar2).Value = SupportDb.StringDbValue(value.Email);
            cmd.Parameters.Add("p_RECIBE_EMAIL", OracleDbType.Int32).Value = value.RecibeEmail ? 1 : 0;
            cmd.Parameters.Add("p_ES_ANALISTA_SOPORTE", OracleDbType.Int32).Value = value.EsAnalistaSoporte ? 1 : 0;
            cmd.Parameters.Add("p_ES_ANALISTA_CNT", OracleDbType.Int32).Value = value.EsAnalistaCnt ? 1 : 0;
            cmd.Parameters.Add("p_ES_ADMIN_CNT", OracleDbType.Int32).Value = value.EsAdminCnt ? 1 : 0;
            cmd.Parameters.Add("p_IS_SUPERUSER", OracleDbType.Int32).Value = value.IsSuperuser ? 1 : 0;
            cmd.Parameters.Add("p_USUARIO_UPD", OracleDbType.Int32).Value = access.Data;
        });
        return Ok(result);
    }

    [HttpPost("updatePassword")]
    public async Task<IActionResult> UpdatePassword(SisUsuarioUpdatePasswordCommand value)
    {
        var access = await RequirePermissionAsync(SupportSecurity.SupportUsersConfig);
        if (!access.IsValid)
        {
            return Ok(new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = access.Message });
        }

        var result = await ExecuteMessageAsync("SIS.SP_SIS_USR_PASS_UPD", cmd =>
        {
            cmd.Parameters.Add("p_CODIGO_USUARIO", OracleDbType.Int32).Value = value.CodigoUsuario;
            cmd.Parameters.Add("p_CLAVE", OracleDbType.Varchar2).Value = SupportDb.StringDbValue(value.Clave);
            cmd.Parameters.Add("p_USUARIO_UPD", OracleDbType.Int32).Value = access.Data;
        });
        return Ok(result);
    }

    [HttpPost("applySupportPermissions")]
    public async Task<IActionResult> ApplySupportPermissions(SisUsuarioApplySupportPermissionsCommand value)
    {
        var access = await RequirePermissionAsync(SupportSecurity.SupportUsersConfig);
        if (!access.IsValid)
        {
            return Ok(new ResultDto<SisUsuarioApplySupportPermissionsResponse>(null!) { Data = null, IsValid = false, Message = access.Message });
        }

        if (value.CodigoUsuario <= 0)
        {
            return Ok(new ResultDto<SisUsuarioApplySupportPermissionsResponse>(null!) { Data = null, IsValid = false, Message = "CodigoUsuario es requerido." });
        }

        if (!SupportDb.TryGetEmpresa(config, out int empresa, out string errorMessage))
        {
            return Ok(new ResultDto<SisUsuarioApplySupportPermissionsResponse>(null!) { Data = null, IsValid = false, Message = errorMessage });
        }

        using var cn = connectionDB.GetSisConnection();
        await cn.OpenAsync();

        var targetUser = await GetSupportPermissionTargetUserAsync(cn, value.CodigoUsuario, empresa);
        if (targetUser is null)
        {
            return Ok(new ResultDto<SisUsuarioApplySupportPermissionsResponse>(null!) { Data = null, IsValid = false, Message = "No se encontró el usuario SIS indicado." });
        }

        var perfil = targetUser.IsSuperuser
            ? "Administrador De Soporte"
            : targetUser.EsAnalistaSoporte
                ? "Analista De Soporte"
                : "Usuario Normal";

        var supportNode = BuildSupportMenuNode(perfil);
        var existingRole = await GetSisRoleByUserAsync(cn, value.CodigoUsuario);
        var jsonMenu = MergeSupportMenu(existingRole.JsonMenu, supportNode);
        var created = existingRole.CodigoUsuarioRol == 0;
        var codigoUsuarioRol = created
            ? await InsertSisRoleAsync(cn, targetUser.Login, value.CodigoUsuario, jsonMenu)
            : await UpdateSisRoleJsonAsync(cn, existingRole.CodigoUsuarioRol, targetUser.Login, value.CodigoUsuario, jsonMenu);

        var response = new SisUsuarioApplySupportPermissionsResponse(value.CodigoUsuario, perfil, codigoUsuarioRol, created);

        return Ok(new ResultDto<SisUsuarioApplySupportPermissionsResponse>(response)
        {
            Data = response,
            IsValid = true,
            Message = $"Permisos de soporte aplicados: {perfil}."
        });
    }

    [HttpPost("applyCntPermissions")]
    public async Task<IActionResult> ApplyCntPermissions(SisUsuarioApplyCntPermissionsCommand value)
    {
        var access = await RequirePermissionAsync(SupportSecurity.SupportUsersConfig);
        if (!access.IsValid)
        {
            return Ok(new ResultDto<SisUsuarioApplyCntPermissionsResponse>(null!) { Data = null, IsValid = false, Message = access.Message });
        }

        if (value.CodigoUsuario <= 0)
        {
            return Ok(new ResultDto<SisUsuarioApplyCntPermissionsResponse>(null!) { Data = null, IsValid = false, Message = "CodigoUsuario es requerido." });
        }

        if (!SupportDb.TryGetEmpresa(config, out int empresa, out string errorMessage))
        {
            return Ok(new ResultDto<SisUsuarioApplyCntPermissionsResponse>(null!) { Data = null, IsValid = false, Message = errorMessage });
        }

        using var cn = connectionDB.GetSisConnection();
        await cn.OpenAsync();

        var targetUser = await GetSupportPermissionTargetUserAsync(cn, value.CodigoUsuario, empresa);
        if (targetUser is null)
        {
            return Ok(new ResultDto<SisUsuarioApplyCntPermissionsResponse>(null!) { Data = null, IsValid = false, Message = "No se encontró el usuario SIS indicado." });
        }

        var perfil = targetUser.EsAdminCnt
            ? "Administrador De Contabilidad"
            : targetUser.EsAnalistaCnt
                ? "Analista De Contabilidad"
                : "Usuario Contable";

        var existingRole = await GetUserRoleByDescriptionAsync(cn, value.CodigoUsuario, CntRoleDescription);
        var jsonMenu = BuildPrincipalMenu(BuildCntMenuNode(perfil));
        var created = existingRole.CodigoUsuarioRol == 0;
        var codigoUsuarioRol = created
            ? await InsertUserRoleAsync(cn, targetUser.Login, value.CodigoUsuario, CntRoleDescription, jsonMenu)
            : await UpdateUserRoleJsonAsync(cn, existingRole.CodigoUsuarioRol, targetUser.Login, value.CodigoUsuario, CntRoleDescription, jsonMenu);

        var sisRole = await GetSisRoleByUserAsync(cn, value.CodigoUsuario);
        if (sisRole.CodigoUsuarioRol > 0)
        {
            var cleanedSisMenu = RemoveModuleFromSystemMenu(sisRole.JsonMenu, "Contabilidad");
            if (!string.Equals(cleanedSisMenu, sisRole.JsonMenu, StringComparison.Ordinal))
            {
                await UpdateSisRoleJsonAsync(cn, sisRole.CodigoUsuarioRol, targetUser.Login, value.CodigoUsuario, cleanedSisMenu);
            }
        }

        var response = new SisUsuarioApplyCntPermissionsResponse(value.CodigoUsuario, perfil, codigoUsuarioRol, created);

        return Ok(new ResultDto<SisUsuarioApplyCntPermissionsResponse>(response)
        {
            Data = response,
            IsValid = true,
            Message = $"Permisos de contabilidad aplicados en modulo CNT: {perfil}."
        });
    }

    private async Task<ResultDto<int>> RequirePermissionAsync(params string[] permissions)
    {
        var session = await SupportSecurity.ResolveAuthenticatedUserAsync(connectionDB, config, User, Request);
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
        var userPermissions = await SupportSecurity.GetPermissionsAsync(cn, session.Data, empresa);

        return SupportSecurity.HasAny(userPermissions, permissions)
            ? session
            : new ResultDto<int>(0) { IsValid = false, Message = $"El usuario no tiene el permiso requerido: {string.Join(" o ", permissions)}." };
    }

    private async Task<ResultDto<int>> RequirePermissionOrSelfAsync(int targetUserId, params string[] permissions)
    {
        var session = await SupportSecurity.ResolveAuthenticatedUserAsync(connectionDB, config, User, Request);
        if (!session.IsValid || session.Data == targetUserId)
        {
            return session;
        }

        if (!SupportDb.TryGetEmpresa(config, out int empresa, out string errorMessage))
        {
            return new ResultDto<int>(0) { IsValid = false, Message = errorMessage };
        }

        using var cn = connectionDB.GetSisConnection();
        await cn.OpenAsync();
        var userPermissions = await SupportSecurity.GetPermissionsAsync(cn, session.Data, empresa);

        return SupportSecurity.HasAny(userPermissions, permissions)
            ? session
            : new ResultDto<int>(0) { IsValid = false, Message = $"El usuario no tiene el permiso requerido: {string.Join(" o ", permissions)}." };
    }

    private async Task<ResultDto<List<SisUsuarioResponse>>> ExecuteListAsync(string procedure, SisUsuarioGetAllQuery value)
    {
        if (!SupportDb.TryGetEmpresa(config, out int empresa, out string errorMessage))
        {
            return new ResultDto<List<SisUsuarioResponse>>(null!) { IsValid = false, Message = errorMessage };
        }

        using var cn = connectionDB.GetSisConnection();
        await cn.OpenAsync();
        using var cmd = new OracleCommand(procedure, cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
        cmd.Parameters.Add("p_PageSize", OracleDbType.Int32).Value = value.PageSize <= 0 ? 10 : value.PageSize;
        cmd.Parameters.Add("p_PageNumber", OracleDbType.Int32).Value = value.PageNumber <= 0 ? 1 : value.PageNumber;
        cmd.Parameters.Add("p_SearchText", OracleDbType.Varchar2).Value = SupportDb.StringDbValue(value.SearchText);
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
        var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
        var list = new List<SisUsuarioResponse>();
        using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                list.Add(MapUsuario(reader));
            }
        }
        var message = SupportDb.GetMessage(pMessage);
        var isSuccess = SupportDb.IsSuccessMessage(message);
        return new ResultDto<List<SisUsuarioResponse>>(list) { Data = isSuccess ? list : null, IsValid = isSuccess, Message = message };
    }

    private async Task<ResultDto<SisUsuarioResponse>> ExecuteSingleAsync(string procedure, Action<OracleCommand> bind)
    {
        if (!SupportDb.TryGetEmpresa(config, out int empresa, out string errorMessage))
        {
            return new ResultDto<SisUsuarioResponse>(null!) { IsValid = false, Message = errorMessage };
        }

        using var cn = connectionDB.GetSisConnection();
        await cn.OpenAsync();
        using var cmd = new OracleCommand(procedure, cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
        bind(cmd);
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
        var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
        SisUsuarioResponse? item = null;
        using (var reader = await cmd.ExecuteReaderAsync())
        {
            if (await reader.ReadAsync())
            {
                item = MapUsuario(reader);
            }
        }
        var message = SupportDb.GetMessage(pMessage);
        var isSuccess = SupportDb.IsSuccessMessage(message) && item is not null;
        return new ResultDto<SisUsuarioResponse>(item!) { Data = isSuccess ? item : null, IsValid = isSuccess, Message = isSuccess ? message : "No se encontró el usuario indicado." };
    }

    private async Task<ResultDto<string>> ExecuteMessageAsync(string procedure, Action<OracleCommand> bind)
    {
        if (!SupportDb.TryGetEmpresa(config, out int empresa, out string errorMessage))
        {
            return new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = errorMessage };
        }

        using var cn = connectionDB.GetSisConnection();
        await cn.OpenAsync();
        using var cmd = new OracleCommand(procedure, cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
        bind(cmd);
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
        var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
        await cmd.ExecuteNonQueryAsync();
        var message = SupportDb.GetMessage(pMessage);
        var isSuccess = SupportDb.IsSuccessMessage(message);
        return new ResultDto<string>(isSuccess ? "OK" : string.Empty) { Data = isSuccess ? "OK" : null, IsValid = isSuccess, Message = message };
    }

    private async Task<ResultDto<int>> ExecuteScalarAsync(string procedure, Action<OracleCommand> bind, string outputName)
    {
        if (!SupportDb.TryGetEmpresa(config, out int empresa, out string errorMessage))
        {
            return new ResultDto<int>(0) { IsValid = false, Message = errorMessage };
        }

        using var cn = connectionDB.GetSisConnection();
        await cn.OpenAsync();
        using var cmd = new OracleCommand(procedure, cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
        bind(cmd);
        cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
        var pOutput = cmd.Parameters.Add(outputName, OracleDbType.Int32, ParameterDirection.Output);
        var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
        await cmd.ExecuteNonQueryAsync();
        var message = SupportDb.GetMessage(pMessage);
        var isSuccess = SupportDb.IsSuccessMessage(message);
        return new ResultDto<int>(isSuccess ? SupportDb.GetIntOutput(pOutput) : 0) { IsValid = isSuccess, Message = message };
    }

    private static SisUsuarioResponse MapUsuario(IDataReader reader)
    {
        decimal? cedula = null;
        int cedulaOrdinal = reader.GetOrdinal("CEDULA");
        if (!reader.IsDBNull(cedulaOrdinal))
        {
            cedula = Convert.ToDecimal(reader.GetValue(cedulaOrdinal));
        }

        return new SisUsuarioResponse(
            reader.SafeGetInt32("CODIGO_USUARIO"),
            reader.SafeGetString("USUARIO"),
            reader.SafeGetString("LOGIN"),
            cedula,
            reader.SafeGetString("STATUS"),
            reader.SafeGetString("EMAIL"),
            SupportDb.SafeGetFlag(reader, "RECIBE_EMAIL"),
            SupportDb.SafeGetFlag(reader, "ES_ANALISTA_SOPORTE"),
            SupportDb.SafeGetFlag(reader, "ES_ANALISTA_CNT"),
            SupportDb.SafeGetFlag(reader, "ES_ADMIN_CNT"),
            SupportDb.SafeGetFlag(reader, "IS_SUPERUSER"),
            reader.SafeGetInt32("CODIGO_EMPRESA")
        );
    }

    private sealed record SupportPermissionTargetUser(string Usuario, string Login, bool EsAnalistaSoporte, bool EsAnalistaCnt, bool EsAdminCnt, bool IsSuperuser);
    private sealed record SisRoleData(int CodigoUsuarioRol, string JsonMenu);

    private static async Task<SupportPermissionTargetUser?> GetSupportPermissionTargetUserAsync(OracleConnection cn, int codigoUsuario, int empresa)
    {
        using var cmd = new OracleCommand(@"
            SELECT USUARIO, LOGIN,
                   NVL(ES_ANALISTA_SOPORTE, 0) ES_ANALISTA_SOPORTE,
                   NVL(ES_ANALISTA_CNT, 0) ES_ANALISTA_CNT,
                   NVL(ES_ADMIN_CNT, 0) ES_ADMIN_CNT,
                   NVL(IS_SUPERUSER, 0) IS_SUPERUSER
              FROM SIS.SIS_USUARIOS
             WHERE CODIGO_USUARIO = :codigoUsuario
               AND CODIGO_EMPRESA = :empresa", cn)
        {
            BindByName = true
        };
        cmd.Parameters.Add("codigoUsuario", OracleDbType.Int32).Value = codigoUsuario;
        cmd.Parameters.Add("empresa", OracleDbType.Int32).Value = empresa;

        using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new SupportPermissionTargetUser(
            reader.SafeGetString("USUARIO"),
            reader.SafeGetString("LOGIN"),
            SupportDb.SafeGetFlag(reader, "ES_ANALISTA_SOPORTE"),
            SupportDb.SafeGetFlag(reader, "ES_ANALISTA_CNT"),
            SupportDb.SafeGetFlag(reader, "ES_ADMIN_CNT"),
            SupportDb.SafeGetFlag(reader, "IS_SUPERUSER")
        );
    }

    private static Task<SisRoleData> GetSisRoleByUserAsync(OracleConnection cn, int codigoUsuario) =>
        GetUserRoleByDescriptionAsync(cn, codigoUsuario, SisRoleDescription);

    private static async Task<SisRoleData> GetUserRoleByDescriptionAsync(OracleConnection cn, int codigoUsuario, string descripcion)
    {
        using var cmd = new OracleCommand(@"
            SELECT CODIGO_USUARIO_ROL, JSON_MENU
              FROM SIS.OSS_USUARIO_ROL
             WHERE CODIGO_USUARIO = :codigoUsuario
               AND UPPER(TRIM(DESCRIPCION)) = UPPER(TRIM(:descripcion))
             ORDER BY CODIGO_USUARIO_ROL DESC", cn)
        {
            BindByName = true
        };
        cmd.Parameters.Add("codigoUsuario", OracleDbType.Int32).Value = codigoUsuario;
        cmd.Parameters.Add("descripcion", OracleDbType.Varchar2).Value = descripcion;

        using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync())
        {
            return new SisRoleData(0, "[]");
        }

        return new SisRoleData(
            reader.SafeGetInt32("CODIGO_USUARIO_ROL"),
            ReadClob(reader, "JSON_MENU")
        );
    }

    private static Task<int> InsertSisRoleAsync(OracleConnection cn, string login, int codigoUsuario, string jsonMenu) =>
        InsertUserRoleAsync(cn, login, codigoUsuario, SisRoleDescription, jsonMenu);

    private static async Task<int> InsertUserRoleAsync(OracleConnection cn, string login, int codigoUsuario, string descripcion, string jsonMenu)
    {
        using var cmd = new OracleCommand(@"
            INSERT INTO SIS.OSS_USUARIO_ROL (
                CODIGO_USUARIO_ROL, USUARIO, CODIGO_USUARIO, DESCRIPCION, JSON_MENU
            ) VALUES (
                (SELECT NVL(MAX(CODIGO_USUARIO_ROL), 0) + 1 FROM SIS.OSS_USUARIO_ROL),
                :usuario, :codigoUsuario, :descripcion, :jsonMenu
            )
            RETURNING CODIGO_USUARIO_ROL INTO :codigoUsuarioRol", cn)
        {
            BindByName = true
        };
        cmd.Parameters.Add("usuario", OracleDbType.Varchar2).Value = SupportDb.StringDbValue(login);
        cmd.Parameters.Add("codigoUsuario", OracleDbType.Int32).Value = codigoUsuario;
        cmd.Parameters.Add("descripcion", OracleDbType.Varchar2).Value = descripcion;
        cmd.Parameters.Add("jsonMenu", OracleDbType.Clob).Value = jsonMenu;
        var output = cmd.Parameters.Add("codigoUsuarioRol", OracleDbType.Int32, ParameterDirection.Output);

        await cmd.ExecuteNonQueryAsync();

        return SupportDb.GetIntOutput(output);
    }

    private static Task<int> UpdateSisRoleJsonAsync(OracleConnection cn, int codigoUsuarioRol, string login, int codigoUsuario, string jsonMenu) =>
        UpdateUserRoleJsonAsync(cn, codigoUsuarioRol, login, codigoUsuario, SisRoleDescription, jsonMenu);

    private static async Task<int> UpdateUserRoleJsonAsync(OracleConnection cn, int codigoUsuarioRol, string login, int codigoUsuario, string descripcion, string jsonMenu)
    {
        using var cmd = new OracleCommand(@"
            UPDATE SIS.OSS_USUARIO_ROL
               SET USUARIO = :usuario,
                   CODIGO_USUARIO = :codigoUsuario,
                   DESCRIPCION = :descripcion,
                   JSON_MENU = :jsonMenu
             WHERE CODIGO_USUARIO_ROL = :codigoUsuarioRol", cn)
        {
            BindByName = true
        };
        cmd.Parameters.Add("usuario", OracleDbType.Varchar2).Value = SupportDb.StringDbValue(login);
        cmd.Parameters.Add("codigoUsuario", OracleDbType.Int32).Value = codigoUsuario;
        cmd.Parameters.Add("descripcion", OracleDbType.Varchar2).Value = descripcion;
        cmd.Parameters.Add("jsonMenu", OracleDbType.Clob).Value = jsonMenu;
        cmd.Parameters.Add("codigoUsuarioRol", OracleDbType.Int32).Value = codigoUsuarioRol;

        await cmd.ExecuteNonQueryAsync();

        return codigoUsuarioRol;
    }

    private static string MergeSupportMenu(string currentJsonMenu, JsonObject supportNode) =>
        MergeModuleMenu(currentJsonMenu, "Soporte", supportNode);

    private static string BuildPrincipalMenu(JsonObject moduleNode)
    {
        var root = new JsonArray(moduleNode);
        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    private static string RemoveModuleFromSystemMenu(string currentJsonMenu, string moduleTitle)
    {
        var root = ParseMenuArray(currentJsonMenu);

        foreach (var item in root.OfType<JsonObject>())
        {
            if (IsTitle(item, "Sistema"))
            {
                RemoveModuleNodes(GetOrCreateChildren(item), moduleTitle);
            }
        }

        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    private static string MergeModuleMenu(string currentJsonMenu, string moduleTitle, JsonObject moduleNode)
    {
        var root = ParseMenuArray(currentJsonMenu);
        JsonObject? systemNode = null;

        foreach (var item in root.OfType<JsonObject>())
        {
            if (IsTitle(item, "Sistema"))
            {
                systemNode ??= item;
                RemoveModuleNodes(GetOrCreateChildren(item), moduleTitle);
            }
        }

        if (systemNode is null)
        {
            systemNode = new JsonObject
            {
                ["title"] = "Sistema",
                ["icon"] = "mdi:shield-account-outline",
                ["children"] = new JsonArray()
            };
            root.Add(systemNode);
        }

        GetOrCreateChildren(systemNode).Add(moduleNode);

        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = false });
    }

    private static JsonArray ParseMenuArray(string jsonMenu)
    {
        try
        {
            var node = JsonNode.Parse(string.IsNullOrWhiteSpace(jsonMenu) ? "[]" : jsonMenu);
            if (node is JsonArray array)
            {
                return array;
            }

            return node is null ? new JsonArray() : new JsonArray(node);
        }
        catch (JsonException)
        {
            return new JsonArray();
        }
    }

    private static JsonArray GetOrCreateChildren(JsonObject node)
    {
        if (node["children"] is JsonArray children)
        {
            return children;
        }

        children = new JsonArray();
        node["children"] = children;
        return children;
    }

    private static void RemoveModuleNodes(JsonArray children, string title)
    {
        for (var i = children.Count - 1; i >= 0; i--)
        {
            if (children[i] is JsonObject child && IsTitle(child, title))
            {
                children.RemoveAt(i);
            }
        }
    }

    private static bool IsTitle(JsonObject item, string title)
    {
        return string.Equals(item["title"]?.GetValue<string>(), title, StringComparison.OrdinalIgnoreCase);
    }

    private static JsonObject BuildSupportMenuNode(string perfil)
    {
        return perfil switch
        {
            "Administrador De Soporte" => SupportNode(
                TicketNode("soporte.tickets.crear", "soporte.tickets.ver_todos", "soporte.tickets.asignar", "soporte.tickets.cambiar_estado", "soporte.tickets.cerrar", "soporte.comentarios.crear", "soporte.comentarios.internos"),
                DashboardNode(),
                NotificationsNode(),
                ConfigNode(),
                SisUsersNode()
            ),
            "Analista De Soporte" => SupportNode(
                TicketNode("soporte.tickets.crear", "soporte.tickets.ver_propios", "soporte.tickets.ver_asignados", "soporte.tickets.ver_sin_asignar", "soporte.tickets.asignar", "soporte.tickets.cambiar_estado", "soporte.comentarios.crear", "soporte.comentarios.internos"),
                DashboardNode(),
                NotificationsNode()
            ),
            _ => SupportNode(
                TicketNode("soporte.tickets.crear", "soporte.tickets.ver_propios", "soporte.comentarios.crear"),
                NotificationsNode()
            )
        };
    }

    private static JsonObject BuildCntMenuNode(string perfil)
    {
        return perfil switch
        {
            "Administrador De Contabilidad" => CntNode(
                CntComprobantesNode("contabilidad.comprobantes.ver", "contabilidad.comprobantes.crear", "contabilidad.comprobantes.editar", "contabilidad.comprobantes.editar_automatico", "contabilidad.comprobantes.eliminar"),
                CntAutomaticosNode(),
                CntProcesosNode("contabilidad.cierre.ver", "contabilidad.cierre.precierre", "contabilidad.cierre.cierre", "contabilidad.cierre.reverso"),
                CntConciliacionNode("contabilidad.conciliacion.ver", "contabilidad.conciliacion.importar", "contabilidad.conciliacion.admin", "contabilidad.conciliacion.cierre_forzado", "contabilidad.conciliacion.editar_precierre"),
                CntReportesNode(),
                CntCatalogosNode("contabilidad.catalogos.ver", "contabilidad.catalogos.admin"),
                CntConfigNode()
            ),
            "Analista De Contabilidad" => CntNode(
                CntComprobantesNode("contabilidad.comprobantes.ver", "contabilidad.comprobantes.crear", "contabilidad.comprobantes.editar"),
                CntAutomaticosNode(),
                CntProcesosNode("contabilidad.cierre.ver", "contabilidad.cierre.precierre"),
                CntConciliacionNode("contabilidad.conciliacion.ver", "contabilidad.conciliacion.importar"),
                CntReportesNode(),
                CntCatalogosNode("contabilidad.catalogos.ver")
            ),
            _ => CntNode(
                CntComprobantesNode("contabilidad.comprobantes.ver", "contabilidad.comprobantes.crear")
            )
        };
    }

    private static JsonObject SupportNode(params JsonObject[] children) => new()
    {
        ["title"] = "Soporte",
        ["children"] = ToJsonArray(children)
    };

    private static JsonObject TicketNode(params string[] permissions) => new()
    {
        ["title"] = "Tickets",
        ["path"] = "/apps/soporte/tickets",
        ["permissions"] = ToJsonArray(permissions)
    };

    private static JsonObject DashboardNode() => new()
    {
        ["title"] = "Dashboard",
        ["path"] = "/apps/soporte/dashboard",
        ["permissions"] = ToJsonArray("soporte.dashboard.ver")
    };

    private static JsonObject NotificationsNode() => new()
    {
        ["title"] = "Notificaciones",
        ["path"] = "/apps/soporte/notificaciones"
    };

    private static JsonObject ConfigNode() => new()
    {
        ["title"] = "Configuracion",
        ["path"] = "/apps/soporte/configuracion",
        ["permissions"] = ToJsonArray("soporte.catalogos.admin", "soporte.sla.admin", "soporte.usuarios.configurar")
    };

    private static JsonObject SisUsersNode() => new()
    {
        ["title"] = "Usuarios SIS",
        ["path"] = "/apps/sis/usuarios",
        ["permissions"] = ToJsonArray("soporte.usuarios.configurar")
    };

    private static JsonObject CntNode(params JsonObject[] children) => new()
    {
        ["title"] = "Contabilidad",
        ["icon"] = "mdi:calculator-variant-outline",
        ["children"] = ToJsonArray(children)
    };

    private static JsonObject CntComprobantesNode(params string[] permissions) => new()
    {
        ["title"] = "Comprobantes",
        ["path"] = "/apps/cnt/comprobantes",
        ["permissions"] = ToJsonArray(permissions)
    };

    private static JsonObject CntAutomaticosNode() => new()
    {
        ["title"] = "Proceso Automatico",
        ["path"] = "/apps/cnt/proceso-automatico",
        ["permissions"] = ToJsonArray("contabilidad.comprobantes.generar_automatico")
    };

    private static JsonObject CntProcesosNode(params string[] cierrePermissions) => new()
    {
        ["title"] = "Procesos",
        ["children"] = ToJsonArray(
            new JsonObject
            {
                ["title"] = "Cierre contable",
                ["path"] = "/apps/cnt/procesos/cierre-contable",
                ["permissions"] = ToJsonArray(cierrePermissions)
            }
        )
    };

    private static JsonObject CntConciliacionNode(params string[] permissions) => new()
    {
        ["title"] = "Conciliacion",
        ["children"] = ToJsonArray(
            new JsonObject
            {
                ["title"] = "Conciliaciones",
                ["path"] = "/apps/cnt/conciliacion",
                ["permissions"] = ToJsonArray(permissions)
            },
            new JsonObject
            {
                ["title"] = "Importar estados de cuenta",
                ["path"] = "/apps/cnt/conciliacion/carga-banco",
                ["permissions"] = ToJsonArray("contabilidad.conciliacion.importar")
            },
            new JsonObject
            {
                ["title"] = "Estados de cuenta",
                ["path"] = "/apps/cnt/conciliacion/estados-cuenta",
                ["permissions"] = ToJsonArray("contabilidad.conciliacion.ver")
            },
            new JsonObject
            {
                ["title"] = "Libro banco",
                ["path"] = "/apps/cnt/conciliacion/libro-banco",
                ["permissions"] = ToJsonArray("contabilidad.conciliacion.ver")
            },
            new JsonObject
            {
                ["title"] = "Configuracion",
                ["path"] = "/apps/cnt/conciliacion/configuracion",
                ["permissions"] = ToJsonArray("contabilidad.conciliacion.admin")
            },
            new JsonObject
            {
                ["title"] = "Formatos banco",
                ["path"] = "/apps/cnt/conciliacion/formatos-banco",
                ["permissions"] = ToJsonArray("contabilidad.conciliacion.formatos.ver", "contabilidad.conciliacion.formatos.editar")
            }
        )
    };

    private static JsonObject CntReportesNode() => new()
    {
        ["title"] = "Reportes",
        ["children"] = ToJsonArray(
            new JsonObject
            {
                ["title"] = "Mayor Analitico",
                ["path"] = "/apps/cnt/reportes/mayor-analitico",
                ["permissions"] = ToJsonArray("contabilidad.reportes.ver")
            },
            new JsonObject
            {
                ["title"] = "Movimiento Auxiliar",
                ["path"] = "/apps/cnt/reportes/movimiento-auxiliar",
                ["permissions"] = ToJsonArray("contabilidad.reportes.ver")
            }
        )
    };

    private static JsonObject CntCatalogosNode(params string[] permissions) => new()
    {
        ["title"] = "Catalogos",
        ["children"] = ToJsonArray(
            new JsonObject
            {
                ["title"] = "Plan de cuentas",
                ["path"] = "/apps/cnt/catalogos/plan-cuentas",
                ["permissions"] = ToJsonArray(permissions)
            },
            new JsonObject
            {
                ["title"] = "Descriptivas",
                ["path"] = "/apps/cnt/catalogos/descriptivas",
                ["permissions"] = ToJsonArray(permissions)
            },
            new JsonObject
            {
                ["title"] = "Rubros",
                ["path"] = "/apps/cnt/catalogos/rubros",
                ["permissions"] = ToJsonArray(permissions)
            },
            new JsonObject
            {
                ["title"] = "Balances",
                ["path"] = "/apps/cnt/catalogos/balances",
                ["permissions"] = ToJsonArray(permissions)
            },
            new JsonObject
            {
                ["title"] = "Mayores",
                ["path"] = "/apps/cnt/catalogos/mayores",
                ["permissions"] = ToJsonArray(permissions)
            },
            new JsonObject
            {
                ["title"] = "Auxiliares",
                ["path"] = "/apps/cnt/catalogos/auxiliares",
                ["permissions"] = ToJsonArray(permissions)
            },
            new JsonObject
            {
                ["title"] = "Auxiliares PUC",
                ["path"] = "/apps/cnt/catalogos/auxiliares-puc",
                ["permissions"] = ToJsonArray(permissions)
            },
            new JsonObject
            {
                ["title"] = "Periodos",
                ["path"] = "/apps/cnt/catalogos/periodos",
                ["permissions"] = ToJsonArray(permissions)
            },
            new JsonObject
            {
                ["title"] = "Relacion documentos",
                ["path"] = "/apps/cnt/catalogos/relacion-documentos",
                ["permissions"] = ToJsonArray(permissions)
            },
            new JsonObject
            {
                ["title"] = "Saldos",
                ["path"] = "/apps/cnt/catalogos/saldos",
                ["permissions"] = ToJsonArray(permissions)
            }
        )
    };

    private static JsonObject CntConfigNode() => new()
    {
        ["title"] = "Configuracion",
        ["path"] = "/apps/cnt/configuracion",
        ["permissions"] = ToJsonArray("contabilidad.catalogos.admin", "contabilidad.comprobantes.reordenar")
    };

    private static JsonArray ToJsonArray(params string[] values)
    {
        var array = new JsonArray();
        foreach (var value in values)
        {
            array.Add(value);
        }

        return array;
    }

    private static JsonArray ToJsonArray(params JsonObject[] values)
    {
        var array = new JsonArray();
        foreach (var value in values)
        {
            array.Add(value);
        }

        return array;
    }

    private static string ReadClob(IDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
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
}
