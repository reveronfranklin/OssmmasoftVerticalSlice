using QuestPDF.Fluent;
using QuestPDF.Infrastructure;

namespace OssmmasoftVerticalSlice.Helpers;

public static class ReportPdfFirmantes
{
    private const string FirmanteBeneficiario = "BENEFICIARIO";
    private const string FirmanteDirectorAdministracion = "DIRECTOR(A) DE ADMINISTRACIÓN";

    public static void Build(IContainer container, float fontSize)
    {
        container.PaddingTop(20).Row(row =>
        {
            row.RelativeItem().Element(element => FirmanteLine(element, FirmanteBeneficiario, fontSize));
            row.ConstantItem(60);
            row.RelativeItem().Element(element => FirmanteLine(element, FirmanteDirectorAdministracion, fontSize));
        });
    }

    private static void FirmanteLine(IContainer container, string label, float fontSize)
    {
        container.Column(column =>
        {
            column.Item().PaddingHorizontal(12).LineHorizontal(0.8f);
            column.Item().PaddingTop(2).AlignCenter().Text(label).Bold().FontSize(fontSize);
        });
    }
}
