using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.FacturacionElectronica;

// T4.8 y T4.9 - emision directa de un documento fiscal: factura y nota de
// entrega.
//
// NO EMITE NOTAS DE DEBITO NI DE CREDITO, y no es un olvido: el Art. 23 de la
// Providencia SNAT/2011/00071 exige que una nota haga referencia a la fecha,
// numero y monto de la factura que soporto la operacion, y ese dato no existe en
// esta solicitud. Aceptar el tipo por esta via producia un documento fiscal
// inconforme que despues no se podia reparar, porque en este modulo no hay
// UPDATE ni DELETE sobre FED_DOCUMENTO. El validador lo rechaza indicando que la
// via es notaCreate (T5.3).
//
// TODO O NADA. Valida, numera el documento, calcula totales, persiste el
// documento con sus renglones e impuestos, ASIGNA EL NUMERO DE CONTROL y registra
// en bitacora, en UNA transaccion. Un fallo en cualquier paso no puede dejar un
// numero de control asignado sin documento ni un documento a medias, y ninguna de
// esas dos cosas seria un bug menor: la primera ensucia el registro del Art. 32
// que se le reporta al SENIAT, y la segunda es un documento fiscal incompleto.
//
// IDEMPOTENCIA (T4.9). INV-3 -nunca mas de un ejemplar del mismo documento, Art.
// 21.2- la sostiene el UNIQUE de (emisor, tipo, serie, numeracion). Pero cuando la
// numeracion la genera el sistema, dos peticiones identicas obtendrian numeros
// distintos y produjeron dos documentos validos por separado. El doble clic no es
// un caso raro: es el caso normal. De ahi la clave de idempotencia, con dos
// caminos: la consulta previa evita trabajo y la captura del UNIQUE cubre la
// carrera. El que garantiza la invariante es el segundo.
//
// EL MECANISMO VIVE EN Factura/FacturaEmision.cs (T5.8, D-26). Este archivo
// decide el orden de los pasos y con que validador; los pasos en si los comparte
// con notaCreate, para que INV-3 tenga una sola implementacion.
public class FacturacionElectronicaFacturaCreateHandler(ConnectionDB _connectionDB, IConfiguration _config)
{
    public async Task<ResultDto<FacturaEmitidaResponse>> HandleAsync(FacturaEmitirCommand command)
    {
        var imprenta = FacturaImprenta.Leer(_config);

        // Nivel 1 - validacion previa. Art. 29.4: validar la estructura es
        // obligacion de la imprenta digital, y va ANTES de asignar el numero.
        var validacion = FacturaValidador.Validar(command, imprenta);

        if (!validacion.EsValida)
        {
            // Un intento rechazado es una accion efectuada (Art. 18.2), asi que
            // deja rastro. Si el registro de la bitacora falla, no se pierde el
            // rechazo: el que manda es el mensaje que se devuelve.
            await FacturaEmision.RegistrarRechazoAsync(
                _connectionDB, command.EmisorId, command.TipoDocumento, command.UsuarioIns, validacion.Mensaje);

            return FacturaEmision.Falla(validacion.Mensaje);
        }

        string tipo = command.TipoDocumento.Trim().ToLowerInvariant();
        string serie = (command.Serie ?? string.Empty).Trim();
        string clave = (command.ClaveIdempotencia ?? string.Empty).Trim();

        using var cn = _connectionDB.GetFedConnection();

        // Nivel 2 - apertura de conexion.
        try
        {
            await cn.OpenAsync();
        }
        catch (Exception ex)
        {
            return FacturaEmision.Falla($"Error técnico al abrir conexión FED: {ex.Message}");
        }

        // Nivel 3 - ejecucion.
        try
        {
            // Idempotencia, camino rapido.
            if (clave.Length > 0)
            {
                var yaEmitido = await FacturaEmision.BuscarPorClaveAsync(cn, null, command.EmisorId, clave, imprenta);

                if (yaEmitido is not null)
                {
                    return FacturaEmision.Exito(yaEmitido);
                }
            }

            using var tx = await cn.BeginTransactionAsync();

            // El emisor se lee DENTRO de la transaccion: sus datos se copian al
            // documento (Art. 29.3) y no pueden cambiar a mitad de la emision.
            FacturaEmisorDatos? emisor = await FacturaEmision.LeerEmisorAsync(cn, tx, command.EmisorId);

            if (emisor is null)
            {
                return await FacturaEmision.FallaEnTxAsync(tx,
                    $"No existe un emisor con el identificador {command.EmisorId}.");
            }

            if (emisor.Estado != "activo")
            {
                return await FacturaEmision.FallaEnTxAsync(tx,
                    "El emisor está inactivo: no puede emitir documentos.");
            }

            // Numeracion del Art. 7.2 segun el modo del emisor (D-21).
            var (numeracion, fallaNumeracion) = await FacturaEmision.ResolverNumeracionAsync(
                cn, tx, emisor, command.EmisorId, tipo, serie, command.NumeracionExterna);

            if (fallaNumeracion is not null)
            {
                return await FacturaEmision.FallaEnTxAsync(tx, fallaNumeracion);
            }

            var totales = FacturaCalculo.Calcular(command.Renglones);

            var (documentoId, emitidoEn) = await FacturaEmision.InsertarDocumentoAsync(
                cn, tx, command, tipo, serie, numeracion, clave, emisor, totales, imprenta);

            await FacturaEmision.InsertarRenglonesAsync(cn, tx, documentoId, command.Renglones, totales);
            await FacturaEmision.InsertarImpuestosAsync(cn, tx, documentoId, totales);

            // NUMERO DE CONTROL, en la MISMA transaccion. Se reusa el SQL de la
            // Fase 2, incluido el bloqueo por emisor: es el mismo mecanismo que ya
            // se demostro bajo 30 peticiones simultaneas.
            var numeroControl = await FacturaEmision.AsignarNumeroControlAsync(
                cn, tx, command.EmisorId, tipo, documentoId, command.UsuarioIns);

            if (numeroControl is null)
            {
                return await FacturaEmision.FallaEnTxAsync(tx,
                    "La secuencia de números de control del emisor se agotó: se consumieron "
                    + "los 99 identificadores de dos dígitos.");
            }

            // Bitacora (Art. 18.2).
            await FacturaEmision.RegistrarAsync(cn, tx, documentoId, command.EmisorId, "emision", command.UsuarioIns,
                new
                {
                    numeracion,
                    serie,
                    numeroControl = numeroControl.Value.Numero,
                    totalGeneral = totales.TotalGeneral,
                    esPrueba = !imprenta.EsDefinitivo
                });

            await tx.CommitAsync();

            return FacturaEmision.Exito(FacturaEmision.Armar(
                documentoId, tipo, serie, numeracion, emitidoEn, totales,
                numeroControl.Value.Numero, numeroControl.Value.Fecha, imprenta, yaExistia: false));
        }
        catch (NpgsqlException ex) when (FacturacionElectronicaDb.EsClaveDuplicada(ex))
        {
            var (existente, falla) = await FacturaEmision.ResolverClaveDuplicadaAsync(
                cn, ex, command.EmisorId, clave, tipo, command.NumeracionExterna, imprenta);

            return existente is not null
                ? FacturaEmision.Exito(existente)
                : FacturaEmision.Falla(falla!);
        }
        catch (Exception ex)
        {
            return FacturaEmision.Falla($"Error técnico: {ex.Message}");
        }
    }
}

[ApiController]
[Authorize]
[Route("api/FacturacionElectronica")]
public class FacturacionElectronicaFacturaCreateController(ConnectionDB _connectionDB, IConfiguration _config) : ControllerBase
{
    [HttpPost]
    [Route("facturaCreate")]
    public async Task<IActionResult> FacturaCreate(FacturaEmitirCommand value)
    {
        var handler = new FacturacionElectronicaFacturaCreateHandler(_connectionDB, _config);
        var result = await handler.HandleAsync(value);

        return Ok(result);
    }
}
