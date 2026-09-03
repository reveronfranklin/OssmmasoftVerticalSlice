using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.FacturacionElectronica;

public record FacturacionElectronicaHealthQuery();

public class FacturacionElectronicaHealthHandler(ConnectionDB _connectionDB)
{
    public async Task<ResultDto<string>> HandleAsync(FacturacionElectronicaHealthQuery query)
    {
        // Sin TryGetEmpresa ni IConfiguration: este endpoint no toca datos de
        // empresa. El nivel 1 de validacion del estandar no aplica; los niveles
        // 2 y 3 si. IConfiguration entra en la Fase 1, con la primera operacion
        // real sobre datos.
        using var cn = _connectionDB.GetFedConnection();

        try
        {
            await cn.OpenAsync();
        }
        catch (Exception ex)
        {
            return new ResultDto<string>(string.Empty)
            {
                Data = null,
                IsValid = false,
                Message = $"Error técnico al abrir conexión FED: {ex.Message}"
            };
        }

        try
        {
            using var cmd = new NpgsqlCommand(FacturacionElectronicaDb.SqlHealth, cn);
            object? valor = await cmd.ExecuteScalarAsync();

            string identidad = valor?.ToString() ?? string.Empty;

            return new ResultDto<string>(identidad)
            {
                IsValid = !string.IsNullOrWhiteSpace(identidad),
                Message = FacturacionElectronicaDb.MensajeExito
            };
        }
        catch (Exception ex)
        {
            return new ResultDto<string>(string.Empty)
            {
                Data = null,
                IsValid = false,
                Message = $"Error técnico: {ex.Message}"
            };
        }
    }
}

[ApiController]
[Authorize]
[Route("api/FacturacionElectronica")]
public class FacturacionElectronicaHealthController(ConnectionDB _connectionDB) : ControllerBase
{
    [HttpPost]
    [Route("health")]
    public async Task<IActionResult> Health(FacturacionElectronicaHealthQuery value)
    {
        var handler = new FacturacionElectronicaHealthHandler(_connectionDB);
        var result = await handler.HandleAsync(value);

        return Ok(result);
    }
}
