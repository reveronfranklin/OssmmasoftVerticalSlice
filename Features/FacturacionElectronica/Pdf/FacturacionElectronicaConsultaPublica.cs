using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.FacturacionElectronica;

// T7.4 - consulta del usuario final, SIN AUTENTICACION. Art. 18.10: el emisor
// debe facilitar la consulta del usuario final a traves de su pagina web o la de
// la imprenta digital. Nosotros somos la imprenta, asi que esta superficie es
// nuestra.
//
// ES EL UNICO ENDPOINT ANONIMO DEL MODULO, y por eso conviene ser explicito sobre
// que lo protege y que no.
//
// LO PROTEGE el codigo firmado de EnlacePublico: sin el secreto no se puede
// fabricar, asi que conocer el documento 129 no da acceso al 130. No hay listado,
// no hay busqueda por RIF y no hay parametro de emisor: la unica entrada es un
// codigo completo.
//
// NO LO PROTEGE nada mas, y no hace falta: quien tiene el codigo es el receptor
// del documento, que es exactamente quien la norma quiere que pueda consultarlo.
//
// UN CODIGO INVALIDO Y UN DOCUMENTO INEXISTENTE DEVUELVEN LO MISMO. Distinguirlos
// convertiria el endpoint en un oraculo: probando codigos se sabria cuales ids
// existen. Es la misma razon por la que un login no dice si el usuario existe.
public record ConsultaPublicaQuery(string Codigo);

// Lo que ve un tercero. Los datos que el documento ya le muestra impreso, mas el
// PDF. Nada del sistema: ni ids internos, ni el emisor_id, ni la clave de
// idempotencia.
public record ConsultaPublicaResponse(
    string Denominacion,
    string Numeracion,
    string NumeroControl,
    string FechaEmision8d,
    string HoraEmision,
    string EmisorRif,
    string EmisorRazonSocial,
    string ReceptorRif,
    string ReceptorNombre,
    decimal TotalGeneral,
    string Moneda,
    bool EsPrueba,
    string NombreArchivo,
    string ContenidoBase64);

public class FacturacionElectronicaConsultaPublicaHandler(
    ConnectionDB _connectionDB, IConfiguration _config, IWebHostEnvironment _environment,
    ILogger<FacturacionElectronicaConsultaPublicaHandler> _logger)
{
    // Un solo mensaje para las tres formas de fallar: codigo mal formado, firma
    // invalida y documento inexistente.
    private const string NoDisponible =
        "El documento no está disponible. Verifique el enlace que recibió.";

    public async Task<ResultDto<ConsultaPublicaResponse>> HandleAsync(ConsultaPublicaQuery query)
    {
        if (!EnlacePublico.Configurado(_config))
        {
            // Sin secreto la superficie publica se apaga entera. Mejor eso que
            // emitir codigos que cualquiera pueda reproducir.
            return Falla("La consulta pública no está habilitada en este ambiente.");
        }

        if (!EnlacePublico.Verificar(_config, query.Codigo, out string tipo, out long id))
        {
            return Falla(NoDisponible);
        }

        using var cn = _connectionDB.GetFedConnection();

        try
        {
            await cn.OpenAsync();
        }
        catch (Exception ex)
        {
            // Endpoint SIN AUTENTICACION: a diferencia del resto del modulo,
            // ex.Message nunca llega al llamador. Un fallo tecnico real (pool
            // agotado, red) no puede convertirse en una fuga hacia cualquier
            // persona de internet con un enlace -o sin el, si la firma nunca
            // se llega a verificar-. El detalle queda en el log del servidor.
            _logger.LogError(ex, "consultaPublica: fallo al abrir la conexion FED.");

            return Falla(NoDisponible);
        }

        try
        {
            byte[]? logo = LogoImprenta.Leer(_environment);

            // TM.7: el papel que se consulta lleva el mismo QR que el que se
            // imprimio. Es el mismo documento, y el Art. 21.2 no admite dos
            // versiones distintas de un ejemplar.
            string? urlConsulta = EnlacePublico.UrlParaQr(_config, tipo, id);

            return tipo == EnlacePublico.TipoRetencion
                ? await ComprobanteAsync(cn, id, logo, urlConsulta)
                : await DocumentoAsync(cn, id, logo, urlConsulta);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "consultaPublica: fallo al generar el documento para id {Id}, tipo {Tipo}.", id, tipo);

            return Falla(NoDisponible);
        }
    }

    private static async Task<ResultDto<ConsultaPublicaResponse>> DocumentoAsync(
        NpgsqlConnection cn, long id, byte[]? logo, string? urlConsulta)
    {
        var documento = await FacturacionElectronicaDocumentoPdfHandler.LeerAsync(cn, id);

        if (documento is null)
        {
            return Falla(NoDisponible);
        }

        var c = documento.Cabecera;
        byte[] pdf = DocumentoPdfPlantilla.Generar(documento, logo, urlConsulta);

        return Exito(new ConsultaPublicaResponse(
            c.Denominacion,
            c.NumeracionConSerie,
            c.NumeroControl,
            c.FechaEmision8d,
            c.HoraEmision,
            c.EmisorRif,
            c.EmisorRazonSocial,
            c.AdqRif,
            c.AdqNombre,
            c.TotalGeneral,
            c.Moneda,
            c.EsPrueba,
            $"{c.Denominacion.Replace(' ', '_')}_{c.NumeracionConSerie}.pdf",
            Convert.ToBase64String(pdf)));
    }

    private static async Task<ResultDto<ConsultaPublicaResponse>> ComprobanteAsync(
        NpgsqlConnection cn, long id, byte[]? logo, string? urlConsulta)
    {
        RetencionImpresionCabecera? cabecera = null;

        using (var cmd = new NpgsqlCommand(RetencionPdfPlantilla.SqlRetencionParaImprimir, cn))
        {
            cmd.Parameters.AddWithValue("retencion_id", id);

            using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                cabecera = RetencionPdfPlantilla.MapCabecera(reader);
            }
        }

        if (cabecera is null)
        {
            return Falla(NoDisponible);
        }

        var detalle = new List<RetencionImpresionDetalle>();

        using (var cmd = new NpgsqlCommand(RetencionPdfPlantilla.SqlDetalleParaImprimir, cn))
        {
            cmd.Parameters.AddWithValue("retencion_id", id);

            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                detalle.Add(RetencionPdfPlantilla.MapDetalle(reader));
            }
        }

        byte[] pdf = RetencionPdfPlantilla.Generar(new RetencionImpresion(cabecera, detalle), logo, urlConsulta);

        return Exito(new ConsultaPublicaResponse(
            "COMPROBANTE DE RETENCIÓN",
            cabecera.Numeracion,

            // Vacio porque el documento no lo lleva, no porque falte el dato.
            string.Empty,
            cabecera.FechaEmision8d,
            cabecera.HoraEmision,
            cabecera.AgenteRif,
            cabecera.AgenteRazonSocial,
            cabecera.ProveedorRif,
            cabecera.ProveedorRazonSocial,
            cabecera.TotalRetenido,
            "VES",
            cabecera.EsPrueba,
            $"COMPROBANTE_RETENCION_{cabecera.Numeracion}.pdf",
            Convert.ToBase64String(pdf)));
    }

    private static ResultDto<ConsultaPublicaResponse> Exito(ConsultaPublicaResponse data) =>
        new(data) { IsValid = true, Message = FacturacionElectronicaDb.MensajeExito };

    private static ResultDto<ConsultaPublicaResponse> Falla(string mensaje) =>
        new(null!) { Data = null, IsValid = false, Message = mensaje };
}

