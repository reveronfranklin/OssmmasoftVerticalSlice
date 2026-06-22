using Microsoft.AspNetCore.Mvc;
using OssmmasoftVerticalSlice.ContextDB;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;

namespace OssmmasoftVerticalSlice.Features.ReportePagoElectronico;

public record ReportePagoElectronicoPdfQuery(int CodigoLotePago, int? CodigoPago = null);

public static class ReportePagoElectronicoPdfGenerator
{
    public static byte[] Generate(
        IReadOnlyCollection<ReportePagoElectronicoItemResponse> items,
        IWebHostEnvironment environment,
        bool esTerceros)
    {
        QuestPDF.Settings.License = LicenseType.Evaluation;

        var logoBytes = TryReadReportAsset(environment, "logoLeft.jpeg");
        var culture = CultureInfo.GetCultureInfo("es-VE");
        var orderedItems = items.ToList();

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.MarginLeft(20);
                page.MarginRight(60);
                page.MarginTop(30);
                page.MarginBottom(70);
                page.DefaultTextStyle(style => style.FontSize(8));

                page.Content().Column(column =>
                {
                    if (orderedItems.Count == 0)
                    {
                        column.Item().Text("No se encontraron pagos para el lote seleccionado.").FontSize(10);
                        return;
                    }

                    var index = 0;
                    foreach (var item in orderedItems)
                    {
                        column.Item().Element(element => BuildHeader(element, item, logoBytes));
                        column.Item().PaddingTop(10).Element(element => BuildBody(element, item, esTerceros, culture));

                        index++;
                        if (index < orderedItems.Count)
                        {
                            column.Item().PageBreak();
                        }
                    }
                });

                page.Footer().AlignRight().Text(text =>
                {
                    text.Span("Pagina ");
                    text.CurrentPageNumber();
                    text.Span(" de ");
                    text.TotalPages();
                });
            });
        }).GeneratePdf();
    }

    private static void BuildHeader(IContainer container, ReportePagoElectronicoItemResponse item, byte[]? logoBytes)
    {
        var reportTitle = string.IsNullOrWhiteSpace(item.TituloReporte)
            ? "NOTA DE DEBITO"
            : item.TituloReporte.Trim().ToUpperInvariant();
        var isElectronicPayment = reportTitle.Contains("ELECTRONICO", StringComparison.OrdinalIgnoreCase)
            || reportTitle.Contains("ELECTR", StringComparison.OrdinalIgnoreCase);

        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn();
                columns.RelativeColumn();
                columns.ConstantColumn(20);
                columns.ConstantColumn(20);
                columns.RelativeColumn();
                columns.RelativeColumn();
            });

            table.Cell().RowSpan(4).ColumnSpan(3).Border(1).Padding(6).Column(column =>
            {
                column.Item().Height(45).Element(element =>
                {
                    if (logoBytes is not null)
                    {
                        element.Image(logoBytes).FitArea();
                    }
                    else
                    {
                        element.Text("LOGO").Bold();
                    }
                });
                column.Item().PaddingTop(6).Text(reportTitle).Bold().FontSize(isElectronicPayment ? 12 : 14);
            });

            table.Cell().RowSpan(4).BorderLeft(1).BorderTop(1).BorderBottom(1).Text(string.Empty);
            HeaderEmpty(table, top: true);
            table.Cell().BorderTop(1).BorderRight(1).Padding(4).AlignRight().Text(text =>
            {
                text.Span($"{item.NumeroPago}\n").Bold();
                text.Span(isElectronicPayment ? "N PAGO ELECTRONICO:" : "N NOTA DE DEBITO:").Bold();
            });

            HeaderLabel(table, "Fecha:");
            HeaderValue(table, FormatDate(item.FechaPago));
            HeaderLabel(table, "Banco:");
            HeaderValue(table, item.Nombre);
            HeaderLabel(table, "N de Cuenta:", bottom: true);
            HeaderValue(table, item.NumeroCuenta, bottom: true);
        });
    }

    private static void BuildBody(IContainer container, ReportePagoElectronicoItemResponse item, bool esTerceros, CultureInfo culture)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(72);
                columns.RelativeColumn(12);
                columns.RelativeColumn(16);
            });

            table.Cell().ColumnSpan(3).Border(1).MinHeight(50).Padding(6).Text(text =>
            {
                text.Span("HEMOS RECIBIDO DEL CONCEJO MUNICIPAL DE CHACAO, POR CONCEPTO DE: \n").Bold();
                text.Span(item.PagarALaOrdenDe);
            });

            table.Cell().ColumnSpan(3).Border(1).MinHeight(100).Padding(6).Text(text =>
            {
                text.Span("Motivo: \n").Bold();
                text.Span(item.Motivo);
            });

            table.Cell().BorderLeft(1).BorderTop(1).Padding(6).Text("N Control PAEL:").Bold();
            table.Cell().ColumnSpan(2).Border(1).Padding(6).Text(text =>
            {
                text.Span("Monto: \n").Bold();
                text.Span(FormatAmount(item.MontoOpIcpPuc, culture));
            });

            table.Cell().BorderLeft(1).BorderRight(1).MinHeight(120).Padding(6).Text(text =>
            {
                text.Span("Detalle Orden de Pago: \n").Bold();
                text.Span(item.DetalleOpIcpPuc);
            });
            table.Cell().BorderLeft(1).Text(string.Empty);
            table.Cell().BorderRight(1).Text(string.Empty);

            table.Cell().BorderLeft(1).Padding(6).Text(text =>
            {
                text.Span("Impuestos y Deducciones: \n").Bold();
                text.Span(esTerceros ? string.Empty : item.DetalleImpRet);
            });
            table.Cell().ColumnSpan(2).BorderLeft(1).BorderRight(1).Padding(6).AlignRight()
                .Text(esTerceros ? string.Empty : FormatAmount(item.MontoImpRet, culture));

            table.Cell().BorderLeft(1).BorderBottom(1).Padding(6).Text("Recibi Conforme:___________________________________\n\nC.I.:\nFecha:");
            table.Cell().ColumnSpan(2).Border(1).Padding(6).Text(text =>
            {
                text.Span("Total: \n").Bold();
                text.Span(FormatAmount(item.Monto, culture));
            });
        });
    }

    private static void HeaderEmpty(TableDescriptor table, bool top = false)
    {
        if (top)
        {
            table.Cell().BorderTop(1).Text(string.Empty);
            return;
        }

        table.Cell().Text(string.Empty);
    }

    private static void HeaderLabel(TableDescriptor table, string label, bool bottom = false)
    {
        var cell = table.Cell().Padding(3);
        if (bottom)
        {
            cell = cell.BorderBottom(1);
        }

        cell.Text(label).Bold();
    }

    private static void HeaderValue(TableDescriptor table, string value, bool bottom = false)
    {
        var cell = table.Cell().BorderRight(1).Padding(3);
        if (bottom)
        {
            cell = cell.BorderBottom(1);
        }

        cell.Text(value);
    }

    private static string FormatDate(DateTime? value)
    {
        return value.HasValue ? value.Value.ToString("dd/MM/yy", CultureInfo.InvariantCulture) : string.Empty;
    }

    private static string FormatAmount(decimal value, CultureInfo culture)
    {
        return value.ToString("N2", culture);
    }

    private static byte[]? TryReadReportAsset(IWebHostEnvironment environment, string fileName)
    {
        var path = Path.Combine(environment.ContentRootPath, "Assets", "Reports", fileName);
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }
}

