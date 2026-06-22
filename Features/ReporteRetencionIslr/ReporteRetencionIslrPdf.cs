using Microsoft.AspNetCore.Mvc;
using OssmmasoftVerticalSlice.ContextDB;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;

namespace OssmmasoftVerticalSlice.Features.ReporteRetencionIslr;

public record ReporteRetencionIslrPdfQuery(int CodigoOrdenPago);

public static class ReporteRetencionIslrPdfGenerator
{
    public static byte[] Generate(ReporteRetencionIslrResponse data, IWebHostEnvironment environment)
    {
        QuestPDF.Settings.License = LicenseType.Evaluation;

        var logoBytes = TryReadReportAsset(environment, "logoLeft.jpeg");
        var culture = CultureInfo.GetCultureInfo("es-VE");
        var header = data.Header ?? throw new InvalidOperationException("El comprobante ISLR no contiene cabecera.");

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter.Landscape());
                page.Margin(16);
                page.DefaultTextStyle(style => style.FontSize(7));

                page.Header().Column(column =>
                {
                    column.Item().Element(element => BuildHeader(element, logoBytes));
                    column.Item().PaddingTop(6).Element(element => BuildSubHeader(element, header));
                });

                page.Content().PaddingTop(8).Element(element => BuildDocuments(element, data.Documentos, culture));

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

    private static void BuildHeader(IContainer container, byte[]? logoBytes)
    {
        container.Row(row =>
        {
            row.ConstantItem(160).Column(column =>
            {
                column.Item().Height(38).Element(element =>
                {
                    if (logoBytes is not null)
                    {
                        element.AlignLeft().Image(logoBytes).FitArea();
                    }
                    else
                    {
                        element.Text("LOGO").Bold();
                    }
                });
                column.Item().Text("CONCEJO MUNICIPAL DEL MUNICIPIO CHACAO").Bold().FontSize(6.5f);
            });

            row.RelativeItem().AlignCenter().AlignMiddle().Text("COMPROBANTE DE RETENCION ISLR").Bold().FontSize(13);
            row.ConstantItem(160);
        });
    }

