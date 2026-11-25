using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace OssmmasoftVerticalSlice.Features.OracleAnalisis;

public sealed class OracleAnalisisAuthentication(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger,
    UrlEncoder encoder, IConfiguration config)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var settings = OracleAnalisisOptions.Read(config);
        var header = Request.Headers[OracleAnalisisRules.Header];
        if (header.Count != 1 || !OracleAnalisisRules.ValidToken(settings, header[0], DateTimeOffset.UtcNow))
            return Task.FromResult(AuthenticateResult.Fail("Token de servicio ausente, invalido o vencido."));
        var claims = new List<Claim> { new(ClaimTypes.Name, settings.ServiceAccess.ClientId) };
        claims.AddRange(settings.ServiceAccess.Permissions.Select(p => new Claim("permission", p)));
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }
}
