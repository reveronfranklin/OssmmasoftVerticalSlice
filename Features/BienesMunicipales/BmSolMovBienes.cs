using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.BienesMunicipales;

[ApiController]
[Route("api/BmSolMovBienes")]
public class BmSolMovBienesController(ConnectionDB connectionDB, IConfiguration config) : ControllerBase
{
    [HttpPost("GetAll")]
    public async Task<IActionResult> GetAll(BmSolicitudMovimientoFilterRequest request)
    {
        if (!BmDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return Ok(BmDb.InvalidList<BmSolicitudMovimientoResponse>(error));
        }

        using var cn = connectionDB.GetBmConnection();
        var openError = await BmDb.TryOpenAsync(cn, "BM");
        if (openError is not null) return Ok(BmDb.InvalidList<BmSolicitudMovimientoResponse>(openError));

        using var cmd = BmDb.StoredProcedure("BM.SP_BM_SOL_MOV_GET", cn);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_Aprobado", OracleDbType.Int32).Value = request.Aprobado;
        cmd.Parameters.Add("p_SearchText", OracleDbType.Varchar2).Value = BmDb.DbValue(request.SearchText);
        cmd.Parameters.Add("p_TipoMovimiento", OracleDbType.Varchar2).Value = BmDb.DbValue(request.TipoMovimiento);
        cmd.Parameters.Add("p_FechaDesde", OracleDbType.Date).Value = BmDb.DbValue(request.FechaDesde);
        cmd.Parameters.Add("p_FechaHasta", OracleDbType.Date).Value = BmDb.DbValue(request.FechaHasta);
        cmd.Parameters.Add("p_CodigoDirBien", OracleDbType.Int32).Value = request.CodigoDirBien;
        cmd.Parameters.Add("p_Page", OracleDbType.Int32).Value = request.Page <= 0 ? 1 : request.Page;
        cmd.Parameters.Add("p_PageSize", OracleDbType.Int32).Value = request.PageSize <= 0 ? 50 : request.PageSize;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);

        return Ok(await BmDb.ExecuteListAsync(cmd, MapSolicitud, request.Page));
    }

    [HttpPost("Create")]
    public async Task<IActionResult> Create(BmSolicitudMovimientoCreateRequest request)
    {
        if (!BmDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return Ok(BmDb.InvalidList<BmSolicitudMovimientoResponse>(error));
        }

        using var cn = connectionDB.GetBmConnection();
        var openError = await BmDb.TryOpenAsync(cn, "BM");
        if (openError is not null) return Ok(BmDb.InvalidList<BmSolicitudMovimientoResponse>(openError));

        using var cmd = BmDb.StoredProcedure("BM.SP_BM_SOL_MOV_INS", cn);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_CodigoBien", OracleDbType.Int32).Value = request.CodigoBien;
        cmd.Parameters.Add("p_TipoMovimiento", OracleDbType.Varchar2).Value = BmDb.DbValue(request.TipoMovimiento);
        cmd.Parameters.Add("p_FechaMovimiento", OracleDbType.Date).Value = BmDb.DbValue(request.FechaMovimiento);
        cmd.Parameters.Add("p_CodigoDirBien", OracleDbType.Int32).Value = request.CodigoDirBien;
        cmd.Parameters.Add("p_ConceptoMovId", OracleDbType.Int32).Value = request.ConceptoMovId == 0 ? DBNull.Value : request.ConceptoMovId;
        cmd.Parameters.Add("p_NumeroSolicitud", OracleDbType.Varchar2).Value = BmDb.DbValue(request.NumeroSolicitud);
        cmd.Parameters.Add("p_UsuarioSolicita", OracleDbType.Int32).Value = request.UsuarioSolicita == 0 ? DBNull.Value : request.UsuarioSolicita;
        cmd.Parameters.Add("p_FechaIncidencia", OracleDbType.Date).Value = BmDb.DbValue(request.FechaIncidencia);
        cmd.Parameters.Add("p_NotaIncidencia", OracleDbType.Varchar2).Value = BmDb.DbValue(request.NotaIncidencia);
        cmd.Parameters.Add("p_Extra1", OracleDbType.Varchar2).Value = BmDb.DbValue(request.Extra1);
        cmd.Parameters.Add("p_Extra2", OracleDbType.Varchar2).Value = BmDb.DbValue(request.Extra2);
        cmd.Parameters.Add("p_Extra3", OracleDbType.Varchar2).Value = BmDb.DbValue(request.Extra3);
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);

        return Ok(await BmDb.ExecuteListAsync(cmd, MapSolicitud));
    }

    [HttpPost("Aprobar")]
    public async Task<IActionResult> Aprobar(BmSolicitudMovimientoAprobarRequest request)
    {
        if (!BmDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return Ok(BmDb.InvalidList<BmSolicitudMovimientoResponse>(error));
        }

        using var cn = connectionDB.GetBmConnection();
        var openError = await BmDb.TryOpenAsync(cn, "BM");
        if (openError is not null) return Ok(BmDb.InvalidList<BmSolicitudMovimientoResponse>(openError));

        using var cmd = BmDb.StoredProcedure("BM.SP_BM_SOL_MOV_APR", cn);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_CodigoSolMovBien", OracleDbType.Int32).Value = request.CodigoSolMovBien;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);

        return Ok(await BmDb.ExecuteListAsync(cmd, MapSolicitud));
    }

    private static BmSolicitudMovimientoResponse MapSolicitud(IDataReader reader)
    {
        var fechaMovimiento = BmDb.GetDate(reader, "FECHA_MOVIMIENTO");
        var fechaSolicita = BmDb.GetDate(reader, "FECHA_SOLICITA");
        return new BmSolicitudMovimientoResponse(
            reader.SafeGetInt32("CODIGO_SOL_MOV_BIEN"),
            reader.SafeGetInt32("CODIGO_BIEN"),
            reader.SafeGetString("NUMERO_PLACA"),
            reader.SafeGetString("ARTICULO"),
            reader.SafeGetString("TIPO_MOVIMIENTO"),
            reader.SafeGetString("TIPO_MOVIMIENTO_DESC"),
            fechaMovimiento,
            BmDb.ToDateString(fechaMovimiento),
            reader.SafeGetInt32("CODIGO_DIR_BIEN"),
            reader.SafeGetInt32("CODIGO_ICP"),
            reader.SafeGetString("UNIDAD_EJECUTORA"),
            reader.SafeGetInt32("CONCEPTO_MOV_ID"),
            reader.SafeGetString("CONCEPTO_MOVIMIENTO"),
            reader.SafeGetString("NUMERO_SOLICITUD"),
            reader.SafeGetBoolean("APROBADO"),
            reader.SafeGetInt32("USUARIO_SOLICITA"),
            fechaSolicita,
            BmDb.ToDateString(fechaSolicita),
            BmDb.GetDate(reader, "FECHA_INCIDENCIA"),
            reader.SafeGetString("NOTA_INCIDENCIA")
        );
    }
}
