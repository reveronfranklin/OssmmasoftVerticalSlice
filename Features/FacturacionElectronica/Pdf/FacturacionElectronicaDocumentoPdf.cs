using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Npgsql;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;

namespace OssmmasoftVerticalSlice.Features.FacturacionElectronica;

// T7.1 - representacion grafica de un documento fiscal. Art. 12 y Art. 31.
//
// DEVUELVE EL PDF EN BASE64 DENTRO DE UN ResultDto, y no un FileResult. Es lo
// que hace el resto del repo -ReporteComprobanteIva, ReporteOrdenPago- y lo que
// el visor del frontend sabe consumir. Devolver un archivo obligaria a la
// pantalla a manejar dos formas de respuesta para el mismo modulo, y el estandar
// del proyecto es explicito en que los reportes van por el flujo de
// previsualizacion existente, sin descarga forzada.
//
// EL PDF NO SE GUARDA. Se genera a demanda desde el documento persistido, que es
// inmutable: mismos datos, mismo papel, siempre. Guardarlo agregaria una copia
// que puede divergir del documento y que habria que versionar, y el Art. 21.2
// castiga justamente tener mas de un ejemplar del mismo documento.
// El logo de Ossmmasoft como IMPRENTA DIGITAL. Vive en Assets/Reports/, que es
// donde el .csproj copia los recursos de reporte al publicar; la carpeta
// "Recursos Multimedia Ossmmasoft" del monorepo es el original compartido, no
// contenido desplegable del backend.
//
// SOLO EL DE OSSMMASOFT. El logoLeft.jpeg que usan los otros reportes es del
// Concejo Municipal de Chacao, o sea de un EMISOR, y en un documento fiscal el
// encabezado identifica al emisor: estampar ahi un logo fijo diria que todos los
// documentos los emite el mismo. Cuando haga falta el logo del emisor, sera un
// dato de FED_EMISOR y no un archivo del servidor.
//
// Se lee en cada llamada y no se cachea: un PDF por peticion no justifica una
// cache, y una cache mal invalidada dejaria el logo viejo en un documento fiscal.
public static class LogoImprenta
{
    public const string NombreArchivo = "logoOssmmasoft.png";

    public static byte[]? Leer(IWebHostEnvironment environment)
    {
        string ruta = Path.Combine(environment.ContentRootPath, "Assets", "Reports", NombreArchivo);

        return File.Exists(ruta) ? File.ReadAllBytes(ruta) : null;
    }
}

public record DocumentoPdfQuery(long DocumentoId);

public record DocumentoPdfResponse(
    long DocumentoId,
    string Denominacion,
    string NumeracionConSerie,
    string NumeroControl,
    string NombreArchivo,
    string ContenidoBase64,
    bool EsPrueba);

public class FacturacionElectronicaDocumentoPdfHandler(ConnectionDB _connectionDB, IWebHostEnvironment _environment)
{
    public async Task<ResultDto<DocumentoPdfResponse>> HandleAsync(DocumentoPdfQuery query)
    {
        if (query.DocumentoId <= 0)
        {
            return Falla("Falta el identificador del documento.");
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
            var documento = await LeerAsync(cn, query.DocumentoId);

            if (documento is null)
            {
                return Falla($"No existe un documento con el identificador {query.DocumentoId}.");
            }

            var c = documento.Cabecera;
            byte[] pdf = DocumentoPdfPlantilla.Generar(documento, LogoImprenta.Leer(_environment));

            // El nombre del archivo lleva la denominacion y el numero de control,
            // que es como se identifica el papel cuando ya salio del sistema.
            string nombre = $"{c.Denominacion.Replace(' ', '_')}_{c.NumeracionConSerie}.pdf";

            return new ResultDto<DocumentoPdfResponse>(new DocumentoPdfResponse(
                c.Id,
                c.Denominacion,
                c.NumeracionConSerie,
                c.NumeroControl,
                nombre,
                Convert.ToBase64String(pdf),
                c.EsPrueba))
            {
                IsValid = true,
                Message = FacturacionElectronicaDb.MensajeExito
            };
        }
        catch (Exception ex)
        {
            return Falla($"Error técnico al generar el PDF: {ex.Message}");
        }
    }

    // Las tres consultas del documento, en una sola conexion. Se lee todo antes
    // de dibujar nada: la plantilla no debe tener que ir a la base.
    public static async Task<DocumentoImpresion?> LeerAsync(NpgsqlConnection cn, long documentoId)
    {
        DocumentoImpresionCabecera? cabecera = null;

        using (var cmd = new NpgsqlCommand(DocumentoPdfDb.SqlDocumentoParaImprimir, cn))
        {
            cmd.Parameters.AddWithValue("documento_id", documentoId);

            using var reader = await cmd.ExecuteReaderAsync();

            if (await reader.ReadAsync())
            {
                cabecera = DocumentoPdfDb.MapCabecera(reader);
            }
        }

        if (cabecera is null)
        {
            return null;
        }

        var renglones = new List<DocumentoImpresionRenglon>();

        using (var cmd = new NpgsqlCommand(DocumentoPdfDb.SqlRenglonesParaImprimir, cn))
        {
            cmd.Parameters.AddWithValue("documento_id", documentoId);

            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                renglones.Add(DocumentoPdfDb.MapRenglon(reader));
            }
        }

