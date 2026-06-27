using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.BienesMunicipales;

[ApiController]
[Route("api/BmConteo")]
public class BmConteoController(ConnectionDB connectionDB, IConfiguration config) : ControllerBase
{
    [HttpGet("GetAll")]
    public async Task<IActionResult> GetAll()
    {
        if (!BmDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return Ok(BmDb.InvalidList<BmConteoResponse>(error));
        }

        using var cn = connectionDB.GetBmcConnection();
        var openError = await BmDb.TryOpenAsync(cn, "BMC");
        if (openError is not null) return Ok(BmDb.InvalidList<BmConteoResponse>(openError));

        using var cmd = BmDb.StoredProcedure("BMC.SP_BM_CONTEO_GET_ALL", cn);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);

        return Ok(await BmDb.ExecuteListAsync(cmd, MapConteo));
    }

    [HttpPost("Create")]
    public async Task<IActionResult> Create(BmConteoUpsertRequest request)
    {
        return Ok(await MutateConteoAsync("BMC.SP_BM_CONTEO_INS", request));
    }

    [HttpPost("Update")]
    public async Task<IActionResult> Update(BmConteoUpsertRequest request)
    {
        return Ok(await MutateConteoAsync("BMC.SP_BM_CONTEO_UPD", request));
    }

    [HttpPost("Delete")]
    public async Task<IActionResult> Delete(BmConteoDeleteRequest request)
    {
        if (!BmDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return Ok(BmDb.InvalidList<BmConteoResponse>(error));
        }

        using var cn = connectionDB.GetBmcConnection();
        var openError = await BmDb.TryOpenAsync(cn, "BMC");
        if (openError is not null) return Ok(BmDb.InvalidList<BmConteoResponse>(openError));

        using var cmd = BmDb.StoredProcedure("BMC.SP_BM_CONTEO_DEL", cn);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_CodigoBmConteo", OracleDbType.Int32).Value = request.CodigoBmConteo;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);

        return Ok(await BmDb.ExecuteListAsync(cmd, MapConteo));
    }

    [HttpPost("CerrarConteo")]
    public async Task<IActionResult> CerrarConteo(BmConteoCerrarRequest request)
    {
        if (!BmDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return Ok(BmDb.InvalidList<BmConteoResponse>(error));
        }

        using var cn = connectionDB.GetBmcConnection();
        var openError = await BmDb.TryOpenAsync(cn, "BMC");
        if (openError is not null) return Ok(BmDb.InvalidList<BmConteoResponse>(openError));

        using var cmd = BmDb.StoredProcedure("BMC.SP_BM_CONTEO_CERRAR", cn);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_CodigoBmConteo", OracleDbType.Int32).Value = request.CodigoBmConteo;
        cmd.Parameters.Add("p_Comentario", OracleDbType.Varchar2).Value = BmDb.DbValue(request.Comentario);
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);

        return Ok(await BmDb.ExecuteListAsync(cmd, MapConteo));
    }

    private async Task<ResultDto<List<BmConteoResponse>>> MutateConteoAsync(string procedureName, BmConteoUpsertRequest request)
    {
        if (!BmDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return BmDb.InvalidList<BmConteoResponse>(error);
        }

        using var cn = connectionDB.GetBmcConnection();
        var openError = await BmDb.TryOpenAsync(cn, "BMC");
        if (openError is not null) return BmDb.InvalidList<BmConteoResponse>(openError);

        using var cmd = BmDb.StoredProcedure(procedureName, cn);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_CodigoBmConteo", OracleDbType.Int32).Value = request.CodigoBmConteo;
        cmd.Parameters.Add("p_Titulo", OracleDbType.Varchar2).Value = BmDb.DbValue(request.Titulo);
        cmd.Parameters.Add("p_Comentario", OracleDbType.Varchar2).Value = BmDb.DbValue(request.Comentario);
        cmd.Parameters.Add("p_CodigoPersonaResp", OracleDbType.Int32).Value = request.CodigoPersonaResponsable;
        cmd.Parameters.Add("p_ConteoId", OracleDbType.Int32).Value = request.ConteoId;
        cmd.Parameters.Add("p_Fecha", OracleDbType.Date).Value = BmDb.DbValue(request.Fecha);
        cmd.Parameters.Add("p_CodigosIcp", OracleDbType.Varchar2).Value = BmDb.DbValue(BmDb.ToIcpCsv(request.ListIcpSeleccionado));
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);

        return await BmDb.ExecuteListAsync(cmd, MapConteo);
    }

    internal static BmConteoResponse MapConteo(IDataReader reader)
    {
        var fecha = BmDb.GetDate(reader, "FECHA");
        var totalCantidad = reader.SafeGetInt32("TOTAL_CANTIDAD");
        var totalContada = reader.SafeGetInt32("TOTAL_CANTIDAD_CONTADA");
        var totalDiferencia = reader.SafeGetInt32("TOTAL_DIFERENCIA");
        var codigo = reader.SafeGetInt32("CODIGO_BM_CONTEO");

        return new BmConteoResponse(
            codigo,
            reader.SafeGetString("TITULO"),
            reader.SafeGetString("COMENTARIO"),
            reader.SafeGetInt32("CODIGO_PERSONA_RESPONSABLE"),
            reader.SafeGetString("NOMBRE_PERSONA_RESPONSABLE"),
            reader.SafeGetInt32("CONTEO_ID"),
            fecha,
            BmDb.ToDateString(fecha),
            BmDb.ToFechaDto(fecha),
            new List<BmConteoDetalleResumenResponse>
            {
                new(codigo, reader.SafeGetInt32("CONTEO"), totalCantidad, totalContada, totalDiferencia)
            },
            BmDb.ToDateString(fecha),
            totalCantidad,
            totalContada,
            totalDiferencia
        );
    }
}
