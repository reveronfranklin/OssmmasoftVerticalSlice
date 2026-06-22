using Microsoft.AspNetCore.Mvc;
using OssmmasoftVerticalSlice.ContextDB;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;

namespace OssmmasoftVerticalSlice.Features.ReporteTimbreFiscal;

public record ReporteTimbreFiscalPdfQuery(int CodigoOrdenPago);

public static class ReporteTimbreFiscalPdfGenerator
{
    public static byte[] Generate(ReporteTimbreFiscalResponse data, IWebHostEnvironment environment)
    {
        QuestPDF.Settings.License = LicenseType.Evaluation;

        var logoLeftBytes = TryReadReportAsset(environment, "logoLeft.jpeg");
        var logoRightBytes = TryReadReportAsset(environment, "logoRight.jpg");
        var culture = CultureInfo.GetCultureInfo("es-VE");
        var header = data.Header ?? throw new InvalidOperationException("El reporte de timbre fiscal no contiene cabecera.");
        var totalGrossAmount = data.Documentos.Sum(item => item.MontoDocumento);
        var totalAmountVat = data.Documentos.Sum(item => item.MontoIva);
        var totalTaxExempt = data.Documentos.Sum(item => item.MontoExento);
        var totalNetTaxableIncome = CalculateTaxableIncome(header.BaseImponible, totalGrossAmount, totalTaxExempt, totalAmountVat);

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(24);
                page.DefaultTextStyle(style => style.FontSize(8));

                page.Header().Column(column =>
                {
                    column.Item().Element(element => BuildHeader(element, header, logoLeftBytes, logoRightBytes));
                    column.Item().PaddingTop(10).Element(element => BuildSubHeader(element, header));
                });

                page.Content().PaddingTop(8).Column(column =>
                {
                    column.Item().Element(element => BuildDocuments(element, data.Documentos, culture));
                    column.Item().PaddingTop(8).Element(element => BuildTotals(
                        element,
                        totalGrossAmount,
                        totalAmountVat,
                        totalNetTaxableIncome,
                        header.MontoRetencion,
                        culture));

                    if (string.Equals(header.Status, "AN", StringComparison.OrdinalIgnoreCase))
                    {
                        column.Item().PaddingTop(8).AlignCenter().Text("REPORTE ANULADO").Bold().FontColor(Colors.Red.Darken2).FontSize(16);
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

    private static void BuildHeader(
        IContainer container,
        ReporteTimbreFiscalHeaderResponse header,
        byte[]? logoLeftBytes,
        byte[]? logoRightBytes)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn();
                columns.RelativeColumn();
                columns.RelativeColumn();
                columns.RelativeColumn();
            });

            table.Cell().Height(64).Element(element => AddLogo(element, logoLeftBytes, "LOGO"));
            table.Cell().Text(string.Empty);
            table.Cell().Text(string.Empty);
            table.Cell().Height(64).Element(element => AddLogo(element.AlignRight(), logoRightBytes, "LOGO"));

            EmptyHeaderCell(table, colSpan: 3);
            HeaderBoxCell(table, "FECHA DE ELABORACION", DateTime.Now.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture));

            EmptyHeaderCell(table, colSpan: 3);
            HeaderBoxCell(table, "ORDEN DE PAGO N", header.NumeroOrdenPago);

            table.Cell().Text(string.Empty);
            table.Cell().ColumnSpan(2).AlignCenter().Text("FORMATO N 2\nPLANILLA PARA EL CALCULO DEL IMPUESTO 1x1000\nAGENTES DE RETENCION\nENTES PUBLICOS")
                .Bold()
                .FontSize(10);
            table.Cell().Text(string.Empty);
        });
    }

    private static void BuildSubHeader(IContainer container, ReporteTimbreFiscalHeaderResponse header)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn();
            });

            InfoCell(table, "AGENTE DE RETENCION:", header.NombreAgenteRetencion);
            SpacerCell(table);
            InfoCell(table, "NUMERO DE RIF DEL AGENTE DE RETENCION:", FormatRif(header.RifAgenteRetencion));
            SpacerCell(table);
            InfoCell(table, "NOMBRE/RAZON SOCIAL DEL CONTRIBUYENTE:", header.NombreContribuyente);
            SpacerCell(table);
            InfoCell(table, "NUMERO DE RIF DEL CONTRIBUYENTE:", FormatRif(header.RifContribuyente));
            SpacerCell(table);
            InfoCell(table, "CONCEPTO DE LA ORDEN DE PAGO (AGREGAR TODA INFORMACION NECESARIA EN RELACION A LA FACTURA Y ORDEN DE PAGO):", header.Motivo, minHeight: 64);
        });
    }

    private static void BuildDocuments(IContainer container, IReadOnlyCollection<ReporteTimbreFiscalDocumentoResponse> documentos, CultureInfo culture)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn();
                columns.RelativeColumn();
                columns.RelativeColumn();
            });

            TableHeader(table, "N Control Factura");
            TableHeader(table, "N De Factura");
            TableHeader(table, "Monto");

            foreach (var item in documentos)
            {
                BodyCell(table, item.NumeroControlFactura);
                BodyCell(table, item.NumeroFactura);
                BodyCell(table, FormatAmount(item.MontoDocumento, culture), alignRight: true);
            }

            if (documentos.Count == 0)
            {
                table.Cell().ColumnSpan(3).Border(1).Padding(5).Text("Sin documentos registrados para el timbre fiscal.").FontSize(7);
            }
        });
    }

    private static void BuildTotals(
        IContainer container,
        decimal totalGrossAmount,
        decimal totalAmountVat,
        decimal totalNetTaxableIncome,
        decimal withholdingAmount,
        CultureInfo culture)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(60);
                columns.RelativeColumn();
                columns.ConstantColumn(110);
                columns.ConstantColumn(70);
            });

            table.Cell().ColumnSpan(4).BorderTop(1).BorderLeft(1).BorderRight(1).Padding(4).Text("CALCULO DEL IMPUESTO 1x1000:").Bold().FontSize(8);
            table.Cell().ColumnSpan(4).BorderLeft(1).BorderRight(1).Height(16);

            TotalRow(table, "MONTO BRUTO (MONTO TOTAL DE LA ORDEN DE PAGO)", FormatAmount(totalGrossAmount, culture));
            TotalRow(table, "MONTO DEL I.V.A. 16%", FormatAmount(totalAmountVat, culture));
            TotalRow(table, "MONTO NETO GRAVABLE", FormatAmount(totalNetTaxableIncome, culture));

            table.Cell().ColumnSpan(4).BorderLeft(1).BorderRight(1).Height(16);
            TotalRow(table, "IMPUESTO (1x1000) A RETENER:", FormatAmount(withholdingAmount, culture), isLast: true);
        });
    }

    private static void TotalRow(TableDescriptor table, string label, string value, bool isLast = false)
    {
        table.Cell().BorderLeft(1).Padding(2).Text(string.Empty);
        table.Cell().Padding(3).Text(label).Bold().FontSize(7.2f);
        table.Cell().Border(1).Padding(3).AlignRight().Text(value).FontSize(7.2f);
        var lastCell = table.Cell().BorderRight(1).Padding(2);
        if (isLast)
        {
            lastCell = lastCell.BorderBottom(1);
        }
        lastCell.Text(string.Empty);
    }

    private static void AddLogo(IContainer container, byte[]? logoBytes, string fallback)
    {
        if (logoBytes is not null)
        {
            container.Image(logoBytes).FitArea();
            return;
        }

        container.Text(fallback).Bold();
    }

    private static void HeaderBoxCell(TableDescriptor table, string label, string value)
    {
        table.Cell().Border(1).Padding(4).AlignCenter().Text(text =>
        {
            text.Span($"{label}:\n").Bold();
            text.Span(value);
        });
    }

    private static void EmptyHeaderCell(TableDescriptor table, uint colSpan = 1)
    {
        var tableCell = colSpan > 1 ? table.Cell().ColumnSpan(colSpan) : table.Cell();
        tableCell.Text(string.Empty);
    }

    private static void InfoCell(TableDescriptor table, string label, string value, float minHeight = 28)
    {
        table.Cell().Border(1).MinHeight(minHeight).Padding(5).Column(column =>
        {
            column.Item().Text(label).Bold().FontSize(7);
            column.Item().Text(value).FontSize(8);
        });
    }

    private static void SpacerCell(TableDescriptor table)
    {
        table.Cell().Height(6).Text(string.Empty);
    }

    private static void TableHeader(TableDescriptor table, string text)
    {
        table.Cell().Background(Colors.Grey.Lighten2).Border(1).Padding(4).AlignCenter().Text(text).Bold().FontSize(7);
    }

    private static void BodyCell(TableDescriptor table, string text, bool alignRight = false)
    {
        var cell = table.Cell().Border(1).Padding(4);
        if (alignRight)
        {
            cell = cell.AlignRight();
        }

        cell.Text(text).FontSize(7);
    }

    private static decimal CalculateTaxableIncome(decimal taxBase, decimal totalGrossAmount, decimal totalTaxExempt, decimal totalAmountVat)
    {
        if (taxBase != 0)
        {
            return taxBase;
        }

        return totalGrossAmount == totalTaxExempt
            ? totalGrossAmount
            : totalGrossAmount - totalAmountVat;
    }

    private static string FormatAmount(decimal value, CultureInfo culture)
    {
        return value.ToString("N2", culture);
    }

    private static string FormatRif(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var cleanValue = value.Replace("-", string.Empty).Trim();
        if (cleanValue.Length <= 1)
        {
            return cleanValue;
        }

        var first = cleanValue[..1];
        var rest = cleanValue[1..].PadLeft(9, '0');
        return $"{first}-{rest}";
    }

    private static byte[]? TryReadReportAsset(IWebHostEnvironment environment, string fileName)
    {
        var path = Path.Combine(environment.ContentRootPath, "Assets", "Reports", fileName);
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }
}

[ApiController]
[Route("api/ReporteTimbreFiscal")]
public class ReporteTimbreFiscalPdfController(ConnectionDB _connectionDB, IWebHostEnvironment _environment) : ControllerBase
{
    [HttpPost]
    [Route("pdf")]
    public async Task<IActionResult> Pdf(ReporteTimbreFiscalPdfQuery value)
    {
        var handler = new ReporteTimbreFiscalGetByCodigoHandler(_connectionDB);
        var result = await handler.HandleAsync(new ReporteTimbreFiscalGetByCodigoQuery(value.CodigoOrdenPago));

        if (!result.IsValid || result.Data is null)
        {
            return BadRequest(result);
        }

        var pdf = ReporteTimbreFiscalPdfGenerator.Generate(result.Data, _environment);
        var fileName = $"timbre-fiscal-{value.CodigoOrdenPago}.pdf";

        Response.Headers.ContentDisposition = $"inline; filename=\"{fileName}\"";
        return File(pdf, "application/pdf");
    }
}
