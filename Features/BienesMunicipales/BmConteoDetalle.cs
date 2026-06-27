using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.BienesMunicipales;

[ApiController]
[Route("api/BmConteoDetalle")]
public class BmConteoDetalleController(ConnectionDB connectionDB, IConfiguration config) : ControllerBase
{
    [HttpPost("GetAllByConteo")]
    public async Task<IActionResult> GetAllByConteo(BmConteoDetalleFilterRequest request)
    {
        return Ok(await GetDetalleAsync("BMC.SP_BM_CONT_DET_GET", request));
    }

    [HttpPost("GetAllByConteoComparar")]
    public async Task<IActionResult> GetAllByConteoComparar(BmConteoDetalleFilterRequest request)
    {
        return Ok(await GetDetalleAsync("BMC.SP_BM_CONT_DET_CMP", request));
    }

    [HttpPost("Update")]
    public async Task<IActionResult> Update(BmConteoDetalleUpdateRequest request)
    {
        if (!BmDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return Ok(BmDb.InvalidList<BmConteoDetalleResponse>(error));
        }

        using var cn = connectionDB.GetBmcConnection();
        var openError = await BmDb.TryOpenAsync(cn, "BMC");
        if (openError is not null) return Ok(BmDb.InvalidList<BmConteoDetalleResponse>(openError));

        using var cmd = BmDb.StoredProcedure("BMC.SP_BM_CONT_DET_UPD", cn);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_CodigoDetalle", OracleDbType.Int32).Value = request.CodigoBmConteoDetalle;
        cmd.Parameters.Add("p_CantidadContada", OracleDbType.Int32).Value = request.CantidadContada;
        cmd.Parameters.Add("p_Comentario", OracleDbType.Varchar2).Value = BmDb.DbValue(request.Comentario);
        cmd.Parameters.Add("p_ReplicarComentario", OracleDbType.Int32).Value = request.ReplicarComentario ? 1 : 0;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);

        return Ok(await BmDb.ExecuteListAsync(cmd, MapDetalle));
    }

    [HttpPost("RecibeConteo")]
    public async Task<IActionResult> RecibeConteo(List<BmConteoRecibeItemRequest> request)
    {
        if (!BmDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return Ok(BmDb.InvalidList<BmConteoDetalleResponse>(error));
        }

        using var cn = connectionDB.GetBmcConnection();
        var openError = await BmDb.TryOpenAsync(cn, "BMC");
        if (openError is not null) return Ok(BmDb.InvalidList<BmConteoDetalleResponse>(openError));

        using var cmd = BmDb.StoredProcedure("BMC.SP_BM_CONT_DET_REC", cn);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_ItemsCsv", OracleDbType.Clob).Value = string.Join("\n", request.Select(item =>
            string.Join("|", item.Id, item.NroPlaca, item.UbicacionFisica, item.CodigoDirBien, item.KeyUbicacionResponsable)));
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);

        return Ok(await BmDb.ExecuteListAsync(cmd, MapDetalle));
    }

    private async Task<ResultDto<List<BmConteoDetalleResponse>>> GetDetalleAsync(string procedureName, BmConteoDetalleFilterRequest request)
    {
        if (!BmDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return BmDb.InvalidList<BmConteoDetalleResponse>(error);
        }

        using var cn = connectionDB.GetBmcConnection();
        var openError = await BmDb.TryOpenAsync(cn, "BMC");
        if (openError is not null) return BmDb.InvalidList<BmConteoDetalleResponse>(openError);

        using var cmd = BmDb.StoredProcedure(procedureName, cn);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_CodigoBmConteo", OracleDbType.Int32).Value = request.CodigoBmConteo;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);

        return await BmDb.ExecuteListAsync(cmd, MapDetalle);
    }

    private static BmConteoDetalleResponse MapDetalle(IDataReader reader)
    {
        var fechaMovimiento = BmDb.GetDate(reader, "FECHA_MOVIMIENTO");
        var fecha = BmDb.GetDate(reader, "FECHA");

        return new BmConteoDetalleResponse(
            reader.SafeGetInt32("CODIGO_BM_CONTEO_DETALLE"),
            reader.SafeGetInt32("CODIGO_BM_CONTEO"),
            reader.SafeGetInt32("CONTEO"),
            reader.SafeGetInt32("CODIGO_ICP"),
            reader.SafeGetString("UNIDAD_TRABAJO"),
            reader.SafeGetString("COMENTARIO"),
            reader.SafeGetString("CODIGO_PLACA"),
            reader.SafeGetInt32("CANTIDAD"),
            reader.SafeGetInt32("CANTIDAD_CONTADA"),
            reader.SafeGetInt32("CANTIDAD_CONTADA_OTRO"),
            reader.SafeGetInt32("DIFERENCIA"),
            reader.SafeGetString("CODIGO_GRUPO"),
            reader.SafeGetString("CODIGO_NIVEL1"),
            reader.SafeGetString("CODIGO_NIVEL2"),
            reader.SafeGetString("NUMERO_LOTE"),
            reader.SafeGetString("NUMERO_PLACA"),
            reader.SafeGetDecimal("VALOR_ACTUAL"),
            reader.SafeGetString("ARTICULO"),
            reader.SafeGetString("ESPECIFICACION"),
            reader.SafeGetString("SERVICIO"),
            reader.SafeGetString("RESPONSABLE_BIEN"),
            fechaMovimiento,
            BmDb.ToDateString(fechaMovimiento),
            BmDb.ToFechaDto(fechaMovimiento),
            reader.SafeGetInt32("CODIGO_BIEN"),
            reader.SafeGetInt32("CODIGO_MOV_BIEN"),
            fecha,
            BmDb.ToDateString(fecha),
            BmDb.ToFechaDto(fecha),
            reader.SafeGetBoolean("REPLICAR_COMENTARIO")
        );
    }
}
