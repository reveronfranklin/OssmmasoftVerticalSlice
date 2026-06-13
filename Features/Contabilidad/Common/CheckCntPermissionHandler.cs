using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Security.Claims;

namespace OssmmasoftVerticalSlice.Features.Contabilidad;

public class CheckCntPermissionHandler(ConnectionDB connectionDB, IConfiguration config)
{
    private static readonly HashSet<string> AllowedPermissions = new(StringComparer.OrdinalIgnoreCase)
    {
        CntSecurity.ComprobanteView,
        CntSecurity.ComprobanteCreate,
        CntSecurity.ComprobanteEdit,
        CntSecurity.ComprobanteEditAutomatic,
        CntSecurity.ComprobanteDelete,
        CntSecurity.ComprobanteAutomatic,
        CntSecurity.ComprobanteReorder,
        CntSecurity.CatalogView,
        CntSecurity.CatalogAdmin,
        CntSecurity.ReportView,
        CntSecurity.ConciliacionView,
        CntSecurity.ConciliacionImport,
        CntSecurity.ConciliacionAdmin,
        CntSecurity.ConciliacionForceClose,
        CntSecurity.ConciliacionEditPreclose,
        CntSecurity.ConciliacionFormatsView,
        CntSecurity.ConciliacionFormatsEdit,
        CntSecurity.ConciliacionOcr,
        CntSecurity.ConciliacionReprocess,
        CntSecurity.CierreView,
        CntSecurity.CierrePrecierre,
        CntSecurity.CierreCierre,
        CntSecurity.CierreReverso
    };

    public async Task<ResultDto<CntPermissionResponse>> HandleAsync(CntPermissionQuery value, ClaimsPrincipal user, HttpRequest request)
    {
        if (!AllowedPermissions.Contains(value.Permission))
        {
            return new ResultDto<CntPermissionResponse>(new CntPermissionResponse(false, value.Permission))
            {
                Data = new CntPermissionResponse(false, value.Permission),
                IsValid = false,
                Message = "Permiso CNT no reconocido."
            };
        }

        var userValidation = await CntSecurity.ValidateRequestUserAsync(connectionDB, config, user, request, value.UsuarioId);
        if (!userValidation.IsValid)
        {
            return new ResultDto<CntPermissionResponse>(new CntPermissionResponse(false, value.Permission))
            {
                Data = new CntPermissionResponse(false, value.Permission),
                IsValid = false,
                Message = userValidation.Message
            };
        }

        var permission = await CntSecurity.CheckPermissionAsync(connectionDB, config, value.UsuarioId, value.Permission);
        var response = new CntPermissionResponse(permission.IsValid, value.Permission);

        return new ResultDto<CntPermissionResponse>(response)
        {
            Data = response,
            IsValid = permission.IsValid,
            Message = permission.IsValid ? "Success" : permission.Message
        };
    }
}
