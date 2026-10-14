using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.StaticFiles;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.BienesMunicipales;

[ApiController]
[Route("api/BmBienesFotos")]
public class BmBienesFotosController(
    ConnectionDB connectionDB,
    IConfiguration config,
    ILogger<BmBienesFotosController> logger) : ControllerBase
{
    [HttpPost("GetByNumeroPlaca")]
    public async Task<IActionResult> GetByNumeroPlaca(BmBienFotoByPlacaRequest request)
    {
        return Ok(await GetByPlacaAsync(request.NumeroPlaca));
    }

    [HttpGet("Image/{numeroPlaca}/{foto}")]
    public async Task<IActionResult> Image(string numeroPlaca, string foto)
    {
        return await GetImageAsync(numeroPlaca, foto);
    }

    [HttpGet("Image")]
    public async Task<IActionResult> ImageByQuery([FromQuery] string numeroPlaca, [FromQuery] string foto)
    {
        return await GetImageAsync(numeroPlaca, foto);
    }

    private async Task<IActionResult> GetImageAsync(string numeroPlaca, string foto)
    {
        var folder = BmDb.GetBmFilesPath(config);
        var fileName = Path.GetFileName(Uri.UnescapeDataString(foto));
        var placaFolder = BuildSafeFolderName(numeroPlaca);

        if (string.IsNullOrWhiteSpace(fileName) || string.IsNullOrWhiteSpace(placaFolder))
        {
            return NotFound();
        }

        var candidates = new[]
        {
            Path.Combine(folder, placaFolder, fileName),
            Path.Combine(folder, fileName),
            Path.Combine(folder, "no-product-image.png")
        };

        var filePath = candidates.FirstOrDefault(System.IO.File.Exists);
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return NotFound();
        }

        var provider = new FileExtensionContentTypeProvider();
        if (!provider.TryGetContentType(filePath, out var contentType))
        {
            contentType = "application/octet-stream";
        }

        var bytes = await System.IO.File.ReadAllBytesAsync(filePath);

        return File(bytes, contentType, Path.GetFileName(filePath));
    }

    [Authorize]
    [RequestSizeLimit(55 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 55 * 1024 * 1024)]
    [HttpPost("AddImage/{codigoBien:int}")]
    public async Task<IActionResult> AddImage(int codigoBien, [FromForm] List<IFormFile> files, [FromForm] string numeroPlaca, [FromForm] string? titulo, [FromForm] string? cargaId = null)
    {
        if (files.Count == 0 || files.Count > 20 || files.Sum(f => f.Length) > 50L * 1024 * 1024)
        {
            return Ok(BmDb.InvalidList<BmBienFotoResponse>("Adjunte entre 1 y 20 imagenes, hasta 50 MB por envio."));
        }

        if (string.IsNullOrWhiteSpace(titulo) || Encoding.UTF8.GetByteCount(titulo.Trim()) > 500)
        {
            return Ok(BmDb.InvalidList<BmBienFotoResponse>("Debe indicar un titulo de hasta 500 bytes."));
        }

        if (cargaId is not null && (!Guid.TryParse(cargaId, out _) || files.Count != 1))
            return Ok(BmDb.InvalidList<BmBienFotoResponse>("Identificador de carga invalido."));

        foreach (var file in files)
        {
            if (!await IsSupportedImageAsync(file))
                return Ok(BmDb.InvalidList<BmBienFotoResponse>("Solo se permiten imagenes JPEG, PNG o WebP de hasta 10 MB, con contenido valido."));
        }

        if (!BmDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return Ok(BmDb.InvalidList<BmBienFotoResponse>(error));
        }

        using var cn = connectionDB.GetBmConnection();
        var openError = await BmDb.TryOpenAsync(cn, "BM");
        if (openError is not null) return Ok(BmDb.InvalidList<BmBienFotoResponse>(openError));

        // La placa que manda el cliente no se usa como clave de almacenamiento: se
        // resuelve la canonica (BM_BIENES.NUMERO_PLACA) desde el codigo del bien,
        // como hacia BmBienesFotoService.AddImage del sistema anterior. La vista
        // BM_V_BM1 expone un numeroPlaca derivado -SUBSTR+LPAD, y un rango cuando
        // agrupa-, y guardar ese valor deja la foto en una carpeta que despues nadie
        // encuentra. Si no se puede resolver, no se escribe fuera del bien validado.
        var placaCanonica = await ResolverPlacaCanonicaAsync(cn, empresa, codigoBien);
        if (string.IsNullOrWhiteSpace(placaCanonica))
            return Ok(BmDb.InvalidList<BmBienFotoResponse>("No se pudo resolver la placa del bien en la empresa configurada."));

        var folder = BmDb.GetBmFilesPath(config);
        var placaFolder = BuildSafeFolderName(placaCanonica);
        var targetFolder = Path.Combine(folder, placaFolder);
        try
        {
            Directory.CreateDirectory(targetFolder);
            foreach (var file in files)
            {
                // Misma carga + mismo contenido => mismo archivo, aun despues de un corte de red.
                await using var input = file.OpenReadStream();
                var hash = Convert.ToHexString(await SHA256.HashDataAsync(input));
                var token = cargaId is null ? Guid.NewGuid().ToString("N") : Guid.Parse(cargaId).ToString("N");
                var fileName = $"{empresa}_{codigoBien}_{token}_{hash}{Path.GetExtension(file.FileName).ToLowerInvariant()}";
                var fullPath = Path.Combine(targetFolder, fileName);
                var temporary = Path.Combine(targetFolder, $".{Guid.NewGuid():N}.tmp");
                try
                {
                    await using (var stream = System.IO.File.Create(temporary))
                        await file.CopyToAsync(stream);
                    try { System.IO.File.Move(temporary, fullPath, false); }
                    catch (IOException) when (System.IO.File.Exists(fullPath)) { }
                }
                finally
                {
                    if (System.IO.File.Exists(temporary)) System.IO.File.Delete(temporary);
                }

                using var cmd = BmDb.StoredProcedure("BM.SP_BM_FOTO_INS", cn);
                cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
                cmd.Parameters.Add("p_CodigoBien", OracleDbType.Int32).Value = codigoBien;
                cmd.Parameters.Add("p_NumeroPlaca", OracleDbType.Varchar2).Value = BmDb.DbValue(placaCanonica);
                cmd.Parameters.Add("p_Foto", OracleDbType.Varchar2).Value = fileName;
                cmd.Parameters.Add("p_Titulo", OracleDbType.Varchar2).Value = BmDb.DbValue(titulo.Trim());
                cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
                var result = await BmDb.ExecuteListAsync(cmd, MapFoto);
                // Conservar el archivo si el resultado es incierto: el reintento es idempotente.
                if (!result.IsValid) return Ok(result);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error al cargar fotos del bien {CodigoBien}", codigoBien);
            return Ok(BmDb.InvalidList<BmBienFotoResponse>("No se pudo completar la carga. Puede reintentar los archivos pendientes."));
        }

        return Ok(await GetByPlacaAsync(placaCanonica));
    }

    [Authorize]
    [HttpPost("Delete")]
    public async Task<IActionResult> Delete(BmBienFotoDeleteRequest request)
    {
        if (!BmDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return Ok(BmDb.InvalidList<BmBienFotoResponse>(error));
        }

        using var cn = connectionDB.GetBmConnection();
        var openError = await BmDb.TryOpenAsync(cn, "BM");
        if (openError is not null) return Ok(BmDb.InvalidList<BmBienFotoResponse>(openError));

        try
        {
            using var cmd = BmDb.StoredProcedure("BM.SP_BM_FOTO_DEL", cn);
            cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
            cmd.Parameters.Add("p_CodigoBienFoto", OracleDbType.Int32).Value = request.CodigoBienFoto;
            cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);

            return Ok(await BmDb.ExecuteListAsync(cmd, MapFoto));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error al eliminar foto {CodigoBienFoto}", request.CodigoBienFoto);
            return Ok(BmDb.InvalidList<BmBienFotoResponse>("No se pudo eliminar la foto."));
        }
    }

    private async Task<OssmmasoftVerticalSlice.Helpers.ResultDto<List<BmBienFotoResponse>>> GetByPlacaAsync(string numeroPlaca)
    {
        if (string.IsNullOrWhiteSpace(numeroPlaca))
        {
            return BmDb.InvalidList<BmBienFotoResponse>("Debe indicar el numero de placa.");
        }

        try
        {
            if (!BmDb.TryGetEmpresa(config, out var empresa, out var error))
            {
                return BmDb.InvalidList<BmBienFotoResponse>(error);
            }

            using var cn = connectionDB.GetBmConnection();
            var openError = await BmDb.TryOpenAsync(cn, "BM");
            if (openError is not null) return BmDb.InvalidList<BmBienFotoResponse>(openError);

            using var cmd = BmDb.StoredProcedure("BM.SP_BM_FOTO_GET_PLACA", cn);
            cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
            cmd.Parameters.Add("p_NumeroPlaca", OracleDbType.Varchar2).Value = BmDb.DbValue(numeroPlaca);
            cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);

            return await BmDb.ExecuteListAsync(cmd, MapFoto);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error al consultar fotos del bien con placa {NumeroPlaca}", numeroPlaca);
            return BmDb.InvalidList<BmBienFotoResponse>($"Error tecnico al consultar las fotos del bien: {ex.Message}");
        }
    }

    private static BmBienFotoResponse MapFoto(IDataReader reader)
    {
        var foto = reader.SafeGetString("FOTO");
        var numeroPlaca = reader.SafeGetString("NUMERO_PLACA");

        return new BmBienFotoResponse(
            reader.SafeGetInt32("CODIGO_BIEN_FOTO"),
            reader.SafeGetInt32("CODIGO_BIEN"),
            numeroPlaca,
            foto,
            reader.SafeGetString("TITULO"),
            BuildFotoPatch(numeroPlaca, foto),
            ReadFechaCarga(reader),
            HasColumn(reader, "ES_PRINCIPAL") && reader.SafeGetInt32("ES_PRINCIPAL") == 1
        );
    }

    [Authorize]
    [HttpPost("UpdateTitulo")]
    public Task<IActionResult> UpdateTitulo(BmBienFotoEditRequest request) => EditAsync(request, false);

    [Authorize]
    [HttpPost("SetPrincipal")]
    public Task<IActionResult> SetPrincipal(BmBienFotoDeleteRequest request) =>
        EditAsync(new BmBienFotoEditRequest(request.CodigoBienFoto, null), true);

    private async Task<IActionResult> EditAsync(BmBienFotoEditRequest request, bool principal)
    {
        if (request.CodigoBienFoto <= 0 || (!principal &&
            (string.IsNullOrWhiteSpace(request.Titulo) || Encoding.UTF8.GetByteCount(request.Titulo.Trim()) > 500)))
            return Ok(BmDb.InvalidList<BmBienFotoResponse>("Indique una foto y un titulo valido de hasta 500 bytes."));
        if (!BmDb.TryGetEmpresa(config, out var empresa, out var error))
            return Ok(BmDb.InvalidList<BmBienFotoResponse>(error));
        using var cn = connectionDB.GetBmConnection();
        var openError = await BmDb.TryOpenAsync(cn, "BM");
        if (openError is not null) return Ok(BmDb.InvalidList<BmBienFotoResponse>(openError));
        try
        {
            using var cmd = BmDb.StoredProcedure("BM.SP_BM_FOTO_EDIT", cn);
            cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
            cmd.Parameters.Add("p_CodigoBienFoto", OracleDbType.Int32).Value = request.CodigoBienFoto;
            cmd.Parameters.Add("p_Titulo", OracleDbType.Varchar2).Value = BmDb.DbValue(request.Titulo?.Trim());
            cmd.Parameters.Add("p_Principal", OracleDbType.Int32).Value = principal ? 1 : 0;
            cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);
            return Ok(await BmDb.ExecuteListAsync(cmd, MapFoto));
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error al modificar foto {CodigoBienFoto}", request.CodigoBienFoto);
            return Ok(BmDb.InvalidList<BmBienFotoResponse>("No se pudo modificar la foto."));
        }
    }

    private static bool HasColumn(IDataReader reader, string name) =>
        Enumerable.Range(0, reader.FieldCount).Any(i => reader.GetName(i).Equals(name, StringComparison.OrdinalIgnoreCase));

    private static DateTime? ReadFechaCarga(IDataReader reader) =>
        HasColumn(reader, "FECHA_INS") && reader["FECHA_INS"] != DBNull.Value
            ? DateTime.SpecifyKind(Convert.ToDateTime(reader["FECHA_INS"]), DateTimeKind.Unspecified) : null;

    private static async Task<bool> IsSupportedImageAsync(IFormFile file)
    {
        if (file.Length == 0 || file.Length > 10L * 1024 * 1024) return false;
        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        var bytes = new byte[12];
        await using var stream = file.OpenReadStream();
        if (await stream.ReadAtLeastAsync(bytes, 12, false) < 12) return false;
        return (extension is ".jpg" or ".jpeg") && file.ContentType == "image/jpeg" &&
                   bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff
            || extension == ".png" && file.ContentType == "image/png" &&
                   bytes.AsSpan(0, 8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 })
            || extension == ".webp" && file.ContentType == "image/webp" &&
                   Encoding.ASCII.GetString(bytes, 0, 4) == "RIFF" && Encoding.ASCII.GetString(bytes, 8, 4) == "WEBP";
    }

    private static string BuildFotoPatch(string numeroPlaca, string foto)
    {
        if (string.IsNullOrWhiteSpace(foto))
        {
            return string.Empty;
        }

        if (Uri.TryCreate(foto, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            return foto;
        }

        var fileName = Path.GetFileName(foto.Replace('\\', '/'));
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return string.Empty;
        }

        return $"/api/BmBienesFotos/Image?numeroPlaca={Uri.EscapeDataString(numeroPlaca)}&foto={Uri.EscapeDataString(fileName)}";
    }

    /// <summary>
    /// Placa canonica del bien: <c>BM_BIENES.NUMERO_PLACA</c>, la misma columna que
    /// la vista <c>BM_V_BM1</c> expone como <c>NRO_PLACA</c> y con la que se guardan
    /// las fotos y se nombra su carpeta en <c>BmFiles</c>.
    ///
    /// Consulta directa y sin procedimiento propio, como
    /// <c>ReporteBm1Esp.ObtenerEntidadAsync</c>: es una fila sin logica de negocio.
    /// Devuelve <c>null</c> si no se puede resolver.
    /// </summary>
    private static async Task<string?> ResolverPlacaCanonicaAsync(OracleConnection cn, int empresa, int codigoBien)
    {
        try
        {
            using var cmd = new OracleCommand(
                @"SELECT B.NUMERO_PLACA
                    FROM BM.BM_BIENES B
                   WHERE B.CODIGO_EMPRESA = :p_CodigoEmpresa
                     AND B.CODIGO_BIEN = :p_CodigoBien", cn)
            {
                BindByName = true
            };

            cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
            cmd.Parameters.Add("p_CodigoBien", OracleDbType.Int32).Value = codigoBien;

            using var reader = await cmd.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return null;
            }

            var placa = reader.SafeGetString("NUMERO_PLACA").Trim();

            return string.IsNullOrWhiteSpace(placa) ? null : placa;
        }
        catch (OracleException)
        {
            return null;
        }
    }

    private static string BuildSafeFolderName(string value)
    {
        var decoded = Uri.UnescapeDataString(value ?? string.Empty);
        var invalidChars = Path.GetInvalidFileNameChars();
        var chars = decoded.Select(ch => invalidChars.Contains(ch) || ch is '/' or '\\' ? '_' : ch).ToArray();
        var folder = new string(chars).Trim();

        return string.IsNullOrWhiteSpace(folder) ? "SINPLACA" : folder;
    }
}