        var impuestos = new List<DocumentoImpresionImpuesto>();

        using (var cmd = new NpgsqlCommand(DocumentoPdfDb.SqlImpuestosParaImprimir, cn))
        {
            cmd.Parameters.AddWithValue("documento_id", documentoId);

            using var reader = await cmd.ExecuteReaderAsync();

            while (await reader.ReadAsync())
            {
                impuestos.Add(DocumentoPdfDb.MapImpuesto(reader));
            }
        }

        return new DocumentoImpresion(cabecera, renglones, impuestos);
    }

    private static ResultDto<DocumentoPdfResponse> Falla(string mensaje) =>
        new(null!) { Data = null, IsValid = false, Message = mensaje };
}

// El comprobante de retencion tiene su propio handler por la misma razon por la
// que tiene su propia tabla y su propia plantilla: no es un FED_DOCUMENTO.
// Pedirle a documentoPdf que acepte "el id de un documento o el de un
// comprobante" seria un parametro que significa dos cosas distintas.
public record RetencionPdfQuery(long RetencionId);

public class FacturacionElectronicaRetencionPdfHandler(ConnectionDB _connectionDB, IWebHostEnvironment _environment)
{
    public async Task<ResultDto<DocumentoPdfResponse>> HandleAsync(RetencionPdfQuery query)
    {
        if (query.RetencionId <= 0)
        {
            return Falla("Falta el identificador del comprobante.");
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
            RetencionImpresionCabecera? cabecera = null;

            using (var cmd = new NpgsqlCommand(RetencionPdfPlantilla.SqlRetencionParaImprimir, cn))
            {
                cmd.Parameters.AddWithValue("retencion_id", query.RetencionId);

                using var reader = await cmd.ExecuteReaderAsync();

                if (await reader.ReadAsync())
                {
                    cabecera = RetencionPdfPlantilla.MapCabecera(reader);
                }
            }

            if (cabecera is null)
            {
                return Falla($"No existe un comprobante de retención con el identificador {query.RetencionId}.");
            }

            var detalle = new List<RetencionImpresionDetalle>();

            using (var cmd = new NpgsqlCommand(RetencionPdfPlantilla.SqlDetalleParaImprimir, cn))
            {
                cmd.Parameters.AddWithValue("retencion_id", query.RetencionId);

                using var reader = await cmd.ExecuteReaderAsync();

                while (await reader.ReadAsync())
                {
                    detalle.Add(RetencionPdfPlantilla.MapDetalle(reader));
                }
            }

            byte[] pdf = RetencionPdfPlantilla.Generar(
                new RetencionImpresion(cabecera, detalle), LogoImprenta.Leer(_environment));

            return new ResultDto<DocumentoPdfResponse>(new DocumentoPdfResponse(
                cabecera.Id,
                "COMPROBANTE DE RETENCIÓN",
                cabecera.Numeracion,

                // Vacio, y no es un dato que falte: este documento no lleva
                // numero de control.
                string.Empty,
                $"COMPROBANTE_RETENCION_{cabecera.Numeracion}.pdf",
                Convert.ToBase64String(pdf),
                cabecera.EsPrueba))
            {
                IsValid = true,
                Message = FacturacionElectronicaDb.MensajeExito
            };
        }
        catch (Exception ex)
        {
            return Falla($"Error técnico al generar el PDF: {ex.Message}");
        }
    }

    private static ResultDto<DocumentoPdfResponse> Falla(string mensaje) =>
        new(null!) { Data = null, IsValid = false, Message = mensaje };
}

[ApiController]
[Authorize]
[Route("api/FacturacionElectronica")]
public class FacturacionElectronicaDocumentoPdfController(
    ConnectionDB _connectionDB, IWebHostEnvironment _environment) : ControllerBase
{
    [HttpPost]
    [Route("documentoPdf")]
    public async Task<IActionResult> DocumentoPdf(DocumentoPdfQuery value)
    {
        var handler = new FacturacionElectronicaDocumentoPdfHandler(_connectionDB, _environment);
        var result = await handler.HandleAsync(value);

        return Ok(result);
    }

    [HttpPost]
    [Route("retencionPdf")]
    public async Task<IActionResult> RetencionPdf(RetencionPdfQuery value)
    {
        var handler = new FacturacionElectronicaRetencionPdfHandler(_connectionDB, _environment);
        var result = await handler.HandleAsync(value);

        return Ok(result);
    }
}
