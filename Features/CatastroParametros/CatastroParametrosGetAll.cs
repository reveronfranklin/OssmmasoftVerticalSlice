using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.CatastroParametros;

public record CatastroParametrosGetAllQuery(int? Ano = null);
public record CatastroAjusteResponse(decimal? Porcentaje, decimal? Alicuota, string? NombreDirector, decimal? DiasMultaCambioFirma, decimal? UnidadTributaria, string? NumeroResolucion, DateTime? FechaResolucion, string? Denominacion, string? BaseJuridica);
public record CatastroTablaImpositivaResponse(long Ano, long? Estado, long? Municipio, long? Parroquia, string? Ambito, long? Tipo, long? TipoUnidad, decimal? Valor, string? Descripcion, decimal? Porcentaje, decimal? ValorAjustado, long? TipoCalculo, bool Deprecia, decimal Ajuste);
public record CatastroZonificacionResponse(long CodigoZonificacion, string? Codigo, string? Descripcion);
public record CatastroParametrosResponse(List<CatastroAjusteResponse> Ajustes, List<CatastroTablaImpositivaResponse> TablaImpositiva, List<CatastroZonificacionResponse> Zonificaciones);

public class CatastroParametrosGetAllHandler(ConnectionDB connections, IConfiguration config)
{
    public async Task<ResultDto<CatastroParametrosResponse>> HandleAsync(CatastroParametrosGetAllQuery query)
    {
        if (!int.TryParse(config["settings:EmpresaConfig"], out var empresa)) return Failure("Configuración 'EmpresaConfig' no encontrada o inválida.");
        using var cn = connections.GetCatConnection();
        try { await cn.OpenAsync(); } catch (Exception ex) { return Failure($"Error técnico al abrir conexión CAT: {ex.Message}"); }
        using var cmd = new OracleCommand("CAT.SP_CAT_PAR_GET_ALL", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
        cmd.Parameters.Add("p_Ano", OracleDbType.Int32).Value = query.Ano.HasValue ? query.Ano.Value : DBNull.Value;
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_Ajustes", OracleDbType.RefCursor, ParameterDirection.Output);
        cmd.Parameters.Add("p_Tabla", OracleDbType.RefCursor, ParameterDirection.Output);
        cmd.Parameters.Add("p_Zonas", OracleDbType.RefCursor, ParameterDirection.Output);
        try
        {
            var ajustes = new List<CatastroAjusteResponse>(); var tabla = new List<CatastroTablaImpositivaResponse>(); var zonas = new List<CatastroZonificacionResponse>();
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync()) ajustes.Add(new(Dec(reader,"PORCENTAJE"), Dec(reader,"ALICUOTA"), Text(reader,"NOMBRE_DIRECTOR"), Dec(reader,"DIAS_MULTA_CAMBIO_FIRMA"), Dec(reader,"UNIDAD_TRIBUTARIA"), Text(reader,"NUMERO_RESOLUCION"), Date(reader,"FECHA_RESOLUCION"), Text(reader,"DENOMINACION"), Text(reader,"BASE_JURIDICA")));
            if (await reader.NextResultAsync()) while (await reader.ReadAsync()) tabla.Add(new(Convert.ToInt64(reader["ANO"]), Long(reader,"ESTADO"), Long(reader,"MUNICIPIO"), Long(reader,"PARROQUIA"), Text(reader,"AMBITO"), Long(reader,"TIPO"), Long(reader,"TIPO_UNIDAD"), Dec(reader,"VALOR"), Text(reader,"DESCRIPCION"), Dec(reader,"PORCENTAJE"), Dec(reader,"VALOR_AJUSTADO"), Long(reader,"TIPO_CALCULO"), Text(reader,"DEPRECIA") == "1", Convert.ToDecimal(reader["AJUSTE"])));
            if (await reader.NextResultAsync()) while (await reader.ReadAsync()) zonas.Add(new(Convert.ToInt64(reader["CODIGO_ZONIFICACION"]), Text(reader,"CODIGO"), Text(reader,"DESCRIPCION")));
            return new(new(ajustes, tabla, zonas)) { IsValid = true, Message = "success" };
        }
        catch (Exception ex) { return Failure($"Error técnico: {ex.Message}"); }
    }
    private static string? Text(OracleDataReader r,string n)=>r[n] is DBNull?null:r[n].ToString();
    private static long? Long(OracleDataReader r,string n)=>r[n] is DBNull?null:Convert.ToInt64(r[n]);
    private static decimal? Dec(OracleDataReader r,string n)=>r[n] is DBNull?null:Convert.ToDecimal(r[n]);
    private static DateTime? Date(OracleDataReader r,string n)=>r[n] is DBNull?null:Convert.ToDateTime(r[n]);
    private static ResultDto<CatastroParametrosResponse> Failure(string message)=>new(null!){IsValid=false,Message=message};
}

[ApiController]
[Authorize]
[Route("api/CatastroParametros")]
public class CatastroParametrosGetAllController(ConnectionDB connections, IConfiguration config) : ControllerBase
{
    [HttpPost("GetAll")]
    public async Task<IActionResult> GetAll(CatastroParametrosGetAllQuery query)=>Ok(await new CatastroParametrosGetAllHandler(connections,config).HandleAsync(query));
}
