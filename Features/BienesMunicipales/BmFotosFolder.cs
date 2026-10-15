namespace OssmmasoftVerticalSlice.Features.BienesMunicipales;

// Normaliza solo el prefijo historico min_; nunca adivina nombres parecidos.
public static class BmFotosFolder
{
    public static IReadOnlyList<string> GetPhotos(string root, string placa)
    {
        if (string.IsNullOrWhiteSpace(placa) || placa is "." or ".." ||
            placa.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0 || placa.Contains('/') || placa.Contains('\\'))
            return Array.Empty<string>();

        var folder = Path.Combine(root, placa);
        // Enumerar completo antes de escribir: un error de acceso no produce una lista parcial.
        return Directory.GetFiles(folder, "*", SearchOption.TopDirectoryOnly)
            .Where(path => new FileInfo(path).Length > 0)
            .Select(Path.GetFileName)
            .Where(name => name is not null && !name.StartsWith('.') &&
                !name.Equals("no-product-image.png", StringComparison.OrdinalIgnoreCase) &&
                new[] { ".jpg", ".jpeg", ".png", ".webp" }.Contains(Path.GetExtension(name).ToLowerInvariant()))
            .Select(name => name!.StartsWith("min_", StringComparison.OrdinalIgnoreCase) ? name[4..] : name!)
            .Where(name => !string.IsNullOrWhiteSpace(Path.GetFileNameWithoutExtension(name)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }
}
