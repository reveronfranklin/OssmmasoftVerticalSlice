using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.FacturacionElectronica;

// Fase 8 (T8.4, T8.5) - notificacion de contingencia (Art. 16, D-4).
//
// El Art. 16 dice que los sujetos pasivos "podran utilizar" medidas de
// contingencia -son opcionales para el emisor- en tres escenarios: falla de
// internet, del dispositivo movil y del servicio electrico. Superada la
// contingencia, el emisor debe registrar en el sistema los comprobantes
// fisicos emitidos: esa obligacion de RECIBIR el registro no es opcional para
// el Rol A. D-4 dejo la app movil y el modo offline fuera de esta version;
// esta bandeja si se construye.
//
// NO ES UNA EMISION. No toca FED_EMISOR_CONTADOR ni FED_NUM_CONTROL: el
// numero que se notifica ya fue asignado por el talonario fisico, no por este
// modulo. Por eso no hay transaccion con contador que bloquear, a diferencia
// de facturaCreate/retencionCreate.
public record FacturacionElectronicaContingenciaNotificarCommand(
    long EmisorId,
    string NumeracionFisica,
    DateTime FechaEmisionFisica,
    string Escenario,
    string UsuarioIns = "");

public class FacturacionElectronicaContingenciaNotificarHandler(ConnectionDB _connectionDB)
{
    private static readonly string[] EscenariosValidos = ["internet", "dispositivo", "electrico"];

