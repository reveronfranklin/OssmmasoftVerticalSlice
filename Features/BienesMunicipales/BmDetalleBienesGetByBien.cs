using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.BienesMunicipales;

[ApiController]
[Route("api/BmDetalleBienes")]
public class BmDetalleBienesController(ConnectionDB connectionDB, IConfiguration config) : ControllerBase
{
    [HttpPost("GetByBien")]
    public async Task<IActionResult> GetByBien(BmDetalleBienRequest request)
    {
        if (!BmDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return Ok(BmDb.InvalidList<BmDetalleBienResponse>(error));
        }

        using var cn = connectionDB.GetBmConnection();
        var openError = await BmDb.TryOpenAsync(cn, "BM");
        if (openError is not null) return Ok(BmDb.InvalidList<BmDetalleBienResponse>(openError));

        using var cmd = BmDb.StoredProcedure("BM.SP_BM_DET_BIEN_GET", cn);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_CodigoBien", OracleDbType.Int32).Value = request.CodigoBien;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);

        return Ok(await BmDb.ExecuteListAsync(cmd, reader => new BmDetalleBienResponse(
            reader.SafeGetInt32("CODIGO_DETALLE_BIEN"),
            reader.SafeGetInt32("CODIGO_BIEN"),
            reader.SafeGetInt32("TIPO_ESPECIFICACION_ID"),
            reader.SafeGetString("TIPO_ESPECIFICACION"),
            reader.SafeGetInt32("ESPECIFICACION_ID"),
            reader.SafeGetString("ESPECIFICACION_ID_DESC"),
            reader.SafeGetString("ESPECIFICACION")
        )));
    }

    [HttpPost("Create")]
    public async Task<IActionResult> Create(BmDetalleBienUpsertRequest request)
    {
        return Ok(await MutateAsync("BM.SP_BM_DET_BIEN_INS", request));
    }

    [HttpPost("Update")]
    public async Task<IActionResult> Update(BmDetalleBienUpsertRequest request)
    {
        return Ok(await MutateAsync("BM.SP_BM_DET_BIEN_UPD", request));
    }

    private async Task<OssmmasoftVerticalSlice.Helpers.ResultDto<List<BmDetalleBienResponse>>> MutateAsync(string procedureName, BmDetalleBienUpsertRequest request)
    {
        if (!BmDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return BmDb.InvalidList<BmDetalleBienResponse>(error);
        }

        using var cn = connectionDB.GetBmConnection();
        var openError = await BmDb.TryOpenAsync(cn, "BM");
        if (openError is not null) return BmDb.InvalidList<BmDetalleBienResponse>(openError);

        using var cmd = BmDb.StoredProcedure(procedureName, cn);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_CodigoDetalleBien", OracleDbType.Int32).Value = request.CodigoDetalleBien;
        cmd.Parameters.Add("p_CodigoBien", OracleDbType.Int32).Value = request.CodigoBien;
        cmd.Parameters.Add("p_TipoEspecificacionId", OracleDbType.Int32).Value = request.TipoEspecificacionId;
        cmd.Parameters.Add("p_EspecificacionId", OracleDbType.Int32).Value = request.EspecificacionId == 0 ? DBNull.Value : request.EspecificacionId;
        cmd.Parameters.Add("p_Especificacion", OracleDbType.Varchar2).Value = BmDb.DbValue(request.Especificacion);
        cmd.Parameters.Add("p_UsuarioId", OracleDbType.Int32).Value = request.UsuarioId == 0 ? DBNull.Value : request.UsuarioId;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);

        return await BmDb.ExecuteListAsync(cmd, reader => new BmDetalleBienResponse(
            reader.SafeGetInt32("CODIGO_DETALLE_BIEN"),
            reader.SafeGetInt32("CODIGO_BIEN"),
            reader.SafeGetInt32("TIPO_ESPECIFICACION_ID"),
            reader.SafeGetString("TIPO_ESPECIFICACION"),
            reader.SafeGetInt32("ESPECIFICACION_ID"),
            reader.SafeGetString("ESPECIFICACION_ID_DESC"),
            reader.SafeGetString("ESPECIFICACION")
        ));
    }
}
