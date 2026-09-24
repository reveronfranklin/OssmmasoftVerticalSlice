using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.FacturacionElectronica;

// Fase 8 (T8.5) - conciliar una notificacion de contingencia con el documento
// que la regulariza en el sistema (Art. 16, paso 2: "registrar en el sistema
// los comprobantes fisicos emitidos").
//
// CONCILIAR ES ASOCIAR, NO EMITIR. Este endpoint no crea ningun FED_DOCUMENTO:
// asocia una notificacion ya existente con un documento que el emisor ya
// registro (o no) por su propia via. Completar CONCILIADO_EN, DOCUMENTO_ID y
// USUARIO_CONCILIA es agregar un dato pendiente, no enmendar la notificacion
// -mismo criterio que ENTREGADO_EN en FED_RETENCION.
public record FacturacionElectronicaContingenciaConciliarCommand(
    long ContingenciaId,
    long DocumentoId,
    string UsuarioConcilia = "");

public class FacturacionElectronicaContingenciaConciliarHandler(ConnectionDB _connectionDB)
{
    public async Task<ResultDto<bool>> HandleAsync(FacturacionElectronicaContingenciaConciliarCommand command)
    {
        // Nivel 1 - validacion previa.
        if (command.ContingenciaId <= 0)
        {
            return Falla("La notificación de contingencia es obligatoria.");
        }

        if (command.DocumentoId <= 0)
        {
            return Falla("El documento con el que se concilia es obligatorio.");
        }

        // Sin este dato el UPDATE viola el CHECK que exige CONCILIADO_EN,
        // DOCUMENTO_ID y USUARIO_CONCILIA juntos, y eso llegaria como un
        // error de SQL crudo en vez de un mensaje de negocio.
        if (string.IsNullOrWhiteSpace(command.UsuarioConcilia))
        {
            return Falla("El usuario que concilia es obligatorio.");
        }

        // Tope de columna (VARCHAR(50)): igual criterio que en Notificar.
        if (command.UsuarioConcilia.Trim().Length > 50)
        {
            return Falla("El usuario no puede superar los 50 caracteres.");
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
            long emisorId;

            using (var cmd = new NpgsqlCommand(FacturacionElectronicaDb.SqlContingenciaPorId, cn))
            {
                cmd.Parameters.AddWithValue("id", command.ContingenciaId);

                using var reader = await cmd.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                {
                    return Falla($"No existe una notificación de contingencia con el identificador {command.ContingenciaId}.");
                }

                if (!reader.IsDBNull(reader.GetOrdinal("conciliado_en")))
                {
                    return Falla("Esa notificación ya está conciliada.");
                }

                emisorId = reader.SafeGetInt64("emisor_id");
            }

            // El documento tiene que ser del mismo emisor: una contingencia no
            // se concilia con el documento de otro (mismo criterio que
            // FacturacionElectronicaNotaCreate para el documento origen).
            using (var cmd = new NpgsqlCommand(FacturacionElectronicaDb.SqlContingenciaDocumentoDeEmisor, cn))
            {
                cmd.Parameters.AddWithValue("documento_id", command.DocumentoId);
                cmd.Parameters.AddWithValue("emisor_id", emisorId);

                object? encontrado = await cmd.ExecuteScalarAsync();

                if (encontrado is null)
                {
                    return Falla("El documento indicado no existe o pertenece a otro emisor.");
                }
            }

            using (var cmd = new NpgsqlCommand(FacturacionElectronicaDb.SqlContingenciaConciliar, cn))
            {
                cmd.Parameters.AddWithValue("id", command.ContingenciaId);
                cmd.Parameters.AddWithValue("documento_id", command.DocumentoId);
                cmd.Parameters.AddWithValue("usuario_concilia", FacturacionElectronicaDb.DbValue(command.UsuarioConcilia));

                using var reader = await cmd.ExecuteReaderAsync();

                if (!await reader.ReadAsync())
                {
                    // Otra peticion la concilio entre la lectura y este UPDATE.
                    return Falla("Esa notificación ya está conciliada.");
                }
            }

            return new ResultDto<bool>(true) { IsValid = true, Message = FacturacionElectronicaDb.MensajeExito };
        }
        catch (Exception ex)
        {
            return Falla($"Error técnico: {ex.Message}");
        }
    }

    private static ResultDto<bool> Falla(string mensaje) =>
        new(false) { IsValid = false, Message = mensaje };
}

[ApiController]
[Authorize]
[Route("api/FacturacionElectronica")]
public class FacturacionElectronicaContingenciaConciliarController(ConnectionDB _connectionDB) : ControllerBase
{
    [HttpPost]
    [Route("contingenciaConciliar")]
    public async Task<IActionResult> ContingenciaConciliar(FacturacionElectronicaContingenciaConciliarCommand value)
    {
        var handler = new FacturacionElectronicaContingenciaConciliarHandler(_connectionDB);
        var result = await handler.HandleAsync(value);

        return Ok(result);
    }
}
