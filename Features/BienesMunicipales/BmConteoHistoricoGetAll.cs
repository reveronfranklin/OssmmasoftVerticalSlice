using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.BienesMunicipales;

[ApiController]
[Route("api/BmConteoHistorico")]
public class BmConteoHistoricoController(ConnectionDB connectionDB, IConfiguration config) : ControllerBase
{
    [HttpGet("GetAll")]
    public async Task<IActionResult> GetAll()
    {
        if (!BmDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return Ok(BmDb.InvalidList<BmConteoResponse>(error));
        }

        using var cn = connectionDB.GetBmcConnection();
        var openError = await BmDb.TryOpenAsync(cn, "BMC");
        if (openError is not null) return Ok(BmDb.InvalidList<BmConteoResponse>(openError));

        using var cmd = BmDb.StoredProcedure("BMC.SP_BM_CONT_HIST_GET", cn);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);

        return Ok(await BmDb.ExecuteListAsync(cmd, BmConteoController.MapConteo));
    }
}
