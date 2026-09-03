using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.FacturacionElectronica;

// El RIF no viaja en el request a proposito. El Articulo 30 ata la secuencia de
// numero de control al RIF del emisor: cambiarlo romperia la unicidad por emisor
// de todo lo ya asignado. Para corregir un RIF equivocado se desactiva el emisor
// y se da de alta el correcto.
public record FacturacionElectronicaEmisorUpdateCommand(
    long Id,
    string RazonSocial,
    string DomicilioFiscal,
    string Correo,
    string Estado,
    DateTime? RifVerificadoEl,
    string RifVerificadoEstado,
    string UsuarioUpd);

public class FacturacionElectronicaEmisorUpdateHandler(ConnectionDB _connectionDB)
{
    private static readonly string[] EstadosValidos = ["activo", "inactivo"];

    public async Task<ResultDto<string>> HandleAsync(FacturacionElectronicaEmisorUpdateCommand command)
    {
        if (command.Id <= 0)
        {
            return Falla("El identificador del emisor no es válido.");
        }

        if (string.IsNullOrWhiteSpace(command.RazonSocial))
        {
            return Falla("La razón social es obligatoria.");
        }

        if (string.IsNullOrWhiteSpace(command.DomicilioFiscal))
        {
            return Falla("El domicilio fiscal es obligatorio.");
        }

        string estado = string.IsNullOrWhiteSpace(command.Estado) ? "activo" : command.Estado.Trim().ToLowerInvariant();

        if (!EstadosValidos.Contains(estado))
        {
            return Falla("El estado del emisor debe ser activo o inactivo.");
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
            using var cmd = new NpgsqlCommand(FacturacionElectronicaDb.SqlEmisorUpdate, cn);
            cmd.Parameters.AddWithValue("id", command.Id);
            cmd.Parameters.AddWithValue("razon_social", command.RazonSocial.Trim());
            cmd.Parameters.AddWithValue("domicilio_fiscal", command.DomicilioFiscal.Trim());
            cmd.Parameters.AddWithValue("correo", FacturacionElectronicaDb.DbValue(command.Correo));
            cmd.Parameters.AddWithValue("estado", estado);
            cmd.Parameters.AddWithValue("rif_verificado_el", FacturacionElectronicaDb.DbValueFecha(command.RifVerificadoEl));
            cmd.Parameters.AddWithValue("rif_verificado_estado", FacturacionElectronicaDb.DbValue(command.RifVerificadoEstado));
            cmd.Parameters.AddWithValue("usuario_upd", FacturacionElectronicaDb.DbValue(command.UsuarioUpd));

            int filas = await cmd.ExecuteNonQueryAsync();

            if (filas == 0)
            {
                return Falla("No se encontró el emisor que se intenta actualizar.");
            }

            return new ResultDto<string>(FacturacionElectronicaDb.MensajeExito)
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

    private static ResultDto<string> Falla(string mensaje) =>
        new(string.Empty) { Data = null, IsValid = false, Message = mensaje };
}

[ApiController]
[Authorize]
[Route("api/FacturacionElectronica")]
public class FacturacionElectronicaEmisorUpdateController(ConnectionDB _connectionDB) : ControllerBase
{
    [HttpPost]
    [Route("update")]
    public async Task<IActionResult> Update(FacturacionElectronicaEmisorUpdateCommand value)
    {
        var handler = new FacturacionElectronicaEmisorUpdateHandler(_connectionDB);
        var result = await handler.HandleAsync(value);

        return Ok(result);
    }
}
