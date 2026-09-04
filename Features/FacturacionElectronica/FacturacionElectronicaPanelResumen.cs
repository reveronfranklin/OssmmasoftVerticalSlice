using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.FacturacionElectronica;

// Panel del modulo: los contadores del periodo en curso, en una sola consulta.
//
// No agrega ninguna capacidad fiscal. Hace visible lo que las fases anteriores
// ya garantizan, y sobre todo una cosa: cuantos periodos del ano calendario
// quedaron vencidos. Dos bastan para revocar la autorizacion, sin sancion
// previa (Art. 34.3), asi que ese numero no puede depender de que alguien entre
// a la pantalla del reporte a mirarlo.
//
// El periodo se acepta como parametro para poder consultar un mes cerrado, pero
// vacio significa "el mes en curso", que es como lo llama la pantalla.
public record FacturacionElectronicaPanelResumenQuery(string Periodo = "");

public class FacturacionElectronicaPanelResumenHandler(ConnectionDB _connectionDB)
{
    public async Task<ResultDto<PanelResumenResponse>> HandleAsync(FacturacionElectronicaPanelResumenQuery query)
    {
        string periodo = (query.Periodo ?? string.Empty).Trim();

        if (periodo.Length == 0)
        {
            periodo = DateTime.Now.ToString("yyyyMM");
        }
        else if (periodo.Length != 6 || !periodo.All(char.IsDigit))
        {
            // Falla de entrada, no excepcion: el contrato del proyecto pide que lo
            // esperado viaje como IsValid = false.
            return Falla("El período debe venir en formato AAAAMM, por ejemplo 202609.");
        }

        string anio = periodo[..4];

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
            using var cmd = new NpgsqlCommand(FacturacionElectronicaDb.SqlPanelResumen, cn);
            cmd.Parameters.AddWithValue("periodo", periodo);
            cmd.Parameters.AddWithValue("anio", anio);

            using var reader = await cmd.ExecuteReaderAsync();

            if (!await reader.ReadAsync())
            {
                // La consulta parte de un FROM (SELECT 1), asi que siempre devuelve
                // una fila. Si no la devolvio, algo cambio en el SQL y es preferible
                // decirlo a mostrar un panel en cero que parezca un sistema vacio.
                return Falla("La consulta del panel no devolvió resultados.");
            }

            var resumen = FacturacionElectronicaDb.MapPanelResumen(reader, periodo);

            return new ResultDto<PanelResumenResponse>(resumen)
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

    private static ResultDto<PanelResumenResponse> Falla(string mensaje) =>
        new(null!) { Data = null, IsValid = false, Message = mensaje };
}

[ApiController]
[Authorize]
[Route("api/FacturacionElectronica")]
public class FacturacionElectronicaPanelResumenController(ConnectionDB _connectionDB) : ControllerBase
{
    [HttpPost]
    [Route("panelResumen")]
    public async Task<IActionResult> PanelResumen(FacturacionElectronicaPanelResumenQuery value)
    {
        var handler = new FacturacionElectronicaPanelResumenHandler(_connectionDB);
        var result = await handler.HandleAsync(value);

        return Ok(result);
    }
}
