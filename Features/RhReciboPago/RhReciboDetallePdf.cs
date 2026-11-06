using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
namespace OssmmasoftVerticalSlice.Features.RhReciboPago;

    public class RhReciboDetallePdf : IComponent
    {
    private readonly int _contador;

    private string Title { get; }

        public List<RhReciboLinea> ModelRecibos { get; }
        public RhReciboDetallePdf(string title, List<RhReciboLinea> modelRecibos , int contador )
        {
            Title = title;
            ModelRecibos  = modelRecibos;
            _contador = contador;
    }

        public void Compose(IContainer container)
        {
            var headerStyle = TextStyle.Default.SemiBold().FontSize(7);
            NumberFormatInfo formato = new CultureInfo("es-AR").NumberFormat;

            formato.CurrencyGroupSeparator = ".";
            formato.NumberDecimalSeparator = ",";
            formato.NumberDecimalDigits = 2;

            container.Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {

                    columns.ConstantColumn(190);
                    columns.ConstantColumn(120);
                    columns.ConstantColumn(20);
                    columns.RelativeColumn(1);
                    columns.RelativeColumn(1);
                    columns.RelativeColumn(1);

                });

                table.Header(header =>
                {

                    header.Cell().Border(1).AlignCenter().Text("CONCEPTO").Style(headerStyle);
                    header.Cell().Border(1).AlignCenter().Text("COMPLEMENTO").Style(headerStyle);
                    header.Cell().Border(1).AlignCenter().Text("%").Style(headerStyle);
                    header.Cell().Border(1).AlignCenter().ScaleToFit().Text("ACUMULADO").Style(headerStyle);
                    header.Cell().Border(1).AlignCenter().ScaleToFit().Text("ASIGNACIONES").Style(headerStyle);
                    header.Cell().Border(1).AlignCenter().ScaleToFit().Text("DEDUCCIONES").Style(headerStyle);

                });

                foreach (var item in ModelRecibos)
                {

                        table.Cell().BorderLeft(1).BorderRight(1).PaddingLeft(5).PaddingTop(5).Element(CellStyle).AlignCenter().ExtendHorizontal().Text($"{item.DenominacionConcepto}").FontSize(7);
                        table.Cell().BorderLeft(1).BorderRight(1).Element(CellStyle).AlignCenter().Text($"{item.ComplementoConcepto}").FontSize(7);
                        table.Cell().BorderLeft(1).BorderRight(1).Element(CellStyle).AlignCenter().Text(item.Porcentaje).FontSize(7);
                        table.Cell().BorderLeft(1).BorderRight(1).ScaleToFit().Element(CellStyle).AlignCenter().Text("").FontSize(7);
                        var asignacion = item.Asignacion.ToString("N", formato);
                        var deduccion = item.Deduccion.ToString("N", formato);
                        table.Cell().BorderLeft(1).BorderRight(1).Element(CellStyle).AlignRight().ScaleToFit().Padding(2).Text($"{asignacion}").FontSize(7);
                        table.Cell().BorderLeft(1).BorderRight(1).Element(CellStyle).AlignRight().ScaleToFit().Padding(2).Text($"{deduccion}").FontSize(7);

                        static IContainer CellStyle(IContainer container) => container.Border(0);

                }

                    table.Cell().BorderLeft(1).BorderRight(1).Text($"").FontSize(7);
                    table.Cell().BorderRight(1).BorderLeft(1).Text($"").FontSize(7);
                    table.Cell().BorderRight(1).Text($"").FontSize(7);
                    table.Cell().BorderRight(1).Text($"").FontSize(7);

                    var asig = ModelRecibos.Sum(x => x.Asignacion);
                    var ded = ModelRecibos.Sum(x => x.Deduccion);
                    var totalAsignacion = asig.ToString("N", formato);
                    var totalDeduccion = ded.ToString("N", formato);
                    var neto = asig - ded;

                    table.Cell().Border(1).AlignRight().Padding(2).Text($"{totalAsignacion}").FontSize(8).SemiBold();
                    table.Cell().Border(1).AlignRight().Padding(2).Text($"{totalDeduccion}").FontSize(8).SemiBold();

                    table.Cell().BorderBottom(1).BorderRight(1).BorderLeft(1).Text($"").FontSize(7);
                    table.Cell().BorderBottom(1).BorderRight(1).Text($"").FontSize(7);
                    table.Cell().BorderBottom(1).BorderRight(1).Text($"").FontSize(7);
                    table.Cell().BorderBottom(1).Text($"").FontSize(7);
                    table.Cell().Border(1).Background(Colors.Grey.Lighten3).AlignRight().PaddingRight(2).Text($"NETO A COBRAR").FontSize(7).SemiBold();
                    table.Cell().Border(1).Background(Colors.Grey.Lighten3).AlignRight().PaddingRight(2).Text($"{neto.ToString("N", formato)}").FontSize(7).SemiBold();

            });

        }
    }
