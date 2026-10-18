using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.RhFamiliares;

public class RhFamiliaresDeleteHandler(ConnectionDB db, IConfiguration config)
{
    public Task<ResultDto<RhFamiliarDelete>> HandleAsync(RhFamiliarDelete request)
        => RhFamiliaresDb.Execute(db, config, "RH.SP_RH_FAM_DEL", cmd =>
            cmd.Parameters.Add("p_FAMILIAR", OracleDbType.Int32).Value = request.CodigoFamiliar,
            async cmd => { await cmd.ExecuteNonQueryAsync(); return request; });
}

[Authorize]
[ApiController]
[Route("api/RhFamiliares")]
public class RhFamiliaresDeleteController(ConnectionDB db, IConfiguration config) : ControllerBase
{
    [HttpPost("Delete")]
    public async Task<IActionResult> Delete(RhFamiliarDelete request)
        => Ok(await new RhFamiliaresDeleteHandler(db, config).HandleAsync(request));
}
