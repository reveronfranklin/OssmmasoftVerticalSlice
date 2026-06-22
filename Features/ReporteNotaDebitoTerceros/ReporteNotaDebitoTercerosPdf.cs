using Microsoft.AspNetCore.Mvc;
using OssmmasoftVerticalSlice.ContextDB;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;

namespace OssmmasoftVerticalSlice.Features.ReporteNotaDebitoTerceros;

public record ReporteNotaDebitoTercerosPdfQuery(int CodigoLotePago, int CodigoPago = 0);

public static class ReporteNotaDebitoTercerosPdfGenerator
{
    public static byte[] Generate(IReadOnlyCollection<ReporteNotaDebitoTercerosItemResponse> items, IWebHostEnvironment environment)
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
                        column.Item().Text("No se encontraron notas de debito de terceros para el lote seleccionado.").FontSize(10);
                        return;
                    }

                    var index = 0;
                    foreach (var item in orderedItems)
                    {
                        column.Item().Element(element => BuildHeader(element, item, logoBytes));
                        column.Item().PaddingTop(10).Element(element => BuildBody(element, item, culture));

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

    private static void BuildHeader(IContainer container, ReporteNotaDebitoTercerosItemResponse item, byte[]? logoBytes)
    {
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
                column.Item().PaddingTop(6).Text("NOTA DE DEBITO").Bold().FontSize(14);
            });

            table.Cell().RowSpan(4).BorderLeft(1).BorderTop(1).BorderBottom(1).Text(string.Empty);
            HeaderEmpty(table, top: true);
            table.Cell().BorderTop(1).BorderRight(1).Padding(4).AlignRight().Text(text =>
            {
                text.Span($"{item.NumeroCheque}\n").Bold();
                text.Span("N NOTA DE DEBITO:").Bold();
            });

            HeaderLabel(table, "Fecha:");
            HeaderValue(table, FormatDate(item.FechaCheque));
            HeaderLabel(table, "Banco:");
            HeaderValue(table, item.Nombre);
            HeaderLabel(table, "N de Cuenta:", bottom: true);
            HeaderValue(table, item.NumeroCuenta, bottom: true);
        });
    }

    private static void BuildBody(IContainer container, ReporteNotaDebitoTercerosItemResponse item, CultureInfo culture)
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

            table.Cell().BorderLeft(1).BorderTop(1).Padding(6).Text("Detalle Orden de Pago:").Bold();
            table.Cell().ColumnSpan(2).Border(1).Padding(6).Text(text =>
            {
                text.Span("Monto: \n").Bold();
                text.Span(FormatAmount(item.MontoOpIcpPuc, culture));
            });

            table.Cell().BorderLeft(1).BorderRight(1).BorderBottom(1).MinHeight(160).Padding(6)
                .Text(item.DetalleOpIcpPuc);
            table.Cell().BorderLeft(1).BorderBottom(1).Padding(6).Text(text =>
            {
                text.Span("Retenciones / Fondo a Tercero: \n\n").Bold();
                text.Span("Beneficiario / Proveedor:").Bold();
            });
            table.Cell().BorderRight(1).BorderBottom(1).Padding(6).AlignRight().Text(text =>
            {
                text.Span($"{FormatAmount(item.MontoImpRet, culture)}\n\n");
                text.Span(FormatAmount(item.Monto, culture));
            });

            table.Cell().ColumnSpan(3).Text(string.Empty);
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
[Route("api/ReporteNotaDebitoTerceros")]
public class ReporteNotaDebitoTercerosPdfController(ConnectionDB _connectionDB, IWebHostEnvironment _environment) : ControllerBase
{
    [HttpPost]
    [Route("pdf")]
    public async Task<IActionResult> Pdf(ReporteNotaDebitoTercerosPdfQuery value)
    {
        var handler = new ReporteNotaDebitoTercerosGetByLotePagoHandler(_connectionDB);
        var result = await handler.HandleAsync(new ReporteNotaDebitoTercerosGetByLotePagoQuery(value.CodigoLotePago, value.CodigoPago));

        if (!result.IsValid || result.Data is null)
        {
            return BadRequest(result);
        }

        var pdf = ReporteNotaDebitoTercerosPdfGenerator.Generate(result.Data, _environment);
        var fileName = $"nota-debito-terceros-{value.CodigoLotePago}.pdf";

        Response.Headers.ContentDisposition = $"inline; filename=\"{fileName}\"";
        return File(pdf, "application/pdf");
    }
}
