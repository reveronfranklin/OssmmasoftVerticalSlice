using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.BienesMunicipales;

[ApiController]
[Route("api/BmDetalleArticulos")]
public class BmDetalleArticulosController(ConnectionDB connectionDB, IConfiguration config) : ControllerBase
{
    [HttpPost("GetByArticulo")]
    public async Task<IActionResult> GetByArticulo(BmDetalleArticuloFilterRequest request)
    {
        if (!BmDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return Ok(BmDb.InvalidList<BmDetalleArticuloResponse>(error));
        }

        using var cn = connectionDB.GetBmConnection();
        var openError = await BmDb.TryOpenAsync(cn, "BM");
        if (openError is not null) return Ok(BmDb.InvalidList<BmDetalleArticuloResponse>(openError));

        using var cmd = BmDb.StoredProcedure("BM.SP_BM_DET_ART_GET", cn);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_CodigoArticulo", OracleDbType.Int32).Value = request.CodigoArticulo;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);

        return Ok(await BmDb.ExecuteListAsync(cmd, MapDetalle));
    }

    [HttpPost("Create")]
    public async Task<IActionResult> Create(BmDetalleArticuloUpsertRequest request)
    {
        return Ok(await MutateAsync("BM.SP_BM_DET_ART_INS", request));
    }

    [HttpPost("Update")]
    public async Task<IActionResult> Update(BmDetalleArticuloUpsertRequest request)
    {
        return Ok(await MutateAsync("BM.SP_BM_DET_ART_UPD", request));
    }

    private async Task<OssmmasoftVerticalSlice.Helpers.ResultDto<List<BmDetalleArticuloResponse>>> MutateAsync(string procedureName, BmDetalleArticuloUpsertRequest request)
    {
        if (!BmDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return BmDb.InvalidList<BmDetalleArticuloResponse>(error);
        }

        using var cn = connectionDB.GetBmConnection();
        var openError = await BmDb.TryOpenAsync(cn, "BM");
        if (openError is not null) return BmDb.InvalidList<BmDetalleArticuloResponse>(openError);

        using var cmd = BmDb.StoredProcedure(procedureName, cn);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_CodigoDetArticulo", OracleDbType.Int32).Value = request.CodigoDetalleArticulo;
        cmd.Parameters.Add("p_CodigoArticulo", OracleDbType.Int32).Value = request.CodigoArticulo;
        cmd.Parameters.Add("p_TipoEspecificacionId", OracleDbType.Int32).Value = request.TipoEspecificacionId;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);

        return await BmDb.ExecuteListAsync(cmd, MapDetalle);
    }

    private static BmDetalleArticuloResponse MapDetalle(IDataReader reader)
    {
        return new BmDetalleArticuloResponse(
            reader.SafeGetInt32("CODIGO_DETALLE_ARTICULO"),
            reader.SafeGetInt32("CODIGO_ARTICULO"),
            reader.SafeGetInt32("TIPO_ESPECIFICACION_ID"),
            reader.SafeGetString("TIPO_ESPECIFICACION")
        );
    }
}
