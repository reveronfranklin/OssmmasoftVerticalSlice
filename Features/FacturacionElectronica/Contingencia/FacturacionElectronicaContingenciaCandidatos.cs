using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.FacturacionElectronica;

// Fase C (TC.1) - los documentos que pueden regularizar una contingencia, para
// elegirlos de una lista en vez de escribir el id a mano.
//
// Recibe la contingencia y no el emisor: el emisor y la fecha del documento
// fisico salen de la base, no de lo que diga quien llama. Las condiciones son
// las mismas que valida contingenciaConciliar (ver SqlContingenciaCandidatos),
// para que la lista no ofrezca nada que despues se rechace.
//
// SearchText filtra por numeracion (con o sin serie), numero de control, nombre o
// RIF del adquiriente. Vacio trae todos.
public record FacturacionElectronicaContingenciaCandidatosQuery(
    long ContingenciaId,
    string SearchText = "",
    int PageSize = 50,
    int PageNumber = 1);

public class FacturacionElectronicaContingenciaCandidatosHandler(ConnectionDB _connectionDB)
{
    public async Task<ResultDto<List<ContingenciaCandidatoResponse>>> HandleAsync(
        FacturacionElectronicaContingenciaCandidatosQuery query)
    {
        // Nivel 1 - validacion previa.
        if (query.ContingenciaId <= 0)
        {
            return Falla("La notificación de contingencia es obligatoria.");
        }

        int pageSize = query.PageSize <= 0 ? 50 : Math.Min(query.PageSize, 100);
        int pageNumber = query.PageNumber <= 0 ? 1 : query.PageNumber;
        string buscar = (query.SearchText ?? string.Empty).Trim();

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
            // Los mismos dos rechazos que contingenciaConciliar: sin ellos, una
            // contingencia inexistente o ya conciliada devolveria una lista vacia
            // que no explica por que.
            using (var cmd = new NpgsqlCommand(FacturacionElectronicaDb.SqlContingenciaPorId, cn))
            {
                cmd.Parameters.AddWithValue("id", query.ContingenciaId);

                using var reader = await cmd.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                {
                    return Falla($"No existe una notificación de contingencia con el identificador {query.ContingenciaId}.");
                }

                if (!reader.IsDBNull(reader.GetOrdinal("conciliado_en")))
                {
                    return Falla("Esa notificación ya está conciliada.");
                }
            }

            var filas = new List<ContingenciaCandidatoResponse>();
            int totalRegistros = 0;

            using (var cmd = new NpgsqlCommand(FacturacionElectronicaDb.SqlContingenciaCandidatos, cn))
            {
                cmd.Parameters.AddWithValue("contingencia_id", query.ContingenciaId);
                cmd.Parameters.AddWithValue("buscar", buscar);
                cmd.Parameters.AddWithValue("page_size", pageSize);
                cmd.Parameters.AddWithValue("row_offset", (pageNumber - 1) * pageSize);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    string tipo = reader.SafeGetString("tipo_documento");
                    var emitidoEn = reader.GetDateTime(reader.GetOrdinal("emitido_en"));

                    filas.Add(new ContingenciaCandidatoResponse(
                        reader.SafeGetInt64("id"),
                        tipo,
                        FacturaFormato.Denominacion(tipo),
                        FacturaFormato.NumeracionConSerie(reader.SafeGetString("serie"), reader.SafeGetString("numeracion")),
                        reader.SafeGetString("numero_control"),
                        FacturaFormato.HoraVenezuela(emitidoEn).ToString("dd/MM/yyyy HH:mm"),
                        reader.SafeGetString("adq_nombre"),
                        reader.SafeGetString("adq_rif"),
                        reader.SafeGetDecimal("total_general"),
                        reader.SafeGetString("moneda")));
                    totalRegistros = reader.SafeGetInt32("total_registros");
                }
            }

            int totalPaginas = totalRegistros == 0
                ? 0
                : (int)Math.Ceiling(totalRegistros / (double)pageSize);

            return new ResultDto<List<ContingenciaCandidatoResponse>>(filas)
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

    private static ResultDto<List<ContingenciaCandidatoResponse>> Falla(string mensaje) =>
        new(null!) { Data = null, IsValid = false, Message = mensaje };
}

[ApiController]
[Authorize]
[Route("api/FacturacionElectronica")]
public class FacturacionElectronicaContingenciaCandidatosController(ConnectionDB _connectionDB) : ControllerBase
{
    [HttpPost]
    [Route("contingenciaDocumentosCandidatos")]
    public async Task<IActionResult> ContingenciaDocumentosCandidatos(FacturacionElectronicaContingenciaCandidatosQuery value)
    {
        var handler = new FacturacionElectronicaContingenciaCandidatosHandler(_connectionDB);
        var result = await handler.HandleAsync(value);

        return Ok(result);
    }
}
