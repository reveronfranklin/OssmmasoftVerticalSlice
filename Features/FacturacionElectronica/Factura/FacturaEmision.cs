using System.Text.Json;
using Npgsql;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.FacturacionElectronica;

// T5.8 - el motor de emision de un documento fiscal, compartido por las dos
// operaciones que emiten: facturaCreate y notaCreate.
//
// POR QUE EXISTE ESTE ARCHIVO. INV-3 -nunca mas de un ejemplar del mismo
// documento, Art. 21.2, causal de revocatoria DEL CLIENTE EMISOR- no vive en el
// validador ni en el calculo: vive aca, en la transaccion unica, en las dos
// rutas de idempotencia y en la asignacion del numero de control dentro del
// mismo commit. Si cada operacion tuviera su copia, un bug de INV-3 habria que
// arreglarlo en dos lugares, y el que se olvide queda emitiendo duplicados.
//
// NO ES UNA CAPA NUEVA, y eso importa por el estandar del proyecto. Es una clase
// estatica con funciones que reciben la conexion y la transaccion abiertas, igual
// que los helpers privados que estaban dentro del handler. No hay interfaz, no hay
// inyeccion, no hay repositorio: quien orquesta sigue siendo el handler de cada
// operacion, que decide el orden de los pasos y con que validador.
//
// El precedente de compartir el mecanismo en vez de llamar al handler vecino ya
// estaba en el modulo: AsignarNumeroControl reusa el SQL y el calculo de la Fase
// 2 en lugar de invocar su handler, porque ese abre su propia conexion y dos
// conexiones son dos transacciones.
//
// QUE NO ESTA ACA, a proposito: el orden de los pasos, la eleccion del validador
// y la construccion del comando. Eso es lo propio de cada operacion y es lo que
// justifica que sean dos y no una.
public static class FacturaEmision
{
    // ------------------------------------------------------------------
    // Emisor
    // ------------------------------------------------------------------

    // Se lee DENTRO de la transaccion: sus datos se copian al documento (Art.
    // 29.3) y no pueden cambiar a mitad de la emision.
    public static async Task<FacturaEmisorDatos?> LeerEmisorAsync(
        NpgsqlConnection cn, NpgsqlTransaction tx, long emisorId)
    {
        using var cmd = new NpgsqlCommand(FacturaDb.SqlEmisorParaEmitir, cn, tx);
        cmd.Parameters.AddWithValue("emisor_id", emisorId);

        using var reader = await cmd.ExecuteReaderAsync();

        return await reader.ReadAsync() ? FacturaDb.MapEmisor(reader) : null;
    }

    // ------------------------------------------------------------------
    // Numeracion propia del documento (Art. 7.2, D-21)
    // ------------------------------------------------------------------

