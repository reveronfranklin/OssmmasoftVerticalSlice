using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.FacturacionElectronica;

// T6B.3 y T6B.4 - emision de un comprobante de retencion. Art. 11 de la
// Providencia SNAT/2024/000102.
//
// EL DOCUMENTO QUE NO LLEVA NUMERO DE CONTROL (D-37). Es el rasgo que lo separa
// de los otros cuatro y lo primero que sorprende a quien lee este archivo
// buscando la asignacion: no esta, y no falta. El Art. 11 no remite a los
// numerales 4 y 5 del Art. 7 -como si hace el Art. 10.2 para la guia de
// despacho-, y su numeral 5 pide el numero de control DE LA FACTURA QUE SE
// RETIENE, no uno propio. La identificacion del comprobante es la numeracion de
// catorce caracteres del 11.1.
//
// PERO EL ROL A INTERVIENE IGUAL (D-39): el numeral 11.9 exige los datos de la
// imprenta digital autorizada. La imprenta aporta identidad sin aportar
// numeracion, y este es el unico documento del alcance con esa forma.
//
// TODO O NADA, como las otras dos emisiones: el comprobante, su desglose y el
// avance del contador entran en una transaccion. Un comprobante sin sus
// documentos retenidos no dice nada, y un contador que avanzo sin comprobante
// deja un hueco en una numeracion que la norma pide consecutiva.
//
// El contador se bloquea con el mismo idioma que los otros dos del modulo, y por
// la misma razon: SELECT MAX() + 1 esta roto bajo concurrencia, y eso quedo
// demostrado en la Fase 2 -la forma prohibida dejo 17 asignaciones de 300-.
public record RetencionEmitirCommand(
    long EmisorId,
    string Periodo,
    string ProveedorRif,
    string ProveedorRazonSocial,
    List<RetencionDocumentoCommand> Documentos,
    string ProveedorDomicilio = "",
    string ProveedorCorreo = "",
    string UsuarioIns = "",
    string ClaveIdempotencia = "");

