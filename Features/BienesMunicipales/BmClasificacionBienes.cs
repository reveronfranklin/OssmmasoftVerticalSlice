using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.BienesMunicipales;

[ApiController]
[Route("api/BmClasificacionBienes")]
public class BmClasificacionBienesController(ConnectionDB connectionDB, IConfiguration config) : ControllerBase
{
    [HttpPost("GetAll")]
    public async Task<IActionResult> GetAll(BmCatalogFilterRequest request)
    {
        if (!BmDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return Ok(BmDb.InvalidList<BmClasificacionResponse>(error));
        }

        using var cn = connectionDB.GetBmConnection();
        var openError = await BmDb.TryOpenAsync(cn, "BM");
        if (openError is not null) return Ok(BmDb.InvalidList<BmClasificacionResponse>(openError));

        using var cmd = BmDb.StoredProcedure("BM.SP_BM_CLASIF_GET_ALL", cn);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_SearchText", OracleDbType.Varchar2).Value = BmDb.DbValue(request.SearchText);
        cmd.Parameters.Add("p_Page", OracleDbType.Int32).Value = request.Page <= 0 ? 1 : request.Page;
        cmd.Parameters.Add("p_PageSize", OracleDbType.Int32).Value = request.PageSize <= 0 ? 50 : request.PageSize;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);

        return Ok(await BmDb.ExecuteListAsync(cmd, MapClasificacion, request.Page));
    }

    [HttpPost("Create")]
    public async Task<IActionResult> Create(BmClasificacionUpsertRequest request)
    {
        return Ok(await MutateAsync("BM.SP_BM_CLASIF_INS", request));
    }

    [HttpPost("Update")]
    public async Task<IActionResult> Update(BmClasificacionUpsertRequest request)
    {
        return Ok(await MutateAsync("BM.SP_BM_CLASIF_UPD", request));
    }

    private async Task<OssmmasoftVerticalSlice.Helpers.ResultDto<List<BmClasificacionResponse>>> MutateAsync(string procedureName, BmClasificacionUpsertRequest request)
    {
        if (!BmDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return BmDb.InvalidList<BmClasificacionResponse>(error);
        }

        using var cn = connectionDB.GetBmConnection();
        var openError = await BmDb.TryOpenAsync(cn, "BM");
        if (openError is not null) return BmDb.InvalidList<BmClasificacionResponse>(openError);

        using var cmd = BmDb.StoredProcedure(procedureName, cn);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_CodigoClasifBien", OracleDbType.Int32).Value = request.CodigoClasificacionBien;
        cmd.Parameters.Add("p_CodigoGrupo", OracleDbType.Varchar2).Value = BmDb.DbValue(request.CodigoGrupo);
        cmd.Parameters.Add("p_CodigoNivel1", OracleDbType.Varchar2).Value = BmDb.DbValue(request.CodigoNivel1);
        cmd.Parameters.Add("p_CodigoNivel2", OracleDbType.Varchar2).Value = BmDb.DbValue(request.CodigoNivel2);
        cmd.Parameters.Add("p_CodigoNivel3", OracleDbType.Varchar2).Value = BmDb.DbValue(request.CodigoNivel3);
        cmd.Parameters.Add("p_Denominacion", OracleDbType.Varchar2).Value = BmDb.DbValue(request.Denominacion);
        cmd.Parameters.Add("p_Descripcion", OracleDbType.Varchar2).Value = BmDb.DbValue(request.Descripcion);
        cmd.Parameters.Add("p_FechaIni", OracleDbType.Date).Value = BmDb.DbValue(request.FechaIni);
        cmd.Parameters.Add("p_FechaFin", OracleDbType.Date).Value = BmDb.DbValue(request.FechaFin);
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);

        return await BmDb.ExecuteListAsync(cmd, MapClasificacion);
    }

    internal static BmClasificacionResponse MapClasificacion(IDataReader reader)
    {
        return new BmClasificacionResponse(
            reader.SafeGetInt32("CODIGO_CLASIFICACION_BIEN"),
            reader.SafeGetString("CODIGO_GRUPO"),
            reader.SafeGetString("CODIGO_NIVEL1"),
            reader.SafeGetString("CODIGO_NIVEL2"),
            reader.SafeGetString("CODIGO_NIVEL3"),
            reader.SafeGetString("DENOMINACION"),
            reader.SafeGetString("DESCRIPCION"),
            BmDb.GetDate(reader, "FECHA_INI"),
            BmDb.GetDate(reader, "FECHA_FIN")
        );
    }
}
