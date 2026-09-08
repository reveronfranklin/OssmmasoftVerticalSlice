using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.FacturacionElectronica;

// T6.3 y T6.4 - emision de una guia de despacho. Art. 10 de la Providencia
// SNAT/2024/000102. DOC-4.
//
// POR QUE ES UNA RUTA PROPIA Y NO UN TIPO MAS DE facturaCreate (D-44). Es el
// mismo razonamiento que cerro T5.3 para las notas: la guia necesita datos que
// esa via no pide -motivo del traslado, receptor con RIF, la medida por
// renglon- y una fila en FED_GUIA_DESPACHO que tiene que entrar en la MISMA
// transaccion que el documento. Emitida por facturaCreate quedaria sin nada de
// eso, y en este modulo eso no se corrige despues: no hay UPDATE sobre
// FED_DOCUMENTO.
//
// Y no es hipotetico: el documento 30 se emitio asi el 2026-09-07, con precio,
// alicuota, base e IVA, y sin la expresion del 10.3, sin la medida del 10.4 y
// sin el RIF del 10.5. Queda como dato de prueba porque no tiene arreglo.
//
// LO QUE ESTE DOCUMENTO NO LLEVA. El Art. 10.2 remite a los numerales 2, 3, 4,
// 5, 6 y 14 del Art. 7, y a ninguno mas. No al 7.8 -descripcion, cantidad y
// precio-, que el 10.4 reemplaza; no al 7.10, 7.11, 7.12 ni 7.13 -ajustes, base
// imponible, IVA y valor total-. Por eso el motor recibe los renglones con
// precio y alicuota en cero, y los totales del documento salen en cero.
//
// SI LLEVA NUMERO DE CONTROL, a diferencia del comprobante de retencion: el 10.2
// remite a los numerales 4 y 5 expresamente. Por eso reusa el mismo motor de
// emision que la factura y la nota, con la asignacion dentro de la transaccion.
public class FacturacionElectronicaGuiaCreateHandler(ConnectionDB _connectionDB, IConfiguration _config)
{
    private const string Tipo = "entrega";

