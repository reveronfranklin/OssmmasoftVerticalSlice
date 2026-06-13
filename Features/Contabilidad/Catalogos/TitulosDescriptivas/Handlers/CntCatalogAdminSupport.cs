using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;
using System.Security.Claims;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

internal record CntCatalogAdminValidation(bool IsValid, string Message, int Empresa);

internal static class CntCatalogAdminSupport
{
    public static async Task<CntCatalogAdminValidation> ValidateAsync(ConnectionDB connectionDB, IConfiguration config, ClaimsPrincipal user, HttpRequest request, int usuarioId, string permissionName)
    {
        var access = await CntSecurity.ValidateRequestUserAsync(connectionDB, config, user, request, usuarioId);
        if (!access.IsValid)
        {
            return new CntCatalogAdminValidation(false, access.Message, 0);
        }

        var permission = await CntSecurity.CheckPermissionAsync(connectionDB, config, usuarioId, permissionName);
        if (!permission.IsValid)
        {
            return new CntCatalogAdminValidation(false, permission.Message, 0);
        }

        return CntDb.TryGetEmpresa(config, out int empresa, out string errorMessage)
            ? new CntCatalogAdminValidation(true, "Success", empresa)
            : new CntCatalogAdminValidation(false, errorMessage, 0);
    }

    public static async Task<CntCatalogAdminValidation> ValidateAnyAsync(ConnectionDB connectionDB, IConfiguration config, ClaimsPrincipal user, HttpRequest request, int usuarioId, params string[] permissionNames)
    {
        var access = await CntSecurity.ValidateRequestUserAsync(connectionDB, config, user, request, usuarioId);
        if (!access.IsValid)
        {
            return new CntCatalogAdminValidation(false, access.Message, 0);
        }

        var permission = await CntSecurity.CheckAnyPermissionAsync(connectionDB, config, usuarioId, permissionNames);
        if (!permission.IsValid)
        {
            return new CntCatalogAdminValidation(false, permission.Message, 0);
        }

        return CntDb.TryGetEmpresa(config, out int empresa, out string errorMessage)
            ? new CntCatalogAdminValidation(true, "Success", empresa)
            : new CntCatalogAdminValidation(false, errorMessage, 0);
    }

    public static CntTituloResponse MapTitulo(IDataReader reader) =>
        new(
            reader.SafeGetInt32("TITULO_ID"),
            CntDb.SafeGetNullableInt(reader, "TITULO_FK_ID"),
            reader.SafeGetString("TITULO"),
            reader.SafeGetString("CODIGO"),
            reader.SafeGetString("EXTRA1"),
            reader.SafeGetString("EXTRA2"),
            reader.SafeGetString("EXTRA3"),
            CntDb.SafeGetNullableInt(reader, "CODIGO_EMPRESA"));

    public static CntDescriptivaResponse MapDescriptiva(IDataReader reader) =>
        new(
            reader.SafeGetInt32("DESCRIPCION_ID"),
            CntDb.SafeGetNullableInt(reader, "DESCRIPCION_FK_ID"),
            reader.SafeGetInt32("TITULO_ID"),
            reader.SafeGetString("TITULO"),
            reader.SafeGetString("DESCRIPCION"),
            reader.SafeGetString("CODIGO"),
            reader.SafeGetString("EXTRA1"),
            reader.SafeGetString("EXTRA2"),
            reader.SafeGetString("EXTRA3"),
            CntDb.SafeGetNullableInt(reader, "CODIGO_EMPRESA"));

    public static async Task<ResultDto<string>> ExecuteDeleteAsync(ConnectionDB connectionDB, string procedure, int id, string idParameter, int empresa)
    {
        try
        {
            using var cn = connectionDB.GetCntConnection();
            await cn.OpenAsync();
            using var cmd = new OracleCommand(procedure, cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
            cmd.Parameters.Add(idParameter, OracleDbType.Int32).Value = id;
            cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;
            var pMessage = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

            await cmd.ExecuteNonQueryAsync();
            var message = CntDb.GetMessage(pMessage);
            var isSuccess = CntDb.IsSuccessMessage(message);
            return new ResultDto<string>(message) { Data = isSuccess ? message : null, IsValid = isSuccess, Message = message };
        }
        catch (Exception ex)
        {
            return new ResultDto<string>(string.Empty) { Data = null, IsValid = false, Message = ex.Message };
        }
    }
}
