using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.FacturacionElectronica;

// Fase 8 (T8.5, T8.6) - la bandeja de contingencia: lo que un emisor notifico
// (Art. 16) y su estado de conciliacion.
//
// EmisorId en 0 significa "todos los emisores" (D-53: ACT-4 administra la
// cartera completa, no hay control de acceso por emisor que construir aqui).
public record FacturacionElectronicaContingenciaGetAllQuery(
    long EmisorId = 0,
    bool SoloPendientes = false,
    int PageSize = 10,
    int PageNumber = 1);

public class FacturacionElectronicaContingenciaGetAllHandler(ConnectionDB _connectionDB)
{
    public async Task<ResultDto<List<ContingenciaListaResponse>>> HandleAsync(
        FacturacionElectronicaContingenciaGetAllQuery query)
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
            using var cmd = new NpgsqlCommand(FacturacionElectronicaDb.SqlContingenciaGetAll, cn);
            cmd.Parameters.AddWithValue("emisor_id", query.EmisorId < 0 ? 0 : query.EmisorId);
            cmd.Parameters.AddWithValue("solo_pendientes", query.SoloPendientes);
            cmd.Parameters.AddWithValue("page_size", pageSize);
            cmd.Parameters.AddWithValue("row_offset", (pageNumber - 1) * pageSize);

            var filas = new List<ContingenciaListaResponse>();
            int totalRegistros = 0;

            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    filas.Add(FacturacionElectronicaDb.MapContingenciaLista(reader));
                    totalRegistros = reader.SafeGetInt32("total_registros");
                }
            }

            int totalPaginas = totalRegistros == 0
                ? 0
                : (int)Math.Ceiling(totalRegistros / (double)pageSize);

            return new ResultDto<List<ContingenciaListaResponse>>(filas)
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

    private static ResultDto<List<ContingenciaListaResponse>> Falla(string mensaje) =>
        new(null!) { Data = null, IsValid = false, Message = mensaje };
}

[ApiController]
[Authorize]
[Route("api/FacturacionElectronica")]
public class FacturacionElectronicaContingenciaGetAllController(ConnectionDB _connectionDB) : ControllerBase
{
    [HttpPost]
    [Route("contingenciaGetAll")]
    public async Task<IActionResult> ContingenciaGetAll(FacturacionElectronicaContingenciaGetAllQuery value)
    {
        var handler = new FacturacionElectronicaContingenciaGetAllHandler(_connectionDB);
        var result = await handler.HandleAsync(value);

        return Ok(result);
    }
}
