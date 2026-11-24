using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Features.Support;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.RhMotorNomina;

[ApiController]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme)]
[Route("api/RhMotorNomina")]
public sealed partial class RhNominaController(ConnectionDB db, IConfiguration config, ILogger<RhNominaController> logger) : ControllerBase
{
    private Task<IActionResult> Run<T>(string permission, Func<CancellationToken, ResultDto<T>> operation) =>
        RunAsync(permission, (_, _, ct) => Task.FromResult(operation(ct)));
    private async Task<IActionResult> RunAsync<T>(string permission, Func<int, int, CancellationToken, Task<ResultDto<T>>> operation, params string[] additionalPermissions)
    {
        if (!SupportDb.TryGetEmpresa(config, out var company, out var error))
            return Ok(new ResultDto<T>(default!) { IsValid = false, Message = error });
        if (company <= 0) return Ok(new ResultDto<T>(default!) { IsValid = false, Message = "Empresa configurada invalida." });
        var ct = HttpContext.RequestAborted;
        using var cn = db.GetSisConnection();
        try { await cn.OpenAsync(ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { return StatusCode(499); }
        catch { return Ok(new ResultDto<T>(default!) { IsValid = false, Message = "No se pudo conectar con seguridad SIS." }); }
        try
        {
            // JWT identity only; a service token or client-supplied user is not an actor.
            var actor = await SupportSecurity.GetAuthenticatedUserIdAsync(cn, User, company, null);
            if (actor <= 0) return Unauthorized();
            if (!await SupportSecurity.IsSuperUserAsync(cn, actor, company))
            {
                var permissions = await SupportSecurity.GetPermissionsAsync(cn, actor, company);
                if (!SupportSecurity.HasAny(permissions, permission) || additionalPermissions.Any(p=>!SupportSecurity.HasAny(permissions,p))) return Forbid(JwtBearerDefaults.AuthenticationScheme);
            }
            var result = await operation(company, actor, ct);
            if (System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(result).Length > 2 * 1024 * 1024)
                return Ok(new ResultDto<T>(default!) { IsValid = false, Message = "La traza excede 2 MiB. Reduzca el modelo de prueba." });
            logger.LogInformation("RhMotorNomina Request={RequestId} Empresa={Company} Actor={Actor} Permiso={Permission} Valido={Valid}",
                HttpContext.TraceIdentifier, company, actor, permission, result.IsValid);
            return Ok(result);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { return StatusCode(499); }
        catch (RhNomError ex)
        {
            return Ok(new ResultDto<T>(default!) { IsValid = false, Message = ex.Message });
        }
        catch (Oracle.ManagedDataAccess.Client.OracleException ex) when (ex.Number is 20001 or 20002 or 20003 or 20004 or 54 or 1)
        {
            var message = ex.Number switch
            {
                20002 => "La version cambio. Recargue antes de guardar.",
                20003 => "Modelo, tipo de nomina o variable no encontrados en la empresa.",
                54 => "La configuracion esta siendo editada. Reintente despues de recargar.",
                20004 or 1 => "Codigo duplicado o intento de modificar una revision/codigo inmutable.",
                _ => "Configuracion invalida. Revise los campos enviados."
            };
            return Ok(new ResultDto<T>(default!) { IsValid = false, Message = message });
        }
        catch (Exception ex)
        {
            logger.LogWarning("RhMotorNomina Request={RequestId} Fallo={Kind}", HttpContext.TraceIdentifier, ex.GetType().Name);
            return Ok(new ResultDto<T>(default!) { IsValid = false, Message = "No se pudo completar la operacion del motor de nomina." });
        }
    }
}
