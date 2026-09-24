using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.FacturacionElectronica;

// TM.4, D-54. El cupo de un emisor: cuanto tiene, cuanto le queda y cada carga
// o renovacion, la mas reciente primero. El total y el disponible tambien salen
// en GetAll de emisores; esto agrega el historial.
//
// Es una lectura sin bloqueo: si en este instante se esta emitiendo, el
// disponible puede quedar viejo por un documento. Para mostrar alcanza; quien
// decide si hay cupo es la asignacion, bajo el bloqueo del contador.
public record FacturacionElectronicaEmisorCupoGetAllQuery(long EmisorId);

public class FacturacionElectronicaEmisorCupoGetAllHandler(ConnectionDB _connectionDB)
{
    public async Task<ResultDto<EmisorCupoResponse?>> HandleAsync(FacturacionElectronicaEmisorCupoGetAllQuery query)
    {
        if (query.EmisorId <= 0)
        {
            return Falla("El emisor es obligatorio.");
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
            long consumido = 0;
            bool existe = false;

            using (var cmd = new NpgsqlCommand(SqlEmisorYConsumido, cn))
            {
                cmd.Parameters.AddWithValue("emisor_id", query.EmisorId);

                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    existe = true;
                    consumido = EmisorCupoDb.LeerEnteroNulable(reader, "consumido") ?? 0;
                }
            }

            if (!existe)
            {
                return Falla($"No existe un emisor con el identificador {query.EmisorId}.");
            }

            var (total, baseConsumo) = await EmisorCupoDb.LeerResumenAsync(cn, null, query.EmisorId);

            var cargas = new List<EmisorCupoCargaResponse>();

            using (var cmd = new NpgsqlCommand(EmisorCupoDb.SqlCargas, cn))
            {
                cmd.Parameters.AddWithValue("emisor_id", query.EmisorId);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    cargas.Add(new EmisorCupoCargaResponse(
                        reader.SafeGetInt64("id"),
                        reader.SafeGetInt32("cantidad"),
                        reader.SafeGetString("usuario_ins"),
                        FacturaFormato.HoraVenezuela(reader.GetDateTime(reader.GetOrdinal("fecha_ins"))).ToString("dd/MM/yyyy HH:mm")));
                }
            }

            var cupo = new EmisorCupoResponse(
                query.EmisorId,
                total,
                EmisorCupoDb.Disponible(total, baseConsumo, consumido),
                cargas);

            return new ResultDto<EmisorCupoResponse?>(cupo)
            {
                IsValid = true,
                Message = FacturacionElectronicaDb.MensajeExito,
                CantidadRegistros = cargas.Count
            };
        }
        catch (Exception ex)
        {
            return Falla($"Error técnico: {ex.Message}");
        }
    }

    // El LEFT JOIN distingue "el emisor no existe" -sin fila- de "existe pero
    // nunca recibio un numero" -fila con consumido NULL-.
    private static readonly string SqlEmisorYConsumido = $@"
        SELECT {EmisorCupoDb.SqlConsumido} AS CONSUMIDO
        FROM FED.FED_EMISOR E
        LEFT JOIN FED.FED_EMISOR_CONTADOR CT ON CT.EMISOR_ID = E.ID
        WHERE E.ID = @emisor_id;";

    private static ResultDto<EmisorCupoResponse?> Falla(string mensaje) =>
        new(null) { IsValid = false, Message = mensaje };
}

[ApiController]
[Authorize]
[Route("api/FacturacionElectronica")]
public class FacturacionElectronicaEmisorCupoGetAllController(ConnectionDB _connectionDB) : ControllerBase
{
    [HttpPost]
    [Route("cupoGetAll")]
    public async Task<IActionResult> CupoGetAll(FacturacionElectronicaEmisorCupoGetAllQuery value)
    {
        var handler = new FacturacionElectronicaEmisorCupoGetAllHandler(_connectionDB);
        var result = await handler.HandleAsync(value);

        return Ok(result);
    }
}
