using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Features.Support;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.FacturacionElectronica;

// T7.3 - remitir el documento al usuario final por correo. Art. 18.9.
//
// SE MANDA UN ENLACE, NO UN ADJUNTO, y no es una preferencia: la cola de correo
// del ERP -SIS_EMAIL_QUEUE- tiene BODY_HTML y BODY_TEXT y NADA MAS. No hay
// columna de adjunto ni tabla de adjuntos, y la tarea es explicita en no crear un
// mecanismo de correo nuevo.
//
// El Art. 18.9 admite las dos formas con una "o": remitir el comprobante digital
// a una cuenta de correo electronico **o** ponerlo a disposicion del usuario
// final mediante pagina web u otro medio. El enlace cumple por la segunda via, y
// ademas la primera lo transporta. Cuando la cola soporte adjuntos, adjuntar el
// PDF es agregar un parametro, no rehacer esto.
//
// SE REUSA EL STORED PROCEDURE, NO SE COPIA EL MECANISMO. La insercion en la cola
// vive privada dentro de EmailQueueController, asi que no se puede invocar desde
// aca sin un salto HTTP. Se llama al MISMO `SIS.SP_EMAIL_Q_INS` con los mismos
// parametros: misma cola, mismo worker, misma configuracion SMTP. Tocar
// Features/Email para extraer un helper dejaria rastro del modulo fuera de su
// carpeta, y este modulo tiene que poder desaparecer sin dejar ninguno.
//
// LA COLA ES ORACLE Y EL DOCUMENTO ES POSTGRES. Son dos conexiones y no hay
// transaccion que las abarque, asi que el orden importa: primero se encola -si
// eso falla, no se registra nada- y despues se deja el asiento en la bitacora. Un
// correo encolado sin asiento es un correo que se envia; un asiento sin correo
// seria una constancia falsa de entrega, y eso es lo que no puede pasar.
public record DocumentoEnviarCommand(
    long Id,
    string CorreoDestino = "",
    string Tipo = EnlacePublico.TipoDocumento,
    string UsuarioIns = "");

public record DocumentoEnviarResponse(
    long EmailId,
    string CorreoDestino,
    string Denominacion,
    string Numeracion,
    string Url,
    bool EsPrueba);

