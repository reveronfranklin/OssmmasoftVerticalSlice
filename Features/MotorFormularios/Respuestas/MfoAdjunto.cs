using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.StaticFiles;
using Oracle.ManagedDataAccess.Client;
using OssmmasoftVerticalSlice.ContextDB;
using OssmmasoftVerticalSlice.Helpers;
using System.Data;
using System.Security.Cryptography;

namespace OssmmasoftVerticalSlice.Features.MotorFormularios;

/// <summary>
/// Subida y descarga de adjuntos. Decision 2 de la Fase 0: los archivos van al
/// filesystem y en la base queda la ruta.
///
/// Es la superficie mas sensible del motor, y las cuatro defensas son
/// deliberadas:
///
///   1. **El nombre que manda el cliente no se usa como nombre de archivo.** Se
///      genera uno propio a partir del VALOR_ID y un GUID. Un nombre del cliente
///      puede contener `..\` y escribir fuera de la carpeta.
///   2. **La extension se valida contra una lista blanca**, no contra una lista
///      negra: una lista negra siempre se queda corta.
///   3. **El MIME que declara el cliente no se usa nunca para servir.** Al
///      descargar se resuelve por la extension real del archivo guardado. Servir
///      con el MIME del cliente permite subir un HTML y hacer que el navegador lo
///      ejecute en el dominio de la aplicacion.
///   4. **La descarga verifica el permiso VER sobre el formulario dueño.** Un
///      VALOR_ID adivinado no debe exponer el archivo de otro formulario.
/// </summary>
[ApiController]
[Route("api/MfoAdjunto")]
public class MfoAdjuntoController(ConnectionDB connectionDB, IConfiguration config) : ControllerBase
{
    private string? Usuario => Request.Headers["X-Usuario"].FirstOrDefault();

