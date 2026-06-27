using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.BienesMunicipales;

[ApiController]
[Route("api/BmArticulos")]
public class BmArticulosController(ConnectionDB connectionDB, IConfiguration config) : ControllerBase
{
    [HttpPost("GetAll")]
    public async Task<IActionResult> GetAll(BmCatalogFilterRequest request)
    {
        if (!BmDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return Ok(BmDb.InvalidList<BmArticuloResponse>(error));
        }

        using var cn = connectionDB.GetBmConnection();
        var openError = await BmDb.TryOpenAsync(cn, "BM");
        if (openError is not null) return Ok(BmDb.InvalidList<BmArticuloResponse>(openError));

        using var cmd = BmDb.StoredProcedure("BM.SP_BM_ART_GET_ALL", cn);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_SearchText", OracleDbType.Varchar2).Value = BmDb.DbValue(request.SearchText);
        cmd.Parameters.Add("p_Page", OracleDbType.Int32).Value = request.Page <= 0 ? 1 : request.Page;
        cmd.Parameters.Add("p_PageSize", OracleDbType.Int32).Value = request.PageSize <= 0 ? 50 : request.PageSize;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);

        return Ok(await BmDb.ExecuteListAsync(cmd, MapArticulo, request.Page));
    }

    [HttpPost("Create")]
    public async Task<IActionResult> Create(BmArticuloUpsertRequest request)
    {
        return Ok(await MutateAsync("BM.SP_BM_ART_INS", request));
    }

    [HttpPost("Update")]
    public async Task<IActionResult> Update(BmArticuloUpsertRequest request)
    {
        return Ok(await MutateAsync("BM.SP_BM_ART_UPD", request));
    }

    private async Task<OssmmasoftVerticalSlice.Helpers.ResultDto<List<BmArticuloResponse>>> MutateAsync(string procedureName, BmArticuloUpsertRequest request)
    {
        if (!BmDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return BmDb.InvalidList<BmArticuloResponse>(error);
        }

        using var cn = connectionDB.GetBmConnection();
        var openError = await BmDb.TryOpenAsync(cn, "BM");
        if (openError is not null) return BmDb.InvalidList<BmArticuloResponse>(openError);

        using var cmd = BmDb.StoredProcedure(procedureName, cn);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_CodigoArticulo", OracleDbType.Int32).Value = request.CodigoArticulo;
        cmd.Parameters.Add("p_CodigoClasifBien", OracleDbType.Int32).Value = request.CodigoClasificacionBien;
        cmd.Parameters.Add("p_Codigo", OracleDbType.Varchar2).Value = BmDb.DbValue(request.Codigo);
        cmd.Parameters.Add("p_Denominacion", OracleDbType.Varchar2).Value = BmDb.DbValue(request.Denominacion);
        cmd.Parameters.Add("p_Descripcion", OracleDbType.Varchar2).Value = BmDb.DbValue(request.Descripcion);
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);

        return await BmDb.ExecuteListAsync(cmd, MapArticulo);
    }

    internal static BmArticuloResponse MapArticulo(IDataReader reader)
    {
        return new BmArticuloResponse(
            reader.SafeGetInt32("CODIGO_ARTICULO"),
            reader.SafeGetInt32("CODIGO_CLASIFICACION_BIEN"),
            reader.SafeGetString("CODIGO"),
            reader.SafeGetString("DENOMINACION"),
            reader.SafeGetString("DESCRIPCION"),
            reader.SafeGetString("CODIGO_GRUPO"),
            reader.SafeGetString("CODIGO_NIVEL1"),
            reader.SafeGetString("CODIGO_NIVEL2"),
            reader.SafeGetString("CODIGO_NIVEL3"),
            reader.SafeGetString("CLASIFICACION")
        );
    }
}
