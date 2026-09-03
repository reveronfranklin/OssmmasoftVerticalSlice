using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.FacturacionElectronica;

// Lectura del registro automatizado del Articulo 32.
//
// Es lo que el SENIAT puede consultar y lo que alimenta el reporte mensual. Sale
// de una vista, no de una tabla: por construccion no puede diferir de lo asignado
// (decision D-7), que es justo lo que castiga el Art. 34.3.
//
// Filtros: por RIF del emisor -numeral 1- y por periodo AAAAMM, que es la unidad
// en que se reporta.
public record FacturacionElectronicaRegistroArt32GetAllQuery(
    string EmisorRif = "",
    string Periodo = "",
    int PageSize = 10,
    int PageNumber = 1);

public class FacturacionElectronicaRegistroArt32GetAllHandler(ConnectionDB _connectionDB)
{
    public async Task<ResultDto<List<RegistroArt32Response>>> HandleAsync(FacturacionElectronicaRegistroArt32GetAllQuery query)
    {
        int pageSize = query.PageSize <= 0 ? 10 : Math.Min(query.PageSize, 100);
        int pageNumber = query.PageNumber <= 0 ? 1 : query.PageNumber;

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
            using var cmd = new NpgsqlCommand(FacturacionElectronicaDb.SqlRegistroArt32GetAll, cn);
            cmd.Parameters.AddWithValue("emisor_rif", (query.EmisorRif ?? string.Empty).Trim());
            cmd.Parameters.AddWithValue("periodo", (query.Periodo ?? string.Empty).Trim());
            cmd.Parameters.AddWithValue("page_size", pageSize);
            cmd.Parameters.AddWithValue("row_offset", (pageNumber - 1) * pageSize);

            var filas = new List<RegistroArt32Response>();
            int totalRegistros = 0;

            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    filas.Add(FacturacionElectronicaDb.MapRegistroArt32(reader));
                    totalRegistros = reader.SafeGetInt32("total_registros");
                }
            }

            int totalPaginas = totalRegistros == 0
                ? 0
                : (int)Math.Ceiling(totalRegistros / (double)pageSize);

            return new ResultDto<List<RegistroArt32Response>>(filas)
            {
                IsValid = true,
                Message = FacturacionElectronicaDb.MensajeExito,
                Page = pageNumber,
                TotalPage = totalPaginas,
                CantidadRegistros = totalRegistros
            };
        }
        catch (Exception ex)
        {
            return Falla($"Error técnico: {ex.Message}");
        }
    }

    private static ResultDto<List<RegistroArt32Response>> Falla(string mensaje) =>
        new(null!) { Data = null, IsValid = false, Message = mensaje };
}

[ApiController]
[Authorize]
[Route("api/FacturacionElectronica")]
public class FacturacionElectronicaRegistroArt32GetAllController(ConnectionDB _connectionDB) : ControllerBase
{
    [HttpPost]
    [Route("registroArt32GetAll")]
    public async Task<IActionResult> RegistroArt32GetAll(FacturacionElectronicaRegistroArt32GetAllQuery value)
    {
        var handler = new FacturacionElectronicaRegistroArt32GetAllHandler(_connectionDB);
        var result = await handler.HandleAsync(value);

        return Ok(result);
    }
}
