using System.Text.RegularExpressions;

namespace OssmmasoftVerticalSlice.Features.FacturacionElectronica;

// Validacion del emisor en el backend. Hasta la Fase E el patron del RIF solo lo
// revisaba el navegador (forms/rules.ts): quien llamara a la API sin pasar por
// la pantalla podia guardar un RIF como "hola", y ReglasYFlujos.md citaba este
// validador sin que existiera.
//
// Mismo criterio que FacturaValidador, GuiaValidador y RetencionValidador: una
// clase estatica en la subcarpeta de su area, que devuelve el mensaje de rechazo
// o null. Sin excepciones: el contrato del proyecto pide IsValid = false.
public static class EmisorValidador
{
    // El mismo patron que el formulario: letra del tipo de contribuyente, ocho
    // digitos y el digito final, separados por guion. Solo el formato: el digito
    // verificador del SENIAT no se calcula, igual que en la pantalla.
    private static readonly Regex PatronRif = new(@"^[VEJPG]-\d{8}-\d$", RegexOptions.Compiled);

    // El RIF tal como se guarda: sin espacios y en mayusculas. La pantalla acepta
    // "j-12345678-9", y como el UNIQUE de FED_EMISOR distingue mayusculas, sin
    // normalizar "j-..." y "J-..." serian dos emisores con el mismo RIF, que es
    // justo lo que el Art. 30 impide.
    public static string NormalizarRif(string? rif) => (rif ?? string.Empty).Trim().ToUpperInvariant();

    public static string? ValidarRif(string rifNormalizado) =>
        PatronRif.IsMatch(rifNormalizado)
            ? null
            : "El RIF debe tener el formato J-12345678-9.";
}
