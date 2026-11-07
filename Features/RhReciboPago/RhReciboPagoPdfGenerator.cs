using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace OssmmasoftVerticalSlice.Features.RhReciboPago;

public static class RhReciboPagoPdfGenerator
{
    public static byte[] Generate(IReadOnlyList<RhReciboLinea> rows, byte[] logo, string tipoNomina, bool provisional)
    {
        if (rows.Count == 0) throw new ArgumentException("No hay recibos para generar.", nameof(rows));
        // Misma configuracion del generador legacy ya utilizado por esta aplicacion.
        QuestPDF.Settings.License = LicenseType.Community;
        return Document.Create(document => document.Page(page =>
        {
            const float horizontalMargin = 0.5f;
            const float verticalMargin = 1f;
            page.Size(PageSizes.A4);
            page.Margin(15);
            if (provisional)
            {
                page.MarginVertical(verticalMargin, Unit.Inch);
                page.MarginHorizontal(horizontalMargin, Unit.Inch);
                page.Background().PaddingVertical(verticalMargin, Unit.Inch).RotateRight().Decoration(decoration =>
                {
                    decoration.Before().RotateRight().RotateRight().Element(DrawSide);
                    decoration.Content().Extend();
                    decoration.After().Element(DrawSide);
                    void DrawSide(IContainer container) => container.Height(horizontalMargin, Unit.Inch)
                        .AlignMiddle().Row(row => {
                            row.AutoItem().PaddingRight(16).Text("PROVISIONAL").FontSize(16).FontColor(Colors.Red.Medium);
                            row.RelativeItem().PaddingTop(12).ExtendHorizontal().LineHorizontal(2).LineColor(Colors.Red.Medium);
                        });
                });
            }
            page.Header().Row(row => {
                row.RelativeItem().Text("");
                row.RelativeItem().Text("");
                row.ConstantItem(125).Image(logo);
            });
            page.Content().PaddingVertical(40).Column(column =>
            {
                var contador = 0;
                foreach (var department in rows.GroupBy(row => row.CodigoIcpConcat))
                {
                    if (contador > 0) column.Item().PageBreak();
                    column.Item().AlignCenter().ShowOnce().Text($"DEPARTAMENTO : {department.Key}").SemiBold();
                    column.Item().PageBreak();
                    var inDepartment = 0;
                    foreach (var persona in department.GroupBy(row => row.CodigoPersona))
                    {
                        if (inDepartment > 0 && inDepartment % 2 == 0) column.Item().PageBreak();
                        inDepartment++;
                        contador++;
                        var lineas = persona.ToList();
                        column.Item().Row(row => row.RelativeItem().Component(new RhReciboPersonaPdf("", lineas[0], tipoNomina)));
                        column.Item().Row(row => row.RelativeItem().Component(new RhReciboDetallePdf("", lineas, contador)));
                        column.Item().Row(row => row.RelativeItem().Component(new RhReciboPiePdf(contador)));
                    }
                }
            });
        })).GeneratePdf();
    }
}
