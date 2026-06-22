using Microsoft.AspNetCore.Mvc;
using OssmmasoftVerticalSlice.ContextDB;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Globalization;

namespace OssmmasoftVerticalSlice.Features.ReporteOrdenPago;

public record ReporteOrdenPagoPdfQuery(int CodigoOrdenPago);

public static class ReporteOrdenPagoPdfGenerator
{
    public static byte[] Generate(ReporteOrdenPagoResponse data, IWebHostEnvironment environment)
    {
        QuestPDF.Settings.License = LicenseType.Evaluation;

        var logoBytes = TryReadReportAsset(environment, "logoLeft.jpeg");
        var culture = CultureInfo.GetCultureInfo("es-VE");
        var header = data.Header ?? throw new InvalidOperationException("La orden de pago no contiene cabecera.");
        var totalOrden = data.Fondos.Sum(item => item.Monto);
        var totalRetenciones = data.Retenciones.Sum(item => item.MontoRetencion);
        var montoPagar = totalOrden - totalRetenciones;

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
                    column.Item().Element(element => BuildSubHeader(element, header));
                    column.Item().PaddingTop(8).Element(element => BuildFondos(element, data.Fondos, totalOrden, culture));
                    column.Item().PaddingTop(6).Element(element => BuildMotivo(element, header.Motivo));
                    column.Item().PaddingTop(6).Element(element => BuildRetenciones(element, data.Retenciones, totalOrden, totalRetenciones, montoPagar, culture));

                    if (string.Equals(header.Status, "AN", StringComparison.OrdinalIgnoreCase))
                    {
                        column.Item().PaddingTop(8).AlignCenter().Text("ORDEN ANULADA").Bold().FontColor(Colors.Red.Darken2).FontSize(16);
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

    private static void BuildHeader(IContainer container, ReporteOrdenPagoHeaderResponse header, byte[]? logoBytes)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(2.2f);
                columns.RelativeColumn();
                columns.RelativeColumn();
                columns.RelativeColumn();
            });

