using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using System.Data;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.RhFamiliares;

public class RhFamiliaresGetByPersonaHandler(ConnectionDB db, IConfiguration config)
{
    public Task<ResultDto<List<RhFamiliarResponse>>> HandleAsync(RhFamiliarFilter filter)
        => RhFamiliaresDb.Execute(db, config, "RH.SP_RH_FAM_GET_PER", cmd =>
        {
            cmd.Parameters.Add("p_PERSONA", OracleDbType.Int32).Value = filter.CodigoPersona;
            cmd.Parameters.Add("p_RESULT", OracleDbType.RefCursor, ParameterDirection.Output);
        }, async cmd =>
        {
            var rows = new List<RhFamiliarResponse>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) rows.Add(RhFamiliaresDb.Map(reader));
            return rows;
        });
}

[Authorize]
[ApiController]
[Route("api/RhFamiliares")]
public class RhFamiliaresGetByPersonaController(ConnectionDB db, IConfiguration config) : ControllerBase
{
    [HttpPost("GetByPersona")]
    public async Task<IActionResult> GetByPersona(RhFamiliarFilter filter)
        => Ok(await new RhFamiliaresGetByPersonaHandler(db, config).HandleAsync(filter));
}
