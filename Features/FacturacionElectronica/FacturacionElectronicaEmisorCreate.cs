using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.FacturacionElectronica;

public record FacturacionElectronicaEmisorCreateCommand(
    string Rif,
    string RazonSocial,
    string DomicilioFiscal,
    string Correo,
    string UsuarioIns);

public class FacturacionElectronicaEmisorCreateHandler(ConnectionDB _connectionDB)
{
    public async Task<ResultDto<int>> HandleAsync(FacturacionElectronicaEmisorCreateCommand command)
    {
        // Nivel 1 - validacion previa. Sin TryGetEmpresa: el modulo no toca datos
        // de empresa. Un emisor es un cliente externo identificado por su RIF, no
        // el settings:EmpresaConfig de Ossmmasoft.
        if (string.IsNullOrWhiteSpace(command.Rif))
        {
            return Falla("El RIF del emisor es obligatorio.");
        }

        if (string.IsNullOrWhiteSpace(command.RazonSocial))
        {
            return Falla("La razón social es obligatoria.");
        }

        if (string.IsNullOrWhiteSpace(command.DomicilioFiscal))
        {
            return Falla("El domicilio fiscal es obligatorio.");
        }

        using var cn = _connectionDB.GetFedConnection();

        // Nivel 2 - apertura de conexion.
        try
        {
            await cn.OpenAsync();
        }
        catch (Exception ex)
        {
            return Falla($"Error técnico al abrir conexión FED: {ex.Message}");
        }

        // Nivel 3 - ejecucion.
        try
        {
            using var cmd = new NpgsqlCommand(FacturacionElectronicaDb.SqlEmisorCreate, cn);
            cmd.Parameters.AddWithValue("rif", command.Rif.Trim());
            cmd.Parameters.AddWithValue("razon_social", command.RazonSocial.Trim());
            cmd.Parameters.AddWithValue("domicilio_fiscal", command.DomicilioFiscal.Trim());
            cmd.Parameters.AddWithValue("correo", FacturacionElectronicaDb.DbValue(command.Correo));
            cmd.Parameters.AddWithValue("estado", "activo");
            cmd.Parameters.AddWithValue("usuario_ins", FacturacionElectronicaDb.DbValue(command.UsuarioIns));

            object? id = await cmd.ExecuteScalarAsync();

            return new ResultDto<int>(Convert.ToInt32(id))
            {
                IsValid = true,
                Message = FacturacionElectronicaDb.MensajeExito
            };
        }
        catch (NpgsqlException ex) when (FacturacionElectronicaDb.EsRifDuplicado(ex))
        {
            // La defensa es el UNIQUE de la tabla, no un SELECT previo: entre la
            // consulta y el insert cabe otra peticion.
            return Falla($"Ya existe un emisor registrado con el RIF {command.Rif.Trim()}.");
        }
        catch (Exception ex)
        {
            return Falla($"Error técnico: {ex.Message}");
        }
    }

    private static ResultDto<int> Falla(string mensaje) =>
        new(0) { IsValid = false, Message = mensaje };
}

[ApiController]
[Authorize]
[Route("api/FacturacionElectronica")]
public class FacturacionElectronicaEmisorCreateController(ConnectionDB _connectionDB) : ControllerBase
{
    [HttpPost]
    [Route("create")]
    public async Task<IActionResult> Create(FacturacionElectronicaEmisorCreateCommand value)
    {
        var handler = new FacturacionElectronicaEmisorCreateHandler(_connectionDB);
        var result = await handler.HandleAsync(value);

        return Ok(result);
    }
}