            table.Cell().RowSpan(2).Border(1).Padding(4).Column(column =>
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

                column.Item().PaddingTop(4).Text(string.IsNullOrWhiteSpace(header.TituloReporte) ? "ORDEN DE PAGO" : header.TituloReporte).Bold().FontSize(11);
            });

            HeaderInfoCell(table, "TIPO DE ORDEN", header.TipoOrdenPago);
            HeaderInfoCell(table, "ORDEN DE PAGO #", header.NumeroOrdenPago);
            HeaderInfoCell(table, "FECHA ORDEN DE PAGO", FormatDate(header.FechaOrdenPago));

            HeaderInfoCell(table, "NUMERO COMPROMISO #", header.NumeroCompromiso, colSpan: 2);
            HeaderInfoCell(table, "FECHA COMPROMISO", FormatDate(header.FechaCompromiso));
        });
    }

    private static void BuildSubHeader(IContainer container, ReporteOrdenPagoHeaderResponse header)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.RelativeColumn(2);
                columns.RelativeColumn();
                columns.RelativeColumn();
                columns.RelativeColumn(2);
            });

            InfoCell(table, "NOMBRE APELLIDO O RAZON SOCIAL DEL PROVEEDOR", header.NombreProveedor, colSpan: 3);
            InfoCell(table, "CEDULA O RIF", string.IsNullOrWhiteSpace(header.RifProveedor) ? header.CedulaProveedor : header.RifProveedor);
            InfoCell(table, "APELLIDOS Y NOMBRES", $"{header.NombreBeneficiario} {header.ApellidoBeneficiario}".Trim(), colSpan: 2);
            InfoCell(table, "CEDULA", header.CedulaBeneficiario);
            InfoCell(table, "PLAZO DE PAGO", $"{FormatDate(header.FechaPlazoDesde)} - {FormatDate(header.FechaPlazoHasta)}");
            InfoCell(table, "NRO DE PAGO", FormatDecimal(header.CantidadPago));
            InfoCell(table, "FORMA DE PAGO", header.FormaPago);
            InfoCell(table, "UNICO O PERIODICO (BOLIVARES EN LETRAS)", header.MontoLetras, colSpan: 2);
        });
    }

    private static void BuildFondos(IContainer container, IReadOnlyCollection<ReporteOrdenPagoFondoResponse> fondos, decimal totalOrden, CultureInfo culture)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(38);
                columns.RelativeColumn(2.2f);
                columns.RelativeColumn();
                columns.RelativeColumn();
                columns.ConstantColumn(72);
                columns.ConstantColumn(72);
            });

            TableHeader(table, "ANO");
            TableHeader(table, "FONDO");
            TableHeader(table, "CODIGO ICP");
            TableHeader(table, "CODIGO PUC");
            TableHeader(table, "PAGO UNICO");
            TableHeader(table, "PAGO ANUAL");

            foreach (var item in fondos)
            {
                BodyCell(table, item.Ano == 0 ? string.Empty : item.Ano.ToString(CultureInfo.InvariantCulture));
                BodyCell(table, item.DescripcionFinanciado);
                BodyCell(table, item.CodigoIcpConcat);
                BodyCell(table, item.CodigoPucConcat);
                BodyCell(table, FormatAmount(item.Monto, culture), alignRight: true);
                BodyCell(table, FormatAmount(item.Monto, culture), alignRight: true);
            }

            if (fondos.Count == 0)
            {
                BodyCell(table, string.Empty);
                BodyCell(table, "Sin imputaciones registradas");
                BodyCell(table, string.Empty);
                BodyCell(table, string.Empty);
                BodyCell(table, string.Empty);
                BodyCell(table, string.Empty);
            }

            TotalCell(table, "TOTAL", colSpan: 4);
            TotalCell(table, FormatAmount(totalOrden, culture), alignRight: true);
            TotalCell(table, FormatAmount(totalOrden, culture), alignRight: true);

            var tituloEspecifica = fondos.LastOrDefault()?.DenominacionPuc ?? string.Empty;
            TotalCell(table, "TITULO DE LA ESPECIFICA", colSpan: 2);
            TotalCell(table, tituloEspecifica, colSpan: 4);
        });
    }

    private static void BuildMotivo(IContainer container, string motivo)
    {
        container.Border(1).Padding(4).Column(column =>
        {
            column.Item().Text("MOTIVO").Bold();
            column.Item().PaddingTop(2).Text(motivo);
        });
    }

    private static void BuildRetenciones(
        IContainer container,
        IReadOnlyCollection<ReporteOrdenPagoRetencionResponse> retenciones,
        decimal totalOrden,
        decimal totalRetenciones,
        decimal montoPagar,
        CultureInfo culture)
    {
        container.Table(table =>
        {
            table.ColumnsDefinition(columns =>
            {
                columns.ConstantColumn(100);
                columns.RelativeColumn();
                columns.ConstantColumn(100);
                columns.ConstantColumn(100);
            });

            TableHeader(table, "TOTAL ORDEN PAGO");
            TableHeader(table, "TOTAL RETENCIONES");
            TableHeader(table, "MONTO RETENIDO");
            TableHeader(table, "MONTO A PAGAR");

            if (retenciones.Count == 0)
            {
                BodyCell(table, FormatAmount(totalOrden, culture), alignRight: true);
                BodyCell(table, "Sin retenciones");
                BodyCell(table, FormatAmount(0, culture), alignRight: true);
                BodyCell(table, FormatAmount(montoPagar, culture), alignRight: true);
            }
            else
            {
                var first = true;
                foreach (var item in retenciones)
                {
                    BodyCell(table, first ? FormatAmount(totalOrden, culture) : string.Empty, alignRight: true);
                    BodyCell(table, $"{FormatDecimal(item.PorRetencion)}% {item.Descripcion}".Trim());
                    BodyCell(table, FormatAmount(item.MontoRetencion, culture), alignRight: true);
                    BodyCell(table, first ? FormatAmount(montoPagar, culture) : string.Empty, alignRight: true);
                    first = false;
                }
            }

            TotalCell(table, string.Empty);
            TotalCell(table, "TOTAL RETENIDO", alignRight: true);
            TotalCell(table, FormatAmount(totalRetenciones, culture), alignRight: true);
            TotalCell(table, FormatAmount(montoPagar, culture), alignRight: true);
        });
    }

    private static void HeaderInfoCell(TableDescriptor table, string label, string value, uint colSpan = 1)
    {
        var tableCell = colSpan > 1 ? table.Cell().ColumnSpan(colSpan) : table.Cell();
        var cell = tableCell.Border(1).Padding(4);

        cell.Column(column =>
        {
            column.Item().Text(label).Bold().FontSize(6.5f);
            column.Item().Text(value).FontSize(7.5f);
        });
    }

    private static void InfoCell(TableDescriptor table, string label, string value, uint colSpan = 1)
    {
        var tableCell = colSpan > 1 ? table.Cell().ColumnSpan(colSpan) : table.Cell();
        var cell = tableCell.Border(1).Padding(4);

        cell.Column(column =>
        {
            column.Item().Text(label).Bold().FontSize(6.2f);
            column.Item().Text(value).FontSize(7.2f);
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

    private static void TotalCell(TableDescriptor table, string text, bool alignRight = false, uint colSpan = 1)
    {
        var tableCell = colSpan > 1 ? table.Cell().ColumnSpan(colSpan) : table.Cell();
        var cell = tableCell.Border(1).Background(Colors.Grey.Lighten3).Padding(3);

        if (alignRight)
        {
            cell = cell.AlignRight();
        }

        cell.Text(text).Bold().FontSize(6.8f);
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
[Route("api/ReporteOrdenPago")]
public class ReporteOrdenPagoPdfController(ConnectionDB _connectionDB, IWebHostEnvironment _environment) : ControllerBase
{
    [HttpPost]
    [Route("pdf")]
    public async Task<IActionResult> Pdf(ReporteOrdenPagoPdfQuery value)
    {
        var handler = new ReporteOrdenPagoGetByCodigoHandler(_connectionDB);
        var result = await handler.HandleAsync(new ReporteOrdenPagoGetByCodigoQuery(value.CodigoOrdenPago));

        if (!result.IsValid || result.Data is null)
        {
            return BadRequest(result);
        }

        var pdf = ReporteOrdenPagoPdfGenerator.Generate(result.Data, _environment);
        var fileName = $"orden-pago-{value.CodigoOrdenPago}.pdf";

        Response.Headers.ContentDisposition = $"inline; filename=\"{fileName}\"";
        return File(pdf, "application/pdf");
    }
}
