using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.BienesMunicipales;

[ApiController]
[Route("api/BmUbicaciones")]
public class BmUbicacionesController(ConnectionDB connectionDB, IConfiguration config) : ControllerBase
{
    [HttpPost("GetAll")]
    public async Task<IActionResult> GetAll(BmUbicacionFilterRequest request)
    {
        if (!BmDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return Ok(BmDb.InvalidList<BmUbicacionResponse>(error));
        }

        using var cn = connectionDB.GetBmConnection();
        var openError = await BmDb.TryOpenAsync(cn, "BM");
        if (openError is not null) return Ok(BmDb.InvalidList<BmUbicacionResponse>(openError));

        using var cmd = BmDb.StoredProcedure("BM.SP_BM_UBI_GET_ALL", cn);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_SearchText", OracleDbType.Varchar2).Value = BmDb.DbValue(request.SearchText);
        cmd.Parameters.Add("p_Page", OracleDbType.Int32).Value = request.Page <= 0 ? 1 : request.Page;
        cmd.Parameters.Add("p_PageSize", OracleDbType.Int32).Value = request.PageSize <= 0 ? 50 : request.PageSize;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);

        return Ok(await BmDb.ExecuteListAsync(cmd, MapUbicacion, request.Page));
    }

    [HttpPost("GetByIcp")]
    public async Task<IActionResult> GetByIcp(BmUbicacionByIcpRequest request)
    {
        if (!BmDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return Ok(BmDb.InvalidList<BmUbicacionResponse>(error));
        }

        using var cn = connectionDB.GetBmConnection();
        var openError = await BmDb.TryOpenAsync(cn, "BM");
        if (openError is not null) return Ok(BmDb.InvalidList<BmUbicacionResponse>(openError));

        using var cmd = BmDb.StoredProcedure("BM.SP_BM_UBI_GET_ICP", cn);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_CodigoIcp", OracleDbType.Int32).Value = request.CodigoIcp;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);

        return Ok(await BmDb.ExecuteListAsync(cmd, MapUbicacion));
    }

    [HttpGet("GetIcp")]
    public async Task<IActionResult> GetIcp()
    {
        if (!BmDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return Ok(BmDb.InvalidList<BmIcpResponse>(error));
        }

        using var cn = connectionDB.GetBmConnection();
        var openError = await BmDb.TryOpenAsync(cn, "BM");
        if (openError is not null) return Ok(BmDb.InvalidList<BmIcpResponse>(openError));

        using var cmd = BmDb.StoredProcedure("BM.SP_BM_UBI_GET_ICP_LST", cn);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);

        return Ok(await BmDb.ExecuteListAsync(cmd, reader => new BmIcpResponse(
            reader.SafeGetInt32("CODIGO_ICP"),
            reader.SafeGetString("UNIDAD_TRABAJO")
        )));
    }

    [HttpPost("Create")]
    public async Task<IActionResult> Create(BmUbicacionUpsertRequest request)
    {
        return Ok(await MutateAsync("BM.SP_BM_UBI_INS", request));
    }

    [HttpPost("Update")]
    public async Task<IActionResult> Update(BmUbicacionUpsertRequest request)
    {
        return Ok(await MutateAsync("BM.SP_BM_UBI_UPD", request));
    }

    private async Task<OssmmasoftVerticalSlice.Helpers.ResultDto<List<BmUbicacionResponse>>> MutateAsync(string procedureName, BmUbicacionUpsertRequest request)
    {
        if (!BmDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return BmDb.InvalidList<BmUbicacionResponse>(error);
        }

        using var cn = connectionDB.GetBmConnection();
        var openError = await BmDb.TryOpenAsync(cn, "BM");
        if (openError is not null) return BmDb.InvalidList<BmUbicacionResponse>(openError);

        using var cmd = BmDb.StoredProcedure(procedureName, cn);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_CodigoDirBien", OracleDbType.Int32).Value = request.CodigoDirBien;
        cmd.Parameters.Add("p_CodigoIcp", OracleDbType.Int32).Value = request.CodigoIcp;
        cmd.Parameters.Add("p_PaisId", OracleDbType.Int32).Value = request.PaisId == 0 ? DBNull.Value : request.PaisId;
        cmd.Parameters.Add("p_EstadoId", OracleDbType.Int32).Value = request.EstadoId == 0 ? DBNull.Value : request.EstadoId;
        cmd.Parameters.Add("p_MunicipioId", OracleDbType.Int32).Value = request.MunicipioId == 0 ? DBNull.Value : request.MunicipioId;
        cmd.Parameters.Add("p_CiudadId", OracleDbType.Int32).Value = request.CiudadId == 0 ? DBNull.Value : request.CiudadId;
        cmd.Parameters.Add("p_ParroquiaId", OracleDbType.Int32).Value = request.ParroquiaId == 0 ? DBNull.Value : request.ParroquiaId;
        cmd.Parameters.Add("p_SectorId", OracleDbType.Int32).Value = request.SectorId == 0 ? DBNull.Value : request.SectorId;
        cmd.Parameters.Add("p_UrbanizacionId", OracleDbType.Int32).Value = request.UrbanizacionId == 0 ? DBNull.Value : request.UrbanizacionId;
        cmd.Parameters.Add("p_ManzanaId", OracleDbType.Int32).Value = request.ManzanaId == 0 ? DBNull.Value : request.ManzanaId;
        cmd.Parameters.Add("p_ParcelaId", OracleDbType.Int32).Value = request.ParcelaId == 0 ? DBNull.Value : request.ParcelaId;
        cmd.Parameters.Add("p_VialidadId", OracleDbType.Int32).Value = request.VialidadId == 0 ? DBNull.Value : request.VialidadId;
        cmd.Parameters.Add("p_Vialidad", OracleDbType.Varchar2).Value = BmDb.DbValue(request.Vialidad);
        cmd.Parameters.Add("p_TipoViviendaId", OracleDbType.Int32).Value = request.TipoViviendaId == 0 ? DBNull.Value : request.TipoViviendaId;
        cmd.Parameters.Add("p_Vivienda", OracleDbType.Varchar2).Value = BmDb.DbValue(request.Vivienda);
        cmd.Parameters.Add("p_TipoNivelId", OracleDbType.Int32).Value = request.TipoNivelId == 0 ? DBNull.Value : request.TipoNivelId;
        cmd.Parameters.Add("p_Nivel", OracleDbType.Varchar2).Value = BmDb.DbValue(request.Nivel);
        cmd.Parameters.Add("p_TipoUnidadId", OracleDbType.Int32).Value = request.TipoUnidadId == 0 ? DBNull.Value : request.TipoUnidadId;
        cmd.Parameters.Add("p_NumeroUnidad", OracleDbType.Varchar2).Value = BmDb.DbValue(request.NumeroUnidad);
        cmd.Parameters.Add("p_ComplementoDir", OracleDbType.Varchar2).Value = BmDb.DbValue(request.ComplementoDir);
        cmd.Parameters.Add("p_TenenciaId", OracleDbType.Int32).Value = request.TenenciaId == 0 ? DBNull.Value : request.TenenciaId;
        cmd.Parameters.Add("p_CodigoPostal", OracleDbType.Int32).Value = request.CodigoPostal == 0 ? DBNull.Value : request.CodigoPostal;
        cmd.Parameters.Add("p_FechaIni", OracleDbType.Date).Value = BmDb.DbValue(request.FechaIni);
        cmd.Parameters.Add("p_FechaFin", OracleDbType.Date).Value = BmDb.DbValue(request.FechaFin);
        cmd.Parameters.Add("p_UnidadTrabajoId", OracleDbType.Int32).Value = request.UnidadTrabajoId == 0 ? DBNull.Value : request.UnidadTrabajoId;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);

        return await BmDb.ExecuteListAsync(cmd, MapUbicacion);
    }

    internal static BmUbicacionResponse MapUbicacion(IDataReader reader)
    {
        return new BmUbicacionResponse(
            reader.SafeGetInt32("CODIGO_DIR_BIEN"),
            reader.SafeGetInt32("CODIGO_ICP"),
            reader.SafeGetString("UNIDAD_EJECUTORA"),
            reader.SafeGetInt32("PAIS_ID"),
            reader.SafeGetInt32("ESTADO_ID"),
            reader.SafeGetInt32("MUNICIPIO_ID"),
            reader.SafeGetInt32("CIUDAD_ID"),
            reader.SafeGetInt32("PARROQUIA_ID"),
            reader.SafeGetInt32("SECTOR_ID"),
            reader.SafeGetInt32("URBANIZACION_ID"),
            reader.SafeGetInt32("MANZANA_ID"),
            reader.SafeGetInt32("PARCELA_ID"),
            reader.SafeGetInt32("VIALIDAD_ID"),
            reader.SafeGetString("VIALIDAD"),
            reader.SafeGetInt32("TIPO_VIVIENDA_ID"),
            reader.SafeGetString("VIVIENDA"),
            reader.SafeGetInt32("TIPO_NIVEL_ID"),
            reader.SafeGetString("NIVEL"),
            reader.SafeGetInt32("TIPO_UNIDAD_ID"),
            reader.SafeGetString("NUMERO_UNIDAD"),
            reader.SafeGetString("COMPLEMENTO_DIR"),
            reader.SafeGetInt32("TENENCIA_ID"),
            reader.SafeGetInt32("CODIGO_POSTAL"),
            BmDb.GetDate(reader, "FECHA_INI"),
            BmDb.GetDate(reader, "FECHA_FIN"),
            reader.SafeGetInt32("UNIDAD_TRABAJO_ID"),
            reader.SafeGetString("DIRECCION"),
            reader.SafeGetString("SEARCH_TEXT")
        );
    }
}