    public static async Task<string> SiguienteNumeracionAsync(
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

    // Resuelve la numeracion segun el modo del emisor. Devuelve null en el
    // segundo elemento cuando todo salio bien; si no, el mensaje de la falla.
    //
    // Se comparte porque la regla es del emisor y no del tipo de documento: un
    // emisor en modo 'externa' trae su numeracion tanto para una factura como
    // para una nota, y mezclar los dos modos sobre la misma serie es como se
    // choca INV-3.
    public static async Task<(string Numeracion, string? Falla)> ResolverNumeracionAsync(
        NpgsqlConnection cn, NpgsqlTransaction tx, FacturaEmisorDatos emisor,
        long emisorId, string tipo, string serie, string numeracionExterna)
    {
        if (emisor.ModoNumeracion == "externa")
        {
            if (string.IsNullOrWhiteSpace(numeracionExterna))
            {
                return (string.Empty,
                    "El emisor está configurado para traer su propia numeración y la solicitud no la incluye.");
            }

            return (numeracionExterna.Trim(), null);
        }

        if (!string.IsNullOrWhiteSpace(numeracionExterna))
        {
            // No se acepta en silencio: aceptarla aca y generar en otra peticion
            // es como se choca la numeracion de una misma serie.
            return (string.Empty,
                "El emisor está configurado para que el sistema genere la numeración, "
                + "así que la solicitud no debe traer una.");
        }

        return (await SiguienteNumeracionAsync(cn, tx, emisorId, tipo, serie), null);
    }

    // ------------------------------------------------------------------
    // Persistencia del documento
    // ------------------------------------------------------------------

    // La instantanea del emisor y de la imprenta se escribe aca: lo estampado es
    // un hecho historico y no puede cambiar porque manana el emisor mude su
    // domicilio.
    public static async Task<(long DocumentoId, DateTime EmitidoEn)> InsertarDocumentoAsync(
        NpgsqlConnection cn, NpgsqlTransaction tx, FacturaEmitirCommand command,
        string tipo, string serie, string numeracion, string clave,
        FacturaEmisorDatos emisor, FacturaTotales totales, FacturaImprentaDatos imprenta)
    {
        using var cmd = new NpgsqlCommand(FacturaDb.SqlDocumentoInsert, cn, tx);
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

        // Moneda (D-30). Vacio es bolivares, y en bolivares los otros dos van
        // NULL: el CHECK de la tabla exige esa coherencia en los dos sentidos.
        string moneda = (command.Moneda ?? string.Empty).Trim().ToUpperInvariant();

        if (moneda.Length == 0)
        {
            moneda = "VES";
        }

        cmd.Parameters.AddWithValue("moneda", moneda);
        cmd.Parameters.AddWithValue("tasa_cambio",
            moneda == "VES" ? DBNull.Value : command.TasaCambio);
        cmd.Parameters.AddWithValue("total_moneda",
            moneda == "VES"
                ? DBNull.Value
                : FacturaCalculo.ConvertirAMoneda(totales.TotalGeneral, command.TasaCambio));

        using var reader = await cmd.ExecuteReaderAsync();
        await reader.ReadAsync();

        return (reader.SafeGetInt64("id"), reader.GetDateTime(reader.GetOrdinal("emitido_en")));
    }

    // Renglones (Arts. 7.8 a 7.10).
    public static async Task InsertarRenglonesAsync(
        NpgsqlConnection cn, NpgsqlTransaction tx, long documentoId,
        List<FacturaRenglonCommand> renglones, FacturaTotales totales)
    {
        for (int i = 0; i < renglones.Count; i++)
        {
            var renglon = renglones[i];

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

            // Art. 10.4. Las tres van juntas o no va ninguna: el CHECK de la
            // tabla no admite una medida a medias, porque una unidad sin valor no
            // senala nada.
            bool conMedida = renglon.MedidaTipo.Trim().Length > 0;
            cmd.Parameters.AddWithValue("medida_tipo", conMedida ? renglon.MedidaTipo.Trim().ToLowerInvariant() : DBNull.Value);
            cmd.Parameters.AddWithValue("medida_valor", conMedida ? renglon.MedidaValor : DBNull.Value);
            cmd.Parameters.AddWithValue("medida_unidad", conMedida ? renglon.MedidaUnidad.Trim() : DBNull.Value);

            await cmd.ExecuteNonQueryAsync();
        }
    }

    // Desglose por alicuota (Arts. 7.11 y 7.12). Una fila por tasa.
    public static async Task InsertarImpuestosAsync(
        NpgsqlConnection cn, NpgsqlTransaction tx, long documentoId, FacturaTotales totales)
    {
        foreach (var grupo in totales.PorAlicuota)
        {
            using var cmd = new NpgsqlCommand(FacturaDb.SqlImpuestoInsert, cn, tx);
            cmd.Parameters.AddWithValue("documento_id", documentoId);
            cmd.Parameters.AddWithValue("alicuota", grupo.Alicuota);
            cmd.Parameters.AddWithValue("base_imponible", grupo.BaseImponible);
            cmd.Parameters.AddWithValue("monto_iva", grupo.MontoIva);

            await cmd.ExecuteNonQueryAsync();
        }
    }

    // ------------------------------------------------------------------
    // Numero de control (Art. 7.4), en la MISMA transaccion
    // ------------------------------------------------------------------

    // Devuelve null si la secuencia del emisor se agoto -99 identificadores por
    // 99.999.999 secuenciales-.
    public static async Task<(string Numero, DateTime Fecha)?> AsignarNumeroControlAsync(
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

    // ------------------------------------------------------------------
    // Idempotencia
    // ------------------------------------------------------------------

    public static async Task<FacturaEmitidaResponse?> BuscarPorClaveAsync(
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

        // El numero de control que el documento YA tiene. La respuesta de un
        // reintento tiene que traerlo: sin el no se puede imprimir un documento
        // conforme, y es lo unico que la imprenta digital aporta.
        //
        // Sale vacio solo si el documento no tiene numero asignado, y eso seria
        // una anomalia: la emision los crea juntos en una transaccion. Cuando
        // pasa, la fecha de asignacion cae a la de emision, que es lo mas cercano
        // a la verdad que hay.
        string numeroControl = reader.SafeGetString("numero_control");
        int ordinalFecha = reader.GetOrdinal("fecha_asignacion");

        DateTime fechaAsignacion = reader.IsDBNull(ordinalFecha)
            ? emitidoEn
            : reader.GetDateTime(ordinalFecha);

        return Armar(documentoId, tipo, serie, numeracion, emitidoEn, totales,
            numeroControl, fechaAsignacion, imprenta, yaExistia: true);
    }

    // El catch del UNIQUE. ESTE es el camino que garantiza INV-3 -la consulta
    // previa solo evita trabajo- y por eso vive en un solo lugar: un error de
    // interpretacion de las restricciones se arregla una vez.
    //
    // Devuelve la respuesta cuando la carrera se resolvio devolviendo el
    // documento que quedo, o el mensaje de falla cuando no hay nada que devolver.
    public static async Task<(FacturaEmitidaResponse? Existente, string? Falla)> ResolverClaveDuplicadaAsync(
        NpgsqlConnection cn, NpgsqlException ex, long emisorId, string clave,
        string tipo, string numeracionExterna, FacturaImprentaDatos imprenta)
    {
        string restriccion = FacturacionElectronicaDb.NombreRestriccion(ex);

        // Carrera de idempotencia: otra peticion identica gano. Se devuelve el
        // documento que quedo, no uno nuevo.
        if (restriccion == "fed_documento_idem_uk" && clave.Length > 0)
        {
            var existente = await BuscarPorClaveAsync(cn, null, emisorId, clave, imprenta);

            if (existente is not null)
            {
                return (existente, null);
            }
        }

        // Choque de la numeracion propia: el emisor externo trajo una que ya uso.
        // Es INV-3 tambien, y aca no hay nada que devolver: el documento que
        // existe es OTRO documento con la misma numeracion.
        if (restriccion == "fed_documento_uk")
        {
            return (null, $"El emisor ya tiene un documento {tipo} con la numeración {numeracionExterna}.");
        }

        return (null, $"Error técnico: {ex.Message}");
    }

    // ------------------------------------------------------------------
    // Respuesta
    // ------------------------------------------------------------------

    public static FacturaEmitidaResponse Armar(
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

    // ------------------------------------------------------------------
    // Bitacora (Art. 18.2)
    // ------------------------------------------------------------------

    public static async Task RegistrarAsync(
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
    public static async Task RegistrarRechazoAsync(
        ConnectionDB conexiones, long emisorId, string tipoDocumento, string usuario, string motivo)
    {
        try
        {
            using var cn = conexiones.GetFedConnection();
            await cn.OpenAsync();

            await RegistrarAsync(cn, null, null, emisorId, "rechazo", usuario,
                new { motivo, tipoDocumento });
        }
        catch
        {
            // Silencio deliberado: el rechazo ya viaja en la respuesta. Hacer
            // fallar la peticion porque no se pudo auditar el fallo cambiaria un
            // mensaje util por un error tecnico.
        }
    }

    // ------------------------------------------------------------------
    // Resultados
    // ------------------------------------------------------------------

    public static async Task<ResultDto<FacturaEmitidaResponse>> FallaEnTxAsync(
        NpgsqlTransaction tx, string mensaje)
    {
        await tx.RollbackAsync();

        return Falla(mensaje);
    }

    public static ResultDto<FacturaEmitidaResponse> Exito(FacturaEmitidaResponse dato) =>
        new(dato) { IsValid = true, Message = FacturacionElectronicaDb.MensajeExito };

    public static ResultDto<FacturaEmitidaResponse> Falla(string mensaje) =>
        new(null!) { Data = null, IsValid = false, Message = mensaje };
}