    private static void BuildSubHeader(IContainer container, ReporteRetencionIslrHeaderResponse header)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(2);
                columns.RelativeColumn(1.7f);
                columns.RelativeColumn();
                columns.ConstantColumn(95);
            });

            InfoCell(table, "Nombre o Razon Social del Agente de Retencion", header.NombreAgenteRetencion);
            InfoCell(table, "Telefonos del Agente de Retencion", header.TelefonoAgenteRetencion);
            InfoCell(table, "RIF del Agente de Retencion", header.RifAgenteRetencion);
            InfoCell(table, "FECHA", FormatDate(header.Fecha));
            InfoCell(table, "Direccion Fiscal del Agente de Retencion", header.DireccionAgenteRetencion, colSpan: 2);
            InfoCell(table, "Periodo Fiscal", FormatFiscalPeriod(header.Fecha), colSpan: 2);
            InfoCell(table, "Nombre o Razon Social del Sujeto Retenido", header.NombreSujetoRetenido, colSpan: 2);
            InfoCell(table, "RIF del Sujeto Retenido", header.RifSujetoRetenido);
            InfoCell(table, "Nro. Orden Pago", header.NumeroOrdenPago);
        });
    }

    private static void BuildDocuments(IContainer container, IReadOnlyCollection<ReporteRetencionIslrDocumentoResponse> documentos, CultureInfo culture)
    {
        var totalBase = documentos.Sum(item => item.BaseImponible);
        var totalRetenido = documentos.Sum(item => item.IslrRetenido);

        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(80);
                columns.ConstantColumn(58);
                columns.RelativeColumn();
                columns.ConstantColumn(78);
                columns.ConstantColumn(78);
                columns.ConstantColumn(68);
                columns.ConstantColumn(78);
                columns.ConstantColumn(64);
            });

            TableHeader(table, "No Factura");
            TableHeader(table, "Fecha Factura");
            TableHeader(table, "Concepto de Pago");
            TableHeader(table, "Impuesto Exento");
            TableHeader(table, "Base Imponible");
            TableHeader(table, "% Alicuota");
            TableHeader(table, "ISLR Retenido");
            TableHeader(table, "Sustraendo");

            foreach (var item in documentos)
            {
                BodyCell(table, item.NumeroFactura);
                BodyCell(table, FormatDate(item.FechaFactura));
                BodyCell(table, item.ConceptoPago);
                BodyCell(table, FormatAmount(item.ImpuestoExento, culture), alignRight: true);
                BodyCell(table, FormatAmount(item.BaseImponible, culture), alignRight: true);
                BodyCell(table, item.Alicuota, alignRight: true);
                BodyCell(table, FormatAmount(item.IslrRetenido, culture), alignRight: true);
                BodyCell(table, FormatAmount(item.Sustraendo, culture), alignRight: true);
            }

            if (documentos.Count == 0)
            {
                table.Cell().ColumnSpan(8).Border(1).Padding(4).Text("Sin documentos registrados para el comprobante ISLR.").FontSize(7);
            }

            EmptyCell(table, colSpan: 3);
            TotalCell(table, "TOTALES", alignRight: true);
            TotalCell(table, FormatAmount(totalBase, culture), alignRight: true);
            TotalCell(table, string.Empty);
            TotalCell(table, FormatAmount(totalRetenido, culture), alignRight: true);
            TotalCell(table, string.Empty);
        });
    }

    private static void InfoCell(TableDescriptor table, string label, string value, uint colSpan = 1)
    {
        var tableCell = colSpan > 1 ? table.Cell().ColumnSpan(colSpan) : table.Cell();
        tableCell.Border(1).Padding(3).Column(column =>
        {
            column.Item().Text(label).Bold().FontSize(6);
            column.Item().Text(value).FontSize(6.8f);
        });
    }

    private static void TableHeader(TableDescriptor table, string text)
    {
        table.Cell().Background(Colors.Grey.Lighten2).Border(1).Padding(3).AlignCenter().Text(text).Bold().FontSize(6);
    }

    private static void BodyCell(TableDescriptor table, string text, bool alignRight = false)
    {
        var cell = table.Cell().Border(1).Padding(3);
        if (alignRight)
        {
            cell = cell.AlignRight();
        }

        cell.Text(text).FontSize(6.2f);
    }

    private static void EmptyCell(TableDescriptor table, uint colSpan = 1)
    {
        var tableCell = colSpan > 1 ? table.Cell().ColumnSpan(colSpan) : table.Cell();
        tableCell.Padding(3).Text(string.Empty);
    }

    private static void TotalCell(TableDescriptor table, string text, bool alignRight = false)
    {
        var cell = table.Cell().Background(Colors.Grey.Lighten3).Padding(3);
        if (alignRight)
        {
            cell = cell.AlignRight();
        }

        cell.Text(text).Bold().FontSize(6.3f);
    }

    private static string FormatDate(DateTime? value)
    {
        return value.HasValue ? value.Value.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) : string.Empty;
    }

    private static string FormatFiscalPeriod(DateTime? value)
    {
        return value.HasValue ? $"Ano: {value.Value:yyyy} Mes: {value.Value:MM}" : string.Empty;
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
[Route("api/ReporteRetencionIslr")]
public class ReporteRetencionIslrPdfController(ConnectionDB _connectionDB, IWebHostEnvironment _environment) : ControllerBase
{
    [HttpPost]
    [Route("pdf")]
    public async Task<IActionResult> Pdf(ReporteRetencionIslrPdfQuery value)
    {
        var handler = new ReporteRetencionIslrGetByCodigoHandler(_connectionDB);
        var result = await handler.HandleAsync(new ReporteRetencionIslrGetByCodigoQuery(value.CodigoOrdenPago));

        if (!result.IsValid || result.Data is null)
        {
            return BadRequest(result);
        }

        var pdf = ReporteRetencionIslrPdfGenerator.Generate(result.Data, _environment);
        var fileName = $"retencion-islr-{value.CodigoOrdenPago}.pdf";

        Response.Headers.ContentDisposition = $"inline; filename=\"{fileName}\"";
        return File(pdf, "application/pdf");
    }
}
