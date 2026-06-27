using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.BienesMunicipales;

[ApiController]
[Route("api/BmPlacaCuarentena")]
public class BmPlacaCuarentenaController(ConnectionDB connectionDB, IConfiguration config) : ControllerBase
{
    [HttpGet("GetAll")]
    public async Task<IActionResult> GetAll()
    {
        if (!BmDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return Ok(BmDb.InvalidList<BmPlacaCuarentenaResponse>(error));
        }

        using var cn = connectionDB.GetBmConnection();
        var openError = await BmDb.TryOpenAsync(cn, "BM");
        if (openError is not null) return Ok(BmDb.InvalidList<BmPlacaCuarentenaResponse>(openError));

        using var cmd = BmDb.StoredProcedure("BM.SP_BM_PLACA_CUA_GET", cn);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);

        return Ok(await BmDb.ExecuteListAsync(cmd, MapPlacaCuarentena));
    }

    [HttpPost("Create")]
    public async Task<IActionResult> Create(BmPlacaCuarentenaRequest request)
    {
        return Ok(await MutateAsync("BM.SP_BM_PLACA_CUA_INS", request));
    }

    [HttpPost("Delete")]
    public async Task<IActionResult> Delete(BmPlacaCuarentenaRequest request)
    {
        return Ok(await MutateAsync("BM.SP_BM_PLACA_CUA_DEL", request));
    }

    private async Task<ResultDto<List<BmPlacaCuarentenaResponse>>> MutateAsync(string procedureName, BmPlacaCuarentenaRequest request)
    {
        if (!BmDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return BmDb.InvalidList<BmPlacaCuarentenaResponse>(error);
        }

        using var cn = connectionDB.GetBmConnection();
        var openError = await BmDb.TryOpenAsync(cn, "BM");
        if (openError is not null) return BmDb.InvalidList<BmPlacaCuarentenaResponse>(openError);

        using var cmd = BmDb.StoredProcedure(procedureName, cn);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_CodigoPlacaCua", OracleDbType.Int32).Value = request.CodigoPlacaCuarentena;
        cmd.Parameters.Add("p_NumeroPlaca", OracleDbType.Varchar2).Value = BmDb.DbValue(request.NumeroPlaca);
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);

        return await BmDb.ExecuteListAsync(cmd, MapPlacaCuarentena);
    }

    private static BmPlacaCuarentenaResponse MapPlacaCuarentena(IDataReader reader)
    {
        return new BmPlacaCuarentenaResponse(
            reader.SafeGetInt32("CODIGO_PLACA_CUARENTENA"),
            reader.SafeGetString("NUMERO_PLACA"),
            reader.SafeGetString("ARTICULO"),
            reader.SafeGetString("SEARCH_TEXT")
        );
    }
}
