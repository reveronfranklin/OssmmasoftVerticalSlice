using Microsoft.AspNetCore.Http;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Security.Claims;

namespace OssmmasoftVerticalSlice.Features.Support;

public class GetSupportPermissionsHandler(ConnectionDB connectionDB, IConfiguration config)
{
    public async Task<ResultDto<SupportPermissionsResponse>> HandleAsync(
        SupportPermissionsQuery value,
        ClaimsPrincipal user,
        HttpRequest request)
    {
        var userValidation = await SupportSecurity.ValidateRequestUserAsync(connectionDB, config, user, request, value.UsuarioId);
        if (!userValidation.IsValid)
        {
            return new ResultDto<SupportPermissionsResponse>(null!) { IsValid = false, Message = userValidation.Message };
        }

        if (!SupportDb.TryGetEmpresa(config, out int empresa, out string errorMessage))
        {
            return new ResultDto<SupportPermissionsResponse>(null!) { IsValid = false, Message = errorMessage };
        }

        using var cn = connectionDB.GetSisConnection();
        await cn.OpenAsync();
        var response = await SupportSecurity.GetPermissionsAsync(cn, value.UsuarioId, empresa);

        return new ResultDto<SupportPermissionsResponse>(response) { IsValid = true, Message = "Success" };
    }
}
