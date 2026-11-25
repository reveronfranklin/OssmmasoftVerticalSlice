namespace OssmmasoftVerticalSlice.Features.Contabilidad;

// Partial rule set already present in the installed cheque/retention builder.
// Other origins require their PUC/document configuration; null is not certification.
public static class CntAutomaticoTipos
{
    private static readonly HashSet<string> Egresos = new(StringComparer.OrdinalIgnoreCase)
    { "CHEAUT", "CHEPROAUT", "CHERETAUT", "CHERETDAUT", "RETENFTDT" };

    public static bool Aplica(string origen) => Egresos.Contains(origen) || origen.Equals("ANCHEAUT", StringComparison.OrdinalIgnoreCase);

    public static string? Validar(string origen, string? codigoTipo)
    {
        if (!Aplica(origen)) return null;
        var code = codigoTipo?.Trim();
        if (string.IsNullOrEmpty(code) || code.Any(c => c is < '0' or > '9'))
            return "El tipo seleccionado no tiene un codigo numerico valido para este origen.";
        code = code.TrimStart('0');
        var required = Egresos.Contains(origen) ? "4" : "99";
        return code == required ? null : $"La combinacion de origen y tipo no es compatible. El origen {origen} requiere el codigo de tipo {(required == "4" ? "04" : "99")}.";
    }
}
