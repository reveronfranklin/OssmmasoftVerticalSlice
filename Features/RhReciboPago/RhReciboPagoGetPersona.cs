using System.Data;
using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.RhReciboPago;

public record RhReciboPersonaQuery(int CodigoPersona);
public record RhReciboPersonaFilter(int CodigoPersona, int CodigoTipoNomina, int CodigoPeriodo)
{
    public string? Validate() => CodigoPersona <= 0 || CodigoTipoNomina <= 0 || CodigoPeriodo <= 0
        ? "Persona, tipo de nomina y periodo son obligatorios y deben ser mayores de cero." : null;
    public RhReciboPagoFilter ToFilter() => new(CodigoTipoNomina, CodigoPeriodo, CodigoPersona: CodigoPersona);
}
public record RhReciboPersonaResumen(string Id, int CodigoPersona, int CodigoTipoNomina, int CodigoPeriodo,
    string TipoNomina, DateTime FechaNomina, string FechaNominaString);

public class RhReciboPagoGetPersonaHandler(ConnectionDB db, IConfiguration config)
{
    public async Task<ResultDto<List<RhReciboPersonaResumen>>> HandleAsync(RhReciboPersonaQuery query, CancellationToken ct = default)
    {
        if (query.CodigoPersona <= 0) return RhReciboPagoDb.Fail<List<RhReciboPersonaResumen>>("Seleccione una persona valida.");
        if (!int.TryParse(config["settings:EmpresaConfig"], out var empresa) || empresa <= 0)
            return RhReciboPagoDb.Fail<List<RhReciboPersonaResumen>>("EmpresaConfig debe ser un numero valido.");
        using var cn = db.GetRhConnection();
        try { await cn.OpenAsync(ct); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { return RhReciboPagoDb.Fail<List<RhReciboPersonaResumen>>($"Error tecnico al abrir conexion RH: {ex.Message}"); }
        using var cmd = new OracleCommand("RH.SP_RH_REC_PERS_RES_GET", cn) { CommandType = CommandType.StoredProcedure, BindByName = true };
        cmd.Parameters.Add("p_EMPRESA", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_PERSONA", OracleDbType.Int32).Value = query.CodigoPersona;
        var message = cmd.Parameters.Add("p_MESSAGE", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);
        cmd.Parameters.Add("p_RESULT", OracleDbType.RefCursor, ParameterDirection.Output);
        try
        {
            var rows = new List<RhReciboPersonaResumen>();
            using (var reader = await cmd.ExecuteReaderAsync(ct))
                while (await reader.ReadAsync(ct))
                {
                    var persona = reader.SafeGetInt32("CODIGO_PERSONA");
                    var tipo = reader.SafeGetInt32("CODIGO_TIPO_NOMINA");
                    var periodo = reader.SafeGetInt32("CODIGO_PERIODO");
                    var fecha = reader.IsDBNull(reader.GetOrdinal("FECHA_NOMINA")) ? default : reader.GetDateTime(reader.GetOrdinal("FECHA_NOMINA"));
                    rows.Add(new($"{persona}:{tipo}:{periodo}", persona, tipo, periodo,
                        reader.SafeGetString("TIPO_NOMINA"), fecha, fecha == default ? "" : fecha.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)));
                }
            var text = message.Value?.ToString() ?? "Sin respuesta de la base de datos.";
            if (!RhReciboPagoDb.Success(text)) return RhReciboPagoDb.Fail<List<RhReciboPersonaResumen>>(text);
            return new(rows) { IsValid = true, Message = "success", CantidadRegistros = rows.Count };
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { return RhReciboPagoDb.Fail<List<RhReciboPersonaResumen>>($"Error tecnico: {ex.Message}"); }
    }
}

[Authorize]
[ApiController]
[Route("api/RhReciboPago")]
public class RhReciboPagoGetPersonaController(ConnectionDB db, IConfiguration config) : ControllerBase
{
    [HttpPost("GetHistoricoPersona")]
    public async Task<IActionResult> GetHistoricoPersona(RhReciboPersonaQuery value, CancellationToken ct)
        => Ok(await new RhReciboPagoGetPersonaHandler(db, config).HandleAsync(value, ct));
}
