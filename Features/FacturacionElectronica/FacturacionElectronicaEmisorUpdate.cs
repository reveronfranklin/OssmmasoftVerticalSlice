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
    string UsuarioUpd,
    string TipoContribuyente = "ordinario");

public class FacturacionElectronicaEmisorUpdateHandler(ConnectionDB _connectionDB)
{
    private static readonly string[] EstadosValidos = ["activo", "inactivo"];
    private static readonly string[] TiposContribuyenteValidos = ["ordinario", "formal", "no_sujeto"];
    private static readonly string[] RifVerificadoEstadosValidos = ["vigente", "no_vigente", "sin_verificar"];

    public async Task<ResultDto<string>> HandleAsync(FacturacionElectronicaEmisorUpdateCommand command)
    {
        if (command.Id <= 0)
        {
            return Falla("El identificador del emisor no es válido.");
        }

        // Cada campo contra su columna real, mismo criterio que EmisorCreate:
        // sin esto, un valor mas largo que su VARCHAR(n) filtraba un SQLSTATE
        // crudo por el catch generico.
        string? error =
            FacturacionElectronicaDb.ValidarTexto(command.RazonSocial, "La razón social", 200)
            ?? FacturacionElectronicaDb.ValidarTexto(command.DomicilioFiscal, "El domicilio fiscal", 300)
            ?? FacturacionElectronicaDb.ValidarTexto(command.Correo, "El correo", 150, obligatorio: false)
            ?? FacturacionElectronicaDb.ValidarTexto(command.UsuarioUpd, "El usuario", 50, obligatorio: false);

        if (error is not null)
        {
            return Falla(error);
        }

        string estado = string.IsNullOrWhiteSpace(command.Estado) ? "activo" : command.Estado.Trim().ToLowerInvariant();

        if (!EstadosValidos.Contains(estado))
        {
            return Falla("El estado del emisor debe ser activo o inactivo.");
        }

        string tipoContribuyente = string.IsNullOrWhiteSpace(command.TipoContribuyente)
            ? "ordinario"
            : command.TipoContribuyente.Trim();

        if (!TiposContribuyenteValidos.Contains(tipoContribuyente))
        {
            return Falla("El tipo de contribuyente debe ser ordinario, formal o no_sujeto.");
        }

        string rifVerificadoEstado = (command.RifVerificadoEstado ?? string.Empty).Trim();

        if (rifVerificadoEstado.Length > 0 && !RifVerificadoEstadosValidos.Contains(rifVerificadoEstado))
        {
            return Falla("El estado de verificación del RIF debe ser vigente, no_vigente o sin_verificar.");
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
            cmd.Parameters.AddWithValue("rif_verificado_estado", FacturacionElectronicaDb.DbValue(rifVerificadoEstado));
            cmd.Parameters.AddWithValue("tipo_contribuyente", tipoContribuyente);
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
