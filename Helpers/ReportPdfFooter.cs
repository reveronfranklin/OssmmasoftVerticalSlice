using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using System.Globalization;

namespace OssmmasoftVerticalSlice.Helpers;

public sealed record ReportPrintContext(string Usuario, DateTimeOffset FechaHoraImpresion)
{
    private const string DefaultUsuario = "No identificado";
    private const string ReportTimeZoneId = "America/Caracas";

    public static ReportPrintContext Create(string? usuario)
    {
        var normalizedUsuario = string.IsNullOrWhiteSpace(usuario)
            ? DefaultUsuario
            : usuario.Trim();

        var reportTimeZone = TimeZoneInfo.FindSystemTimeZoneById(ReportTimeZoneId);
        var fechaHoraImpresion = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, reportTimeZone);

        return new ReportPrintContext(normalizedUsuario, fechaHoraImpresion);
    }
}

public static class ReportPdfFooter
{
    public static void Build(IContainer container, ReportPrintContext printContext, float fontSize)
    {
        container.Row(row =>
        {
            row.RelativeItem().AlignLeft().Text(
                $"Usuario: {printContext.Usuario} | Impreso: {printContext.FechaHoraImpresion.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture)}")
                .FontSize(fontSize);

            row.RelativeItem().AlignRight().Text(text =>
            {
                text.DefaultTextStyle(style => style.FontSize(fontSize));
                text.Span("Pagina ");
                text.CurrentPageNumber();
                text.Span(" de ");
                text.TotalPages();
            });
        });
    }
}