public class FacturacionElectronicaDocumentoEnviarHandler(ConnectionDB _connectionDB, IConfiguration _config)
{
    public async Task<ResultDto<DocumentoEnviarResponse>> HandleAsync(DocumentoEnviarCommand command)
    {
        if (command.Id <= 0)
        {
            return Falla("Falta el identificador del documento.");
        }

        if (!EnlacePublico.Configurado(_config))
        {
            return Falla("No se puede enviar: falta configurar el secreto del enlace de consulta pública.");
        }

        string tipo = command.Tipo == EnlacePublico.TipoRetencion
            ? EnlacePublico.TipoRetencion
            : EnlacePublico.TipoDocumento;

        using var cn = _connectionDB.GetFedConnection();

        try
        {
            await cn.OpenAsync();
        }
        catch (Exception ex)
        {
            return Falla($"Error técnico al abrir conexión FED: {ex.Message}");
        }

        // Los datos que van en el cuerpo del correo salen del documento, no del
        // request: quien envia no decide que dice el correo sobre el documento.
        var datos = tipo == EnlacePublico.TipoRetencion
            ? await LeerComprobanteAsync(cn, command.Id)
            : await LeerDocumentoAsync(cn, command.Id);

        if (datos is null)
        {
            return Falla($"No existe el documento con el identificador {command.Id}.");
        }

        // El correo del request manda; si no viene, se usa el que el documento ya
        // tiene. Solo el comprobante de retencion tiene uno: el Art. 11.4 lo exige
        // y por eso esta en la tabla. La factura no lo tiene, y por eso ahi es
        // obligatorio indicarlo.
        string destino = (command.CorreoDestino ?? string.Empty).Trim();

        if (destino.Length == 0)
        {
            destino = datos.CorreoConocido;
        }

        if (destino.Length == 0)
        {
            return Falla("Falta el correo del destinatario: este documento no tiene uno registrado.");
        }

        if (!destino.Contains('@') || destino.StartsWith('@') || destino.EndsWith('@'))
        {
            return Falla($"El correo «{destino}» no tiene un formato válido.");
        }

        string url = EnlacePublico.Url(_config, tipo, command.Id);
        string asunto = $"{datos.Denominacion} N° {datos.Numeracion} · {datos.EmisorRazonSocial}";

        var encolado = await EncolarAsync(destino, datos, asunto, url, command.Id);

        if (!encolado.IsValid)
        {
            return Falla($"No se pudo encolar el correo: {encolado.Message}");
        }

        // Art. 18.2 - toda accion se registra. El envio es una accion, y por eso
        // el script 20 la agrego al CHECK de la bitacora.
        //
        // VA DESPUES DE ENCOLAR Y NO PUEDE TUMBAR LA RESPUESTA. Son dos motores
        // distintos -la cola es Oracle, el documento es Postgres- y no hay
        // transaccion que los abarque, asi que cuando el asiento falla el correo
        // YA ESTA ENCOLADO y se va a enviar igual.
        //
        // La primera version dejaba escapar la excepcion: el correo salia y quien
        // llamo recibia un error 500. Eso es lo peor de los dos mundos, porque
        // invita a reintentar y a mandar el documento dos veces. Ahora el fallo
        // del asiento degrada a un aviso en el mensaje, que es lo unico honesto:
        // el correo se encolo, y de eso no hay constancia.
        string aviso = string.Empty;

        if (tipo == EnlacePublico.TipoDocumento)
        {
            try
            {
                await FacturaEmision.RegistrarAsync(cn, null, command.Id, datos.EmisorId, "envio",
                    command.UsuarioIns,
                    new { correoDestino = destino, emailId = encolado.Data, url, asunto });
            }
            catch (Exception ex)
            {
                aviso = " El correo quedó encolado, pero NO se pudo registrar en la bitácora: "
                    + PrimeraLinea(ex.Message);
            }
        }

        return new ResultDto<DocumentoEnviarResponse>(new DocumentoEnviarResponse(
            encolado.Data,
            destino,
            datos.Denominacion,
            datos.Numeracion,
            url,
            datos.EsPrueba))
        {
            IsValid = true,
            Message = FacturacionElectronicaDb.MensajeExito + aviso
        };
    }

    // ------------------------------------------------------------------
    // La cola del ERP. Mismo procedimiento que usa EmailQueueController.
    // ------------------------------------------------------------------

    private async Task<ResultDto<long>> EncolarAsync(
        string destino, DocumentoParaCorreo datos, string asunto, string url, long referencia)
    {
        if (!SupportDb.TryGetEmpresa(_config, out int empresa, out string errorEmpresa))
        {
            return new ResultDto<long>(0) { IsValid = false, Message = errorEmpresa };
        }

        try
        {
            using var cn = _connectionDB.GetSisConnection();
            await cn.OpenAsync();

            using var cmd = new OracleCommand("SIS.SP_EMAIL_Q_INS", cn)
            {
                CommandType = CommandType.StoredProcedure,
                BindByName = true
            };

            cmd.Parameters.Add("p_MODULO_ORIGEN", OracleDbType.Varchar2).Value = "FED";
            cmd.Parameters.Add("p_REFERENCIA_ID", OracleDbType.Int32).Value = (int)referencia;
            cmd.Parameters.Add("p_TO_EMAIL", OracleDbType.Varchar2).Value = destino;
            cmd.Parameters.Add("p_TO_NAME", OracleDbType.Varchar2).Value =
                SupportDb.StringDbValue(datos.ReceptorNombre);
            cmd.Parameters.Add("p_SUBJECT", OracleDbType.Varchar2).Value = asunto;
            cmd.Parameters.Add("p_BODY_HTML", OracleDbType.Clob).Value = CuerpoHtml(datos, url);
            cmd.Parameters.Add("p_BODY_TEXT", OracleDbType.Clob).Value = CuerpoTexto(datos, url);
            cmd.Parameters.Add("p_FECHA_PROGRAMADA", OracleDbType.Date).Value = DBNull.Value;
            cmd.Parameters.Add("p_CODIGO_EMPRESA", OracleDbType.Int32).Value = empresa;

            var pId = cmd.Parameters.Add("p_EMAIL_ID_OUT", OracleDbType.Int32, ParameterDirection.Output);
            var pMensaje = cmd.Parameters.Add("p_Message", OracleDbType.Varchar2, 4000, null, ParameterDirection.Output);

            await cmd.ExecuteNonQueryAsync();

            string mensaje = SupportDb.GetMessage(pMensaje);
            bool exito = SupportDb.IsSuccessMessage(mensaje);

            return new ResultDto<long>(exito ? SupportDb.GetIntOutput(pId) : 0)
            {
                IsValid = exito,
                Message = mensaje
            };
        }
        catch (Exception ex)
        {
            return new ResultDto<long>(0) { IsValid = false, Message = ex.Message };
        }
    }