public class FacturacionElectronicaRetencionCreateHandler(ConnectionDB _connectionDB, IConfiguration _config)
{
    public async Task<ResultDto<RetencionEmitidaResponse>> HandleAsync(RetencionEmitirCommand command)
    {
        var imprenta = FacturaImprenta.Leer(_config);

        // Art. 29.4: validar la estructura va ANTES de tocar el contador.
        var validacion = RetencionValidador.Validar(command);

        if (!validacion.EsValida)
        {
            // Un intento rechazado es una accion efectuada (Art. 18.2). Se
            // registra con el mismo mecanismo que la emision de documentos.
            await FacturaEmision.RegistrarRechazoAsync(
                _connectionDB, command.EmisorId, "retencion", command.UsuarioIns, validacion.Mensaje);

            return Falla(validacion.Mensaje);
        }

        string periodo = command.Periodo.Trim();
        string clave = (command.ClaveIdempotencia ?? string.Empty).Trim();

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

            // El agente de retencion es el emisor, y sus datos se copian al
            // comprobante: mismo principio de instantanea del Art. 29.3.
            FacturaEmisorDatos? agente = await FacturaEmision.LeerEmisorAsync(cn, tx, command.EmisorId);

            if (agente is null)
            {
                return await FallaEnTxAsync(tx, $"No existe un emisor con el identificador {command.EmisorId}.");
            }

            if (agente.Estado != "activo")
            {
                return await FallaEnTxAsync(tx, "El emisor está inactivo: no puede emitir comprobantes.");
            }

            // Contador del secuencial, bloqueado por agente y periodo (D-38).
            long ultimo;

            using (var cmd = new NpgsqlCommand(RetencionDb.SqlContadorBloquear, cn, tx))
            {
                cmd.Parameters.AddWithValue("emisor_id", command.EmisorId);
                cmd.Parameters.AddWithValue("periodo", periodo);

                using var reader = await cmd.ExecuteReaderAsync();
                await reader.ReadAsync();
                ultimo = reader.SafeGetInt64("ultimo_numero");
            }

            if (ultimo >= 99999999)
            {
                return await FallaEnTxAsync(tx,
                    $"La numeración de comprobantes del período {periodo} se agotó: se consumieron "
                    + "los 99.999.999 secuenciales que admite el formato del Artículo 11.1.");
            }

            long secuencial = ultimo + 1;
            string numeracion = RetencionDb.FormatearNumeracion(periodo, secuencial);

            using (var cmd = new NpgsqlCommand(RetencionDb.SqlContadorActualizar, cn, tx))
            {
                cmd.Parameters.AddWithValue("ultimo_numero", secuencial);
                cmd.Parameters.AddWithValue("emisor_id", command.EmisorId);
                cmd.Parameters.AddWithValue("periodo", periodo);

                await cmd.ExecuteNonQueryAsync();
            }

            // Los totales salen del desglose, no del request: pedirlos seria
            // dejar que quien llama declare un total que no cierra contra sus
            // propias lineas.
            var totales = RetencionDb.Sumar(command.Documentos);

            long retencionId;
            DateTime emitidoEn;

            using (var cmd = new NpgsqlCommand(RetencionDb.SqlRetencionInsert, cn, tx))
            {
                cmd.Parameters.AddWithValue("emisor_id", command.EmisorId);
                cmd.Parameters.AddWithValue("numeracion", numeracion);
                cmd.Parameters.AddWithValue("periodo", periodo);
                cmd.Parameters.AddWithValue("agente_rif", agente.Rif);
                cmd.Parameters.AddWithValue("agente_razon_social", agente.RazonSocial);
                cmd.Parameters.AddWithValue("agente_domicilio", agente.Domicilio);
                cmd.Parameters.AddWithValue("proveedor_rif", command.ProveedorRif.Trim());
                cmd.Parameters.AddWithValue("proveedor_razon_social", command.ProveedorRazonSocial.Trim());
                cmd.Parameters.AddWithValue("proveedor_domicilio", FacturacionElectronicaDb.DbValue(command.ProveedorDomicilio));
                cmd.Parameters.AddWithValue("proveedor_correo", FacturacionElectronicaDb.DbValue(command.ProveedorCorreo));
                cmd.Parameters.AddWithValue("total_documentos", totales.TotalDocumentos);
                cmd.Parameters.AddWithValue("total_base", totales.TotalBase);
                cmd.Parameters.AddWithValue("total_impuesto", totales.TotalImpuesto);
                cmd.Parameters.AddWithValue("total_retenido", totales.TotalRetenido);
                cmd.Parameters.AddWithValue("imprenta_rif", FacturacionElectronicaDb.DbValue(imprenta.Rif));
                cmd.Parameters.AddWithValue("imprenta_razon_social", FacturacionElectronicaDb.DbValue(imprenta.RazonSocial));
                cmd.Parameters.AddWithValue("imprenta_providencia", FacturacionElectronicaDb.DbValue(imprenta.Providencia));
                cmd.Parameters.AddWithValue("es_prueba", !imprenta.EsDefinitivo);
                cmd.Parameters.AddWithValue("clave_idempotencia", FacturacionElectronicaDb.DbValue(clave));
                cmd.Parameters.AddWithValue("usuario_ins", FacturacionElectronicaDb.DbValue(command.UsuarioIns));

                using var reader = await cmd.ExecuteReaderAsync();
                await reader.ReadAsync();

                retencionId = reader.SafeGetInt64("id");
                emitidoEn = reader.GetDateTime(reader.GetOrdinal("emitido_en"));
            }

            // Los documentos retenidos (11.5, 11.6, 11.8).
            for (int i = 0; i < command.Documentos.Count; i++)
            {
                var doc = command.Documentos[i];

                using var cmd = new NpgsqlCommand(RetencionDb.SqlDetalleInsert, cn, tx);
                cmd.Parameters.AddWithValue("retencion_id", retencionId);
                cmd.Parameters.AddWithValue("orden", i + 1);
                cmd.Parameters.AddWithValue("documento_numero", doc.DocumentoNumero.Trim());
                cmd.Parameters.AddWithValue("documento_control", doc.DocumentoControl.Trim());
                cmd.Parameters.AddWithValue("documento_fecha", doc.DocumentoFecha.Date);
                cmd.Parameters.AddWithValue("monto_total", doc.MontoTotal);
                cmd.Parameters.AddWithValue("base_imponible", doc.BaseImponible);
                cmd.Parameters.AddWithValue("impuesto_causado", doc.ImpuestoCausado);
                cmd.Parameters.AddWithValue("monto_retenido", doc.MontoRetenido);
                cmd.Parameters.AddWithValue("porcentaje", doc.Porcentaje);

                await cmd.ExecuteNonQueryAsync();
            }

            // Bitacora (Art. 18.2). El comprobante no vive en FED_DOCUMENTO, asi
            // que su id no puede ir en DOCUMENTO_ID -esa columna tiene foranea-.
            // Va en el detalle jsonb, que es libre.
            await FacturaEmision.RegistrarAsync(cn, tx, null, command.EmisorId, "emision", command.UsuarioIns,
                new
                {
                    documento = "retencion",
                    retencionId,
                    numeracion,
                    periodo,
                    totalRetenido = totales.TotalRetenido,
                    documentosRetenidos = command.Documentos.Count,
                    esPrueba = !imprenta.EsDefinitivo
                });

            await tx.CommitAsync();

            return Exito(new RetencionEmitidaResponse(
                retencionId,
                numeracion,
                periodo,
                FacturaFormato.FechaOchoDigitos(emitidoEn),
                FacturaFormato.HoraConMeridiano(emitidoEn),
                agente.Rif,
                agente.RazonSocial,
                command.ProveedorRif.Trim(),
                command.ProveedorRazonSocial.Trim(),
                totales.TotalDocumentos,
                totales.TotalBase,
                totales.TotalImpuesto,
                totales.TotalRetenido,
                command.Documentos.Count,
                !imprenta.EsDefinitivo,
                FacturaImprenta.MotivoDePrueba(imprenta, "11.9"),
                YaExistia: false));
        }
        catch (NpgsqlException ex) when (FacturacionElectronicaDb.EsClaveDuplicada(ex))
        {
            string restriccion = FacturacionElectronicaDb.NombreRestriccion(ex);

            // Carrera de idempotencia: otra peticion identica gano.
            if (restriccion == "fed_retencion_idem_uk" && clave.Length > 0)
            {
                var existente = await BuscarPorClaveAsync(cn, null, command.EmisorId, clave, imprenta);

                if (existente is not null)
                {
                    return Exito(existente);
                }
            }

            // Choque de numeracion. No deberia ocurrir -el contador se bloquea-
            // pero si ocurre es la invariante del documento y hay que verla.
            if (restriccion == "fed_retencion_uk")
            {
                return Falla("El agente ya tiene un comprobante con esa numeración.");
            }

            return Falla($"Error técnico: {ex.Message}");
        }
        catch (Exception ex)
        {
            return Falla($"Error técnico: {ex.Message}");
        }
    }

    // ------------------------------------------------------------------

    private static async Task<RetencionEmitidaResponse?> BuscarPorClaveAsync(
        NpgsqlConnection cn, NpgsqlTransaction? tx, long emisorId, string clave, FacturaImprentaDatos imprenta)
    {
        using var cmd = new NpgsqlCommand(RetencionDb.SqlRetencionPorClave, cn, tx);
        cmd.Parameters.AddWithValue("emisor_id", emisorId);
        cmd.Parameters.AddWithValue("clave", clave);

        using var reader = await cmd.ExecuteReaderAsync();

        return await reader.ReadAsync() ? RetencionDb.MapPorClave(reader, imprenta) : null;
    }

    private static async Task<ResultDto<RetencionEmitidaResponse>> FallaEnTxAsync(
        NpgsqlTransaction tx, string mensaje)
    {
        await tx.RollbackAsync();

        return Falla(mensaje);
    }

    private static ResultDto<RetencionEmitidaResponse> Exito(RetencionEmitidaResponse dato) =>
        new(dato) { IsValid = true, Message = FacturacionElectronicaDb.MensajeExito };

    private static ResultDto<RetencionEmitidaResponse> Falla(string mensaje) =>
        new(null!) { Data = null, IsValid = false, Message = mensaje };
}

[ApiController]
[Authorize]
[Route("api/FacturacionElectronica")]
public class FacturacionElectronicaRetencionCreateController(ConnectionDB _connectionDB, IConfiguration _config) : ControllerBase
{
    [HttpPost]
    [Route("retencionCreate")]
    public async Task<IActionResult> RetencionCreate(RetencionEmitirCommand value)
    {
        var handler = new FacturacionElectronicaRetencionCreateHandler(_connectionDB, _config);
        var result = await handler.HandleAsync(value);

        return Ok(result);
    }
}
