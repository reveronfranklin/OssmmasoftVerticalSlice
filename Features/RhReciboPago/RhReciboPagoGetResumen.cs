using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.RhReciboPago;

public class RhReciboPagoGetResumenHandler(ConnectionDB db, IConfiguration config)
{
    public async Task<ResultDto<List<RhReciboResumen>>> HandleAsync(RhReciboPagoFilter filter, CancellationToken ct = default)
    {
        var data = await RhReciboPagoDb.ReadAsync(db, config, filter, true, ct);
        if (!data.IsValid || data.Data is null) return RhReciboPagoDb.Fail<List<RhReciboResumen>>(data.Message);
        var rows = RhReciboPagoDb.Summary(data.Data.Lineas);
        return new(rows) { IsValid = true, Message = "success", CantidadRegistros = rows.Count };
    }
}

[Authorize]
[ApiController]
[Route("api/RhReciboPago")]
public class RhReciboPagoGetResumenController(ConnectionDB db, IConfiguration config) : ControllerBase
{
    [HttpPost("GetResumen")]
    public async Task<IActionResult> GetResumen(RhReciboPagoFilter filter, CancellationToken ct)
        => Ok(await new RhReciboPagoGetResumenHandler(db, config).HandleAsync(filter, ct));
}
