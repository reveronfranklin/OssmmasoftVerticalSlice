using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.FacturacionElectronica;

// Asignacion del numero de control. Art. 30 de la Providencia SNAT/2024/000102.
//
// DocumentoId es opcional: en la Fase 2 el Rol B todavia no existe, asi que se
// puede asignar sin documento. Cuando viene, la operacion es idempotente, y esa
// idempotencia es la defensa directa contra INV-1 (Art. 34.2, dos numeros de
// control distintos para un mismo documento, causal de revocatoria).
public record FacturacionElectronicaNumeroControlAsignarCommand(
    long EmisorId,
    string TipoDocumento,
    long DocumentoId,
    string UsuarioIns);

public class FacturacionElectronicaNumeroControlAsignarHandler(ConnectionDB _connectionDB)
{
    public async Task<ResultDto<NumeroControlResponse>> HandleAsync(FacturacionElectronicaNumeroControlAsignarCommand command)
    {
        // Nivel 1 - validacion previa.
        if (command.EmisorId <= 0)
        {
            return Falla("El emisor es obligatorio.");
        }

        string tipoDocumento = (command.TipoDocumento ?? string.Empty).Trim().ToLowerInvariant();

        if (!FacturacionElectronicaDb.TiposDocumento.Contains(tipoDocumento))
        {
            return Falla("El tipo de documento debe ser factura, débito, crédito o entrega.");
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
            // Idempotencia, camino rapido: si el documento ya tiene numero, se
            // devuelve ese y no se abre transaccion ni se toca el contador.
            if (command.DocumentoId > 0)
            {
                var yaAsignado = await BuscarPorDocumentoAsync(cn, command.DocumentoId);

                if (yaAsignado is not null)
                {
                    return Exito(yaAsignado);
                }
            }

            using var tx = await cn.BeginTransactionAsync();

            // El emisor tiene que existir y estar activo. Se comprueba dentro de
            // la transaccion y no antes, para que no cambie a mitad de camino.
            string estado = await LeerEstadoEmisorAsync(cn, tx, command.EmisorId);

            if (string.IsNullOrEmpty(estado))
            {
                await tx.RollbackAsync();

                return Falla($"No existe un emisor con el identificador {command.EmisorId}.");
            }

            if (estado != "activo")
            {
                await tx.RollbackAsync();

                return Falla("El emisor está inactivo: no se le pueden asignar números de control.");
            }

            // Aqui esta el corazon de INV-1. Se bloquea la fila del contador de
            // este emisor y cualquier otra peticion para el MISMO emisor espera
            // aqui. No hay SELECT MAX() ni contador en memoria: el valor siguiente
            // sale de una fila bloqueada, y si aun asi se colara un duplicado, los
            // dos UNIQUE de FED_NUM_CONTROL lo rechazan.
            string identificadorActual;
            int secuencialActual;

            using (var cmdBloqueo = new NpgsqlCommand(FacturacionElectronicaDb.SqlContadorBloquear, cn, tx))
            {
                cmdBloqueo.Parameters.AddWithValue("emisor_id", command.EmisorId);

                using var reader = await cmdBloqueo.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                {
                    return await FallaEnTransaccionAsync(tx, "No se pudo obtener el contador del emisor.");
                }

                identificadorActual = reader.SafeGetString("identificador");
                secuencialActual = reader.SafeGetInt32("secuencial");
            }

            // Decision D-2: el identificador rota al agotarse el secuencial.
            if (!FacturacionElectronicaDb.CalcularSiguiente(
                    identificadorActual, secuencialActual, out string identificador, out int secuencial))
            {
                return await FallaEnTransaccionAsync(
                    tx,
                    "La secuencia de números de control del emisor se agotó: se consumieron "
                    + "los 99 identificadores de dos dígitos.");
            }

            using (var cmdContador = new NpgsqlCommand(FacturacionElectronicaDb.SqlContadorActualizar, cn, tx))
            {
                cmdContador.Parameters.AddWithValue("identificador", identificador);
                cmdContador.Parameters.AddWithValue("secuencial", secuencial);
                cmdContador.Parameters.AddWithValue("emisor_id", command.EmisorId);

                await cmdContador.ExecuteNonQueryAsync();
            }

            long id;
            DateTime fechaAsignacion;

            using (var cmdInsert = new NpgsqlCommand(FacturacionElectronicaDb.SqlNumControlInsert, cn, tx))
            {
                cmdInsert.Parameters.AddWithValue("emisor_id", command.EmisorId);
                cmdInsert.Parameters.AddWithValue("documento_id", FacturacionElectronicaDb.DbValueId(command.DocumentoId));
                cmdInsert.Parameters.AddWithValue("identificador", identificador);
                cmdInsert.Parameters.AddWithValue("secuencial", secuencial);
                cmdInsert.Parameters.AddWithValue("tipo_documento", tipoDocumento);
                cmdInsert.Parameters.AddWithValue("usuario_ins", FacturacionElectronicaDb.DbValue(command.UsuarioIns));

                using var reader = await cmdInsert.ExecuteReaderAsync();
                await reader.ReadAsync();

                id = reader.SafeGetInt64("id");
                fechaAsignacion = reader.GetDateTime(reader.GetOrdinal("fecha_asignacion"));
            }

            await tx.CommitAsync();

            return Exito(new NumeroControlResponse(
                id,
                command.EmisorId,
                command.DocumentoId,
                identificador,
                secuencial,
                FacturacionElectronicaDb.FormatearNumeroControl(identificador, secuencial),
                FacturacionElectronicaDb.FormatearNumeroControlTexto(identificador, secuencial),
                tipoDocumento,
                fechaAsignacion.ToString("dd/MM/yyyy HH:mm:ss"),
                YaExistia: false));
        }
        catch (NpgsqlException ex) when (FacturacionElectronicaDb.EsClaveDuplicada(ex)
                                         && FacturacionElectronicaDb.NombreRestriccion(ex)
                                            == FacturacionElectronicaDb.RestriccionDocumentoUnico)
        {
            // Carrera perdida: entre el chequeo de idempotencia y el insert, otra
            // peticion asigno el numero de este mismo documento. El UNIQUE la
            // freno, que es para lo que esta. Se devuelve el numero que quedo y no
            // uno nuevo: dos numeros para un documento es exactamente INV-1.
            var existente = await BuscarPorDocumentoAsync(cn, command.DocumentoId);

            return existente is not null
                ? Exito(existente)
                : Falla("El documento ya tiene un número de control asignado.");
        }
        catch (Exception ex)
        {
            return Falla($"Error técnico: {ex.Message}");
        }
    }

