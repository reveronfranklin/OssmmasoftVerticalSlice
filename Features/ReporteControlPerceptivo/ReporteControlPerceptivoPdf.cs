using Microsoft.AspNetCore.Mvc;
using OssmmasoftVerticalSlice.ContextDB;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;

namespace OssmmasoftVerticalSlice.Features.ReporteControlPerceptivo;

public record ReporteControlPerceptivoPdfQuery(int CodigoCompromiso);

public static class ReporteControlPerceptivoPdfGenerator
{
    public static byte[] Generate(ReporteControlPerceptivoResponse data, IWebHostEnvironment environment)
    {
        QuestPDF.Settings.License = LicenseType.Evaluation;

        var logoBytes = TryReadReportAsset(environment, "logoLeft.jpeg");
        var culture = CultureInfo.GetCultureInfo("es-VE");
        var header = data.Header ?? throw new InvalidOperationException("El compromiso no contiene cabecera.");
        var parrafoLegal = BuildParrafoLegal(header, data.MontoTotal, culture);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(22);
                page.DefaultTextStyle(style => style.FontSize(8));

                page.Header().Element(element => BuildHeader(element, header, logoBytes));
                page.Content().PaddingTop(8).Column(column =>
                {
                    column.Item().Element(element => BuildParrafoLegalElement(element, parrafoLegal));
                    column.Item().PaddingTop(6).Element(element => BuildDetalle(element, data, culture));
                    column.Item().PaddingTop(6).Element(BuildConformidad);
                    column.Item().PaddingTop(18).Element(BuildFirmas);
                });

                page.Footer().Column(column =>
                {
                    column.Item().AlignCenter().Text("ADMINISTRACION").Bold().FontSize(8);
                    column.Item().AlignCenter().Text("Forma SAMI-ADM_CONTROL_PERCEPTIVO").FontSize(6.5f);
                });
            });
        }).GeneratePdf();
    }

    private static string BuildParrafoLegal(ReporteControlPerceptivoHeaderResponse header, decimal montoTotal, CultureInfo culture)
    {
        return "En el " + header.NombreEmpresa + " " + header.FechaEmisionTexto
            + ", en la oficina de la Direccion de Administracion, situada en " + header.DireccionEmpresa
            + " se constituyeron los ciudadanos _____________________________________ y" +
            " _____________________________________ , titulares de la Cedula de Identidad Nros:" +
            " _____________ y _____________ funcionario de la unidad Solicitante y de la empresa" +
            " respectivamente, para efectuar el Control Perceptivo sobre el Contenido del Compromiso" +
            " Presupuestario No." + header.NumeroCompromiso + " de fecha "
            + FormatDate(header.FechaCompromiso) + ", por un monto de Bs." + FormatAmount(montoTotal, culture)
            + " a favor de la Empresa " + header.Proveedor + " por la descripcion siguiente:";
    }

    private static void BuildHeader(IContainer container, ReporteControlPerceptivoHeaderResponse header, byte[]? logoBytes)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(2.2f);
                columns.RelativeColumn(3);
            });

            table.Cell().Border(1).Padding(4).Height(46).Element(element =>
            {
                if (logoBytes is not null)
                {
                    element.AlignLeft().Image(logoBytes).FitArea();
                }
                else
                {
                    element.AlignMiddle().Text(header.NombreEmpresa).Bold();
                }
            });

            table.Cell().Border(1).Padding(4).AlignMiddle().AlignCenter().Text("CONTROL PERCEPTIVO").Bold().FontSize(12);
        });
    }

    private static void BuildParrafoLegalElement(IContainer container, string parrafoLegal)
    {
        container.Border(1).Padding(6).Text(parrafoLegal).FontSize(7.5f);
    }

    private static void BuildDetalle(IContainer container, ReporteControlPerceptivoResponse data, CultureInfo culture)
    {
        container.Column(column =>
        {
            column.Item().Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.ConstantColumn(45);
                    columns.ConstantColumn(70);
                    columns.RelativeColumn();
                    columns.ConstantColumn(70);
                    columns.ConstantColumn(70);
                });

                table.Cell().ColumnSpan(5).Background(Colors.Grey.Lighten2).Border(1).Padding(3).AlignCenter()
                    .Text("DETALLES DEL CONTROL PERCEPTIVO").Bold().FontSize(7);

                TableHeader(table, "CANTIDAD");
                TableHeader(table, "UNIDAD DE MEDIDA");
                TableHeader(table, "DESCRIPCION DE LOS ART. O SERVICIOS");
                TableHeader(table, "PRECIO UNITARIO");
                TableHeader(table, "TOTAL BOLIVARES");

                foreach (var item in data.Detalle)
                {
                    BodyCell(table, item.Cantidad == 0 ? string.Empty : FormatDecimal(item.Cantidad));
                    BodyCell(table, item.Udm);
                    BodyCell(table, item.DescripcionArticulo);
                    BodyCell(table, FormatAmount(item.PrecioUnitario, culture), alignRight: true);
                    BodyCell(table, FormatAmount(item.Precio, culture), alignRight: true);
                }

                if (data.Detalle.Count == 0)
                {
                    BodyCell(table, string.Empty);
                    BodyCell(table, string.Empty);
                    BodyCell(table, "Sin lineas registradas");
                    BodyCell(table, string.Empty);
                    BodyCell(table, string.Empty);
                }
            });

            column.Item().PaddingTop(4).Table(table =>
            {
                table.ColumnsDefinition(columns =>
                {
                    columns.RelativeColumn(2);
                    columns.ConstantColumn(70);
                    columns.ConstantColumn(70);
                });

                table.Cell().RowSpan(3).Border(1).Padding(4).Column(inner =>
                {
                    inner.Item().Text("MONTO TOTAL EN LETRA").Bold().FontSize(6.5f);
                    inner.Item().Text(data.MontoLetras).FontSize(7);
                });

                InfoAmountCell(table, "Sub TOTAL", FormatAmount(data.SubTotal, culture));
                InfoAmountCell(table, "IVA", FormatAmount(data.MontoImpuesto, culture));
                InfoAmountCell(table, "TOTAL", FormatAmount(data.MontoTotal, culture), bold: true);
            });
        });
    }

    private static void BuildConformidad(IContainer container)
    {
        container.Text(
            "En la recepcion y verificacion efectuada se constato que la mercancia entregada por la firma" +
            " proveedora antes indicada, se ajusta a las caracteristicas exigidas en el Compromiso" +
            " Presupuestario, asi mismo se deja constancia que el material o servicio senalado se entrego" +
            " dentro del plazo establecido.\n\nEn prueba a su conformidad, Firman:")
            .FontSize(7.5f);
    }

    private static void BuildFirmas(IContainer container)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn();
                columns.RelativeColumn();
            });

            table.Cell().Border(1).Height(60).Padding(4).AlignBottom().AlignCenter().Text("UNIDAD SOLICITANTE").Bold().FontSize(7);
            table.Cell().Border(1).Height(60).Padding(4).AlignBottom().AlignCenter().Text("POR EL PROVEEDOR").Bold().FontSize(7);
        });
    }

    private static void TableHeader(TableDescriptor table, string text)
    {
        table.Cell().Background(Colors.Grey.Lighten2).Border(1).Padding(3).AlignCenter().Text(text).Bold().FontSize(6.5f);
    }

    private static void BodyCell(TableDescriptor table, string text, bool alignRight = false)
    {
        var cell = table.Cell().Border(1).Padding(3);
        if (alignRight)
        {
            cell = cell.AlignRight();
        }

        cell.Text(text).FontSize(6.8f);
    }

    private static void InfoAmountCell(TableDescriptor table, string label, string value, bool bold = false)
    {
        var cell = table.Cell().Border(1).Padding(3).AlignRight();
        var text = cell.Text($"{label}   {value}").FontSize(7);
        if (bold)
        {
            text.Bold();
        }
    }

    private static string FormatDate(DateTime? value)
    {
        return value.HasValue ? value.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) : string.Empty;
    }

    private static string FormatAmount(decimal value, CultureInfo culture)
    {
        return value.ToString("N2", culture);
    }

    private static string FormatDecimal(decimal value)
    {
        return value == decimal.Truncate(value)
            ? value.ToString("N0", CultureInfo.InvariantCulture)
            : value.ToString("N2", CultureInfo.InvariantCulture);
    }

    private static byte[]? TryReadReportAsset(IWebHostEnvironment environment, string fileName)
    {
        var path = Path.Combine(environment.ContentRootPath, "Assets", "Reports", fileName);
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }
}

[ApiController]
[Route("api/ReporteControlPerceptivo")]
public class ReporteControlPerceptivoPdfController(ConnectionDB _connectionDB, IWebHostEnvironment _environment) : ControllerBase
{
    [HttpPost]
    [Route("pdf")]
    public async Task<IActionResult> Pdf(ReporteControlPerceptivoPdfQuery value)
    {
        var handler = new ReporteControlPerceptivoGetByCodigoHandler(_connectionDB);
        var result = await handler.HandleAsync(new ReporteControlPerceptivoGetByCodigoQuery(value.CodigoCompromiso));

        if (!result.IsValid || result.Data is null)
        {
            return BadRequest(result);
        }

        var pdf = ReporteControlPerceptivoPdfGenerator.Generate(result.Data, _environment);
        var fileName = $"control-perceptivo-{value.CodigoCompromiso}.pdf";

        Response.Headers.ContentDisposition = $"inline; filename=\"{fileName}\"";
        return File(pdf, "application/pdf");
    }
}
