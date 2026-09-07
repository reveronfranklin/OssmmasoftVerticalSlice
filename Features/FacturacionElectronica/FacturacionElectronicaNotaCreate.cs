using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.FacturacionElectronica;

// T5.10 - emision de una nota de debito o de credito.
//
// Providencia SNAT/2011/00071, Art. 22: las notas se emiten cuando ventas o
// servicios "quedaren sin efecto parcial o totalmente u originaren un ajuste, por
// cualquier causa, y por las cuales se otorgaron facturas". Art. 23: cumplen los
// requisitos del Art. 13 o del 15 segun el caso, salvo el numeral 1, e
// "igualmente deben hacer referencia a la fecha, numero y monto de la factura que
// soporto la operacion".
//
// POR QUE ES UNA OPERACION PROPIA Y NO UNA RAMA DE facturaCreate (D-26). Porque
// facturaCreate RECHAZA estos tipos: sin el documento origen, una nota queda sin
// la referencia del Art. 23, y en este modulo eso no se puede reparar despues. Un
// endpoint que rechaza notas no puede ademas emitirlas.
//
// PERO EL MOTOR NO SE DUPLICA. La transaccion unica, las dos rutas de
// idempotencia, la numeracion, el numero de control y la bitacora viven en
// Factura/FacturaEmision.cs, compartidos con la emision directa. INV-3 tiene una
// sola implementacion: un bug ahi se arregla una vez.
//
// LA NOTA LLEVA MONTOS POSITIVOS. El signo lo pone la denominacion -"NOTA DE
// CREDITO"-, no el importe, igual que en papel. Por eso el CHECK de totales >= 0
// de FED_DOCUMENTO no molesta y FacturaCalculo no se toca: el sentido se aplica
// al calcular el saldo, en la vista.
//
// LA ANULACION ES UNA NOTA CON SU INTENCION DECLARADA (D-32), no un endpoint
// aparte. El Art. 22 distingue "quedaren sin efecto" de "originaren un ajuste", y
// una nota de credito por el importe exacto de una devolucion total es
// aritmeticamente identica a una anulacion sin ser el mismo acto juridico. Se
// declara, no se deduce del saldo.
public record NotaEmitirCommand(
    long EmisorId,
    long DocumentoOrigenId,
    string TipoDocumento,
    string Motivo,
    List<FacturaRenglonCommand> Renglones,
    bool EsAnulacion = false,
    string Serie = "",
    string NumeracionExterna = "",
    string AdqNombre = "",
    string AdqRif = "",
    string AdqDocumentoId = "",
    string Moneda = "",
    decimal TasaCambio = 0,
    string UsuarioIns = "",
    string ClaveIdempotencia = "");