// El enlace de un documento, para que la pantalla interna pueda mostrarlo o
// copiarlo. Va autenticado: armar el enlace es una accion del emisor, usarlo es
// una accion del receptor.
public record EnlacePublicoQuery(long Id, string Tipo = EnlacePublico.TipoDocumento);

public record EnlacePublicoResponse(string Codigo, string Url, bool Habilitado);

[ApiController]
[Route("api/FacturacionElectronica")]
public class FacturacionElectronicaConsultaPublicaController(
    ConnectionDB _connectionDB, IConfiguration _config, IWebHostEnvironment _environment,
    ILogger<FacturacionElectronicaConsultaPublicaHandler> _logger) : ControllerBase
{
    // SIN [Authorize], y es el unico del modulo. Ver la cabecera del handler.
    [AllowAnonymous]
    [HttpPost]
    [Route("consultaPublica")]
    public async Task<IActionResult> ConsultaPublica(ConsultaPublicaQuery value)
    {
        var handler = new FacturacionElectronicaConsultaPublicaHandler(_connectionDB, _config, _environment, _logger);
        var result = await handler.HandleAsync(value);

        return Ok(result);
    }

    [Authorize]
    [HttpPost]
    [Route("enlacePublico")]
    public IActionResult Enlace(EnlacePublicoQuery value)
    {
        string tipo = value.Tipo == EnlacePublico.TipoRetencion
            ? EnlacePublico.TipoRetencion
            : EnlacePublico.TipoDocumento;

        if (!EnlacePublico.Configurado(_config))
        {
            return Ok(new ResultDto<EnlacePublicoResponse>(new EnlacePublicoResponse(string.Empty, string.Empty, false))
            {
                IsValid = false,
                Message = "La consulta pública no está habilitada: falta configurar el secreto del enlace."
            });
        }

        var data = new EnlacePublicoResponse(
            EnlacePublico.Codigo(_config, tipo, value.Id),
            EnlacePublico.Url(_config, tipo, value.Id),
            true);

        return Ok(new ResultDto<EnlacePublicoResponse>(data)
        {
            IsValid = true,
            Message = FacturacionElectronicaDb.MensajeExito
        });
    }
}
