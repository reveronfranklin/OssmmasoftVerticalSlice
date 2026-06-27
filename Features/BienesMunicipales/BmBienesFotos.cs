using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using System.Data;

namespace OssmmasoftVerticalSlice.Features.BienesMunicipales;

[ApiController]
[Route("api/BmBienesFotos")]
public class BmBienesFotosController(ConnectionDB connectionDB, IConfiguration config) : ControllerBase
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

    [HttpPost("AddImage/{codigoBien:int}")]
    public async Task<IActionResult> AddImage(int codigoBien, [FromForm] List<IFormFile> files, [FromForm] string numeroPlaca, [FromForm] string? titulo)
    {
        if (files.Count == 0)
        {
            return Ok(BmDb.InvalidList<BmBienFotoResponse>("Debe adjuntar al menos una imagen."));
        }

        if (string.IsNullOrWhiteSpace(titulo))
        {
            return Ok(BmDb.InvalidList<BmBienFotoResponse>("Debe indicar el titulo de la foto."));
        }

        if (!BmDb.TryGetEmpresa(config, out var empresa, out var error))
        {
            return Ok(BmDb.InvalidList<BmBienFotoResponse>(error));
        }

        using var cn = connectionDB.GetBmConnection();
        var openError = await BmDb.TryOpenAsync(cn, "BM");
        if (openError is not null) return Ok(BmDb.InvalidList<BmBienFotoResponse>(openError));

        var folder = BmDb.GetBmFilesPath(config);
        var placaFolder = BuildSafeFolderName(numeroPlaca);
        var targetFolder = Path.Combine(folder, placaFolder);
        Directory.CreateDirectory(targetFolder);

        foreach (var file in files.Where(file => file.Length > 0))
        {
            var fileName = BmDb.BuildSafeFileName(codigoBien, numeroPlaca, file.FileName);
            var fullPath = Path.Combine(targetFolder, fileName);

            await using (var stream = System.IO.File.Create(fullPath))
            {
                await file.CopyToAsync(stream);
            }

            using var cmd = BmDb.StoredProcedure("BM.SP_BM_FOTO_INS", cn);
            cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
            cmd.Parameters.Add("p_CodigoBien", OracleDbType.Int32).Value = codigoBien;
            cmd.Parameters.Add("p_NumeroPlaca", OracleDbType.Varchar2).Value = BmDb.DbValue(numeroPlaca);
            cmd.Parameters.Add("p_Foto", OracleDbType.Varchar2).Value = fileName;
            cmd.Parameters.Add("p_Titulo", OracleDbType.Varchar2).Value = BmDb.DbValue(titulo);
            cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);

            var result = await BmDb.ExecuteListAsync(cmd, MapFoto);
            if (!result.IsValid)
            {
                return Ok(result);
            }
        }

        return Ok(await GetByPlacaAsync(numeroPlaca));
    }

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

        using var cmd = BmDb.StoredProcedure("BM.SP_BM_FOTO_DEL", cn);
        cmd.Parameters.Add("p_CodigoEmpresa", OracleDbType.Int32).Value = empresa;
        cmd.Parameters.Add("p_CodigoBienFoto", OracleDbType.Int32).Value = request.CodigoBienFoto;
        cmd.Parameters.Add("p_ResultSet", OracleDbType.RefCursor, ParameterDirection.Output);

        return Ok(await BmDb.ExecuteListAsync(cmd, MapFoto));
    }

    private async Task<OssmmasoftVerticalSlice.Helpers.ResultDto<List<BmBienFotoResponse>>> GetByPlacaAsync(string numeroPlaca)
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
            BuildFotoPatch(numeroPlaca, foto)
        );
    }

    private static string BuildFotoPatch(string numeroPlaca, string foto)
    {
        if (string.IsNullOrWhiteSpace(foto))
        {
            return string.Empty;
        }

        if (Uri.TryCreate(foto, UriKind.Absolute, out _))
        {
            return foto;
        }

        var fileName = Path.GetFileName(foto);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return string.Empty;
        }

        return $"/api/BmBienesFotos/Image?numeroPlaca={Uri.EscapeDataString(numeroPlaca)}&foto={Uri.EscapeDataString(fileName)}";
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
