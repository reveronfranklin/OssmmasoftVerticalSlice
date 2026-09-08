using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.FacturacionElectronica;

// Listado de comprobantes de retencion. Fase 6B, T6B.6.
//
// Se filtra por PERIODO y no solo por emisor porque este documento se consulta
// por periodo de imposicion: es el eje del 11.7 y la forma en que el agente de
// retencion rinde cuentas. Por eso el periodo se guarda en columna propia
// aunque este embebido en la numeracion -filtrar por prefijo de un CHAR(14) no
// usa indice-.
public record RetencionGetAllQuery(
    long EmisorId = 0,
    string Periodo = "",
    int PageSize = 10,
    int PageNumber = 1);

// Lo que ve el listado. NO tiene NumeroControl, y no es una omision del
// listado sino del documento: el Art. 11 no remite a los numerales 4 y 5 del
// Art. 7. Su identificacion es la numeracion de catorce caracteres del 11.1.
public record RetencionListaResponse(
    long Id,
    long EmisorId,
    string Numeracion,
    string Periodo,
    string PeriodoFormato,
    string EmitidoEn,
    string FechaEmision8d,
    string AgenteRif,
    string AgenteRazonSocial,
    string ProveedorRif,
    string ProveedorRazonSocial,
    decimal TotalDocumentos,
    decimal TotalBase,
    decimal TotalImpuesto,
    decimal TotalRetenido,
    int CantidadDocumentos,
    bool EsPrueba);

public class FacturacionElectronicaRetencionGetAllHandler(ConnectionDB _connectionDB)
{
    public async Task<ResultDto<List<RetencionListaResponse>>> HandleAsync(RetencionGetAllQuery query)
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
            using var cmd = new NpgsqlCommand(RetencionDb.SqlRetencionGetAll, cn);
            cmd.Parameters.AddWithValue("emisor_id", query.EmisorId < 0 ? 0 : query.EmisorId);
            cmd.Parameters.AddWithValue("periodo", (query.Periodo ?? string.Empty).Trim());
            cmd.Parameters.AddWithValue("page_size", pageSize);
            cmd.Parameters.AddWithValue("row_offset", (pageNumber - 1) * pageSize);

            var filas = new List<RetencionListaResponse>();
            int totalRegistros = 0;
            int dePrueba = 0;
            decimal retenidoDeLaPagina = 0;

            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var emitidoEn = reader.GetDateTime(reader.GetOrdinal("emitido_en"));
                    string periodo = reader.SafeGetString("periodo");
                    bool esPrueba = reader.GetBoolean(reader.GetOrdinal("es_prueba"));
                    decimal retenido = reader.SafeGetDecimal("total_retenido");

                    filas.Add(new RetencionListaResponse(
                        reader.SafeGetInt64("id"),
                        reader.SafeGetInt64("emisor_id"),
                        reader.SafeGetString("numeracion"),
                        periodo,
                        RetencionDb.FormatearPeriodo(periodo),
                        emitidoEn.ToString("dd/MM/yyyy HH:mm"),
                        FacturaFormato.FechaOchoDigitos(emitidoEn),
                        reader.SafeGetString("agente_rif"),
                        reader.SafeGetString("agente_razon_social"),
                        reader.SafeGetString("proveedor_rif"),
                        reader.SafeGetString("proveedor_razon_social"),
                        reader.SafeGetDecimal("total_documentos"),
                        reader.SafeGetDecimal("total_base"),
                        reader.SafeGetDecimal("total_impuesto"),
                        retenido,
                        (int)reader.SafeGetInt64("cantidad_documentos"),
                        esPrueba));

                    totalRegistros = reader.SafeGetInt32("total_registros");
                    retenidoDeLaPagina += retenido;

                    if (esPrueba)
                    {
                        dePrueba++;
                    }
                }
            }

            int totalPaginas = totalRegistros == 0
                ? 0
                : (int)Math.Ceiling(totalRegistros / (double)pageSize);

            return new ResultDto<List<RetencionListaResponse>>(filas)
            {
                IsValid = true,
                Message = FacturacionElectronicaDb.MensajeExito,
                Page = pageNumber,
                TotalPage = totalPaginas,
                CantidadRegistros = totalRegistros,

                // Mismo criterio que el listado de documentos: cuantos de esta
                // pagina son de prueba. Hasta que el SENIAT autorice, todos.
                Total1 = dePrueba,

                // Lo retenido en la pagina. Es la cifra que el agente busca
                // cuando abre esta pantalla por periodo.
                Total2 = retenidoDeLaPagina
            };
        }
        catch (Exception ex)
        {
            return Falla($"Error técnico: {ex.Message}");
        }
    }

    private static ResultDto<List<RetencionListaResponse>> Falla(string mensaje) =>
        new(null!) { Data = null, IsValid = false, Message = mensaje };
}

[ApiController]
[Authorize]
[Route("api/FacturacionElectronica")]
public class FacturacionElectronicaRetencionGetAllController(ConnectionDB _connectionDB) : ControllerBase
{
    [HttpPost]
    [Route("retencionGetAll")]
    public async Task<IActionResult> RetencionGetAll(RetencionGetAllQuery value)
    {
        var handler = new FacturacionElectronicaRetencionGetAllHandler(_connectionDB);
        var result = await handler.HandleAsync(value);

        return Ok(result);
    }
}
