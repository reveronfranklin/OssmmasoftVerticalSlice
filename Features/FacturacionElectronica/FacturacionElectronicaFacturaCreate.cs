using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.FacturacionElectronica;

// T4.8 y T4.9 - emision completa de un documento fiscal.
//
// TODO O NADA. Valida, numera el documento, calcula totales, persiste el
// documento con sus renglones e impuestos, ASIGNA EL NUMERO DE CONTROL y registra
// en bitacora, en UNA transaccion. Un fallo en cualquier paso no puede dejar un
// numero de control asignado sin documento ni un documento a medias, y ninguna de
// esas dos cosas seria un bug menor: la primera ensucia el registro del Art. 32
// que se le reporta al SENIAT, y la segunda es un documento fiscal incompleto.
//
// Por eso la asignacion del numero de control se hace ACA con el SQL compartido,
// y no llamando al handler de la Fase 2: ese abre su propia conexion, y dos
// conexiones son dos transacciones.
//
// IDEMPOTENCIA (T4.9). INV-3 -nunca mas de un ejemplar del mismo documento, Art.
// 21.2- la sostiene el UNIQUE de (emisor, tipo, serie, numeracion). Pero cuando la
// numeracion la genera el sistema, dos peticiones identicas obtendrian numeros
// distintos y produjeron dos documentos validos por separado. El doble clic no es
// un caso raro: es el caso normal. De ahi la clave de idempotencia, con dos
// caminos: la consulta previa evita trabajo y la captura del UNIQUE cubre la
// carrera. El que garantiza la invariante es el segundo.
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
            await RegistrarRechazoAsync(command, validacion.Mensaje);

            return Falla(validacion.Mensaje);
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
            return Falla($"Error técnico al abrir conexión FED: {ex.Message}");
        }

        // Nivel 3 - ejecucion.
        try
        {
            // Idempotencia, camino rapido.
            if (clave.Length > 0)
            {
                var yaEmitido = await BuscarPorClaveAsync(cn, null, command.EmisorId, clave, imprenta);

                if (yaEmitido is not null)
                {
                    return Exito(yaEmitido);
                }
            }

            using var tx = await cn.BeginTransactionAsync();

            // El emisor se lee DENTRO de la transaccion: sus datos se copian al
            // documento (Art. 29.3) y no pueden cambiar a mitad de la emision.
            FacturaEmisorDatos? emisor = await LeerEmisorAsync(cn, tx, command.EmisorId);

            if (emisor is null)
            {
                return await FallaEnTxAsync(tx, $"No existe un emisor con el identificador {command.EmisorId}.");
            }

            if (emisor.Estado != "activo")
            {
                return await FallaEnTxAsync(tx, "El emisor está inactivo: no puede emitir documentos.");
            }

            // Numeracion del Art. 7.2 segun el modo del emisor (D-21).
            string numeracion;

            if (emisor.ModoNumeracion == "externa")
            {
                if (string.IsNullOrWhiteSpace(command.NumeracionExterna))
                {
                    return await FallaEnTxAsync(tx,
                        "El emisor está configurado para traer su propia numeración y la solicitud no la incluye.");
                }

                numeracion = command.NumeracionExterna.Trim();
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(command.NumeracionExterna))
                {
                    // No se acepta en silencio: aceptarla aca y generar en otra
                    // peticion es como se choca la numeracion de una misma serie.
                    return await FallaEnTxAsync(tx,
                        "El emisor está configurado para que el sistema genere la numeración, "
                        + "así que la solicitud no debe traer una.");
                }

                numeracion = await SiguienteNumeracionAsync(cn, tx, command.EmisorId, tipo, serie);
            }

            var totales = FacturaCalculo.Calcular(command.Renglones);

            // Documento. La instantanea del emisor y de la imprenta se escribe
            // aca: lo estampado es un hecho historico.
            long documentoId;
            DateTime emitidoEn;

            using (var cmd = new NpgsqlCommand(FacturaDb.SqlDocumentoInsert, cn, tx))
            {
                cmd.Parameters.AddWithValue("emisor_id", command.EmisorId);
                cmd.Parameters.AddWithValue("tipo_documento", tipo);
                cmd.Parameters.AddWithValue("serie", serie);
                cmd.Parameters.AddWithValue("numeracion", numeracion);
                cmd.Parameters.AddWithValue("emisor_rif", emisor.Rif);
                cmd.Parameters.AddWithValue("emisor_razon_social", emisor.RazonSocial);
                cmd.Parameters.AddWithValue("emisor_domicilio", emisor.Domicilio);
                cmd.Parameters.AddWithValue("adq_nombre", FacturacionElectronicaDb.DbValue(command.AdqNombre));
                cmd.Parameters.AddWithValue("adq_rif", FacturacionElectronicaDb.DbValue(command.AdqRif));
                cmd.Parameters.AddWithValue("adq_documento_id", FacturacionElectronicaDb.DbValue(command.AdqDocumentoId));
                cmd.Parameters.AddWithValue("total_exento", totales.TotalExento);
                cmd.Parameters.AddWithValue("total_base", totales.TotalBase);
                cmd.Parameters.AddWithValue("total_iva", totales.TotalIva);
                cmd.Parameters.AddWithValue("total_general", totales.TotalGeneral);
                cmd.Parameters.AddWithValue("imprenta_rif", FacturacionElectronicaDb.DbValue(imprenta.Rif));
                cmd.Parameters.AddWithValue("imprenta_razon_social", FacturacionElectronicaDb.DbValue(imprenta.RazonSocial));
                cmd.Parameters.AddWithValue("imprenta_providencia", FacturacionElectronicaDb.DbValue(imprenta.Providencia));
                cmd.Parameters.AddWithValue("es_prueba", !imprenta.EsDefinitivo);
                cmd.Parameters.AddWithValue("clave_idempotencia", FacturacionElectronicaDb.DbValue(clave));
                cmd.Parameters.AddWithValue("usuario_ins", FacturacionElectronicaDb.DbValue(command.UsuarioIns));

                using var reader = await cmd.ExecuteReaderAsync();
                await reader.ReadAsync();

                documentoId = reader.SafeGetInt64("id");
                emitidoEn = reader.GetDateTime(reader.GetOrdinal("emitido_en"));
            }

            // Renglones (Arts. 7.8 a 7.10).
            for (int i = 0; i < command.Renglones.Count; i++)
            {
                var renglon = command.Renglones[i];

                using var cmd = new NpgsqlCommand(FacturaDb.SqlDetalleInsert, cn, tx);
                cmd.Parameters.AddWithValue("documento_id", documentoId);
                cmd.Parameters.AddWithValue("orden", i + 1);
                cmd.Parameters.AddWithValue("descripcion", renglon.Descripcion.Trim());
                cmd.Parameters.AddWithValue("codigo", FacturacionElectronicaDb.DbValue(renglon.Codigo));
                cmd.Parameters.AddWithValue("cantidad", renglon.Cantidad);
                cmd.Parameters.AddWithValue("precio", renglon.Precio);
                cmd.Parameters.AddWithValue("alicuota", renglon.Alicuota);
                cmd.Parameters.AddWithValue("exento", renglon.Exento || renglon.Alicuota == 0);
                cmd.Parameters.AddWithValue("bienes_entregados", FacturacionElectronicaDb.DbValue(renglon.BienesEntregados));
                cmd.Parameters.AddWithValue("ajuste_descripcion", FacturacionElectronicaDb.DbValue(renglon.AjusteDescripcion));
                cmd.Parameters.AddWithValue("ajuste_valor", renglon.AjusteValor);
                cmd.Parameters.AddWithValue("total_renglon", totales.RenglonTotales[i]);

                await cmd.ExecuteNonQueryAsync();
            }

            // Desglose por alicuota (Arts. 7.11 y 7.12). Una fila por tasa.
            foreach (var grupo in totales.PorAlicuota)
            {
                using var cmd = new NpgsqlCommand(FacturaDb.SqlImpuestoInsert, cn, tx);
                cmd.Parameters.AddWithValue("documento_id", documentoId);
                cmd.Parameters.AddWithValue("alicuota", grupo.Alicuota);
                cmd.Parameters.AddWithValue("base_imponible", grupo.BaseImponible);
                cmd.Parameters.AddWithValue("monto_iva", grupo.MontoIva);

                await cmd.ExecuteNonQueryAsync();
            }

            // NUMERO DE CONTROL, en la MISMA transaccion. Se reusa el SQL de la
            // Fase 2, incluido el bloqueo por emisor: es el mismo mecanismo que ya
            // se demostro bajo 30 peticiones simultaneas.
            var numeroControl = await AsignarNumeroControlAsync(cn, tx, command.EmisorId, tipo, documentoId, command.UsuarioIns);

            if (numeroControl is null)
            {
                return await FallaEnTxAsync(tx,
                    "La secuencia de números de control del emisor se agotó: se consumieron "
                    + "los 99 identificadores de dos dígitos.");
            }

            // Bitacora (Art. 18.2).
            await RegistrarAsync(cn, tx, documentoId, command.EmisorId, "emision", command.UsuarioIns,
                new
                {
                    numeracion,
                    serie,
                    numeroControl = numeroControl.Value.Numero,
                    totalGeneral = totales.TotalGeneral,
                    esPrueba = !imprenta.EsDefinitivo
                });

            await tx.CommitAsync();

            return Exito(Armar(documentoId, tipo, serie, numeracion, emitidoEn, totales,
                numeroControl.Value.Numero, numeroControl.Value.Fecha, imprenta, yaExistia: false));
        }
        catch (NpgsqlException ex) when (FacturacionElectronicaDb.EsClaveDuplicada(ex))
        {
            string restriccion = FacturacionElectronicaDb.NombreRestriccion(ex);

            // Carrera de idempotencia: otra peticion identica gano. Se devuelve el
            // documento que quedo, no uno nuevo. ESTE es el camino que garantiza
            // INV-3; la consulta previa solo evita trabajo.
            if (restriccion == "fed_documento_idem_uk" && clave.Length > 0)
            {
                var existente = await BuscarPorClaveAsync(cn, null, command.EmisorId, clave, imprenta);

                if (existente is not null)
                {
                    return Exito(existente);
                }
            }

            // Choque de la numeracion propia: el emisor externo trajo una que ya
            // uso. Es INV-3 tambien, y aca no hay nada que devolver: el documento
            // que existe es OTRO documento con la misma numeracion.
            if (restriccion == "fed_documento_uk")
            {
                return Falla($"El emisor ya tiene un documento {tipo} con la numeración {command.NumeracionExterna}.");
            }

            return Falla($"Error técnico: {ex.Message}");
        }
        catch (Exception ex)
        {
            return Falla($"Error técnico: {ex.Message}");
        }
    }

    // ------------------------------------------------------------------

    private static async Task<FacturaEmisorDatos?> LeerEmisorAsync(NpgsqlConnection cn, NpgsqlTransaction tx, long emisorId)
    {
        using var cmd = new NpgsqlCommand(FacturaDb.SqlEmisorParaEmitir, cn, tx);
        cmd.Parameters.AddWithValue("emisor_id", emisorId);

        using var reader = await cmd.ExecuteReaderAsync();

        return await reader.ReadAsync() ? FacturaDb.MapEmisor(reader) : null;
    }

    private static async Task<string> SiguienteNumeracionAsync(
        NpgsqlConnection cn, NpgsqlTransaction tx, long emisorId, string tipo, string serie)
    {
        long ultimo;

        using (var cmd = new NpgsqlCommand(FacturaDb.SqlContadorDocBloquear, cn, tx))
        {
            cmd.Parameters.AddWithValue("emisor_id", emisorId);
            cmd.Parameters.AddWithValue("tipo_documento", tipo);
            cmd.Parameters.AddWithValue("serie", serie);

            using var reader = await cmd.ExecuteReaderAsync();
            await reader.ReadAsync();
            ultimo = reader.SafeGetInt64("ultimo_numero");
        }

        string numeracion = FacturaDb.SiguienteNumeracion(ultimo);

        using (var cmd = new NpgsqlCommand(FacturaDb.SqlContadorDocActualizar, cn, tx))
        {
            cmd.Parameters.AddWithValue("ultimo_numero", ultimo + 1);
            cmd.Parameters.AddWithValue("emisor_id", emisorId);
            cmd.Parameters.AddWithValue("tipo_documento", tipo);
            cmd.Parameters.AddWithValue("serie", serie);

            await cmd.ExecuteNonQueryAsync();
        }

        return numeracion;
    }

    // Asigna el numero de control con el mecanismo de la Fase 2, dentro de la
    // transaccion de la emision. Devuelve null si la secuencia del emisor se
    // agoto -99 identificadores por 99.999.999 secuenciales-.
    private static async Task<(string Numero, DateTime Fecha)?> AsignarNumeroControlAsync(
        NpgsqlConnection cn, NpgsqlTransaction tx, long emisorId, string tipo, long documentoId, string usuario)
    {
        string identificadorActual;
        int secuencialActual;

        using (var cmd = new NpgsqlCommand(FacturacionElectronicaDb.SqlContadorBloquear, cn, tx))
        {
            cmd.Parameters.AddWithValue("emisor_id", emisorId);

            using var reader = await cmd.ExecuteReaderAsync();
            await reader.ReadAsync();

            identificadorActual = reader.SafeGetString("identificador");
            secuencialActual = reader.SafeGetInt32("secuencial");
        }

        if (!FacturacionElectronicaDb.CalcularSiguiente(
                identificadorActual, secuencialActual, out string identificador, out int secuencial))
        {
            return null;
        }

        using (var cmd = new NpgsqlCommand(FacturacionElectronicaDb.SqlContadorActualizar, cn, tx))
        {
            cmd.Parameters.AddWithValue("identificador", identificador);
            cmd.Parameters.AddWithValue("secuencial", secuencial);
            cmd.Parameters.AddWithValue("emisor_id", emisorId);

            await cmd.ExecuteNonQueryAsync();
        }

        DateTime fechaAsignacion;

        using (var cmd = new NpgsqlCommand(FacturacionElectronicaDb.SqlNumControlInsert, cn, tx))
        {
            cmd.Parameters.AddWithValue("emisor_id", emisorId);
            cmd.Parameters.AddWithValue("documento_id", documentoId);
            cmd.Parameters.AddWithValue("identificador", identificador);
            cmd.Parameters.AddWithValue("secuencial", secuencial);
            cmd.Parameters.AddWithValue("tipo_documento", tipo);
            cmd.Parameters.AddWithValue("usuario_ins", FacturacionElectronicaDb.DbValue(usuario));

            using var reader = await cmd.ExecuteReaderAsync();
            await reader.ReadAsync();

            fechaAsignacion = reader.GetDateTime(reader.GetOrdinal("fecha_asignacion"));
        }

        return (FacturacionElectronicaDb.FormatearNumeroControl(identificador, secuencial), fechaAsignacion);
    }

    private static async Task<FacturaEmitidaResponse?> BuscarPorClaveAsync(
        NpgsqlConnection cn, NpgsqlTransaction? tx, long emisorId, string clave, FacturaImprentaDatos imprenta)
    {
        using var cmd = new NpgsqlCommand(FacturaDb.SqlDocumentoPorClave, cn, tx);
        cmd.Parameters.AddWithValue("emisor_id", emisorId);
        cmd.Parameters.AddWithValue("clave", clave);

        using var reader = await cmd.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return null;
        }

        long documentoId = reader.SafeGetInt64("id");
        string tipo = reader.SafeGetString("tipo_documento");
        string serie = reader.SafeGetString("serie");
        string numeracion = reader.SafeGetString("numeracion");
        DateTime emitidoEn = reader.GetDateTime(reader.GetOrdinal("emitido_en"));

        var totales = new FacturaTotales(
            reader.SafeGetDecimal("total_exento"),
            reader.SafeGetDecimal("total_base"),
            reader.SafeGetDecimal("total_iva"),
            reader.SafeGetDecimal("total_general"),
            [], []);

        // El numero de control ya asignado se recupera del listado; para la
        // respuesta idempotente alcanza con lo que el documento tiene.
        return Armar(documentoId, tipo, serie, numeracion, emitidoEn, totales,
            numeroControl: string.Empty, fechaAsignacion: emitidoEn, imprenta, yaExistia: true);
    }

    private static FacturaEmitidaResponse Armar(
        long documentoId, string tipo, string serie, string numeracion, DateTime emitidoEn,
        FacturaTotales totales, string numeroControl, DateTime fechaAsignacion,
        FacturaImprentaDatos imprenta, bool yaExistia) => new(
            documentoId,
            numeracion,
            FacturaFormato.NumeracionConSerie(serie, numeracion),
            serie,
            tipo,
            FacturaFormato.Denominacion(tipo),
            numeroControl,
            numeroControl.Length > 0 ? $"N° de Control {numeroControl}" : string.Empty,
            numeroControl.Length > 0 ? FacturaFormato.RangoNumerosControl(numeroControl) : string.Empty,
            FacturaFormato.FechaOchoDigitos(emitidoEn),
            FacturaFormato.HoraConMeridiano(emitidoEn),
            FacturaFormato.FechaOchoDigitos(fechaAsignacion),
            totales.TotalExento,
            totales.TotalBase,
            totales.TotalIva,
            totales.TotalGeneral,
            !imprenta.EsDefinitivo,
            FacturaImprenta.MotivoDePrueba(imprenta),
            FacturaFormato.LeyendaProvidencia,
            yaExistia);

    private static async Task RegistrarAsync(
        NpgsqlConnection cn, NpgsqlTransaction? tx, long? documentoId, long emisorId,
        string accion, string usuario, object detalle)
    {
        using var cmd = new NpgsqlCommand(FacturaDb.SqlBitacoraInsert, cn, tx);
        cmd.Parameters.AddWithValue("documento_id", documentoId.HasValue ? documentoId.Value : DBNull.Value);
        cmd.Parameters.AddWithValue("emisor_id", emisorId);
        cmd.Parameters.AddWithValue("accion", accion);
        cmd.Parameters.AddWithValue("usuario", FacturacionElectronicaDb.DbValue(usuario));
        cmd.Parameters.AddWithValue("detalle", JsonSerializer.Serialize(detalle));

        await cmd.ExecuteNonQueryAsync();
    }

    // El rechazo se registra en su propia conexion: la emision nunca empezo, y un
    // fallo al auditar no puede convertirse en un fallo distinto del que se le
    // devuelve a quien llamo.
    private async Task RegistrarRechazoAsync(FacturaEmitirCommand command, string motivo)
    {
        try
        {
            using var cn = _connectionDB.GetFedConnection();
            await cn.OpenAsync();

            await RegistrarAsync(cn, null, null, command.EmisorId, "rechazo", command.UsuarioIns,
                new { motivo, tipoDocumento = command.TipoDocumento });
        }
        catch
        {
            // Silencio deliberado: el rechazo ya viaja en la respuesta. Hacer
            // fallar la peticion porque no se pudo auditar el fallo cambiaria un
            // mensaje util por un error tecnico.
        }
    }

    private static async Task<ResultDto<FacturaEmitidaResponse>> FallaEnTxAsync(NpgsqlTransaction tx, string mensaje)
    {
        await tx.RollbackAsync();

        return Falla(mensaje);
    }

    private static ResultDto<FacturaEmitidaResponse> Exito(FacturaEmitidaResponse dato) =>
        new(dato) { IsValid = true, Message = FacturacionElectronicaDb.MensajeExito };

    private static ResultDto<FacturaEmitidaResponse> Falla(string mensaje) =>
        new(null!) { Data = null, IsValid = false, Message = mensaje };
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
