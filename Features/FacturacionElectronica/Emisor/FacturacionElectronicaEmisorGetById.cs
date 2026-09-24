using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.FacturacionElectronica;

public record FacturacionElectronicaEmisorGetByIdQuery(long Id);

public class FacturacionElectronicaEmisorGetByIdHandler(ConnectionDB _connectionDB)
{
    public async Task<ResultDto<EmisorResponse>> HandleAsync(FacturacionElectronicaEmisorGetByIdQuery query)
    {
        if (query.Id <= 0)
        {
            return Falla("El identificador del emisor no es válido.");
        }

        using var cn = _connectionDB.GetFedConnection();

        try
        {
            await cn.OpenAsync();
        }
        catch (Exception ex)
        {
            return Falla($"Error técnico al abrir conexión FED: {ex.Message}");
        }

        try
        {
            using var cmd = new NpgsqlCommand(FacturacionElectronicaDb.SqlEmisorGetById, cn);
            cmd.Parameters.AddWithValue("id", query.Id);

            EmisorResponse? emisor = null;

            using (var reader = await cmd.ExecuteReaderAsync())
            {
                if (await reader.ReadAsync())
                {
                    emisor = FacturacionElectronicaDb.MapEmisor(reader);
                }
            }

            if (emisor is null)
            {
                return Falla("No se encontró el emisor solicitado.");
            }

            return new ResultDto<EmisorResponse>(emisor)
            {
                IsValid = true,
                Message = FacturacionElectronicaDb.MensajeExito
            };
        }
        catch (Exception ex)
        {
            return Falla($"Error técnico: {ex.Message}");
        }
    }

    private static ResultDto<EmisorResponse> Falla(string mensaje) =>
        new(null!) { Data = null, IsValid = false, Message = mensaje };
}

[ApiController]
[Authorize]
[Route("api/FacturacionElectronica")]
public class FacturacionElectronicaEmisorGetByIdController(ConnectionDB _connectionDB) : ControllerBase
{
    [HttpPost]
    [Route("getById")]
    public async Task<IActionResult> GetById(FacturacionElectronicaEmisorGetByIdQuery value)
    {
        var handler = new FacturacionElectronicaEmisorGetByIdHandler(_connectionDB);
        var result = await handler.HandleAsync(value);

        return Ok(result);
    }
}