    public async Task<ResultDto<ContingenciaResponse>> HandleAsync(
        FacturacionElectronicaContingenciaNotificarCommand command)
    {
        // Nivel 1 - validacion previa. Un solo punto de salida y un solo
        // registro de rechazo, igual que el validador de FacturaCreate/
        // GuiaCreate/NotaCreate/RetencionCreate: un intento rechazado es una
        // accion efectuada (Art. 18.2), asi que deja rastro en bitacora. Si el
        // registro falla, no se pierde el rechazo: el que manda es el mensaje
        // que se devuelve (ver RegistrarRechazoAsync).
        string numeracion = (command.NumeracionFisica ?? string.Empty).Trim();
        string escenario = (command.Escenario ?? string.Empty).Trim().ToLowerInvariant();
        string? mensajeRechazo = Validar(command, numeracion, escenario);

        if (mensajeRechazo is not null)
        {
            await FacturaEmision.RegistrarRechazoAsync(
                _connectionDB, command.EmisorId, "contingencia", command.UsuarioIns, mensajeRechazo);

            return Falla(mensajeRechazo);
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

        // Nivel 3 - ejecucion. TODO O NADA: el INSERT y la bitacora entran en
        // una transaccion, igual que el resto del modulo (facturaCreate,
        // retencionCreate, guiaCreate). Sin esto, un fallo en RegistrarAsync
        // dejaba la notificacion ya confirmada en la base mientras el cliente
        // recibia un error, sin forma de saberlo.
        try
        {
            using var tx = await cn.BeginTransactionAsync();

            var emisor = await FacturaEmision.LeerEmisorAsync(cn, tx, command.EmisorId);

            if (emisor is null)
            {
                await tx.RollbackAsync();

                return Falla($"No existe un emisor con el identificador {command.EmisorId}.");
            }

            long id;
            DateTime notificadoEn;

            using (var cmd = new NpgsqlCommand(FacturacionElectronicaDb.SqlContingenciaInsert, cn, tx))
            {
                cmd.Parameters.AddWithValue("emisor_id", command.EmisorId);
                cmd.Parameters.AddWithValue("numeracion_fisica", numeracion);
                cmd.Parameters.AddWithValue("fecha_emision_fisica", command.FechaEmisionFisica.Date);
                cmd.Parameters.AddWithValue("escenario", escenario);
                cmd.Parameters.AddWithValue("usuario_ins", FacturacionElectronicaDb.DbValue(command.UsuarioIns));

                using var reader = await cmd.ExecuteReaderAsync();
                await reader.ReadAsync();

                id = reader.SafeGetInt64("id");
                notificadoEn = reader.GetDateTime(reader.GetOrdinal("notificado_en"));
            }

            // Bitacora (Art. 18.2): "toda accion efectuada", y notificar una
            // contingencia es una accion nueva sobre el sistema, no un cambio
            // sobre un documento existente.
            await FacturaEmision.RegistrarAsync(cn, tx, null, command.EmisorId, "contingencia",
                command.UsuarioIns, new { contingenciaId = id, numeracion, escenario });

            await tx.CommitAsync();

            return Exito(new ContingenciaResponse(
                id, command.EmisorId, numeracion,
                FacturaFormato.FechaOchoDigitos(command.FechaEmisionFisica),
                escenario,
                notificadoEn.ToString("dd/MM/yyyy HH:mm"),
                string.Empty, 0, string.Empty, Conciliado: false));
        }
        catch (NpgsqlException ex) when (FacturacionElectronicaDb.EsClaveDuplicada(ex)
            && FacturacionElectronicaDb.NombreRestriccion(ex) == FacturacionElectronicaDb.RestriccionContingenciaUnica)
        {
            return Falla("Ese emisor ya notificó una contingencia con esa numeración física.");
        }
        catch (Exception ex)
        {
            return Falla($"Error técnico: {ex.Message}");
        }
    }

    // Un solo punto de validacion, mismo criterio que FacturaValidador/
    // RetencionValidador: devuelve null si esta todo bien, o el mensaje de
    // negocio que hay que mostrar y auditar.
    private static string? Validar(
        FacturacionElectronicaContingenciaNotificarCommand command, string numeracion, string escenario)
    {
        if (command.EmisorId <= 0)
        {
            return "El emisor es obligatorio.";
        }

        if (numeracion.Length == 0)
        {
            return "La numeración física es obligatoria.";
        }

        // Tope de columna (VARCHAR(50)). Sin este chequeo, un valor mas largo
        // llega a la base y el "value too long" de Postgres se cuela por el
        // catch generico como un error tecnico crudo en vez de un mensaje de
        // negocio.
        if (numeracion.Length > 50)
        {
            return "La numeración física no puede superar los 50 caracteres.";
        }

        // Art. 16.3: precedida de "contingencia" mas caracteres que la
        // identifiquen y diferencien. El CHECK de la base lo repite (y la
        // unicidad la aplica sin distinguir mayusculas de minusculas, ver
        // Sql/23_fed_contingencia_uk_ci.sql); se valida antes para devolver
        // un mensaje de negocio y no un error de SQL.
        if (!numeracion.StartsWith("contingencia", StringComparison.OrdinalIgnoreCase))
        {
            return "La numeración física debe estar precedida de la palabra \"contingencia\", "
                + "conforme al Artículo 16.3.";
        }

        if (!EscenariosValidos.Contains(escenario))
        {
            return "El escenario debe ser uno de: internet, dispositivo o eléctrico (Artículo 16).";
        }

        // Igual que UsuarioConcilia en contingenciaConciliar: sin este dato el
        // INSERT viola el NOT NULL de USUARIO_INS y el error crudo de
        // Postgres (23502) se filtraria al llamador en vez de un mensaje de
        // negocio.
        if (string.IsNullOrWhiteSpace(command.UsuarioIns))
        {
            return "El usuario que notifica es obligatorio.";
        }

        if (command.UsuarioIns.Trim().Length > 50)
        {
            return "El usuario no puede superar los 50 caracteres.";
        }

        return null;
    }

    private static ResultDto<ContingenciaResponse> Exito(ContingenciaResponse dato) =>
        new(dato) { IsValid = true, Message = FacturacionElectronicaDb.MensajeExito };

    private static ResultDto<ContingenciaResponse> Falla(string mensaje) =>
        new(null!) { Data = null, IsValid = false, Message = mensaje };
}

[ApiController]
[Authorize]
[Route("api/FacturacionElectronica")]
public class FacturacionElectronicaContingenciaNotificarController(ConnectionDB _connectionDB) : ControllerBase
{
    [HttpPost]
    [Route("contingenciaNotificar")]
    public async Task<IActionResult> ContingenciaNotificar(
        FacturacionElectronicaContingenciaNotificarCommand value)
    {
        var handler = new FacturacionElectronicaContingenciaNotificarHandler(_connectionDB);
        var result = await handler.HandleAsync(value);

        return Ok(result);
    }
}