    public async Task<ResultDto<GuiaEmitidaResponse>> HandleAsync(GuiaEmitirCommand command)
    {
        var imprenta = FacturaImprenta.Leer(_config);

        string serie = (command.Serie ?? string.Empty).Trim();
        string clave = (command.ClaveIdempotencia ?? string.Empty).Trim();
        string motivo = (command.MotivoTraslado ?? string.Empty).Trim();
        string destino = (command.Destino ?? string.Empty).Trim();

        // Art. 29.4: validar la estructura va ANTES de tocar la numeracion.
        var validacion = GuiaValidador.Validar(command);

        if (!validacion.EsValida)
        {
            // Un intento rechazado es una accion efectuada (Art. 18.2).
            await FacturaEmision.RegistrarRechazoAsync(
                _connectionDB, command.EmisorId, Tipo, command.UsuarioIns, validacion.Mensaje);

            return Falla(validacion.Mensaje);
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
            // Idempotencia, camino rapido. Sin esto, un doble clic ampara el mismo
            // traslado con dos guias, y una de las dos es un documento fiscal que
            // no corresponde a ningun despacho.
            if (clave.Length > 0)
            {
                var yaEmitida = await BuscarPorClaveAsync(cn, command.EmisorId, clave, imprenta);

                if (yaEmitida is not null)
                {
                    return Exito(yaEmitida);
                }
            }

            using var tx = await cn.BeginTransactionAsync();

            FacturaEmisorDatos? emisor = await FacturaEmision.LeerEmisorAsync(cn, tx, command.EmisorId);

            if (emisor is null)
            {
                return await FallaEnTxAsync(tx, $"No existe un emisor con el identificador {command.EmisorId}.");
            }

            if (emisor.Estado != "activo")
            {
                return await FallaEnTxAsync(tx, "El emisor está inactivo: no puede emitir documentos.");
            }

            // Los renglones, sin precio ni alicuota. Los totales salen en cero por
            // construccion y no por una resta: no hay monto que restar.
            var renglones = command.Renglones.Select(GuiaDb.ComoRenglon).ToList();
            var totales = FacturaCalculo.Calcular(renglones);

            var comando = ComandoEquivalente(command, renglones, serie, clave);

            var (numeracion, fallaNumeracion) = await FacturaEmision.ResolverNumeracionAsync(
                cn, tx, emisor, command.EmisorId, Tipo, serie, command.NumeracionExterna);

            if (fallaNumeracion is not null)
            {
                return await FallaEnTxAsync(tx, fallaNumeracion);
            }

            var (documentoId, emitidoEn) = await FacturaEmision.InsertarDocumentoAsync(
                cn, tx, comando, Tipo, serie, numeracion, clave, emisor, totales, imprenta);

            await FacturaEmision.InsertarRenglonesAsync(cn, tx, documentoId, renglones, totales);

            // NO se insertan impuestos, y la ausencia es deliberada: el Art. 10.2
            // no remite al 7.11 ni al 7.12, asi que una guia no tiene desglose por
            // alicuota que registrar. Escribir filas en cero seria afirmar que el
            // documento discrimina un impuesto que no causa.

            // El motivo y el destino, en la MISMA transaccion que el documento.
            // Una guia sin motivo no debe poder existir ni un instante, igual que
            // un documento sin numero de control.
            using (var cmd = new NpgsqlCommand(GuiaDb.SqlGuiaInsert, cn, tx))
            {
                cmd.Parameters.AddWithValue("documento_id", documentoId);
                cmd.Parameters.AddWithValue("motivo_traslado", motivo);
                cmd.Parameters.AddWithValue("destino", FacturacionElectronicaDb.DbValue(destino));
                cmd.Parameters.AddWithValue("usuario_ins", FacturacionElectronicaDb.DbValue(command.UsuarioIns));

                await cmd.ExecuteNonQueryAsync();
            }

            var numeroControl = await FacturaEmision.AsignarNumeroControlAsync(
                cn, tx, command.EmisorId, Tipo, documentoId, command.UsuarioIns);

            if (numeroControl is null)
            {
                return await FallaEnTxAsync(tx,
                    "La secuencia de números de control del emisor se agotó: se consumieron "
                    + "los 99 identificadores de dos dígitos.");
            }

            await FacturaEmision.RegistrarAsync(cn, tx, documentoId, command.EmisorId, "emision", command.UsuarioIns,
                new
                {
                    numeracion,
                    serie,
                    numeroControl = numeroControl.Value.Numero,
                    motivoTraslado = motivo,
                    destino,
                    renglones = command.Renglones.Count,
                    esPrueba = !imprenta.EsDefinitivo
                });

            await tx.CommitAsync();

            return Exito(Armar(
                documentoId, serie, numeracion, emitidoEn, numeroControl.Value.Numero,
                numeroControl.Value.Fecha, command, motivo, destino, imprenta, yaExistia: false));
        }
        catch (NpgsqlException ex) when (FacturacionElectronicaDb.EsClaveDuplicada(ex))
        {
            // Dos solicitudes con la misma clave llegaron a la vez y la segunda
            // choco contra el UNIQUE. Se devuelve la que gano.
            var existente = await BuscarPorClaveAsync(cn, command.EmisorId, clave, imprenta);

            return existente is not null
                ? Exito(existente)
                : Falla($"Error técnico al emitir la guía de despacho: {ex.Message}");
        }
        catch (Exception ex)
        {
            return Falla($"Error técnico al emitir la guía de despacho: {ex.Message}");
        }
    }

    // La guia expresada como el comando que el motor de emision entiende. El
    // receptor del 10.5 viaja en las columnas ADQ_*, que son el mismo lugar del
    // documento con otro nombre legal (D-41).
    private static FacturaEmitirCommand ComandoEquivalente(
        GuiaEmitirCommand command, List<FacturaRenglonCommand> renglones, string serie, string clave) =>
        new(command.EmisorId,
            Tipo,
            renglones,
            serie,
            command.NumeracionExterna,
            command.ReceptorNombre,
            command.ReceptorRif,
            AdqDocumentoId: string.Empty,
            command.UsuarioIns,
            clave);

