using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.CatastroMovimientos;

public record CatastroDiferenciaResponse(DateTime? Desde, DateTime? Hasta, decimal? AforoBase, decimal? AforoAnterior, long? CodigoBeneficio, string? ViviendaPrincipal, bool Aplicado, decimal? Alicuota);
public record CatastroMovimientoResponse(long CodigoMovimiento, long CodigoConcepto, string? ConceptoExtra, string? NumeroDocumento, DateTime FechaMovimiento, decimal Monto, DateTime FechaPeriodoInicio, DateTime FechaPeriodoFin, long? AplicadoId, long? Estatus, decimal Recargos, decimal Intereses, decimal MontoExtra);
public record CatastroHistorialMovimientoResponse(List<CatastroDiferenciaResponse> Diferencias, List<CatastroMovimientoResponse> Movimientos);
public record CatastroMovimientosGetByInmuebleQuery(long CodigoInmueble, long CodigoContribuyente);

public class CatastroMovimientosGetByInmuebleHandler(ConnectionDB connections, IConfiguration config)
{
    public async Task<ResultDto<CatastroHistorialMovimientoResponse>> HandleAsync(CatastroMovimientosGetByInmuebleQuery query)
    {
        if (query.CodigoInmueble <= 0 || query.CodigoContribuyente <= 0) return Failure("Inmueble y contribuyente son obligatorios.");
        if (!int.TryParse(config["settings:EmpresaConfig"], out var empresa)) return Failure("Configuración 'EmpresaConfig' no encontrada o inválida.");
        try
        {
            var diferencias = await ReadDifferences(query, empresa);
            var movimientos = await ReadMovements(query, empresa);
            return new(new(diferencias, movimientos)) { IsValid = true, Message = "success" };
        }
        catch (Exception ex) { return Failure($"Error técnico al consultar movimientos: {ex.Message}"); }
    }

    private async Task<List<CatastroDiferenciaResponse>> ReadDifferences(CatastroMovimientosGetByInmuebleQuery query, int empresa)
    {
        using var cn = connections.GetCatConnection(); await cn.OpenAsync();
        using var cmd = Command("CAT.SP_CAT_DIF_GET_INM", cn, query, empresa);
        var rows = new List<CatastroDiferenciaResponse>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync()) rows.Add(new(Date(reader, "DESDE"), Date(reader, "HASTA"), Decimal(reader, "AFORO_B_T"), Decimal(reader, "AFORO_ANT"), Long(reader, "CODIGO_BENEFICIO"), Text(reader, "VP"), Long(reader, "APLICADO") == 1, Decimal(reader, "ALICUOTA")));
        return rows;
    }

    private async Task<List<CatastroMovimientoResponse>> ReadMovements(CatastroMovimientosGetByInmuebleQuery query, int empresa)
    {
        using var cn = connections.GetRmConnection(); await cn.OpenAsync();
        using var cmd = Command("RM.SP_RM_MOV_GET_INM", cn, query, empresa);
        var rows = new List<CatastroMovimientoResponse>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync()) rows.Add(new(Convert.ToInt64(reader["CODIGO_MOV_RAMO"]), Convert.ToInt64(reader["CODIGO_CONCEPTO"]), Text(reader, "EXTRA_CONCEPTO"), Text(reader, "NUMERO_DOCUMENTO"), Convert.ToDateTime(reader["FECHA_MOV"]), Convert.ToDecimal(reader["MONTO_MOV"]), Convert.ToDateTime(reader["FECHA_PERIODO_INI"]), Convert.ToDateTime(reader["FECHA_PERIODO_FIN"]), Long(reader, "APLICADO_ID"), Long(reader, "ESTATUS"), Convert.ToDecimal(reader["MONTO_RECARGOS"]), Convert.ToDecimal(reader["MONTO_INTERESES"]), Convert.ToDecimal(reader["MONTO_EXTRA"])));
        return rows;
    }

    private static OracleCommand Command(string name, OracleConnection cn, CatastroMovimientosGetByInmuebleQuery query, int empresa)
    {
        var cmd = new OracleCommand(name, cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
        cmd.Parameters.Add("p_CodigoInmueble", OracleDbType.Int64).Value = query.CodigoInmueble;
        cmd.Parameters.Add("p_CodigoContribuyente", OracleDbType.Int64).Value = query.CodigoContribuyente;
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
        return cmd;
    }

    private static string? Text(OracleDataReader r, string n) => r[n] is DBNull ? null : r[n].ToString();
    private static long? Long(OracleDataReader r, string n) => r[n] is DBNull ? null : Convert.ToInt64(r[n]);
    private static decimal? Decimal(OracleDataReader r, string n) => r[n] is DBNull ? null : Convert.ToDecimal(r[n]);
    private static DateTime? Date(OracleDataReader r, string n) => r[n] is DBNull ? null : Convert.ToDateTime(r[n]);
    private static ResultDto<CatastroHistorialMovimientoResponse> Failure(string message) => new(null!) { IsValid = false, Message = message };
}

[ApiController]
[Authorize]
[Route("api/CatastroMovimientos")]
public class CatastroMovimientosGetByInmuebleController(ConnectionDB connections, IConfiguration config) : ControllerBase
{
    [HttpPost("getByInmueble")]
    public async Task<IActionResult> GetByInmueble(CatastroMovimientosGetByInmuebleQuery query) => Ok(await new CatastroMovimientosGetByInmuebleHandler(connections, config).HandleAsync(query));
}
