using Microsoft.AspNetCore.Mvc;
using OssmmasoftVerticalSlice.ContextDB;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;

namespace OssmmasoftVerticalSlice.Features.ReporteComprobanteIva;

public record ReporteComprobanteIvaPdfQuery(int CodigoOrdenPago);

public static class ReporteComprobanteIvaPdfGenerator
{
    public static byte[] Generate(ReporteComprobanteIvaResponse data, IWebHostEnvironment environment)
    {
        QuestPDF.Settings.License = LicenseType.Evaluation;

        var logoBytes = TryReadReportAsset(environment, "logoLeft.jpeg");
        var culture = CultureInfo.GetCultureInfo("es-VE");
        var header = data.Header ?? throw new InvalidOperationException("El comprobante IVA no contiene cabecera.");

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter.Landscape());
                page.Margin(12);
                page.DefaultTextStyle(style => style.FontSize(6.4f));

                page.Header().Column(column =>
                {
                    column.Item().Element(element => BuildHeader(element, header, logoBytes));
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

    private static void BuildHeader(IContainer container, ReporteComprobanteIvaHeaderResponse header, byte[]? logoBytes)
    {
        container.Row(row =>
        {
            row.ConstantItem(150).Column(column =>
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

            row.RelativeItem().AlignCenter().AlignMiddle().Text("COMPROBANTE DE RETENCION IVA").Bold().FontSize(13);
            row.ConstantItem(150);
        });
    }

    private static void BuildSubHeader(IContainer container, ReporteComprobanteIvaHeaderResponse header)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(2);
                columns.RelativeColumn(2);
                columns.RelativeColumn();
                columns.RelativeColumn();
            });

            EmptyCell(table, colSpan: 2);
            InfoCell(table, "No Comprobante", header.NumeroComprobante);
            InfoCell(table, "FECHA", FormatDate(header.Fecha));
            InfoCell(table, "Nombre o Razon Social del Agente de Retencion", header.NombreAgenteRetencion, colSpan: 2);
            InfoCell(table, "RIF del Agente de Retencion", FormatRif(header.RifAgenteRetencion));
            InfoCell(table, "Periodo Fiscal", FormatFiscalPeriod(header.Fecha));
            InfoCell(table, "Direccion Fiscal del Agente de Retencion", header.DireccionAgenteRetencion, colSpan: 4);
            InfoCell(table, "Nombre o Razon Social del Sujeto Retenido", header.NombreSujetoRetenido, colSpan: 2);
            InfoCell(table, "RIF del Sujeto Retenido", FormatRif(header.RifSujetoRetenido));
            InfoCell(table, "Nro. Orden Pago", header.NumeroOrdenPago);
        });
    }

    private static void BuildDocuments(IContainer container, IReadOnlyCollection<ReporteComprobanteIvaDocumentoResponse> documentos, CultureInfo culture)
    {
        var totalCompras = documentos.Sum(item => item.TotalComprasIncluyendoIva);
        var totalSinCredito = documentos.Sum(item => item.ComprasSinDerechoCredito);
        var totalBase = documentos.Sum(item => item.BaseImponible);
        var totalIva = documentos.Sum(item => item.ImpuestoIva);
        var totalRetenido = documentos.Sum(item => item.IvaRetenido);

        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(28);
                columns.ConstantColumn(48);
                columns.ConstantColumn(58);
                columns.ConstantColumn(58);
                columns.ConstantColumn(58);
                columns.ConstantColumn(58);
                columns.ConstantColumn(58);
                columns.ConstantColumn(58);
                columns.RelativeColumn();
                columns.RelativeColumn();
                columns.RelativeColumn();
                columns.ConstantColumn(45);
                columns.RelativeColumn();
                columns.RelativeColumn();
            });

            TableHeader(table, "No Oper.");
            TableHeader(table, "Fecha Factura");
            TableHeader(table, "No Factura");
            TableHeader(table, "No Control Factura");
            TableHeader(table, "No Nota Debito");
            TableHeader(table, "No Nota Credito");
            TableHeader(table, "Tipo Transaccion");
            TableHeader(table, "No Factura Afectada");
            TableHeader(table, "Total Compras Incl. IVA");
            TableHeader(table, "Compras sin Credito IVA");
            TableHeader(table, "Base Imponible");
            TableHeader(table, "% Alicuota");
            TableHeader(table, "Impuesto IVA");
            TableHeader(table, "IVA Retenido");

            foreach (var item in documentos)
            {
                BodyCell(table, item.NumeroOperacion.ToString(CultureInfo.InvariantCulture));
                BodyCell(table, FormatDate(item.FechaFactura));
                BodyCell(table, item.NumeroFactura);
                BodyCell(table, item.NumeroControlFactura);
                BodyCell(table, item.NumeroNotaDebito);
                BodyCell(table, item.NumeroNotaCredito);
                BodyCell(table, item.TipoTransaccion);
                BodyCell(table, item.NumeroFacturaAfectada);
                BodyCell(table, FormatAmount(item.TotalComprasIncluyendoIva, culture), alignRight: true);
                BodyCell(table, FormatAmount(item.ComprasSinDerechoCredito, culture), alignRight: true);
                BodyCell(table, FormatAmount(item.BaseImponible, culture), alignRight: true);
                BodyCell(table, item.Alicuota, alignRight: true);
                BodyCell(table, FormatAmount(item.ImpuestoIva, culture), alignRight: true);
                BodyCell(table, FormatAmount(item.IvaRetenido, culture), alignRight: true);
            }

            if (documentos.Count == 0)
            {
                var cell = table.Cell().ColumnSpan(14).Border(1).Padding(4);
                cell.Text("Sin documentos registrados para el comprobante IVA.").FontSize(7);
            }

            EmptyCell(table, colSpan: 6);
            TotalCell(table, "TOTALES", colSpan: 2, alignRight: true);
            TotalCell(table, FormatAmount(totalCompras, culture), alignRight: true);
            TotalCell(table, FormatAmount(totalSinCredito, culture), alignRight: true);
            TotalCell(table, FormatAmount(totalBase, culture), alignRight: true);
            TotalCell(table, string.Empty);
            TotalCell(table, FormatAmount(totalIva, culture), alignRight: true);
            TotalCell(table, FormatAmount(totalRetenido, culture), alignRight: true);
        });
    }

    private static void InfoCell(TableDescriptor table, string label, string value, uint colSpan = 1)
    {
        var tableCell = colSpan > 1 ? table.Cell().ColumnSpan(colSpan) : table.Cell();
        tableCell.Border(1).Padding(3).Column(column =>
        {
            column.Item().Text(label).Bold().FontSize(5.8f);
            column.Item().Text(value).FontSize(6.5f);
        });
    }

    private static void EmptyCell(TableDescriptor table, uint colSpan = 1)
    {
        var tableCell = colSpan > 1 ? table.Cell().ColumnSpan(colSpan) : table.Cell();
        tableCell.Border(1).Padding(3).Text(string.Empty);
    }

    private static void TableHeader(TableDescriptor table, string text)
    {
        table.Cell().Background(Colors.Grey.Lighten2).Border(1).Padding(2).AlignCenter().Text(text).Bold().FontSize(5.3f);
    }

    private static void BodyCell(TableDescriptor table, string text, bool alignRight = false)
    {
        var cell = table.Cell().Border(1).Padding(2);
        if (alignRight)
        {
            cell = cell.AlignRight();
        }

        cell.Text(text).FontSize(5.5f);
    }

    private static void TotalCell(TableDescriptor table, string text, bool alignRight = false, uint colSpan = 1)
    {
        var tableCell = colSpan > 1 ? table.Cell().ColumnSpan(colSpan) : table.Cell();
        var cell = tableCell.Border(1).Background(Colors.Grey.Lighten3).Padding(2);
        if (alignRight)
        {
            cell = cell.AlignRight();
        }

        cell.Text(text).Bold().FontSize(5.6f);
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

    private static string FormatRif(string value)
    {
        return value?.Trim() ?? string.Empty;
    }

    private static byte[]? TryReadReportAsset(IWebHostEnvironment environment, string fileName)
    {
        var path = Path.Combine(environment.ContentRootPath, "Assets", "Reports", fileName);
        return File.Exists(path) ? File.ReadAllBytes(path) : null;
    }
}

[ApiController]
[Route("api/ReporteComprobanteIva")]
public class ReporteComprobanteIvaPdfController(ConnectionDB _connectionDB, IWebHostEnvironment _environment) : ControllerBase
{
    [HttpPost]
    [Route("pdf")]
    public async Task<IActionResult> Pdf(ReporteComprobanteIvaPdfQuery value)
    {
        var handler = new ReporteComprobanteIvaGetByCodigoHandler(_connectionDB);
        var result = await handler.HandleAsync(new ReporteComprobanteIvaGetByCodigoQuery(value.CodigoOrdenPago));

        if (!result.IsValid || result.Data is null)
        {
            return BadRequest(result);
        }

        var pdf = ReporteComprobanteIvaPdfGenerator.Generate(result.Data, _environment);
        var fileName = $"comprobante-iva-{value.CodigoOrdenPago}.pdf";

        Response.Headers.ContentDisposition = $"inline; filename=\"{fileName}\"";
        return File(pdf, "application/pdf");
    }
}
