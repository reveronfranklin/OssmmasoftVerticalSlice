using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.FacturacionElectronica;

// TM.4, D-54. Carga o renueva el cupo de documentos de un emisor.
//
// Renovar SUMA una fila a FED_EMISOR_CUPO, no reemplaza la anterior: la tabla es
// de solo insercion, asi que cada renovacion queda trazada con su cantidad, su
// fecha y su usuario. La primera carga de un emisor que ya emitia fija la base:
// lo consumido antes no cuenta contra el cupo.
public record FacturacionElectronicaEmisorCupoRenovarCommand(
    long EmisorId,
    int Cantidad,
    string UsuarioIns);

public class FacturacionElectronicaEmisorCupoRenovarHandler(ConnectionDB _connectionDB)
{
    public async Task<ResultDto<EmisorCupoResponse?>> HandleAsync(FacturacionElectronicaEmisorCupoRenovarCommand command)
    {
        // Nivel 1 - validacion previa.
        if (command.EmisorId <= 0)
        {
            return Falla("El emisor es obligatorio.");
        }

        if (command.Cantidad is < 1 or > FacturacionElectronicaDb.SecuencialMaximo)
        {
            return Falla($"La cantidad debe estar entre 1 y {EmisorCupoDb.CantidadMaximaTexto}.");
        }

        string? error = FacturacionElectronicaDb.ValidarTexto(command.UsuarioIns, "El usuario", 50);

        if (error is not null)
        {
            return Falla(error);
        }

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
            using var tx = await cn.BeginTransactionAsync();

            // Renovar a un emisor inactivo se permite: es una decision comercial,
            // y deja el cupo listo para cuando se reactive. Solo tiene que existir.
            using (var cmdEmisor = new NpgsqlCommand(FacturacionElectronicaDb.SqlEmisorEstado, cn, tx))
            {
                cmdEmisor.Parameters.AddWithValue("emisor_id", command.EmisorId);

                if (await cmdEmisor.ExecuteScalarAsync() is null)
                {
                    await tx.RollbackAsync();

                    return Falla($"No existe un emisor con el identificador {command.EmisorId}.");
                }
            }

            // El MISMO bloqueo que la asignacion. Asi el consumido que se guarda
            // como base es exacto aunque haya emisiones de este emisor en curso:
            // o terminaron antes y cuentan, o esperan a que esta confirme.
            long consumido;

            using (var cmdBloqueo = new NpgsqlCommand(FacturacionElectronicaDb.SqlContadorBloquear, cn, tx))
            {
                cmdBloqueo.Parameters.AddWithValue("emisor_id", command.EmisorId);

                using var reader = await cmdBloqueo.ExecuteReaderAsync();
                await reader.ReadAsync();

                consumido = EmisorCupoDb.Consumido(
                    reader.SafeGetString("identificador"), reader.SafeGetInt32("secuencial"));
            }

            using (var cmdCupo = new NpgsqlCommand(EmisorCupoDb.SqlInsertar, cn, tx))
            {
                cmdCupo.Parameters.AddWithValue("emisor_id", command.EmisorId);
                cmdCupo.Parameters.AddWithValue("cantidad", command.Cantidad);
                cmdCupo.Parameters.AddWithValue("consumido", consumido);
                cmdCupo.Parameters.AddWithValue("usuario_ins", command.UsuarioIns.Trim());

                await cmdCupo.ExecuteNonQueryAsync();
            }

            var (total, baseConsumo) = await EmisorCupoDb.LeerResumenAsync(cn, tx, command.EmisorId);

            await tx.CommitAsync();

            var cupo = new EmisorCupoResponse(
                command.EmisorId,
                total,
                EmisorCupoDb.Disponible(total, baseConsumo, consumido),
                []);

            return new ResultDto<EmisorCupoResponse?>(cupo)
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

    private static ResultDto<EmisorCupoResponse?> Falla(string mensaje) =>
        new(null) { IsValid = false, Message = mensaje };
}

[ApiController]
[Authorize]
[Route("api/FacturacionElectronica")]
public class FacturacionElectronicaEmisorCupoRenovarController(ConnectionDB _connectionDB) : ControllerBase
{
    [HttpPost]
    [Route("cupoRenovar")]
    public async Task<IActionResult> CupoRenovar(FacturacionElectronicaEmisorCupoRenovarCommand value)
    {
        var handler = new FacturacionElectronicaEmisorCupoRenovarHandler(_connectionDB);
        var result = await handler.HandleAsync(value);

        return Ok(result);
    }
}
