using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.FacturacionElectronica;

public record FacturacionElectronicaEmisorGetAllQuery(
    int PageSize = 10,
    int PageNumber = 1,
    string SearchText = "");

public class FacturacionElectronicaEmisorGetAllHandler(ConnectionDB _connectionDB)
{
    public async Task<ResultDto<List<EmisorResponse>>> HandleAsync(FacturacionElectronicaEmisorGetAllQuery query)
    {
        int pageSize = query.PageSize <= 0 ? 10 : Math.Min(query.PageSize, 100);
        int pageNumber = query.PageNumber <= 0 ? 1 : query.PageNumber;
        string busqueda = (query.SearchText ?? string.Empty).Trim();

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
            using var cmd = new NpgsqlCommand(FacturacionElectronicaDb.SqlEmisorGetAll, cn);
            cmd.Parameters.AddWithValue("search", busqueda);
            cmd.Parameters.AddWithValue("like", $"%{busqueda}%");
            cmd.Parameters.AddWithValue("page_size", pageSize);
            cmd.Parameters.AddWithValue("row_offset", (pageNumber - 1) * pageSize);

            var filas = new List<EmisorResponse>();
            int totalRegistros = 0;

            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    filas.Add(FacturacionElectronicaDb.MapEmisor(reader));
                    totalRegistros = reader.SafeGetInt32("total_registros");
                }
            }

            int totalPaginas = totalRegistros == 0
                ? 0
                : (int)Math.Ceiling(totalRegistros / (double)pageSize);

            return new ResultDto<List<EmisorResponse>>(filas)
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

    private static ResultDto<List<EmisorResponse>> Falla(string mensaje) =>
        new(null!) { Data = null, IsValid = false, Message = mensaje };
}

[ApiController]
[Authorize]
[Route("api/FacturacionElectronica")]
public class FacturacionElectronicaEmisorGetAllController(ConnectionDB _connectionDB) : ControllerBase
{
    [HttpPost]
    [Route("GetAll")]
    public async Task<IActionResult> GetAll(FacturacionElectronicaEmisorGetAllQuery value)
    {
        var handler = new FacturacionElectronicaEmisorGetAllHandler(_connectionDB);
        var result = await handler.HandleAsync(value);

        return Ok(result);
    }
}
