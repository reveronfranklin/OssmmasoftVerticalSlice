using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.BienesMunicipales;

[ApiController]
[Route("api/BmMovBienes")]
public class BmMovBienesController(ConnectionDB connectionDB, IConfiguration config) : ControllerBase
{
    [HttpPost("GetByBien")]
    public async Task<IActionResult> GetByBien(BmMovimientoByBienRequest request)
    {
        if (!BmDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return Ok(BmDb.InvalidList<BmMovimientoResponse>(error));
        }

        using var cn = connectionDB.GetBmConnection();
        var openError = await BmDb.TryOpenAsync(cn, "BM");
        if (openError is not null) return Ok(BmDb.InvalidList<BmMovimientoResponse>(openError));

        using var cmd = BmDb.StoredProcedure("BM.SP_BM_MOV_GET_BIEN", cn);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_CodigoBien", OracleDbType.Int32).Value = request.CodigoBien;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);

        return Ok(await BmDb.ExecuteListAsync(cmd, MapMovimiento));
    }

    [HttpPost("Create")]
    public async Task<IActionResult> Create(BmMovimientoCreateRequest request)
    {
        if (!BmDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return Ok(BmDb.InvalidList<BmMovimientoResponse>(error));
        }

        using var cn = connectionDB.GetBmConnection();
        var openError = await BmDb.TryOpenAsync(cn, "BM");
        if (openError is not null) return Ok(BmDb.InvalidList<BmMovimientoResponse>(openError));

        using var cmd = BmDb.StoredProcedure("BM.SP_BM_MOV_INS", cn);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_CodigoBien", OracleDbType.Int32).Value = request.CodigoBien;
        cmd.Parameters.Add("p_TipoMovimiento", OracleDbType.Varchar2).Value = BmDb.DbValue(request.TipoMovimiento);
        cmd.Parameters.Add("p_FechaMovimiento", OracleDbType.Date).Value = BmDb.DbValue(request.FechaMovimiento);
        cmd.Parameters.Add("p_CodigoDirBien", OracleDbType.Int32).Value = request.CodigoDirBien;
        cmd.Parameters.Add("p_ConceptoMovId", OracleDbType.Int32).Value = request.ConceptoMovId == 0 ? DBNull.Value : request.ConceptoMovId;
        cmd.Parameters.Add("p_CodigoSolMovBien", OracleDbType.Int32).Value = request.CodigoSolMovBien == 0 ? DBNull.Value : request.CodigoSolMovBien;
        cmd.Parameters.Add("p_Extra1", OracleDbType.Varchar2).Value = BmDb.DbValue(request.Extra1);
        cmd.Parameters.Add("p_Extra2", OracleDbType.Varchar2).Value = BmDb.DbValue(request.Extra2);
        cmd.Parameters.Add("p_Extra3", OracleDbType.Varchar2).Value = BmDb.DbValue(request.Extra3);
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);

        return Ok(await BmDb.ExecuteListAsync(cmd, MapMovimiento));
    }

    internal static BmMovimientoResponse MapMovimiento(IDataReader reader)
    {
        var fecha = BmDb.GetDate(reader, "FECHA_MOVIMIENTO");
        return new BmMovimientoResponse(
            reader.SafeGetInt32("CODIGO_MOV_BIEN"),
            reader.SafeGetInt32("CODIGO_BIEN"),
            reader.SafeGetString("NUMERO_PLACA"),
            reader.SafeGetString("ARTICULO"),
            reader.SafeGetString("TIPO_MOVIMIENTO"),
            reader.SafeGetString("TIPO_MOVIMIENTO_DESC"),
            fecha,
            BmDb.ToDateString(fecha),
            reader.SafeGetInt32("CODIGO_DIR_BIEN"),
            reader.SafeGetInt32("CODIGO_ICP"),
            reader.SafeGetString("UNIDAD_EJECUTORA"),
            reader.SafeGetInt32("CONCEPTO_MOV_ID"),
            reader.SafeGetString("CONCEPTO_MOVIMIENTO"),
            reader.SafeGetInt32("CODIGO_SOL_MOV_BIEN"),
            reader.SafeGetBoolean("ES_MOVIMIENTO_FINAL"),
            reader.SafeGetString("EXTRA1"),
            reader.SafeGetString("EXTRA2"),
            reader.SafeGetString("EXTRA3")
        );
    }
}
