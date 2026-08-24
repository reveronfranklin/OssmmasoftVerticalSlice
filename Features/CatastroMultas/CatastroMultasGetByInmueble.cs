using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.CatastroMultas;

public record CatastroMultaResponse(long CodigoMulta, long? CodigoDocumentoLegal, decimal? Monto, DateTime? Fecha, DateTime? FechaRegistro, long? CodigoFicha);
public record CatastroMultasGetByInmuebleQuery(long CodigoInmueble, long CodigoContribuyente);

public class CatastroMultasGetByInmuebleHandler(ConnectionDB connections, IConfiguration config)
{
    public async Task<ResultDto<List<CatastroMultaResponse>>> HandleAsync(CatastroMultasGetByInmuebleQuery query)
    {
        if (query.CodigoInmueble <= 0 || query.CodigoContribuyente <= 0) return Failure("Inmueble y contribuyente son obligatorios.");
        if (!int.TryParse(config["settings:EmpresaConfig"], out var empresa)) return Failure("Configuración 'EmpresaConfig' no encontrada o inválida.");
        using var cn = connections.GetCatConnection();
        try { await cn.OpenAsync(); } catch (Exception ex) { return Failure($"Error técnico al abrir conexión CAT: {ex.Message}"); }
        using var cmd = new OracleCommand("CAT.SP_CAT_MULTAS_GET_INM", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
        cmd.Parameters.Add("p_CodigoInmueble", OracleDbType.Int64).Value = query.CodigoInmueble;
        cmd.Parameters.Add("p_CodigoContribuyente", OracleDbType.Int64).Value = query.CodigoContribuyente;
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
        try
        {
            var rows = new List<CatastroMultaResponse>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) rows.Add(new(Convert.ToInt64(reader["CODIGO_MULTA"]), Long(reader,"CODIGO_DOCUMENTOS_LEGALES"), Decimal(reader,"MONTO"), Date(reader,"FECHA"), Date(reader,"FECHA_REGISTRO"), Long(reader,"CODIGO_FICHA")));
            return new(rows) { IsValid = true, Message = "success", CantidadRegistros = rows.Count };
        }
        catch (Exception ex) { return Failure($"Error técnico: {ex.Message}"); }
    }
    private static long? Long(OracleDataReader r,string n)=>r[n] is DBNull?null:Convert.ToInt64(r[n]);
    private static decimal? Decimal(OracleDataReader r,string n)=>r[n] is DBNull?null:Convert.ToDecimal(r[n]);
    private static DateTime? Date(OracleDataReader r,string n)=>r[n] is DBNull?null:Convert.ToDateTime(r[n]);
    private static ResultDto<List<CatastroMultaResponse>> Failure(string message)=>new(null!){IsValid=false,Message=message};
}

[ApiController]
[Authorize]
[Route("api/CatastroMultas")]
public class CatastroMultasGetByInmuebleController(ConnectionDB connections, IConfiguration config) : ControllerBase
{
    [HttpPost("getByInmueble")]
    public async Task<IActionResult> GetByInmueble(CatastroMultasGetByInmuebleQuery query)=>Ok(await new CatastroMultasGetByInmuebleHandler(connections,config).HandleAsync(query));
}
