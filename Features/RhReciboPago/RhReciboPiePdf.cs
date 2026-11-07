using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
namespace OssmmasoftVerticalSlice.Features.RhReciboPago;

  public class RhReciboPiePdf : IComponent
    {
    private readonly int _contador;

    public RhReciboPiePdf(int contador)
    {

        _contador = contador;
    }

    public void Compose(IContainer container)
    {
        var headerStyle = TextStyle.Default.SemiBold().FontSize(7);
        NumberFormatInfo formato = new CultureInfo("es-AR").NumberFormat;
        static IContainer CellStyle(IContainer container) => container.BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingVertical(2);
        formato.CurrencyGroupSeparator = ".";
        formato.NumberDecimalSeparator = ",";
        formato.NumberDecimalDigits = 2;
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {

                columns.ConstantColumn(50);
                columns.ConstantColumn(400);
                columns.RelativeColumn();

            });

            table.Cell().Border(1).AlignCenter().Element(CellStyle).Text($"No {_contador} ").SemiBold().FontSize(7);
            table.Cell().Border(1).AlignCenter().Element(CellStyle).Text($"Liquidacion de Sueldos y Salarios. Acepto que despues de hechas las  deducciones de mi sueldo o salario, he recibido conforme el saldo abajo indicado, en pago de los servicios que he prestado hasta la fecha que se indica.").SemiBold().FontSize(7);
            table.Cell().Border(1).AlignMiddle().AlignCenter().Element(CellStyle).Text($"________________________ \n" + "Firma del Beneficiario ").SemiBold().FontSize(7);

            table.Cell().ColumnSpan(3).PaddingVertical(20);

        });

    }
    }