    // ------------------------------------------------------------------
    // El cuerpo. Dice QUE documento es y COMO verlo; no repite el documento.
    // ------------------------------------------------------------------

    private static string CuerpoHtml(DocumentoParaCorreo d, string url)
    {
        string aviso = d.EsPrueba
            ? "<p style=\"border:1px solid #b26a00;padding:8px;color:#b26a00\"><b>Documento de prueba · "
              + "sin validez fiscal.</b> Falta la autorización del SENIAT.</p>"
            : string.Empty;

        string control = d.NumeroControl.Length > 0
            ? $"<tr><td style=\"padding:2px 8px 2px 0\">N° de Control</td><td><b>{d.NumeroControl}</b></td></tr>"
            : string.Empty;

        return $@"<div style=""font-family:Arial,Helvetica,sans-serif;font-size:14px;color:#222"">
  <p>Estimado(a) {d.ReceptorNombre},</p>
  <p>{d.EmisorRazonSocial} (RIF {d.EmisorRif}) ha emitido a su nombre el siguiente documento:</p>
  <table style=""font-size:14px"">
    <tr><td style=""padding:2px 8px 2px 0"">Documento</td><td><b>{d.Denominacion}</b></td></tr>
    <tr><td style=""padding:2px 8px 2px 0"">Numeración</td><td><b>{d.Numeracion}</b></td></tr>
    {control}
    <tr><td style=""padding:2px 8px 2px 0"">Fecha de emisión</td><td>{d.FechaEmision8d}</td></tr>
  </table>
  <p><a href=""{url}"" style=""display:inline-block;padding:10px 16px;background:#5a3fc0;color:#fff;
     text-decoration:none;border-radius:4px"">Ver y descargar el documento</a></p>
  <p style=""font-size:12px;color:#666"">Si el botón no funciona, copie este enlace en su navegador:<br>{url}</p>
  {aviso}
  <p style=""font-size:12px;color:#666"">Este enlace es personal. Consérvelo: da acceso a su documento.</p>
</div>";
    }

    private static string CuerpoTexto(DocumentoParaCorreo d, string url)
    {
        string control = d.NumeroControl.Length > 0 ? $"N° de Control: {d.NumeroControl}\n" : string.Empty;
        string aviso = d.EsPrueba ? "\nDOCUMENTO DE PRUEBA - SIN VALIDEZ FISCAL. Falta la autorización del SENIAT.\n" : string.Empty;

        return $"Estimado(a) {d.ReceptorNombre},\n\n"
            + $"{d.EmisorRazonSocial} (RIF {d.EmisorRif}) ha emitido a su nombre el siguiente documento:\n\n"
            + $"{d.Denominacion}\nNumeración: {d.Numeracion}\n{control}"
            + $"Fecha de emisión: {d.FechaEmision8d}\n\n"
            + $"Véalo y descárguelo aquí:\n{url}\n"
            + aviso
            + "\nEste enlace es personal. Consérvelo: da acceso a su documento.\n";
    }

    // ------------------------------------------------------------------
    // Lectura
    // ------------------------------------------------------------------

    private static async Task<DocumentoParaCorreo?> LeerDocumentoAsync(NpgsqlConnection cn, long id)
    {
        const string sql = @"
            SELECT d.EMISOR_ID, d.TIPO_DOCUMENTO, d.SERIE, d.NUMERACION, d.EMITIDO_EN,
                   d.EMISOR_RIF, d.EMISOR_RAZON_SOCIAL, d.ADQ_NOMBRE, d.ES_PRUEBA,
                   COALESCE(nc.IDENTIFICADOR || '-' || LPAD(nc.SECUENCIAL::text, 8, '0'), '') AS NUMERO_CONTROL
            FROM FED.FED_DOCUMENTO d
            LEFT JOIN FED.FED_NUM_CONTROL nc ON nc.DOCUMENTO_ID = d.ID
            WHERE d.ID = @id;";

        using var cmd = new NpgsqlCommand(sql, cn);
        cmd.Parameters.AddWithValue("id", id);

        using var reader = await cmd.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return null;
        }

