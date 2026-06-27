using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.BienesMunicipales;

[ApiController]
[Route("api/BmProcesosMasivos")]
public class BmProcesosMasivosController(ConnectionDB connectionDB, IConfiguration config) : ControllerBase
{
    [HttpPost("Preview")]
    public async Task<IActionResult> Preview(BmProcesoMasivoRequest request)
    {
        return Ok(await ExecuteAsync("BM.SP_BM_PROC_MAS_PRE", request, false));
    }

    [HttpPost("Execute")]
    public async Task<IActionResult> Execute(BmProcesoMasivoRequest request)
    {
        return Ok(await ExecuteAsync("BM.SP_BM_PROC_MAS_EJE", request, true));
    }

    private async Task<OssmmasoftVerticalSlice.Helpers.ResultDto<List<BmProcesoMasivoResponse>>> ExecuteAsync(
        string procedureName,
        BmProcesoMasivoRequest request,
        bool includeExecutionFields)
    {
        if (!BmDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return BmDb.InvalidList<BmProcesoMasivoResponse>(error);
        }

        using var cn = connectionDB.GetBmConnection();
        var openError = await BmDb.TryOpenAsync(cn, "BM");
        if (openError is not null) return BmDb.InvalidList<BmProcesoMasivoResponse>(openError);

        using var cmd = BmDb.StoredProcedure(procedureName, cn);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_CodigoIcp", OracleDbType.Int32).Value = request.CodigoIcp;
        cmd.Parameters.Add("p_CodigoDirOrigen", OracleDbType.Int32).Value = request.CodigoDirOrigen;
        cmd.Parameters.Add("p_CodigoArticulo", OracleDbType.Int32).Value = request.CodigoArticulo;
        cmd.Parameters.Add("p_PlacasCsv", OracleDbType.Varchar2).Value = BmDb.DbValue(NormalizeCsv(request.PlacasCsv));
        cmd.Parameters.Add("p_ResponsableText", OracleDbType.Varchar2).Value = BmDb.DbValue(request.ResponsableText);
        cmd.Parameters.Add("p_CodigoDirDestino", OracleDbType.Int32).Value = request.CodigoDirDestino;

        if (includeExecutionFields)
        {
            cmd.Parameters.Add("p_ConceptoMovId", OracleDbType.Int32).Value = request.ConceptoMovId;
            cmd.Parameters.Add("p_FechaMovimiento", OracleDbType.Date).Value = BmDb.DbValue(request.FechaMovimiento);
            cmd.Parameters.Add("p_UsuarioId", OracleDbType.Int32).Value = request.UsuarioId;
            cmd.Parameters.Add("p_Observacion", OracleDbType.Varchar2).Value = BmDb.DbValue(request.Observacion);
        }

        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);

        try
        {
            return await BmDb.ExecuteListAsync(cmd, MapProcesoMasivo);
        }
        catch (Exception ex)
        {
            return BmDb.InvalidList<BmProcesoMasivoResponse>($"Error tecnico: {ex.Message}");
        }
    }

    private static string NormalizeCsv(string? values)
    {
        if (string.IsNullOrWhiteSpace(values)) return string.Empty;

        return string.Join(
            ",",
            values.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.ToUpperInvariant())
                .Distinct()
        );
    }

    private static BmProcesoMasivoResponse MapProcesoMasivo(IDataReader reader)
    {
        return new BmProcesoMasivoResponse(
            reader.SafeGetInt32("CODIGO_PROC_MASIVO"),
            reader.SafeGetInt32("CODIGO_PROC_MAS_DET"),
            reader.SafeGetInt32("CODIGO_BIEN"),
            reader.SafeGetString("NUMERO_PLACA"),
            reader.SafeGetString("ARTICULO"),
            reader.SafeGetInt32("CODIGO_DIR_ORIGEN"),
            reader.SafeGetInt32("CODIGO_ICP_ORIGEN"),
            reader.SafeGetString("UNIDAD_ORIGEN"),
            reader.SafeGetInt32("CODIGO_DIR_DESTINO"),
            reader.SafeGetString("UNIDAD_DESTINO"),
            reader.SafeGetString("ESTADO"),
            reader.SafeGetString("MENSAJE"),
            reader.SafeGetInt32("CODIGO_MOV_BIEN"),
            reader.SafeGetInt32("TOTAL_PROCESADOS"),
            reader.SafeGetInt32("TOTAL_EXITOSOS"),
            reader.SafeGetInt32("TOTAL_RECHAZADOS")
        );
    }
}
