using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.BienesMunicipales;

[ApiController]
[Route("api/BmUbicacionesHistorico")]
public class BmUbicacionesHistoricoController(ConnectionDB connectionDB, IConfiguration config) : ControllerBase
{
    [HttpPost("GetByDir")]
    public async Task<IActionResult> GetByDir(BmUbicacionHistoricoRequest request)
    {
        if (!BmDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return Ok(BmDb.InvalidList<BmUbicacionHistoricoResponse>(error));
        }

        using var cn = connectionDB.GetBmConnection();
        var openError = await BmDb.TryOpenAsync(cn, "BM");
        if (openError is not null) return Ok(BmDb.InvalidList<BmUbicacionHistoricoResponse>(openError));

        using var cmd = BmDb.StoredProcedure("BM.SP_BM_UBI_HIST_GET", cn);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_CodigoDirBien", OracleDbType.Int32).Value = request.CodigoDirBien;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);

        return Ok(await BmDb.ExecuteListAsync(cmd, reader => new BmUbicacionHistoricoResponse(
            reader.SafeGetInt32("CODIGO_H_DIR_BIEN"),
            reader.SafeGetInt32("CODIGO_DIR_BIEN"),
            reader.SafeGetInt32("CODIGO_ICP"),
            reader.SafeGetString("UNIDAD_EJECUTORA"),
            reader.SafeGetString("DIRECCION"),
            BmDb.GetDate(reader, "FECHA_INI"),
            BmDb.GetDate(reader, "FECHA_FIN"),
            BmDb.GetDate(reader, "FECHA_H_INS")
        )));
    }
}