    // Idempotencia: la guia ya emitida con esta clave, reconstruida.
    private static async Task<GuiaEmitidaResponse?> BuscarPorClaveAsync(
        NpgsqlConnection cn, long emisorId, string clave, FacturaImprentaDatos imprenta)
    {
        var documento = await FacturaEmision.BuscarPorClaveAsync(cn, null, emisorId, clave, imprenta);

        if (documento is null)
        {
            return null;
        }

        var datos = new GuiaDatos(string.Empty, string.Empty, string.Empty, string.Empty, 0);

        using (var cmd = new NpgsqlCommand(GuiaDb.SqlGuiaPorDocumento, cn))
        {
            cmd.Parameters.AddWithValue("documento_id", documento.DocumentoId);

            using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                datos = GuiaDb.MapGuia(reader);
            }
        }

        return new GuiaEmitidaResponse(
            documento.DocumentoId,
            documento.Numeracion,
            documento.NumeracionConSerie,
            documento.NumeroControl,
            documento.NumeroControlTexto,
            documento.RangoNumerosControl,
            documento.Denominacion,
            documento.FechaEmision8d,
            documento.HoraEmision,
            documento.FechaAsignacion8d,
            datos.ReceptorNombre,
            datos.ReceptorRif,
            datos.MotivoTraslado,
            datos.Destino,
            datos.CantidadRenglones,
            FacturaFormato.LeyendaSinCreditoFiscal,
            FacturaFormato.LeyendaProvidencia,
            documento.EsPrueba,
            documento.MotivoPrueba,
            YaExistia: true);
    }

    private static GuiaEmitidaResponse Armar(
        long documentoId, string serie, string numeracion, DateTime emitidoEn,
        string numeroControl, DateTime fechaAsignacion, GuiaEmitirCommand command,
        string motivo, string destino, FacturaImprentaDatos imprenta, bool yaExistia) => new(
            documentoId,
            numeracion,
            FacturaFormato.NumeracionConSerie(serie, numeracion),
            numeroControl,
            $"N° de Control {numeroControl}",
            FacturaFormato.RangoNumerosControl(numeroControl),
            FacturaFormato.Denominacion(Tipo),
            FacturaFormato.FechaOchoDigitos(emitidoEn),
            FacturaFormato.HoraConMeridiano(emitidoEn),
            FacturaFormato.FechaOchoDigitos(fechaAsignacion),
            command.ReceptorNombre,
            command.ReceptorRif,
            motivo,
            destino,
            command.Renglones.Count,
            FacturaFormato.LeyendaSinCreditoFiscal,
            FacturaFormato.LeyendaProvidencia,
            !imprenta.EsDefinitivo,
            FacturaImprenta.MotivoDePrueba(imprenta),
            yaExistia);

    private static ResultDto<GuiaEmitidaResponse> Exito(GuiaEmitidaResponse data) =>
        new(data) { IsValid = true, Message = FacturacionElectronicaDb.MensajeExito };

    private static ResultDto<GuiaEmitidaResponse> Falla(string mensaje) =>
        new(null!) { Data = null, IsValid = false, Message = mensaje };

    private static async Task<ResultDto<GuiaEmitidaResponse>> FallaEnTxAsync(NpgsqlTransaction tx, string mensaje)
    {
        await tx.RollbackAsync();

        return Falla(mensaje);
    }
}

[ApiController]
[Authorize]
[Route("api/FacturacionElectronica")]
public class FacturacionElectronicaGuiaCreateController(ConnectionDB _connectionDB, IConfiguration _config)
    : ControllerBase
{
    [HttpPost]
    [Route("guiaCreate")]
    public async Task<IActionResult> GuiaCreate(GuiaEmitirCommand value)
    {
        var handler = new FacturacionElectronicaGuiaCreateHandler(_connectionDB, _config);
        var result = await handler.HandleAsync(value);

        return Ok(result);
    }
}