        string tipo = reader.SafeGetString("tipo_documento");
        var emitidoEn = reader.GetDateTime(reader.GetOrdinal("emitido_en"));

        return new DocumentoParaCorreo(
            reader.SafeGetInt64("emisor_id"),
            FacturaFormato.Denominacion(tipo),
            FacturaFormato.NumeracionConSerie(reader.SafeGetString("serie"), reader.SafeGetString("numeracion")),
            reader.SafeGetString("numero_control"),
            FacturaFormato.FechaOchoDigitos(emitidoEn),
            reader.SafeGetString("emisor_rif"),
            reader.SafeGetString("emisor_razon_social"),
            reader.SafeGetString("adq_nombre"),

            // La factura no guarda correo del adquiriente: el Art. 7.7 no lo pide.
            string.Empty,
            reader.SafeGetBoolean("es_prueba"));
    }

    private static async Task<DocumentoParaCorreo?> LeerComprobanteAsync(NpgsqlConnection cn, long id)
    {
        const string sql = @"
            SELECT EMISOR_ID, NUMERACION, EMITIDO_EN, AGENTE_RIF, AGENTE_RAZON_SOCIAL,
                   PROVEEDOR_RAZON_SOCIAL, COALESCE(PROVEEDOR_CORREO, '') AS PROVEEDOR_CORREO, ES_PRUEBA
            FROM FED.FED_RETENCION
            WHERE ID = @id;";

        using var cmd = new NpgsqlCommand(sql, cn);
        cmd.Parameters.AddWithValue("id", id);

        using var reader = await cmd.ExecuteReaderAsync();

        if (!await reader.ReadAsync())
        {
            return null;
        }

        return new DocumentoParaCorreo(
            reader.SafeGetInt64("emisor_id"),
            "COMPROBANTE DE RETENCIÓN",
            reader.SafeGetString("numeracion"),

            // No lleva, y no es que falte.
            string.Empty,
            FacturaFormato.FechaOchoDigitos(reader.GetDateTime(reader.GetOrdinal("emitido_en"))),
            reader.SafeGetString("agente_rif"),
            reader.SafeGetString("agente_razon_social"),
            reader.SafeGetString("proveedor_razon_social"),

            // Este SI tiene correo: el Art. 11.4 lo exige y por eso esta en la tabla.
            reader.SafeGetString("proveedor_correo"),
            reader.SafeGetBoolean("es_prueba"));
    }

    // CR y LF por codigo y no por escape: este archivo se genero por guion y
    // los escapes de barra invertida se comieron dos veces en el camino.
    private static readonly char[] SaltosDeLinea = [(char)13, (char)10];

    // Solo la primera linea del error: un stack trace en el mensaje de una
    // respuesta de negocio no le sirve a nadie y filtra rutas del servidor.
    private static string PrimeraLinea(string texto)
    {
        int corte = texto.IndexOfAny(SaltosDeLinea);

        return corte < 0 ? texto : texto[..corte];
    }

    private static ResultDto<DocumentoEnviarResponse> Falla(string mensaje) =>
        new(null!) { Data = null, IsValid = false, Message = mensaje };
}

public record DocumentoParaCorreo(
    long EmisorId,
    string Denominacion,
    string Numeracion,
    string NumeroControl,
    string FechaEmision8d,
    string EmisorRif,
    string EmisorRazonSocial,
    string ReceptorNombre,
    string CorreoConocido,
    bool EsPrueba);

[ApiController]
[Authorize]
[Route("api/FacturacionElectronica")]
public class FacturacionElectronicaDocumentoEnviarController(
    ConnectionDB _connectionDB, IConfiguration _config) : ControllerBase
{
    [HttpPost]
    [Route("documentoEnviar")]
    public async Task<IActionResult> DocumentoEnviar(DocumentoEnviarCommand value)
    {
        var handler = new FacturacionElectronicaDocumentoEnviarHandler(_connectionDB, _config);
        var result = await handler.HandleAsync(value);

        return Ok(result);
    }
}
