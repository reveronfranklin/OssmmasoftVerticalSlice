using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.FacturacionElectronica;

// Consulta de numeros de control asignados. Es la lectura del registro del
// Art. 32: por emisor -numeral 1- y por fecha de asignacion -numeral 2-.
//
// EmisorId en 0 significa "todos los emisores". Las fechas nulas significan "sin
// limite" por ese extremo.
public record FacturacionElectronicaNumeroControlGetAllQuery(
    long EmisorId = 0,
    DateTime? FechaDesde = null,
    DateTime? FechaHasta = null,
    int PageSize = 10,
    int PageNumber = 1);

public class FacturacionElectronicaNumeroControlGetAllHandler(ConnectionDB _connectionDB)
{
    public async Task<ResultDto<List<NumeroControlListaResponse>>> HandleAsync(FacturacionElectronicaNumeroControlGetAllQuery query)
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
            using var cmd = new NpgsqlCommand(FacturacionElectronicaDb.SqlNumControlGetAll, cn);
            cmd.Parameters.AddWithValue("emisor_id", query.EmisorId < 0 ? 0 : query.EmisorId);
            cmd.Parameters.AddWithValue("fecha_desde", FacturacionElectronicaDb.DbValueFecha(query.FechaDesde));
            cmd.Parameters.AddWithValue("fecha_hasta", FacturacionElectronicaDb.DbValueFecha(query.FechaHasta));
            cmd.Parameters.AddWithValue("page_size", pageSize);
            cmd.Parameters.AddWithValue("row_offset", (pageNumber - 1) * pageSize);

            var filas = new List<NumeroControlListaResponse>();
            int totalRegistros = 0;

            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    filas.Add(FacturacionElectronicaDb.MapNumeroControlLista(reader));
                    totalRegistros = reader.SafeGetInt32("total_registros");
                }
            }

            int totalPaginas = totalRegistros == 0
                ? 0
                : (int)Math.Ceiling(totalRegistros / (double)pageSize);

            return new ResultDto<List<NumeroControlListaResponse>>(filas)
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

    private static ResultDto<List<NumeroControlListaResponse>> Falla(string mensaje) =>
        new(null!) { Data = null, IsValid = false, Message = mensaje };
}

[ApiController]
[Authorize]
[Route("api/FacturacionElectronica")]
public class FacturacionElectronicaNumeroControlGetAllController(ConnectionDB _connectionDB) : ControllerBase
{
    [HttpPost]
    [Route("numeroControlGetAll")]
    public async Task<IActionResult> NumeroControlGetAll(FacturacionElectronicaNumeroControlGetAllQuery value)
    {
        var handler = new FacturacionElectronicaNumeroControlGetAllHandler(_connectionDB);
        var result = await handler.HandleAsync(value);

        return Ok(result);
    }
}
