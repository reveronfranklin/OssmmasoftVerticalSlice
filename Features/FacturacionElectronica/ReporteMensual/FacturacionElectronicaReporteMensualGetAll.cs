using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.FacturacionElectronica;

// Listado de periodos mensuales del Art. 29.7.
//
// Es la pantalla con la que ACT-4 tiene que ver de un vistazo si algun periodo
// quedo sin reportar. Las filas existen desde antes de que haya algo que
// reportar, asi que un periodo omitido aparece solo: no hay que salir a
// calcular que meses faltan.
public record FacturacionElectronicaReporteMensualGetAllQuery(
    string Estado = "",
    int PageSize = 24,
    int PageNumber = 1);

public class FacturacionElectronicaReporteMensualGetAllHandler(ConnectionDB _connectionDB)
{
    public async Task<ResultDto<List<ReporteMensualResponse>>> HandleAsync(FacturacionElectronicaReporteMensualGetAllQuery query)
    {
        int pageSize = query.PageSize <= 0 ? 24 : Math.Min(query.PageSize, 100);
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
            using var cmd = new NpgsqlCommand(FacturacionElectronicaDb.SqlReporteGetAll, cn);
            cmd.Parameters.AddWithValue("estado", (query.Estado ?? string.Empty).Trim());
            cmd.Parameters.AddWithValue("page_size", pageSize);
            cmd.Parameters.AddWithValue("row_offset", (pageNumber - 1) * pageSize);

            var filas = new List<ReporteMensualResponse>();
            int totalRegistros = 0;
            int vencidos = 0;

            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var fila = FacturacionElectronicaDb.MapReporteMensual(reader);
                    filas.Add(fila);
                    totalRegistros = reader.SafeGetInt32("total_registros");

                    if (fila.Vencido)
                    {
                        vencidos++;
                    }
                }
            }

            int totalPaginas = totalRegistros == 0
                ? 0
                : (int)Math.Ceiling(totalRegistros / (double)pageSize);

            return new ResultDto<List<ReporteMensualResponse>>(filas)
            {
                IsValid = true,
                Message = FacturacionElectronicaDb.MensajeExito,
                Page = pageNumber,
                TotalPage = totalPaginas,
                CantidadRegistros = totalRegistros,

                // Los vencidos viajan aparte para que la pantalla pueda avisar sin
                // recorrer las filas. Dos en un ano calendario son causal de
                // revocatoria (Art. 34.3), asi que el numero importa por si mismo.
                Total1 = vencidos
            };
        }
        catch (Exception ex)
        {
            return Falla($"Error técnico: {ex.Message}");
        }
    }

    private static ResultDto<List<ReporteMensualResponse>> Falla(string mensaje) =>
        new(null!) { Data = null, IsValid = false, Message = mensaje };
}

[ApiController]
[Authorize]
[Route("api/FacturacionElectronica")]
public class FacturacionElectronicaReporteMensualGetAllController(ConnectionDB _connectionDB) : ControllerBase
{
    [HttpPost]
    [Route("reporteMensualGetAll")]
    public async Task<IActionResult> ReporteMensualGetAll(FacturacionElectronicaReporteMensualGetAllQuery value)
    {
        var handler = new FacturacionElectronicaReporteMensualGetAllHandler(_connectionDB);
        var result = await handler.HandleAsync(value);

        return Ok(result);
    }
}
