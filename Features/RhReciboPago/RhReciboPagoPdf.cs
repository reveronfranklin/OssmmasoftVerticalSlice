using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.RhReciboPago;

public class RhReciboPagoPdfHandler(ConnectionDB db, IConfiguration config, IWebHostEnvironment environment,
    ILogger<RhReciboPagoPdfHandler> logger)
{
    public async Task<ResultDto<byte[]>> HandleAsync(RhReciboPagoFilter filter, CancellationToken ct = default)
    {
        var data = await RhReciboPagoDb.ReadAsync(db, config, filter, false, ct);
        if (!data.IsValid || data.Data is null) return RhReciboPagoDb.Fail<byte[]>(data.Message);
        if (data.Data.Lineas.Count == 0) return RhReciboPagoDb.Fail<byte[]>("No hay recibos para los filtros seleccionados.");
        try
        {
            var logo = ResolveLogo(config, environment);
            if (logo is null) return RhReciboPagoDb.Fail<byte[]>("No se encontro el logo del recibo. Verifique la configuracion del servidor.");
            ct.ThrowIfCancellationRequested();
            var pdf = RhReciboPagoPdfGenerator.Generate(data.Data.Lineas, File.ReadAllBytes(logo),
                data.Data.TipoNomina, data.Data.EsTemporal && !(filter.CodigoPersona > 0));
            ct.ThrowIfCancellationRequested();
            return new(pdf) { IsValid = true, Message = "success" };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error PDF recibo vertical. Nomina {Nomina}, periodo {Periodo}, persona {Persona}",
                filter.CodigoTipoNomina, filter.CodigoPeriodo, filter.CodigoPersona);
            return RhReciboPagoDb.Fail<byte[]>("No se pudo generar el PDF del recibo. Revise los registros del servidor.");
        }
    }

    internal static string? ResolveLogo(IConfiguration config, IWebHostEnvironment environment)
    {
        var explicitLogo = config["settings:RhReciboLogo"];
        if (!string.IsNullOrWhiteSpace(explicitLogo))
        {
            var path = Path.IsPathRooted(explicitLogo) ? explicitLogo : Path.Combine(environment.ContentRootPath, explicitLogo);
            return File.Exists(path) ? path : null;
        }
        var candidates = new[] {
            Path.Combine(config["settings:BmFiles"] ?? "", "LogoIzquierda.jpeg"),
            Path.Combine(environment.ContentRootPath, "Assets", "Reports", "logoLeft.jpeg"),
            Path.Combine(AppContext.BaseDirectory, "Assets", "Reports", "logoLeft.jpeg")
        };
        return candidates.Select(path => Path.IsPathRooted(path) ? path : Path.Combine(environment.ContentRootPath, path))
            .FirstOrDefault(File.Exists);
    }
}

[Authorize]
[ApiController]
[Route("api/RhReciboPago")]
public class RhReciboPagoPdfController(ConnectionDB db, IConfiguration config, IWebHostEnvironment environment,
    ILogger<RhReciboPagoPdfHandler> logger) : ControllerBase
{
    [HttpPost("pdf")]
    public async Task<IActionResult> Pdf(RhReciboPagoFilter filter, CancellationToken ct)
    {
        var result = await new RhReciboPagoPdfHandler(db, config, environment, logger).HandleAsync(filter, ct);
        if (!result.IsValid) return Ok(result);
        Response.Headers.CacheControl = "no-store";
        Response.Headers.ContentDisposition = "inline; filename=recibo-nomina.pdf";
        return File(result.Data!, "application/pdf");
    }
}