public class FacturacionElectronicaNotaCreateHandler(ConnectionDB _connectionDB, IConfiguration _config)
{
    public async Task<ResultDto<FacturaEmitidaResponse>> HandleAsync(NotaEmitirCommand command)
    {
        var imprenta = FacturaImprenta.Leer(_config);

        string tipo = (command.TipoDocumento ?? string.Empty).Trim().ToLowerInvariant();
        string serie = (command.Serie ?? string.Empty).Trim();
        string clave = (command.ClaveIdempotencia ?? string.Empty).Trim();
        string motivo = (command.Motivo ?? string.Empty).Trim();

        // Art. 22 - la norma admite cualquier causa, pero exige que exista.
        if (motivo.Length == 0)
        {
            return FacturaEmision.Falla(
                "La nota debe indicar el motivo: el Artículo 22 de la Providencia SNAT/2011/00071 "
                + "admite cualquier causa, pero no la ausencia de causa.");
        }

        if (command.DocumentoOrigenId <= 0)
        {
            return FacturaEmision.Falla(
                "La nota debe indicar el documento que corrige: el Artículo 23 exige la referencia a la "
                + "fecha, número y monto de la factura que soportó la operación.");
        }

        using var cn = _connectionDB.GetFedConnection();

        try
        {
            await cn.OpenAsync();
        }
        catch (Exception ex)
        {
            return FacturaEmision.Falla($"Error técnico al abrir conexión FED: {ex.Message}");
        }

        try
        {
            // Idempotencia, camino rapido.
            if (clave.Length > 0)
            {
                var yaEmitida = await FacturaEmision.BuscarPorClaveAsync(cn, null, command.EmisorId, clave, imprenta);

                if (yaEmitida is not null)
                {
                    return FacturaEmision.Exito(yaEmitida);
                }
            }

            using var tx = await cn.BeginTransactionAsync();

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

            // EL BLOQUEO VA ANTES DE LEER EL SALDO, y el orden no es cosmetico
            // (D-33, D-34): leer, calcular y despues insertar es el orden roto.
            // Dos notas de credito concurrentes por el saldo completo leerian el
            // mismo saldo y las dos se creerian validas.
            using (var cmd = new NpgsqlCommand(NotaDb.SqlBloquearOrigen, cn, tx))
            {
                cmd.Parameters.AddWithValue("ns", NotaDb.AdvisoryNamespace);
                cmd.Parameters.AddWithValue("documento_id", (int)command.DocumentoOrigenId);

                await cmd.ExecuteScalarAsync();
            }

            NotaOrigenDatos? origen;

            using (var cmd = new NpgsqlCommand(NotaDb.SqlOrigenParaNota, cn, tx))
            {
                cmd.Parameters.AddWithValue("documento_id", command.DocumentoOrigenId);

                using var reader = await cmd.ExecuteReaderAsync();
                origen = await reader.ReadAsync() ? NotaDb.MapOrigen(reader) : null;
            }

            var fallaOrigen = ValidarOrigen(origen, command, tipo);

            if (fallaOrigen is not null)
            {
                return await FacturaEmision.FallaEnTxAsync(tx, fallaOrigen);
            }

            // Validacion de contenido contra el Art. 13 o el 15 segun el tipo de
            // contribuyente del emisor (Art. 23, "segun sea el caso", D-31).
            var comando = ComandoEquivalente(command, tipo, serie, clave, origen!.Moneda);
            var validacion = FacturaValidador.ValidarNota(comando, imprenta, emisor.TipoContribuyente);

            if (!validacion.EsValida)
            {
                await FacturaEmision.RegistrarRechazoAsync(
                    _connectionDB, command.EmisorId, tipo, command.UsuarioIns, validacion.Mensaje);

                return await FacturaEmision.FallaEnTxAsync(tx, validacion.Mensaje);
            }

            var totales = FacturaCalculo.Calcular(command.Renglones);

            // D-34 - una nota de credito no puede exceder el saldo pendiente.
            //
            // Sin esto se pueden emitir diez notas de credito por el total de la
            // misma factura y dejar el saldo en negativo. En una tabla sin DELETE
            // ni UPDATE eso no deja un dato mal: lo deja mal para siempre.
            if (tipo == "credito" && totales.TotalGeneral > origen.Saldo)
            {
                return await FacturaEmision.FallaEnTxAsync(tx,
                    $"La nota de crédito es por {totales.TotalGeneral:N2} y al documento le queda un saldo "
                    + $"pendiente de {origen.Saldo:N2}. Una nota no puede exceder lo que queda por corregir.");
            }

            var (numeracion, fallaNumeracion) = await FacturaEmision.ResolverNumeracionAsync(
                cn, tx, emisor, command.EmisorId, tipo, serie, command.NumeracionExterna);

            if (fallaNumeracion is not null)
            {
                return await FacturaEmision.FallaEnTxAsync(tx, fallaNumeracion);
            }

            var (documentoId, emitidoEn) = await FacturaEmision.InsertarDocumentoAsync(
                cn, tx, comando, tipo, serie, numeracion, clave, emisor, totales, imprenta);

            await FacturaEmision.InsertarRenglonesAsync(cn, tx, documentoId, command.Renglones, totales);
            await FacturaEmision.InsertarImpuestosAsync(cn, tx, documentoId, totales);

            // El vinculo y la instantanea del Art. 23, en la MISMA transaccion.
            string origenNumeracion = FacturaFormato.NumeracionConSerie(origen.Serie, origen.Numeracion);
            string origenFecha8d = FacturaFormato.FechaOchoDigitos(origen.EmitidoEn);

            using (var cmd = new NpgsqlCommand(NotaDb.SqlNotaInsert, cn, tx))
            {
                cmd.Parameters.AddWithValue("documento_id", documentoId);
                cmd.Parameters.AddWithValue("documento_origen_id", origen.Id);
                cmd.Parameters.AddWithValue("motivo", motivo);
                cmd.Parameters.AddWithValue("es_anulacion", command.EsAnulacion);
                cmd.Parameters.AddWithValue("origen_numeracion", origenNumeracion);
                cmd.Parameters.AddWithValue("origen_fecha_8d", origenFecha8d);
                cmd.Parameters.AddWithValue("origen_total", origen.TotalGeneral);
                cmd.Parameters.AddWithValue("origen_moneda", origen.Moneda);
                cmd.Parameters.AddWithValue("usuario_ins", FacturacionElectronicaDb.DbValue(command.UsuarioIns));

                await cmd.ExecuteNonQueryAsync();
            }

            var numeroControl = await FacturaEmision.AsignarNumeroControlAsync(
                cn, tx, command.EmisorId, tipo, documentoId, command.UsuarioIns);

            if (numeroControl is null)
            {
                return await FacturaEmision.FallaEnTxAsync(tx,
                    "La secuencia de números de control del emisor se agotó: se consumieron "
                    + "los 99 identificadores de dos dígitos.");
            }

            // Bitacora (Art. 18.2). La emision de la nota apunta a la nota.
            await FacturaEmision.RegistrarAsync(cn, tx, documentoId, command.EmisorId, "emision", command.UsuarioIns,
                new
                {
                    numeracion,
                    serie,
                    numeroControl = numeroControl.Value.Numero,
                    totalGeneral = totales.TotalGeneral,
                    documentoOrigenId = origen.Id,
                    esAnulacion = command.EsAnulacion,
                    esPrueba = !imprenta.EsDefinitivo
                });

            // Y si la nota declara que anula, una SEGUNDA fila que apunta AL
            // DOCUMENTO ANULADO. Esa orientacion es la que hace indexable la
            // pregunta "este documento esta anulado" con el indice que ya existia,
            // sin tocar el jsonb (D-28).
            //
            // El documento original no se modifica: se le agrega un hecho. Art. 41
            // -sin tachaduras ni enmendaduras- y Art. 36 -el anulado se conserva-.
            if (command.EsAnulacion)
            {
                await FacturaEmision.RegistrarAsync(cn, tx, origen.Id, command.EmisorId, "anulacion", command.UsuarioIns,
                    new
                    {
                        notaId = documentoId,
                        notaNumeracion = numeracion,
                        notaNumeroControl = numeroControl.Value.Numero,
                        motivo
                    });
            }

            await tx.CommitAsync();

            var respuesta = FacturaEmision.Armar(
                documentoId, tipo, serie, numeracion, emitidoEn, totales,
                numeroControl.Value.Numero, numeroControl.Value.Fecha, imprenta, yaExistia: false)
                with
            {
                DocumentoOrigenId = origen.Id,
                ReferenciaOriginal = FacturaFormato.ReferenciaOriginal(
                    origenFecha8d, origenNumeracion, origen.TotalGeneral, origen.Moneda),
                Motivo = motivo,
                LeyendaContribuyente = FacturaFormato.LeyendaContribuyente(emisor.TipoContribuyente)
            };

            return FacturaEmision.Exito(respuesta);
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

    // ------------------------------------------------------------------

    // Lo que la nota exige del documento que corrige. Cada rechazo sale de un
    // articulo, no de una preferencia.
    private static string? ValidarOrigen(NotaOrigenDatos? origen, NotaEmitirCommand command, string tipo)
    {
        if (origen is null)
        {
            return $"No existe un documento con el identificador {command.DocumentoOrigenId}.";
        }

        if (origen.EmisorId != command.EmisorId)
        {
            // Una nota de un emisor no puede corregir el documento de otro: la
            // numeracion y la secuencia de control son del emisor (Art. 30).
            return "El documento que se intenta corregir pertenece a otro emisor.";
        }

        if (origen.TipoDocumento != "factura")
        {
            // Art. 22: las notas se emiten por operaciones "por las cuales se
            // otorgaron facturas". Una nota no corrige otra nota.
            return $"Solo se corrige una factura, y el documento indicado es de tipo {origen.TipoDocumento}. "
                + "El Artículo 22 de la Providencia SNAT/2011/00071 habla de operaciones por las cuales "
                + "se otorgaron facturas.";
        }

        if (origen.Estado == "anulado")
        {
            return "El documento que se intenta corregir ya está anulado.";
        }

        string moneda = (command.Moneda ?? string.Empty).Trim().ToUpperInvariant();

        if (moneda.Length == 0)
        {
            moneda = "VES";
        }

        if (moneda != origen.Moneda.Trim())
        {
            // Si difirieran, la referencia al monto del Art. 23 y el calculo del
            // saldo compararian cifras de monedas distintas: dos numeros que no
            // significan lo mismo.
            return $"La nota está en {moneda} y el documento que corrige está en {origen.Moneda.Trim()}. "
                + "Deben coincidir para que la referencia al monto del Artículo 23 signifique algo.";
        }

        if (tipo == "debito" && command.EsAnulacion)
        {
            // Anular es dejar sin efecto, y eso no se hace aumentando el monto.
            return "Una nota de débito no puede declarar una anulación: el Artículo 22 la reserva para "
                + "operaciones que quedan sin efecto, y una nota de débito aumenta el monto.";
        }

        return null;
    }

    // La nota reusa el motor de emision, que habla en FacturaEmitirCommand. Se
    // traduce aca en vez de duplicar el motor.
    private static FacturaEmitirCommand ComandoEquivalente(
        NotaEmitirCommand command, string tipo, string serie, string clave, string moneda) => new(
            command.EmisorId,
            tipo,
            command.Renglones,
            serie,
            command.NumeracionExterna,
            command.AdqNombre,
            command.AdqRif,
            command.AdqDocumentoId,
            command.UsuarioIns,
            clave,
            moneda,
            command.TasaCambio,
            command.DocumentoOrigenId,
            command.Motivo,
            command.EsAnulacion);
}

[ApiController]
[Authorize]
[Route("api/FacturacionElectronica")]
public class FacturacionElectronicaNotaCreateController(ConnectionDB _connectionDB, IConfiguration _config) : ControllerBase
{
    [HttpPost]
    [Route("notaCreate")]
    public async Task<IActionResult> NotaCreate(NotaEmitirCommand value)
    {
        var handler = new FacturacionElectronicaNotaCreateHandler(_connectionDB, _config);
        var result = await handler.HandleAsync(value);

        return Ok(result);
    }
}
