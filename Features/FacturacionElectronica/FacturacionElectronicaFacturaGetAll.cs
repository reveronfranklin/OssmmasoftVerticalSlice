using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.FacturacionElectronica;

// Listado de documentos emitidos. Alimenta la pantalla de T4.10 y es la base de
// la superficie de consulta del SENIAT (Art. 18.7, diez años).
//
// El numero de control llega por LEFT JOIN y no por INNER: un documento sin
// numero de control no deberia existir -la emision los crea juntos y en una
// transaccion- pero si alguna vez existiera, esconderlo del listado seria
// esconder justo la anomalia que hay que ver.
public record FacturaGetAllQuery(
    long EmisorId = 0,
    string TipoDocumento = "",
    int PageSize = 10,
    int PageNumber = 1);

public record FacturaListaResponse(
    long Id,
    long EmisorId,
    string TipoDocumento,
    string Denominacion,
    string Serie,
    string Numeracion,
    string NumeracionConSerie,
    string NumeroControl,
    string EmitidoEn,
    string FechaEmision8d,
    string HoraEmision,
    string EmisorRif,
    string EmisorRazonSocial,
    string AdqNombre,
    string AdqRif,
    decimal TotalExento,
    decimal TotalBase,
    decimal TotalIva,
    decimal TotalGeneral,
    bool EsPrueba);

public class FacturacionElectronicaFacturaGetAllHandler(ConnectionDB _connectionDB)
{
    public async Task<ResultDto<List<FacturaListaResponse>>> HandleAsync(FacturaGetAllQuery query)
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
            using var cmd = new NpgsqlCommand(FacturaDb.SqlDocumentoGetAll, cn);
            cmd.Parameters.AddWithValue("emisor_id", query.EmisorId < 0 ? 0 : query.EmisorId);
            cmd.Parameters.AddWithValue("tipo_documento", (query.TipoDocumento ?? string.Empty).Trim());
            cmd.Parameters.AddWithValue("page_size", pageSize);
            cmd.Parameters.AddWithValue("row_offset", (pageNumber - 1) * pageSize);

            var filas = new List<FacturaListaResponse>();
            int totalRegistros = 0;
            int dePrueba = 0;

            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var emitidoEn = reader.GetDateTime(reader.GetOrdinal("emitido_en"));
                    string tipo = reader.SafeGetString("tipo_documento");
                    string serie = reader.SafeGetString("serie");
                    string numeracion = reader.SafeGetString("numeracion");
                    bool esPrueba = reader.GetBoolean(reader.GetOrdinal("es_prueba"));

                    filas.Add(new FacturaListaResponse(
                        reader.SafeGetInt64("id"),
                        reader.SafeGetInt64("emisor_id"),
                        tipo,
                        FacturaFormato.Denominacion(tipo),
                        serie,
                        numeracion,
                        FacturaFormato.NumeracionConSerie(serie, numeracion),
                        reader.SafeGetString("numero_control"),
                        emitidoEn.ToString("dd/MM/yyyy HH:mm"),
                        FacturaFormato.FechaOchoDigitos(emitidoEn),
                        FacturaFormato.HoraConMeridiano(emitidoEn),
                        reader.SafeGetString("emisor_rif"),
                        reader.SafeGetString("emisor_razon_social"),
                        reader.SafeGetString("adq_nombre"),
                        reader.SafeGetString("adq_rif"),
                        reader.SafeGetDecimal("total_exento"),
                        reader.SafeGetDecimal("total_base"),
                        reader.SafeGetDecimal("total_iva"),
                        reader.SafeGetDecimal("total_general"),
                        esPrueba));

                    totalRegistros = reader.SafeGetInt32("total_registros");

                    if (esPrueba)
                    {
                        dePrueba++;
                    }
                }
            }

            int totalPaginas = totalRegistros == 0
                ? 0
                : (int)Math.Ceiling(totalRegistros / (double)pageSize);

            return new ResultDto<List<FacturaListaResponse>>(filas)
            {
                IsValid = true,
                Message = FacturacionElectronicaDb.MensajeExito,
                Page = pageNumber,
                TotalPage = totalPaginas,
                CantidadRegistros = totalRegistros,

                // Cuantos de esta pagina son de prueba. La pantalla lo necesita
                // para avisar sin recorrer filas: hasta que el SENIAT autorice,
                // TODOS lo son, y eso no puede pasar desapercibido.
                Total1 = dePrueba
            };
        }
        catch (Exception ex)
        {
            return Falla($"Error técnico: {ex.Message}");
        }
    }

    private static ResultDto<List<FacturaListaResponse>> Falla(string mensaje) =>
        new(null!) { Data = null, IsValid = false, Message = mensaje };
}

[ApiController]
[Authorize]
[Route("api/FacturacionElectronica")]
public class FacturacionElectronicaFacturaGetAllController(ConnectionDB _connectionDB) : ControllerBase
{
    [HttpPost]
    [Route("facturaGetAll")]
    public async Task<IActionResult> FacturaGetAll(FacturaGetAllQuery value)
    {
        var handler = new FacturacionElectronicaFacturaGetAllHandler(_connectionDB);
        var result = await handler.HandleAsync(value);

        return Ok(result);
    }
}
