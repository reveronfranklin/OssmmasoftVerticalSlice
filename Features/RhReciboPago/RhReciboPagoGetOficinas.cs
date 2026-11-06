using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.RhReciboPago;

public class RhReciboPagoGetOficinasHandler(ConnectionDB db, IConfiguration config)
{
    public async Task<ResultDto<List<RhReciboOficina>>> HandleAsync(RhReciboPagoFilter filter, CancellationToken ct = default)
    {
        if (RhReciboPagoDb.Validate(filter) is { } error) return RhReciboPagoDb.Fail<List<RhReciboOficina>>(error);
        var data = await RhReciboPagoDb.ReadAsync(db, config, filter with { CodigoIcp = null, CodigoPersona = null }, true, ct);
        if (!data.IsValid || data.Data is null) return RhReciboPagoDb.Fail<List<RhReciboOficina>>(data.Message);
        var rows = data.Data.Lineas.Select(row => new RhReciboOficina(row.CodigoIcpConcat, row.CodigoIcp, row.Denominacion))
            .Distinct().OrderBy(row => row.Denominacion).ToList();
        return new(rows) { IsValid = true, Message = "success", CantidadRegistros = rows.Count };
    }
}

[Authorize]
[ApiController]
[Route("api/RhReciboPago")]
public class RhReciboPagoGetOficinasController(ConnectionDB db, IConfiguration config) : ControllerBase
{
    [HttpPost("GetOficinas")]
    public async Task<IActionResult> GetOficinas(RhReciboPagoFilter filter, CancellationToken ct)
        => Ok(await new RhReciboPagoGetOficinasHandler(db, config).HandleAsync(filter, ct));
}