    /// <summary>
    /// Lista blanca de extensiones. Ampliarla requiere despliegue, igual que la
    /// lista blanca de catalogos y por la misma razon.
    /// </summary>
    private static readonly HashSet<string> ExtensionesPermitidas =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ".pdf", ".jpg", ".jpeg", ".png", ".gif", ".bmp",
            ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
            ".txt", ".csv", ".zip", ".rar"
        };

    private const long MaxBytesPorDefecto = 10 * 1024 * 1024;

    private string CarpetaBase()
    {
        var configurada = config["settings:MfoFiles"];
        return string.IsNullOrWhiteSpace(configurada)
            ? Path.Combine(AppContext.BaseDirectory, "MfoFiles")
            : configurada;
    }

    [HttpPost("upload")]
    public async Task<IActionResult> Upload(
        [FromQuery] int valorId,
        [FromForm] IFormFile archivo)
    {
        if (archivo is null || archivo.Length == 0)
        {
            return Ok(MfoDb.Invalid<int>("No se recibio ningun archivo."));
        }

        if (archivo.Length > MaxBytesPorDefecto)
        {
            return Ok(MfoDb.Invalid<int>(
                $"El archivo supera el limite de {MaxBytesPorDefecto / 1024 / 1024} MB."));
        }

        // Solo el nombre, nunca la ruta: un navegador puede mandar la ruta
        // completa del cliente y algunos mandan separadores de Windows.
        var nombreOriginal = Path.GetFileName(archivo.FileName.Replace('\\', '/'));
        var extension = Path.GetExtension(nombreOriginal);

        if (!ExtensionesPermitidas.Contains(extension))
        {
            return Ok(MfoDb.Invalid<int>($"La extension {extension} no esta permitida."));
        }

        var contexto = await ResolverValorAsync(valorId);
        if (contexto is null)
        {
            return Ok(MfoDb.Invalid<int>("El valor indicado no existe."));
        }

        var permiso = await MfoAutorizacion.PuedeAsync(
            connectionDB, contexto.FormularioId, MfoAutorizacion.Llenar, Usuario);
        if (!permiso.Permitido)
        {
            return Ok(MfoDb.Invalid<int>(permiso.Mensaje));
        }

        // Nombre generado por el servidor. El del cliente se conserva solo como
        // dato en NOMBRE_ARCHIVO, para mostrarlo.
        var carpeta = Path.Combine(CarpetaBase(), contexto.FormularioId.ToString());
        Directory.CreateDirectory(carpeta);

        var nombreFisico = $"{valorId}_{Guid.NewGuid():N}{extension}";
        var rutaFisica = Path.Combine(carpeta, nombreFisico);

        string hash;
        await using (var destino = System.IO.File.Create(rutaFisica))
        {
            await archivo.CopyToAsync(destino);
        }

        await using (var leer = System.IO.File.OpenRead(rutaFisica))
        {
            hash = Convert.ToHexString(await SHA256.HashDataAsync(leer)).ToLowerInvariant();
        }

        using var cn = connectionDB.GetMfoConnection();
        var openError = await MfoDb.TryOpenAsync(cn);
        if (openError is not null)
        {
            System.IO.File.Delete(rutaFisica);
            return Ok(MfoDb.Invalid<int>(openError));
        }

        using var cmd = new OracleCommand(
            @"INSERT INTO MFO_ADJUNTO (ADJUNTO_ID, VALOR_ID, NOMBRE_ARCHIVO, MIME, TAMANO_BYTES,
                                       HASH_SHA256, RUTA, USUARIO_INS, FECHA_INS)
              VALUES (SEQ_MFO_ADJUNTO.NEXTVAL, :p_ValorId, :p_Nombre, :p_Mime, :p_Tamano,
                      :p_Hash, :p_Ruta, :p_Usuario, SYSDATE)", cn)
        {
            BindByName = true
        };

        cmd.Parameters.Add("p_ValorId", OracleDbType.Int32).Value = valorId;
        cmd.Parameters.Add("p_Nombre", OracleDbType.Varchar2).Value = nombreOriginal;
        // Se guarda el MIME declarado como dato informativo, pero al servir se
        // ignora: la fuente es la extension del archivo guardado.
        cmd.Parameters.Add("p_Mime", OracleDbType.Varchar2).Value = MfoDb.DbValue(archivo.ContentType);
        cmd.Parameters.Add("p_Tamano", OracleDbType.Int64).Value = archivo.Length;
        cmd.Parameters.Add("p_Hash", OracleDbType.Varchar2).Value = hash;
        cmd.Parameters.Add("p_Ruta", OracleDbType.Varchar2).Value = rutaFisica;
        cmd.Parameters.Add("p_Usuario", OracleDbType.Varchar2).Value = MfoDb.DbValue(Usuario);

        await cmd.ExecuteNonQueryAsync();

        return Ok(new ResultDto<int>(1) { Data = 1, IsValid = true, Message = string.Empty });
    }

    [HttpGet("download")]
    public async Task<IActionResult> Download([FromQuery] int adjuntoId)
    {
        using var cn = connectionDB.GetMfoConnection();
        var openError = await MfoDb.TryOpenAsync(cn);
        if (openError is not null) return Ok(MfoDb.Invalid<string>(openError));

        using var cmd = new OracleCommand(
            @"SELECT A.NOMBRE_ARCHIVO, A.RUTA, R.FORMULARIO_ID
                FROM MFO_ADJUNTO A
                JOIN MFO_VALOR V     ON V.VALOR_ID     = A.VALOR_ID
                JOIN MFO_RESPUESTA R ON R.RESPUESTA_ID = V.RESPUESTA_ID
               WHERE A.ADJUNTO_ID = :p_AdjuntoId", cn)
        {
            BindByName = true
        };

        cmd.Parameters.Add("p_AdjuntoId", OracleDbType.Int32).Value = adjuntoId;

        string nombre, ruta;
        int formularioId;

        using (var reader = await cmd.ExecuteReaderAsync())
        {
            if (!await reader.ReadAsync())
            {
                return Ok(MfoDb.Invalid<string>("El adjunto indicado no existe."));
            }

            nombre = reader.SafeGetString("NOMBRE_ARCHIVO");
            ruta = reader.SafeGetString("RUTA");
            formularioId = reader.SafeGetInt32("FORMULARIO_ID");
        }

        // Sin esto, un ADJUNTO_ID adivinado expone el archivo de cualquier otro
        // formulario.
        var permiso = await MfoAutorizacion.PuedeAsync(
            connectionDB, formularioId, MfoAutorizacion.Ver, Usuario);
        if (!permiso.Permitido)
        {
            return Ok(MfoDb.Invalid<string>(permiso.Mensaje));
        }

        if (string.IsNullOrWhiteSpace(ruta) || !System.IO.File.Exists(ruta))
        {
            return Ok(MfoDb.Invalid<string>("El archivo ya no esta disponible en el servidor."));
        }

        // El MIME se resuelve por la extension del archivo guardado, nunca por el
        // que declaro el cliente al subirlo.
        var provider = new FileExtensionContentTypeProvider();
        if (!provider.TryGetContentType(ruta, out var contentType))
        {
            contentType = "application/octet-stream";
        }

        var bytes = await System.IO.File.ReadAllBytesAsync(ruta);
        return File(bytes, contentType, nombre);
    }

    private sealed record ValorContexto(int FormularioId, int RespuestaId);

    private async Task<ValorContexto?> ResolverValorAsync(int valorId)
    {
        using var cn = connectionDB.GetMfoConnection();
        if (await MfoDb.TryOpenAsync(cn) is not null) return null;

        using var cmd = new OracleCommand(
            @"SELECT R.FORMULARIO_ID, R.RESPUESTA_ID
                FROM MFO_VALOR V
                JOIN MFO_RESPUESTA R ON R.RESPUESTA_ID = V.RESPUESTA_ID
               WHERE V.VALOR_ID = :p_ValorId", cn)
        {
            BindByName = true
        };

        cmd.Parameters.Add("p_ValorId", OracleDbType.Int32).Value = valorId;

        using var reader = await cmd.ExecuteReaderAsync();
        return await reader.ReadAsync()
            ? new ValorContexto(reader.SafeGetInt32("FORMULARIO_ID"), reader.SafeGetInt32("RESPUESTA_ID"))
            : null;
    }
}