[ApiController]
[Route("api/ReportePagoElectronico")]
public class ReportePagoElectronicoPdfController(ConnectionDB _connectionDB, IWebHostEnvironment _environment) : ControllerBase
{
    [HttpPost]
    [Route("pdf")]
    public async Task<IActionResult> Pdf(ReportePagoElectronicoPdfQuery value)
    {
        return await GeneratePdfAsync(value, esTerceros: false, filePrefix: "pago-electronico");
    }

    [HttpPost]
    [Route("terceros/pdf")]
    public async Task<IActionResult> TercerosPdf(ReportePagoElectronicoPdfQuery value)
    {
        return await GeneratePdfAsync(value, esTerceros: true, filePrefix: "pago-electronico-terceros");
    }

    private async Task<IActionResult> GeneratePdfAsync(ReportePagoElectronicoPdfQuery value, bool esTerceros, string filePrefix)
    {
        var handler = new ReportePagoElectronicoGetByLoteHandler(_connectionDB);
        var result = await handler.HandleAsync(new ReportePagoElectronicoGetByLoteQuery(value.CodigoLotePago));

        if (!result.IsValid || result.Data is null)
        {
            return BadRequest(result);
        }

        var pdf = ReportePagoElectronicoPdfGenerator.Generate(result.Data, _environment, esTerceros);
        var fileName = $"{filePrefix}-{value.CodigoLotePago}.pdf";

        Response.Headers.ContentDisposition = $"inline; filename=\"{fileName}\"";
        return File(pdf, "application/pdf");
    }
}
