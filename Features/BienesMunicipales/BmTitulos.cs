using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.BienesMunicipales;

[ApiController]
[Route("api/BmTitulos")]
public class BmTitulosController(ConnectionDB connectionDB, IConfiguration config) : ControllerBase
{
    [HttpPost("GetAll")]
    public async Task<IActionResult> GetAll(BmCatalogFilterRequest request)
    {
        if (!BmDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return Ok(BmDb.InvalidList<BmTituloResponse>(error));
        }

        using var cn = connectionDB.GetBmConnection();
        var openError = await BmDb.TryOpenAsync(cn, "BM");
        if (openError is not null) return Ok(BmDb.InvalidList<BmTituloResponse>(openError));

        using var cmd = BmDb.StoredProcedure("BM.SP_BM_TIT_GET_ALL", cn);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_SearchText", OracleDbType.Varchar2).Value = BmDb.DbValue(request.SearchText);
        cmd.Parameters.Add("p_Page", OracleDbType.Int32).Value = request.Page <= 0 ? 1 : request.Page;
        cmd.Parameters.Add("p_PageSize", OracleDbType.Int32).Value = request.PageSize <= 0 ? 50 : request.PageSize;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);

        return Ok(await BmDb.ExecuteListAsync(cmd, MapTitulo, request.Page));
    }

    [HttpPost("Create")]
    public async Task<IActionResult> Create(BmTituloUpsertRequest request)
    {
        return Ok(await MutateAsync("BM.SP_BM_TIT_INS", request));
    }

    [HttpPost("Update")]
    public async Task<IActionResult> Update(BmTituloUpsertRequest request)
    {
        return Ok(await MutateAsync("BM.SP_BM_TIT_UPD", request));
    }

    private async Task<OssmmasoftVerticalSlice.Helpers.ResultDto<List<BmTituloResponse>>> MutateAsync(string procedureName, BmTituloUpsertRequest request)
    {
        if (!BmDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return BmDb.InvalidList<BmTituloResponse>(error);
        }

        using var cn = connectionDB.GetBmConnection();
        var openError = await BmDb.TryOpenAsync(cn, "BM");
        if (openError is not null) return BmDb.InvalidList<BmTituloResponse>(openError);

        using var cmd = BmDb.StoredProcedure(procedureName, cn);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_TituloId", OracleDbType.Int32).Value = request.TituloId;
        cmd.Parameters.Add("p_TituloFkId", OracleDbType.Int32).Value = request.TituloFkId == 0 ? DBNull.Value : request.TituloFkId;
        cmd.Parameters.Add("p_Titulo", OracleDbType.Varchar2).Value = BmDb.DbValue(request.Titulo);
        cmd.Parameters.Add("p_Codigo", OracleDbType.Varchar2).Value = BmDb.DbValue(request.Codigo);
        cmd.Parameters.Add("p_Extra1", OracleDbType.Varchar2).Value = BmDb.DbValue(request.Extra1);
        cmd.Parameters.Add("p_Extra2", OracleDbType.Varchar2).Value = BmDb.DbValue(request.Extra2);
        cmd.Parameters.Add("p_Extra3", OracleDbType.Varchar2).Value = BmDb.DbValue(request.Extra3);
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);

        return await BmDb.ExecuteListAsync(cmd, MapTitulo);
    }

    internal static BmTituloResponse MapTitulo(IDataReader reader)
    {
        return new BmTituloResponse(
            reader.SafeGetInt32("TITULO_ID"),
            reader.SafeGetInt32("TITULO_FK_ID"),
            reader.SafeGetString("TITULO"),
            reader.SafeGetString("CODIGO"),
            reader.SafeGetString("EXTRA1"),
            reader.SafeGetString("EXTRA2"),
            reader.SafeGetString("EXTRA3")
        );
    }
}