    private static async Task<NumeroControlResponse?> BuscarPorDocumentoAsync(NpgsqlConnection cn, long documentoId)
    {
        using var cmd = new NpgsqlCommand(FacturacionElectronicaDb.SqlNumControlPorDocumento, cn);
        cmd.Parameters.AddWithValue("documento_id", documentoId);

        using var reader = await cmd.ExecuteReaderAsync();

        return await reader.ReadAsync()
            ? FacturacionElectronicaDb.MapNumeroControl(reader, yaExistia: true)
            : null;
    }

    private static async Task<string> LeerEstadoEmisorAsync(NpgsqlConnection cn, NpgsqlTransaction tx, long emisorId)
    {
        using var cmd = new NpgsqlCommand(FacturacionElectronicaDb.SqlEmisorEstado, cn, tx);
        cmd.Parameters.AddWithValue("emisor_id", emisorId);

        object? estado = await cmd.ExecuteScalarAsync();

        return estado is null or DBNull ? string.Empty : Convert.ToString(estado) ?? string.Empty;
    }

    private static async Task<ResultDto<NumeroControlResponse>> FallaEnTransaccionAsync(NpgsqlTransaction tx, string mensaje)
    {
        await tx.RollbackAsync();

        return Falla(mensaje);
    }

    private static ResultDto<NumeroControlResponse> Exito(NumeroControlResponse dato) =>
        new(dato) { IsValid = true, Message = FacturacionElectronicaDb.MensajeExito };

    private static ResultDto<NumeroControlResponse> Falla(string mensaje) =>
        new(null!) { Data = null, IsValid = false, Message = mensaje };
}

[ApiController]
[Authorize]
[Route("api/FacturacionElectronica")]
public class FacturacionElectronicaNumeroControlAsignarController(ConnectionDB _connectionDB) : ControllerBase
{
    [HttpPost]
    [Route("asignarNumeroControl")]
    public async Task<IActionResult> AsignarNumeroControl(FacturacionElectronicaNumeroControlAsignarCommand value)
    {
        var handler = new FacturacionElectronicaNumeroControlAsignarHandler(_connectionDB);
        var result = await handler.HandleAsync(value);

        return Ok(result);
    }
}
